using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Phần nhập tay của báo cáo tổng hợp theo kỳ và tổ chức Đảng; số liệu luôn tính lại khi xuất, không lưu ở đây.</summary>
public class ReportDraft : IAuditableEntity, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PeriodId { get; set; }
    public EvaluationPeriod Period { get; set; } = null!;

    /// <summary>Null = toàn Đảng bộ.</summary>
    public Guid? PartyCellId { get; set; }
    public PartyCell? PartyCell { get; set; }

    /// <summary>Ví dụ <c>M16</c>.</summary>
    public string FormCode { get; set; } = string.Empty;

    /// <summary>jsonb theo mã mục của biểu mẫu.</summary>
    public string Content { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
