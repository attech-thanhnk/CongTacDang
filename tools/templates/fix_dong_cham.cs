#:package DocumentFormat.OpenXml@3.5.1
// Ô dòng chấm lấy được từ dữ liệu: gắn control để in tên/giá trị, trống thì giữ chữ mặc định.
// Mẫu 01: "Người quản lý trực tiếp … Chức vụ …" (MANAGER_NAME, MANAGER_POSITION), "Mã số cán bộ" (EMPLOYEE_CODE).
// Mẫu 02, 10: họ tên ô "Cấp có thẩm quyền duyệt" (APPROVER_NAME).
// Mẫu 04: ô "Hồ sơ, minh chứng kèm theo" (EVIDENCE) bỏ chữ hướng dẫn trong ngoặc vuông, để dòng chấm ghi tay.
// Mẫu 16: khối "Nơi nhận" thành một control RECEIVERS (nhiều dòng), thay cho MEMBERS_RECEIVER.
// Mẫu 06, 07, 08: dòng họ tên dưới ô ký Bí thư (SECRETARY_NAME), Mẫu 06 thêm ô Thủ trưởng đơn vị (HEAD_SIGN_NAME).
// Cách chạy: dotnet run tools/templates/fix_dong_cham.cs -- <thư mục Templates/Word>. Chạy lại an toàn (đã sửa thì bỏ qua).
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
const string Dots = "................................";
const string LongDots = "………………………………………";

Edit("Mau_01_PhieuGiaoNhiemVu.docx", body => { TagManager(body); TagEmployeeCode(body); });
Edit("Mau_02_TuDanhGia.docx", body => TagSignDots(body, "CẤP CÓ THẨM QUYỀN DUYỆT", "APPROVER_NAME"));
Edit("Mau_10_PhieuThamDinh.docx", body => TagSignDots(body, "CẤP CÓ THẨM QUYỀN DUYỆT", "APPROVER_NAME"));
Edit("Mau_04_PhieuDeNghiDieuChinh.docx", body => SetDefault(body, "EVIDENCE", LongDots + LongDots));
Edit("Mau_06_BaoCaoKetQuaTapThe.docx", body =>
{
    AddSignName(body, CellWithText(body, "THỦ TRƯỞNG ĐƠN VỊ"), "HEAD_SIGN_NAME");
    AddSignName(body, CellWithTag(body, "SIGN_ON_BEHALF"), "SECRETARY_NAME");
});
Edit("Mau_16_BaoCaoKetQuaDanhGia.docx", TagReceivers);
Edit("Mau_07_BaoCaoTuDanhGiaTapThe.docx", body => AddSignName(body, CellWithTag(body, "SIGN_ON_BEHALF"), "SECRETARY_NAME"));
Edit("Mau_08_TongHopKetQuaNhiemVu.docx", body => AddSignName(body, CellWithTag(body, "SIGN_ON_BEHALF"), "SECRETARY_NAME"));

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

string? TagOf(OpenXmlElement e) => (e as SdtElement)?.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

bool HasTag(Body body, string tag) => body.Descendants<SdtElement>().Any(s => TagOf(s) == tag);

int NextId(Body body) => body.Descendants<SdtId>().Select(i => i.Val?.Value ?? 0).DefaultIfEmpty(0).Max() + 1;

SdtRun SdtOf(Body body, string tag, Run content) =>
    new(new SdtProperties(new SdtAlias { Val = tag }, new Tag { Val = tag }, new SdtId { Val = NextId(body) }), new SdtContentRun(content));

void TagManager(Body body)
{
    if (HasTag(body, "MANAGER_NAME"))
        return;
    var paragraph = body.Descendants<Paragraph>().Single(p => p.InnerText.StartsWith("Người quản lý trực tiếp:", StringComparison.Ordinal));
    var runs = paragraph.Elements<Run>().ToList();
    var nameRun = runs.Single(r => r.InnerText == Dots);
    var positionRun = runs.Single(r => r.InnerText.Contains("Chức vụ:", StringComparison.Ordinal));

    var name = SdtOf(body, "MANAGER_NAME", (Run)nameRun.CloneNode(true));
    nameRun.InsertAfterSelf(name);
    nameRun.Remove();
    SplitLabel(body, positionRun, "Chức vụ: ", "MANAGER_POSITION", Array.Empty<Run>());
}

// "  Mã số cán bộ: ……" + run "……" → chữ nhãn giữ nguyên, toàn bộ dòng chấm vào một control.
void TagEmployeeCode(Body body)
{
    if (HasTag(body, "EMPLOYEE_CODE"))
        return;
    var paragraph = body.Descendants<Paragraph>().Single(p => p.InnerText.Contains("Mã số cán bộ:", StringComparison.Ordinal));
    var runs = paragraph.Elements<Run>().ToList();
    var labelRun = runs.Single(r => r.InnerText.Contains("Mã số cán bộ:", StringComparison.Ordinal));
    SplitLabel(body, labelRun, "Mã số cán bộ: ", "EMPLOYEE_CODE", runs.SkipWhile(r => r != labelRun).Skip(1).ToList());
}

// Tách run "<nhãn><dòng chấm>": nhãn ở lại, dòng chấm (gộp các run chấm theo sau) vào control.
void SplitLabel(Body body, Run labelRun, string label, string tag, IReadOnlyList<Run> trailingDots)
{
    var text = labelRun.InnerText;
    var cut = text.IndexOf(label, StringComparison.Ordinal) + label.Length;
    var dotsRun = (Run)labelRun.CloneNode(true);
    dotsRun.GetFirstChild<Text>()!.Text = text[cut..] + string.Concat(trailingDots.Select(r => r.InnerText));
    labelRun.GetFirstChild<Text>()!.Text = text[..cut];
    labelRun.GetFirstChild<Text>()!.Space = SpaceProcessingModeValues.Preserve;
    foreach (var run in trailingDots)
        run.Remove();
    labelRun.InsertAfterSelf(SdtOf(body, tag, dotsRun));
}

// Dòng chấm cuối ô ký (đã có sẵn) → control.
void TagSignDots(Body body, string label, string tag)
{
    if (HasTag(body, tag))
        return;
    var cell = body.Descendants<TableCell>().Single(c => c.Elements<Paragraph>().FirstOrDefault()?.InnerText.StartsWith(label, StringComparison.Ordinal) == true);
    var run = cell.Elements<Paragraph>().Last().Elements<Run>().Single(r => r.InnerText == Dots);
    run.InsertAfterSelf(SdtOf(body, tag, (Run)run.CloneNode(true)));
    run.Remove();
}

TableCell CellWithText(Body body, string text) =>
    body.Descendants<TableCell>().Single(c => c.InnerText.Contains(text, StringComparison.Ordinal));

TableCell CellWithTag(Body body, string tag) =>
    body.Descendants<TableCell>().Single(c => c.Descendants<SdtElement>().Any(s => TagOf(s) == tag));

// Ba dòng trống để ký rồi dòng họ tên (control, mặc định trống như trước), cùng căn lề và cỡ chữ với chức danh.
void AddSignName(Body body, TableCell cell, string tag)
{
    if (HasTag(body, tag))
        return;
    var heading = cell.Elements<Paragraph>().First();
    var pPr = heading.ParagraphProperties;
    var size = heading.Descendants<RunProperties>().FirstOrDefault()?.FontSize?.Val?.Value;
    Paragraph Empty()
    {
        var p = new Paragraph();
        if (pPr != null)
            p.AppendChild((ParagraphProperties)pPr.CloneNode(true));
        return p;
    }
    for (var i = 0; i < 3; i++)
        cell.AppendChild(Empty());
    var props = new RunProperties(new Bold());
    if (size != null)
        props.AppendChild(new FontSize { Val = size });
    var line = Empty();
    line.AppendChild(SdtOf(body, tag, new Run(props, new Text("") { Space = SpaceProcessingModeValues.Preserve })));
    cell.AppendChild(line);
}

// Các dòng sau "Nơi nhận:" gộp vào một đoạn có control; chữ mặc định giữ nguyên nội dung mẫu gốc, xuống dòng bằng ngắt dòng.
void TagReceivers(Body body)
{
    if (HasTag(body, "RECEIVERS"))
        return;
    var cell = body.Descendants<TableCell>().Single(c => c.Elements<Paragraph>().FirstOrDefault()?.InnerText == "Nơi nhận:");
    var lines = cell.Elements<Paragraph>().Skip(1).ToList();
    var first = lines[0];
    var runProps = first.Descendants<RunProperties>().FirstOrDefault();
    var run = new Run();
    if (runProps != null)
        run.AppendChild((RunProperties)runProps.CloneNode(true));
    for (var i = 0; i < lines.Count; i++)
    {
        if (i > 0)
            run.AppendChild(new Break());
        run.AppendChild(new Text(lines[i].InnerText) { Space = SpaceProcessingModeValues.Preserve });
    }
    var paragraph = new Paragraph();
    if (first.ParagraphProperties != null)
        paragraph.AppendChild((ParagraphProperties)first.ParagraphProperties.CloneNode(true));
    paragraph.AppendChild(SdtOf(body, "RECEIVERS", run));
    first.InsertBeforeSelf(paragraph);
    foreach (var line in lines)
        line.Remove();
}

void SetDefault(Body body, string tag, string text)
{
    var sdt = body.Descendants<SdtElement>().Single(s => TagOf(s) == tag);
    var texts = sdt.Descendants<Text>().ToList();
    if (texts.Count == 1 && texts[0].Text == text)
        return;
    texts[0].Text = text;
    foreach (var extra in texts.Skip(1))
        extra.Remove();
}
