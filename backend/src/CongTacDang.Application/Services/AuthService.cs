using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ xác thực đăng nhập, xoay vòng Refresh Token và quản lý phiên làm việc
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IJwtService _jwtService;

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IJwtService jwtService)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _jwtService = jwtService;
    }

    /// <summary>Xác thực đăng nhập người dùng bằng BCrypt hash và cấp cặp Access/Refresh Token</summary>
    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Tên đăng nhập và mật khẩu không được để trống.");

        var member = await _userRepo.GetWithRolesAndPermissionsAsync(request.Username.Trim());

        if (member == null)
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");

        // Xác thực mật khẩu theo chuẩn mã hóa an toàn BCrypt
        if (string.IsNullOrWhiteSpace(member.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, member.PasswordHash))
        {
            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");
        }

        if (!member.IsActive)
            throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

        var roles = member.Roles.Select(r => r.Code).Distinct().ToList();
        var permissions = member.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().ToList();

        // 1. Sinh Access Token ngắn hạn (15 phút)
        var (accessToken, accessExpiresAt) = _jwtService.GenerateToken(member, roles, permissions);

        // 2. Sinh Refresh Token dài hạn (7 ngày) và lưu trữ qua Repository
        var refreshToken = _jwtService.GenerateRefreshToken(member.Id, ipAddress);
        await _refreshTokenRepo.AddAsync(refreshToken);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            UserResponse = new LoginResponseDto
            {
                Id = member.Id,
                FullName = member.FullName,
                UserName = member.Username,
                Roles = roles.ToArray(),
                Permissions = permissions.ToArray(),
                ExpiresAt = accessExpiresAt
            }
        };
    }

    /// <summary>Làm mới phiên làm việc qua Refresh Token (áp dụng kỹ thuật xoay vòng Token Rotation và phát hiện gian lận)</summary>
    public async Task<AuthResultDto> RefreshTokenAsync(string refreshTokenValue, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
            throw new UnauthorizedAccessException("Không tìm thấy Refresh Token hợp lệ.");

        var tokenRecord = await _refreshTokenRepo.GetByTokenWithUserAsync(refreshTokenValue);

        if (tokenRecord == null)
            throw new UnauthorizedAccessException("Refresh Token không tồn tại hoặc đã bị hủy.");

        // Kiểm tra phát hiện tái sử dụng token (Token Reuse Detection)
        if (tokenRecord.IsRevoked)
        {
            if (!string.IsNullOrEmpty(tokenRecord.ReplacedByToken))
            {
                // Token đã bị thu hồi trước đó nhưng bị gửi lại -> Nguy cơ bị lộ -> Thu hồi toàn bộ token của user
                await _refreshTokenRepo.RevokeAllUserTokensAsync(tokenRecord.UserId);
            }

            throw new UnauthorizedAccessException("Refresh Token đã bị thu hồi. Vui lòng đăng nhập lại.");
        }

        if (tokenRecord.IsExpired)
            throw new UnauthorizedAccessException("Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.");

        var user = tokenRecord.User;
        if (!user.IsActive)
            throw new UnauthorizedAccessException("Tài khoản cán bộ đã bị vô hiệu hóa.");

        // 1. Sinh Refresh Token mới (Token Rotation)
        var newRefreshToken = _jwtService.GenerateRefreshToken(user.Id, ipAddress);

        // 2. Thu hồi token cũ và liên kết tới token mới
        tokenRecord.IsRevoked = true;
        tokenRecord.ReplacedByToken = newRefreshToken.Token;

        await _refreshTokenRepo.UpdateAsync(tokenRecord);
        await _refreshTokenRepo.AddAsync(newRefreshToken);

        // 3. Trích xuất vai trò & quyền mới nhất từ DB
        var roles = user.Roles.Select(r => r.Code).Distinct().ToList();
        var permissions = user.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().ToList();

        var (newAccessToken, accessExpiresAt) = _jwtService.GenerateToken(user, roles, permissions);

        return new AuthResultDto
        {
            AccessToken = newAccessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = newRefreshToken.Token,
            RefreshTokenExpiresAt = newRefreshToken.ExpiresAt,
            UserResponse = new LoginResponseDto
            {
                Id = user.Id,
                FullName = user.FullName,
                UserName = user.Username,
                Roles = roles.ToArray(),
                Permissions = permissions.ToArray(),
                ExpiresAt = accessExpiresAt
            }
        };
    }

    /// <summary>Đăng xuất phiên làm việc và thu hồi Refresh Token</summary>
    public async Task LogoutAsync(string? refreshTokenValue)
    {
        if (!string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            var tokenRecord = await _refreshTokenRepo.GetByTokenWithUserAsync(refreshTokenValue);
            if (tokenRecord != null)
            {
                tokenRecord.IsRevoked = true;
                await _refreshTokenRepo.UpdateAsync(tokenRecord);
            }
        }
    }
}
