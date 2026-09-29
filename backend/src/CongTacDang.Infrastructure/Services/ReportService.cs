using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
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
        var partyPositions = await PartyPositionNamesAsync(members.Select(m => m.Id), DateTime.UtcNow);

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
                ws.Cell(row, 5).Value = partyPositions.GetValueOrDefault(m.Id) ?? "";
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
        var at = PositionDate(activePeriod);

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
                "Điểm chung (30đ)", "Điểm chuyên môn (70đ)", "Tổng điểm tự chấm (100đ)", "Tập thể lãnh đạo đề xuất",
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
                var partyPositions = await PartyPositionNamesAsync(records.Select(r => r.MemberId), at);
                foreach (var r in records)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = r.Member?.FullName ?? "";
                    ws.Cell(row, 3).Value = partyPositions.GetValueOrDefault(r.MemberId) ?? "";
                    ws.Cell(row, 4).Value = r.Member?.PositionTitle ?? "";
                    ws.Cell(row, 5).Value = r.Member?.PartyCell?.Name ?? r.PartyCell?.Name ?? "";
                    ws.Cell(row, 6).Value = r.Member?.Department?.Name ?? r.Department?.Name ?? "";
                    ws.Cell(row, 7).Value = r.GeneralCriteriaScore;
                    ws.Cell(row, 8).Value = r.TasksScore;
                    ws.Cell(row, 9).Value = r.TotalSelfScore;
                    ws.Cell(row, 10).Value = LeaderProposal(r) != CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai ? LeaderProposal(r).ToString() : "-";
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
                var cellIds = await SubtreeCellIdsAsync(scopeCell);
                var members = (await _userRepo.GetAllWithDetailsAsync())
                    .Where(m => cellIds == null || (m.PartyCellId.HasValue && cellIds.Contains(m.PartyCellId.Value)))
                    .ToList();
                var partyPositions = await PartyPositionNamesAsync(members.Select(m => m.Id), at);
                foreach (var m in members)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = m.FullName;
                    ws.Cell(row, 3).Value = partyPositions.GetValueOrDefault(m.Id) ?? "";
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
                    LeaderProposal(r) == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac ||
                    LeaderProposal(r) == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot);

                int maxAllowed = EvaluationScoring.ExcellentQuota(goodOrBetter, QuotaParameters(activePeriod));

                int proposedExcellent = cellRecords.Count(r =>
                    r.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac ||
                    (r.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai &&
                     LeaderProposal(r) == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac));

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

    /// <summary>
    /// Mẫu 15A (HD03 tr.73–74): tổng hợp kết quả xếp loại theo mã chức danh M1–M16 (đối tượng đề nghị BTV Đảng ủy Tổng công ty
    /// quyết định). Mỗi cán bộ được thống kê một lần theo mã chức danh của người (mã nhỏ nhất trong các chức vụ đang hiệu lực).
    /// </summary>
    public Task<ReportFileResult> ExportForm15AReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
        => ExportStatCodeFormAsync(periodId, partyCellId, format, Form15AFirstCode, Form15ALastCode, "15A",
            "(Đối tượng đề nghị Ban Thường vụ Đảng ủy Tổng công ty quyết định, phê duyệt mức xếp loại)", "Mau_15A_TongHopTheoChucDanh");

    /// <summary>
    /// Mẫu 15B (HD03 tr.75–76): tổng hợp kết quả xếp loại theo mã chức danh M17–M26 (đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở
    /// quyết định). Mỗi cán bộ được thống kê một lần theo mã chức danh của người.
    /// </summary>
    public Task<ReportFileResult> ExportForm15BReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original)
        => ExportStatCodeFormAsync(periodId, partyCellId, format, Form15BFirstCode, Form15BLastCode, "15B",
            "(Đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định, phê duyệt mức xếp loại)", "Mau_15B_TongHopTheoChucDanh");

    /// <summary>Khoảng mã chức danh của Mẫu 15A/15B (bố cục biểu mẫu HD03).</summary>
    private const int Form15AFirstCode = 1, Form15ALastCode = 16, Form15BFirstCode = 17, Form15BLastCode = 26;

    /// <summary>Tên nhóm chức danh theo mã (HD03 tr.73–76, bản trích xuất mục 7).</summary>
    private static readonly IReadOnlyDictionary<string, string> StatCodeNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["M1"] = "Bí thư Đảng ủy Tổng công ty",
        ["M2"] = "Phó Bí thư Đảng ủy Tổng công ty",
        ["M3"] = "Thành viên Hội đồng thành viên",
        ["M4"] = "Phó Tổng giám đốc",
        ["M5"] = "Ủy viên Ban Thường vụ Đảng ủy Tổng công ty",
        ["M6"] = "Ủy viên Ban Chấp hành Đảng bộ Tổng công ty",
        ["M7"] = "Ủy viên Ủy ban Kiểm tra Đảng ủy Tổng công ty",
        ["M8"] = "Bí thư các đảng bộ, chi bộ trực thuộc Đảng ủy Tổng công ty",
        ["M9"] = "Phó Bí thư các đảng bộ, chi bộ trực thuộc Đảng ủy Tổng công ty",
        ["M10"] = "Bí thư các chi bộ trực thuộc Đảng ủy bộ phận Văn phòng Tổng công ty",
        ["M11"] = "Phó Bí thư các chi bộ trực thuộc Đảng ủy bộ phận Văn phòng Tổng công ty",
        ["M12"] = "Trưởng, phó chuyên trách các cơ quan tham mưu, giúp việc Đảng ủy Tổng công ty",
        ["M13"] = "Kế toán trưởng",
        ["M14"] = "Phó Trưởng Ban, Phó Giám đốc",
        ["M15"] = "Kiểm soát viên của Tổng công ty tại Công ty con",
        ["M16"] = "Bí thư Đoàn Thanh niên Tổng công ty",
        ["M17"] = "Ủy viên Ban Thường vụ đảng ủy cơ sở",
        ["M18"] = "Ủy viên Ban Chấp hành đảng bộ cơ sở",
        ["M19"] = "Ủy viên Ủy ban Kiểm tra đảng ủy cơ sở",
        ["M20"] = "Bí thư các đảng bộ bộ phận trực thuộc đảng ủy cơ sở",
        ["M21"] = "Phó Bí thư các đảng bộ bộ phận trực thuộc đảng ủy cơ sở",
        ["M22"] = "Bí thư các chi bộ trực thuộc đảng ủy cơ sở",
        ["M23"] = "Phó Bí thư các chi bộ trực thuộc đảng ủy cơ sở",
        ["M24"] = "Bí thư các chi bộ trực thuộc các đảng ủy bộ phận",
        ["M25"] = "Phó Bí thư các chi bộ trực thuộc các đảng ủy bộ phận",
        ["M26"] = "Trưởng phòng, Phó Trưởng phòng (và tương đương)"
    };

    /// <summary>Dựng Mẫu 15A/15B: một dòng cho mỗi mã chức danh trong khoảng, cột số lượng theo mức xếp loại, dòng tổng, kiểm soát trần.</summary>
    private async Task<ReportFileResult> ExportStatCodeFormAsync(Guid? periodId, Guid? partyCellId, ReportFormat format,
        int firstCode, int lastCode, string formName, string subject, string fileBase)
    {
        var (activePeriod, records, scopeCell) = await LoadExcelScopeAsync(periodId, partyCellId);
        var statCodes = await PersonStatCodesAsync(records.Select(r => r.MemberId), PositionDate(activePeriod));

        var byCode = records
            .Select(r => (Record: r, Order: PositionRules.StatCodeOrder(statCodes.GetValueOrDefault(r.MemberId))))
            .ToList();
        var inForm = byCode.Where(x => x.Order >= firstCode && x.Order <= lastCode).ToList();
        var withoutCode = byCode.Count(x => x.Order == null);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"Mẫu {formName}");
        ws.Cell("A1").Value = $"TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ ({ExcelTitleSuffix(activePeriod, scopeCell)}) - MẪU {formName}";
        ws.Range("A1:I1").Merge().Style.Font.SetBold(true).Font.SetFontSize(13)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell("A2").Value = subject;
        ws.Range("A2:I2").Merge().Style.Font.SetItalic(true).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        ws.Cell("A3").Value = "Mỗi cán bộ thống kê một lần theo nhóm chức danh có thứ tự đứng trước (HD03 tr.74).";
        ws.Range("A3:I3").Merge().Style.Font.SetFontSize(10).Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        var headers = new[]
        {
            "Mã", "Chức danh", "Tổng số", "Hoàn thành xuất sắc", "Hoàn thành tốt", "Hoàn thành", "Không hoàn thành", "Chưa xếp loại", "Ghi chú"
        };
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(5, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        var row = 6;
        void WriteRow(string code, string name, IReadOnlyCollection<EvaluationRecord> group, string note, bool bold)
        {
            var grades = group.Select(GetEffectiveGrade).ToList();
            var values = new object[]
            {
                code, name, grades.Count,
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanh),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.KhongHoanThanh),
                grades.Count(g => g == CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai),
                note
            };
            for (var i = 0; i < values.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = values[i] is int n ? n : values[i]?.ToString() ?? string.Empty;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Font.Bold = bold;
            }
            row++;
        }

        for (var order = firstCode; order <= lastCode; order++)
        {
            var code = "M" + order;
            var group = inForm.Where(x => x.Order == order).Select(x => x.Record).ToList();
            WriteRow(code, StatCodeNames.GetValueOrDefault(code) ?? code, group, string.Empty, bold: false);
        }
        WriteRow(string.Empty, "Tổng cộng", inForm.Select(x => x.Record).ToList(), string.Empty, bold: true);

        // Kiểm soát trần tỷ lệ Hoàn thành xuất sắc trên nhóm của mẫu.
        var formRecords = inForm.Select(x => x.Record).ToList();
        var goodOrBetter = formRecords.Count(IsGoodOrBetter);
        var proposedExcellent = formRecords.Count(IsProposedExcellent);
        var maxAllowed = EvaluationScoring.ExcellentQuota(goodOrBetter, QuotaParameters(activePeriod));
        row++;
        ws.Cell(row, 1).Value = $"Kiểm soát trần: đề xuất xuất sắc {proposedExcellent}/{goodOrBetter} hoàn thành tốt trở lên; tối đa {maxAllowed}"
            + (proposedExcellent > maxAllowed ? " — VƯỢT TRẦN." : " — đạt chuẩn.");
        ws.Range(row, 1, row, 9).Merge();
        if (withoutCode > 0)
        {
            row++;
            ws.Cell(row, 1).Value = $"Có {withoutCode} cán bộ chưa có chức vụ mang mã chức danh thống kê nên không được đưa vào Mẫu 15A/15B. "
                + "Hãy cập nhật chức vụ của cán bộ hoặc mã thống kê trong danh mục chức vụ.";
            ws.Range(row, 1, row, 9).Merge().Style.Font.SetFontColor(XLColor.Red);
        }

        ws.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return await ToResultAsync(stream.ToArray(), XlsxMimeType, ExcelFileName(fileBase, activePeriod, scopeCell), format);
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
            kv => string.Join(", ", kv.Value.Where(p => p.Side == CongTacDang.Domain.Enums.PositionSide.Party).Select(p => p.Name)));

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

        // Nhóm chức vụ = mã chức danh thống kê của người (M1…M26); cán bộ chưa có mã → "Khác".
        var statCodes = await PersonStatCodesAsync(records.Select(r => r.MemberId), PositionDate(activePeriod));
        var grouped = records
            .Where(r => r.Member != null)
            .GroupBy(r => statCodes.GetValueOrDefault(r.MemberId))
            .OrderBy(g => PositionRules.StatCodeOrder(g.Key) ?? int.MaxValue)
            .Select(g => new { Key = g.Key == null ? "Khác (chưa có mã chức danh)" : $"{g.Key} — {StatCodeNames.GetValueOrDefault(g.Key) ?? g.Key}", Items = g.ToList() })
            .ToList();
        var row = 5;
        var stt = 1;
        foreach (var group in grouped)
        {
            var grades = group.Items.Select(GetEffectiveGrade).ToList();
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
    /// Phạm vi Chi bộ đã được kiểm tra quyền qua IReportAccessService ở controller.
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
        {
            // Tổ chức Đảng được chọn bao gồm mọi tổ chức con cháu (cây tổ chức, task 14).
            var cellIds = (await SubtreeCellIdsAsync(cell))!;
            records = records.Where(r => (r.PartyCellId.HasValue && cellIds.Contains(r.PartyCellId.Value))
                || (r.Member?.PartyCellId is { } memberCell && cellIds.Contains(memberCell))).ToList();
        }

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

    /// <summary>Mức đề xuất của tập thể lãnh đạo (B3a).</summary>
    private static CongTacDang.Domain.Enums.EvaluationGrade LeaderProposal(EvaluationRecord record) => record.CollectiveProposedGrade;

    /// <summary>Tham số trần tỷ lệ của kỳ (mặc định 20 %, làm tròn xuống).</summary>
    private static EvaluationParameters QuotaParameters(EvaluationPeriod? period)
    {
        if (period == null)
            return new EvaluationParameters();
        try
        {
            return period.GetSettings().Parameters;
        }
        catch (FormatException)
        {
            return new EvaluationParameters();
        }
    }

    private static bool IsGoodOrBetter(CongTacDang.Domain.Entities.EvaluationRecord record)
    {
        return IsGrade(record.AppraisalProposedGrade, CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot) ||
               IsGrade(LeaderProposal(record), CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhTot);
    }

    private static bool IsProposedExcellent(CongTacDang.Domain.Entities.EvaluationRecord record)
    {
        return record.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac ||
               (record.AppraisalProposedGrade == CongTacDang.Domain.Enums.EvaluationGrade.ChuaXepLoai &&
                LeaderProposal(record) == CongTacDang.Domain.Enums.EvaluationGrade.HoanThanhXuatSac);
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
        return LeaderProposal(record);
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

        var bytes = RenderWord(Mau13Data.TemplateFileName, Mau13Data.From(period, records, branch?.Name, totalVoters, tallies));
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
