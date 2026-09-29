using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Bản nháp phần nhập tay của một báo cáo tổng hợp theo kỳ và phạm vi tổ chức Đảng (task 19 — Mẫu 16: số văn bản, nơi gửi,
/// đề xuất…). Số liệu của báo cáo luôn tính lại từ kết quả kỳ khi xuất, không lưu ở đây.
/// </summary>
public class ReportDraft : IAuditableEntity, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Kỳ đánh giá.</summary>
    public Guid PeriodId { get; set; }
    public EvaluationPeriod Period { get; set; } = null!;

    /// <summary>Tổ chức Đảng lập báo cáo; null = toàn Đảng bộ.</summary>
    public Guid? PartyCellId { get; set; }
    public PartyCell? PartyCell { get; set; }

    /// <summary>Mã biểu mẫu (ví dụ <c>M16</c>).</summary>
    public string FormCode { get; set; } = string.Empty;

    /// <summary>Nội dung nhập tay (jsonb, khóa theo mã mục của biểu mẫu).</summary>
    public string Content { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
