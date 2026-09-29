using System;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Kết quả của một bước do cấp trên / cơ quan ngoài hệ thống thực hiện (chế độ <see cref="StepMode.External"/> trong hồ sơ luồng),
/// do người có quyền <c>evaluation.external.record</c> ghi nhận: cơ quan, số/ngày văn bản, nhận xét, mức đề xuất/quyết định,
/// tệp đính kèm tùy chọn. Mỗi hồ sơ một bản ghi cho mỗi bước (ghi lại sau khi mở lại hồ sơ thì cập nhật bản ghi cũ).
/// </summary>
public class EvaluationExternalResult : IAuditableEntity
{
    /// <summary>Mã bản ghi.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Hồ sơ đánh giá.</summary>
    public Guid RecordId { get; set; }

    /// <summary>Hồ sơ đánh giá (navigation).</summary>
    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Bước được ghi nhận.</summary>
    public WorkflowStep Step { get; set; }

    /// <summary>Cơ quan / cấp thực hiện (ví dụ "BTV Đảng ủy Tổng công ty").</summary>
    public string AuthorityName { get; set; } = string.Empty;

    /// <summary>Số văn bản.</summary>
    public string? DocumentNumber { get; set; }

    /// <summary>Ngày văn bản.</summary>
    public DateTime? DocumentDate { get; set; }

    /// <summary>Nhận xét / nội dung kết luận.</summary>
    public string? Comment { get; set; }

    /// <summary>Mức đề xuất / quyết định (bước có mức xếp loại).</summary>
    public EvaluationGrade Grade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Điểm (tùy chọn — thẩm định, quyết định).</summary>
    public double? Score { get; set; }

    /// <summary>Tệp đính kèm (tùy chọn, tham chiếu mềm tới tệp đính kèm).</summary>
    public Guid? AttachmentId { get; set; }

    /// <summary>Người ghi nhận.</summary>
    public Guid? RecordedById { get; set; }

    /// <summary>Họ tên người ghi nhận.</summary>
    public string? RecordedByName { get; set; }

    /// <summary>Thời điểm ghi nhận.</summary>
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
