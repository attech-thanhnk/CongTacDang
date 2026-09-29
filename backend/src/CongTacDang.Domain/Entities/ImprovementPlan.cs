using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Trạng thái kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (Mẫu 17):
/// <c>Draft → Approved</c> (thủ trưởng đơn vị duyệt) <c>→ Acknowledged</c> (cá nhân xác nhận cam kết) <c>→ Closed</c>
/// (đã ghi kết quả mốc 90 ngày). Ghi kết quả mốc được khi đã duyệt.
/// </summary>
public enum ImprovementPlanStatus
{
    /// <summary>Đang lập — sửa được nội dung.</summary>
    Draft = 0,

    /// <summary>Thủ trưởng đơn vị đã duyệt — chờ cá nhân xác nhận cam kết.</summary>
    Approved = 1,

    /// <summary>Cá nhân đã xác nhận cam kết khắc phục — đang thực hiện.</summary>
    Acknowledged = 2,

    /// <summary>Đã đánh giá mốc 90 ngày — đóng kế hoạch.</summary>
    Closed = 3
}

/// <summary>
/// Kế hoạch hỗ trợ, khắc phục và phát triển 30-60-90 ngày (Mẫu 17, task 20 — T-88) gắn một hồ sơ đánh giá đã công bố.
/// Bắt buộc với hồ sơ có mức chính thức thuộc danh sách <c>ImprovementPlanRequiredGrades</c> của bộ tiêu chí của kỳ.
/// Nội dung theo đúng các mục của Mẫu 17 lưu ở <see cref="Content"/> (jsonb, theo mã mục — <c>ImprovementPlanContent</c>).
/// </summary>
public class ImprovementPlan : IAuditableEntity, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    /// <summary>Mã kế hoạch.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Hồ sơ đánh giá (mỗi hồ sơ tối đa một kế hoạch).</summary>
    public Guid RecordId { get; set; }

    /// <summary>Hồ sơ đánh giá (navigation).</summary>
    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Nội dung Mẫu 17 (jsonb): người hỗ trợ, các mốc 30/60/90 ngày với hạn chế, mục tiêu, biện pháp, phối hợp, kết quả.</summary>
    public string Content { get; set; } = "{}";

    /// <summary>Ngày bắt đầu kế hoạch (mốc 30/60/90 ngày tính từ ngày này); mặc định ngày duyệt.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Trạng thái.</summary>
    public ImprovementPlanStatus Status { get; set; } = ImprovementPlanStatus.Draft;

    /// <summary>Người lập.</summary>
    public Guid? PreparedById { get; set; }

    /// <summary>Họ tên người lập.</summary>
    public string? PreparedByName { get; set; }

    /// <summary>Thủ trưởng đơn vị duyệt.</summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>Họ tên người duyệt.</summary>
    public string? ApprovedByName { get; set; }

    /// <summary>Thời điểm duyệt.</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Thời điểm cá nhân xác nhận cam kết.</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>Ý kiến của cá nhân khi xác nhận.</summary>
    public string? AcknowledgementComment { get; set; }

    /// <summary>Thời điểm đóng kế hoạch (ghi kết quả mốc 90 ngày).</summary>
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
