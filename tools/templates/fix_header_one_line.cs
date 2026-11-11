#:package DocumentFormat.OpenXml@3.5.1
// Bảng tiêu đề (tổ chức bên trái, quốc hiệu/"ĐẢNG CỘNG SẢN VIỆT NAM" bên phải): nới cột trái, chữ cột trái 12pt
// để tên tổ chức dài nhất (khoảng 42 ký tự in hoa đậm) nằm trên một dòng; giữ nguyên chữ và tag.
// Khổ đứng: bảng rộng 11100 (vượt lề, cách mép giấy khoảng 0,7 cm), cột trái 6600; khổ ngang: cột trái 7600, hết khổ in.
// Cách chạy: dotnet run tools/templates/fix_header_one_line.cs -- <thư mục Templates/Word>. Chạy lại an toàn (gán giá trị cố định).
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

var dir = args[0];
var AfterSize = new HashSet<string>
{
    "highlight", "u", "effect", "bdr", "shd", "fitText", "vertAlign", "rtl", "cs", "em", "lang", "eastAsianLayout", "specVanish", "oMath"
};
string[] files =
{
    "Mau_03_PhieuYKienChuyenMon.docx", "Mau_04_PhieuDeNghiDieuChinh.docx", "Mau_05_PhieuNhiemVuDotXuat.docx", "Mau_06_BaoCaoKetQuaTapThe.docx", "Mau_07_BaoCaoTuDanhGiaTapThe.docx",
    "Mau_08_TongHopKetQuaNhiemVu.docx", "Mau_10_PhieuThamDinh.docx", "Mau_11_PhieuBoPhieuChiBo.docx",
    "Mau_12_BienBanHoiNghi.docx", "Mau_13_BienBanKiemPhieu.docx", "Mau_16_BaoCaoKetQuaDanhGia.docx",
    "Mau_17_KeHoachHoTro.docx", "Mau_19_PhieuKiemSoat.docx", "Mau_20_BienBanHieuChuan.docx", "Mau_QD_QuyetDinhXepLoai.docx"
};

foreach (var file in files)
{
    using var doc = WordprocessingDocument.Open(Path.Combine(dir, file), true);
    var body = doc.MainDocumentPart!.Document!.Body!;
    var tables = body.Descendants<Table>()
        .Where(t => !t.Descendants<Table>().Any() && t.Elements<TableRow>().Take(3).Any(r => r.Elements<TableCell>().Count() == 2
            && IsStateHeading(r.Elements<TableCell>().Last().InnerText)))
        .ToList();
    if (tables.Count == 0)
        throw new InvalidOperationException(file + ": không thấy bảng tiêu đề.");
    foreach (var table in tables)
    {
        var section = SectionOf(table, body);
        var pageWidth = (int)section.GetFirstChild<PageSize>()!.Width!.Value;
        var marginLeft = (int)section.GetFirstChild<PageMargin>()!.Left!.Value;
        var marginRight = (int)section.GetFirstChild<PageMargin>()!.Right!.Value;
        var landscape = pageWidth > 15000;
        int width, left, indent;
        if (landscape)
        {
            width = pageWidth - marginLeft - marginRight;
            left = 7600;
            indent = 0;
        }
        else
        {
            // Mép bảng cách mép giấy 403 twip mỗi bên.
            width = 11100;
            left = 6600;
            indent = 403 - marginLeft;
        }
        var right = width - left;

        var props = table.GetFirstChild<TableProperties>() ?? table.PrependChild(new TableProperties());
        props.TableWidth = new TableWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa };
        props.TableIndentation = new TableIndentation { Width = indent, Type = TableWidthUnitValues.Dxa };
        props.TableLayout = new TableLayout { Type = TableLayoutValues.Fixed };
        var grid = table.GetFirstChild<TableGrid>()!;
        var columns = grid.Elements<GridColumn>().ToList();
        if (columns.Count != 2)
            throw new InvalidOperationException(file + ": bảng tiêu đề không phải hai cột.");
        columns[0].Width = left.ToString();
        columns[1].Width = right.ToString();

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            if (cells.Count != 2)
                continue;
            SetWidth(cells[0], left);
            SetWidth(cells[1], right);
            // Chữ cột trái 12pt; dòng trống, ảnh, đường kẻ không đổi.
            foreach (var run in cells[0].Descendants<Run>().Where(r => r.GetFirstChild<Text>() != null))
            {
                var rp = run.RunProperties ?? run.PrependChild(new RunProperties());
                rp.FontSize = new FontSize { Val = "24" };
                rp.FontSizeComplexScript = new FontSizeComplexScript { Val = "24" };
            }
            foreach (var mark in cells[0].Descendants<ParagraphMarkRunProperties>())
            {
                mark.RemoveAllChildren<FontSize>();
                mark.RemoveAllChildren<FontSizeComplexScript>();
                // sz, szCs đứng trước highlight, u, …, lang theo thứ tự lược đồ.
                var after = mark.ChildElements.FirstOrDefault(e => AfterSize.Contains(e.LocalName));
                foreach (var size in new OpenXmlElement[] { new FontSize { Val = "24" }, new FontSizeComplexScript { Val = "24" } })
                {
                    if (after != null)
                        after.InsertBeforeSelf(size);
                    else
                        mark.Append(size);
                }
            }
        }
    }
    doc.MainDocumentPart.Document.Save();
    var errors = new DocumentFormat.OpenXml.Validation.OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).Count();
    Console.WriteLine($"{file}: {tables.Count} bảng tiêu đề, {errors} lỗi lược đồ.");
}

static bool IsStateHeading(string text) =>
    text.Contains("ĐẢNG CỘNG SẢN VIỆT NAM", StringComparison.Ordinal) || text.Contains("CỘNG HÒA XÃ HỘI", StringComparison.Ordinal);

static void SetWidth(TableCell cell, int width)
{
    var props = cell.TableCellProperties ?? cell.PrependChild(new TableCellProperties());
    props.TableCellWidth = new TableCellWidth { Width = width.ToString(), Type = TableWidthUnitValues.Dxa };
}

// Section áp dụng cho bảng: sectPr trong đoạn đầu tiên phía sau, không có thì sectPr cuối thân văn bản.
static SectionProperties SectionOf(Table table, Body body)
{
    foreach (var p in table.ElementsAfter().SelectMany(e => e is Paragraph para ? new[] { para } : e.Descendants<Paragraph>()))
    {
        var s = p.ParagraphProperties?.GetFirstChild<SectionProperties>();
        if (s != null)
            return s;
    }
    return body.GetFirstChild<SectionProperties>() ?? body.Descendants<SectionProperties>().Last();
}
