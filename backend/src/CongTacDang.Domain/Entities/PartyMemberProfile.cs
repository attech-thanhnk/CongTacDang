using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Hồ sơ cán bộ kiêm tài khoản đăng nhập; gồm thông tin Đảng và chính quyền.</summary>
public class PartyMemberProfile : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>In trên Mẫu 09C.</summary>
    public DateOnly? DateOfBirth { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    #region Vai 1: Công tác Đảng

    public bool IsPartyMember { get; set; } = true;

    public string? PartyCardNumber { get; set; }

    /// <summary>Ngày vào Đảng (dự bị).</summary>
    public DateTime? JoinPartyDate { get; set; }

    /// <summary>Ngày công nhận chính thức.</summary>
    public DateTime? OfficialPartyDate { get; set; }

    /// <summary>Nơi sinh hoạt Đảng (thường là Chi bộ).</summary>
    public Guid? PartyCellId { get; set; }

    public PartyCell? PartyCell { get; set; }

    #endregion

    #region Vai 2: Chính quyền & Chuyên môn

    /// <summary>Đơn vị công tác chính.</summary>
    public Guid? DepartmentId { get; set; }

    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Chức danh trên văn bản; mặc định = tên chức vụ chính, sửa tay được.</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>Khung tỷ trọng mặc định (ví dụ "K2"); hồ sơ đánh giá chụp lại khi thêm vào kỳ.</summary>
    public string? WeightFrameCode { get; set; }

    #endregion

    /// <summary>Thẩm quyền đang áp dụng = <see cref="ApprovalAuthorityOverride"/> nếu có, ngược lại CapTren khi có chức vụ
    /// hiệu lực mặc định CapTren. Tính lại khi chức vụ hoặc giá trị ghi đè thay đổi.</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; } = ApprovalAuthority.CoSo;

    /// <summary>Ghi đè thẩm quyền suy ra từ chức vụ; null = suy ra.</summary>
    public ApprovalAuthority? ApprovalAuthorityOverride { get; set; }

    /// <summary>Bắt buộc khi có <see cref="ApprovalAuthorityOverride"/>.</summary>
    public string? ApprovalAuthorityOverrideReason { get; set; }

    /// <summary>Ghi đè kiểu trình bày suy ra từ chức vụ; null = suy ra.</summary>
    public Guid? FormLayoutOverrideId { get; set; }

    public FormLayout? FormLayoutOverride { get; set; }

    /// <summary>Đổi khi đổi/đặt lại mật khẩu, khóa hoặc xóa, để vô hiệu phiên đang dùng.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    public bool IsActive { get; set; } = true;

    /// <summary>Buộc đổi mật khẩu tạm ở lần đăng nhập kế tiếp.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Số lần đăng nhập sai liên tiếp.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Hết khóa đăng nhập tạm thời.</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}
