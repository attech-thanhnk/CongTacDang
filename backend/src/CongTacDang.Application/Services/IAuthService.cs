using System;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện dịch vụ xác thực và quản lý phiên làm việc của hệ thống
/// </summary>
public interface IAuthService
{
    /// <summary>Xác thực đăng nhập người dùng, ghi nhật ký đăng nhập và cấp cặp token</summary>
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null);

    /// <summary>Làm mới phiên làm việc qua Refresh Token (Token Rotation)</summary>
    Task<AuthResultDto> RefreshTokenAsync(string refreshTokenValue, string? ipAddress = null);

    /// <summary>
    /// Đổi mật khẩu của người dùng đang đăng nhập: đổi dấu bảo mật, thu hồi các phiên khác,
    /// cấp lại access token cho phiên hiện tại. <see cref="AuthResultDto.RefreshToken"/> rỗng nghĩa là
    /// giữ nguyên refresh token hiện tại.
    /// </summary>
    Task<AuthResultDto> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, string? currentRefreshToken, string? ipAddress = null);

    /// <summary>Đăng xuất và thu hồi Refresh Token</summary>
    Task LogoutAsync(string? refreshTokenValue);
}
