#:package DocumentFormat.OpenXml@3.5.1
// Bổ sung control cho template: họ tên người ký (Mẫu 02, 09A, 10, 05), tổng trọng số và đơn vị gốc (Mẫu 01, 02),
// dòng ngày Mẫu 11, nhãn chức vụ một dòng Mẫu 9D, căn cứ Hướng dẫn của Quyết định, dòng "Không có…" mục 2 Mẫu 20,
// chữ gợi ý "Đảng ủy / Chi ủy / Tập thể Lãnh đạo" Mẫu 06 chỉ in khi chưa có tên tập thể.
// Cách chạy: dotnet run tools/templates/fix_signers_totals.cs -- <thư mục Templates/Word>. Chạy lại an toàn (đã sửa thì bỏ qua).
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
const string Dots = "................................";

Edit("Mau_01_PhieuGiaoNhiemVu.docx", body =>
{
    WrapHeaderDepartment(body);
    TagTotalWeight(body);
});
Edit("Mau_02_TuDanhGia.docx", body =>
{
    WrapHeaderDepartment(body);
    TagTotalWeight(body);
    AddSignName(body, "CẤP CÓ THẨM QUYỀN DUYỆT", null);
    AddSignName(body, "ĐẠI DIỆN LÃNH ĐẠO ĐƠN VỊ", "UNIT_LEADER_NAME");
    AddSignName(body, "CÁ NHÂN TỰ ĐÁNH GIÁ", "SELF_NAME");
});
Edit("Mau_09A_PhieuTuCham.docx", body =>
{
    AddSignName(body, "XÁC NHẬN CỦA CHI BỘ", "CELL_CONFIRMER_NAME");
    AddSignName(body, "CÁ NHÂN TỰ ĐÁNH GIÁ", "SELF_NAME");
});
Edit("Mau_10_PhieuThamDinh.docx", body =>
{
    AddSignName(body, "CẤP CÓ THẨM QUYỀN DUYỆT", null);
    AddSignName(body, "NGƯỜI TRỰC TIẾP SỬ DỤNG", "SUPERVISOR_NAME");
    AddSignName(body, "ĐẠI DIỆN CƠ QUAN THẨM ĐỊNH", "APPRAISER_NAME");
});
Edit("Mau_05_PhieuNhiemVuDotXuat.docx", body => AddApproverSign(body));
Edit("Mau_9D_PhuLucKetQuaNhiemVu.docx", body => ReplaceText(body, "Chức vụ (Đảng, Đoàn thể, Chính quyền)", "Chức vụ (Đảng, Chính quyền, Đoàn thể)"));
Edit("Mau_11_PhieuBoPhieuChiBo.docx", body => TagMau11DateLine(body));
Edit("Mau_QD_QuyetDinhXepLoai.docx", body => FixGuideReference(body));
Edit("Mau_20_BienBanHieuChuan.docx", body => AddNoCasesRow(body));
Edit("Mau_06_BaoCaoKetQuaTapThe.docx", body => WrapSubjectPrefix(body));

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

SdtProperties Props(Body body, string tag) => new(new SdtAlias { Val = tag }, new Tag { Val = tag }, new SdtId { Val = NextId(body) });

SdtRun SdtOf(Body body, string tag, params OpenXmlElement[] content)
{
    var sdt = new SdtRun(Props(body, tag), new SdtContentRun());
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

// Tiêu đề trái: "<tên công ty>" + ngắt dòng + DEPARTMENT; đơn vị gốc (là chính công ty) thì bỏ phần sau.
void WrapHeaderDepartment(Body body)
{
    if (HasTag(body, "if:HAS_DEPARTMENT"))
        return;
    var company = body.Descendants<SdtRun>().First(s => TagOf(s) == "ORG_COMPANY_NAME_UPPER");
    var breakRun = company.NextSibling() as Run ?? throw new InvalidOperationException("Không thấy ngắt dòng sau tên công ty.");
    var department = breakRun.NextSibling() as SdtRun;
    if (breakRun.GetFirstChild<Break>() == null || department == null || TagOf(department) != "DEPARTMENT")
        throw new InvalidOperationException("Tiêu đề trái không như dự kiến.");
    var wrapper = new SdtRun(Props(body, "if:HAS_DEPARTMENT"), new SdtContentRun());
    company.InsertAfterSelf(wrapper);
    breakRun.Remove();
    department.Remove();
    wrapper.SdtContentRun!.Append(breakRun, department);
}

// Ô trọng số của dòng CỘNG đang là chữ cố định "70,0".
void TagTotalWeight(Body body)
{
    if (HasTag(body, "TOTAL_WEIGHT"))
        return;
    var row = body.Descendants<TableRow>().Single(r => r.Elements<TableCell>().FirstOrDefault()?.InnerText.Trim() == "CỘNG");
    var cell = row.Elements<TableCell>().Single(c => c.InnerText.Trim() == "70,0");
    var paragraph = cell.Elements<Paragraph>().Single();
    var runs = paragraph.Elements<Run>().ToList();
    var sdt = SdtOf(body, "TOTAL_WEIGHT");
    runs[0].InsertBeforeSelf(sdt);
    foreach (var r in runs)
    {
        r.Remove();
        sdt.SdtContentRun!.AppendChild(r);
    }
}

// Ô ký chỉ có tiêu đề: thêm 3 dòng trống và dòng họ tên (control khi có tag, không thì dòng chấm ký tay) như Mẫu 01.
void AddSignName(Body body, string label, string? tag)
{
    var cell = body.Descendants<TableCell>().Single(c => c.Elements<Paragraph>().FirstOrDefault()?.InnerText.StartsWith(label, StringComparison.Ordinal) == true);
    if (cell.Elements<Paragraph>().Count() > 1)
        return;
    var heading = cell.Elements<Paragraph>().Single();
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
    var nameProps = size != null ? new RunProperties(new FontSize { Val = size }) : null;
    var line = Empty();
    line.AppendChild<OpenXmlElement>(tag == null ? TextRun(nameProps, Dots) : SdtOf(body, tag, TextRun(nameProps, Dots)));
    cell.AppendChild(line);
}

// Ô "CẤP CÓ THẨM QUYỀN GIAO VIỆC": họ tên người duyệt ở dòng cuối, cùng kiểu với ô người được giao.
void AddApproverSign(Body body)
{
    if (HasTag(body, "APPROVER_SIGN"))
        return;
    var assignee = body.Descendants<SdtRun>().Single(s => TagOf(s) == "ASSIGNEE_SIGN");
    var cell = body.Descendants<TableCell>().Single(c => c.InnerText.StartsWith("CẤP CÓ THẨM QUYỀN GIAO VIỆC", StringComparison.Ordinal));
    var last = cell.Elements<Paragraph>().Last();
    if (last.InnerText.Length != 0)
        throw new InvalidOperationException("Ô ký cấp giao việc không như dự kiến.");
    var paragraph = (Paragraph)assignee.Ancestors<Paragraph>().First().CloneNode(true);
    foreach (var a in paragraph.Descendants().Prepend(paragraph).SelectMany(n => n.GetAttributes().Where(a => a.LocalName is "paraId" or "textId").Select(a => (n, a))).ToList())
        a.n.RemoveAttribute(a.a.LocalName, a.a.NamespaceUri);
    var sdt = paragraph.Descendants<SdtRun>().Single();
    sdt.SdtProperties = Props(body, "APPROVER_SIGN");
    last.InsertAfterSelf(paragraph);
    last.Remove();
}

void ReplaceText(Body body, string from, string to)
{
    if (body.Descendants<Text>().Any(t => t.Text.Contains(to, StringComparison.Ordinal)))
        return;
    var text = body.Descendants<Text>().Single(t => t.Text.Contains(from, StringComparison.Ordinal));
    text.Text = text.Text.Replace(from, to, StringComparison.Ordinal);
}

// ", ngày…… tháng… … năm……" sau ORG_LOCATION: phần ngày vào control DATE_LINE.
void TagMau11DateLine(Body body)
{
    if (HasTag(body, "DATE_LINE"))
        return;
    const string text = ", ngày…… tháng… … năm……";
    var run = body.Descendants<Run>().Single(r => r.InnerText == text);
    run.GetFirstChild<Text>()!.Text = ", ";
    var dateRun = (Run)run.CloneNode(true);
    dateRun.GetFirstChild<Text>()!.Text = text[2..];
    run.InsertAfterSelf(SdtOf(body, "DATE_LINE", dateRun));
}

// "… của Thường vụ Đảng ủy <công ty chủ quản> về việc đánh giá định kỳ hằng quý … trong <Đảng bộ cấp trên>;" (đúng trích yếu Hướng dẫn).
void FixGuideReference(Body body)
{
    if (HasTag(body, "GUIDE_PARTY"))
        return;
    var parent = body.Descendants<SdtRun>().Single(s => TagOf(s) == "ORG_PARENT_COMPANY_NAME");
    var paragraph = parent.Ancestors<Paragraph>().First();
    var tail = parent.ElementsAfter().ToList();
    if (!string.Concat(tail.Select(e => e.InnerText)).StartsWith(" về việc đánh giá, xếp loại cán bộ Quý ", StringComparison.Ordinal))
        throw new InvalidOperationException("Dòng căn cứ Hướng dẫn không như dự kiến.");
    var props = parent.Descendants<RunProperties>().First();
    foreach (var e in tail)
        e.Remove();
    paragraph.Append(
        TextRun(props, " về việc đánh giá định kỳ hằng quý đối với cán bộ lãnh đạo, quản lý trong "),
        SdtOf(body, "GUIDE_PARTY", TextRun(props, "Đảng bộ …")),
        TextRun(props, ";"));
}

// Bảng mục 2 không có trường hợp nào: một dòng gộp cột "Không có …".
void AddNoCasesRow(Body body)
{
    if (HasTag(body, "ifnot:HAS_CASES"))
        return;
    var cases = body.Descendants<SdtRow>().Single(s => TagOf(s) == "repeat:CASES");
    var template = cases.SdtContentRow!.Elements<TableRow>().Single();
    var row = (TableRow)template.CloneNode(true);
    var cells = row.Elements<TableCell>().ToList();
    var span = cells.Sum(c => c.TableCellProperties?.GridSpan?.Val?.Value ?? 1);
    var width = cells.Sum(c => int.TryParse(c.TableCellProperties?.TableCellWidth?.Width?.Value, out var w) ? w : 0);
    foreach (var c in cells.Skip(1))
        c.Remove();
    var cell = cells[0];
    var tcPr = cell.TableCellProperties!;
    tcPr.TableCellWidth = new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa };
    tcPr.GridSpan = new GridSpan { Val = span };
    var paragraph = cell.Elements<Paragraph>().First();
    var props = paragraph.Descendants<RunProperties>().FirstOrDefault();
    foreach (var e in paragraph.ChildElements.Where(e => e is not ParagraphProperties).ToList())
        e.Remove();
    paragraph.AppendChild(TextRun(props, "Không có trường hợp nào được thảo luận, điều chỉnh."));
    var sdt = new SdtRow(Props(body, "ifnot:HAS_CASES"), new SdtContentRow(row));
    cases.InsertAfterSelf(sdt);
}

// "Tập thể tự đánh giá: Đảng ủy / Chi ủy / Tập thể Lãnh đạo " + SUBJECT_NAME: phần gợi ý chỉ in khi chưa có tên tập thể.
void WrapSubjectPrefix(Body body)
{
    if (HasTag(body, "ifnot:HAS_SUBJECT"))
        return;
    const string label = "Tập thể tự đánh giá: ";
    var run = body.Descendants<Run>().Single(r => r.InnerText == label + "Đảng ủy / Chi ủy / Tập thể Lãnh đạo ");
    run.GetFirstChild<Text>()!.Text = label;
    var hint = (Run)run.CloneNode(true);
    hint.GetFirstChild<Text>()!.Text = "Đảng ủy / Chi ủy / Tập thể Lãnh đạo ";
    run.InsertAfterSelf(SdtOf(body, "ifnot:HAS_SUBJECT", hint));
}
