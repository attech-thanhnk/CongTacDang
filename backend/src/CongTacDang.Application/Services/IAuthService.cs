using System;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null);

    /// <summary>Làm mới phiên với xoay vòng refresh token.</summary>
    Task<AuthResultDto> RefreshTokenAsync(string refreshTokenValue, string? ipAddress = null);

    /// <summary>
    /// Đổi dấu bảo mật, thu hồi các phiên khác, cấp lại access token cho phiên hiện tại;
    /// <see cref="AuthResultDto.RefreshToken"/> rỗng = giữ refresh token hiện tại.
    /// </summary>
    Task<AuthResultDto> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, string? currentRefreshToken, string? ipAddress = null);

    Task LogoutAsync(string? refreshTokenValue);
}
