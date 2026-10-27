using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Đề nghị thay đổi danh mục việc trong quý (Mẫu 04, 05).</summary>
public enum TaskChangeType
{
    /// <summary>Điều chỉnh nội dung, chỉ tiêu, thời hạn, trọng số (Mẫu 04).</summary>
    Adjust = 1,

    /// <summary>Chuyển sang quý sau (Mẫu 04); không chấm trong quý này.</summary>
    Carryover = 2,

    /// <summary>Loại trừ do khách quan, bất khả kháng (Mẫu 04); không chấm.</summary>
    Exclude = 3,

    /// <summary>Việc đột xuất (Mẫu 05); thêm một việc mới.</summary>
    Urgent = 4
}

public enum TaskChangeStatus
{
    Pending = 0,

    /// <summary>Đã áp vào danh mục việc của hồ sơ.</summary>
    Approved = 1,

    /// <summary>Có lý do.</summary>
    Rejected = 2
}

/// <summary>Việc vẫn hiện trên phiếu nhưng trọng số 0, không chấm.</summary>
public enum TaskChangeMark
{
    Excluded = 1,

    CarriedOver = 2
}

/// <summary>Đề nghị sau khi danh mục đã duyệt và trước khi nộp tự chấm; khi duyệt được áp vào
/// <see cref="EvaluationTask"/> của hồ sơ.</summary>
public class TaskChangeRequest : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    public TaskChangeType Type { get; set; }

    /// <summary>Null với việc đột xuất.</summary>
    public Guid? TaskId { get; set; }

    /// <summary>Để hiển thị khi việc đã đổi tên.</summary>
    public string? TaskNameSnapshot { get; set; }

    /// <summary>jsonb <c>TaskChangeProposal</c>.</summary>
    public string Proposal { get; set; } = "{}";

    /// <summary>jsonb: Id việc → trọng số, cho các việc còn hiệu lực.</summary>
    public string Weights { get; set; } = "{}";

    /// <summary>jsonb: Id việc → trọng số lúc đề nghị, để đối chiếu trước/sau.</summary>
    public string WeightsBefore { get; set; } = "{}";

    /// <summary>jsonb <c>TaskChangeProposal</c>: nội dung việc đích lúc đề nghị, để in Mẫu 04 khi đã duyệt và ghi đè; null với đề nghị lập trước khi lưu bản chụp.</summary>
    public string? TaskBefore { get; set; }

    /// <summary>Lý do, căn cứ.</summary>
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
