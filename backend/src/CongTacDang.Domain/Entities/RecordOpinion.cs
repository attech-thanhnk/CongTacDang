using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Loại ý kiến ghi nhận trước phê duyệt danh mục và trước nhận xét.</summary>
public enum RecordOpinionKind
{
    /// <summary>Kết quả họp rà soát, thống nhất danh mục sản phẩm của tập thể lãnh đạo.</summary>
    ListMeeting = 1,

    /// <summary>Kết quả rà soát tính hợp lệ của danh mục (Phòng TCCB-LĐ).</summary>
    ListCheck = 2,

    /// <summary>Ý kiến của Phó Giám đốc phụ trách về danh mục, trước khi Giám đốc phê duyệt.</summary>
    DeputyOnList = 3,

    /// <summary>Ý kiến của Phó Giám đốc phụ trách về đề xuất mức, trước khi Giám đốc nhận xét.</summary>
    DeputyOnReview = 4
}

/// <summary>Ý kiến ghi nhận trên hồ sơ; không chặn bước nào của luồng. Ghi thêm bản mới thay vì sửa bản cũ.</summary>
public class RecordOpinion : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    public RecordOpinionKind Kind { get; set; }

    public string Content { get; set; } = string.Empty;

    public Guid? RecordedById { get; set; }

    public string RecordedByName { get; set; } = string.Empty;

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
