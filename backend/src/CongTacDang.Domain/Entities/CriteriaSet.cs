using System;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>Bộ tiêu chí có phiên bản; nội dung jsonb <see cref="CriteriaSetContent"/>. Bản đã xuất bản bất biến,
/// chỉ nhân bản thành nháp mới.</summary>
public class CriteriaSet : IAuditableEntity, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Duy nhất.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CriteriaSetStatus Status { get; set; } = CriteriaSetStatus.Draft;

    /// <summary>Căn cứ văn bản, phạm vi áp dụng.</summary>
    public string? Notes { get; set; }

    public string Content { get; set; } = new CriteriaSetContent().ToJson();

    /// <summary>Bộ gốc khi được nhân bản.</summary>
    public Guid? SourceSetId { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Guid? PublishedBy { get; set; }

    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public CriteriaSetContent GetContent() => CriteriaSetContent.Parse(Content);

    /// <summary>Ảnh chụp nguyên bộ để lưu vào kỳ.</summary>
    public CriteriaSnapshot TakeSnapshot(DateTime takenAt) => new()
    {
        SetId = Id,
        Code = Code,
        Name = Name,
        TakenAt = takenAt,
        Content = GetContent()
    };
}
