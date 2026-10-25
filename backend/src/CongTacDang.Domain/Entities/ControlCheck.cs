using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

public enum ControlCheckStatus
{
    Draft = 0,
    Submitted = 1,
    Confirmed = 2,

    /// <summary>Hủy có lý do; hồ sơ trong phiếu được giải phóng.</summary>
    Cancelled = 3
}

/// <summary>Kết luận kiểm soát; null khi phiếu chưa kết luận.</summary>
public enum ControlConclusion
{
    /// <summary>Đủ điều kiện trình phê duyệt.</summary>
    Eligible = 1,

    /// <summary>Cần hoàn thiện trước khi trình.</summary>
    NeedsWork = 2
}

/// <summary>Kết quả một mục kiểm soát; null khi chưa chọn.</summary>
public enum ControlItemResult
{
    Pass = 1,

    /// <summary>Chưa đạt; cản kết luận "Đủ điều kiện".</summary>
    Fail = 2,

    /// <summary>Không áp dụng (K/AD); cần lý do, không cản kết luận.</summary>
    NotApplicable = 3
}

/// <summary>Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán (Mẫu 19) cho một nhóm hồ sơ trong một kỳ.</summary>
public class ControlCheck : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PeriodId { get; set; }

    public EvaluationPeriod Period { get; set; } = null!;

    /// <summary>Cơ quan/đơn vị của phiếu, dùng lọc danh sách và kiểm quyền xem.</summary>
    public Guid? DepartmentId { get; set; }

    public Guid? PartyCellId { get; set; }

    /// <summary>Nhóm chức danh/phạm vi rà soát.</summary>
    public string GroupName { get; set; } = string.Empty;

    public ControlCheckStatus Status { get; set; } = ControlCheckStatus.Draft;

    public ControlConclusion? Conclusion { get; set; }

    /// <summary>Bắt buộc khi kết luận cần hoàn thiện.</summary>
    public string? NeedsWorkContent { get; set; }

    /// <summary>Phiên bản bộ mười mục đã chụp vào phiếu.</summary>
    public int ItemsVersion { get; set; }

    /// <summary>Tăng mỗi lần mở lại phiếu đã xác nhận.</summary>
    public int Revision { get; set; } = 1;

    /// <summary>Người gửi xác nhận gần nhất (người lập).</summary>
    public Guid? PerformedById { get; set; }

    public string PerformedByName { get; set; } = string.Empty;

    public DateTime? PerformedAt { get; set; }

    public Guid? ConfirmedById { get; set; }

    public string? ConfirmedByName { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Lý do trả lại gần nhất; xóa khi gửi lại.</summary>
    public string? ReturnReason { get; set; }

    public ICollection<ControlCheckItem> Items { get; set; } = new List<ControlCheckItem>();

    public ICollection<ControlCheckRecord> Records { get; set; } = new List<ControlCheckRecord>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Một trong mười mục kiểm soát; nguyên văn mục được chụp lúc lập phiếu.</summary>
public class ControlCheckItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CheckId { get; set; }

    public ControlCheck Check { get; set; } = null!;

    /// <summary><c>K01</c>…<c>K10</c>.</summary>
    public string ItemCode { get; set; } = string.Empty;

    public string ItemText { get; set; } = string.Empty;

    public ControlItemResult? Result { get; set; }

    /// <summary>Minh chứng/biện pháp; bắt buộc khi chưa đạt hoặc không áp dụng.</summary>
    public string? Evidence { get; set; }
}

/// <summary>Hồ sơ thuộc phiếu kèm điểm/mức lúc kiểm soát.</summary>
public class ControlCheckRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CheckId { get; set; }

    public ControlCheck Check { get; set; } = null!;

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>Còn thuộc phiếu hiệu lực; mỗi hồ sơ chỉ có một dòng đang hiệu lực.</summary>
    public bool IsCurrent { get; set; } = true;

    public EvaluationGrade GradeAtCheck { get; set; } = EvaluationGrade.ChuaXepLoai;

    public double ScoreAtCheck { get; set; }
}
