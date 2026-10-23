using System;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

public class ReportFileResult
{
    public byte[] FileBytes { get; set; } = System.Array.Empty<byte>();

    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public string FileName { get; set; } = string.Empty;
}

/// <summary>Kiểm tra trước khi bấm xuất Quyết định xếp loại.</summary>
public class DecisionReadinessDto
{
    public bool Ready { get; set; }

    public string? Message { get; set; }
}

public enum ReportFormat
{
    /// <summary>Word (.docx) hoặc Excel (.xlsx).</summary>
    Original = 0,

    /// <summary>Chuyển phía máy chủ từ định dạng gốc.</summary>
    Pdf = 1
}

/// <summary>Kết xuất biểu mẫu và báo cáo; phạm vi đã được <see cref="IReportAccessService"/> kiểm tra trước khi gọi.</summary>
public interface IReportService
{
    #region Báo cáo nội bộ (không mang số mẫu HD03)

    /// <summary>Chỉ cán bộ trong <paramref name="scope"/>.</summary>
    Task<ReportFileResult> ExportCadresReportAsync(CongTacDang.Application.Common.Security.ScopeFilter scope);

    /// <summary>Kiểm soát tỷ lệ Hoàn thành xuất sắc theo tổ chức Đảng.</summary>
    Task<ReportFileResult> ExportExcellentQuotaReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Bảng tổng hợp xếp loại theo cấp quyết định; chỉ hồ sơ trong <paramref name="viewerScope"/>.</summary>
    Task<ReportFileResult> ExportRatingSummaryAsync(Guid? periodId, Guid? partyCellId,
        CongTacDang.Application.Common.Security.ScopeFilter viewerScope, ReportFormat format = ReportFormat.Original);

    #endregion

    #region Hồ sơ nộp theo HD03 — bảng tính (periodId null = kỳ đang hoạt động; partyCellId null = toàn Đảng bộ)

    /// <summary>Mẫu 14 — mỗi cấp quyết định một trang tính.</summary>
    Task<ReportFileResult> ExportForm14ReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 15A — đối tượng cấp trên quyết định (M1–M16).</summary>
    Task<ReportFileResult> ExportForm15AReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 15B — đối tượng cơ sở quyết định (M17–M26).</summary>
    Task<ReportFileResult> ExportForm15BReportAsync(Guid? periodId = null, Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    #endregion

    #region Hồ sơ nộp theo HD03 — văn bản Word

    /// <summary>Mẫu 16 — báo cáo kết quả xếp loại quý.</summary>
    Task<ReportFileResult> ExportMau16DocxAsync(Guid periodId, Guid? partyCellId, ReportFormat format = ReportFormat.Original);

    /// <summary>
    /// Quyết định xếp loại kèm Phụ lục cho hồ sơ diện cơ sở quyết định; không có hồ sơ hoặc còn hồ sơ chưa ghi quyết định
    /// → <see cref="ArgumentException"/>.
    /// </summary>
    Task<ReportFileResult> ExportDecisionDocxAsync(Guid periodId, Guid? partyCellId, ReportFormat format = ReportFormat.Original);

    /// <summary>Cùng quy tắc với <see cref="ExportDecisionDocxAsync"/>.</summary>
    Task<DecisionReadinessDto> GetDecisionReadinessAsync(Guid periodId, Guid? partyCellId);

    /// <summary>Phần nhập tay Mẫu 16 kèm số liệu tổng hợp tự động.</summary>
    Task<Form16DraftDto> GetForm16DraftAsync(Guid periodId, Guid? partyCellId);

    /// <summary>Kiểm tra phiên bản.</summary>
    Task<Form16DraftDto> SaveForm16DraftAsync(Guid periodId, Guid? partyCellId, Guid requesterId, SaveForm16DraftDto dto);

    /// <summary>Mẫu 07 — từ hồ sơ tập thể.</summary>
    Task<ReportFileResult> ExportMau07DocxAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 08 — từ hồ sơ tập thể.</summary>
    Task<ReportFileResult> ExportMau08DocxAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 08 bản Excel, cùng dữ liệu bản Word, đúng cột của biểu mẫu gốc.</summary>
    Task<ReportFileResult> ExportForm08ExcelAsync(Guid collectiveRecordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 12 — Biên bản hội nghị.</summary>
    Task<ReportFileResult> ExportMau12DocxAsync(Guid meetingId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 01 — Phiếu giao/đăng ký sản phẩm.</summary>
    Task<ReportFileResult> ExportMau01DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 02 — Phiếu tự đánh giá.</summary>
    Task<ReportFileResult> ExportMau02DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 10 — Phiếu thẩm định.</summary>
    Task<ReportFileResult> ExportMau10DocxAsync(Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 11 — Phiếu bỏ phiếu kín.</summary>
    Task<ReportFileResult> ExportMau11DocxAsync(Guid periodId, Guid? branchId, ReportFormat format = ReportFormat.Original);

    /// <summary>Mẫu 13 — Biên bản kiểm phiếu.</summary>
    Task<ReportFileResult> ExportMau13DocxAsync(Guid meetingId, ReportFormat format = ReportFormat.Original);

    #endregion
}
