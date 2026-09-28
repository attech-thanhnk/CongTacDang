using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ xác thực đăng nhập, xoay vòng Refresh Token và quản lý phiên làm việc
/// </summary>
public class AuthService : IAuthService
{
    private const string DummyPasswordHash = "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshGracePeriod = TimeSpan.FromSeconds(30);
    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IJwtService _jwtService;
    private readonly IPermissionResolver _permissionResolver;

    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IJwtService jwtService,
        IPermissionResolver permissionResolver)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _jwtService = jwtService;
        _permissionResolver = permissionResolver;
    }

    /// <summary>Xác thực đăng nhập người dùng bằng BCrypt hash và cấp cặp Access/Refresh Token</summary>
    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Tên đăng nhập và mật khẩu không được để trống.");

        var member = await _userRepo.GetWithRolesAndPermissionsAsync(request.Username.Trim());

        // Luôn chạy BCrypt kể cả khi username không tồn tại để tránh lộ thời gian phản hồi.
        var passwordHash = member?.PasswordHash;
        var passwordValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            string.IsNullOrWhiteSpace(passwordHash) ? DummyPasswordHash : passwordHash);

        if (member == null || !passwordValid)
        {
            if (member != null)
            {
                if (member.LockoutEnd <= DateTime.UtcNow)
                {
                    member.FailedLoginCount = 0;
                    member.LockoutEnd = null;
                }

                member.FailedLoginCount++;
                if (member.FailedLoginCount >= MaxFailedLogins)
                    member.LockoutEnd = DateTime.UtcNow.Add(LockoutDuration);

                await _userRepo.UpdateAsync(member);
            }

            throw new UnauthorizedAccessException("Tên đăng nhập hoặc mật khẩu không đúng.");
        }

        if (member.LockoutEnd.HasValue && member.LockoutEnd.Value > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Tài khoản đang bị khóa tạm thời. Vui lòng thử lại sau.");

        if (!member.IsActive)
            throw new UnauthorizedAccessException("Tài khoản đã bị vô hiệu hóa.");

        if (member.FailedLoginCount != 0 || member.LockoutEnd.HasValue)
        {
            member.FailedLoginCount = 0;
            member.LockoutEnd = null;
            await _userRepo.UpdateAsync(member);
        }

        // Quyền lấy từ nguồn quyền duy nhất (IPermissionResolver), không tự tính từ member.Roles.
        var effective = await _permissionResolver.GetAsync(member.Id);
        var roles = effective.LegacyRoleCodes.ToList();
        var permissions = effective.Codes.ToList();

        // 1. Sinh Access Token ngắn hạn (15 phút)
        var (accessToken, accessExpiresAt) = _jwtService.GenerateToken(member, roles, permissions);

        // 2. Sinh Refresh Token dài hạn (7 ngày) và lưu trữ qua Repository
        var refreshToken = _jwtService.GenerateRefreshToken(member.Id, ipAddress);
        await _refreshTokenRepo.AddAsync(refreshToken);

        return BuildAuthResult(member, accessToken, accessExpiresAt, refreshToken, roles, permissions);
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
            if (!string.IsNullOrEmpty(tokenRecord.ReplacedByTokenHash) &&
                tokenRecord.RevokedAt.HasValue &&
                DateTime.UtcNow - tokenRecord.RevokedAt.Value <= RefreshGracePeriod)
                throw new RefreshTokenGracePeriodException("Refresh Token đã được thay thế do một yêu cầu khác đang làm mới phiên.");

            if (!string.IsNullOrEmpty(tokenRecord.ReplacedByTokenHash))
                await _refreshTokenRepo.RevokeAllUserTokensAsync(tokenRecord.UserId);

            throw new UnauthorizedAccessException("Refresh Token đã bị thu hồi. Vui lòng đăng nhập lại.");
        }

        if (tokenRecord.IsExpired)
            throw new UnauthorizedAccessException("Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.");

        var user = tokenRecord.User;
        if (!user.IsActive)
            throw new UnauthorizedAccessException("Tài khoản cán bộ đã bị vô hiệu hóa.");

        // 1. Sinh Refresh Token mới (Token Rotation)
        var newRefreshToken = _jwtService.GenerateRefreshToken(user.Id, ipAddress);

        // 2. Thu hồi token cũ và thêm token mới trong một lần SaveChanges.
        if (!await _refreshTokenRepo.RotateAsync(refreshTokenValue, newRefreshToken))
        {
            var latestRecord = await _refreshTokenRepo.GetByTokenWithUserAsync(refreshTokenValue);
            if (latestRecord?.RevokedAt.HasValue == true &&
                DateTime.UtcNow - latestRecord.RevokedAt.Value <= RefreshGracePeriod)
                throw new RefreshTokenGracePeriodException("Refresh Token đã được thay thế do một yêu cầu khác đang làm mới phiên.");

            if (latestRecord?.ReplacedByTokenHash != null)
                await _refreshTokenRepo.RevokeAllUserTokensAsync(tokenRecord.UserId);

            throw new UnauthorizedAccessException("Refresh Token đã bị thu hồi. Vui lòng đăng nhập lại.");
        }

        // 3. Vai trò & quyền mới nhất từ nguồn quyền duy nhất (IPermissionResolver)
        var effective = await _permissionResolver.GetAsync(user.Id);
        var roles = effective.LegacyRoleCodes.ToList();
        var permissions = effective.Codes.ToList();

        var (newAccessToken, accessExpiresAt) = _jwtService.GenerateToken(user, roles, permissions);

        return BuildAuthResult(user, newAccessToken, accessExpiresAt, newRefreshToken, roles, permissions);
    }

    public async Task ChangePasswordAsync(string username, ChangePasswordRequestDto request, string? currentRefreshToken)
    {
        ValidatePassword(request.NewPassword);

        var member = await _userRepo.GetByUsernameAsync(username);
        if (member == null || string.IsNullOrWhiteSpace(member.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, member.PasswordHash))
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, member.PasswordHash))
            throw new ValidationException("Mật khẩu mới phải khác mật khẩu hiện tại.");

        member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        member.MustChangePassword = false;
        member.FailedLoginCount = 0;
        member.LockoutEnd = null;
        await _userRepo.UpdateAsync(member);
        await _refreshTokenRepo.RevokeOtherUserTokensAsync(member.Id, currentRefreshToken);
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
                tokenRecord.RevokedAt = DateTime.UtcNow;
                await _refreshTokenRepo.UpdateAsync(tokenRecord);
            }
        }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 ||
            !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException("Mật khẩu mới phải có tối thiểu 8 ký tự, bao gồm cả chữ và số.");
    }

    private static AuthResultDto BuildAuthResult(
        Domain.Entities.PartyMemberProfile member,
        string accessToken,
        DateTime accessExpiresAt,
        Domain.Entities.RefreshToken refreshToken,
        IEnumerable<string> roles,
        IEnumerable<string> permissions)
    {
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
                MustChangePassword = member.MustChangePassword,
                ExpiresAt = accessExpiresAt
            }
        };
    }
}
