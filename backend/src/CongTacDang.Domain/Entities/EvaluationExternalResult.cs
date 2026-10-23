using System;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>Kết quả bước do cấp trên thực hiện (<see cref="StepMode.External"/>). Mỗi hồ sơ một bản ghi cho mỗi bước;
/// ghi lại sau khi mở lại hồ sơ thì cập nhật bản ghi cũ.</summary>
public class EvaluationExternalResult : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    public WorkflowStep Step { get; set; }

    /// <summary>Ví dụ "BTV Đảng ủy Tổng công ty".</summary>
    public string AuthorityName { get; set; } = string.Empty;

    public string? DocumentNumber { get; set; }

    public DateTime? DocumentDate { get; set; }

    public string? Comment { get; set; }

    /// <summary>Mức đề xuất / quyết định của bước có mức xếp loại.</summary>
    public EvaluationGrade Grade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Tùy chọn (thẩm định, quyết định).</summary>
    public double? Score { get; set; }

    /// <summary>Tham chiếu mềm, tùy chọn.</summary>
    public Guid? AttachmentId { get; set; }

    public Guid? RecordedById { get; set; }

    public string? RecordedByName { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
