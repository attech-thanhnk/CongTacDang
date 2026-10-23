using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Danh mục chức vụ; không có chức vụ cố định trong code.</summary>
public class Position : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Duy nhất trong các chức vụ chưa xóa.</summary>
    public string Name { get; set; } = string.Empty;

    public PositionSide Side { get; set; } = PositionSide.Administrative;

    /// <summary>Mã chức danh thống kê Mẫu 15A/15B (M1…M26); null = không thuộc nhóm thống kê.</summary>
    public string? StatCode { get; set; }

    /// <summary>Thẩm quyền xếp loại mặc định của người giữ chức vụ; null = không ảnh hưởng tới suy ra thẩm quyền.</summary>
    public ApprovalAuthority? DefaultApprovalAuthority { get; set; }

    /// <summary>Mã hồ sơ luồng (cách xét duyệt) gợi ý cho người giữ chức vụ khi thêm vào kỳ; null = theo thẩm quyền.
    /// Chỉ dùng khi kỳ có hồ sơ luồng mã này.</summary>
    public string? DefaultWorkflowProfileCode { get; set; }

    public bool IsLeadership { get; set; }

    /// <summary>Người đứng đầu: không tính vào trần Xuất sắc của nhóm chức danh (cả số Xuất sắc và số Hoàn thành tốt).</summary>
    public bool IsHead { get; set; }

    /// <summary>Kiểu trình bày biểu mẫu tự đánh giá mặc định; null = không xác định.</summary>
    public Guid? FormLayoutId { get; set; }

    public FormLayout? FormLayout { get; set; }

    /// <summary>Nhỏ đứng trước.</summary>
    public int SortOrder { get; set; }

    /// <summary>False = ngừng dùng: không gán mới được, chức vụ đã gán vẫn giữ.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Chức vụ cán bộ giữ tại một đơn vị; được kiêm nhiệm, đúng một chức vụ đang hiệu lực là chức vụ chính.</summary>
public class MemberPosition : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public PartyMemberProfile? User { get; set; }

    public Guid PositionId { get; set; }

    public Position? Position { get; set; }

    /// <summary>Chỉ một trong hai: tổ chức Đảng hoặc đơn vị chính quyền (có thể cả hai trống).</summary>
    public Guid? PartyCellId { get; set; }

    public PartyCell? PartyCell { get; set; }

    public Guid? DepartmentId { get; set; }

    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Mặc định dùng làm chức danh hiển thị trên văn bản.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Giữ chức vụ dạng "Quyền": đánh giá như chính chức vụ, chỉ khác chức danh hiển thị ("Quyền " + tên).</summary>
    public bool IsActing { get; set; }

    /// <summary>UTC, bao gồm.</summary>
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    /// <summary>UTC, không bao gồm; null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Số quyết định bổ nhiệm…</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Chưa xóa và <c>ValidFrom ≤ at &lt; ValidTo</c> (hoặc ValidTo null).</summary>
    public bool IsEffectiveAt(DateTime at) =>
        !IsDeleted && ValidFrom <= at && (ValidTo == null || at < ValidTo.Value);
}
