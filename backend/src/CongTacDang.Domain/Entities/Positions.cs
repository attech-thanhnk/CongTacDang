using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Danh mục chức vụ (bảng <c>positions</c>): Bí thư Đảng ủy, Giám đốc, Trưởng phòng… Quản trị thêm/sửa/ngừng dùng,
/// không có chức vụ cố định trong code.
/// </summary>
public class Position : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh chức vụ.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tên chức vụ (duy nhất trong các chức vụ chưa xóa).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bên: Đảng / Chính quyền / Đoàn thể / Khác.</summary>
    public PositionSide Side { get; set; } = PositionSide.Administrative;

    /// <summary>
    /// Mã chức danh thống kê theo Mẫu 15A/15B (<c>M1</c>…<c>M26</c>, HD03 tr.73–76); null = không thuộc nhóm thống kê.
    /// </summary>
    public string? StatCode { get; set; }

    /// <summary>
    /// Cấp có thẩm quyền quyết định xếp loại mặc định của người giữ chức vụ (HD03 mục I.5); null = không xác định
    /// (không ảnh hưởng tới suy ra thẩm quyền).
    /// </summary>
    public ApprovalAuthority? DefaultApprovalAuthority { get; set; }

    /// <summary>Chức vụ lãnh đạo, quản lý.</summary>
    public bool IsLeadership { get; set; }

    /// <summary>Thứ tự hiển thị (nhỏ đứng trước).</summary>
    public int SortOrder { get; set; }

    /// <summary>Đang dùng (false = ngừng dùng: không gán mới được, chức vụ đã gán vẫn giữ).</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>
/// Một chức vụ cán bộ đang/đã giữ tại một đơn vị (bảng <c>member_positions</c>). Một người có thể giữ nhiều chức vụ ở
/// nhiều đơn vị cùng lúc (kiêm nhiệm); đúng một chức vụ đang hiệu lực được đánh dấu là chức vụ chính.
/// </summary>
public class MemberPosition : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cán bộ giữ chức vụ.</summary>
    public Guid UserId { get; set; }

    /// <summary>Cán bộ giữ chức vụ (navigation).</summary>
    public PartyMemberProfile? User { get; set; }

    /// <summary>Chức vụ trong danh mục.</summary>
    public Guid PositionId { get; set; }

    /// <summary>Chức vụ (navigation).</summary>
    public Position? Position { get; set; }

    /// <summary>Tổ chức Đảng nơi giữ chức vụ (chỉ một trong hai: tổ chức Đảng hoặc đơn vị chính quyền; có thể cả hai trống).</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Tổ chức Đảng (navigation).</summary>
    public PartyCell? PartyCell { get; set; }

    /// <summary>Đơn vị chính quyền nơi giữ chức vụ.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Đơn vị chính quyền (navigation).</summary>
    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Chức vụ chính (mặc định dùng làm chức danh hiển thị trên văn bản).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Hiệu lực từ (UTC, bao gồm).</summary>
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    /// <summary>Hiệu lực đến (UTC, không bao gồm); null = đang giữ, không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Ghi chú (số quyết định bổ nhiệm…).</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Đang hiệu lực tại <paramref name="at"/>: chưa xóa và <c>ValidFrom ≤ at &lt; ValidTo</c> (hoặc ValidTo null).</summary>
    public bool IsEffectiveAt(DateTime at) =>
        !IsDeleted && ValidFrom <= at && (ValidTo == null || at < ValidTo.Value);
}
