using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

public enum CrosscheckStatus
{
    /// <summary>Đã yêu cầu, cơ quan đối soát chưa lập.</summary>
    Requested = 0,

    /// <summary>Đang lập (hoặc được trả lại để lập lại).</summary>
    InProgress = 1,

    /// <summary>Đã gửi, chờ lãnh đạo cơ quan chuyên môn xác nhận.</summary>
    Submitted = 2,

    /// <summary>Đã xác nhận; khóa sửa.</summary>
    Confirmed = 3,

    /// <summary>Hủy có lý do.</summary>
    Cancelled = 4
}

/// <summary>Kết luận đối soát của một dòng chỉ tiêu.</summary>
public enum CrosscheckResult
{
    /// <summary>Đạt chuẩn.</summary>
    Pass = 1,

    /// <summary>Vượt chuẩn.</summary>
    Exceed = 2,

    /// <summary>Có khiếm khuyết.</summary>
    Defect = 3
}

/// <summary>Dạng đề xuất tăng/giảm điểm; chỉ là tham khảo, không tự đổi điểm hồ sơ.</summary>
public enum CrosscheckAdjustment
{
    None = 0,

    /// <summary>Giảm điểm, kèm số điểm.</summary>
    Decrease = 1,

    /// <summary>Ghi nhận vượt chuẩn.</summary>
    RecognizeExceed = 2
}

/// <summary>Phiếu ý kiến chuyên môn độc lập / đối soát dữ liệu chuyên ngành (Mẫu 03) cho một hồ sơ và một cơ quan đối soát.</summary>
public class ExpertCrosscheck : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PeriodId { get; set; }

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Ảnh chụp từ hồ sơ lúc yêu cầu.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Ảnh chụp từ hồ sơ lúc yêu cầu.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Cơ quan đối soát trong danh mục; null khi cơ quan ngoài hệ thống (<see cref="ExpertAgencyName"/>).</summary>
    public Guid? ExpertDepartmentId { get; set; }

    public AdministrativeDepartment? ExpertDepartment { get; set; }

    /// <summary>Tên cơ quan ngoài hệ thống; người yêu cầu nhập thay.</summary>
    public string? ExpertAgencyName { get; set; }

    /// <summary>jsonb: mảng mã lĩnh vực (<c>CrosscheckFields</c>).</summary>
    public string Fields { get; set; } = "[]";

    /// <summary>Ô "Kính gửi".</summary>
    public string AddresseeName { get; set; } = string.Empty;

    public string RequestReason { get; set; } = string.Empty;

    public CrosscheckStatus Status { get; set; } = CrosscheckStatus.Requested;

    public Guid? RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public Guid? SubmittedById { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? SubmittedAt { get; set; }

    public Guid? ConfirmedById { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Ngày trên phiếu.</summary>
    public DateOnly? IssuedDate { get; set; }

    /// <summary>Lý do lần trả lại gần nhất; xóa khi gửi lại.</summary>
    public string? ReturnReason { get; set; }

    public string? CancelReason { get; set; }

    public Guid? CancelledById { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }

    public List<ExpertCrosscheckItem> Items { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Một dòng chỉ tiêu/nhiệm vụ cần đối soát.</summary>
public class ExpertCrosscheckItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CrosscheckId { get; set; }

    public ExpertCrosscheck Crosscheck { get; set; } = null!;

    public int Order { get; set; }

    /// <summary>Sản phẩm đăng ký liên quan; dòng tự do thì null (tham chiếu mềm).</summary>
    public Guid? TaskId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    /// <summary>Dữ liệu cá nhân tự khai.</summary>
    public string SelfData { get; set; } = string.Empty;

    /// <summary>Kết quả đối soát với dữ liệu gốc.</summary>
    public string SourceData { get; set; } = string.Empty;

    public CrosscheckResult? Result { get; set; }

    public CrosscheckAdjustment AdjustmentType { get; set; } = CrosscheckAdjustment.None;

    /// <summary>Số điểm giảm, bắt buộc khi <see cref="CrosscheckAdjustment.Decrease"/>.</summary>
    public double? AdjustmentPoints { get; set; }

    /// <summary>Căn cứ, xác nhận chuyên môn.</summary>
    public string BasisText { get; set; } = string.Empty;

    public string BasisDocNumber { get; set; } = string.Empty;

    public DateOnly? BasisDocDate { get; set; }

    public string BasisAgency { get; set; } = string.Empty;

    /// <summary>Tiêu chí không áp dụng (in "K/AD"); các cột kết quả để trống.</summary>
    public bool NotApplicable { get; set; }
}
