using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IPermissionResolver _permissionResolver;
    private readonly IRoleAssignmentService _roleAssignments;

    public AuthController(
        IAuthService authService,
        IUserService userService,
        IPermissionResolver permissionResolver,
        IRoleAssignmentService roleAssignments)
    {
        _authService = authService;
        _userService = userService;
        _permissionResolver = permissionResolver;
        _roleAssignments = roleAssignments;
    }

    /// <summary>Đăng nhập bằng username/password — cấp Access Token (15 phút) và Refresh Token (7 ngày)</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();
            var result = await _authService.LoginAsync(request, ipAddress, string.IsNullOrEmpty(userAgent) ? null : userAgent);
            SetAuthCookies(result);

            return Ok(ApiResponse<LoginResponseDto>.Ok(result.UserResponse, "Đăng nhập thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Làm mới phiên làm việc bằng Refresh Token theo chuẩn Token Rotation</summary>
    [HttpPost("refresh-token")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RefreshToken()
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var tokenValue) || string.IsNullOrWhiteSpace(tokenValue))
        {
            return Unauthorized(ApiResponse.Fail("Không tìm thấy phiên đăng nhập. Vui lòng đăng nhập lại."));
        }

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _authService.RefreshTokenAsync(tokenValue, ipAddress);
            SetAuthCookies(result);

            return Ok(ApiResponse<LoginResponseDto>.Ok(result.UserResponse, "Làm mới phiên làm việc thành công."));
        }
        catch (UnauthorizedAccessException ex)
        {
            Response.Cookies.Delete("auth_token");
            if (ex is not CongTacDang.Application.Common.Exceptions.RefreshTokenGracePeriodException)
                DeleteRefreshCookies();
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Đổi mật khẩu hiện tại: đổi dấu bảo mật, thu hồi mọi phiên khác, cấp lại cookie cho phiên đang dùng.
    /// Sai mật khẩu hiện tại / mật khẩu mới không đạt chính sách → 400.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse.Fail("Không xác định được tài khoản hiện tại. Vui lòng đăng nhập lại."));

        Request.Cookies.TryGetValue("refresh_token", out var refreshToken);
        var result = await _authService.ChangePasswordAsync(
            userId.Value, request, refreshToken, HttpContext.Connection.RemoteIpAddress?.ToString());
        SetAuthCookies(result);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result.UserResponse, "Đổi mật khẩu thành công. Các phiên đăng nhập khác đã bị đăng xuất."));
    }

    /// <summary>Đăng xuất — thu hồi Refresh Token trong DB và xóa toàn bộ Cookie</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        Request.Cookies.TryGetValue("refresh_token", out var tokenValue);
        await _authService.LogoutAsync(tokenValue);

        Response.Cookies.Delete("auth_token");
        DeleteRefreshCookies();
        return Ok(ApiResponse.Ok("Đã đăng xuất thành công."));
    }

    /// <summary>Xóa refresh_token hiện hành (Path=/api/auth) và cookie kiểu cũ (Path=/) còn sót ở trình duyệt.</summary>
    private void DeleteRefreshCookies()
    {
        // Thứ tự quan trọng: Delete loại bỏ header Set-Cookie trước đó có chứa "path=<path>",
        // nên xóa Path=/ trước để lệnh xóa Path=/api/auth không bị gộp mất.
        Response.Cookies.Delete("refresh_token");
        Response.Cookies.Delete("refresh_token", new Microsoft.AspNetCore.Http.CookieOptions { Path = "/api/auth" });
    }

    /// <summary>Thông tin phiên hiện tại: hồ sơ, quyền (từ IPermissionResolver), trạng thái bắt buộc đổi mật khẩu.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(ApiResponse.Fail("Không xác định được tài khoản hiện tại. Vui lòng đăng nhập lại."));

        var profile = await _userService.GetProfileByIdAsync(userId.Value);
        // Vai trò/quyền lấy từ nguồn quyền duy nhất (IPermissionResolver), cùng nguồn với kiểm tra policy.
        var effective = await _permissionResolver.GetAsync(profile.Id, HttpContext.RequestAborted);
        var grants = await _roleAssignments.GetGrantsAsync(profile.Id, HttpContext.RequestAborted);

        return Ok(ApiResponse<object>.Ok(new
        {
            profile.Id,
            profile.FullName,
            profile.UserName,
            Roles = effective.RoleNames.ToArray(),
            Permissions = effective.Codes.ToArray(),
            Grants = grants,
            profile.MustChangePassword
        }, "Lấy thông tin phiên đăng nhập thành công."));
    }

    private void SetAuthCookies(AuthResultDto result)
    {
        var isSecure = Request.IsHttps || string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);
        Response.Cookies.Append("auth_token", result.AccessToken, new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            // Cookie sống theo refresh token để middleware Next.js nhận biết phiên; JWT bên trong vẫn hết hạn theo AccessTokenExpiryMinutes.
            Expires = result.RefreshTokenExpiresAt
        });

        // RefreshToken rỗng: giữ nguyên refresh token hiện tại (đổi mật khẩu trên phiên đang dùng).
        if (string.IsNullOrEmpty(result.RefreshToken))
            return;

        Response.Cookies.Append("refresh_token", result.RefreshToken, new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            Secure = isSecure,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = result.RefreshTokenExpiresAt
        });
    }
}
