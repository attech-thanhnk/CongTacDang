using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Nhật ký một lần đăng nhập (thành công hoặc thất bại). Chỉ thêm mới, không sửa/xóa.
/// </summary>
public class LoginEvent
{
    /// <summary>Độ dài tối đa lưu cho tên đăng nhập đã nhập.</summary>
    public const int MaxUsernameLength = 100;

    /// <summary>Độ dài tối đa lưu cho User-Agent.</summary>
    public const int MaxUserAgentLength = 500;

    /// <summary>Mã định danh bản ghi.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tài khoản tương ứng (null khi tên đăng nhập không tồn tại).</summary>
    public Guid? UserId { get; set; }

    /// <summary>Tên đăng nhập người dùng đã nhập (chuẩn hóa chữ thường, cắt tối đa 100 ký tự).</summary>
    public string UsernameAttempted { get; set; } = string.Empty;

    /// <summary>Kết quả đăng nhập.</summary>
    public LoginResult Result { get; set; }

    /// <summary>Địa chỉ IP của máy gửi yêu cầu.</summary>
    public string? IpAddress { get; set; }

    /// <summary>User-Agent của trình duyệt.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Thời điểm đăng nhập (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
