using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CongTacDang.Infrastructure.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CongTacDang.Infrastructure.Services;

/// <summary>Kết quả điền template Word.</summary>
public sealed class DocxRenderResult
{
    public DocxRenderResult(byte[] content, IReadOnlyList<string> missingTags)
    {
        Content = content;
        MissingTags = missingTags;
    }

    /// <summary>Nội dung tệp .docx đã điền.</summary>
    public byte[] Content { get; }

    /// <summary>Tag có trong template nhưng không có trong dữ liệu (giữ nguyên chữ mặc định của template).</summary>
    public IReadOnlyList<string> MissingTags { get; }
}

/// <summary>
/// Bộ điền template Word dựa trên Content Control (SDT) của OpenXML — không tìm/thay chuỗi, nên không bị ảnh hưởng
/// khi Word tách chữ thành nhiều run.
/// <list type="bullet">
/// <item><c>TAG</c>: trường đơn — nội dung control được thay bằng giá trị (giữ định dạng của run đầu tiên);
/// giá trị null giữ nguyên chữ mặc định trong template.</item>
/// <item><c>repeat:TÊN</c>: khối lặp — nội dung control (một hay nhiều dòng bảng / đoạn văn) được nhân bản cho mỗi phần tử
/// của danh sách TÊN; Tag bên trong tra theo phần tử trước, sau đó tới dữ liệu cha.</item>
/// <item><c>if:TÊN</c> / <c>ifnot:TÊN</c>: khối điều kiện — giữ nội dung khi điều kiện đúng / sai, ngược lại bỏ đi.</item>
/// </list>
/// Sau khi điền, mọi control có Tag được gỡ bỏ (chỉ giữ nội dung); control không có Tag (ví dụ số trang) giữ nguyên.
/// </summary>
public static class DocxTemplateEngine
{
    /// <summary>Tiền tố Tag của khối lặp.</summary>
    public const string RepeatPrefix = "repeat:";

    /// <summary>Tiền tố Tag của khối hiện khi điều kiện đúng.</summary>
    public const string IfPrefix = "if:";

    /// <summary>Tiền tố Tag của khối hiện khi điều kiện sai.</summary>
    public const string IfNotPrefix = "ifnot:";

    private const string Word2010Namespace = "http://schemas.microsoft.com/office/word/2010/wordml";

    /// <summary>Điền dữ liệu vào template (.docx) và trả về tài liệu mới; template gốc không bị thay đổi.</summary>
    public static DocxRenderResult Render(byte[] template, TemplateData data)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(data);

        var missing = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        using var ms = new MemoryStream();
        ms.Write(template, 0, template.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var mainPart = doc.MainDocumentPart
                ?? throw new InvalidDataException("Template Word không có phần nội dung chính.");
            var renderer = new Renderer(missing);
            var root = new Scope(data, null);

            foreach (var element in PartRoots(mainPart))
            {
                renderer.Process(element, root);
                EnsureValidTableCells(element);
            }

            foreach (var element in PartRoots(mainPart))
            {
                if (element is OpenXmlPartRootElement partRoot)
                    partRoot.Save();
            }
        }

        return new DocxRenderResult(ms.ToArray(), missing.ToList());
    }

    /// <summary>Liệt kê Tag của mọi Content Control có Tag trong template (phục vụ kiểm tra template và tài liệu).</summary>
    public static IReadOnlyList<string> GetTags(byte[] template)
    {
        using var ms = new MemoryStream(template, writable: false);
        using var doc = WordprocessingDocument.Open(ms, false);
        var mainPart = doc.MainDocumentPart
            ?? throw new InvalidDataException("Template Word không có phần nội dung chính.");

        return PartRoots(mainPart)
            .SelectMany(root => root.Descendants<SdtElement>())
            .Select(GetTag)
            .Where(tag => !string.IsNullOrEmpty(tag))
            .Select(tag => tag!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Phần thân tài liệu, header và footer.</summary>
    private static IEnumerable<OpenXmlElement> PartRoots(MainDocumentPart mainPart)
    {
        if (mainPart.Document != null)
            yield return mainPart.Document;
        foreach (var header in mainPart.HeaderParts)
        {
            if (header.Header != null)
                yield return header.Header;
        }
        foreach (var footer in mainPart.FooterParts)
        {
            if (footer.Footer != null)
                yield return footer.Footer;
        }
    }

    private static string? GetTag(SdtElement sdt) =>
        sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value?.Trim();

    /// <summary>Mỗi ô bảng phải kết thúc bằng một đoạn văn (khối điều kiện có thể làm ô rỗng).</summary>
    private static void EnsureValidTableCells(OpenXmlElement root)
    {
        foreach (var cell in root.Descendants<TableCell>().ToList())
        {
            if (cell.LastChild is not Paragraph)
                cell.AppendChild(new Paragraph());
        }
    }

    /// <summary>Phạm vi tra cứu dữ liệu: phần tử lặp hiện tại, sau đó tới dữ liệu cha.</summary>
    private sealed class Scope
    {
        private readonly TemplateData _data;
        private readonly Scope? _parent;

        public Scope(TemplateData data, Scope? parent)
        {
            _data = data;
            _parent = parent;
        }

        public bool TryGetField(string tag, out string? value)
        {
            if (_data.Fields.TryGetValue(tag, out value))
                return true;
            if (_parent != null)
                return _parent.TryGetField(tag, out value);
            value = null;
            return false;
        }

        public bool TryGetCondition(string tag, out bool value)
        {
            if (_data.Conditions.TryGetValue(tag, out value))
                return true;
            if (_parent != null)
                return _parent.TryGetCondition(tag, out value);
            value = false;
            return false;
        }

        public bool TryGetCollection(string tag, out List<TemplateData> items)
        {
            if (_data.Collections.TryGetValue(tag, out var found))
            {
                items = found;
                return true;
            }
            if (_parent != null)
                return _parent.TryGetCollection(tag, out items);
            items = new List<TemplateData>();
            return false;
        }
    }

    private sealed class Renderer
    {
        private readonly ISet<string> _missing;

        public Renderer(ISet<string> missing) => _missing = missing;

        /// <summary>Xử lý các phần tử con của <paramref name="parent"/>; control có Tag được thay bằng nội dung đã điền.</summary>
        public void Process(OpenXmlElement parent, Scope scope)
        {
            foreach (var child in parent.ChildElements.ToList())
            {
                if (child is SdtElement sdt && GetTag(sdt) is { Length: > 0 } tag)
                {
                    foreach (var replacement in Render(sdt, tag, scope))
                        sdt.InsertBeforeSelf(replacement);
                    sdt.Remove();
                }
                else
                {
                    Process(child, scope);
                }
            }
        }

        private List<OpenXmlElement> Render(SdtElement sdt, string tag, Scope scope)
        {
            var content = sdt.ChildElements.FirstOrDefault(e => e.LocalName == "sdtContent");
            var children = content?.ChildElements.ToList() ?? new List<OpenXmlElement>();

            if (tag.StartsWith(RepeatPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var name = tag[RepeatPrefix.Length..].Trim();
                if (!scope.TryGetCollection(name, out var items))
                    _missing.Add(tag);

                var result = new List<OpenXmlElement>();
                foreach (var item in items)
                {
                    var clones = children.Select(c => StripParagraphIds(c.CloneNode(true))).ToList();
                    result.AddRange(ProcessDetached(clones, new Scope(item, scope)));
                }
                return result;
            }

            var isIf = tag.StartsWith(IfPrefix, StringComparison.OrdinalIgnoreCase);
            var isIfNot = tag.StartsWith(IfNotPrefix, StringComparison.OrdinalIgnoreCase);
            if (isIf || isIfNot)
            {
                var name = tag[(isIf ? IfPrefix.Length : IfNotPrefix.Length)..].Trim();
                if (!scope.TryGetCondition(name, out var condition))
                    _missing.Add(tag);

                if (condition != isIf)
                    return new List<OpenXmlElement>();
                return ProcessDetached(Detach(children), scope);
            }

            if (!scope.TryGetField(tag, out var value))
                _missing.Add(tag);

            var detached = Detach(children);
            if (value != null)
                detached = SetText(detached, value);
            return ProcessDetached(detached, scope);
        }

        /// <summary>Xử lý danh sách phần tử đã tách khỏi cây (bản sao của khối lặp hoặc nội dung control).</summary>
        private List<OpenXmlElement> ProcessDetached(List<OpenXmlElement> elements, Scope scope)
        {
            var result = new List<OpenXmlElement>();
            foreach (var element in elements)
            {
                if (element is SdtElement sdt && GetTag(sdt) is { Length: > 0 } tag)
                {
                    result.AddRange(Render(sdt, tag, scope));
                }
                else
                {
                    Process(element, scope);
                    result.Add(element);
                }
            }
            return result;
        }

        private static List<OpenXmlElement> Detach(List<OpenXmlElement> elements)
        {
            foreach (var element in elements)
                element.Remove();
            return elements;
        }

        /// <summary>
        /// Thay nội dung control bằng <paramref name="value"/>: giữ run đầu tiên (và định dạng của nó), bỏ các run còn lại.
        /// Xuống dòng trong giá trị được chuyển thành ngắt dòng.
        /// </summary>
        private static List<OpenXmlElement> SetText(List<OpenXmlElement> elements, string value)
        {
            var runs = elements
                .SelectMany(e => e is Run run ? new[] { run } : e.Descendants<Run>())
                .ToList();

            Run target;
            if (runs.Count > 0)
            {
                target = runs[0];
            }
            else
            {
                target = new Run();
                var paragraph = elements
                    .SelectMany(e => e is Paragraph p ? new[] { p } : e.Descendants<Paragraph>())
                    .FirstOrDefault();
                if (paragraph != null)
                    paragraph.AppendChild(target);
                else if (elements.Count == 0 || elements.All(e => e is Run))
                    elements.Add(target);
                else
                    throw new InvalidDataException("Không xác định được vị trí điền dữ liệu trong Content Control.");
            }

            foreach (var child in target.ChildElements.Where(c => c is not RunProperties).ToList())
                child.Remove();

            var lines = value.Replace("\r\n", "\n").Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    target.AppendChild(new Break());
                target.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            }

            var targetParagraph = target.Ancestors<Paragraph>().FirstOrDefault();
            foreach (var run in runs.Skip(1))
            {
                if (run.Parent != null)
                    run.Remove();
                elements.Remove(run);
            }

            // Đoạn văn phụ của placeholder nhiều đoạn: bỏ nếu đã rỗng.
            elements.RemoveAll(e => e is Paragraph p && p != targetParagraph && !p.Descendants<Run>().Any());
            return elements;
        }

        /// <summary>Bỏ w14:paraId/w14:textId trên bản sao để không trùng định danh đoạn văn.</summary>
        private static OpenXmlElement StripParagraphIds(OpenXmlElement element)
        {
            foreach (var node in element.Descendants().Prepend(element))
            {
                foreach (var attribute in node.GetAttributes()
                    .Where(a => a.NamespaceUri == Word2010Namespace && a.LocalName is "paraId" or "textId")
                    .ToList())
                {
                    node.RemoveAttribute(attribute.LocalName, attribute.NamespaceUri);
                }
            }
            return element;
        }
    }
}
