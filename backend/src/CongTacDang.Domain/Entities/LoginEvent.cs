using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Nhật ký đăng nhập; chỉ thêm mới, không sửa/xóa.</summary>
public class LoginEvent
{
    public const int MaxUsernameLength = 100;

    public const int MaxUserAgentLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Null khi tên đăng nhập không tồn tại.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Chữ thường, cắt tối đa 100 ký tự.</summary>
    public string UsernameAttempted { get; set; } = string.Empty;

    public LoginResult Result { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
