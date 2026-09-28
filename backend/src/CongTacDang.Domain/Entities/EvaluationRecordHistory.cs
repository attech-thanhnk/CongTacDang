using System;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Lịch sử chuyển trạng thái hồ sơ đánh giá: người làm, thời điểm, bước, hành động, lý do,
/// ảnh chụp điểm/mức trước–sau (docs/thiet-ke/luong-danh-gia.md mục 2).
/// </summary>
public class EvaluationRecordHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordId { get; set; }
    public EvaluationRecord Record { get; set; } = null!;
    public RecordStatus? FromStatus { get; set; }
    public RecordStatus ToStatus { get; set; }

    /// <summary>Bước thực hiện (null với thao tác không thuộc bước, ví dụ sửa ảnh chụp).</summary>
    public WorkflowStep? Step { get; set; }

    /// <summary>Hành động: hoàn thành / trả lại / mở lại / thêm vào danh sách / sửa ảnh chụp.</summary>
    public WorkflowAction Action { get; set; } = WorkflowAction.Complete;

    /// <summary>Lý do (bắt buộc khi trả lại, mở lại, sửa ảnh chụp).</summary>
    public string? Reason { get; set; }

    /// <summary>Điểm hiệu lực trước khi thực hiện (quyết định → thẩm định → tự chấm).</summary>
    public double? ScoreBefore { get; set; }

    /// <summary>Điểm hiệu lực sau khi thực hiện.</summary>
    public double? ScoreAfter { get; set; }

    /// <summary>Mức hiệu lực trước khi thực hiện (mức quyết định hoặc mức đề xuất mới nhất).</summary>
    public EvaluationGrade? GradeBefore { get; set; }

    /// <summary>Mức hiệu lực sau khi thực hiện.</summary>
    public EvaluationGrade? GradeAfter { get; set; }

    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
