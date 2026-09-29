using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Reports;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Kết xuất biểu mẫu HD03 (Word: 01, 02, 07, 08, 10, 11, 12, 13, 16; bảng tính: 14, 15A, 15B) và báo cáo nội bộ.
/// Bố cục, tiêu đề, cột đúng nguyên văn biểu mẫu gốc (<c>docs/2.03-HD.TVDU … (Bieu mau).docx</c>, PDF HD03 tr.72–78).
/// </summary>
public class ReportService : IReportService
{
    private readonly CongTacDangDbContext _db;
    private readonly IUserRepository _userRepo;
    private readonly IOrganizationRepository _orgRepo;
    private readonly IEvaluationRepository _evalRepo;
    private readonly IWordTemplateStore _templates;
    private readonly IPdfConverter _pdfConverter;
    private readonly IOrganizationSettingsService _orgSettings;
    private readonly IUnitOfWork _unitOfWork;

    public ReportService(
        CongTacDangDbContext db,
        IUserRepository userRepo,
        IOrganizationRepository orgRepo,
        IEvaluationRepository evalRepo,
        IWordTemplateStore templates,
        IPdfConverter pdfConverter,
        IOrganizationSettingsService orgSettings,
        IUnitOfWork unitOfWork)
    {
        _db = db;
        _userRepo = userRepo;
        _orgRepo = orgRepo;
        _evalRepo = evalRepo;
        _templates = templates;
        _pdfConverter = pdfConverter;
        _orgSettings = orgSettings;
        _unitOfWork = unitOfWork;
    }

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    #region Báo cáo nội bộ

    public async Task<ReportFileResult> ExportCadresReportAsync(CongTacDang.Application.Common.Security.ScopeFilter scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        // T-61: chỉ cán bộ thuộc phạm vi report.export của người yêu cầu (Toàn công ty / Phòng / Chi bộ).
        var members = (await _userRepo.GetAllWithDetailsAsync())
            .Where(m => scope.Matches(null, m.DepartmentId, m.PartyCellId))
            .ToList();
        var partyPositions = await PartyPositionNamesAsync(members.Select(m => m.Id), DateTime.UtcNow);
        var org = await _orgSettings.GetAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Danh sách cán bộ");

        ws.Cell("A1").Value = org.PartyCommitteeName;
        ws.Cell("A1").Style.Font.Bold = true;

        ws.Cell("A3").Value = $"DANH SÁCH CÁN BỘ LÃNH ĐẠO, QUẢN LÝ {org.ShortName.ToUpperInvariant()}";
        ws.Range("A3:H3").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell("A4").Value = "(Báo cáo nội bộ — không thuộc danh mục biểu mẫu HD03)";
        ws.Range("A4:H4").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        var headers = new[] { "STT", "Họ và tên", "Số thẻ Đảng", "Chi bộ", "Chức vụ Đảng", "Đơn vị chuyên môn", "Chức danh chính quyền", "Trạng thái" };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(5, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        var row = 6;
        var stt = 1;
        foreach (var m in members)
        {
            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = m.FullName;
            ws.Cell(row, 3).Value = m.PartyCardNumber ?? "";
            ws.Cell(row, 4).Value = m.PartyCell?.Name ?? "";
            ws.Cell(row, 5).Value = partyPositions.GetValueOrDefault(m.Id) ?? "";
            ws.Cell(row, 6).Value = m.Department?.Name ?? "";
            ws.Cell(row, 7).Value = m.PositionTitle ?? "";
            ws.Cell(row, 8).Value = m.IsActive ? "Đang hoạt động" : "Đã khóa";
            for (var c = 1; c <= 8; c++)
                ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new ReportFileResult
        {
            FileBytes = stream.ToArray(),
            FileName = $"DanhSach_CanBo_{SafeName(org.ShortName, "DonVi")}.xlsx"
        };
    }

    /// <summary>
    /// Báo cáo nội bộ — kiểm soát tỷ lệ Hoàn thành xuất sắc theo tổ chức Đảng (trần theo bộ tiêu chí của kỳ, HD03 mục III.6).
    /// Không phải biểu mẫu HD03 (trước đây mang nhầm tên "Mẫu 15").
    /// </summary>
    public async Task<ReportFileResult> ExportExcellentQuotaReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
    {
        var (activePeriod, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);
        var cells = (await _orgRepo.GetPartyCellsWithMembersAsync())
            .Where(c => scopeCell == null || c.Id == scopeCell.Id)
            .ToList();
        var org = await _orgSettings.GetAsync();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Kiểm soát tỷ lệ xuất sắc");
        ws.Cell("A1").Value = org.SuperiorPartyName;
        ws.Cell("A2").Value = org.PartyCommitteeName;
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A2").Style.Font.Bold = true;

        ws.Cell("A4").Value = $"BÁO CÁO NỘI BỘ — KIỂM SOÁT TỶ LỆ HOÀN THÀNH XUẤT SẮC NHIỆM VỤ THEO TỔ CHỨC ĐẢNG ({ExcelTitleSuffix(activePeriod, scopeCell)})";
        ws.Range("A4:H4").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell("A5").Value = "(Không thuộc danh mục biểu mẫu HD03. Trần tỷ lệ theo bộ tiêu chí của kỳ — HD03 mục III.6.)";
        ws.Range("A5:H5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        var headers = new[]
        {
            "STT", "Tổ chức Đảng", "Tổng số cán bộ", "Số hoàn thành tốt trở lên", "Số xuất sắc tối đa",
            "Đề xuất xuất sắc", "Tỷ lệ thực tế (%)", "Kết luận"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(7, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        var row = 8;
        var stt = 1;
        foreach (var c in cells)
        {
            var cellRecords = records.Where(r => r.Member?.PartyCellId == c.Id || r.PartyCellId == c.Id).ToList();
            var total = cellRecords.Count > 0 ? cellRecords.Count : c.Members.Count;
            var goodOrBetter = cellRecords.Count(IsGoodOrBetter);
            var proposedExcellent = cellRecords.Count(IsProposedExcellent);
            var maxAllowed = EvaluationScoring.ExcellentQuota(goodOrBetter, proposedExcellent, QuotaRule(activePeriod));
            var actualPercent = goodOrBetter > 0 ? Math.Round((double)proposedExcellent / goodOrBetter * 100.0, 1) : 0.0;
            var isExceeding = proposedExcellent > maxAllowed;

            ws.Cell(row, 1).Value = stt++;
            ws.Cell(row, 2).Value = c.Name;
            ws.Cell(row, 3).Value = total;
            ws.Cell(row, 4).Value = goodOrBetter;
            ws.Cell(row, 5).Value = maxAllowed;
            ws.Cell(row, 6).Value = proposedExcellent;
            ws.Cell(row, 7).Value = $"{actualPercent}%";
            ws.Cell(row, 8).Value = isExceeding ? "Vượt trần" : "Trong giới hạn";
            if (isExceeding)
            {
                ws.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
                ws.Cell(row, 8).Style.Font.Bold = true;
            }
            for (var col = 1; col <= 8; col++)
                ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            row++;
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await ToResultAsync(stream.ToArray(), XlsxMimeType, ExcelFileName("BaoCaoNoiBo_KiemSoatTyLeXuatSac", activePeriod, scopeCell), format);
    }

    #endregion

    #region Mẫu 14, 15A, 15B (bảng tính — HD03 V.1: lập trên file Excel)

    /// <summary>Tiêu đề và cột Mẫu 14 đúng nguyên văn biểu mẫu gốc.</summary>
    private static readonly string[] Form14Columns =
    {
        "TT", "Họ và tên", "Chức vụ, đơn vị công tác (đảng, chính quyền, đoàn thể)", "Mã chức danh",
        "Điểm nhóm tiêu chí chung", "Điểm nhóm tiêu chí kết quả thực hiện chức trách, NV được giao", "Mức tự xếp loại đề xuất",
        "Tập thể lãnh đạo phòng/trung tâm, ban, đơn vị đề xuất",
        "Cơ quan tham mưu về công tác TCCB hoặc tổ chức, cá nhân được giao nhiệm vụ thực hiện tổng hợp, thẩm định và đề xuất",
        "Cấp có thẩm quyền trực tiếp sử dụng cán bộ đề xuất",
        "Ban Thường vụ Đảng ủy TCT hoặc đảng ủy, chi ủy cơ sở quyết định, phê duyệt mức xếp loại",
        "Tóm tắt căn cứ, cơ sở lý do trong trường hợp đề xuất mức xếp loại hoàn thành xuất sắc hoặc mức xếp loại không hoàn thành nhiệm vụ hoặc các nội dung khác (nếu có)",
        "Đề xuất nội dung liên quan về công tác cán bộ (nếu có)"
    };

    /// <summary>
    /// Mẫu 14 — "DANH SÁCH ĐÁNH GIÁ VÀ ĐỀ XUẤT XẾP LOẠI QUÝ … NĂM … ĐỐI VỚI CÁN BỘ THUỘC DIỆN … QUYẾT ĐỊNH, PHÊ DUYỆT MỨC XẾP LOẠI".
    /// Mỗi cấp quyết định (thẩm quyền ảnh chụp trên hồ sơ) một trang tính: đảng ủy cơ sở, Ban Thường vụ Đảng ủy Tổng công ty.
    /// </summary>
    public async Task<ReportFileResult> ExportForm14ReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
    {
        var (period, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);
        var at = PositionDate(period);
        var header = await ReportHeaderAsync(scopeCell);
        var org = await _orgSettings.GetAsync();
        var held = await HeldPositionsAsync(records.Select(r => r.MemberId), at);

        using var workbook = new XLWorkbook();
        foreach (var (authority, sheetName, decider) in new[]
        {
            (ApprovalAuthority.CoSo, "Mẫu 14 - ĐU cơ sở", "ĐẢNG ỦY CƠ SỞ"),
            (ApprovalAuthority.CapTren, "Mẫu 14 - BTV ĐUTCT", "BAN THƯỜNG VỤ ĐẢNG ỦY TỔNG CÔNG TY")
        })
        {
            var ws = workbook.Worksheets.Add(sheetName);
            var last = Form14Columns.Length;
            WriteFormHeader(ws, last, "Mẫu 14", header, org.Location, leftColumns: 4);
            TitleRow(ws, 5, last, $"DANH SÁCH ĐÁNH GIÁ VÀ ĐỀ XUẤT XẾP LOẠI QUÝ {QuarterYear(period, " NĂM ")}", bold: true);
            TitleRow(ws, 6, last, $"ĐỐI VỚI CÁN BỘ THUỘC DIỆN {decider} QUYẾT ĐỊNH, PHÊ DUYỆT MỨC XẾP LOẠI", bold: true);

            // Hai dòng tiêu đề: cột 5–7 gộp nhóm "Cá nhân tự chấm điểm, đề xuất mức xếp loại".
            const int h1 = 8, h2 = 9, numbers = 10;
            for (var c = 1; c <= last; c++)
            {
                if (c is >= 5 and <= 7)
                {
                    ws.Cell(h2, c).Value = Form14Columns[c - 1];
                    continue;
                }
                ws.Cell(h1, c).Value = Form14Columns[c - 1];
                ws.Range(h1, c, h2, c).Merge();
            }
            ws.Cell(h1, 5).Value = "Cá nhân tự chấm điểm, đề xuất mức xếp loại";
            ws.Range(h1, 5, h1, 7).Merge();
            for (var c = 1; c <= last; c++)
                ws.Cell(numbers, c).Value = c;
            StyleHeader(ws.Range(h1, 1, numbers, last));

            var row = numbers + 1;
            var stt = 1;
            foreach (var r in records.Where(r => r.ApprovalAuthority == authority))
            {
                var positions = held.GetValueOrDefault(r.MemberId) ?? new List<Position>();
                var values = new object?[]
                {
                    stt++,
                    r.Member?.FullName,
                    PositionAndUnit(r, positions),
                    PositionRules.PersonStatCode(positions.Select(p => new HeldPosition(p.Name, p.StatCode, p.DefaultApprovalAuthority))),
                    r.SelfScoredAt.HasValue ? r.GeneralCriteriaScore : null,
                    r.SelfScoredAt.HasValue ? r.TasksScore : null,
                    GradeOrBlank(r.SelfProposedGrade),
                    GradeOrBlank(r.CollectiveProposedGrade),
                    GradeOrBlank(r.AppraisalProposedGrade),
                    GradeOrBlank(r.DirectorProposedGrade),
                    GradeOrBlank(r.FinalGrade),
                    Rationale(r),
                    r.CadreWorkProposal
                };
                for (var c = 1; c <= last; c++)
                {
                    var cell = ws.Cell(row, c);
                    switch (values[c - 1])
                    {
                        case int n: cell.Value = n; break;
                        case double d: cell.Value = d; break;
                        case string s: cell.Value = s; break;
                    }
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Alignment.WrapText = true;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                }
                row++;
            }

            row++;
            ws.Cell(row++, 1).Value = "Ghi chú: - Mẫu này được áp dụng chung đối với tất cả các cấp tại cột 8, 9, 10, 11.";
            ws.Cell(row++, 1).Value = "- Cột 4 (Mã chức danh): Ghi mã chức danh theo Mẫu 15.";
            ws.Cell(row++, 1).Value = "- Cột 12, 13 ghi ý kiến của cấp đề xuất hoặc cấp quyết định, phê duyệt mức xếp loại.";
            row++;
            SignatureRow(ws, row, last, "NGƯỜI LẬP", "(Ký, ghi rõ họ tên)",
                "T/M CẤP ỦY (HOẶC TẬP THỂ LÃNH ĐẠO, QUẢN LÝ CƠ QUAN, ĐƠN VỊ)", "(Ký, ghi rõ họ tên và đóng dấu)");

            ws.Column(1).Width = 5;
            ws.Column(2).Width = 22;
            ws.Column(3).Width = 30;
            ws.Column(4).Width = 8;
            for (var c = 5; c <= last; c++)
                ws.Column(c).Width = c >= 12 ? 30 : 14;
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await ToResultAsync(stream.ToArray(), XlsxMimeType, Hd03FileName("14", org.ShortName, scopeCell, period, ".xlsx"), format);
    }

    /// <summary>Mẫu 15A (HD03 tr.73–74): mã chức danh M1–M16, đối tượng đề nghị BTVĐUTCT quyết định.</summary>
    public Task<ReportFileResult> ExportForm15AReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
        => ExportStatCodeFormAsync(periodId, partyCellId, format, Hd03FormCatalog.Form15AFirstCode, Hd03FormCatalog.Form15ALastCode, "15A",
            "(Đối tượng đề nghị BTVĐUTCT quyết định, phê duyệt mức xếp loại)", "Mức xếp loại đề nghị BTVĐUTCT quyết định");

    /// <summary>Mẫu 15B (HD03 tr.75–76): mã chức danh M17–M26, đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định.</summary>
    public Task<ReportFileResult> ExportForm15BReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
        => ExportStatCodeFormAsync(periodId, partyCellId, format, Hd03FormCatalog.Form15BFirstCode, Hd03FormCatalog.Form15BLastCode, "15B",
            "(Đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định, phê duyệt mức xếp loại)", "Mức xếp loại");

    /// <summary>
    /// Dựng Mẫu 15A/15B đúng bố cục biểu mẫu: TT | Đối tượng | Mã chức danh | Tổng số | 5 mức | Tỷ lệ % xuất sắc trong số tốt trở lên |
    /// Ghi chú; một dòng cho mỗi mã; dòng "Tổng cộng"; ghi chú "ưu tiên nhóm chức danh có thứ tự đứng trước". Cán bộ chưa có mã
    /// liệt kê ở trang tính "Kiểm tra dữ liệu".
    /// </summary>
    private async Task<ReportFileResult> ExportStatCodeFormAsync(Guid? periodId, Guid? partyCellId, ReportFormat format,
        int firstCode, int lastCode, string formName, string subject, string gradeGroupTitle)
    {
        var (period, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);
        var statCodes = await PersonStatCodesAsync(records.Select(r => r.MemberId), PositionDate(period));
        var header = await ReportHeaderAsync(scopeCell);
        var org = await _orgSettings.GetAsync();

        var coded = records
            .Select(r => (Record: r, Order: PositionRules.StatCodeOrder(statCodes.GetValueOrDefault(r.MemberId))))
            .ToList();
        var inForm = coded.Where(x => x.Order >= firstCode && x.Order <= lastCode).ToList();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"Mẫu {formName}");
        const int last = 11;
        WriteFormHeader(ws, last, $"Mẫu {formName}", header, org.Location, leftColumns: 3);
        TitleRow(ws, 5, last, $"TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ QUÝ {QuarterYear(period, " NĂM ")}", bold: true);
        TitleRow(ws, 6, last, subject, bold: true);
        TitleRow(ws, 7, last, "-----", bold: false);

        const int h1 = 8, h2 = 9, numbers = 10;
        var titles = new[]
        {
            "TT", "Đối tượng đánh giá, xếp loại", "Mã chức danh", "Tổng số đối tượng đánh giá, xếp loại", null, null, null, null, null,
            "Tỷ lệ % xếp loại xuất sắc trong số xếp loại tốt trở lên", "Ghi chú"
        };
        for (var c = 1; c <= last; c++)
        {
            if (titles[c - 1] == null)
                continue;
            ws.Cell(h1, c).Value = titles[c - 1];
            ws.Range(h1, c, h2, c).Merge();
        }
        ws.Cell(h1, 5).Value = gradeGroupTitle;
        ws.Range(h1, 5, h1, 9).Merge();
        var gradeTitles = new[] { "Hoàn thành xuất sắc nhiệm vụ", "Hoàn thành tốt nhiệm vụ", "Hoàn thành nhiệm vụ", "Không hoàn thành nhiệm vụ", "Chưa xếp loại" };
        for (var i = 0; i < gradeTitles.Length; i++)
            ws.Cell(h2, 5 + i).Value = gradeTitles[i];
        for (var c = 1; c <= last; c++)
            ws.Cell(numbers, c).Value = c;
        StyleHeader(ws.Range(h1, 1, numbers, last));

        var row = numbers + 1;
        void WriteRow(string order, string name, string code, IReadOnlyCollection<EvaluationRecord> group, bool bold)
        {
            var counts = CountGrades(group);
            ws.Cell(row, 1).Value = order;
            ws.Cell(row, 2).Value = name;
            ws.Cell(row, 3).Value = code;
            ws.Cell(row, 4).Value = counts.Total;
            ws.Cell(row, 5).Value = counts.Excellent;
            ws.Cell(row, 6).Value = counts.Good;
            ws.Cell(row, 7).Value = counts.Satisfactory;
            ws.Cell(row, 8).Value = counts.Unsatisfactory;
            ws.Cell(row, 9).Value = counts.NotRated;
            if (counts.ExcellentPercent is { } pct)
                ws.Cell(row, 10).Value = FormText.Number(pct, 1) + "%";
            var range = ws.Range(row, 1, row, last);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            range.Style.Font.Bold = bold;
            ws.Cell(row, 2).Style.Alignment.WrapText = true;
            row++;
        }

        for (var order = firstCode; order <= lastCode; order++)
        {
            var code = "M" + order.ToString(CultureInfo.InvariantCulture);
            var group = inForm.Where(x => x.Order == order).Select(x => x.Record).ToList();
            WriteRow((order - firstCode + 1).ToString(CultureInfo.InvariantCulture),
                Hd03FormCatalog.StatCodeSubjects.GetValueOrDefault(code) ?? code, code, group, bold: false);
        }
        WriteRow(string.Empty, "Tổng cộng", string.Empty, inForm.Select(x => x.Record).ToList(), bold: true);

        row++;
        ws.Cell(row, 1).Value = "Ghi chú: Để tránh trùng lặp số liệu thì ưu tiên thống kê theo nhóm chức danh có thứ tự đứng trước.";
        ws.Range(row, 1, row, last).Merge().Style.Font.SetItalic(true);
        row += 2;
        SignatureRow(ws, row, last, "NGƯỜI LẬP", "(ký, ghi rõ họ tên)", "T/M ĐẢNG ỦY (CHI BỘ)", "(ký, ghi rõ họ tên và đóng dấu)");

        ws.Column(1).Width = 5;
        ws.Column(2).Width = 45;
        ws.Column(3).Width = 9;
        for (var c = 4; c <= 9; c++)
            ws.Column(c).Width = 12;
        ws.Column(10).Width = 16;
        ws.Column(11).Width = 12;
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;

        // Kiểm tra dữ liệu (không in): cán bộ chưa có chức vụ mang mã chức danh thống kê không được đưa vào Mẫu 15A/15B.
        var withoutCode = coded.Where(x => x.Order == null).Select(x => x.Record).ToList();
        var check = workbook.Worksheets.Add("Kiểm tra dữ liệu");
        check.Cell(1, 1).Value = withoutCode.Count == 0
            ? "Mọi cán bộ trong phạm vi đã có mã chức danh thống kê."
            : $"Có {withoutCode.Count} cán bộ chưa có chức vụ mang mã chức danh thống kê nên không được đưa vào Mẫu 15A/15B. "
              + "Hãy cập nhật chức vụ của cán bộ hoặc mã thống kê trong danh mục chức vụ.";
        var checkRow = 3;
        foreach (var r in withoutCode)
            check.Cell(checkRow++, 1).Value = r.Member?.FullName ?? r.MemberId.ToString();
        check.Column(1).Width = 60;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await ToResultAsync(stream.ToArray(), XlsxMimeType, Hd03FileName(formName, org.ShortName, scopeCell, period, ".xlsx"), format);
    }

    /// <summary>Tiêu đề trái/phải dùng chung: dòng mẫu, "ĐẢNG BỘ …" / "ĐẢNG CỘNG SẢN VIỆT NAM", "ĐẢNG ỦY (CHI BỘ) …" / địa danh, ngày.</summary>
    private static void WriteFormHeader(IXLWorksheet ws, int lastColumn, string formLabel, PartyHeader header, string location, int leftColumns)
    {
        ws.Cell(1, lastColumn).Value = formLabel;
        ws.Cell(1, lastColumn).Style.Font.Bold = true;
        ws.Cell(1, lastColumn).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        ws.Cell(2, 1).Value = header.Parent ?? "ĐẢNG BỘ …";
        ws.Cell(3, 1).Value = header.Organization ?? "ĐẢNG ỦY (CHI BỘ) …";
        ws.Range(2, 1, 2, leftColumns).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Range(3, 1, 3, leftColumns).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetBold(true);

        var rightStart = Math.Max(leftColumns + 2, lastColumn - 4);
        ws.Cell(2, rightStart).Value = "ĐẢNG CỘNG SẢN VIỆT NAM";
        ws.Cell(3, rightStart).Value = DateLine(location);
        ws.Range(2, rightStart, 2, lastColumn).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetBold(true);
        ws.Range(3, rightStart, 3, lastColumn).Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center).Font.SetItalic(true);
    }

    private static void TitleRow(IXLWorksheet ws, int row, int lastColumn, string text, bool bold)
    {
        ws.Cell(row, 1).Value = text;
        ws.Range(row, 1, row, lastColumn).Merge().Style
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Font.SetBold(bold).Font.SetFontSize(bold ? 13 : 11);
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        range.Style.Alignment.WrapText = true;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        range.LastRow().Style.Font.Bold = false;
        range.LastRow().Style.Font.Italic = true;
    }

    private static void SignatureRow(IXLWorksheet ws, int row, int lastColumn, string left, string leftNote, string right, string rightNote)
    {
        var half = lastColumn / 2;
        ws.Cell(row, 1).Value = left;
        ws.Cell(row + 1, 1).Value = leftNote;
        ws.Cell(row, half + 1).Value = right;
        ws.Cell(row + 1, half + 1).Value = rightNote;
        ws.Range(row, 1, row, half).Merge().Style.Font.SetBold(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Range(row + 1, 1, row + 1, half).Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Range(row, half + 1, row, lastColumn).Merge().Style.Font.SetBold(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Range(row + 1, half + 1, row + 1, lastColumn).Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
    }

    /// <summary>Cột 3 Mẫu 14: các chức vụ đang giữ (Đảng, chính quyền, đoàn thể) và đơn vị công tác.</summary>
    private static string PositionAndUnit(EvaluationRecord record, IReadOnlyCollection<Position> positions)
    {
        var names = positions.Count > 0 ? string.Join(", ", positions.Select(p => p.Name)) : record.Member?.PositionTitle;
        var unit = record.Department?.Name ?? record.Member?.Department?.Name;
        return string.Join(" — ", new[] { names, unit }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>Cột 12 Mẫu 14: căn cứ, lý do khi đề xuất Hoàn thành xuất sắc hoặc Không hoàn thành nhiệm vụ.</summary>
    private static string? Rationale(EvaluationRecord record)
    {
        var grade = ReportGrade(record);
        if (grade is not (EvaluationGrade.HoanThanhXuatSac or EvaluationGrade.KhongHoanThanh))
            return null;
        var parts = new[] { record.AppraisalExplanation, record.AppraisalComment, record.DirectorComment, record.CollectiveComment }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct()
            .ToList();
        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    private static string? GradeOrBlank(EvaluationGrade grade) =>
        grade == EvaluationGrade.ChuaXepLoai ? null : FormText.Grade(grade);

    #endregion

    #region Mẫu 16 — báo cáo Word của cấp ủy

    private const string Form16Code = "M16";
    private static readonly JsonSerializerOptions DraftJson = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task<Form16DraftDto> GetForm16DraftAsync(Guid periodId, Guid? partyCellId)
    {
        var period = await _db.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");
        var (_, records, cell) = await LoadExcelScopeAsync(periodId, partyCellId);
        var draft = await _db.Set<ReportDraft>().AsNoTracking()
            .FirstOrDefaultAsync(d => d.PeriodId == periodId && d.PartyCellId == partyCellId && d.FormCode == Form16Code);
        var header = await ReportHeaderAsync(cell);
        var statCodes = await PersonStatCodesAsync(records.Select(r => r.MemberId), PositionDate(period));

        var latestDecision = await _db.EvaluationMeetings.AsNoTracking()
            .Where(m => m.PeriodId == periodId && m.Stage == WorkflowStep.B4_DECISION && (partyCellId == null || m.PartyCellId == partyCellId))
            .OrderByDescending(m => m.StartedAt)
            .Select(m => (DateTime?)m.StartedAt)
            .FirstOrDefaultAsync();

        return new Form16DraftDto
        {
            PeriodId = period.Id,
            PeriodName = period.Name,
            PartyCellId = partyCellId,
            PartyOrganizationName = header.UnitName ?? string.Empty,
            Version = draft?.Version,
            UpdatedAt = draft?.UpdatedAt ?? draft?.CreatedAt,
            Content = ReadDraft(draft?.Content),
            BaseRows = Summarize(records.Where(r => r.ApprovalAuthority == ApprovalAuthority.CoSo), statCodes),
            SuperiorRows = Summarize(records.Where(r => r.ApprovalAuthority == ApprovalAuthority.CapTren), statCodes),
            SuggestedMeetingDate = latestDecision.HasValue ? CollectiveFormText.Date(latestDecision.Value) : null
        };
    }

    /// <inheritdoc />
    public async Task<Form16DraftDto> SaveForm16DraftAsync(Guid periodId, Guid? partyCellId, Guid requesterId, SaveForm16DraftDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        _ = await _db.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");
        var content = CleanDraft(dto.Content ?? new Form16DraftContentDto());
        var json = JsonSerializer.Serialize(content, DraftJson);

        var draft = await _db.Set<ReportDraft>()
            .FirstOrDefaultAsync(d => d.PeriodId == periodId && d.PartyCellId == partyCellId && d.FormCode == Form16Code);
        if (draft == null)
        {
            if (dto.Version.HasValue)
                throw new CongTacDang.Application.Common.Exceptions.ConflictException(
                    "Bản nháp Mẫu 16 đã bị xóa hoặc chưa từng được lưu. Hãy tải lại trang rồi nhập lại.");
            _db.Set<ReportDraft>().Add(new ReportDraft
            {
                PeriodId = periodId,
                PartyCellId = partyCellId,
                FormCode = Form16Code,
                Content = json,
                CreatedBy = requesterId,
                UpdatedBy = requesterId,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            draft.Content = json;
            draft.UpdatedBy = requesterId;
            draft.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.SetOriginalVersion(draft, dto.Version);
        }
        await _unitOfWork.SaveChangesAsync();
        return await GetForm16DraftAsync(periodId, partyCellId);
    }

    /// <inheritdoc />
    public async Task<ReportFileResult> ExportMau16DocxAsync(Guid periodId, Guid? partyCellId, ReportFormat format = ReportFormat.Original)
    {
        var period = await _db.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");
        var draft = await GetForm16DraftAsync(periodId, partyCellId);
        var cell = partyCellId.HasValue ? await _db.PartyCells.AsNoTracking().FirstOrDefaultAsync(c => c.Id == partyCellId.Value) : null;
        var header = await ReportHeaderAsync(cell);
        var org = await _orgSettings.GetAsync();
        var bytes = await RenderWordAsync(Mau16Data.TemplateFileName, Mau16Data.From(period, header, draft));
        return await ToResultAsync(bytes, DocxMimeType, Hd03FileName("16", org.ShortName, cell, period, ".docx"), format);
    }

    /// <summary>Số liệu Mẫu 16 theo nhóm chức danh (mã thống kê); dòng cuối "Tổng cộng".</summary>
    private static List<Form16SummaryRowDto> Summarize(IEnumerable<EvaluationRecord> records, IReadOnlyDictionary<Guid, string?> statCodes)
    {
        var list = records.ToList();
        var rows = list
            .GroupBy(r => statCodes.GetValueOrDefault(r.MemberId))
            .OrderBy(g => PositionRules.StatCodeOrder(g.Key) ?? int.MaxValue)
            .Select(g => ToSummaryRow(g.Key, g.Key == null
                ? Hd03FormCatalog.NoStatCodeSubject
                : Hd03FormCatalog.StatCodeSubjects.GetValueOrDefault(g.Key) ?? g.Key, g.ToList()))
            .ToList();
        rows.Add(ToSummaryRow(null, "Tổng cộng", list));
        return rows;
    }

    private static Form16SummaryRowDto ToSummaryRow(string? code, string subject, IReadOnlyCollection<EvaluationRecord> group)
    {
        var c = CountGrades(group);
        return new Form16SummaryRowDto
        {
            StatCode = code,
            Subject = subject,
            Total = c.Total,
            Excellent = c.Excellent,
            Good = c.Good,
            Satisfactory = c.Satisfactory,
            Unsatisfactory = c.Unsatisfactory,
            NotRated = c.NotRated,
            ExcellentPercent = c.ExcellentPercent
        };
    }

    private static Form16DraftContentDto ReadDraft(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Form16DraftContentDto();
        try
        {
            return JsonSerializer.Deserialize<Form16DraftContentDto>(json, DraftJson) ?? new Form16DraftContentDto();
        }
        catch (JsonException)
        {
            return new Form16DraftContentDto();
        }
    }

    /// <summary>Cắt khoảng trắng, chuỗi rỗng → null; kiểm tra độ dài từng mục.</summary>
    private static Form16DraftContentDto CleanDraft(Form16DraftContentDto c)
    {
        static string? Clean(string? value, int max, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            var text = value.Trim();
            if (text.Length > max)
                throw new ArgumentException($"Mục \"{name}\" dài {text.Length} ký tự, vượt giới hạn {max} ký tự. Hãy rút gọn.");
            return text;
        }

        return new Form16DraftContentDto
        {
            DocumentNumber = Clean(c.DocumentNumber, 100, "Số văn bản"),
            Recipient = Clean(c.Recipient, 300, "Nơi gửi"),
            WorkingRules = Clean(c.WorkingRules, 500, "Quy chế làm việc"),
            MeetingDate = Clean(c.MeetingDate, 100, "Ngày tổ chức hội nghị"),
            Organizer = Clean(c.Organizer, 300, "Tổ chức Đảng tổ chức hội nghị"),
            Proposer = Clean(c.Proposer, 300, "Đề xuất của"),
            Proposal1 = Clean(c.Proposal1, 10000, "Đề xuất III.1"),
            Proposal2 = Clean(c.Proposal2, 10000, "Đề xuất III.2"),
            Proposal3 = Clean(c.Proposal3, 10000, "Đề xuất III.3"),
            SignerName = Clean(c.SignerName, 200, "Người ký")
        };
    }

    #endregion

    #region Số liệu dùng chung

    /// <summary>Số lượng theo 5 mức và tỷ lệ % xuất sắc trong số tốt trở lên.</summary>
    private sealed record GradeCounts(int Total, int Excellent, int Good, int Satisfactory, int Unsatisfactory, int NotRated)
    {
        public double? ExcellentPercent => Excellent + Good > 0 ? Math.Round((double)Excellent / (Excellent + Good) * 100, 1) : null;
    }

    private static GradeCounts CountGrades(IReadOnlyCollection<EvaluationRecord> group)
    {
        var grades = group.Select(ReportGrade).ToList();
        return new GradeCounts(
            grades.Count,
            grades.Count(g => g == EvaluationGrade.HoanThanhXuatSac),
            grades.Count(g => g == EvaluationGrade.HoanThanhTot),
            grades.Count(g => g == EvaluationGrade.HoanThanh),
            grades.Count(g => g == EvaluationGrade.KhongHoanThanh),
            grades.Count(g => g == EvaluationGrade.ChuaXepLoai));
    }

    /// <summary>
    /// Mức dùng cho báo cáo tổng hợp: mức quyết định; chưa có thì mức đề xuất gần nhất (cấp trực tiếp sử dụng → thẩm định →
    /// tập thể lãnh đạo). Không tính mức cá nhân tự đề xuất; chưa có mức nào → "Chưa xếp loại".
    /// </summary>
    private static EvaluationGrade ReportGrade(EvaluationRecord record)
    {
        foreach (var grade in new[] { record.FinalGrade, record.DirectorProposedGrade, record.AppraisalProposedGrade, record.CollectiveProposedGrade })
        {
            if (grade != EvaluationGrade.ChuaXepLoai)
                return grade;
        }
        return EvaluationGrade.ChuaXepLoai;
    }

    /// <summary>
    /// Thời điểm xét chức vụ đang hiệu lực cho báo cáo của kỳ: cuối kỳ nếu kỳ đã kết thúc, ngược lại hiện tại.
    /// </summary>
    private static DateTime PositionDate(EvaluationPeriod? period)
    {
        var now = DateTime.UtcNow;
        if (period == null || period.EndDate == default)
            return now;
        var end = DateTime.SpecifyKind(period.EndDate, DateTimeKind.Utc);
        return end < now ? end : now;
    }

    /// <summary>Chức vụ đang hiệu lực của các cán bộ (kèm chức vụ trong danh mục).</summary>
    private async Task<Dictionary<Guid, List<Position>>> HeldPositionsAsync(IEnumerable<Guid> memberIds, DateTime at)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, List<Position>>();
        var rows = await _db.MemberPositions.AsNoTracking()
            .Include(mp => mp.Position)
            .Where(mp => ids.Contains(mp.UserId) && mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at) && mp.Position != null)
            .ToListAsync();
        return rows.GroupBy(mp => mp.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(mp => mp.Position!.SortOrder).Select(mp => mp.Position!).ToList());
    }

    /// <summary>Mã chức danh thống kê của từng cán bộ (mã nhỏ nhất trong các chức vụ đang hiệu lực).</summary>
    private async Task<Dictionary<Guid, string?>> PersonStatCodesAsync(IEnumerable<Guid> memberIds, DateTime at)
        => (await HeldPositionsAsync(memberIds, at)).ToDictionary(
            kv => kv.Key,
            kv => PositionRules.PersonStatCode(kv.Value.Select(p => new HeldPosition(p.Name, p.StatCode, p.DefaultApprovalAuthority))));

    /// <summary>Tên các chức vụ Đảng đang giữ của từng cán bộ (ngăn cách bằng dấu phẩy).</summary>
    private async Task<Dictionary<Guid, string>> PartyPositionNamesAsync(IEnumerable<Guid> memberIds, DateTime at)
        => (await HeldPositionsAsync(memberIds, at)).ToDictionary(
            kv => kv.Key,
            kv => string.Join(", ", kv.Value.Where(p => p.Side == PositionSide.Party).Select(p => p.Name)));

    /// <summary>Id tổ chức Đảng được chọn và mọi tổ chức con cháu (cây); null = toàn Đảng bộ.</summary>
    private async Task<HashSet<Guid>?> SubtreeCellIdsAsync(PartyCell? cell)
    {
        if (cell == null)
            return null;
        var nodes = await _db.PartyCells.AsNoTracking().Select(c => new { c.Id, c.Path }).ToListAsync();
        var ids = OrgTree.SelfAndDescendants(nodes.Select(n => (n.Id, n.Path)), cell.Id).ToHashSet();
        ids.Add(cell.Id);
        return ids;
    }

    /// <summary>
    /// Tiêu đề trái của báo cáo theo phạm vi: tổ chức Đảng được chọn (dòng trên là tổ chức cha, không có thì tên Đảng bộ);
    /// toàn Đảng bộ (dòng trên là tổ chức Đảng cấp trên) — lấy từ cài đặt đơn vị, không ghi cứng.
    /// </summary>
    private async Task<PartyHeader> ReportHeaderAsync(PartyCell? cell)
    {
        var org = await _orgSettings.GetAsync();
        if (cell == null)
            return PartyHeader.Of(org.SuperiorPartyName, org.PartyCommitteeName);
        var parentName = cell.ParentId.HasValue
            ? await _db.PartyCells.AsNoTracking().Where(c => c.Id == cell.ParentId.Value).Select(c => c.Name).FirstOrDefaultAsync()
            : null;
        return PartyHeader.Of(parentName ?? org.PartyCommitteeName, cell.Name);
    }

    /// <summary>
    /// Phạm vi dữ liệu báo cáo: kỳ được chọn (mặc định kỳ đang hoạt động) và tổ chức Đảng (null = toàn Đảng bộ), gồm tổ chức con.
    /// Phạm vi đã được kiểm tra quyền qua IReportAccessService ở controller.
    /// </summary>
    private async Task<(EvaluationPeriod? Period, List<EvaluationRecord> Records, PartyCell? Cell)> LoadExcelScopeAsync(Guid? periodId, Guid? partyCellId)
    {
        EvaluationPeriod? period;
        if (periodId.HasValue && periodId.Value != Guid.Empty)
        {
            period = await _evalRepo.GetPeriodByIdAsync(periodId.Value)
                ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");
        }
        else
        {
            period = await _evalRepo.GetActivePeriodAsync();
        }

        PartyCell? cell = null;
        if (partyCellId.HasValue && partyCellId.Value != Guid.Empty)
        {
            cell = await _orgRepo.GetPartyCellByIdAsync(partyCellId.Value)
                ?? throw new KeyNotFoundException($"Không tìm thấy tổ chức Đảng với Id: {partyCellId}");
        }

        var records = period != null
            ? await _evalRepo.GetRecordsByPeriodAsync(period.Id)
            : new List<EvaluationRecord>();
        if (cell != null)
        {
            var cellIds = (await SubtreeCellIdsAsync(cell))!;
            records = records.Where(r => (r.PartyCellId.HasValue && cellIds.Contains(r.PartyCellId.Value))
                || (r.Member?.PartyCellId is { } memberCell && cellIds.Contains(memberCell))).ToList();
        }

        return (period, records, cell);
    }

    /// <summary>Dòng "Địa danh, ngày … tháng … năm …" theo ngày hiện tại.</summary>
    private static string DateLine(string location) =>
        $"{location}, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}";

    /// <summary>"III NĂM 2026" (dấu phân cách tùy biểu mẫu); chưa có kỳ → "… NĂM …".</summary>
    private static string QuarterYear(EvaluationPeriod? period, string separator) =>
        period == null ? "…" + separator + "…" : CollectiveFormText.RomanQuarter(period.Quarter) + separator + FormText.Year(period.Year);

    /// <summary>Tiêu đề phạm vi của báo cáo nội bộ: tên kỳ (chữ hoa) và tên tổ chức Đảng nếu xuất theo tổ chức.</summary>
    private static string ExcelTitleSuffix(EvaluationPeriod? period, PartyCell? cell)
    {
        var periodTitle = period != null ? period.Name.ToUpper(Vietnamese) : "CHƯA CÓ KỲ ĐÁNH GIÁ";
        return cell != null ? $"{periodTitle} - {cell.Name.ToUpper(Vietnamese)}" : periodTitle;
    }

    /// <summary>Tên tệp báo cáo nội bộ theo kỳ và phạm vi.</summary>
    private static string ExcelFileName(string baseName, EvaluationPeriod? period, PartyCell? cell)
    {
        var scope = cell != null ? "_" + SafeName(cell.Name, "ChiBo") : "_ToanDangBo";
        var periodPart = period != null ? $"_Q{(int)period.Quarter}_{period.Year}" : string.Empty;
        return $"{baseName}{scope}{periodPart}.xlsx";
    }

    /// <summary>
    /// Tên tệp theo quy cách HD03 V.1: "Mau bao cao_Ten viet tat co quan, don vi_Ky bao cao", viết không dấu
    /// (ví dụ "Mau 16_QLBMB_Quy III-2026"). Xuất theo tổ chức Đảng: tên viết tắt là tên tổ chức (không dấu, bỏ khoảng trắng).
    /// </summary>
    private static string Hd03FileName(string formCode, string? shortName, PartyCell? cell, EvaluationPeriod? period, string extension)
    {
        var unit = cell != null ? Ascii(cell.Name).Replace(" ", string.Empty) : Ascii(shortName ?? string.Empty).Replace(" ", string.Empty);
        if (string.IsNullOrEmpty(unit))
            unit = "DonVi";
        var periodPart = period != null ? $"Quy {CollectiveFormText.RomanQuarter(period.Quarter)}-{period.Year}" : "Ky bao cao";
        return $"Mau {formCode}_{unit}_{periodPart}{extension}";
    }

    /// <summary>Bỏ dấu tiếng Việt và ký tự không hợp lệ trong tên tệp.</summary>
    private static string Ascii(string value)
    {
        var normalized = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch) || ch is ' ' or '-')
                sb.Append(ch);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Quy tắc trần tỷ lệ Hoàn thành xuất sắc theo bộ tiêu chí của kỳ (kỳ chưa chọn bộ → mặc định của bộ tiêu chí).</summary>
    private static ExcellentQuotaRule QuotaRule(EvaluationPeriod? period) =>
        (EvaluationMapping.SafeCriteria(period)?.Content.Parameters ?? new CriteriaParameters()).ExcellentQuota;

    private static bool IsGoodOrBetter(EvaluationRecord record) =>
        IsGrade(record.AppraisalProposedGrade, EvaluationGrade.HoanThanhTot) ||
        IsGrade(record.CollectiveProposedGrade, EvaluationGrade.HoanThanhTot);

    private static bool IsProposedExcellent(EvaluationRecord record) =>
        record.AppraisalProposedGrade == EvaluationGrade.HoanThanhXuatSac ||
        (record.AppraisalProposedGrade == EvaluationGrade.ChuaXepLoai && record.CollectiveProposedGrade == EvaluationGrade.HoanThanhXuatSac);

    private static bool IsGrade(EvaluationGrade grade, EvaluationGrade minimum) =>
        grade == EvaluationGrade.HoanThanhXuatSac || grade == minimum;

    #endregion

    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfMimeType = "application/pdf";

    #region Biểu mẫu Word — mỗi mẫu = một template .docx + một lớp dữ liệu trong Documents/Forms

    public async Task<ReportFileResult> ExportMau01DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadRecordAsync(recordId);
        var bytes = await RenderWordAsync(Mau01Data.TemplateFileName, Mau01Data.From(record));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_01_DangKyNhiemVu_{SafeName(record.Member?.FullName, "CanBo")}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau02DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadRecordAsync(recordId);
        var evidenceNames = await GetEvidenceNamesAsync(record.Tasks);
        var bytes = await RenderWordAsync(Mau02Data.TemplateFileName, Mau02Data.From(record, evidenceNames));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_02_TuDanhGia_{SafeName(record.Member?.FullName, "CanBo")}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau10DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadRecordAsync(recordId);
        var bytes = await RenderWordAsync(Mau10Data.TemplateFileName, Mau10Data.From(record));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_10_PhieuThamDinh_{SafeName(record.Member?.FullName, "CanBo")}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau11DocxAsync(Guid periodId, Guid? branchId, ReportFormat format = ReportFormat.Original)
    {
        var (period, records, branch) = await LoadPeriodRecordsAsync(periodId, branchId);
        // Xuất toàn Đảng bộ: dòng tổ chức Đảng lập phiếu là tên Đảng bộ (cài đặt đơn vị).
        var issuer = branch?.Name ?? (await _orgSettings.GetAsync()).PartyCommitteeName;
        var bytes = await RenderWordAsync(Mau11Data.TemplateFileName, Mau11Data.From(period, records, issuer));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_11_PhieuBoPhieu_{SafeName(branch?.Name, "ToanDangBo")}_Q{(int)period.Quarter}_{period.Year}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau13DocxAsync(Guid periodId, Guid? branchId, ReportFormat format = ReportFormat.Original)
    {
        var (period, records, branch) = await LoadPeriodRecordsAsync(periodId, branchId);

        // Task 12: kết quả kiểm phiếu tổng hợp lưu trên biên bản (Mẫu 12/13) — lấy biên bản mới nhất có dòng của từng hồ sơ.
        var recordIds = records.Select(r => r.Id).ToList();
        var summaries = await _db.EvaluationMeetingVoteSummaries.AsNoTracking()
            .Include(v => v.Meeting)
            .Where(v => recordIds.Contains(v.RecordId) && !v.Meeting.IsDeleted)
            .ToListAsync();
        var tallies = summaries
            .GroupBy(v => v.RecordId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.Meeting.StartedAt).First());
        var meetings = tallies.Values.Select(v => v.Meeting).DistinctBy(m => m.Id).ToList();

        // Số người bỏ phiếu: số có mặt của biên bản (khi mọi dòng thuộc một biên bản); không có thì sĩ số Chi bộ.
        int? totalVoters = meetings.Count == 1 && meetings[0].PresentCount > 0
            ? meetings[0].PresentCount
            : null;
        if (totalVoters is null or 0 && branch != null)
            totalVoters = await _db.PartyMemberProfiles.CountAsync(m => m.PartyCellId == branch.Id);

        var issuer = branch?.Name ?? (await _orgSettings.GetAsync()).PartyCommitteeName;
        var bytes = await RenderWordAsync(Mau13Data.TemplateFileName, Mau13Data.From(period, records, issuer, totalVoters, tallies));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_13_BienBanKiemPhieu_{SafeName(branch?.Name, "ToanDangBo")}_Q{(int)period.Quarter}_{period.Year}.docx", format);
    }

    /// <inheritdoc />
    public async Task<ReportFileResult> ExportMau07DocxAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadCollectiveAsync(collectiveRecordId, CollectiveEvaluationForm.M07);
        var header = await CollectiveHeaderAsync(record.PartyCell);
        var sections = CollectiveEvaluationService.DeserializeSections(record.Sections);
        var org = await _orgSettings.GetAsync();
        var bytes = await RenderWordAsync(Mau07Data.TemplateFileName, Mau07Data.From(record, header, sections));
        return await ToResultAsync(bytes, DocxMimeType, Hd03FileName("07", org.ShortName, record.PartyCell, record.Period, ".docx"), format);
    }

    /// <inheritdoc />
    public async Task<ReportFileResult> ExportMau08DocxAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadCollectiveAsync(collectiveRecordId, CollectiveEvaluationForm.M08);
        var header = await CollectiveHeaderAsync(record.PartyCell);
        var org = await _orgSettings.GetAsync();
        var bytes = await RenderWordAsync(Mau08Data.TemplateFileName, Mau08Data.From(record, header));
        return await ToResultAsync(bytes, DocxMimeType, Hd03FileName("08", org.ShortName, record.PartyCell, record.Period, ".docx"), format);
    }

    /// <inheritdoc />
    public async Task<ReportFileResult> ExportMau12DocxAsync(Guid meetingId, ReportFormat format = ReportFormat.Original)
    {
        var meeting = await _db.EvaluationMeetings.AsNoTracking()
            .Include(m => m.Period)
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .FirstOrDefaultAsync(m => m.Id == meetingId)
            ?? throw new KeyNotFoundException($"Không tìm thấy biên bản hội nghị với Id: {meetingId}");
        if (meeting.FormCode != "M12")
            throw new ArgumentException("Biên bản này là biên bản kiểm phiếu (Mẫu 13). Mẫu 12 chỉ xuất từ biên bản hội nghị (M12).");

        var org = await _orgSettings.GetAsync();
        PartyHeader header;
        string? unit;
        string? archive;
        if (meeting.PartyCell != null)
        {
            header = await CollectiveHeaderAsync(meeting.PartyCell);
            unit = meeting.PartyCell.Name;
            archive = meeting.PartyCell.Name;
        }
        else
        {
            header = PartyHeader.Of(org.SuperiorPartyName, org.PartyCommitteeName);
            unit = meeting.Department?.Name ?? org.PartyCommitteeName;
            archive = org.PartyCommitteeName;
        }

        var details = CollectiveEvaluationService.DeserializeDetails(meeting.Details);
        var bytes = await RenderWordAsync(Mau12Data.TemplateFileName, Mau12Data.From(meeting, header, unit, archive, details));
        var unitForFile = meeting.PartyCell ?? (meeting.Department != null ? new PartyCell { Name = meeting.Department.Name } : null);
        return await ToResultAsync(bytes, DocxMimeType, Hd03FileName("12", org.ShortName, unitForFile, meeting.Period, ".docx"), format);
    }

    /// <summary>Hồ sơ tập thể kèm kỳ, tổ chức, dòng nội dung; biểu mẫu phải đúng loại.</summary>
    private async Task<CollectiveEvaluationRecord> LoadCollectiveAsync(Guid id, CollectiveEvaluationForm form)
    {
        var record = await _db.CollectiveEvaluationRecords.AsNoTracking()
            .Include(r => r.Period)
            .Include(r => r.PartyCell)
            .Include(r => r.Department)
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ tập thể với Id: {id}");
        if (record.Form != form)
            throw new ArgumentException($"Hồ sơ tập thể này lập theo {record.Form}, không xuất được theo Mẫu {(int)form:00}.");
        return record;
    }

    /// <summary>Tiêu đề trái của văn bản tập thể/biên bản: tổ chức Đảng của hồ sơ (không gắn → Đảng bộ).</summary>
    private Task<PartyHeader> CollectiveHeaderAsync(PartyCell? cell) => ReportHeaderAsync(cell);

    /// <summary>
    /// Điền lớp dữ liệu mẫu vào template (phiên bản đang kích hoạt hoặc file mẫu gốc), kèm tag thông tin đơn vị dùng chung
    /// (<see cref="OrganizationTemplateFields"/>, lấy từ cài đặt đơn vị).
    /// </summary>
    private async Task<byte[]> RenderWordAsync(string templateFileName, object formData)
    {
        var template = await _templates.LoadAsync(templateFileName);
        var org = OrganizationSettingsService.ToTemplateFields(await _orgSettings.GetAsync());
        var data = TemplateDataBinder.Bind(formData).WithShared(TemplateDataBinder.Bind(org));
        return DocxTemplateEngine.Render(template, data).Content;
    }

    /// <summary>Trả tệp ở định dạng gốc, hoặc chuyển sang PDF phía máy chủ khi <paramref name="format"/> là PDF.</summary>
    private async Task<ReportFileResult> ToResultAsync(byte[] bytes, string contentType, string fileName, ReportFormat format)
    {
        if (format != ReportFormat.Pdf)
            return new ReportFileResult { FileBytes = bytes, ContentType = contentType, FileName = fileName };

        var pdf = await _pdfConverter.ConvertToPdfAsync(bytes, Path.GetExtension(fileName));
        return new ReportFileResult
        {
            FileBytes = pdf,
            ContentType = PdfMimeType,
            FileName = Path.ChangeExtension(fileName, ".pdf")
        };
    }

    private static string SafeName(string? name, string fallback) =>
        string.IsNullOrWhiteSpace(name) ? fallback : name.Trim().Replace(" ", "_");

    private async Task<EvaluationRecord> LoadRecordAsync(Guid recordId)
    {
        return await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Include(r => r.Tasks)
            .FirstOrDefaultAsync(r => r.Id == recordId)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}");
    }

    private async Task<(EvaluationPeriod Period, List<EvaluationRecord> Records, PartyCell? Branch)> LoadPeriodRecordsAsync(Guid periodId, Guid? branchId)
    {
        var period = await _db.EvaluationPeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == periodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");

        var query = _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Where(r => r.PeriodId == periodId);

        PartyCell? branch = null;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(r => r.PartyCellId == branchId.Value || r.Member.PartyCellId == branchId.Value);
            branch = await _db.PartyCells.AsNoTracking().FirstOrDefaultAsync(b => b.Id == branchId.Value);
        }

        return (period, await query.ToListAsync(), branch);
    }

    /// <summary>Tên tệp minh chứng (phiên bản hiện hành) của từng nhiệm vụ.</summary>
    private async Task<Dictionary<Guid, string>> GetEvidenceNamesAsync(IEnumerable<EvaluationTask> tasks)
    {
        var linked = tasks.Where(t => t.AttachmentId.HasValue).ToList();
        var result = new Dictionary<Guid, string>();
        if (linked.Count == 0)
            return result;

        var versionIds = linked.Select(t => t.AttachmentId!.Value).Distinct().ToList();
        var versions = await _db.TaskAttachments.AsNoTracking()
            .Where(a => versionIds.Contains(a.Id))
            .Select(a => new { a.Id, GroupId = a.FileGroupId ?? a.Id })
            .ToListAsync();
        var groupIds = versions.Select(v => v.GroupId).Distinct().ToList();
        var currents = await _db.TaskAttachments.AsNoTracking()
            .Where(a => !a.IsSuperseded && (groupIds.Contains(a.Id) || (a.FileGroupId.HasValue && groupIds.Contains(a.FileGroupId.Value))))
            .Select(a => new { GroupId = a.FileGroupId ?? a.Id, a.OriginalFileName })
            .ToListAsync();

        foreach (var task in linked)
        {
            var version = versions.FirstOrDefault(v => v.Id == task.AttachmentId);
            var current = version == null ? null : currents.FirstOrDefault(c => c.GroupId == version.GroupId);
            if (current != null && !string.IsNullOrWhiteSpace(current.OriginalFileName))
                result[task.Id] = current.OriginalFileName;
        }
        return result;
    }

    #endregion
}
