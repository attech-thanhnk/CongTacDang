using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.DTOs;

/// <summary>Yêu cầu tạo tài khoản (<c>POST /api/users</c>).</summary>
public class CreateAccountRequestDto
{
    /// <summary>Tên đăng nhập (bắt buộc; chuẩn hóa chữ thường; <c>^[a-z0-9._-]{3,50}$</c>; không trùng kể cả tài khoản đã xóa).</summary>
    public string? Username { get; set; }

    /// <summary>Họ và tên (bắt buộc).</summary>
    public string? FullName { get; set; }

    /// <summary>Email (tùy chọn, đúng định dạng nếu có).</summary>
    public string? Email { get; set; }

    /// <summary>Số điện thoại (tùy chọn).</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Số thẻ Đảng viên; có giá trị → là Đảng viên.</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Chức danh hiển thị trên văn bản.</summary>
    public string? PositionTitle { get; set; }

    /// <summary>Tên cũ của <see cref="PositionTitle"/> (giữ tương thích giao diện cũ).</summary>
    public string? AdminTitle { get; set; }

    /// <summary>Phòng/đơn vị (phải tồn tại, đang hoạt động).</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Chi bộ sinh hoạt (phải tồn tại, đang hoạt động).</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Cấp có thẩm quyền quyết định xếp loại (mặc định <c>CoSo</c>).</summary>
    public ApprovalAuthority? ApprovalAuthority { get; set; }
}

/// <summary>Yêu cầu cập nhật tài khoản (<c>PUT /api/users/{id}</c>). Trường null → giữ nguyên.</summary>
public class UpdateAccountRequestDto
{
    /// <summary>Họ và tên (không được để trống nếu gửi).</summary>
    public string? FullName { get; set; }

    /// <summary>Email; chuỗi rỗng → xóa.</summary>
    public string? Email { get; set; }

    /// <summary>Số điện thoại; chuỗi rỗng → xóa.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>Số thẻ Đảng viên; chuỗi rỗng → xóa (không còn là Đảng viên).</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Chức danh hiển thị.</summary>
    public string? PositionTitle { get; set; }

    /// <summary>Tên cũ của <see cref="PositionTitle"/> (giữ tương thích giao diện cũ).</summary>
    public string? AdminTitle { get; set; }

    /// <summary>Phòng; <c>Guid.Empty</c> → bỏ gán.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Chi bộ; <c>Guid.Empty</c> → bỏ gán.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Cấp có thẩm quyền quyết định xếp loại.</summary>
    public ApprovalAuthority? ApprovalAuthority { get; set; }

    /// <summary>
    /// (Giữ tương thích) Đổi trạng thái hoạt động — tương đương gọi activate/deactivate, áp dụng cùng chốt chặn.
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>Một tài khoản trong danh sách quản trị (<c>GET /api/users</c>, <c>GET /api/users/{id}</c>).</summary>
public class AccountListItemDto
{
    /// <summary>Id tài khoản.</summary>
    public Guid Id { get; set; }

    /// <summary>Tên đăng nhập.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Họ và tên.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Email.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Số điện thoại.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Số thẻ Đảng viên.</summary>
    public string? PartyCardNumber { get; set; }

    /// <summary>Là Đảng viên.</summary>
    public bool IsPartyMember { get; set; }

    /// <summary>Chức danh.</summary>
    public string PositionTitle { get; set; } = string.Empty;

    /// <summary>Id Phòng.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Tên Phòng.</summary>
    public string? DepartmentName { get; set; }

    /// <summary>Id Chi bộ.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Tên Chi bộ.</summary>
    public string? PartyCellName { get; set; }

    /// <summary>Cấp có thẩm quyền quyết định xếp loại.</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; }

    /// <summary>Đang hoạt động (false: quản trị đã khóa).</summary>
    public bool IsActive { get; set; }

    /// <summary>Đang bị khóa tạm thời do nhập sai mật khẩu nhiều lần.</summary>
    public bool IsLockedOut { get; set; }

    /// <summary>Thời điểm hết khóa tạm thời (UTC).</summary>
    public DateTime? LockoutEnd { get; set; }

    /// <summary>Số lần nhập sai mật khẩu liên tiếp.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Đang dùng mật khẩu tạm, phải đổi ở lần đăng nhập kế tiếp.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Lần đăng nhập thành công gần nhất (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Thời điểm tạo tài khoản (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>Một bản ghi nhật ký đăng nhập (<c>GET /api/audit/logins</c>).</summary>
public class LoginEventDto
{
    /// <summary>Id bản ghi.</summary>
    public Guid Id { get; set; }

    /// <summary>Id tài khoản (null nếu tên đăng nhập không tồn tại).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Tên đăng nhập đã nhập.</summary>
    public string UsernameAttempted { get; set; } = string.Empty;

    /// <summary>Kết quả: <c>Success</c>, <c>InvalidPassword</c>, <c>UnknownUser</c>, <c>LockedOut</c>, <c>Disabled</c>.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Địa chỉ IP.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Thời điểm (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
