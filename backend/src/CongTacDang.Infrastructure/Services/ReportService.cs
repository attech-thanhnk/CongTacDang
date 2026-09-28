using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;

namespace CongTacDang.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly CongTacDangDbContext _db;
    private readonly IUserRepository _userRepo;
    private readonly IOrganizationRepository _orgRepo;
    private readonly IEvaluationRepository _evalRepo;
    private readonly IWordTemplateStore _templates;
    private readonly IPdfConverter _pdfConverter;

    public ReportService(
        CongTacDangDbContext db,
        IUserRepository userRepo,
        IOrganizationRepository orgRepo,
        IEvaluationRepository evalRepo,
        IWordTemplateStore templates,
        IPdfConverter pdfConverter)
    {
        _db = db;
        _userRepo = userRepo;
        _orgRepo = orgRepo;
        _evalRepo = evalRepo;
        _templates = templates;
        _pdfConverter = pdfConverter;
    }

    public async Task<ReportFileResult> ExportCadresReportAsync(CongTacDang.Application.Common.Security.ScopeFilter scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        // T-61: chỉ cán bộ thuộc phạm vi report.export của người yêu cầu (Toàn công ty / Phòng / Chi bộ).
        var members = (await _userRepo.GetAllWithDetailsAsync())
            .Where(m => scope.Matches(null, m.DepartmentId, m.PartyCellId))
            .ToList();

        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Danh sách cán bộ");

            ws.Cell("A1").Value = "ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY";
            ws.Cell("A1").Style.Font.Bold = true;

            ws.Cell("A3").Value = "DANH SÁCH CÁN BỘ LÃNH ĐẠO, QUẢN LÝ ATTECH";
            ws.Range("A3:H3").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[] { "STT", "Họ và tên", "Số thẻ Đảng", "Chi bộ", "Chức vụ Đảng", "Đơn vị chuyên môn", "Chức danh chính quyền", "Trạng thái" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(5, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 6;
            int stt = 1;
            foreach (var m in members)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = m.FullName;
                ws.Cell(row, 3).Value = m.PartyCardNumber ?? "";
                ws.Cell(row, 4).Value = m.PartyCell?.Name ?? "";
                ws.Cell(row, 5).Value = m.PartyRole.ToString();
                ws.Cell(row, 6).Value = m.Department?.Name ?? "";
                ws.Cell(row, 7).Value = m.PositionTitle ?? "";
                ws.Cell(row, 8).Value = m.IsActive ? "Đang hoạt động" : "Đã khóa";

                for (int c = 1; c <= 8; c++)
                {
                    ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                row++;
            }

            ws.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return new ReportFileResult
                {
                    FileBytes = stream.ToArray(),
                    FileName = "DanhSach_CanBo_ATTECH.xlsx"
                };
            }
        }
    }

    public async Task<ReportFileResult> ExportForm14ReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
    {
        var (activePeriod, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);

        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Mẫu 14 - Xếp loại cán bộ");

            ws.Cell("A1").Value = "ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM";
            ws.Cell("A2").Value = "ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.Bold = true;

            ws.Cell("F1").Value = "ĐẢNG CỘNG SẢN VIỆT NAM";
            ws.Cell("F1").Style.Font.Bold = true;
            ws.Cell("F2").Value = $"Hà Nội, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}";

            ws.Cell("A4").Value = $"BẢNG TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CHẤT LƯỢNG CÁN BỘ ({ExcelTitleSuffix(activePeriod, scopeCell)})";
            ws.Range("A4:L4").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell("A5").Value = "(Ban hành kèm theo Hướng dẫn số 03-HD/TVĐU ngày 10/9/2026 của Ban Thường vụ Đảng ủy Tổng công ty)";
            ws.Range("A5:L5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[]
            {
                "STT", "Họ và tên cán bộ", "Chức vụ Đảng", "Chức danh chính quyền", "Chi bộ", "Đơn vị chuyên môn",
                "Điểm chung (30đ)", "Điểm chuyên môn (70đ)", "Tổng điểm tự chấm (100đ)", "Chi bộ đề xuất",
                "Điểm thẩm định", "Mức xếp loại chính thức"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(7, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 8;
            int stt = 1;

            if (records.Count > 0)
            {
                foreach (var r in records)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = r.Member?.FullName ?? "";
                    ws.Cell(row, 3).Value = r.Member?.PartyRole.ToString() ?? "";
                    ws.Cell(row, 4).Value = r.Member?.PositionTitle ?? "";
                    ws.Cell(row, 5).Value = r.Member?.PartyCell?.Name ?? r.PartyCell?.Name ?? "";
                    ws.Cell(row, 6).Value = r.Member?.Department?.Name ?? r.Department?.Name ?? "";
                    ws.Cell(row, 7).Value = r.GeneralCriteriaScore;
                    ws.Cell(row, 8).Value = r.TasksScore;
                    ws.Cell(row, 9).Value = r.TotalSelfScore;
                    ws.Cell(row, 10).Value = r.PartyCellProposedGrade != CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai ? r.PartyCellProposedGrade.ToString() : "-";
                    ws.Cell(row, 11).Value = r.AppraisalScore.HasValue ? r.AppraisalScore.Value.ToString("F1") : "-";
                    ws.Cell(row, 12).Value = r.FinalGrade != CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai ? r.FinalGrade.ToString() : "Chờ chuẩn y";

                    for (int c = 1; c <= 12; c++)
                    {
                        ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    row++;
                }
            }
            else
            {
                var members = (await _userRepo.GetAllWithDetailsAsync())
                    .Where(m => scopeCell == null || m.PartyCellId == scopeCell.Id)
                    .ToList();
                foreach (var m in members)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = m.FullName;
                    ws.Cell(row, 3).Value = m.PartyRole.ToString();
                    ws.Cell(row, 4).Value = m.PositionTitle ?? "";
                    ws.Cell(row, 5).Value = m.PartyCell?.Name ?? "";
                    ws.Cell(row, 6).Value = m.Department?.Name ?? "";
                    ws.Cell(row, 7).Value = "-";
                    ws.Cell(row, 8).Value = "-";
                    ws.Cell(row, 9).Value = "-";
                    ws.Cell(row, 10).Value = "-";
                    ws.Cell(row, 11).Value = "-";
                    ws.Cell(row, 12).Value = "Chờ đánh giá";

                    for (int c = 1; c <= 12; c++)
                    {
                        ws.Cell(row, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    }
                    row++;
                }
            }

            ws.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return await ToResultAsync(stream.ToArray(), XlsxMimeType, ExcelFileName("Mau_14_TongHopXepLoaiCanBo", activePeriod, scopeCell), format);
            }
        }
    }

    public async Task<ReportFileResult> ExportForm15ReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
    {
        var (activePeriod, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);

        var cells = (await _orgRepo.GetPartyCellsWithMembersAsync())
            .Where(c => scopeCell == null || c.Id == scopeCell.Id)
            .ToList();

        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.Worksheets.Add("Mẫu 15 - Kiểm soát trần 20%");

            ws.Cell("A1").Value = "ĐẢNG BỘ TỔNG CÔNG TY QUẢN LÝ BAY VIỆT NAM";
            ws.Cell("A2").Value = "ĐẢNG BỘ CÔNG TY TNHH KỸ THUẬT QUẢN LÝ BAY";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A2").Style.Font.Bold = true;

            ws.Cell("E1").Value = "ĐẢNG CỘNG SẢN VIỆT NAM";
            ws.Cell("E1").Style.Font.Bold = true;
            ws.Cell("E2").Value = $"Hà Nội, ngày {DateTime.Now:dd} tháng {DateTime.Now:MM} năm {DateTime.Now:yyyy}";

            ws.Cell("A4").Value = $"BẢNG KIỂM SOÁT TỶ LỆ TRẦN 20% HOÀN THÀNH XUẤT SẮC NHIỆM VỤ THEO CHI BỘ ({ExcelTitleSuffix(activePeriod, scopeCell)})";
            ws.Range("A4:H4").Merge().Style.Font.SetBold(true).Font.SetFontSize(13).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell("A5").Value = "(Quy định tại Điều 12 Hướng dẫn 03-HD/TVĐU: Tỷ lệ HTXSNV không vượt quá 20% số cán bộ hoàn thành tốt nhiệm vụ trở lên)";
            ws.Range("A5:H5").Merge().Style.Font.SetItalic(true).Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            var headers = new[]
            {
                "STT", "Tên Chi bộ", "Tổng số cán bộ", "Số HT tốt trở lên", "Trần 20% tối đa",
                "Đề xuất xuất sắc", "Tỷ lệ thực tế (%)", "Kết luận kiểm soát trần"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(7, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FED7AA");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }

            int row = 8;
            int stt = 1;
            foreach (var c in cells)
            {
                var cellRecords = records.Where(r => r.Member?.PartyCellId == c.Id || r.PartyCellId == c.Id).ToList();
                int total = cellRecords.Count > 0 ? cellRecords.Count : c.Members.Count;

                int goodOrBetter = cellRecords.Count(r =>
                    r.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac ||
                    r.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot ||
                    r.PartyCellProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac ||
                    r.PartyCellProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot);

                int maxAllowed = (int)Math.Floor(goodOrBetter * 0.20);

                int proposedExcellent = cellRecords.Count(r =>
                    r.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac ||
                    (r.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai &&
                     r.PartyCellProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac));

                double actualPercent = goodOrBetter > 0 ? Math.Round(((double)proposedExcellent / goodOrBetter) * 100.0, 1) : 0.0;
                bool isExceeding = proposedExcellent > maxAllowed;

                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = c.Name;
                ws.Cell(row, 3).Value = total;
                ws.Cell(row, 4).Value = goodOrBetter;
                ws.Cell(row, 5).Value = maxAllowed;
                ws.Cell(row, 6).Value = proposedExcellent;
                ws.Cell(row, 7).Value = $"{actualPercent}%";
                ws.Cell(row, 8).Value = isExceeding ? "VƯỢT TRẦN 20% (Vi phạm)" : "Đạt chuẩn";

                if (isExceeding)
                {
                    ws.Cell(row, 8).Style.Font.FontColor = XLColor.Red;
                    ws.Cell(row, 8).Style.Font.Bold = true;
                }

                for (int col = 1; col <= 8; col++)
                {
                    ws.Cell(row, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                }
                row++;
            }

            ws.Columns().AdjustToContents();

            using (var stream = new MemoryStream())
            {
                workbook.SaveAs(stream);
                return await ToResultAsync(stream.ToArray(), XlsxMimeType, ExcelFileName("Mau_15_KiemSoatTran20_ChiBo", activePeriod, scopeCell), format);
            }
        }
    }

    public async Task<ReportFileResult> ExportForm15AReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
    {
        var (activePeriod, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);

        var total = records.Count;
        var goodOrBetter = records.Count(IsGoodOrBetter);
        var proposedExcellent = records.Count(IsProposedExcellent);
        var maxAllowed = (int)Math.Floor(goodOrBetter * 0.20);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 15A - Tổng hợp");
        ws.Cell("A1").Value = "BẢNG KIỂM SOÁT TỶ LỆ TRẦN 20% - MẪU 15A";
        ws.Range("A1:H1").Merge().Style.Font.SetBold(true).Font.SetFontSize(13)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell("A2").Value = activePeriod?.Name ?? "Chưa có kỳ đánh giá";
        ws.Range("A2:H2").Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        var headers = new[]
        {
            "STT", "Phạm vi", "Tổng số cán bộ", "HT tốt trở lên", "Trần 20% tối đa",
            "Đề xuất xuất sắc", "Tỷ lệ thực tế (%)", "Kết luận"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FED7AA");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        var actualPercent = goodOrBetter > 0 ? Math.Round((double)proposedExcellent / goodOrBetter * 100, 1) : 0;
        var exceeds = proposedExcellent > maxAllowed;
        ws.Cell(5, 1).Value = 1;
        ws.Cell(5, 2).Value = scopeCell?.Name ?? "Toàn Đảng bộ Công ty";
        ws.Cell(5, 3).Value = total;
        ws.Cell(5, 4).Value = goodOrBetter;
        ws.Cell(5, 5).Value = maxAllowed;
        ws.Cell(5, 6).Value = proposedExcellent;
        ws.Cell(5, 7).Value = $"{actualPercent}%";
        ws.Cell(5, 8).Value = exceeds ? "VƯỢT TRẦN 20%" : "Đạt chuẩn";
        if (exceeds)
        {
            ws.Cell(5, 8).Style.Font.FontColor = XLColor.Red;
            ws.Cell(5, 8).Style.Font.Bold = true;
        }
        for (var i = 1; i <= 8; i++)
            ws.Cell(5, i).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await ToResultAsync(stream.ToArray(), XlsxMimeType, ExcelFileName("Mau_15A_KiemSoatTran20", activePeriod, scopeCell), format);
    }

    public async Task<ReportFileResult> ExportForm15BReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
    {
        var result = await ExportForm15ReportAsync(periodId, partyCellId);
        var fileName = result.FileName.Replace("Mau_15_KiemSoatTran20_ChiBo", "Mau_15B_KiemSoatTran20_TheoChiBo");
        return await ToResultAsync(result.FileBytes, result.ContentType, fileName, format);
    }

    public async Task<ReportFileResult> ExportForm16ReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
    {
        var (activePeriod, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 16 - Tổng hợp");
        ws.Cell("A1").Value = "BẢNG TỔNG HỢP KẾT QUẢ XẾP LOẠI CÁN BỘ - MẪU 16";
        ws.Range("A1:H1").Merge().Style.Font.SetBold(true).Font.SetFontSize(13)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell("A2").Value = ExcelTitleSuffix(activePeriod, scopeCell);
        ws.Range("A2:H2").Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        var headers = new[]
        {
            "STT", "Nhóm chức vụ", "Tổng số", "Hoàn thành xuất sắc",
            "Hoàn thành tốt", "Hoàn thành", "Không hoàn thành", "Chưa xếp loại"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        var grouped = records
            .Where(r => r.Member != null)
            .GroupBy(r => r.Member!.PartyRole.ToString())
            .OrderBy(g => g.Key)
            .ToList();
        var row = 5;
        var stt = 1;
        foreach (var group in grouped)
        {
            var grades = group.Select(GetEffectiveGrade).ToList();
            var values = new object[]
            {
                stt++, group.Key, grades.Count,
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanh),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.KhongHoanThanh),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai)
            };
            for (var i = 0; i < values.Length; i++)
            {
                ws.Cell(row, i + 1).Value = values[i]?.ToString() ?? string.Empty;
                ws.Cell(row, i + 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            row++;
        }
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await ToResultAsync(stream.ToArray(), XlsxMimeType, ExcelFileName("Mau_16_TongHopKetQuaXepLoai", activePeriod, scopeCell), format);
    }

    /// <summary>
    /// Phạm vi dữ liệu báo cáo Excel: kỳ được chọn (mặc định kỳ đang hoạt động) và Chi bộ (null = toàn Đảng bộ).
    /// Phạm vi Chi bộ đã được kiểm tra quyền qua IReportAccessService/IAccessPolicy ở controller.
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
                ?? throw new KeyNotFoundException($"Không tìm thấy Chi bộ với Id: {partyCellId}");
        }

        var records = period != null
            ? await _evalRepo.GetRecordsByPeriodAsync(period.Id)
            : new List<EvaluationRecord>();
        if (cell != null)
            records = records.Where(r => r.PartyCellId == cell.Id || r.Member?.PartyCellId == cell.Id).ToList();

        return (period, records, cell);
    }

    /// <summary>Tiêu đề phạm vi: tên kỳ (chữ hoa) và tên Chi bộ nếu xuất theo Chi bộ.</summary>
    private static string ExcelTitleSuffix(EvaluationPeriod? period, PartyCell? cell)
    {
        var periodTitle = period != null ? period.Name.ToUpper() : "CHƯA CÓ KỲ ĐÁNH GIÁ";
        return cell != null ? $"{periodTitle} - {cell.Name.ToUpper()}" : periodTitle;
    }

    /// <summary>Tên tệp Excel theo kỳ và phạm vi, ví dụ Mau_14_..._ChiBo_Ky_Thuat_Q3_2026.xlsx.</summary>
    private static string ExcelFileName(string baseName, EvaluationPeriod? period, PartyCell? cell)
    {
        var scope = cell != null ? "_" + SafeName(cell.Name, "ChiBo") : "_ToanDangBo";
        var periodPart = period != null ? $"_Q{(int)period.Quarter}_{period.Year}" : string.Empty;
        return $"{baseName}{scope}{periodPart}.xlsx";
    }

    private static bool IsGoodOrBetter(CongTacDang.Domain.Entities.EvaluationRecord record)
    {
        return IsGrade(record.AppraisalProposedGrade, CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot) ||
               IsGrade(record.PartyCellProposedGrade, CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot);
    }

    private static bool IsProposedExcellent(CongTacDang.Domain.Entities.EvaluationRecord record)
    {
        return record.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac ||
               (record.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai &&
                record.PartyCellProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac);
    }

    private static bool IsGrade(CongTacDang.Domain.Enums.EvaluationGrade grade, CongTacDang.Domain.Enums.EvaluationGrade minimum)
    {
        return grade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac || grade == minimum;
    }

    private static CongTacDang.Domain.Enums.EvaluationGrade GetEffectiveGrade(CongTacDang.Domain.Entities.EvaluationRecord record)
    {
        if (record.FinalGrade != CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai)
            return record.FinalGrade;
        if (record.AppraisalProposedGrade != CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai)
            return record.AppraisalProposedGrade;
        return record.PartyCellProposedGrade;
    }

    private const string DocxMimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string XlsxMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfMimeType = "application/pdf";

    #region Biểu mẫu Word — mỗi mẫu = một template .docx + một lớp dữ liệu trong Documents/Forms

    public async Task<ReportFileResult> ExportMau01DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadRecordAsync(recordId);
        var bytes = RenderWord(Mau01Data.TemplateFileName, Mau01Data.From(record));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_01_DangKyNhiemVu_{SafeName(record.Member?.FullName, "CanBo")}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau02DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadRecordAsync(recordId);
        var evidenceNames = await GetEvidenceNamesAsync(record.Tasks);
        var bytes = RenderWord(Mau02Data.TemplateFileName, Mau02Data.From(record, evidenceNames));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_02_TuDanhGia_{SafeName(record.Member?.FullName, "CanBo")}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau10DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original)
    {
        var record = await LoadRecordAsync(recordId);
        var bytes = RenderWord(Mau10Data.TemplateFileName, Mau10Data.From(record));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_10_PhieuThamDinh_{SafeName(record.Member?.FullName, "CanBo")}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau11DocxAsync(Guid periodId, Guid? branchId, ReportFormat format = ReportFormat.Original)
    {
        var (period, records, branch) = await LoadPeriodRecordsAsync(periodId, branchId);
        var bytes = RenderWord(Mau11Data.TemplateFileName, Mau11Data.From(period, records, branch?.Name));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_11_PhieuBoPhieu_{SafeName(branch?.Name, "ToanDangBo")}_Q{(int)period.Quarter}_{period.Year}.docx", format);
    }

    public async Task<ReportFileResult> ExportMau13DocxAsync(Guid periodId, Guid? branchId, ReportFormat format = ReportFormat.Original)
    {
        var (period, records, branch) = await LoadPeriodRecordsAsync(periodId, branchId);

        // Số người bỏ phiếu: lấy giá trị đã lưu trên hồ sơ; hồ sơ chưa lưu thì dùng sĩ số Chi bộ.
        int? totalVoters = records.Select(r => r.TotalVoters).FirstOrDefault(v => v > 0);
        if (totalVoters is null or 0 && branch != null)
            totalVoters = await _db.PartyMemberProfiles.CountAsync(m => m.PartyCellId == branch.Id);

        var bytes = RenderWord(Mau13Data.TemplateFileName, Mau13Data.From(period, records, branch?.Name, totalVoters));
        return await ToResultAsync(bytes, DocxMimeType, $"Mau_13_BienBanKiemPhieu_{SafeName(branch?.Name, "ToanDangBo")}_Q{(int)period.Quarter}_{period.Year}.docx", format);
    }

    /// <summary>Điền lớp dữ liệu mẫu vào template.</summary>
    private byte[] RenderWord(string templateFileName, object formData)
    {
        var template = _templates.Load(templateFileName);
        return DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(formData)).Content;
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
