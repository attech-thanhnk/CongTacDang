using System;
using System.Text.Json.Serialization;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.DTOs;

/// <summary>Yêu cầu tạo tài khoản (<c>POST /api/users</c>).</summary>
public class CreateAccountRequestDto
{
    /// <summary>Bắt buộc; chuẩn hóa chữ thường; <c>^[a-z0-9._-]{3,50}$</c>; không trùng kể cả tài khoản đã xóa.</summary>
    public string? Username { get; set; }

    /// <summary>Bắt buộc.</summary>
    public string? FullName { get; set; }

    /// <summary>"yyyy-MM-dd" (tùy chọn).</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Tùy chọn; đúng định dạng nếu có.</summary>
    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>Có giá trị → là Đảng viên.</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Hiển thị trên văn bản.</summary>
    public string? PositionTitle { get; set; }

    /// <summary>Phải tồn tại, đang hoạt động.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Phải tồn tại, đang hoạt động.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Đặt tay; null → suy ra từ chức vụ. Nhận tên (<c>"CoSo"</c>, <c>"CapTren"</c>) hoặc số (1, 2).</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ApprovalAuthority? ApprovalAuthority { get; set; }

    /// <summary>Bắt buộc khi <see cref="ApprovalAuthority"/> khác cấp suy ra từ chức vụ.</summary>
    public string? ApprovalAuthorityReason { get; set; }

    /// <summary>Theo bộ tiêu chí (vd. "K2"); trống → chưa chọn.</summary>
    public string? WeightFrameCode { get; set; }
}

/// <summary>Trường null → giữ nguyên.</summary>
public class UpdateAccountRequestDto
{
    /// <summary>Không được để trống nếu gửi.</summary>
    public string? FullName { get; set; }

    /// <summary>"yyyy-MM-dd"; chuỗi rỗng → xóa.</summary>
    public string? DateOfBirth { get; set; }

    /// <summary>Chuỗi rỗng → xóa.</summary>
    public string? Email { get; set; }

    /// <summary>Chuỗi rỗng → xóa.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Chuỗi rỗng → xóa (không còn là Đảng viên).</summary>
    public string? PartyCardNumber { get; set; }

    public string? PositionTitle { get; set; }

    /// <summary><c>Guid.Empty</c> → bỏ gán.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary><c>Guid.Empty</c> → bỏ gán.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Chuỗi rỗng → bỏ chọn.</summary>
    public string? WeightFrameCode { get; set; }
}

/// <summary>Một tài khoản trong danh sách quản trị (<c>GET /api/users</c>, <c>GET /api/users/{id}</c>).</summary>
public class AccountListItemDto
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    /// <summary>JSON "yyyy-MM-dd"; null khi chưa nhập.</summary>
    public DateOnly? DateOfBirth { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? PartyCardNumber { get; set; }

    public bool IsPartyMember { get; set; }

    public string PositionTitle { get; set; } = string.Empty;

    public string? WeightFrameCode { get; set; }

    public Guid? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public Guid? PartyCellId { get; set; }

    public string? PartyCellName { get; set; }

    /// <summary>Đang áp dụng (đặt tay hoặc suy ra từ chức vụ).</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; }

    /// <summary>null = suy ra từ chức vụ.</summary>
    public ApprovalAuthority? ApprovalAuthorityOverride { get; set; }

    public string? ApprovalAuthorityOverrideReason { get; set; }

    /// <summary>Quyền quản lý bao trùm đơn vị của tài khoản; chỉ trả ở chi tiết.</summary>
    public bool CanManage { get; set; }

    /// <summary>false: quản trị đã khóa.</summary>
    public bool IsActive { get; set; }

    /// <summary>Khóa tạm thời do nhập sai mật khẩu nhiều lần.</summary>
    public bool IsLockedOut { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>Liên tiếp.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Mật khẩu tạm, phải đổi ở lần đăng nhập kế tiếp.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>UTC.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>Một bản ghi nhật ký đăng nhập (<c>GET /api/audit/logins</c>).</summary>
public class LoginEventDto
{
    public Guid Id { get; set; }

    /// <summary>null nếu tên đăng nhập không tồn tại.</summary>
    public Guid? UserId { get; set; }

    public string UsernameAttempted { get; set; } = string.Empty;

    /// <summary><c>Success</c> | <c>InvalidPassword</c> | <c>UnknownUser</c> | <c>LockedOut</c> | <c>Disabled</c>.</summary>
    public string Result { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; }
}
