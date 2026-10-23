using System;

namespace CongTacDang.Domain.Entities;

/// <summary><c>Draft → Approved → Acknowledged → Closed</c>; ghi kết quả mốc được khi đã duyệt.</summary>
public enum ImprovementPlanStatus
{
    /// <summary>Đang lập, sửa được nội dung.</summary>
    Draft = 0,

    /// <summary>Thủ trưởng đơn vị đã duyệt, chờ cá nhân xác nhận cam kết.</summary>
    Approved = 1,

    /// <summary>Cá nhân đã xác nhận cam kết, đang thực hiện.</summary>
    Acknowledged = 2,

    /// <summary>Đã đánh giá mốc 90 ngày.</summary>
    Closed = 3
}

/// <summary>Kế hoạch 30-60-90 ngày (Mẫu 17) của hồ sơ đã công bố; bắt buộc khi mức chính thức thuộc
/// <c>ImprovementPlanRequiredGrades</c> của bộ tiêu chí.</summary>
public class ImprovementPlan : IAuditableEntity, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mỗi hồ sơ tối đa một kế hoạch.</summary>
    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>jsonb <c>ImprovementPlanContent</c>.</summary>
    public string Content { get; set; } = "{}";

    /// <summary>Các mốc tính từ ngày này; mặc định ngày duyệt.</summary>
    public DateOnly? StartDate { get; set; }

    public ImprovementPlanStatus Status { get; set; } = ImprovementPlanStatus.Draft;

    public Guid? PreparedById { get; set; }

    public string? PreparedByName { get; set; }

    /// <summary>Thủ trưởng đơn vị.</summary>
    public Guid? ApprovedById { get; set; }

    public string? ApprovedByName { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? AcknowledgedAt { get; set; }

    public string? AcknowledgementComment { get; set; }

    /// <summary>Khi ghi kết quả mốc 90 ngày.</summary>
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
