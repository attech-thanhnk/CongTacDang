using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Dịch vụ xử lý nghiệp vụ xác thực đăng nhập, xoay vòng Refresh Token và quản lý phiên làm việc.
/// Mỗi lần đăng nhập (thành công hoặc thất bại) được ghi vào nhật ký đăng nhập.
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>Số lần nhập sai liên tiếp trước khi khóa tạm thời.</summary>
    public const int MaxFailedLogins = 5;

    /// <summary>Thời gian khóa tạm thời.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private const string DummyPasswordHash = "$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy";
    private const string InvalidCredentialsMessage =
        "Tên đăng nhập hoặc mật khẩu không đúng. Hãy kiểm tra lại (tên đăng nhập không phân biệt chữ hoa/thường).";
    private static readonly TimeSpan RefreshGracePeriod = TimeSpan.FromSeconds(30);

    private readonly IUserRepository _userRepo;
    private readonly IRefreshTokenRepository _refreshTokenRepo;
    private readonly IJwtService _jwtService;
    private readonly IPermissionResolver _permissionResolver;
    private readonly IUserAccountRepository? _accounts;
    private readonly ILoginEventRepository? _loginEvents;
    private readonly IAccessCacheInvalidator? _accessCache;
    private readonly IAccountStateProvider? _accountState;
    private readonly PasswordPolicy _passwordPolicy;

    /// <summary>
    /// Khởi tạo dịch vụ. Các phụ thuộc tùy chọn (nhật ký đăng nhập, cache, chính sách mật khẩu) luôn được DI cung cấp;
    /// để trống chỉ dùng trong test đơn vị.
    /// </summary>
    public AuthService(
        IUserRepository userRepo,
        IRefreshTokenRepository refreshTokenRepo,
        IJwtService jwtService,
        IPermissionResolver permissionResolver,
        IUserAccountRepository? accounts = null,
        ILoginEventRepository? loginEvents = null,
        IAccessCacheInvalidator? accessCache = null,
        IAccountStateProvider? accountState = null,
        PasswordPolicy? passwordPolicy = null)
    {
        _userRepo = userRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _jwtService = jwtService;
        _permissionResolver = permissionResolver;
        _accounts = accounts;
        _loginEvents = loginEvents;
        _accessCache = accessCache;
        _accountState = accountState;
        _passwordPolicy = passwordPolicy ?? new PasswordPolicy();
    }

    /// <summary>Xác thực đăng nhập người dùng bằng BCrypt hash, ghi nhật ký và cấp cặp Access/Refresh Token</summary>
    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request, string? ipAddress = null, string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("Tên đăng nhập và mật khẩu không được để trống. Hãy nhập đủ cả hai.");

        var username = AccountRules.NormalizeUsername(request.Username);
        var member = _accounts != null
            ? await _accounts.FindByUsernameAsync(username)
            : await _userRepo.GetByUsernameAsync(username);

        // Luôn chạy BCrypt kể cả khi username không tồn tại để tránh lộ thời gian phản hồi.
        var passwordHash = member?.PasswordHash;
        var passwordValid = BCrypt.Net.BCrypt.Verify(
            request.Password,
            string.IsNullOrWhiteSpace(passwordHash) ? DummyPasswordHash : passwordHash);

        if (member == null)
        {
            await RecordAsync(null, username, LoginResult.UnknownUser, ipAddress, userAgent);
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        var now = DateTime.UtcNow;

        // Đang khóa tạm thời: từ chối trước khi xét mật khẩu để không để lộ mật khẩu đúng/sai trong thời gian khóa.
        if (member.LockoutEnd.HasValue && member.LockoutEnd.Value > now)
        {
            await RecordAsync(member.Id, username, LoginResult.LockedOut, ipAddress, userAgent);
            var minutes = Math.Max(1, (int)Math.Ceiling((member.LockoutEnd.Value - now).TotalMinutes));
            throw new UnauthorizedAccessException(
                $"Tài khoản đang bị khóa tạm thời do nhập sai mật khẩu nhiều lần. Hãy thử lại sau khoảng {minutes} phút "
                + "hoặc liên hệ quản trị hệ thống để được mở khóa.");
        }

        if (!passwordValid)
        {
            if (member.LockoutEnd.HasValue)
            {
                // Hết thời gian khóa: bắt đầu đếm lại.
                member.FailedLoginCount = 0;
                member.LockoutEnd = null;
            }

            member.FailedLoginCount++;
            if (member.FailedLoginCount >= MaxFailedLogins)
                member.LockoutEnd = now.Add(LockoutDuration);

            await _userRepo.UpdateAsync(member);
            await RecordAsync(member.Id, username, LoginResult.InvalidPassword, ipAddress, userAgent);
            throw new UnauthorizedAccessException(InvalidCredentialsMessage);
        }

        if (!member.IsActive)
        {
            await RecordAsync(member.Id, username, LoginResult.Disabled, ipAddress, userAgent);
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa bởi quản trị hệ thống. Hãy liên hệ quản trị hệ thống để được mở lại.");
        }

        member.FailedLoginCount = 0;
        member.LockoutEnd = null;
        member.LastLoginAt = now;
        await _userRepo.UpdateAsync(member);
        await RecordAsync(member.Id, username, LoginResult.Success, ipAddress, userAgent);

        var refreshToken = _jwtService.GenerateRefreshToken(member.Id, ipAddress);
        await _refreshTokenRepo.AddAsync(refreshToken);

        return await IssueAsync(member, refreshToken.Token, refreshToken.ExpiresAt);
    }

    /// <summary>Làm mới phiên làm việc qua Refresh Token (áp dụng kỹ thuật xoay vòng Token Rotation và phát hiện gian lận)</summary>
    public async Task<AuthResultDto> RefreshTokenAsync(string refreshTokenValue, string? ipAddress = null)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
            throw new UnauthorizedAccessException("Không tìm thấy Refresh Token hợp lệ.");

        var tokenRecord = await _refreshTokenRepo.GetByTokenWithUserAsync(refreshTokenValue);

        if (tokenRecord == null)
            throw new UnauthorizedAccessException("Phiên đăng nhập không còn hiệu lực. Vui lòng đăng nhập lại.");

        // Kiểm tra phát hiện tái sử dụng token (Token Reuse Detection)
        if (tokenRecord.IsRevoked)
        {
            if (!string.IsNullOrEmpty(tokenRecord.ReplacedByTokenHash) &&
                tokenRecord.RevokedAt.HasValue &&
                DateTime.UtcNow - tokenRecord.RevokedAt.Value <= RefreshGracePeriod)
                throw new RefreshTokenGracePeriodException("Refresh Token đã được thay thế do một yêu cầu khác đang làm mới phiên.");

            if (!string.IsNullOrEmpty(tokenRecord.ReplacedByTokenHash))
                await _refreshTokenRepo.RevokeAllUserTokensAsync(tokenRecord.UserId);

            throw new UnauthorizedAccessException("Phiên đăng nhập đã bị thu hồi. Vui lòng đăng nhập lại.");
        }

        if (tokenRecord.IsExpired)
            throw new UnauthorizedAccessException("Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.");

        var user = tokenRecord.User;
        if (user == null || user.IsDeleted || !user.IsActive)
        {
            await _refreshTokenRepo.RevokeAllUserTokensAsync(tokenRecord.UserId);
            throw new UnauthorizedAccessException("Tài khoản đã bị khóa hoặc không còn tồn tại. Hãy liên hệ quản trị hệ thống.");
        }

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

            throw new UnauthorizedAccessException("Phiên đăng nhập đã bị thu hồi. Vui lòng đăng nhập lại.");
        }

        return await IssueAsync(user, newRefreshToken.Token, newRefreshToken.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<AuthResultDto> ChangePasswordAsync(Guid userId, ChangePasswordRequestDto request, string? currentRefreshToken, string? ipAddress = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var member = _accounts != null
            ? await _accounts.FindByIdAsync(userId)
            : await _userRepo.GetByIdAsync(userId);
        if (member == null || !member.IsActive)
            throw new UnauthorizedAccessException("Tài khoản không còn hiệu lực. Vui lòng đăng nhập lại.");

        // Sai mật khẩu hiện tại → 400 (không dùng 401 để giao diện không hiểu nhầm là hết phiên).
        if (string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrWhiteSpace(member.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, member.PasswordHash))
            throw new ValidationException("Mật khẩu hiện tại không đúng. Hãy nhập lại mật khẩu đang dùng (mật khẩu tạm nếu vừa được cấp).");

        _passwordPolicy.Validate(request.NewPassword);
        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, member.PasswordHash))
            throw new ValidationException("Mật khẩu mới phải khác mật khẩu hiện tại. Hãy chọn mật khẩu khác.");

        member.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        member.MustChangePassword = false;
        member.FailedLoginCount = 0;
        member.LockoutEnd = null;
        member.SecurityStamp = UserAccountService.NewSecurityStamp();
        await _userRepo.UpdateAsync(member);
        InvalidateCaches(member.Id);

        // Giữ refresh token của phiên hiện tại (nếu còn hợp lệ), thu hồi mọi phiên khác.
        var current = string.IsNullOrWhiteSpace(currentRefreshToken)
            ? null
            : await _refreshTokenRepo.GetByTokenWithUserAsync(currentRefreshToken);
        if (current != null && current.UserId == member.Id && current.IsActive)
        {
            await _refreshTokenRepo.RevokeOtherUserTokensAsync(member.Id, currentRefreshToken);
            return await IssueAsync(member, string.Empty, current.ExpiresAt);
        }

        await _refreshTokenRepo.RevokeAllUserTokensAsync(member.Id);
        var replacement = _jwtService.GenerateRefreshToken(member.Id, ipAddress);
        await _refreshTokenRepo.AddAsync(replacement);
        return await IssueAsync(member, replacement.Token, replacement.ExpiresAt);
    }

    /// <summary>Đăng xuất phiên làm việc và thu hồi Refresh Token</summary>
    public async Task LogoutAsync(string? refreshTokenValue)
    {
        if (!string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            var tokenRecord = await _refreshTokenRepo.GetByTokenWithUserAsync(refreshTokenValue);
            if (tokenRecord != null && !tokenRecord.IsRevoked)
            {
                tokenRecord.IsRevoked = true;
                tokenRecord.RevokedAt = DateTime.UtcNow;
                await _refreshTokenRepo.UpdateAsync(tokenRecord);
            }
        }
    }

    private async Task<AuthResultDto> IssueAsync(PartyMemberProfile member, string refreshToken, DateTime refreshExpiresAt)
    {
        // Quyền lấy từ nguồn quyền duy nhất (IPermissionResolver); JWT chỉ chứa danh tính.
        var effective = await _permissionResolver.GetAsync(member.Id);
        var (accessToken, accessExpiresAt) = _jwtService.GenerateToken(member);

        return new AuthResultDto
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessExpiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshExpiresAt,
            UserResponse = new LoginResponseDto
            {
                Id = member.Id,
                FullName = member.FullName,
                UserName = member.Username,
                Roles = effective.LegacyRoleCodes.ToArray(),
                Permissions = effective.Codes.ToArray(),
                MustChangePassword = member.MustChangePassword,
                ExpiresAt = accessExpiresAt
            }
        };
    }

    private async Task RecordAsync(Guid? userId, string username, LoginResult result, string? ipAddress, string? userAgent)
    {
        if (_loginEvents == null)
            return;

        await _loginEvents.AddAsync(new LoginEvent
        {
            UserId = userId,
            UsernameAttempted = Truncate(username, LoginEvent.MaxUsernameLength) ?? string.Empty,
            Result = result,
            IpAddress = Truncate(ipAddress, 100),
            UserAgent = Truncate(userAgent, LoginEvent.MaxUserAgentLength),
            CreatedAt = DateTime.UtcNow
        });
    }

    private void InvalidateCaches(Guid userId)
    {
        _accessCache?.InvalidateUser(userId);
        _accountState?.Invalidate(userId);
    }

    private static string? Truncate(string? value, int max) =>
        value == null || value.Length <= max ? value : value[..max];
}
