using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện dịch vụ xác thực và quản lý phiên làm việc của hệ thống
/// </summary>
public interface IAuthService
{
    /// <summary>Xác thực đăng nhập người dùng và cấp cặp token</summary>
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null);

    /// <summary>Làm mới phiên làm việc qua Refresh Token (Token Rotation)</summary>
    Task<AuthResultDto> RefreshTokenAsync(string refreshTokenValue, string? ipAddress = null);

    /// <summary>Đăng xuất và thu hồi Refresh Token</summary>
    Task LogoutAsync(string? refreshTokenValue);
}
