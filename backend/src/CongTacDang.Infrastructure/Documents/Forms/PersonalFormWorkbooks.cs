using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using static CongTacDang.Infrastructure.Documents.ExcelFormLayout;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Bản Excel của phiếu cá nhân 01, 02, 09A, 9D, 10: dựng từ cùng lớp dữ liệu với bản Word để hai bản luôn khớp.</summary>
public static class PersonalFormWorkbooks
{
    private const string Dots = "……";
    private const string DateDots = "ngày …… tháng …… năm 20…";
    private const string SignNote = "(Ký, ghi rõ họ tên)";
    private const string EvidenceNote =
        "Lưu ý: Mọi điểm số/mức nhận xét phải có minh chứng; tiêu chí không áp dụng ghi “K/AD” và xử lý trọng số theo Hướng dẫn; "
        + "không tính trùng cùng một kết quả ở nhiều tiêu chí; phần điều chỉnh sau hiệu chuẩn phải nêu rõ căn cứ.";

    private static readonly string[] Form01Columns =
    {
        "STT", "Tên sản phẩm/nhiệm vụ", "Mã SP", "Trục KQ (T1-T6)", "Vai trò", "Trọng số (đ)", "Thời hạn/mốc quý",
        "Chuẩn đạt", "Tiêu chí vượt chuẩn", "Nguồn dữ liệu/minh chứng", "Thẩm quyền xác nhận"
    };

    private static readonly string[] Form02Columns =
    {
        "STT", "Tên sản phẩm/nhiệm vụ", "Trọng số (đ)", "A. Khối lượng (%)", "B. Chất lượng, an toàn (%)", "C. Tiến độ (%)",
        "D. Tổ chức, hiệu quả (%)", "Kết quả SP (%)", "Điểm đạt (đ)", "Ghi nhận vượt chuẩn", "Minh chứng/đường dẫn"
    };

    private static readonly string[] Form09AColumns =
    {
        "TT", "Nội dung tiêu chí", "Đảm bảo (đánh dấu X)", "Không đảm bảo (đánh dấu X)", "Điểm tối đa",
        "Điểm đạt (chấm tối đa nếu đảm bảo; chấm 0 nếu không đảm bảo)", "Tóm tắt kết quả/Tài liệu minh chứng; Ghi chú khác"
    };

    private static readonly string[] Form9DColumns =
    {
        "TT", "Nội dung nhiệm vụ, công việc", "Thời hạn hoàn thành", "Tình hình thực hiện", "Sản phẩm hoàn thành", "Đánh giá tiến độ", "Ghi chú"
    };

    private static readonly string[] Form10Columns =
    {
        "Nội dung thẩm định", "Cá nhân tự chấm", "Thẩm định chính thức", "Chênh lệch", "Căn cứ thẩm định/đối soát"
    };

    public static byte[] Mau01(Mau01Data data, OrganizationTemplateFields org)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 01");
        var last = Form01Columns.Length;
        SetWidths(ws, 5, 34, 9, 10, 10, 9, 13, 22, 22, 20, 16);
        var row = WriteHeader(ws, last, "Mẫu số 01", 3, CompanyHeader(org.CompanyNameUpper, "CƠ QUAN QUẢN LÝ CẤP TRÊN", data.Department), StateHeader(org, data.DateLine)) + 1;
        TitleRow(ws, row++, last, "PHIẾU GIAO / ĐĂNG KÝ SẢN PHẨM, CÔNG VIỆC CHUYÊN MÔN HẰNG QUÝ", bold: true);
        TextRow(ws, row++, last, "(Dùng để thống nhất đầu ra, mốc quý, tiêu chuẩn và trọng số. Thông thường 03-07 sản phẩm/nhóm công việc; tổng trọng số cố định là 70 điểm.)", italic: true);
        InfoRow(ws, row++, last, $"Kỳ đánh giá: Quý {data.Quarter ?? "..."} / Năm {data.Year ?? "20..."}");
        InfoRow(ws, row++, last, $"Họ và tên cán bộ: {data.FullName ?? Dots}", $"Mã số cán bộ: {Dots}");
        InfoRow(ws, row++, last, $"Chức danh /Vị trí công tác: {data.Position ?? Dots}", $"Đơn vị công tác: {data.Department ?? Dots}");
        InfoRow(ws, row++, last, $"Người quản lý trực tiếp: {data.SupervisorName ?? Dots}", $"Chức vụ: {Dots}");
        TextRow(ws, row++, last, "Khối chức danh áp dụng: ☐ Khối 1-Khai thác bay | ☐ Khối 2-Kỹ thuật CNS | ☐ Khối 3-An toàn/An ninh | ☐ Khối 4-Kế hoạch/Tài chính/Dự án | ☐ Khối 5-Công tác Đảng/TCCB/Đoàn thể | ☐ Khối 6-Văn phòng/Đối ngoại");
        TextRow(ws, row++, last, "(Ghi chú: Đăng ký từ 03 đến 07 sản phẩm/nhóm nhiệm vụ trọng tâm; Tổng trọng số cố định = 70 điểm; Hoàn thành trong 05 ngày làm việc đầu quý)", italic: true);

        var head = ++row;
        WriteColumnTitles(ws, head, Form01Columns);
        row = head + 1;
        foreach (var t in data.Tasks)
        {
            SetNumber(ws.Cell(row, 1), t.Order);
            SetText(ws.Cell(row, 2), t.Name);
            SetText(ws.Cell(row, 3), t.Code);
            SetText(ws.Cell(row, 4), t.Axis);
            SetText(ws.Cell(row, 5), t.Role);
            SetNumber(ws.Cell(row, 6), t.Weight);
            SetText(ws.Cell(row, 7), t.Deadline);
            SetText(ws.Cell(row, 8), t.Standard);
            SetText(ws.Cell(row, 9), t.ExceedCriteria);
            SetText(ws.Cell(row, 10), t.Evidence);
            SetText(ws.Cell(row, 11), t.Signer);
            row++;
        }
        TotalRow(ws, row, last, "TỔNG TRỌNG SỐ NHÓM NHIỆM VỤ CHUYÊN MÔN");
        WeightTotal(ws.Cell(row, 6), head + 1, row - 1);
        StyleBody(ws.Range(head + 1, 1, row, last));
        row++;
        TextRow(ws, row, last, EvidenceNote, italic: true);
        row += 2;
        Signatures(ws, row, last, ("CẤP CÓ THẨM QUYỀN PHÊ DUYỆT", SignNote), ("CƠ QUAN TỔ CHỨC CÁN BỘ", SignNote),
            ("NGƯỜI TRỰC TIẾP SỬ DỤNG", SignNote), ("CÁ NHÂN ĐĂNG KÝ", SignNote));

        return Save(workbook, ws);
    }

    public static byte[] Mau02(Mau02Data data, OrganizationTemplateFields org)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 02");
        var last = Form02Columns.Length;
        SetWidths(ws, 5, 34, 9, 11, 12, 10, 12, 11, 10, 14, 22);
        var row = WriteHeader(ws, last, "Mẫu số 02", 3, CompanyHeader(org.CompanyNameUpper, "CƠ QUAN QUẢN LÝ CẤP TRÊN", data.Department), StateHeader(org, data.DateLine)) + 1;
        TitleRow(ws, row++, last, "PHIẾU TỰ ĐÁNH GIÁ KẾT QUẢ THỰC HIỆN SẢN PHẨM, CÔNG VIỆC HẰNG QUÝ", bold: true);
        TextRow(ws, row++, last, "(Sai sót hoặc sự cố chỉ ghi nhận bất lợi khi đã xác định trách nhiệm; hành động báo cáo/dừng/điều chỉnh đúng quy trình vì an toàn không bị trừ điểm)", italic: true);
        InfoRow(ws, row++, last, $"Kỳ đánh giá: Quý {data.Quarter ?? "..."} / Năm {data.Year ?? "20..."}");
        InfoRow(ws, row++, last, $"Họ và tên cán bộ: {data.FullName ?? Dots}");
        InfoRow(ws, row++, last, $"Chức vụ: {data.Position ?? Dots}");
        InfoRow(ws, row++, last, $"Đơn vị công tác: {data.Department ?? Dots}");
        TextRow(ws, row++, last, "(Thang mức tham chiếu A-B-C-D: 100% = Hoàn thành đầy đủ chuẩn giao; 90%-95% = Hoàn thành cơ bản trọn vẹn; 80%-85% = Đạt phần lớn yêu cầu; 65%-75% = Đáp ứng mức tối thiểu/cơ bản; 50%-60% = Chỉ hoàn thành một phần; 0%-45% = Không đạt/sai sót nghiêm trọng)", italic: true);

        var head = ++row;
        WriteColumnTitles(ws, head, Form02Columns);
        row = head + 1;
        foreach (var t in data.Tasks)
        {
            SetNumber(ws.Cell(row, 1), t.Order);
            SetText(ws.Cell(row, 2), t.Name);
            SetNumber(ws.Cell(row, 3), t.Weight);
            SetNumber(ws.Cell(row, 4), t.CriteriaA);
            SetNumber(ws.Cell(row, 5), t.CriteriaB);
            SetNumber(ws.Cell(row, 6), t.CriteriaC);
            SetNumber(ws.Cell(row, 7), t.CriteriaD);
            SetNumber(ws.Cell(row, 8), t.ResultPercent);
            SetNumber(ws.Cell(row, 9), t.Score);
            ws.Cell(row, 10).Value = t.IsExceedStandard ? "Đạt vượt chuẩn" : "-";
            SetText(ws.Cell(row, 11), t.Evidence);
            row++;
        }
        TotalRow(ws, row, last, "TỔNG ĐIỂM SẢN PHẨM CHUYÊN MÔN");
        WeightTotal(ws.Cell(row, 3), head + 1, row - 1);
        if (data.TotalScore != null)
            SetNumber(ws.Cell(row, 9), data.TotalScore);
        else
            ws.Cell(row, 9).Value = "… / 70,0";
        ws.Cell(row, 10).Value = $"{data.ExceedCount ?? "…"} SP";
        StyleBody(ws.Range(head + 1, 1, row, last));
        row++;
        TextRow(ws, row++, last, EvidenceNote, italic: true);
        row++;
        TextRow(ws, row++, last, "Công thức tính:", bold: true);
        TextRow(ws, row++, last, "Kết quả thực hiện sản phẩm (%) = (A × Tỷ trọng A) + (B × Tỷ trọng B) + (C × Tỷ trọng C) + (D × Tỷ trọng D)");
        TextRow(ws, row++, last, "Điểm sản phẩm (đ) = [Kết quả thực hiện sản phẩm (%) × Trọng số sản phẩm] / 100");
        row++;
        Signatures(ws, row, last, ("CẤP CÓ THẨM QUYỀN DUYỆT", SignNote), ("ĐẠI DIỆN LÃNH ĐẠO ĐƠN VỊ", SignNote), ("CÁ NHÂN TỰ ĐÁNH GIÁ", SignNote));

        return Save(workbook, ws);
    }

    public static byte[] Mau09A(Mau09AData data, OrganizationTemplateFields org)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 09A");
        var last = Form09AColumns.Length;
        SetWidths(ws, 6, 48, 10, 11, 9, 14, 30);
        var row = WriteHeader(ws, last, "Mẫu số 09A", 2, PartyHeaderLines(org, data.PartyCell),
            new[] { new ExcelHeaderLine("ĐẢNG CỘNG SẢN VIỆT NAM", Bold: true), new ExcelHeaderLine(DateText(org, data.DateLine), Italic: true) }) + 1;
        TitleRow(ws, row++, last, "PHIẾU TỰ CHẤM ĐIỂM ĐÁNH GIÁ, XẾP LOẠI CHẤT LƯỢNG HẰNG QUÝ", bold: true);
        TitleRow(ws, row++, last, "ĐỐI VỚI CÁ NHÂN GIỮ CHỨC VỤ LÃNH ĐẠO, QUẢN LÝ", bold: true);
        TitleRow(ws, row++, last, $"Quý{data.Quarter ?? " ….."} Năm {data.Year ?? "….."}", bold: false);
        InfoRow(ws, row++, last, $"Họ và tên: {data.FullName ?? Dots}", $"Ngày sinh: {data.DateOfBirth ?? "……/……/…………"}");
        InfoRow(ws, row++, last, $"Chức vụ Đảng: {data.PartyPosition ?? Dots}", $"Chức vụ chính quyền: {data.AdministrativePosition ?? Dots}");
        InfoRow(ws, row++, last, $"Chức vụ đoàn thể: {data.MassPosition ?? Dots}", $"Đơn vị công tác: {data.Department ?? Dots}");
        row++;
        TextRow(ws, row++, last, "I. KẾT QUẢ TỰ ĐÁNH GIÁ THEO CẤU TRÚC 100 ĐIỂM", bold: true);
        TextRow(ws, row++, last, "1. NHÓM TIÊU CHÍ CHUNG (TỐI ĐA 30 ĐIỂM)", bold: true);

        var head = row;
        WriteColumnTitles(ws, head, Form09AColumns);
        row = head + 1;
        double maxTotal = 0;
        foreach (var group in data.Groups)
        {
            SetText(ws.Cell(row, 1), group.Number);
            SetText(ws.Cell(row, 2), group.Name);
            SetNumber(ws.Cell(row, 5), group.MaxScore);
            SetText(ws.Cell(row, 7), group.Note);
            ws.Range(row, 1, row, last).Style.Font.Bold = true;
            if (TryParse(group.MaxScore, out var max))
                maxTotal += max;
            row++;
            foreach (var item in group.Items)
            {
                SetText(ws.Cell(row, 1), item.Code);
                SetText(ws.Cell(row, 2), item.Text);
                SetText(ws.Cell(row, 3), item.Met);
                SetText(ws.Cell(row, 4), item.NotMet);
                SetNumber(ws.Cell(row, 5), item.MaxScore);
                SetNumber(ws.Cell(row, 6), item.Score);
                SetText(ws.Cell(row, 7), item.Note);
                ws.Range(row, 3, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row++;
            }
        }
        ws.Cell(row, 2).Value = "Tổng";
        ws.Cell(row, 5).Value = maxTotal;
        SetNumber(ws.Cell(row, 6), data.GeneralScore);
        ws.Range(row, 1, row, last).Style.Font.Bold = true;
        StyleBody(ws.Range(head + 1, 1, row, last));
        row += 2;

        TextRow(ws, row++, last, "2. NHÓM KẾT QUẢ THỰC HIỆN NHIỆM VỤ CHUYÊN MÔN (TỐI ĐA 70 ĐIỂM )", bold: true);
        TextRow(ws, row++, last, $"Tổng điểm thực hiện sản phẩm chuyên môn (từ Mẫu số 02 chuyển sang): {data.TasksScore ?? "…………"} / 70,0 điểm.");
        TextRow(ws, row++, last, $"Số lượng sản phẩm đạt \"Ghi nhận vượt chuẩn\": {data.ExceedCount ?? "…………"} / {data.TaskCount ?? "…………"} sản phẩm (đạt tỷ lệ: {data.ExceedPercent ?? "……"}%).");
        row++;
        TextRow(ws, row++, last, "TỔNG HỢP ĐIỂM VÀ TỰ ĐỀ XUẤT MỨC XẾP LOẠI", bold: true);

        // Bảng tổng hợp sáu cột đặt từ cột 2 (cột 1 hẹp dành cho số thứ tự).
        var summaryHead = row;
        var summaryTitles = new[] { "Điểm Nhóm chung (TC / 30)", "Điểm Nhiệm vụ (CV / 70)", "TỔNG ĐIỂM (100)", "Tỷ lệ SP vượt chuẩn (%)", "Tự nhận xét điều kiện bắt buộc", "Mức tự đề xuất xếp loại" };
        for (var i = 0; i < summaryTitles.Length; i++)
            ws.Cell(summaryHead, 2 + i).Value = summaryTitles[i];
        StyleHeaderRow(ws.Range(summaryHead, 2, summaryHead, last));
        row++;
        SetNumber(ws.Cell(row, 2), data.GeneralScore);
        SetNumber(ws.Cell(row, 3), data.TasksScore);
        SetNumber(ws.Cell(row, 4), data.TotalScore);
        SetNumber(ws.Cell(row, 5), data.ExceedPercent != null ? data.ExceedPercent + "%" : null);
        ws.Cell(row, 7).Value = string.Join("\n",
            $"{data.CheckExcellent} Hoàn thành xuất sắc", $"{data.CheckGood} Hoàn thành tốt",
            $"{data.CheckDone} Hoàn thành nhiệm vụ", $"{data.CheckFailed} Không hoàn thành");
        StyleBody(ws.Range(row, 2, row, last));
        ws.Row(row).Height = 62;
        row++;
        TextRow(ws, row++, last, EvidenceNote, italic: true);
        row++;
        TextRow(ws, row++, last, "B. CAM KẾT TRÁCH NHIỆM", bold: true);
        TextRow(ws, row++, last, "Tôi xin cam đoan những nội dung tự đánh giá, điểm số và hồ sơ minh chứng trên là hoàn toàn trung thực, chính xác.");
        row++;
        SignatureRow(ws, row, last, "XÁC NHẬN CỦA CHI BỘ", "(Xác lập thời điểm, ký, ghi rõ họ tên)", "CÁ NHÂN TỰ ĐÁNH GIÁ", SignNote);

        return Save(workbook, ws);
    }

    public static byte[] Mau9D(Mau9DData data, OrganizationTemplateFields org)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 9D");
        var last = Form9DColumns.Length;
        SetWidths(ws, 8, 34, 14, 26, 24, 16, 16);
        var right = new List<ExcelHeaderLine> { new("ĐẢNG CỘNG SẢN VIỆT NAM", Bold: true) };
        if (data.HasDateLine)
            right.Add(new ExcelHeaderLine(DateText(org, data.DateLine), Italic: true));
        var row = WriteHeader(ws, last, "Mẫu số 09D", 2, PartyHeaderLines(org, data.PartyCell), right) + 1;
        TitleRow(ws, row++, last, "PHỤ LỤC", bold: true);
        TitleRow(ws, row++, last, $"KẾT QUẢ THỰC HIỆN NHIỆM VỤ, CÔNG VIỆC ĐƯỢC GIAO TRONG QUÝ{data.Quarter ?? "…."} NĂM{data.Year ?? "….."}", bold: true);
        TitleRow(ws, row++, last, "-----", bold: false);
        InfoRow(ws, row++, last, $"Họ và tên: {data.FullName ?? "…"}");
        if (data.PositionsOneLine)
        {
            InfoRow(ws, row++, last, $"Chức vụ (Đảng, Đoàn thể, Chính quyền): {data.Positions ?? "…"}");
        }
        else
        {
            InfoRow(ws, row++, last, $"Chức vụ: Chức vụ Đảng: {data.PartyPosition ?? "…"}");
            InfoRow(ws, row++, last, $"Chức vụ chính quyền: {data.AdministrativePosition ?? "…"}");
            InfoRow(ws, row++, last, $"Chức vụ đoàn thể: {data.MassPosition ?? "…"}");
        }
        var company = org.CompanyName ?? "…";
        InfoRow(ws, row++, last, "Đơn vị công tác: " + (data.HasDepartment ? $"{data.Department ?? "…"}, {company}" : company));
        InfoRow(ws, row++, last, $"Chi bộ, Đảng bộ đang sinh hoạt: {data.CellName ?? "…"}");
        row++;
        TitleRow(ws, row++, last, "KẾT QUẢ THỰC HIỆN", bold: true);

        var head = row;
        for (var c = 1; c <= last; c++)
        {
            ws.Cell(head, c).Value = Form9DColumns[c - 1];
            ws.Cell(head + 1, c).Value = $"({c})";
        }
        StyleHeader(ws.Range(head, 1, head + 1, last));
        row = head + 2;
        foreach (var axis in data.Axes)
        {
            SetText(ws.Cell(row, 1), axis.Label);
            SetText(ws.Cell(row, 2), axis.Name);
            SetText(ws.Cell(row, 3), axis.Deadline);
            SetText(ws.Cell(row, 4), axis.Status);
            SetText(ws.Cell(row, 5), axis.Product);
            SetText(ws.Cell(row, 6), axis.Progress);
            SetText(ws.Cell(row, 7), axis.Note);
            row++;
        }
        if (row > head + 2)
            StyleBody(ws.Range(head + 2, 1, row - 1, last));
        row++;
        SignatureRow(ws, row, last, "XÁC NHẬN CỦA CHI BỘ", "(Xác lập thời điểm, ký, ghi rõ họ tên)", "CÁ NHÂN TỰ ĐÁNH GIÁ", SignNote);

        return Save(workbook, ws);
    }

    public static byte[] Mau10(Mau10Data data, OrganizationTemplateFields org)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 10");
        var last = Form10Columns.Length;
        SetWidths(ws, 44, 18, 20, 14, 34);
        var left = new[]
        {
            new ExcelHeaderLine(org.CompanyNameUpper ?? "CÔNG TY …"),
            new ExcelHeaderLine(org.AppraisalUnitNameUpper ?? "CƠ QUAN THẨM ĐỊNH", Bold: true)
        };
        var row = WriteHeader(ws, last, "Mẫu số 10", 2, left, StateHeader(org, data.DateLine)) + 1;
        TitleRow(ws, row++, last, "PHIẾU THẨM ĐỊNH, NHẬN XÉT, ĐỀ XUẤT XẾP LOẠI VÀ GHI NHẬN GIẢI TRÌNH", bold: true);
        InfoRow(ws, row++, last, $"Kỳ đánh giá: Quý {data.Quarter ?? "..."} / Năm {data.Year ?? "20..."}");
        InfoRow(ws, row++, last, $"Họ và tên cán bộ được đánh giá: {data.FullName ?? Dots}", $"Chức vụ: {data.Position ?? Dots}");
        InfoRow(ws, row++, last, $"Đơn vị công tác: {data.Department ?? Dots}");
        row++;
        TextRow(ws, row++, last, "I. KẾT QUẢ ĐỐI SOÁT VÀ THẨM ĐỊNH ĐIỂM SỐ", bold: true);

        var head = row;
        WriteColumnTitles(ws, head, Form10Columns);
        row = head + 1;
        void ScoreRow(string title, string? self, string? appraisal, string? diff)
        {
            ws.Cell(row, 1).Value = title;
            SetNumber(ws.Cell(row, 2), self);
            if (appraisal != null) SetNumber(ws.Cell(row, 3), appraisal); else ws.Cell(row, 3).Value = "…";
            if (diff != null) SetNumber(ws.Cell(row, 4), diff); else ws.Cell(row, 4).Value = "…";
            row++;
        }
        ScoreRow("1. Nhóm tiêu chí chung (TC / 30đ)", data.GeneralSelfScore, data.GeneralAppraisalScore, data.GeneralDiff);
        ScoreRow("2. Nhóm sản phẩm chuyên môn (CV / 70đ)", data.TasksSelfScore, data.TasksAppraisalScore, data.TasksDiff);
        ScoreRow("TỔNG ĐIỂM (100đ)", data.TotalSelfScore, data.TotalAppraisalScore, data.TotalDiff);
        ws.Range(row - 1, 1, row - 1, last).Style.Font.Bold = true;
        // Mức chất lượng chỉ có ở thẩm định nên cột tự chấm để trống.
        ws.Cell(row, 1).Value = "3. Đánh giá 3 Mức chất lượng nhiệm vụ";
        ws.Cell(row, 3).Value = data.TaskLevels ?? "Mức 1: … SP\nMức 2: … SP\nMức 3: … SP";
        ws.Cell(row, 3).Style.Alignment.WrapText = true;
        row++;
        foreach (var (title, value) in new[]
        {
            ("4. Kiểm tra 5 Điều kiện Hoàn thành xuất sắc", "☐ Đạt  ☐ Không"),
            ("5. Trần xếp loại / điều kiện bắt buộc về an toàn bắt buộc", "Không vi phạm")
        })
        {
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 2).Value = value;
            ws.Cell(row, 3).Value = value;
            row++;
        }
        StyleBody(ws.Range(head + 1, 1, row - 1, last));
        TextRow(ws, row++, last, EvidenceNote, italic: true);
        row++;
        TextRow(ws, row++, last, "II. Ý KIẾN CỦA CÁC BÊN VÀ KẾT QUẢ GIẢI TRÌNH", bold: true);
        TextRow(ws, row++, last, $"Ý kiến của Người trực tiếp sử dụng cán bộ: {data.SupervisorComment ?? "…………"}");
        TextRow(ws, row++, last, $"Nội dung giải trình của cá nhân (khi chênh lệch ≥ 5 điểm): {data.AppraisalExplanation ?? "…………"}");
        TextRow(ws, row++, last, $"Kết luận của Cơ quan thẩm định: {data.AppraisalComment ?? "…………"}");
        TextRow(ws, row++, last, "Mức xếp loại đề xuất trình cấp có thẩm quyền:", bold: true);
        TextRow(ws, row++, last,
            $"{data.CheckExcellent} Hoàn thành xuất sắc      {data.CheckGood} Hoàn thành tốt      {data.CheckDone} Hoàn thành nhiệm vụ      {data.CheckFailed} Không hoàn thành");
        row++;
        Signatures(ws, row, last, ("CẤP CÓ THẨM QUYỀN DUYỆT", SignNote), ("NGƯỜI TRỰC TIẾP SỬ DỤNG", SignNote), ("ĐẠI DIỆN CƠ QUAN THẨM ĐỊNH", SignNote));

        return Save(workbook, ws, landscape: false);
    }

    /// <summary>Tiêu đề trái Mẫu 01/02: tên công ty (in hoa) rồi đơn vị của hồ sơ.</summary>
    private static ExcelHeaderLine[] CompanyHeader(string? companyUpper, string fallback, string? department) => new[]
    {
        new ExcelHeaderLine(companyUpper ?? fallback),
        new ExcelHeaderLine(department ?? "................", Bold: true)
    };

    /// <summary>Tiêu đề trái Mẫu 09A/9D: tên Đảng bộ rồi tên Chi bộ của hồ sơ.</summary>
    private static ExcelHeaderLine[] PartyHeaderLines(OrganizationTemplateFields org, string? partyCell) => new[]
    {
        new ExcelHeaderLine(org.PartyCommitteeName ?? "ĐẢNG BỘ…"),
        new ExcelHeaderLine(partyCell ?? "CHI BỘ …", Bold: true)
    };

    private static ExcelHeaderLine[] StateHeader(OrganizationTemplateFields org, string? dateLine) => new[]
    {
        new ExcelHeaderLine("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", Bold: true),
        new ExcelHeaderLine("Độc lập – Tự do – Hạnh phúc", Bold: true),
        new ExcelHeaderLine(DateText(org, dateLine), Italic: true)
    };

    /// <summary>Địa danh lấy từ cài đặt đơn vị, ngày lấy từ lớp dữ liệu (chưa có → dòng chấm như bản Word).</summary>
    private static string DateText(OrganizationTemplateFields org, string? dateLine) => $"{org.Location ?? Dots}, {dateLine ?? DateDots}";

    private static void WriteColumnTitles(IXLWorksheet ws, int row, IReadOnlyList<string> titles)
    {
        for (var c = 1; c <= titles.Count; c++)
            ws.Cell(row, c).Value = titles[c - 1];
        StyleHeaderRow(ws.Range(row, 1, row, titles.Count));
    }

    private static void TotalRow(IXLWorksheet ws, int row, int lastColumn, string title)
    {
        ws.Cell(row, 1).Value = "CỘNG";
        ws.Cell(row, 2).Value = title;
        ws.Range(row, 1, row, lastColumn).Style.Font.Bold = true;
    }

    /// <summary>Tổng trọng số bằng công thức để người dùng sửa trên Excel vẫn đúng; chưa có dòng → in "70,0" như bản Word.</summary>
    private static void WeightTotal(IXLCell cell, int firstRow, int lastRow)
    {
        if (lastRow < firstRow)
        {
            SetNumber(cell, "70,0");
            return;
        }
        var column = cell.Address.ColumnLetter;
        cell.FormulaA1 = $"SUM({column}{firstRow}:{column}{lastRow})";
        cell.Style.NumberFormat.Format = "0.0";
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void SetWidths(IXLWorksheet ws, params double[] widths)
    {
        for (var i = 0; i < widths.Length; i++)
            ws.Column(i + 1).Width = widths[i];
    }

    private static byte[] Save(XLWorkbook workbook, IXLWorksheet ws, bool landscape = true)
    {
        ws.PageSetup.PageOrientation = landscape ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        ws.PageSetup.FitToPages(1, 0);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
