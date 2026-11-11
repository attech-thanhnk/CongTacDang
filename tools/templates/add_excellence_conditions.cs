#:package DocumentFormat.OpenXml@3.5.1
// Gắn control điều kiện Hoàn thành xuất sắc: ô "Tự nhận xét điều kiện bắt buộc" Mẫu 09A (SELF_CONDITIONS);
// mục 4, 5 Mẫu 10 (SELF_EXCELLENT, APPRAISAL_EXCELLENT, SELF_CAP, APPRAISAL_CAP), bỏ chữ cứng "Không vi phạm".
// Cách chạy: dotnet run tools/templates/add_excellence_conditions.cs -- <thư mục Templates/Word>. Chạy lại an toàn.
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
const string Dots = "……………";

Edit("Mau_09A_PhieuTuCham.docx", body =>
{
    if (HasTag(body, "SELF_CONDITIONS"))
        return;
    var head = body.Descendants<TableRow>().Single(r => r.Elements<TableCell>().Any(c => c.InnerText.Trim() == "Tự nhận xét điều kiện bắt buộc"));
    var index = head.Elements<TableCell>().ToList().FindIndex(c => c.InnerText.Trim() == "Tự nhận xét điều kiện bắt buộc");
    var row = head.ElementsAfter().OfType<TableRow>().First();
    var cell = row.Elements<TableCell>().ElementAt(index);
    if (cell.InnerText.Trim().Length != 0)
        throw new InvalidOperationException("Ô tự nhận xét Mẫu 09A không trống như dự kiến.");
    // Cỡ chữ theo ô tỷ lệ vượt chuẩn bên trái.
    var props = row.Elements<TableCell>().ElementAt(index - 1).Descendants<RunProperties>().FirstOrDefault();
    var paragraph = cell.Elements<Paragraph>().First();
    paragraph.AppendChild(SdtOf(body, "SELF_CONDITIONS", TextRun(props, string.Empty)));
});

Edit("Mau_10_PhieuThamDinh.docx", body =>
{
    var excellent = Row(body, "4. Kiểm tra 5 Điều kiện Hoàn thành xuất sắc");
    WrapCell(body, excellent.Elements<TableCell>().ElementAt(1), "SELF_EXCELLENT", null);
    WrapCell(body, excellent.Elements<TableCell>().ElementAt(2), "APPRAISAL_EXCELLENT", null);
    // Ô in cả lý do: bỏ font ký hiệu của ô gốc để chữ theo font của bảng.
    PlainFont(body, "SELF_EXCELLENT");
    PlainFont(body, "APPRAISAL_EXCELLENT");
    var cap = Row(body, "5. Trần xếp loại");
    WrapCell(body, cap.Elements<TableCell>().ElementAt(1), "SELF_CAP", Dots);
    WrapCell(body, cap.Elements<TableCell>().ElementAt(2), "APPRAISAL_CAP", Dots);
});

void Edit(string file, Action<Body> change)
{
    using var doc = WordprocessingDocument.Open(Path.Combine(dir, file), true);
    var body = doc.MainDocumentPart!.Document!.Body!;
    var before = body.OuterXml;
    change(body);
    if (body.OuterXml == before)
    {
        Console.WriteLine(file + ": đã sửa — bỏ qua.");
        return;
    }
    doc.MainDocumentPart.Document.Save();
    Console.WriteLine(file + ": đã sửa.");
}

TableRow Row(Body body, string title) =>
    body.Descendants<TableRow>().Single(r => r.Elements<TableCell>().FirstOrDefault()?.InnerText.StartsWith(title, StringComparison.Ordinal) == true);

// Gom các run của đoạn đầu tiên vào control; text != null thì thay chữ mặc định.
void WrapCell(Body body, TableCell cell, string tag, string? text)
{
    if (cell.Descendants<SdtElement>().Any(s => TagOf(s) == tag))
        return;
    var paragraph = cell.Elements<Paragraph>().First();
    var runs = paragraph.Elements<Run>().ToList();
    if (runs.Count == 0)
        throw new InvalidOperationException($"Ô {tag} không có chữ như dự kiến.");
    if (text != null)
    {
        runs[0].RemoveAllChildren<Text>();
        runs[0].AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        foreach (var extra in runs.Skip(1))
            extra.Remove();
        runs = runs.Take(1).ToList();
    }
    var sdt = SdtOf(body, tag);
    runs[0].InsertBeforeSelf(sdt);
    foreach (var r in runs)
    {
        r.Remove();
        sdt.SdtContentRun!.AppendChild(r);
    }
}

void PlainFont(Body body, string tag)
{
    var sdt = body.Descendants<SdtElement>().Single(s => TagOf(s) == tag);
    foreach (var fonts in sdt.Descendants<RunFonts>().ToList())
        fonts.Remove();
}

string? TagOf(OpenXmlElement e) => (e as SdtElement)?.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

bool HasTag(Body body, string tag) => body.Descendants<SdtElement>().Any(s => TagOf(s) == tag);

int NextId(Body body) => body.Descendants<SdtId>().Select(i => i.Val?.Value ?? 0).DefaultIfEmpty(0).Max() + 1;

SdtRun SdtOf(Body body, string tag, params OpenXmlElement[] content)
{
    var sdt = new SdtRun(new SdtProperties(new SdtAlias { Val = tag }, new Tag { Val = tag }, new SdtId { Val = NextId(body) }), new SdtContentRun());
    foreach (var c in content)
        sdt.SdtContentRun!.AppendChild(c);
    return sdt;
}

Run TextRun(RunProperties? props, string text)
{
    var run = new Run();
    if (props != null)
        run.AppendChild((RunProperties)props.CloneNode(true));
    run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    return run;
}
