#:package DocumentFormat.OpenXml@3.5.1
// Sửa bố cục bản in (PDF qua LibreOffice) của template Word: độ rộng cột, không cắt hàng, lặp tiêu đề bảng, khối ký liền nội dung,
// cỡ chữ đồng bộ, bỏ đoạn trống thừa. Giữ nguyên mọi Content Control/Tag và chữ in sẵn (riêng khối ký Mẫu 20 trình bày lại).
// Cách chạy: dotnet run tools/templates/fix_print_layout.cs -- <thư mục Templates/Word>. Chạy lại an toàn (đặt giá trị tuyệt đối, khối đã chuyển thì bỏ qua).
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
const string Times = "Times New Roman";

Edit("Mau_01_PhieuGiaoNhiemVu.docx", body => TotalRowSize(body, "TỔNG TRỌNG SỐ NHÓM"));
Edit("Mau_02_TuDanhGia.docx", Mau02);
Edit("Mau_03_PhieuYKienChuyenMon.docx", Mau03);
Edit("Mau_04_PhieuDeNghiDieuChinh.docx", Mau04);
Edit("Mau_05_PhieuNhiemVuDotXuat.docx", Mau05);
Edit("Mau_07_BaoCaoTuDanhGiaTapThe.docx", Mau07);
Edit("Mau_08_TongHopKetQuaNhiemVu.docx", Mau08);
Edit("Mau_09A_PhieuTuCham.docx", Mau09A);
Edit("Mau_9D_PhuLucKetQuaNhiemVu.docx", Mau9D);
Edit("Mau_10_PhieuThamDinh.docx", Mau10);
Edit("Mau_11_PhieuBoPhieuChiBo.docx", Mau11);
Edit("Mau_12_BienBanHoiNghi.docx", Mau12);
Edit("Mau_13_BienBanKiemPhieu.docx", Mau13);
Edit("Mau_16_BaoCaoKetQuaDanhGia.docx", Mau16);
Edit("Mau_19_PhieuKiemSoat.docx", body => PartyHeader(body));
Edit("Mau_20_BienBanHieuChuan.docx", Mau20);
Edit("Mau_QD_QuyetDinhXepLoai.docx", body => SetGrid(TableWith(body, "Mức tự đề xuất"), 700, 1900, 2700, 1588, 1588, 1588, 1588, 1588, 1896));

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

void Mau02(Body body)
{
    TotalRowSize(body, "TỔNG ĐIỂM SẢN PHẨM");
    // Hai dòng công thức không tách trang.
    Keep(Para(body, "Công thức tính"), next: true);
    Keep(Para(body, "Kết quả thực hiện sản phẩm (%) ="), next: true);
    Keep(Para(body, "Điểm sản phẩm (đ) ="), next: false);
}

void Mau03(Body body)
{
    var row = RowWith(body, "CÁN BỘ ĐỐI SOÁT DỮ LIỆU");
    SignCellsTop(row);
    // Chỉ đoạn chức danh cách trên; các dòng trống để ký không cộng thêm khoảng cách.
    foreach (var cell in Cells(row))
        foreach (var p in cell.Elements<Paragraph>().Skip(1))
            Spacing(p, before: 0);
    SignNames(body, "LEADER_NAME", "REVIEWER_NAME");
}

void Mau04(Body body)
{
    Spacing(Para(body, "PHIẾU ĐỀ NGHỊ ĐIỀU CHỈNH"), before: 240);
    var table = TableWith(body, "Thông tin đề nghị");
    SetGrid(table, Grid(table));
    var row = RowWith(body, "CÁ NHÂN ĐỀ NGHỊ");
    SignCellsTop(row);
    EqualizeSignCells(row);
    SignNames(body, "APPROVER_SIGN", "REQUESTER_SIGN");
    foreach (var sdt in Sdts(body, "AFFECTED_TASK"))
        Size(sdt, 20);
}

void Mau05(Body body)
{
    Spacing(Para(body, "PHIẾU GHI NHẬN NHIỆM VỤ BỔ SUNG"), before: 120);
    // "Kỳ đánh giá" căn trái như các dòng thông tin khác.
    Align(Para(body, "Kỳ đánh giá"), JustificationValues.Left);
    // Dòng địa danh, ngày tháng không in đậm.
    var header = Para(body, "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM");
    var runs = header.Descendants<Run>().ToList();
    var lastBreak = runs.FindLastIndex(r => r.GetFirstChild<Break>() != null);
    foreach (var run in runs.Skip(lastBreak + 1))
        Bold(run, false);
    var table = TableWith(body, "Thông tin nhiệm vụ đột xuất");
    SetGrid(table, Grid(table));
    var row = RowWith(body, "NGƯỜI ĐƯỢC GIAO NHIỆM VỤ");
    // Đoạn trống đầu ô chuyển xuống trên dòng tên: chức danh và tên các ô ký cùng một dòng.
    foreach (var cell in Cells(row))
    {
        var paras = cell.Elements<Paragraph>().ToList();
        if (paras.Count > 1 && paras[0].InnerText.Length == 0 && !paras[0].Descendants<SdtElement>().Any())
        {
            paras[0].Remove();
            paras[^1].InsertBeforeSelf(paras[0]);
        }
    }
    SignCellsTop(row);
    EqualizeSignCells(row);
    SignNames(body, "APPROVER_SIGN", "ASSIGNEE_SIGN");
}

void Mau07(Body body)
{
    // Tiêu đề mục, câu hỏi gợi ý đi liền đoạn nội dung ngay sau.
    string[] headings =
    [
        "A. NỘI DUNG TỰ ĐÁNH GIÁ", "I. Ưu điểm", "1. Việc chấp hành", "2. Kết quả thực hiện các mục tiêu", "3. Công tác xây dựng",
        "4. Trách nhiệm của tập thể", "II. Hạn chế", "- Hạn chế, khuyết điểm.", "- Nguyên nhân của hạn chế", "III. Kết quả khắc phục",
        "Kiểm điểm rõ từng", "IV. Giải trình", "Giải trình từng vấn đề", "V. Trách nhiệm", "Về hạn chế, khuyết điểm trong",
        "VI. Phương hướng", "B. KẾT QUẢ CHẤM ĐIỂM", "(Theo khung tiêu chí",
    ];
    foreach (var h in headings)
        Keep(Para(body, h), next: true);
    // Mục V giãn dòng như các mục khác.
    var v = Para(body, "Về hạn chế, khuyết điểm trong");
    foreach (var p in new[] { v, NextParagraph(v) })
    {
        var s = p.ParagraphProperties!.SpacingBetweenLines!;
        s.Before = "120";
        s.After = null;
        s.Line = "234";
        s.LineRule = LineSpacingRuleValues.Auto;
    }
}

void Mau08(Body body)
{
    var table = TableWith(body, "Nội dung công việc");
    var rows = Rows(table).ToList();
    if (rows[0].InnerText.Contains("BÁO CÁO TỔNG HỢP"))
    {
        // Tên báo cáo đưa ra ngoài bảng để hàng tiêu đề cột là hàng đầu (lặp lại được ở trang sau).
        var titleCell = Cells(rows[0]).First();
        foreach (var child in titleCell.ChildElements.Where(c => c is not TableCellProperties).ToList())
        {
            child.Remove();
            table.InsertBeforeSelf(child);
        }
        table.InsertBeforeSelf(EmptyParagraph(20));
        rows[0].Remove();
        rows[1].Remove();
        rows = Rows(table).ToList();
    }
    SetGrid(table, 600, 2254, 1559, 1743, 1911, 992, 11);
    // Không lặp tiêu đề cột: hàng tiêu đề cao, lặp lại đẩy khối ký sang trang riêng.
    foreach (var row in rows)
        NoSplit(row);
}

void Mau09A(Body body)
{
    var table = TableWith(body, "Nội dung tiêu chí");
    foreach (var row in Rows(table).Skip(1))
    {
        foreach (var cell in Cells(row))
        {
            VAlign(cell, TableVerticalAlignmentValues.Center);
            foreach (var p in cell.Elements<Paragraph>())
                MarkSize(p, 18);
        }
    }
    // Ô điểm, ghi chú cùng cỡ 9pt với cả bảng; điểm và dấu X căn giữa.
    foreach (var (tag, bold, center) in new[]
    {
        ("G_NOTE", true, false), ("I_NOTE", false, false), ("I_MET", true, true), ("I_NOT_MET", true, true),
        ("I_SCORE", false, true), ("GENERAL_SCORE", true, true),
    })
    {
        foreach (var sdt in Sdts(table, tag))
        {
            Size(sdt, 18, bold);
            if (center)
                Align(sdt.Ancestors<Paragraph>().First(), JustificationValues.Center);
        }
    }
    RemoveTrailingEmptyParagraphs(body);
}

void Mau9D(Body body)
{
    // Ô tiêu đề trái đủ rộng cho tên chi bộ dài; gạch dưới giữ độ dài cũ.
    var head = body.Elements<Table>().First();
    SetGrid(head, 8000, 8585);
    foreach (var p in head.Descendants<Paragraph>().Where(p => p.ParagraphProperties?.Indentation?.Left != null))
    {
        var inLeft = Cells(Rows(head).First()).First().Descendants<Paragraph>().Contains(p);
        var ind = p.ParagraphProperties!.Indentation!;
        ind.Left = ind.Right = inLeft ? "2797" : "2812";
    }

    var table = TableWith(body, "Thời hạn hoàn thành");
    // Cột thời hạn đủ cho ngày dd/MM/yyyy một dòng.
    SetGrid(table, 1135, 2268, 1400, 4270, 1417, 1701, 1683);
    if (!table.InnerText.Contains("CÁ NHÂN TỰ ĐÁNH GIÁ"))
        MoveSignIntoTable(table, blanks: 4);
    // Lề trái trừ phần thụt của bảng (562) và lề ô (108): chữ và điểm dừng tab giữ vị trí như khi nằm ngoài bảng.
    foreach (var p in Cells(Rows(table).Last()).First().Elements<Paragraph>())
    {
        if (p.ParagraphProperties?.Indentation is { Left: not null } ind)
            ind.Left = "50";
    }
    foreach (var row in Rows(table))
        NoSplit(row);
}

void Mau10(Body body)
{
    // Dòng "Mức xếp loại đề xuất" đi liền các ô chọn và khối ký.
    var label = Para(body, "Mức xếp loại đề xuất trình cấp có thẩm quyền");
    Keep(label, next: true);
    Keep(NextParagraph(label), next: true);
    // Ô "Lưu ý" phủ hết 6 cột như các hàng trên.
    var table = TableWith(body, "Nội dung thẩm định");
    var note = Rows(table).Last();
    note.TableRowProperties?.RemoveAllChildren<GridAfter>();
    note.TableRowProperties?.RemoveAllChildren<WidthAfterTableRow>();
    Cells(note).First().TableCellProperties!.GridSpan = new GridSpan { Val = Grid(table).Length };
    SetGrid(table, Grid(table));
}

void Mau11(Body body)
{
    var table = TableWith(body, "Họ và tên");
    // TT đủ rộng cho "TT"; cột mức xếp loại không dàn 5 dòng; cột (6) đủ rộng để tiêu đề lặp ở trang sau không bị cắt chữ.
    SetGrid(table, 567, 1400, 1606, 800, 1136, 950, 900, 1900, 1250, 850, 800, 800, 820, 804);
    foreach (var row in Rows(table))
        NoSplit(row);
    HeaderRows(table, 3);
    GroupRowsLeft(table);
    // "Ý kiến khác" và người ghi phiếu thành hàng cuối bảng để không dồn riêng sang trang sau.
    if (!table.InnerText.Contains("NGƯỜI GHI PHIẾU"))
        MoveSignIntoTable(table, blanks: 0);
}

// Đưa các đoạn ngay sau bảng (khối ký) vào hàng cuối không viền: LibreOffice không giữ hàng cuối bảng với đoạn sau.
void MoveSignIntoTable(Table table, int blanks)
{
    var moved = new List<Paragraph>();
    for (var e = table.NextSibling(); e is Paragraph p; e = e.NextSibling())
        moved.Add(p);
    var cell = new TableCell(new TableCellProperties(
        new TableCellWidth { Width = Grid(table).Sum().ToString(), Type = TableWidthUnitValues.Dxa },
        new GridSpan { Val = Grid(table).Length },
        new TableCellBorders(new LeftBorder { Val = BorderValues.Nil }, new BottomBorder { Val = BorderValues.Nil },
            new RightBorder { Val = BorderValues.Nil })));
    foreach (var p in moved)
    {
        p.Remove();
        cell.AppendChild(p);
    }
    for (var i = 0; i < blanks; i++)
        cell.AppendChild(EmptyParagraph(24));
    foreach (var run in cell.Descendants<Run>())
        TimesFont(run);
    foreach (var p in cell.Elements<Paragraph>())
        TimesMark(p);
    table.AppendChild(new TableRow(new TableRowProperties(new CantSplit()), cell));
    table.InsertAfterSelf(TinyParagraph());
}

void Mau12(Body body)
{
    // Thêm dòng trống dưới "THƯ KÝ" cho ngang dòng chức danh bên Chủ trì; hai dòng "(ký, ghi rõ họ tên)" thẳng hàng.
    var cell = body.Descendants<TableCell>().First(c => c.InnerText.StartsWith("THƯ KÝ"));
    var paras = cell.Elements<Paragraph>().ToList();
    if (paras.Count == 2)
    {
        var blank = new Paragraph(paras[0].ParagraphProperties!.CloneNode(true));
        paras[0].InsertAfterSelf(blank);
    }
}

void Mau13(Body body)
{
    var table = TableWith(body, "Số phiếu bầu");
    // Cột chức vụ, đơn vị rộng hơn; bớt từ cột Ghi chú.
    SetGrid(table, 604, 1405, 2289, 1000, 1000, 803, 1000, 1000, 1100);
    foreach (var row in Rows(table))
        NoSplit(row);
    HeaderRows(table, 3);
    GroupRowsLeft(table);
    Spacing(Para(body, "Hội nghị kết thúc vào hồi"), after: 240);
}

void Mau16(Body body)
{
    // Cột "Đối tượng" rộng hơn; bớt từ cột Ghi chú, (3), (9).
    foreach (var table in body.Descendants<Table>().Where(t => t.InnerText.Contains("Đối tượng")).ToList())
    {
        SetGrid(table, 698, 2403, 1129, 705, 676, 772, 772, 768, 1278, 1000);
        foreach (var row in Rows(table))
            NoSplit(row);
        HeaderRows(table, 3);
    }
    Keep(Para(body, "I. Các trường hợp thuộc thẩm quyền"), next: true);
    var second = Para(body, "II. Các trường hợp thuộc thẩm quyền");
    Keep(second, next: true);
    Keep(NextParagraph(second), next: true);
}

// Tiêu đề cơ quan bên trái (Mẫu 19, 20): ô rộng hơn, cỡ 11pt để tên dài không xuống dòng mồ côi.
void PartyHeader(Body body)
{
    var head = body.Descendants<Table>().First(t => t.InnerText.Contains("ĐẢNG CỘNG SẢN VIỆT NAM"));
    SetGrid(head, 6300, 4200);
    var left = Cells(Rows(head).First()).First();
    foreach (var p in left.Elements<Paragraph>())
    {
        if (p.ParagraphProperties?.Indentation != null)
            p.ParagraphProperties.Indentation = null;
    }
    Size(left, 22);
}

void Mau20(Body body)
{
    PartyHeader(body);

    foreach (var t in body.Descendants<Table>().Where(t => t.InnerText.Contains("Thời gian, địa điểm") || t.InnerText.Contains("Nhóm/mức xếp loại")
                 || t.InnerText.Contains("Họ tên/Mã hồ sơ")).ToList())
        SetGrid(t, Grid(t));

    var old = body.Descendants<Table>().FirstOrDefault(t => t.InnerText.Contains("Thư ký (ký, ghi rõ họ tên)"));
    if (old == null)
        return;
    // Khối ký trình bày như các mẫu khác: thành viên/ý kiến bảo lưu, rồi hai cột Chủ trì – Thư ký (chức danh, ghi chú, chỗ ký, họ tên).
    var chair = Sdts(old, "CHAIR_NAME").First();
    var secretary = Sdts(old, "SECRETARY_NAME").First();
    var membersCell = Sdts(old, "repeat:MEMBERS").First().Ancestors<TableCell>().First();
    var memberBlocks = membersCell.ChildElements.Where(c => c is SdtBlock).ToList();

    var members = new TableCell(new TableCellProperties(
        new TableCellWidth { Width = "9537", Type = TableWidthUnitValues.Dxa }, new GridSpan { Val = 2 }));
    members.AppendChild(TextParagraph("Các thành viên/ý kiến bảo lưu:", JustificationValues.Left, 24, bold: false, italic: false, before: 120));
    foreach (var block in memberBlocks)
    {
        block.Remove();
        Size(block, 24);
        members.AppendChild(block);
    }

    var table = new Table(
        new TableProperties(
            new TableWidth { Width = "9537", Type = TableWidthUnitValues.Dxa },
            new TableJustification { Val = TableRowAlignmentValues.Center },
            new TableBorders(
                new TopBorder { Val = BorderValues.Nil }, new LeftBorder { Val = BorderValues.Nil }, new BottomBorder { Val = BorderValues.Nil },
                new RightBorder { Val = BorderValues.Nil }, new InsideHorizontalBorder { Val = BorderValues.Nil },
                new InsideVerticalBorder { Val = BorderValues.Nil }),
            new TableLayout { Type = TableLayoutValues.Fixed }),
        new TableGrid(new GridColumn { Width = "4768" }, new GridColumn { Width = "4769" }),
        new TableRow(new TableRowProperties(new CantSplit()), members),
        new TableRow(new TableRowProperties(new CantSplit()), SignCell("CHỦ TRÌ", chair, 4768), SignCell("THƯ KÝ", secretary, 4769)));
    old.InsertBeforeSelf(table);
    old.Remove();
}

TableCell SignCell(string title, SdtElement name, int width)
{
    var cell = new TableCell(new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa }));
    cell.AppendChild(TextParagraph(title, JustificationValues.Center, 24, bold: true, italic: false, before: 240));
    cell.AppendChild(TextParagraph("(Ký, ghi rõ họ tên)", JustificationValues.Center, 22, bold: false, italic: true, before: 0));
    for (var i = 0; i < 4; i++)
        cell.AppendChild(TextParagraph("", JustificationValues.Center, 24, bold: false, italic: false, before: 0));
    var line = TextParagraph("", JustificationValues.Center, 24, bold: false, italic: false, before: 0);
    name.Remove();
    Size(name, 24, false);
    line.AppendChild(name);
    cell.AppendChild(line);
    return cell;
}

Paragraph TextParagraph(string text, JustificationValues align, int size, bool bold, bool italic, int before)
{
    RunProperties Props()
    {
        var rp = new RunProperties(new RunFonts { Ascii = Times, HighAnsi = Times, ComplexScript = Times });
        if (bold)
            rp.Bold = new Bold();
        if (italic)
            rp.Italic = new Italic();
        rp.FontSize = new FontSize { Val = size.ToString() };
        rp.FontSizeComplexScript = new FontSizeComplexScript { Val = size.ToString() };
        return rp;
    }
    var pp = new ParagraphProperties(
        new SpacingBetweenLines { Before = before.ToString(), After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto },
        new Justification { Val = align });
    pp.ParagraphMarkRunProperties = new ParagraphMarkRunProperties(new FontSize { Val = size.ToString() }, new FontSizeComplexScript { Val = size.ToString() });
    var p = new Paragraph(pp);
    if (text.Length > 0)
        p.AppendChild(new Run(Props(), new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    return p;
}

// ——— Tiện ích ———

string? TagOf(OpenXmlElement e) => (e as SdtElement)?.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

IEnumerable<SdtElement> Sdts(OpenXmlElement root, string tag) =>
    root.Descendants<SdtElement>().Where(s => string.Equals(TagOf(s), tag, StringComparison.OrdinalIgnoreCase)).ToList();

Table TableWith(Body body, string text) => body.Descendants<Table>().First(t => t.InnerText.Contains(text));

TableRow RowWith(Body body, string text) => body.Descendants<TableRow>().First(r => r.InnerText.Contains(text));

Paragraph Para(Body body, string startsWith) =>
    body.Descendants<Paragraph>().First(p => p.InnerText.TrimStart().StartsWith(startsWith, StringComparison.Ordinal));

Paragraph NextParagraph(Paragraph p) => p.ElementsAfter().OfType<Paragraph>().First();


IEnumerable<TableRow> Rows(Table t) => t.Descendants<TableRow>().Where(r => r.Ancestors<Table>().First() == t).ToList();

IEnumerable<TableCell> Cells(TableRow r) => r.Descendants<TableCell>().Where(c => c.Ancestors<TableRow>().First() == r).ToList();

int[] Grid(Table t) => t.GetFirstChild<TableGrid>()!.Elements<GridColumn>().Select(g => int.Parse(g.Width!.Value!)).ToArray();

// Đặt độ rộng cột lưới, bảng và từng ô (theo gridSpan) cho khớp nhau.
void SetGrid(Table t, params int[] widths)
{
    var grid = t.GetFirstChild<TableGrid>()!;
    grid.RemoveAllChildren<GridColumn>();
    foreach (var w in widths)
        grid.AppendChild(new GridColumn { Width = w.ToString() });
    t.GetFirstChild<TableProperties>()!.TableWidth = new TableWidth { Width = widths.Sum().ToString(), Type = TableWidthUnitValues.Dxa };
    foreach (var row in Rows(t))
    {
        var col = 0;
        foreach (var cell in Cells(row))
        {
            var span = cell.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
            var w = widths.Skip(col).Take(span).Sum();
            (cell.TableCellProperties ??= new TableCellProperties()).TableCellWidth =
                new TableCellWidth { Width = w.ToString(), Type = TableWidthUnitValues.Dxa };
            col += span;
        }
    }
}

void NoSplit(TableRow row)
{
    var props = row.TableRowProperties ??= new TableRowProperties();
    if (props.GetFirstChild<CantSplit>() == null)
        props.AppendChild(new CantSplit());
}

void HeaderRows(Table t, int count)
{
    foreach (var row in Rows(t).Take(count))
    {
        var props = row.TableRowProperties ??= new TableRowProperties();
        if (props.GetFirstChild<TableHeader>() == null)
            props.AppendChild(new TableHeader());
    }
}

// Hàng nhóm (ô gộp nhiều cột) căn trái, không giãn chữ.
void GroupRowsLeft(Table t)
{
    // Bỏ qua hàng khối ký (giữ căn giữa chức danh người ký).
    foreach (var cell in t.Descendants<TableCell>().Where(c => (c.TableCellProperties?.GridSpan?.Val?.Value ?? 1) >= 8
                 && !c.InnerText.Contains("NGƯỜI GHI PHIẾU")))
        foreach (var p in cell.Elements<Paragraph>())
            Align(p, JustificationValues.Left);
}

void Keep(Paragraph p, bool next)
{
    var pp = p.ParagraphProperties ??= new ParagraphProperties();
    if (next)
        pp.KeepNext ??= new KeepNext();
    pp.KeepLines ??= new KeepLines();
}

void Spacing(Paragraph p, int? before = null, int? after = null)
{
    var pp = p.ParagraphProperties ??= new ParagraphProperties();
    var s = pp.SpacingBetweenLines ??= new SpacingBetweenLines();
    if (before != null)
        s.Before = before.Value.ToString();
    if (after != null)
        s.After = after.Value.ToString();
}

void Align(Paragraph p, JustificationValues value) =>
    (p.ParagraphProperties ??= new ParagraphProperties()).Justification = new Justification { Val = value };

void VAlign(TableCell cell, TableVerticalAlignmentValues value) =>
    (cell.TableCellProperties ??= new TableCellProperties()).TableCellVerticalAlignment = new TableCellVerticalAlignment { Val = value };

void Size(OpenXmlElement scope, int halfPoints, bool? bold = null)
{
    foreach (var run in scope.Descendants<Run>())
    {
        var rp = run.RunProperties ??= new RunProperties();
        rp.FontSize = new FontSize { Val = halfPoints.ToString() };
        rp.FontSizeComplexScript = new FontSizeComplexScript { Val = halfPoints.ToString() };
        if (bold != null)
            Bold(run, bold.Value);
    }
}

void Bold(Run run, bool on)
{
    var rp = run.RunProperties ??= new RunProperties();
    rp.Bold = on ? new Bold() : null;
    rp.BoldComplexScript = on ? new BoldComplexScript() : null;
}

void MarkSize(Paragraph p, int halfPoints)
{
    var pp = p.ParagraphProperties ??= new ParagraphProperties();
    var mark = pp.ParagraphMarkRunProperties ??= new ParagraphMarkRunProperties();
    mark.RemoveAllChildren<FontSize>();
    mark.RemoveAllChildren<FontSizeComplexScript>();
    mark.AddChild(new FontSize { Val = halfPoints.ToString() });
    mark.AddChild(new FontSizeComplexScript { Val = halfPoints.ToString() });
}

void TimesFont(Run run)
{
    var rp = run.RunProperties ??= new RunProperties();
    var fonts = rp.RunFonts ??= new RunFonts();
    fonts.Ascii ??= Times;
    fonts.HighAnsi ??= Times;
    fonts.ComplexScript ??= Times;
}

void TimesMark(Paragraph p)
{
    var mark = (p.ParagraphProperties ??= new ParagraphProperties()).ParagraphMarkRunProperties ??= new ParagraphMarkRunProperties();
    if (mark.GetFirstChild<RunFonts>() == null)
        mark.AddChild(new RunFonts { Ascii = Times, HighAnsi = Times, ComplexScript = Times });
}

Paragraph EmptyParagraph(int halfPoints)
{
    var p = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }));
    MarkSize(p, halfPoints);
    return p;
}

// Đoạn cuối tài liệu (bắt buộc sau bảng) gần như không chiếm chỗ, tránh trang trắng.
Paragraph TinyParagraph()
{
    var p = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = LineSpacingRuleValues.Exact }));
    MarkSize(p, 2);
    return p;
}

void RemoveTrailingEmptyParagraphs(Body body)
{
    var trailing = body.ChildElements.Reverse().TakeWhile(e => e is Paragraph or SectionProperties).OfType<Paragraph>().ToList();
    if (trailing.Count == 0 || trailing.Any(p => p.InnerText.Trim().Length > 0 || p.Descendants<Break>().Any()))
        return;
    foreach (var p in trailing.Skip(1))
        p.Remove();
    var last = trailing[0];
    last.RemoveAllChildren<Run>();
    var pp = last.ParagraphProperties ??= new ParagraphProperties();
    pp.SpacingBetweenLines = new SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = LineSpacingRuleValues.Exact };
    MarkSize(last, 2);
}

// Hàng CỘNG cùng cỡ chữ với hàng dữ liệu.
void TotalRowSize(Body body, string label)
{
    var row = RowWith(body, label);
    Size(row, 20);
    foreach (var p in row.Descendants<Paragraph>())
        MarkSize(p, 20);
}

// Ô ký căn trên, không tô nền: chức danh thẳng hàng giữa các cột.
void SignCellsTop(TableRow row)
{
    foreach (var cell in Cells(row))
    {
        VAlign(cell, TableVerticalAlignmentValues.Top);
        cell.TableCellProperties!.Shading = null;
    }
}

// Ô ký thiếu dòng tên được thêm dòng trống cho cao bằng nhau.
void EqualizeSignCells(TableRow row)
{
    var cells = Cells(row).ToList();
    var max = cells.Max(c => c.Elements<Paragraph>().Count());
    foreach (var cell in cells)
    {
        var paras = cell.Elements<Paragraph>().ToList();
        for (var i = paras.Count; i < max; i++)
            cell.AppendChild(new Paragraph(paras[^1].ParagraphProperties!.CloneNode(true)));
    }
}

// Họ tên người ký: chữ thường 10pt như các mẫu khác.
void SignNames(Body body, params string[] tags)
{
    foreach (var tag in tags)
    {
        foreach (var sdt in Sdts(body, tag))
        {
            Size(sdt, 20, false);
            MarkSize(sdt.Ancestors<Paragraph>().First(), 20);
        }
    }
}
