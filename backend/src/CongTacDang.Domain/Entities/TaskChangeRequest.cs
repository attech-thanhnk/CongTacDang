using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Loại đề nghị thay đổi danh mục việc trong quý (Mẫu 04, Mẫu 05).</summary>
public enum TaskChangeType
{
    /// <summary>Điều chỉnh nội dung / chỉ tiêu / thời hạn / trọng số của một việc (Mẫu 04).</summary>
    Adjust = 1,

    /// <summary>Chuyển tiếp một việc sang quý sau (Mẫu 04) — việc không chấm trong quý này.</summary>
    Carryover = 2,

    /// <summary>Loại trừ / ngoại lệ một việc do nguyên nhân khách quan, bất khả kháng (Mẫu 04) — việc không chấm.</summary>
    Exclude = 3,

    /// <summary>Ghi nhận nhiệm vụ bổ sung, khẩn cấp, đột xuất (Mẫu 05) — thêm một việc loại đột xuất.</summary>
    Urgent = 4
}

/// <summary>Trạng thái đề nghị thay đổi danh mục việc.</summary>
public enum TaskChangeStatus
{
    /// <summary>Chờ cấp có thẩm quyền duyệt.</summary>
    Pending = 0,

    /// <summary>Đã duyệt — đã áp vào danh mục việc của hồ sơ.</summary>
    Approved = 1,

    /// <summary>Không duyệt (có lý do).</summary>
    Rejected = 2
}

/// <summary>Dấu của việc đã được loại trừ / chuyển tiếp hợp lệ: việc vẫn hiện trên phiếu nhưng trọng số 0, không chấm.</summary>
public enum TaskChangeMark
{
    /// <summary>Đã loại trừ.</summary>
    Excluded = 1,

    /// <summary>Chuyển tiếp sang quý sau.</summary>
    CarriedOver = 2
}

/// <summary>
/// Đề nghị điều chỉnh, chuyển tiếp, loại trừ việc (Mẫu 04) hoặc ghi nhận việc đột xuất (Mẫu 05) của một hồ sơ trong quý, sau khi
/// danh mục đã duyệt và trước khi nộp tự chấm. Khi duyệt, nội dung đề nghị và bảng trọng số sau điều chỉnh được áp vào
/// <see cref="EvaluationTask"/> của hồ sơ (tổng trọng số các việc còn hiệu lực vẫn đúng theo bộ tiêu chí).
/// </summary>
public class TaskChangeRequest : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Hồ sơ đánh giá.</summary>
    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Loại đề nghị.</summary>
    public TaskChangeType Type { get; set; }

    /// <summary>Việc bị tác động (null với việc đột xuất).</summary>
    public Guid? TaskId { get; set; }

    /// <summary>Tên việc bị tác động tại thời điểm đề nghị (để hiển thị khi việc đã đổi tên).</summary>
    public string? TaskNameSnapshot { get; set; }

    /// <summary>
    /// Nội dung đề nghị (jsonb, <c>TaskChangeProposal</c>): các trường mới của việc; với việc đột xuất là toàn bộ việc mới.
    /// </summary>
    public string Proposal { get; set; } = "{}";

    /// <summary>Bảng trọng số sau điều chỉnh (jsonb: Id việc → trọng số) cho các việc còn hiệu lực.</summary>
    public string Weights { get; set; } = "{}";

    /// <summary>Trọng số các việc còn hiệu lực lúc đề nghị (jsonb: Id việc → trọng số) — để đối chiếu trước/sau.</summary>
    public string WeightsBefore { get; set; } = "{}";

    /// <summary>Lý do / nguyên nhân khách quan, căn cứ.</summary>
    public string Reason { get; set; } = string.Empty;

    public TaskChangeStatus Status { get; set; } = TaskChangeStatus.Pending;

    public Guid? RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Guid? DecidedById { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>Ý kiến khi duyệt / lý do khi không duyệt.</summary>
    public string? DecisionComment { get; set; }

    /// <summary>Việc mới được thêm khi duyệt việc đột xuất.</summary>
    public Guid? ResultTaskId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
