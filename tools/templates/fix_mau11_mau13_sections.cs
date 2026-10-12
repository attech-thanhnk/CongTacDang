#:package DocumentFormat.OpenXml@3.5.1
// Sửa template Mẫu 11, 13: chia hai phần I/II theo diện (if:SHOW_SUPERIOR / if:SHOW_BASE); Mẫu 11 thêm cột (5)–(7) và BASE_AUTHORITY.
// Cách chạy: dotnet run tools/templates/fix_mau11_mau13_sections.cs -- <thư mục Templates/Word> (sau build_mau13.py, fix_form_tags.py). Chạy lại an toàn (đã sửa thì bỏ qua).
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
var nextId = 921000;

FixMau11(Path.Combine(dir, "Mau_11_PhieuBoPhieuChiBo.docx"));
FixMau13(Path.Combine(dir, "Mau_13_BienBanKiemPhieu.docx"));

string? TagOf(OpenXmlElement e) => (e as SdtElement)?.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

SdtProperties Props(string tag) => new(new SdtAlias { Val = tag }, new Tag { Val = tag }, new SdtId { Val = nextId++ });

SdtRow WrapRows(string tag, params OpenXmlElement[] rows)
{
    var first = rows[0];
    var sdt = new SdtRow(Props(tag), new SdtContentRow());
    first.InsertBeforeSelf(sdt);
    foreach (var r in rows)
    {
        r.Remove();
        sdt.SdtContentRow!.AppendChild(r);
    }
    return sdt;
}

void StripIds(OpenXmlElement element)
{
    foreach (var node in element.Descendants().Prepend(element))
        foreach (var a in node.GetAttributes().Where(a => a.LocalName is "paraId" or "textId").ToList())
            node.RemoveAttribute(a.LocalName, a.NamespaceUri);
}

void FixMau11(string path)
{
    using var doc = WordprocessingDocument.Open(path, true);
    var body = doc.MainDocumentPart!.Document!.Body!;
    if (body.Descendants<SdtElement>().Any(s => TagOf(s) == "if:SHOW_SUPERIOR"))
    {
        Console.WriteLine("Mẫu 11 đã sửa — bỏ qua.");
        return;
    }
    var records = body.Descendants<SdtRow>().Single(s => TagOf(s) == "repeat:RECORDS");
    var table = records.Ancestors<Table>().First();
    var headI = table.Elements<TableRow>().Single(r => r.InnerText.Contains("BAN THƯỜNG VỤ ĐẢNG ỦY TCT"));
    var blank = (TableRow)headI.NextSibling()!;
    var headII = table.Elements<TableRow>().Single(r => r.InnerText.Contains("ĐẢNG ỦY/CHI ỦY CƠ SỞ"));
    if (blank.InnerText.Trim().Length != 0 || blank.NextSibling() != headII || headII.NextSibling() != records)
        throw new InvalidOperationException("Bố cục bảng Mẫu 11 không như dự kiến.");

    // Cột (5), (6), (7) của dòng lặp: control mới chép từ R_SELF_GRADE.
    var row = records.SdtContentRow!.Elements<TableRow>().Single();
    var cells = row.Elements<TableCell>().ToList();
    var selfSdt = cells[5].Descendants<SdtRun>().Single();
    foreach (var (index, tag) in new[] { (6, "R_COLLECTIVE_GRADE"), (7, "R_APPRAISAL_GRADE"), (8, "R_DIRECTOR_GRADE") })
    {
        var paragraph = cells[index].GetFirstChild<Paragraph>()!;
        foreach (var r in paragraph.Elements<Run>().ToList())
            r.Remove();
        var sdt = (SdtRun)selfSdt.CloneNode(true);
        sdt.SdtProperties = Props(tag);
        sdt.Descendants<Text>().Single().Text = "";
        paragraph.AppendChild(sdt);
    }

    // Dòng lặp mục II (BASE_ROWS) và mục I (SUPERIOR_ROWS — bản sao).
    records.SdtProperties = Props("repeat:BASE_ROWS");
    var superior = (SdtRow)records.CloneNode(true);
    StripIds(superior);
    superior.SdtProperties = Props("repeat:SUPERIOR_ROWS");
    blank.InsertBeforeSelf(superior);

    // Dòng trống khi mục không có cán bộ.
    var blankBase = (TableRow)blank.CloneNode(true);
    StripIds(blankBase);
    records.InsertAfterSelf(blankBase);
    var ifnotSuperior = WrapRows("ifnot:HAS_SUPERIOR_ROWS", blank);
    var ifnotBase = WrapRows("ifnot:HAS_BASE_ROWS", blankBase);

    // Tiêu đề mục II: "… CỦA " + control BASE_AUTHORITY ("ĐẢNG ỦY/CHI ỦY CƠ SỞ…").
    var titleRun = headII.Descendants<Run>().Single(r => r.InnerText.Contains("ĐẢNG ỦY/CHI ỦY CƠ SỞ"));
    var full = titleRun.InnerText;
    var cut = full.IndexOf("ĐẢNG ỦY/CHI ỦY CƠ SỞ", StringComparison.Ordinal);
    titleRun.GetFirstChild<Text>()!.Text = full[..cut];
    titleRun.GetFirstChild<Text>()!.Space = SpaceProcessingModeValues.Preserve;
    var authorityRun = (Run)titleRun.CloneNode(true);
    authorityRun.GetFirstChild<Text>()!.Text = full[cut..];
    titleRun.InsertAfterSelf(new SdtRun(Props("BASE_AUTHORITY"), new SdtContentRun(authorityRun)));

    WrapRows("if:SHOW_SUPERIOR", headI, superior, ifnotSuperior);
    WrapRows("if:SHOW_BASE", headII, records, ifnotBase);
    doc.MainDocumentPart.Document.Save();
    Console.WriteLine("Đã sửa Mẫu 11.");
}

void FixMau13(string path)
{
    using var doc = WordprocessingDocument.Open(path, true);
    var body = doc.MainDocumentPart!.Document!.Body!;
    if (body.Descendants<SdtElement>().Any(s => TagOf(s) == "if:SHOW_SUPERIOR"))
    {
        Console.WriteLine("Mẫu 13 đã sửa — bỏ qua.");
        return;
    }
    foreach (var (heading, repeat, empty, wrap) in new[]
    {
        ("CỦA BTVĐUTCT", "repeat:SUPERIOR_ROWS", "ifnot:HAS_SUPERIOR_ROWS", "if:SHOW_SUPERIOR"),
        ("XẾP LOẠI CỦA ", "repeat:BASE_ROWS", "ifnot:HAS_BASE_ROWS", "if:SHOW_BASE")
    })
    {
        var rep = body.Descendants<SdtRow>().Single(s => TagOf(s) == repeat);
        var head = rep.PreviousSibling() as TableRow ?? throw new InvalidOperationException("Không thấy tiêu đề mục trước " + repeat);
        if (!head.InnerText.Contains(heading))
            throw new InvalidOperationException("Tiêu đề mục không như dự kiến: " + head.InnerText);
        var empt = rep.NextSibling() as SdtRow;
        if (empt == null || TagOf(empt) != empty)
            throw new InvalidOperationException("Không thấy " + empty);
        WrapRows(wrap, head, rep, empt);
    }
    doc.MainDocumentPart.Document.Save();
    Console.WriteLine("Đã sửa Mẫu 13.");
}
