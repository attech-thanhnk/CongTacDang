#:package DocumentFormat.OpenXml@3.5.1
// Sửa template sau lần chạy thử trọn luồng: tên người ký Mẫu 09C (CELL_CONFIRMER_NAME) và 9D (CELL_CONFIRMER_NAME, SELF_NAME),
// khối ký 09C gọn và đi liền "Lưu ý"; 09A không tách hàng, ô điểm căn giữa; độ rộng cột Mẫu 10, 19; Mẫu 13 bỏ dấu "-" cứng trước C_TITLE;
// Mẫu 17 thêm START_DATE và hạn từng mốc (M30_DUE, M60_DUE, M90_DUE).
// Cách chạy: dotnet run tools/templates/fix_signs_widths_dates.cs -- <thư mục Templates/Word>. Chạy lại an toàn.
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
const string NameDots = "................................";
const string DateDots = "……/……/………";

Edit("Mau_09C_BanTuDanhGia.docx", Mau09C);
Edit("Mau_9D_PhuLucKetQuaNhiemVu.docx", Mau9D);
Edit("Mau_09A_PhieuTuCham.docx", Mau09A);
Edit("Mau_10_PhieuThamDinh.docx", body => SetGrid(TableWith(body, "Nội dung thẩm định"), 3500, 2300, 3900, 1000, 2000, 1514));
Edit("Mau_13_BienBanKiemPhieu.docx", Mau13);
Edit("Mau_17_KeHoachHoTro.docx", Mau17);
Edit("Mau_19_PhieuKiemSoat.docx", body => SetGrid(TableWith(body, "Nội dung kiểm soát"), 700, 5100, 1100, 1500, 2207));

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

void Mau09C(Body body)
{
    var table = TableWith(body, "XÁC NHẬN CỦA CHI BỘ");
    var rows = Rows(table).ToList();
    // Hàng cá nhân ký: bỏ chiều cao cố định và dòng trống cuối để cả khối ký vừa trang đầu.
    rows[0].TableRowProperties?.RemoveAllChildren<TableRowHeight>();
    var selfCell = Cells(rows[0]).First(c => c.InnerText.Contains("CÁ NHÂN TỰ ĐÁNH GIÁ"));
    var last = selfCell.Elements<Paragraph>().Last();
    if (last.InnerText.Trim().Length == 0 && !last.Descendants<SdtElement>().Any() && Sdts(selfCell, "SIGNER_NAME").Any())
        last.Remove();

    var confirmCell = Cells(rows[1]).First(c => c.InnerText.Contains("XÁC NHẬN CỦA CHI BỘ"));
    if (!Sdts(confirmCell, "CELL_CONFIRMER_NAME").Any())
    {
        var nameProps = Sdts(body, "SIGNER_NAME").First().Descendants<RunProperties>().FirstOrDefault();
        confirmCell.Elements<Paragraph>().Last().AppendChild(SdtOf(body, "CELL_CONFIRMER_NAME", TextRun(nameProps, NameDots)));
    }
    // Dòng trống để ký không cộng thêm khoảng cách trên.
    foreach (var p in selfCell.Elements<Paragraph>().Where(p => p.InnerText.Trim().Length == 0 && !p.Descendants<SdtElement>().Any()))
        Spacing(p, before: 0);
    foreach (var p in confirmCell.Elements<Paragraph>())
        Keep(p);
    NoSplit(rows[1]);

    // "Lưu ý" thành hàng cuối bảng cùng ô xác nhận: LibreOffice không giữ hàng cuối bảng với đoạn sau nên "Lưu ý" bị đẩy sang trang riêng.
    if (table.NextSibling() is Paragraph note && note.InnerText.TrimStart().StartsWith("Lưu ý", StringComparison.Ordinal))
    {
        note.Remove();
        var cell = new TableCell(new TableCellProperties(
            new TableCellWidth { Width = Grid(table).Sum().ToString(), Type = TableWidthUnitValues.Dxa },
            new GridSpan { Val = Grid(table).Length }));
        cell.AppendChild(note);
        table.AppendChild(new TableRow(new TableRowProperties(new CantSplit()), cell));
        table.InsertAfterSelf(TinyParagraph());
    }
}

void Spacing(Paragraph p, int before)
{
    var s = (p.ParagraphProperties ??= new ParagraphProperties()).SpacingBetweenLines ??= new SpacingBetweenLines();
    s.Before = before.ToString();
}

// Đoạn cuối tài liệu (bắt buộc sau bảng) gần như không chiếm chỗ.
Paragraph TinyParagraph() => new(new ParagraphProperties(
    new SpacingBetweenLines { Before = "0", After = "0", Line = "20", LineRule = LineSpacingRuleValues.Exact },
    new ParagraphMarkRunProperties(new FontSize { Val = "2" }, new FontSizeComplexScript { Val = "2" })));

void Mau9D(Body body)
{
    var table = TableWith(body, "Thời hạn hoàn thành");
    // Cùng độ rộng cột với fix_print_layout.cs: cột nội dung rộng để tên trục dài không kéo cao hàng.
    SetGrid(table, 1135, 3100, 1400, 3438, 1417, 1701, 1683);
    if (Sdts(body, "SELF_NAME").Any())
        return;
    var row = Rows(table).Last();
    var old = Cells(row).Single();
    var titleProps = old.Descendants<Run>().First(r => r.InnerText.Contains("XÁC NHẬN")).RunProperties;
    var noteProps = old.Descendants<Run>().First(r => r.InnerText.Contains("Xác lập")).RunProperties;
    var grid = Grid(table);
    // Hai khối ký thành hai ô (cột 1–3 và 5–7) để in được tên dưới từng khối.
    // Gắn từng ô vào bảng trước khi tạo ô sau để mã control không trùng.
    old.Remove();
    row.AppendChild(SignCell(grid.Take(3).Sum(), 3, "XÁC NHẬN CỦA CHI BỘ", "(Xác lập thời điểm, ký, ghi rõ họ tên)", "CELL_CONFIRMER_NAME"));
    row.AppendChild(BorderlessCell(grid[3], 1)).AppendChild(new Paragraph());
    row.AppendChild(SignCell(grid.Skip(4).Sum(), 3, "CÁ NHÂN TỰ ĐÁNH GIÁ", "(Ký, ghi rõ họ tên)", "SELF_NAME"));

    TableCell SignCell(int width, int span, string title, string note, string tag)
    {
        var cell = BorderlessCell(width, span);
        cell.AppendChild(CenterParagraph(before: 120, keepNext: true, TextRun(titleProps, title)));
        cell.AppendChild(CenterParagraph(before: 0, keepNext: true, TextRun(noteProps, note)));
        for (var i = 0; i < 3; i++)
            cell.AppendChild(CenterParagraph(before: 0, keepNext: true));
        cell.AppendChild(CenterParagraph(before: 0, keepNext: false, SdtOf(body, tag, TextRun(titleProps, NameDots))));
        return cell;
    }
}

void Mau09A(Body body)
{
    // Hàng tiêu chí (kể cả hàng lặp) không bị tách sang hai trang.
    foreach (var row in TableWith(body, "Nội dung tiêu chí").Descendants<TableRow>())
        NoSplit(row);
    // Ô điểm bảng tổng hợp căn giữa như ô Nhóm chung.
    foreach (var tag in new[] { "TASKS_SCORE", "TOTAL_SCORE" })
        foreach (var sdt in Sdts(body, tag).Where(s => s.Ancestors<TableCell>().Any()))
            Align(sdt.Ancestors<Paragraph>().First(), JustificationValues.Center);
}

void Mau13(Body body)
{
    // Dấu "-" vào chỗ điền C_TITLE: người kiểm phiếu không ghi chức vụ thì không còn "-" lẻ.
    foreach (var sdt in Sdts(body, "C_TITLE"))
    {
        if (sdt.PreviousSibling() is not Run dash || !dash.InnerText.EndsWith("-", StringComparison.Ordinal))
            continue;
        var text = dash.GetFirstChild<Text>()!;
        text.Text = text.Text[..text.Text.LastIndexOf('-')].TrimEnd();
        if (text.Text.Length == 0)
            dash.Remove();
        var content = sdt.Descendants<Text>().First();
        content.Text = " -" + content.Text;
        content.Space = SpaceProcessingModeValues.Preserve;
    }
}

void Mau17(Body body)
{
    if (!Sdts(body, "START_DATE").Any())
    {
        var supporter = Sdts(body, "SUPPORTER_TITLE").First().Ancestors<Paragraph>().First();
        var props = supporter.Descendants<Run>().First().RunProperties;
        var line = new Paragraph((ParagraphProperties)supporter.ParagraphProperties!.CloneNode(true));
        line.Append(TextRun(props, "Ngày bắt đầu thực hiện: "), SdtOf(body, "START_DATE", TextRun(props, DateDots)));
        supporter.InsertAfterSelf(line);
    }
    foreach (var (label, tag) in new[] { ("Mốc 30 ngày", "M30_DUE"), ("Mốc 60 ngày", "M60_DUE"), ("Mốc 90 ngày", "M90_DUE") })
    {
        if (Sdts(body, tag).Any())
            continue;
        var cell = body.Descendants<TableCell>().First(c => c.InnerText.StartsWith(label, StringComparison.Ordinal));
        var first = cell.Elements<Paragraph>().First();
        var props = (RunProperties?)first.Descendants<Run>().First().RunProperties?.CloneNode(true) ?? new RunProperties();
        props.Italic = new Italic();
        var due = new Paragraph((ParagraphProperties)first.ParagraphProperties!.CloneNode(true));
        due.Append(TextRun(props, "Hạn: "), SdtOf(body, tag, TextRun(props, DateDots)));
        cell.AppendChild(due);
    }
}

// ——— Tiện ích ———

string? TagOf(OpenXmlElement e) => (e as SdtElement)?.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

IEnumerable<SdtElement> Sdts(OpenXmlElement root, string tag) =>
    root.Descendants<SdtElement>().Where(s => string.Equals(TagOf(s), tag, StringComparison.OrdinalIgnoreCase)).ToList();

Table TableWith(Body body, string text) => body.Descendants<Table>().First(t => t.InnerText.Contains(text));

IEnumerable<TableRow> Rows(Table t) => t.Descendants<TableRow>().Where(r => r.Ancestors<Table>().First() == t).ToList();

IEnumerable<TableCell> Cells(TableRow r) => r.Descendants<TableCell>().Where(c => c.Ancestors<TableRow>().First() == r).ToList();

int[] Grid(Table t) => t.GetFirstChild<TableGrid>()!.Elements<GridColumn>().Select(g => int.Parse(g.Width!.Value!)).ToArray();

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

void Keep(Paragraph p)
{
    var pp = p.ParagraphProperties ??= new ParagraphProperties();
    pp.KeepNext ??= new KeepNext();
    pp.KeepLines ??= new KeepLines();
}

void Align(Paragraph p, JustificationValues value) =>
    (p.ParagraphProperties ??= new ParagraphProperties()).Justification = new Justification { Val = value };

// Ô không viền trái/phải/dưới (viền trên là đường kẻ cuối bảng dữ liệu).
TableCell BorderlessCell(int width, int span)
{
    var props = new TableCellProperties(new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa });
    if (span > 1)
        props.AppendChild(new GridSpan { Val = span });
    props.AppendChild(new TableCellBorders(new LeftBorder { Val = BorderValues.Nil }, new BottomBorder { Val = BorderValues.Nil },
        new RightBorder { Val = BorderValues.Nil }));
    return new TableCell(props);
}

Paragraph CenterParagraph(int before, bool keepNext, params OpenXmlElement[] content)
{
    var pp = new ParagraphProperties();
    if (keepNext)
        pp.KeepNext = new KeepNext();
    pp.SpacingBetweenLines = new SpacingBetweenLines { Before = before.ToString(), After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto };
    pp.Justification = new Justification { Val = JustificationValues.Center };
    var p = new Paragraph(pp);
    p.Append(content);
    return p;
}

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
