using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Infrastructure.Services;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace CongTacDang.Infrastructure.Documents;

/// <summary>
/// Kiểm tra file mẫu Word trước khi dùng (task 17 — T-81):
/// <list type="number">
/// <item>Mở được bằng OpenXML và là tài liệu Word thường (.docx, không phải mẫu/macro).</item>
/// <item>Tag không nhận diện (không thuộc lớp dữ liệu của mẫu, không phải tag thông tin đơn vị) → <b>lỗi</b>.</item>
/// <item>Tag bắt buộc (khai báo trên lớp dữ liệu) bị thiếu → <b>cảnh báo</b>; khối điều kiện chỉ cần <c>if:</c> hoặc <c>ifnot:</c>.</item>
/// <item>Sinh thử với dữ liệu giả: lỗi khi điền → <b>lỗi</b>; tài liệu sinh ra sai cấu trúc OpenXML → <b>cảnh báo</b>.</item>
/// </list>
/// </summary>
public static class WordTemplateValidator
{
    /// <summary>Số lỗi cấu trúc tối đa liệt kê trong cảnh báo.</summary>
    private const int MaxSchemaMessages = 5;

    /// <summary>Kiểm tra nội dung <paramref name="content"/> theo danh mục tag của <paramref name="form"/>.</summary>
    public static WordTemplateCheckDto Check(WordFormDefinition form, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(content);
        var result = new WordTemplateCheckDto();

        try
        {
            using var ms = new MemoryStream(content, writable: false);
            using var doc = WordprocessingDocument.Open(ms, false);
            if (doc.DocumentType != WordprocessingDocumentType.Document)
                result.Errors.Add("Tệp không phải tài liệu Word thường (.docx). Hãy lưu lại bằng Word với kiểu \"Word Document (*.docx)\".");
            else if (doc.MainDocumentPart?.Document?.Body == null)
                result.Errors.Add("Tệp Word không có phần nội dung chính.");
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or FileFormatException or IOException or InvalidOperationException)
        {
            result.Errors.Add("Không mở được tệp bằng OpenXML — tệp hỏng hoặc không phải tài liệu Word (.docx). Hãy mở và lưu lại bằng Word.");
        }

        if (result.Errors.Count > 0)
            return result;

        result.Tags = DocxTemplateEngine.GetTags(content).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();

        var known = form.FormTags.Select(t => t.Tag)
            .Concat(OrganizationTemplateFields.Tags.Select(t => t.Tag))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        result.UnknownTags = result.Tags.Where(t => !known.Contains(t)).ToList();
        foreach (var tag in result.UnknownTags)
            result.Errors.Add($"Tag \"{tag}\" không thuộc danh mục tag của {form.Name}. Hãy sửa hoặc xóa Content Control này (danh mục tag: docs/bieu-mau.md).");

        result.MissingRequiredTags = MissingRequired(form, result.Tags);
        foreach (var tag in result.MissingRequiredTags)
            result.Warnings.Add($"Thiếu tag bắt buộc \"{tag}\": dữ liệu tương ứng sẽ không xuất hiện trên biểu mẫu.");

        if (result.Errors.Count == 0)
            DryRun(form, content, result);

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    /// <summary>Tag bắt buộc của mẫu không có trong tệp (điều kiện: thiếu cả <c>if:</c> lẫn <c>ifnot:</c>).</summary>
    private static List<string> MissingRequired(WordFormDefinition form, IReadOnlyCollection<string> tags)
    {
        var present = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var tag in form.FormTags)
        {
            if (tag.Kind == TemplateTagCatalog.IfNotKind)
                continue;
            if (tag.Kind == TemplateTagCatalog.IfKind)
            {
                if (!present.Contains(tag.Tag) && !present.Contains(DocxTemplateEngine.IfNotPrefix + tag.ConditionName))
                    missing.Add($"{tag.Tag} / {DocxTemplateEngine.IfNotPrefix}{tag.ConditionName}");
                continue;
            }
            if (!present.Contains(tag.Tag))
                missing.Add(tag.Tag);
        }
        return missing;
    }

    /// <summary>Sinh thử với dữ liệu giả (mọi trường có giá trị, điều kiện đúng, danh sách hai phần tử).</summary>
    private static void DryRun(WordFormDefinition form, byte[] content, WordTemplateCheckDto result)
    {
        byte[] rendered;
        try
        {
            var data = TemplateTagCatalog.SampleData(form.DataType)
                .WithShared(TemplateTagCatalog.SampleData(typeof(OrganizationTemplateFields)));
            rendered = DocxTemplateEngine.Render(content, data).Content;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException or OpenXmlPackageException)
        {
            result.Errors.Add($"Sinh thử tài liệu với dữ liệu giả thất bại: {ex.Message} Hãy kiểm tra các Content Control (không lồng trường đơn vào nhau, khối lặp bao trọn dòng bảng).");
            return;
        }

        try
        {
            using var ms = new MemoryStream(rendered, writable: false);
            using var doc = WordprocessingDocument.Open(ms, false);
            var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).ToList();
            if (errors.Count > 0)
            {
                result.Warnings.Add($"Tài liệu sinh thử có {errors.Count} lỗi cấu trúc OpenXML (Word vẫn có thể mở được): "
                    + string.Join("; ", errors.Take(MaxSchemaMessages).Select(e => e.Description)));
            }
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or IOException)
        {
            result.Errors.Add("Tài liệu sinh thử không mở lại được. Hãy mở và lưu lại file mẫu bằng Word.");
        }
    }
}
