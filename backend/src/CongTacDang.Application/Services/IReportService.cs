using System;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Kết quả xuất file báo cáo nhị phân (Excel)
/// </summary>
public class ReportFileResult
{
    /// <summary>Mảng byte nội dung file</summary>
    public byte[] FileBytes { get; set; } = System.Array.Empty<byte>();

    /// <summary>Định dạng MIME của file</summary>
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Tên file xuất ra kèm phần mở rộng (.xlsx)</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>Định dạng tệp xuất.</summary>
public enum ReportFormat
{
    /// <summary>Định dạng gốc của biểu mẫu: Word (.docx) hoặc Excel (.xlsx).</summary>
    Original = 0,

    /// <summary>PDF chuyển phía máy chủ từ định dạng gốc.</summary>
    Pdf = 1
}

/// <summary>
/// Giao diện dịch vụ kết xuất biểu mẫu HD03 và báo cáo nội bộ. Phạm vi dữ liệu đã được kiểm tra quyền
/// (<see cref="IReportAccessService"/>) trước khi gọi.
/// </summary>
public interface IReportService
{
    #region Báo cáo nội bộ (không mang số mẫu HD03)

    /// <summary>
    /// Danh sách cán bộ (nội bộ) — chỉ cán bộ trong <paramref name="scope"/> (phạm vi <c>report.export</c> của người yêu cầu, T-61).
    /// </summary>
    Task<ReportFileResult> ExportCadresReportAsync(CongTacDang.Application.Common.Security.ScopeFilter scope);

    /// <summary>Báo cáo nội bộ — kiểm soát tỷ lệ Hoàn thành xuất sắc theo tổ chức Đảng (trần của bộ tiêu chí của kỳ).</summary>
    Task<ReportFileResult> ExportExcellentQuotaReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    #endregion

    #region Hồ sơ nộp theo HD03 — bảng tính (periodId null = kỳ đang hoạt động; partyCellId null = toàn Đảng bộ)

    /// <summary>Mẫu 14 — Danh sách đánh giá và đề xuất xếp loại (mỗi cấp quyết định một trang tính).</summary>
    Task<ReportFileResult> ExportForm14ReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 15A — Tổng hợp kết quả đánh giá, xếp loại (đối tượng đề nghị BTV Đảng ủy Tổng công ty quyết định, mã M1–M16).</summary>
    Task<ReportFileResult> ExportForm15AReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 15B — Tổng hợp kết quả đánh giá, xếp loại (đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định, mã M17–M26).</summary>
    Task<ReportFileResult> ExportForm15BReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    #endregion

    #region Hồ sơ nộp theo HD03 — văn bản Word

    /// <summary>Mẫu 16 — Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý (Word).</summary>
    Task<ReportFileResult> ExportMau16DocxAsync(Guid periodId, Guid? partyCellId, ReportFormat format = ReportFormat.Original);

    /// <summary>Bản nháp phần nhập tay Mẫu 16 kèm số liệu tổng hợp tự động.</summary>
    Task<Form16DraftDto> GetForm16DraftAsync(Guid periodId, Guid? partyCellId);

    /// <summary>Lưu bản nháp phần nhập tay Mẫu 16 (kiểm tra phiên bản).</summary>
    Task<Form16DraftDto> SaveForm16DraftAsync(Guid periodId, Guid? partyCellId, Guid requesterId, SaveForm16DraftDto dto);

    /// <summary>Mẫu 07 — Báo cáo tự đánh giá, xếp loại chất lượng của tập thể (từ hồ sơ tập thể M07).</summary>
    Task<ReportFileResult> ExportMau07DocxAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 08 — Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị (từ hồ sơ tập thể M08).</summary>
    Task<ReportFileResult> ExportMau08DocxAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original);

    /// <summary>
    /// Mẫu 08 bản Excel (HD03 V.1: hồ sơ, biểu mẫu lập trên file Excel trừ Mẫu 07, 09C, 12, 13, 16) — cùng dữ liệu bản Word,
    /// đúng cột và 13 nhóm nội dung của biểu mẫu gốc.
    /// </summary>
    Task<ReportFileResult> ExportForm08ExcelAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 12 — Biên bản hội nghị (từ biên bản đã lập).</summary>
    Task<ReportFileResult> ExportMau12DocxAsync(Guid meetingId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 01: Phiếu giao/đăng ký sản phẩm, công việc chuyên môn (.docx)</summary>
    Task<ReportFileResult> ExportMau01DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 02: Phiếu tự đánh giá kết quả thực hiện sản phẩm (.docx)</summary>
    Task<ReportFileResult> ExportMau02DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 10: Phiếu thẩm định, nhận xét, ghi nhận giải trình (.docx)</summary>
    Task<ReportFileResult> ExportMau10DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 11: Phiếu đánh giá, xếp loại cán bộ quý bỏ phiếu kín (.docx)</summary>
    Task<ReportFileResult> ExportMau11DocxAsync(Guid periodId, Guid? branchId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 13: Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý (.docx)</summary>
    Task<ReportFileResult> ExportMau13DocxAsync(Guid meetingId, ReportFormat format = ReportFormat.Original);

    #endregion
}
