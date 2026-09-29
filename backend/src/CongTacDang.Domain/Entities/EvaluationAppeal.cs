using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Trạng thái kiến nghị sau công bố (HD03 PL II mục III.2): <c>Submitted → UnderReview → Accepted | Rejected</c>
/// (được xử lý thẳng từ <c>Submitted</c>).
/// </summary>
public enum AppealStatus
{
    /// <summary>Đã gửi, chờ cấp có thẩm quyền xem xét.</summary>
    Submitted = 0,

    /// <summary>Đang xem xét.</summary>
    UnderReview = 1,

    /// <summary>Chấp nhận (có căn cứ) — gợi ý mở lại hồ sơ để đính chính.</summary>
    Accepted = 2,

    /// <summary>Không chấp nhận (có căn cứ).</summary>
    Rejected = 3
}

/// <summary>
/// Kiến nghị của chủ hồ sơ về kết quả đánh giá đã công bố (task 20 — T-87; HD03 PL II mục III.2, tr.4 "quyền được biết").
/// Một hồ sơ có thể có nhiều kiến nghị nhưng không có hai kiến nghị cùng đang xử lý. Khi còn kiến nghị đang xử lý, kết quả
/// cá nhân được ghi chú "đang xem xét". Tệp đính kèm gắn đối tượng <see cref="AttachmentOwnerTypes.EvaluationAppeal"/>.
/// </summary>
public class EvaluationAppeal : IAuditableEntity, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    /// <summary>Mã kiến nghị.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Hồ sơ đánh giá bị kiến nghị.</summary>
    public Guid RecordId { get; set; }

    /// <summary>Hồ sơ đánh giá (navigation).</summary>
    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Người gửi (chủ hồ sơ).</summary>
    public Guid SubmittedById { get; set; }

    /// <summary>Họ tên người gửi.</summary>
    public string SubmittedByName { get; set; } = string.Empty;

    /// <summary>Thời điểm gửi.</summary>
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Nội dung kiến nghị, đề nghị hiệu chỉnh.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Mã các bước mà kiến nghị liên quan tới (ví dụ <c>B3B_APPRAISAL</c>, <c>B4_DECISION</c>) — người đã thực hiện các bước này
    /// trên hồ sơ không được xử lý kiến nghị (xung đột lợi ích, HD03 PL II III.2).
    /// </summary>
    public List<string> ConcernedSteps { get; set; } = new();

    /// <summary>Người có xung đột lợi ích (người thực hiện các bước bị kiến nghị, chụp khi gửi) — bị chặn xử lý.</summary>
    public List<Guid> ConflictedUserIds { get; set; } = new();

    /// <summary>Trạng thái.</summary>
    public AppealStatus Status { get; set; } = AppealStatus.Submitted;

    /// <summary>Người nhận xem xét.</summary>
    public Guid? ReviewStartedById { get; set; }

    /// <summary>Họ tên người nhận xem xét.</summary>
    public string? ReviewStartedByName { get; set; }

    /// <summary>Thời điểm nhận xem xét.</summary>
    public DateTime? ReviewStartedAt { get; set; }

    /// <summary>Người xử lý (trả lời).</summary>
    public Guid? ResolvedById { get; set; }

    /// <summary>Họ tên người xử lý.</summary>
    public string? ResolvedByName { get; set; }

    /// <summary>Thời điểm trả lời.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Nội dung trả lời — bắt buộc nêu căn cứ chấp nhận / không chấp nhận.</summary>
    public string? Response { get; set; }

    /// <summary>Thời điểm hồ sơ được mở lại theo kiến nghị (chấp nhận → mở lại bằng thao tác "Mở lại" hiện có).</summary>
    public DateTime? ReopenedAt { get; set; }

    /// <summary>Người mở lại hồ sơ theo kiến nghị.</summary>
    public Guid? ReopenedById { get; set; }

    /// <summary>Kiến nghị còn đang xử lý (chưa trả lời).</summary>
    public bool IsOpen => Status is AppealStatus.Submitted or AppealStatus.UnderReview;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
