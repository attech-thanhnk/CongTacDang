using System;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>Lịch sử chuyển trạng thái hồ sơ, kèm điểm/mức trước–sau.</summary>
public class EvaluationRecordHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordId { get; set; }
    public EvaluationRecord Record { get; set; } = null!;
    public RecordStatus? FromStatus { get; set; }
    public RecordStatus ToStatus { get; set; }

    /// <summary>Null với thao tác không thuộc bước (ví dụ sửa ảnh chụp).</summary>
    public WorkflowStep? Step { get; set; }

    public WorkflowAction Action { get; set; } = WorkflowAction.Complete;

    /// <summary>Bắt buộc khi trả lại, mở lại, sửa ảnh chụp.</summary>
    public string? Reason { get; set; }

    /// <summary>Điểm hiệu lực (quyết định → thẩm định → tự chấm).</summary>
    public double? ScoreBefore { get; set; }

    public double? ScoreAfter { get; set; }

    /// <summary>Mức quyết định hoặc mức đề xuất mới nhất.</summary>
    public EvaluationGrade? GradeBefore { get; set; }

    public EvaluationGrade? GradeAfter { get; set; }

    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
