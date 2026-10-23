using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Sản phẩm chuyên môn đăng ký (Mẫu 01) và tự chấm (Mẫu 02).</summary>
public class EvaluationTask : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Bắt đầu từ 1.</summary>
    public int TaskOrder { get; set; }

    public string TaskName { get; set; } = string.Empty;

    /// <summary>Chỉ tiêu đầu ra, quy cách, tiêu chuẩn kỹ thuật.</summary>
    public string TargetOutput { get; set; } = string.Empty;

    /// <summary>Tổng trọng số các việc phải bằng tổng trọng số của bộ tiêu chí.</summary>
    public double Weight { get; set; }

    public DateTime Deadline { get; set; }

    /// <summary>Thuộc bộ tiêu chí của kỳ; null nếu chưa chọn.</summary>
    public string? AxisCode { get; set; }

    /// <summary>Không bắt buộc.</summary>
    public string? ProductCode { get; set; }

    /// <summary>Null với dữ liệu đăng ký trước khi có trường này.</summary>
    public TaskKind? Kind { get; set; }

    /// <summary>Null với dữ liệu cũ.</summary>
    public TaskRole? Role { get; set; }

    public string? ExceedCriteria { get; set; }

    public string? EvidenceSource { get; set; }

    public string? Confirmer { get; set; }

    /// <summary>Đã loại trừ / chuyển tiếp (Mẫu 04 đã duyệt): giữ trên phiếu, trọng số 0, không chấm; null = còn hiệu lực.</summary>
    public TaskChangeMark? ChangeStatus { get; set; }

    #region Tự chấm điểm 4 Tiêu chí A-B-C-D theo khung tỷ trọng (0.0 đến 1.0)

    /// <summary>A — khối lượng.</summary>
    public double CriteriaA_Ratio { get; set; } = 1.0;

    /// <summary>B — chất lượng.</summary>
    public double CriteriaB_Ratio { get; set; } = 1.0;

    /// <summary>C — tiến độ.</summary>
    public double CriteriaC_Ratio { get; set; } = 1.0;

    /// <summary>D — hiệu quả, sáng kiến.</summary>
    public double CriteriaD_Ratio { get; set; } = 1.0;

    /// <summary>Trọng số × tỷ lệ đạt theo khung tỷ trọng.</summary>
    public double SelfScore { get; set; }

    /// <summary>Điểm thẩm định.</summary>
    public double? SupervisorScore { get; set; }

    /// <summary>Vượt chuẩn, dùng khi xét mức Xuất sắc.</summary>
    public bool IsExceedStandard { get; set; } = false;

    /// <summary>1 = chủ động, chất lượng cao; 2 = cơ bản đáp ứng; 3 = không đáp ứng. Null khi chưa thẩm định.</summary>
    public int? AppraisalLevel { get; set; }

    /// <summary>Không bắt buộc.</summary>
    public string? AppraisalComment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public Guid? AttachmentId { get; set; }

    public TaskAttachment? Attachment { get; set; }

    #endregion
}
