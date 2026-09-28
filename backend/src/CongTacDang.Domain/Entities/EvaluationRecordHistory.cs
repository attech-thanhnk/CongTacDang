using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Lịch sử chuyển trạng thái hồ sơ đánh giá, phục vụ truy vết quy trình 5 bước.
/// </summary>
public class EvaluationRecordHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordId { get; set; }
    public EvaluationRecord Record { get; set; } = null!;
    public RecordStatus? FromStatus { get; set; }
    public RecordStatus ToStatus { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
