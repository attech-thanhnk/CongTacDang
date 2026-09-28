using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Động cơ điền dữ liệu vào Phôi Word (.docx Template Engine)
/// Hoạt động theo cơ chế nạp file .docx phôi thiết kế sẵn, tìm các thẻ {{TAG}} và đẩy dữ liệu vào
/// Cho phép người dùng chỉnh sửa trực tiếp file Word mẫu mà không cần sửa code.
/// </summary>
public static class DocxTemplateEngine
{
    private static readonly string[] SearchDirectories = new[]
    {
        Path.Combine(AppContext.BaseDirectory, "Templates", "Word"),
        Path.Combine(Directory.GetCurrentDirectory(), "Templates", "Word"),
        Path.Combine(Directory.GetCurrentDirectory(), "src", "CongTacDang.Infrastructure", "Templates", "Word"),
        Path.Combine(Directory.GetCurrentDirectory(), "..", "src", "CongTacDang.Infrastructure", "Templates", "Word")
    };

    public static string GetTemplatePath(string templateName)
    {
        foreach (var dir in SearchDirectories)
        {
            var fullPath = Path.Combine(dir, templateName);
            if (File.Exists(fullPath)) return fullPath;
        }

        // Mặc định thư mục trong base directory
        var targetDir = Path.Combine(AppContext.BaseDirectory, "Templates", "Word");
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }
        return Path.Combine(targetDir, templateName);
    }

    /// <summary>
    /// Thay thế toàn bộ các placeholder {{TAG}} trong văn bản Word
    /// </summary>
    public static void ReplacePlaceholders(OpenXmlElement element, Dictionary<string, string> replacements)
    {
        var texts = element.Descendants<Text>().ToList();
        foreach (var text in texts)
        {
            if (string.IsNullOrEmpty(text.Text)) continue;
            foreach (var kv in replacements)
            {
                if (text.Text.Contains(kv.Key))
                {
                    text.Text = text.Text.Replace(kv.Key, kv.Value);
                }
            }
        }
    }

    /// <summary>
    /// Định dạng enum mức xếp loại sang tiếng Việt
    /// </summary>
    public static string FormatGrade(EvaluationGrade? grade) => grade switch
    {
        EvaluationGrade.HoanThanhXuatSac => "Hoàn thành xuất sắc nhiệm vụ",
        EvaluationGrade.HoanThanhTot => "Hoàn thành tốt nhiệm vụ",
        EvaluationGrade.HoanThanh => "Hoàn thành nhiệm vụ",
        EvaluationGrade.KhongHoanThanh => "Không hoàn thành nhiệm vụ",
        _ => "Chưa xếp loại"
    };

    #region Khởi tạo phôi vật lý ban đầu (nếu chưa có trên đĩa)

    public static void EnsureMasterTemplatesExist()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Templates", "Word");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        // Cũng đảm bảo lưu trong thư mục source code nếu có
        var srcDir = Path.Combine(Directory.GetCurrentDirectory(), "src", "CongTacDang.Infrastructure", "Templates", "Word");
        if (Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "src", "CongTacDang.Infrastructure")))
        {
            if (!Directory.Exists(srcDir)) Directory.CreateDirectory(srcDir);
        }

        CreateDefaultTemplateIfNotExists("Mau_01_PhieuGiaoNhiemVu.docx", dir, srcDir, CreateMasterMau01Template);
        CreateDefaultTemplateIfNotExists("Mau_02_TuDanhGia.docx", dir, srcDir, CreateMasterMau02Template);
        CreateDefaultTemplateIfNotExists("Mau_10_PhieuThamDinh.docx", dir, srcDir, CreateMasterMau10Template);
        CreateDefaultTemplateIfNotExists("Mau_11_PhieuBoPhieuChiBo.docx", dir, srcDir, CreateMasterMau11Template);
        CreateDefaultTemplateIfNotExists("Mau_13_BienBanKiemPhieu.docx", dir, srcDir, CreateMasterMau13Template);
    }

    private static void CreateDefaultTemplateIfNotExists(string fileName, string dir, string srcDir, Func<byte[]> generator)
    {
        var path1 = Path.Combine(dir, fileName);
        if (!File.Exists(path1))
        {
            var bytes = generator();
            File.WriteAllBytes(path1, bytes);
        }

        if (Directory.Exists(srcDir))
        {
            var path2 = Path.Combine(srcDir, fileName);
            if (!File.Exists(path2))
            {
                var bytes = generator();
                File.WriteAllBytes(path2, bytes);
            }
        }
    }

    #endregion

    #region Tạo file Phôi Mẫu (Master Template Builders)

    private static TableBorders CreateBorders(bool visible = true)
    {
        var val = visible ? BorderValues.Single : BorderValues.None;
        var sz = (uint)(visible ? 4 : 0);
        var col = visible ? "000000" : "auto";
        return new TableBorders(
            new TopBorder { Val = val, Size = sz, Color = col },
            new BottomBorder { Val = val, Size = sz, Color = col },
            new LeftBorder { Val = val, Size = sz, Color = col },
            new RightBorder { Val = val, Size = sz, Color = col },
            new InsideHorizontalBorder { Val = val, Size = sz, Color = col },
            new InsideVerticalBorder { Val = val, Size = sz, Color = col }
        );
    }

    private static Paragraph CreateP(string text, bool bold = false, bool italic = false, int size = 26, JustificationValues? align = null, int before = 0, int after = 60, bool underline = false)
    {
        var resolvedAlign = align ?? JustificationValues.Left;
        var p = new Paragraph();
        var pPr = new ParagraphProperties();
        pPr.AppendChild(new Justification { Val = resolvedAlign });
        pPr.AppendChild(new SpacingBetweenLines { Before = before.ToString(), After = after.ToString(), Line = "240", LineRule = LineSpacingRuleValues.Auto });
        p.AppendChild(pPr);

        if (!string.IsNullOrEmpty(text))
        {
            var run = new Run();
            var rPr = new RunProperties();
            rPr.AppendChild(new RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
            rPr.AppendChild(new FontSize { Val = size.ToString() });
            if (bold) rPr.AppendChild(new Bold());
            if (italic) rPr.AppendChild(new Italic());
            if (underline) rPr.AppendChild(new Underline { Val = UnderlineValues.Single });
            run.AppendChild(rPr);
            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            p.AppendChild(run);
        }
        return p;
    }

    private static TableCell CreateCell(string text, bool bold = false, bool italic = false, int size = 22, JustificationValues? align = null, string? widthDxa = null, string? bgColor = null)
    {
        var cell = new TableCell();
        var tcPr = new TableCellProperties();
        if (!string.IsNullOrEmpty(widthDxa)) tcPr.AppendChild(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = widthDxa });
        if (!string.IsNullOrEmpty(bgColor)) tcPr.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = bgColor });
        tcPr.AppendChild(new TableCellMargin(
            new TopMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
            new BottomMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
            new LeftMargin { Width = "120", Type = TableWidthUnitValues.Dxa },
            new RightMargin { Width = "120", Type = TableWidthUnitValues.Dxa }
        ));
        cell.AppendChild(tcPr);
        cell.AppendChild(CreateP(text, bold, italic, size, align, 0, 0));
        return cell;
    }

    private static Table CreateHeaderTable(string formNumber)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(false)));
        var row = new TableRow();

        var cellLeft = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2300" }));
        cellLeft.AppendChild(CreateP("{{ORG_PARENT}}", false, false, 22, JustificationValues.Center, 0, 20));
        cellLeft.AppendChild(CreateP("{{ORG_UNIT}}", true, false, 22, JustificationValues.Center, 0, 20, true));
        row.AppendChild(cellLeft);

        var cellRight = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2700" }));
        cellRight.AppendChild(CreateP(formNumber, true, true, 20, JustificationValues.Right, 0, 20));
        cellRight.AppendChild(CreateP("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", true, false, 22, JustificationValues.Center, 0, 20));
        cellRight.AppendChild(CreateP("Độc lập - Tự do - Hạnh phúc", true, false, 22, JustificationValues.Center, 0, 40, true));
        cellRight.AppendChild(CreateP("{{DATE_STRING}}", false, true, 22, JustificationValues.Right, 0, 20));
        row.AppendChild(cellRight);

        table.AppendChild(row);
        return table;
    }

    private static SectionProperties CreatePortraitProps()
    {
        return new SectionProperties(
            new PageSize { Width = 11906U, Height = 16838U, Orient = PageOrientationValues.Portrait },
            new PageMargin { Top = 1134, Bottom = 1134, Left = 1701, Right = 850, Header = 720U, Footer = 720U, Gutter = 0U }
        );
    }

    public static byte[] CreateMasterMau01Template()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();

            body.AppendChild(CreateHeaderTable("Mẫu số 01"));
            body.AppendChild(CreateP("PHIẾU GIAO / ĐĂNG KÝ SẢN PHẨM, CÔNG VIỆC CHUYÊN MÔN HÀNG QUÝ", true, false, 28, JustificationValues.Center, 120, 40));
            body.AppendChild(CreateP("(Dùng để thống nhất đầu ra, mốc quý, tiêu chuẩn và trọng số. Thông thường 03-07 sản phẩm/nhóm công việc; tổng trọng số cố định là 70 điểm.)", false, true, 22, JustificationValues.Center, 0, 120));

            body.AppendChild(CreateP("Kỳ đánh giá: {{PERIOD_NAME}}", false, false, 24, JustificationValues.Left, 40, 30));
            body.AppendChild(CreateP("Họ và tên cán bộ: {{FULL_NAME}}      Mã số cán bộ: {{CARD_NO}}", false, false, 24, JustificationValues.Left, 0, 30));
            body.AppendChild(CreateP("Chức danh / Vị trí công tác: {{POSITION}}      Đơn vị công tác: {{DEPARTMENT}}", false, false, 24, JustificationValues.Left, 0, 30));
            body.AppendChild(CreateP("Người quản lý trực tiếp: {{SUPERVISOR_NAME}}      Chức vụ: {{SUPERVISOR_ROLE}}", false, false, 24, JustificationValues.Left, 0, 30));
            body.AppendChild(CreateP("Khối chức danh áp dụng: {{JOB_GROUP_NAME}}", false, true, 20, JustificationValues.Left, 0, 40));
            body.AppendChild(CreateP("(Ghi chú: Đăng ký từ 03 đến 07 sản phẩm/nhóm nhiệm vụ trọng tâm; Tổng trọng số cố định = 70 điểm; Hoàn thành trong 05 ngày làm việc đầu quý)", false, true, 20, JustificationValues.Left, 0, 80));

            var table = new Table();
            table.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(true)));

            var hRow = new TableRow(new TableRowProperties(new TableHeader()));
            hRow.AppendChild(CreateCell("STT", true, false, 20, JustificationValues.Center, "450", "F1F5F9"));
            hRow.AppendChild(CreateCell("Tên sản phẩm/nhiệm vụ", true, false, 20, JustificationValues.Center, "1800", "F1F5F9"));
            hRow.AppendChild(CreateCell("Mã SP", true, false, 20, JustificationValues.Center, "650", "F1F5F9"));
            hRow.AppendChild(CreateCell("Trục KQ", true, false, 20, JustificationValues.Center, "650", "F1F5F9"));
            hRow.AppendChild(CreateCell("Vai trò", true, false, 20, JustificationValues.Center, "650", "F1F5F9"));
            hRow.AppendChild(CreateCell("Trọng số", true, false, 20, JustificationValues.Center, "650", "F1F5F9"));
            hRow.AppendChild(CreateCell("Thời hạn", true, false, 20, JustificationValues.Center, "850", "F1F5F9"));
            hRow.AppendChild(CreateCell("Chuẩn đạt", true, false, 20, JustificationValues.Center, "1300", "F1F5F9"));
            hRow.AppendChild(CreateCell("Vượt chuẩn", true, false, 20, JustificationValues.Center, "1100", "F1F5F9"));
            hRow.AppendChild(CreateCell("Minh chứng", true, false, 20, JustificationValues.Center, "1100", "F1F5F9"));
            hRow.AppendChild(CreateCell("Xác nhận", true, false, 20, JustificationValues.Center, "800", "F1F5F9"));
            table.AppendChild(hRow);

            // Dòng Template lặp lại
            var tRow = new TableRow();
            tRow.AppendChild(CreateCell("{{T_STT}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_NAME}}", false, false, 20));
            tRow.AppendChild(CreateCell("{{T_CODE}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_AXIS}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_ROLE}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_WEIGHT}}", true, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_DEADLINE}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_STANDARD}}", false, false, 20));
            tRow.AppendChild(CreateCell("{{T_EXCEED}}", false, false, 20));
            tRow.AppendChild(CreateCell("{{T_EVIDENCE}}", false, false, 20));
            tRow.AppendChild(CreateCell("{{T_SIGNER}}", false, false, 20, JustificationValues.Center));
            table.AppendChild(tRow);

            // Dòng tổng
            var sumRow = new TableRow();
            sumRow.AppendChild(CreateCell("CỘNG", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("TỔNG TRỌNG SỐ NHÓM NHIỆM VỤ CHUYÊN MÔN", true, false, 20, JustificationValues.Left, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("{{TOTAL_WEIGHT}}", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            table.AppendChild(sumRow);

            body.AppendChild(table);

            // 4 Chữ ký
            body.AppendChild(CreateP("", false, false, 20, JustificationValues.Left, 120, 40));
            var signTbl = new Table();
            signTbl.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(false)));
            var sRow = new TableRow();

            var s1 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1250" }));
            s1.AppendChild(CreateP("CẤP CÓ THẨM QUYỀN PHÊ DUYỆT", true, false, 19, JustificationValues.Center, 0, 20));
            s1.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            s1.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s1);

            var s2 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1250" }));
            s2.AppendChild(CreateP("CƠ QUAN TỔ CHỨC CÁN BỘ", true, false, 19, JustificationValues.Center, 0, 20));
            s2.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            s2.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s2);

            var s3 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1250" }));
            s3.AppendChild(CreateP("NGƯỜI TRỰC TIẾP SỬ DỤNG", true, false, 19, JustificationValues.Center, 0, 20));
            s3.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            s3.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s3);

            var s4 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1250" }));
            s4.AppendChild(CreateP("CÁ NHÂN ĐĂNG KÝ", true, false, 19, JustificationValues.Center, 0, 20));
            s4.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            s4.AppendChild(CreateP("{{FULL_NAME}}", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s4);

            signTbl.AppendChild(sRow);
            body.AppendChild(signTbl);

            body.AppendChild(CreateP("Lưu ý: Mọi điểm số/mức nhận xét phải có minh chứng; tiêu chí không áp dụng ghi \"K/AD\"; không tính trùng cùng một kết quả ở nhiều tiêu chí.", false, true, 19, JustificationValues.Left, 120, 0));
            body.AppendChild(CreatePortraitProps());

            main.Document.AppendChild(body);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    public static byte[] CreateMasterMau02Template()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();

            body.AppendChild(CreateHeaderTable("Mẫu số 02"));
            body.AppendChild(CreateP("PHIẾU TỰ ĐÁNH GIÁ KẾT QUẢ THỰC HIỆN SẢN PHẨM, CÔNG VIỆC HÀNG QUÝ", true, false, 28, JustificationValues.Center, 120, 40));
            body.AppendChild(CreateP("(Sai sót hoặc sự cố chỉ ghi nhận bất lợi khi đã xác định trách nhiệm; hành động báo cáo/dừng/điều chỉnh đúng quy trình vì an toàn không bị trừ điểm)", false, true, 21, JustificationValues.Center, 0, 120));

            body.AppendChild(CreateP("Kỳ đánh giá: {{PERIOD_NAME}}", false, false, 24, JustificationValues.Left, 40, 30));
            body.AppendChild(CreateP("Họ và tên cán bộ: {{FULL_NAME}}", false, false, 24, JustificationValues.Left, 0, 30));
            body.AppendChild(CreateP("Chức vụ: {{POSITION}}      Đơn vị công tác: {{DEPARTMENT}}", false, false, 24, JustificationValues.Left, 0, 40));
            body.AppendChild(CreateP("(Thang mức tham chiếu A-B-C-D: 100% = Hoàn thành đầy đủ chuẩn giao; 90%-95% = Hoàn thành cơ bản trọn vẹn; 80%-85% = Đạt phần lớn yêu cầu; 65%-75% = Đáp ứng mức tối thiểu/cơ bản; 50%-60% = Chỉ hoàn thành một phần; 0%-45% = Không đạt/sai sót nghiêm trọng)", false, true, 20, JustificationValues.Left, 0, 80));

            var table = new Table();
            table.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(true)));

            var hRow = new TableRow(new TableRowProperties(new TableHeader()));
            hRow.AppendChild(CreateCell("STT", true, false, 20, JustificationValues.Center, "450", "F1F5F9"));
            hRow.AppendChild(CreateCell("Tên sản phẩm/nhiệm vụ", true, false, 20, JustificationValues.Center, "1900", "F1F5F9"));
            hRow.AppendChild(CreateCell("Trọng số", true, false, 20, JustificationValues.Center, "700", "F1F5F9"));
            hRow.AppendChild(CreateCell("A. Khối lượng (%)", true, false, 20, JustificationValues.Center, "700", "F1F5F9"));
            hRow.AppendChild(CreateCell("B. Chất lượng (%)", true, false, 20, JustificationValues.Center, "700", "F1F5F9"));
            hRow.AppendChild(CreateCell("C. Tiến độ (%)", true, false, 20, JustificationValues.Center, "700", "F1F5F9"));
            hRow.AppendChild(CreateCell("D. Hiệu quả (%)", true, false, 20, JustificationValues.Center, "700", "F1F5F9"));
            hRow.AppendChild(CreateCell("Kết quả SP (%)", true, false, 20, JustificationValues.Center, "800", "F1F5F9"));
            hRow.AppendChild(CreateCell("Điểm đạt (đ)", true, false, 20, JustificationValues.Center, "750", "F1F5F9"));
            hRow.AppendChild(CreateCell("Vượt chuẩn", true, false, 20, JustificationValues.Center, "850", "F1F5F9"));
            hRow.AppendChild(CreateCell("Minh chứng", true, false, 20, JustificationValues.Center, "1100", "F1F5F9"));
            table.AppendChild(hRow);

            // Dòng Template lặp lại
            var tRow = new TableRow();
            tRow.AppendChild(CreateCell("{{T_STT}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_NAME}}", false, false, 20));
            tRow.AppendChild(CreateCell("{{T_WEIGHT}}", true, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_A}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_B}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_C}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_D}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_RESULT_PCT}}", true, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_SCORE}}", true, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_EXCEED}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{T_EVIDENCE}}", false, false, 20));
            table.AppendChild(tRow);

            var sumRow = new TableRow();
            sumRow.AppendChild(CreateCell("CỘNG", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("TỔNG ĐIỂM SẢN PHẨM CHUYÊN MÔN", true, false, 20, JustificationValues.Left, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("70.0", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("{{TOTAL_TASK_SCORE}}", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            sumRow.AppendChild(CreateCell("", false, false, 20, JustificationValues.Center, null, "F8FAFC"));
            table.AppendChild(sumRow);

            body.AppendChild(table);

            body.AppendChild(CreateP("Công thức tính:", true, false, 20, JustificationValues.Left, 60, 20));
            body.AppendChild(CreateP("Kết quả thực hiện sản phẩm (%) = (A × Tỷ trọng A) + (B × Tỷ trọng B) + (C × Tỷ trọng C) + (D × Tỷ trọng D)", false, true, 20, JustificationValues.Left, 0, 20));
            body.AppendChild(CreateP("Điểm sản phẩm (đ) = [Kết quả thực hiện sản phẩm (%) × Trọng số sản phẩm] / 100", false, true, 20, JustificationValues.Left, 0, 80));

            var signTbl = new Table();
            signTbl.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(false)));
            var sRow = new TableRow();

            var s1 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1666" }));
            s1.AppendChild(CreateP("CẤP CÓ THẨM QUYỀN DUYỆT", true, false, 20, JustificationValues.Center, 0, 20));
            s1.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            s1.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s1);

            var s2 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1667" }));
            s2.AppendChild(CreateP("ĐẠI DIỆN LÃNH ĐẠO ĐƠN VỊ", true, false, 20, JustificationValues.Center, 0, 20));
            s2.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            s2.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s2);

            var s3 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1667" }));
            s3.AppendChild(CreateP("CÁ NHÂN TỰ ĐÁNH GIÁ", true, false, 20, JustificationValues.Center, 0, 20));
            s3.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            s3.AppendChild(CreateP("{{FULL_NAME}}", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s3);

            signTbl.AppendChild(sRow);
            body.AppendChild(signTbl);
            body.AppendChild(CreatePortraitProps());

            main.Document.AppendChild(body);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    public static byte[] CreateMasterMau10Template()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();

            body.AppendChild(CreateHeaderTable("Mẫu số 10"));
            body.AppendChild(CreateP("PHIẾU THẨM ĐỊNH, NHẬN XÉT, ĐỀ XUẤT XẾP LOẠI VÀ GHI NHẬN GIẢI TRÌNH", true, false, 26, JustificationValues.Center, 120, 60));

            body.AppendChild(CreateP("Kỳ đánh giá: {{PERIOD_NAME}}", false, false, 24, JustificationValues.Left, 20, 20));
            body.AppendChild(CreateP("Họ và tên cán bộ được đánh giá: {{FULL_NAME}}      Chức vụ: {{POSITION}}", false, false, 24, JustificationValues.Left, 0, 20));
            body.AppendChild(CreateP("Đơn vị công tác: {{DEPARTMENT}}", false, false, 24, JustificationValues.Left, 0, 60));

            body.AppendChild(CreateP("I. KẾT QUẢ ĐỐI SOÁT VÀ THẨM ĐỊNH ĐIỂM SỐ", true, false, 24, JustificationValues.Left, 40, 40));

            var table = new Table();
            table.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(true)));

            var hRow = new TableRow(new TableRowProperties(new TableHeader()));
            hRow.AppendChild(CreateCell("Nội dung thẩm định", true, false, 20, JustificationValues.Center, "2200", "F1F5F9"));
            hRow.AppendChild(CreateCell("Cá nhân tự chấm", true, false, 20, JustificationValues.Center, "800", "F1F5F9"));
            hRow.AppendChild(CreateCell("Thẩm định chính thức", true, false, 20, JustificationValues.Center, "800", "F1F5F9"));
            hRow.AppendChild(CreateCell("Chênh lệch", true, false, 20, JustificationValues.Center, "600", "F1F5F9"));
            hRow.AppendChild(CreateCell("Căn cứ thẩm định/đối soát", true, false, 20, JustificationValues.Center, "1600", "F1F5F9"));
            table.AppendChild(hRow);

            var r1 = new TableRow();
            r1.AppendChild(CreateCell("1. Nhóm tiêu chí chung (TC /30đ)"));
            r1.AppendChild(CreateCell("{{SELF_GEN_SCORE}}", false, false, 20, JustificationValues.Center));
            r1.AppendChild(CreateCell("30.0 đ", false, false, 20, JustificationValues.Center));
            r1.AppendChild(CreateCell("0.0 đ", false, false, 20, JustificationValues.Center));
            r1.AppendChild(CreateCell("Đạt đầy đủ chuẩn phẩm chất chính trị"));
            table.AppendChild(r1);

            var r2 = new TableRow();
            r2.AppendChild(CreateCell("2. Nhóm sản phẩm chuyên môn (CV /70đ)"));
            r2.AppendChild(CreateCell("{{SELF_TASK_SCORE}}", false, false, 20, JustificationValues.Center));
            r2.AppendChild(CreateCell("{{APPRAISAL_TASK_SCORE}}", false, false, 20, JustificationValues.Center));
            r2.AppendChild(CreateCell("{{TASK_DIFF}}", false, false, 20, JustificationValues.Center));
            r2.AppendChild(CreateCell("Đối soát theo sản phẩm & tỷ trọng khung"));
            table.AppendChild(r2);

            var rSum = new TableRow();
            rSum.AppendChild(CreateCell("TỔNG ĐIỂM (100đ)", true, false, 20, JustificationValues.Left, null, "F8FAFC"));
            rSum.AppendChild(CreateCell("{{TOTAL_SELF_SCORE}}", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            rSum.AppendChild(CreateCell("{{TOTAL_APP_SCORE}}", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            rSum.AppendChild(CreateCell("{{TOTAL_DIFF}}", true, false, 20, JustificationValues.Center, null, "F8FAFC"));
            rSum.AppendChild(CreateCell("{{DIFF_EXPLANATION}}", false, true, 20, JustificationValues.Left, null, "F8FAFC"));
            table.AppendChild(rSum);

            body.AppendChild(table);

            body.AppendChild(CreateP("II. Ý KIẾN CỦA CÁC BÊN VÀ KẾT QUẢ GIẢI TRÌNH", true, false, 24, JustificationValues.Left, 80, 40));
            body.AppendChild(CreateP("Ý kiến của Người trực tiếp sử dụng cán bộ: .....................................................................................", false, false, 22, JustificationValues.Left, 0, 30));
            body.AppendChild(CreateP("Nội dung giải trình của cá nhân (khi chênh lệch ≥ 5 điểm): {{EXPLANATION_COMMENT}}", false, false, 22, JustificationValues.Left, 0, 30));
            body.AppendChild(CreateP("Kết luận của Cơ quan thẩm định: Nhất trí đề xuất mức xếp loại {{PROPOSED_GRADE}}.", false, false, 22, JustificationValues.Left, 0, 60));

            var signTbl = new Table();
            signTbl.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(false)));
            var sRow = new TableRow();

            var c1 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1666" }));
            c1.AppendChild(CreateP("CẤP CÓ THẨM QUYỀN DUYỆT", true, false, 19, JustificationValues.Center, 0, 20));
            c1.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            c1.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(c1);

            var c2 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1667" }));
            c2.AppendChild(CreateP("NGƯỜI TRỰC TIẾP SỬ DỤNG", true, false, 19, JustificationValues.Center, 0, 20));
            c2.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            c2.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(c2);

            var c3 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1667" }));
            c3.AppendChild(CreateP("ĐẠI DIỆN CƠ QUAN THẨM ĐỊNH", true, false, 19, JustificationValues.Center, 0, 20));
            c3.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 18, JustificationValues.Center, 0, 600));
            c3.AppendChild(CreateP("........................................", true, false, 20, JustificationValues.Center, 0, 0));
            sRow.AppendChild(c3);

            signTbl.AppendChild(sRow);
            body.AppendChild(signTbl);
            body.AppendChild(CreatePortraitProps());

            main.Document.AppendChild(body);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    public static byte[] CreateMasterMau11Template()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();

            body.AppendChild(CreateHeaderTable("Mẫu số 11"));
            body.AppendChild(CreateP("PHIẾU ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ {{PERIOD_QUARTER_YEAR}}", true, false, 28, JustificationValues.Center, 120, 40));
            body.AppendChild(CreateP("Căn cứ kết quả tự đánh giá, xếp loại của cá nhân, đề xuất của các cấp và các thông tin có liên quan, đề nghị đồng chí cho biết ý kiến đánh giá, xếp loại cán bộ trong quý bằng cách đánh dấu (X) vào ô tương ứng trong danh sách dưới đây:", false, true, 22, JustificationValues.Left, 0, 80));

            var table = new Table();
            table.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(true)));

            var hRow = new TableRow(new TableRowProperties(new TableHeader()));
            hRow.AppendChild(CreateCell("TT", true, false, 19, JustificationValues.Center, "400", "F1F5F9"));
            hRow.AppendChild(CreateCell("Họ và tên", true, false, 19, JustificationValues.Center, "1800", "F1F5F9"));
            hRow.AppendChild(CreateCell("Chức vụ, đơn vị công tác", true, false, 19, JustificationValues.Center, "1600", "F1F5F9"));
            hRow.AppendChild(CreateCell("Điểm tự chấm", true, false, 19, JustificationValues.Center, "700", "F1F5F9"));
            hRow.AppendChild(CreateCell("Tự xếp loại", true, false, 19, JustificationValues.Center, "800", "F1F5F9"));
            hRow.AppendChild(CreateCell("HT Xuất sắc", true, false, 19, JustificationValues.Center, "650", "F1F5F9"));
            hRow.AppendChild(CreateCell("HT Tốt", true, false, 19, JustificationValues.Center, "650", "F1F5F9"));
            hRow.AppendChild(CreateCell("HT Nhiệm vụ", true, false, 19, JustificationValues.Center, "650", "F1F5F9"));
            hRow.AppendChild(CreateCell("Không HT", true, false, 19, JustificationValues.Center, "650", "F1F5F9"));
            table.AppendChild(hRow);

            var tRow = new TableRow();
            tRow.AppendChild(CreateCell("{{R_STT}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{R_NAME}}", true, false, 20));
            tRow.AppendChild(CreateCell("{{R_POSITION_DEPT}}", false, false, 20));
            tRow.AppendChild(CreateCell("{{R_SCORE}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{R_SELF_GRADE}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("[   ]", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("[   ]", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("[   ]", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("[   ]", false, false, 20, JustificationValues.Center));
            table.AppendChild(tRow);

            body.AppendChild(table);

            body.AppendChild(CreateP("Ý kiến khác (nếu có): ............................................................................................................................................................", false, true, 20, JustificationValues.Left, 80, 80));
            body.AppendChild(CreateP("NGƯỜI GHI PHIẾU", true, false, 22, JustificationValues.Right, 80, 20));
            body.AppendChild(CreateP("(có thể ký hoặc không ký tên)", false, true, 19, JustificationValues.Right, 0, 400));
            body.AppendChild(CreatePortraitProps());

            main.Document.AppendChild(body);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    public static byte[] CreateMasterMau13Template()
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();

            body.AppendChild(CreateHeaderTable("Mẫu số 13"));
            body.AppendChild(CreateP("BIÊN BẢN KIỂM PHIẾU", true, false, 28, JustificationValues.Center, 120, 20));
            body.AppendChild(CreateP("Hội nghị Đảng ủy/Chi ủy cơ sở về việc đánh giá, xếp loại chất lượng cán bộ {{PERIOD_QUARTER_YEAR}}", true, false, 22, JustificationValues.Center, 0, 80));

            body.AppendChild(CreateP("I. THỜI GIAN, ĐỊA ĐIỂM VÀ THÀNH PHẦN", true, false, 24, JustificationValues.Left, 40, 20));
            body.AppendChild(CreateP("1. Thời gian: Bắt đầu vào hồi 08h30, ngày {{MEETING_DATE}}", false, false, 22, JustificationValues.Left, 0, 20));
            body.AppendChild(CreateP("2. Địa điểm: Phòng họp Chi bộ", false, false, 22, JustificationValues.Left, 0, 20));
            body.AppendChild(CreateP("3. Thành phần: Tổng số triệu tập: {{TOTAL_VOTERS}} đồng chí; Có mặt: {{TOTAL_VOTERS}} đồng chí; Vắng mặt: 0 đồng chí.", false, false, 22, JustificationValues.Left, 0, 60));

            body.AppendChild(CreateP("II. KẾT QUẢ KIỂM PHIẾU", true, false, 24, JustificationValues.Left, 40, 40));

            var table = new Table();
            table.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(true)));

            var hRow = new TableRow(new TableRowProperties(new TableHeader()));
            hRow.AppendChild(CreateCell("STT", true, false, 19, JustificationValues.Center, "450", "F1F5F9"));
            hRow.AppendChild(CreateCell("Họ và tên cán bộ", true, false, 19, JustificationValues.Center, "2000", "F1F5F9"));
            hRow.AppendChild(CreateCell("HT Xuất sắc", true, false, 19, JustificationValues.Center, "900", "F1F5F9"));
            hRow.AppendChild(CreateCell("HT Tốt", true, false, 19, JustificationValues.Center, "900", "F1F5F9"));
            hRow.AppendChild(CreateCell("HT Nhiệm vụ", true, false, 19, JustificationValues.Center, "900", "F1F5F9"));
            hRow.AppendChild(CreateCell("Không HT", true, false, 19, JustificationValues.Center, "900", "F1F5F9"));
            hRow.AppendChild(CreateCell("Tỷ lệ % biểu quyết", true, false, 19, JustificationValues.Center, "1200", "F1F5F9"));
            table.AppendChild(hRow);

            var tRow = new TableRow();
            tRow.AppendChild(CreateCell("{{V_STT}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{V_NAME}}", true, false, 20));
            tRow.AppendChild(CreateCell("{{V_EXC}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{V_GOOD}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{V_SAT}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{V_UNSAT}}", false, false, 20, JustificationValues.Center));
            tRow.AppendChild(CreateCell("{{V_PCT}}", true, false, 20, JustificationValues.Center));
            table.AppendChild(tRow);

            body.AppendChild(table);

            body.AppendChild(CreateP("", false, false, 20, JustificationValues.Left, 120, 40));
            var signTbl = new Table();
            signTbl.AppendChild(new TableProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }, CreateBorders(false)));
            var sRow = new TableRow();

            var s1 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" }));
            s1.AppendChild(CreateP("TỔ KIỂM PHIẾU", true, false, 22, JustificationValues.Center, 0, 20));
            s1.AppendChild(CreateP("(Ký, ghi rõ họ tên)", false, true, 19, JustificationValues.Center, 0, 600));
            s1.AppendChild(CreateP("........................................", true, false, 22, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s1);

            var s2 = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" }));
            s2.AppendChild(CreateP("CHỦ TRÌ HỘI NGHỊ / BÍ THƯ", true, false, 22, JustificationValues.Center, 0, 20));
            s2.AppendChild(CreateP("(Ký, ghi rõ họ tên, đóng dấu)", false, true, 19, JustificationValues.Center, 0, 600));
            s2.AppendChild(CreateP("........................................", true, false, 22, JustificationValues.Center, 0, 0));
            sRow.AppendChild(s2);

            signTbl.AppendChild(sRow);
            body.AppendChild(signTbl);
            body.AppendChild(CreatePortraitProps());

            main.Document.AppendChild(body);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    #endregion

    #region Điền Dữ Liệu Thực Tế Vào Phôi (Fillers)

    public static byte[] FillMau01Template(EvaluationRecord record)
    {
        EnsureMasterTemplatesExist();
        var templatePath = GetTemplatePath("Mau_01_PhieuGiaoNhiemVu.docx");
        var templateBytes = File.ReadAllBytes(templatePath);

        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            var replacements = new Dictionary<string, string>
            {
                { "{{ORG_PARENT}}", "CƠ QUAN QUẢN LÝ CẤP TRÊN" },
                { "{{ORG_UNIT}}", record.Department?.Name?.ToUpper() ?? record.PartyCell?.Name?.ToUpper() ?? "CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY" },
                { "{{DATE_STRING}}", $"Hà Nội, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}" },
                { "{{PERIOD_NAME}}", record.Period != null ? $"Quý {record.Period.Quarter} / Năm {record.Period.Year}" : "Quý ... / Năm 20..." },
                { "{{QUARTER}}", record.Period != null ? record.Period.Quarter.ToString() : "..." },
                { "{{YEAR}}", record.Period != null ? record.Period.Year.ToString() : DateTime.Now.Year.ToString() },
                { "{{FULL_NAME}}", record.Member?.FullName ?? "................................" },
                { "{{CARD_NUMBER}}", record.Member?.PartyCardNumber ?? "................" },
                { "{{CARD_NO}}", record.Member?.PartyCardNumber ?? "................" },
                { "{{POSITION}}", record.Member?.PositionTitle ?? "................" },
                { "{{DEPARTMENT}}", record.Department?.Name ?? record.PartyCell?.Name ?? "................" },
                { "{{SUPERVISOR_NAME}}", "................................" },
                { "{{SUPERVISOR_ROLE}}", "................" },
                { "{{JOB_GROUP_NAME}}", "Khối 2-Kỹ thuật CNS" }
            };

            // 1. Thay thế placeholders chung
            ReplacePlaceholders(body, replacements);

            // 2. Điền bảng nhiệm vụ (nhân bản dòng template)
            var tasks = record.Tasks?.OrderBy(t => t.TaskOrder).ToList() ?? new List<EvaluationTask>();
            double totalWeight = 0;

            var table = body.Descendants<Table>().FirstOrDefault(t => t.InnerText.Contains("{{T_NAME}}"));
            if (table != null)
            {
                var templateRow = table.Descendants<TableRow>().FirstOrDefault(r => r.InnerText.Contains("{{T_NAME}}"));
                if (templateRow != null)
                {
                    int idx = 1;
                    foreach (var t in tasks)
                    {
                        totalWeight += t.Weight;
                        var newRow = (TableRow)templateRow.CloneNode(true);
                        var rowReplacements = new Dictionary<string, string>
                        {
                            { "{{T_STT}}", idx.ToString() },
                            { "{{T_NAME}}", t.TaskName },
                            { "{{T_CODE}}", $"SP-0{idx}" },
                            { "{{T_AXIS}}", $"T{((idx - 1) % 6) + 1}" },
                            { "{{T_ROLE}}", idx == 1 ? "Chủ trì" : "Phối hợp" },
                            { "{{T_WEIGHT}}", t.Weight.ToString("F1") },
                            { "{{T_DEADLINE}}", t.Deadline.ToString("dd/MM/yyyy") },
                            { "{{T_STANDARD}}", string.IsNullOrEmpty(t.TargetOutput) ? "Đạt chuẩn kỹ thuật được giao" : t.TargetOutput },
                            { "{{T_EXCEED}}", "Vượt tiến độ / chất lượng xuất sắc" },
                            { "{{T_EVIDENCE}}", "Biên bản nghiệm thu / Sản phẩm" },
                            { "{{T_SIGNER}}", "Lãnh đạo đơn vị" }
                        };
                        ReplacePlaceholders(newRow, rowReplacements);
                        table.InsertBefore(newRow, templateRow);
                        idx++;
                    }

                    // Xóa dòng marker mẫu
                    templateRow.Remove();
                }

                // Cập nhật tổng trọng số
                var sumReplacements = new Dictionary<string, string>
                {
                    { "{{TOTAL_WEIGHT}}", totalWeight > 0 ? totalWeight.ToString("F1") : "70.0" }
                };
                ReplacePlaceholders(table, sumReplacements);
            }

            EnsureOrientation(body, isLandscape: true);
            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] FillMau02Template(EvaluationRecord record)
    {
        EnsureMasterTemplatesExist();
        var templatePath = GetTemplatePath("Mau_02_TuDanhGia.docx");
        var templateBytes = File.ReadAllBytes(templatePath);

        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            var replacements = new Dictionary<string, string>
            {
                { "{{ORG_PARENT}}", "CƠ QUAN QUẢN LÝ CẤP TRÊN" },
                { "{{ORG_UNIT}}", record.Department?.Name?.ToUpper() ?? record.PartyCell?.Name?.ToUpper() ?? "CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY" },
                { "{{DATE_STRING}}", $"Hà Nội, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}" },
                { "{{PERIOD_NAME}}", record.Period != null ? $"Quý {record.Period.Quarter} / Năm {record.Period.Year}" : "Quý ... / Năm 20..." },
                { "{{QUARTER}}", record.Period != null ? record.Period.Quarter.ToString() : "..." },
                { "{{YEAR}}", record.Period != null ? record.Period.Year.ToString() : DateTime.Now.Year.ToString() },
                { "{{FULL_NAME}}", record.Member?.FullName ?? "................................" },
                { "{{POSITION}}", record.Member?.PositionTitle ?? "................" },
                { "{{DEPARTMENT}}", record.Department?.Name ?? record.PartyCell?.Name ?? "................" }
            };

            ReplacePlaceholders(body, replacements);

            var tasks = record.Tasks?.OrderBy(t => t.TaskOrder).ToList() ?? new List<EvaluationTask>();
            double totalAchievedScore = 0;

            var table = body.Descendants<Table>().FirstOrDefault(t => t.InnerText.Contains("{{T_NAME}}"));
            if (table != null)
            {
                var templateRow = table.Descendants<TableRow>().FirstOrDefault(r => r.InnerText.Contains("{{T_NAME}}"));
                if (templateRow != null)
                {
                    int idx = 1;
                    foreach (var t in tasks)
                    {
                        double aPct = Math.Round((t.CriteriaA_Ratio) * 100);
                        double bPct = Math.Round((t.CriteriaB_Ratio) * 100);
                        double cPct = Math.Round((t.CriteriaC_Ratio) * 100);
                        double dPct = Math.Round((t.CriteriaD_Ratio) * 100);
                        double resultPct = Math.Round((aPct * 0.15) + (bPct * 0.50) + (cPct * 0.15) + (dPct * 0.20), 1);
                        double score = Math.Round((resultPct * t.Weight) / 100.0, 2);
                        totalAchievedScore += score;

                        var newRow = (TableRow)templateRow.CloneNode(true);
                        var rowReplacements = new Dictionary<string, string>
                        {
                            { "{{T_STT}}", idx.ToString() },
                            { "{{T_NAME}}", t.TaskName },
                            { "{{T_WEIGHT}}", t.Weight.ToString("F1") },
                            { "{{T_A}}", $"{aPct}%" },
                            { "{{T_B}}", $"{bPct}%" },
                            { "{{T_C}}", $"{cPct}%" },
                            { "{{T_D}}", $"{dPct}%" },
                            { "{{T_RESULT_PCT}}", $"{resultPct}%" },
                            { "{{T_SCORE}}", score.ToString("F2") },
                            { "{{T_EXCEED}}", t.IsExceedStandard ? "Đạt vượt chuẩn" : "-" },
                            { "{{T_EVIDENCE}}", string.IsNullOrEmpty(t.Attachment?.OriginalFileName) ? "Hồ sơ thực tế" : t.Attachment.OriginalFileName }
                        };
                        ReplacePlaceholders(newRow, rowReplacements);
                        table.InsertBefore(newRow, templateRow);
                        idx++;
                    }

                    templateRow.Remove();
                }

                var sumReplacements = new Dictionary<string, string>
                {
                    { "{{TOTAL_TASK_SCORE}}", $"{totalAchievedScore:F2} / 70.0" }
                };
                ReplacePlaceholders(table, sumReplacements);
            }

            EnsureOrientation(body, isLandscape: true);
            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] FillMau10Template(EvaluationRecord record)
    {
        EnsureMasterTemplatesExist();
        var templatePath = GetTemplatePath("Mau_10_PhieuThamDinh.docx");
        var templateBytes = File.ReadAllBytes(templatePath);

        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            double selfGen = record.GeneralScoreT1 + record.GeneralScoreT2 + record.GeneralScoreT3 + record.GeneralScoreT4 + record.GeneralScoreT5 + record.GeneralScoreT6;
            if (selfGen <= 0) selfGen = 30.0;
            double appScore = record.AppraisalScore ?? record.TotalSelfScore;
            double diff = Math.Round(appScore - record.TotalSelfScore, 1);

            var replacements = new Dictionary<string, string>
            {
                { "{{ORG_PARENT}}", "TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM" },
                { "{{ORG_UNIT}}", "CƠ QUAN THẨM ĐỊNH" },
                { "{{DATE_STRING}}", $"Hà Nội, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}" },
                { "{{PERIOD_NAME}}", record.Period != null ? $"Quý {record.Period.Quarter} / Năm {record.Period.Year}" : "Quý ... / Năm 20..." },
                { "{{QUARTER}}", record.Period != null ? record.Period.Quarter.ToString() : "..." },
                { "{{YEAR}}", record.Period != null ? record.Period.Year.ToString() : DateTime.Now.Year.ToString() },
                { "{{FULL_NAME}}", record.Member?.FullName ?? "................................" },
                { "{{POSITION}}", record.Member?.PositionTitle ?? "................" },
                { "{{DEPARTMENT}}", record.Department?.Name ?? record.PartyCell?.Name ?? "................" },
                { "{{SCORE}}", appScore.ToString("F1") },
                { "{{SELF_GEN_SCORE}}", $"{selfGen:F1} đ" },
                { "{{SELF_TASK_SCORE}}", $"{record.TasksScore:F1} đ" },
                { "{{APPRAISAL_TASK_SCORE}}", $"{(appScore - 30.0):F1} đ" },
                { "{{TASK_DIFF}}", $"{diff:F1} đ" },
                { "{{TOTAL_SELF_SCORE}}", $"{record.TotalSelfScore:F1} đ" },
                { "{{TOTAL_APP_SCORE}}", $"{appScore:F1} đ" },
                { "{{TOTAL_DIFF}}", $"{diff:F1} đ" },
                { "{{DIFF_EXPLANATION}}", Math.Abs(diff) >= 5 ? "Chênh lệch >= 5đ: Yêu cầu giải trình" : "Hợp lệ không chênh lệch lớn" },
                { "{{EXPLANATION_COMMENT}}", record.AppraisalComment ?? "Không có ý kiến khác." },
                { "{{PROPOSED_GRADE}}", FormatGrade(record.AppraisalProposedGrade) }
            };

            ReplacePlaceholders(body, replacements);
            EnsureOrientation(body, isLandscape: true);
            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] FillMau11Template(EvaluationPeriod period, List<EvaluationRecord> records, string? branchName)
    {
        EnsureMasterTemplatesExist();
        var templatePath = GetTemplatePath("Mau_11_PhieuBoPhieuChiBo.docx");
        var templateBytes = File.ReadAllBytes(templatePath);

        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            var replacements = new Dictionary<string, string>
            {
                { "{{ORG_PARENT}}", "ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM" },
                { "{{ORG_UNIT}}", branchName?.ToUpper() ?? "CHI BỘ CƠ SỞ TRỰC THUỘC" },
                { "{{PARTY_CELL}}", branchName?.ToUpper() ?? "CHI BỘ CƠ SỞ TRỰC THUỘC" },
                { "{{DATE_STRING}}", $"Hà Nội, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}" },
                { "{{PERIOD_QUARTER_YEAR}}", $"QUÝ {period.Quarter} NĂM {period.Year}" },
                { "{{QUARTER}}", period.Quarter.ToString() },
                { "{{YEAR}}", period.Year.ToString() }
            };

            ReplacePlaceholders(body, replacements);

            var table = body.Descendants<Table>().FirstOrDefault(t => t.InnerText.Contains("{{R_NAME}}"));
            if (table != null)
            {
                var templateRow = table.Descendants<TableRow>().FirstOrDefault(r => r.InnerText.Contains("{{R_NAME}}"));
                if (templateRow != null)
                {
                    int idx = 1;
                    foreach (var r in records)
                    {
                        var newRow = (TableRow)templateRow.CloneNode(true);
                        var rowReplacements = new Dictionary<string, string>
                        {
                            { "{{R_STT}}", idx.ToString() },
                            { "{{R_NAME}}", r.Member?.FullName ?? "Cán bộ" },
                            { "{{R_POSITION_DEPT}}", $"{r.Member?.PositionTitle} • {r.Department?.Name ?? r.PartyCell?.Name}" },
                            { "{{R_SCORE}}", $"{r.TotalSelfScore:F1}đ" },
                            { "{{R_SELF_GRADE}}", FormatGrade(r.SelfProposedGrade) }
                        };
                        ReplacePlaceholders(newRow, rowReplacements);
                        table.InsertBefore(newRow, templateRow);
                        idx++;
                    }

                    templateRow.Remove();
                }
            }

            EnsureOrientation(body, isLandscape: true);
            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    public static byte[] FillMau13Template(EvaluationPeriod period, List<EvaluationRecord> records, string? branchName, int totalVoters)
    {
        EnsureMasterTemplatesExist();
        var templatePath = GetTemplatePath("Mau_13_BienBanKiemPhieu.docx");
        var templateBytes = File.ReadAllBytes(templatePath);

        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;

            var replacements = new Dictionary<string, string>
            {
                { "{{ORG_PARENT}}", "ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM" },
                { "{{ORG_UNIT}}", branchName?.ToUpper() ?? "CHI BỘ CƠ SỞ TRỰC THUỘC" },
                { "{{PARTY_CELL}}", branchName?.ToUpper() ?? "CHI BỘ CƠ SỞ TRỰC THUỘC" },
                { "{{DATE_STRING}}", $"Hà Nội, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}" },
                { "{{PERIOD_QUARTER_YEAR}}", $"QUÝ {period.Quarter} NĂM {period.Year}" },
                { "{{MEETING_DATE}}", DateTime.Now.ToString("dd/MM/yyyy") },
                { "{{QUARTER}}", period.Quarter.ToString() },
                { "{{YEAR}}", period.Year.ToString() },
                { "{{TOTAL_VOTERS}}", totalVoters > 0 ? totalVoters.ToString() : (records.FirstOrDefault()?.TotalVoters.ToString() ?? "0") }
            };

            ReplacePlaceholders(body, replacements);

            var table = body.Descendants<Table>().FirstOrDefault(t => t.InnerText.Contains("{{V_NAME}}"));
            if (table != null)
            {
                var templateRow = table.Descendants<TableRow>().FirstOrDefault(r => r.InnerText.Contains("{{V_NAME}}"));
                if (templateRow != null)
                {
                    int idx = 1;
                    foreach (var r in records)
                    {
                        int voters = r.TotalVoters > 0 ? r.TotalVoters : totalVoters;
                        if (voters <= 0) voters = 10;
                        double pct = Math.Round(((double)(r.VotesExcellent + r.VotesGood) / voters) * 100, 1);

                        var newRow = (TableRow)templateRow.CloneNode(true);
                        var rowReplacements = new Dictionary<string, string>
                        {
                            { "{{V_STT}}", idx.ToString() },
                            { "{{V_NAME}}", r.Member?.FullName ?? "Cán bộ" },
                            { "{{V_POSITION_DEPT}}", $"{r.Member?.PositionTitle} • {r.Department?.Name ?? r.PartyCell?.Name}" },
                            { "{{V_EXC}}", $"{r.VotesExcellent} phiếu" },
                            { "{{V_GOOD}}", $"{r.VotesGood} phiếu" },
                            { "{{V_SAT}}", $"{r.VotesSatisfactory} phiếu" },
                            { "{{V_UNSAT}}", $"{r.VotesUnsatisfactory} phiếu" },
                            { "{{V_PCT}}", $"{pct}% ({FormatGrade(r.PartyCellProposedGrade)})" }
                        };
                        ReplacePlaceholders(newRow, rowReplacements);
                        table.InsertBefore(newRow, templateRow);
                        idx++;
                    }

                    templateRow.Remove();
                }
            }

            EnsureOrientation(body, isLandscape: false);
            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    /// <summary>Đảm bảo khổ giấy chính xác tuyệt đối (Khổ Ngang Landscape cho bảng biểu nhiều cột, hoặc Khổ Đứng Portrait)</summary>
    public static void EnsureOrientation(Body body, bool isLandscape)
    {
        var sectPr = body.Elements<SectionProperties>().LastOrDefault();
        if (sectPr == null)
        {
            sectPr = new SectionProperties();
            body.Append(sectPr);
        }

        var pageSize = sectPr.GetFirstChild<PageSize>();
        if (pageSize == null)
        {
            pageSize = new PageSize();
            sectPr.PrependChild(pageSize);
        }

        if (isLandscape)
        {
            pageSize.Width = 16838U;
            pageSize.Height = 11906U;
            pageSize.Orient = PageOrientationValues.Landscape;
        }
        else
        {
            pageSize.Width = 11906U;
            pageSize.Height = 16838U;
            pageSize.Orient = PageOrientationValues.Portrait;
        }
    }

    #endregion
}
