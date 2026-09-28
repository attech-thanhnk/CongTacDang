using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly CongTacDangDbContext _db;
    private readonly IUserRepository _userRepo;
    private readonly IOrganizationRepository _orgRepo;
    private readonly IEvaluationRepository _evalRepo;

    public ReportService(
        CongTacDangDbContext db,
        IUserRepository userRepo,
        IOrganizationRepository orgRepo,
        IEvaluationRepository evalRepo)
    {
        _db = db;
        _userRepo = userRepo;
        _orgRepo = orgRepo;
        _evalRepo = evalRepo;
    }

    public async Task<ReportFileResult> ExportCadresReportAsync()
    {
        var members = await _userRepo.GetAllWithDetailsAsync();

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

    public async Task<ReportFileResult> ExportForm14ReportAsync()
    {
        var activePeriod = await _evalRepo.GetActivePeriodAsync();
        var periodId = activePeriod?.Id ?? Guid.Empty;
        var records = periodId != Guid.Empty
            ? await _evalRepo.GetRecordsByPeriodAsync(periodId)
            : new List<CongTacDang.Domain.Entities.EvaluationRecord>();

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

            string periodTitle = activePeriod != null ? activePeriod.Name : "QUÝ III/2026";
            ws.Cell("A4").Value = $"BẢNG TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CHẤT LƯỢNG CÁN BỘ ({periodTitle.ToUpper()})";
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
                var members = await _userRepo.GetAllWithDetailsAsync();
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
                return new ReportFileResult
                {
                    FileBytes = stream.ToArray(),
                    FileName = "Mau_14_TongHopXepLoaiCanBo_Q3_2026.xlsx"
                };
            }
        }
    }

    public async Task<ReportFileResult> ExportForm15ReportAsync()
    {
        var activePeriod = await _evalRepo.GetActivePeriodAsync();
        var periodId = activePeriod?.Id ?? Guid.Empty;
        var records = periodId != Guid.Empty
            ? await _evalRepo.GetRecordsByPeriodAsync(periodId)
            : new List<CongTacDang.Domain.Entities.EvaluationRecord>();

        var cells = await _orgRepo.GetPartyCellsWithMembersAsync();

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

            string periodTitle = activePeriod != null ? activePeriod.Name : "QUÝ III/2026";
            ws.Cell("A4").Value = $"BẢNG KIỂM SOÁT TỶ LỆ TRẦN 20% HOÀN THÀNH XUẤT SẮC NHIỆM VỤ THEO CHI BỘ ({periodTitle.ToUpper()})";
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
                return new ReportFileResult
                {
                    FileBytes = stream.ToArray(),
                    FileName = "Mau_15_KiemSoatTran20_ChiBo_Q3_2026.xlsx"
                };
            }
        }
    }

    public async Task<ReportFileResult> ExportForm15AReportAsync()
    {
        var activePeriod = await _evalRepo.GetActivePeriodAsync();
        var periodId = activePeriod?.Id ?? Guid.Empty;
        var records = periodId != Guid.Empty
            ? await _evalRepo.GetRecordsByPeriodAsync(periodId)
            : new List<CongTacDang.Domain.Entities.EvaluationRecord>();

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
        ws.Cell(5, 2).Value = "Toàn Đảng bộ Công ty";
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
        return new ReportFileResult
        {
            FileBytes = stream.ToArray(),
            FileName = "Mau_15A_KiemSoatTran20_ToanDangBo.xlsx"
        };
    }

    public async Task<ReportFileResult> ExportForm15BReportAsync()
    {
        var result = await ExportForm15ReportAsync();
        result.FileName = "Mau_15B_KiemSoatTran20_TheoChiBo.xlsx";
        return result;
    }

    public async Task<ReportFileResult> ExportForm16ReportAsync()
    {
        var activePeriod = await _evalRepo.GetActivePeriodAsync();
        var periodId = activePeriod?.Id ?? Guid.Empty;
        var records = periodId != Guid.Empty
            ? await _evalRepo.GetRecordsByPeriodAsync(periodId)
            : new List<CongTacDang.Domain.Entities.EvaluationRecord>();

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Mẫu 16 - Tổng hợp");
        ws.Cell("A1").Value = "BẢNG TỔNG HỢP KẾT QUẢ XẾP LOẠI CÁN BỘ - MẪU 16";
        ws.Range("A1:H1").Merge().Style.Font.SetBold(true).Font.SetFontSize(13)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell("A2").Value = activePeriod?.Name ?? "Chưa có kỳ đánh giá";
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
        return new ReportFileResult
        {
            FileBytes = stream.ToArray(),
            FileName = "Mau_16_TongHopKetQuaXepLoai.xlsx"
        };
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

    public async Task<ReportFileResult> ExportMau01DocxAsync(Guid recordId)
    {
        var record = await _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Include(r => r.Tasks)
            .FirstOrDefaultAsync(r => r.Id == recordId);

        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}");

        var bytes = DocxTemplateEngine.FillMau01Template(record);
        var safeName = record.Member?.FullName?.Replace(" ", "_") ?? "CanBo";

        return new ReportFileResult
        {
            FileBytes = bytes,
            ContentType = DocxMimeType,
            FileName = $"Mau_01_DangKyNhiemVu_{safeName}.docx"
        };
    }

    public async Task<ReportFileResult> ExportMau02DocxAsync(Guid recordId)
    {
        var record = await _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .FirstOrDefaultAsync(r => r.Id == recordId);

        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}");

        var bytes = DocxTemplateEngine.FillMau02Template(record);
        var safeName = record.Member?.FullName?.Replace(" ", "_") ?? "CanBo";

        return new ReportFileResult
        {
            FileBytes = bytes,
            ContentType = DocxMimeType,
            FileName = $"Mau_02_TuDanhGia_{safeName}.docx"
        };
    }

    public async Task<ReportFileResult> ExportMau10DocxAsync(Guid recordId)
    {
        var record = await _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Include(r => r.Tasks)
            .FirstOrDefaultAsync(r => r.Id == recordId);

        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}");

        var bytes = DocxTemplateEngine.FillMau10Template(record);
        var safeName = record.Member?.FullName?.Replace(" ", "_") ?? "CanBo";

        return new ReportFileResult
        {
            FileBytes = bytes,
            ContentType = DocxMimeType,
            FileName = $"Mau_10_PhieuThamDinh_{safeName}.docx"
        };
    }

    public async Task<ReportFileResult> ExportMau11DocxAsync(Guid periodId, Guid? branchId)
    {
        var period = await _db.EvaluationPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
        if (period == null)
            throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");

        var query = _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Where(r => r.PeriodId == periodId);

        string? branchName = null;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(r => r.PartyCellId == branchId.Value || r.Member.PartyCellId == branchId.Value);
            var branch = await _db.PartyCells.FirstOrDefaultAsync(b => b.Id == branchId.Value);
            branchName = branch?.Name;
        }

        var records = await query.ToListAsync();
        var bytes = DocxTemplateEngine.FillMau11Template(period, records, branchName);

        var safeBranch = !string.IsNullOrEmpty(branchName) ? branchName.Replace(" ", "_") : "ToanDangBo";
        return new ReportFileResult
        {
            FileBytes = bytes,
            ContentType = DocxMimeType,
            FileName = $"Mau_11_PhieuBoPhieu_{safeBranch}_Q{period.Quarter}_{period.Year}.docx"
        };
    }

    public async Task<ReportFileResult> ExportMau13DocxAsync(Guid periodId, Guid? branchId)
    {
        var period = await _db.EvaluationPeriods.FirstOrDefaultAsync(p => p.Id == periodId);
        if (period == null)
            throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");

        var query = _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Where(r => r.PeriodId == periodId);

        string? branchName = null;
        int totalVoters = 12;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            query = query.Where(r => r.PartyCellId == branchId.Value || r.Member.PartyCellId == branchId.Value);
            var branch = await _db.PartyCells.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == branchId.Value);
            branchName = branch?.Name;
            if (branch?.Members?.Count > 0)
            {
                totalVoters = branch.Members.Count;
            }
        }

        var records = await query.ToListAsync();
        if (records.Count > 0 && records[0].TotalVoters > 0)
        {
            totalVoters = records[0].TotalVoters;
        }

        var bytes = DocxTemplateEngine.FillMau13Template(period, records, branchName, totalVoters);
        var safeBranch = !string.IsNullOrEmpty(branchName) ? branchName.Replace(" ", "_") : "ToanDangBo";

        return new ReportFileResult
        {
            FileBytes = bytes,
            ContentType = DocxMimeType,
            FileName = $"Mau_13_BienBanKiemPhieu_{safeBranch}_Q{period.Quarter}_{period.Year}.docx"
        };
    }
}
