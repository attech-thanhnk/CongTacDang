using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể Công việc / Sản phẩm chuyên môn đăng ký và đánh giá (Mẫu 01 & Mẫu 02) theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class EvaluationTask : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    /// <summary>Mã định danh công việc</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã hồ sơ đánh giá sở hữu</summary>
    public Guid RecordId { get; set; }

    /// <summary>Đối tượng hồ sơ đánh giá</summary>
    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Thứ tự công việc trong danh sách (từ 1 đến 7 việc)</summary>
    public int TaskOrder { get; set; }

    /// <summary>Tên sản phẩm, công việc chuyên môn</summary>
    public string TaskName { get; set; } = string.Empty;

    /// <summary>Chỉ tiêu đầu ra, quy cách sản phẩm, tiêu chuẩn kỹ thuật</summary>
    public string TargetOutput { get; set; } = string.Empty;

    /// <summary>Trọng số điểm giao việc (Tổng trọng số của 3-7 việc phải = đúng 70.0 điểm)</summary>
    public double Weight { get; set; }

    /// <summary>Mốc thời gian / hạn chót hoàn thành trong quý</summary>
    public DateTime Deadline { get; set; }

    #region Tự chấm điểm 4 Tiêu chí A-B-C-D theo Khung chức danh (0.0 đến 1.0)

    /// <summary>Tỷ lệ hoàn thành Tiêu chí A - Khối lượng công việc (0.0 đến 1.0 tương ứng 0% đến 100%)</summary>
    public double CriteriaA_Ratio { get; set; } = 1.0;

    /// <summary>Tỷ lệ hoàn thành Tiêu chí B - Chất lượng sản phẩm (0.0 đến 1.0 tương ứng 0% đến 100%)</summary>
    public double CriteriaB_Ratio { get; set; } = 1.0;

    /// <summary>Tỷ lệ hoàn thành Tiêu chí C - Tiến độ thực hiện (0.0 đến 1.0 tương ứng 0% đến 100%)</summary>
    public double CriteriaC_Ratio { get; set; } = 1.0;

    /// <summary>Tỷ lệ hoàn thành Tiêu chí D - Hiệu quả, sáng kiến đổi mới (0.0 đến 1.0 tương ứng 0% đến 100%)</summary>
    public double CriteriaD_Ratio { get; set; } = 1.0;

    /// <summary>Điểm tự chấm của công việc (bằng Trọng số nhân tổng tỷ trọng đạt được theo Khung chức danh)</summary>
    public double SelfScore { get; set; }

    /// <summary>Điểm do cấp ủy / lãnh đạo thẩm định chấm</summary>
    public double? SupervisorScore { get; set; }

    /// <summary>Có đạt tiêu chuẩn vượt chuẩn tiến độ/chất lượng để xét danh hiệu Xuất sắc hay không</summary>
    public bool IsExceedStandard { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Mã tệp tin đính kèm làm bằng chứng dẫn chiếu (nếu có)</summary>
    public Guid? AttachmentId { get; set; }

    /// <summary>Đối tượng tệp tin đính kèm</summary>
    public TaskAttachment? Attachment { get; set; }

    #endregion
}
