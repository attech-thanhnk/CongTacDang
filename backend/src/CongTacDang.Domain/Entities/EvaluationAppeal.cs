using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

/// <summary>Trạng thái kiến nghị: <c>Submitted → UnderReview → Accepted | Rejected</c> (xử lý thẳng từ Submitted được).</summary>
public enum AppealStatus
{
    Submitted = 0,

    UnderReview = 1,

    /// <summary>Có căn cứ; gợi ý mở lại hồ sơ để đính chính.</summary>
    Accepted = 2,

    /// <summary>Có căn cứ.</summary>
    Rejected = 3
}

/// <summary>Kiến nghị về kết quả đã công bố. Một hồ sơ không có hai kiến nghị cùng đang xử lý; khi còn kiến nghị
/// đang xử lý, kết quả cá nhân được ghi chú "đang xem xét".</summary>
public class EvaluationAppeal : IAuditableEntity, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Chủ hồ sơ.</summary>
    public Guid SubmittedById { get; set; }

    public string SubmittedByName { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public string Content { get; set; } = string.Empty;

    /// <summary>Mã bước bị kiến nghị (ví dụ <c>B4_DECISION</c>); người đã làm các bước này không được xử lý (xung đột lợi ích).</summary>
    public List<string> ConcernedSteps { get; set; } = new();

    /// <summary>Chụp khi gửi; bị chặn xử lý kiến nghị.</summary>
    public List<Guid> ConflictedUserIds { get; set; } = new();

    public AppealStatus Status { get; set; } = AppealStatus.Submitted;

    public Guid? ReviewStartedById { get; set; }

    public string? ReviewStartedByName { get; set; }

    public DateTime? ReviewStartedAt { get; set; }

    public Guid? ResolvedById { get; set; }

    public string? ResolvedByName { get; set; }

    public DateTime? ResolvedAt { get; set; }

    /// <summary>Bắt buộc nêu căn cứ.</summary>
    public string? Response { get; set; }

    /// <summary>Chấp nhận thì mở lại bằng thao tác "Mở lại" hiện có.</summary>
    public DateTime? ReopenedAt { get; set; }

    public Guid? ReopenedById { get; set; }

    /// <summary>Chưa trả lời.</summary>
    public bool IsOpen => Status is AppealStatus.Submitted or AppealStatus.UnderReview;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
