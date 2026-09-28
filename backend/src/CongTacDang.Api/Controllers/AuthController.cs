using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    public AuthController(IAuthService authService, IUserService userService, IPermissionResolver permissionResolver)
    {
        _authService = authService;
        _userService = userService;
        _permissionResolver = permissionResolver;
    }

    /// <summary>Đăng nhập bằng username/password — cấp Access Token (15 phút) và Refresh Token (7 ngày)</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _authService.LoginAsync(request, ipAddress);
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
            return Unauthorized(ApiResponse.Fail("Không tìm thấy Refresh Token hợp lệ."));
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

    /// <summary>Đổi mật khẩu hiện tại và giữ lại refresh token của tab đang dùng.</summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var username = User.FindFirst("username")?.Value ?? User.FindFirst("unique_name")?.Value;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(ApiResponse.Fail("Không xác định được tài khoản hiện tại."));

        Request.Cookies.TryGetValue("refresh_token", out var refreshToken);
        await _authService.ChangePasswordAsync(username, request, refreshToken);
        return Ok(ApiResponse.Ok("Đổi mật khẩu thành công."));
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

    /// <summary>Lấy thông tin user đang đăng nhập từ JWT Claims</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var username = User.FindFirstValue("username") ?? User.FindFirstValue("unique_name");
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized(ApiResponse.Fail("Không xác định được tài khoản hiện tại."));

        var profile = await _userService.GetProfileAsync(username);
        // Danh sách quyền lấy từ nguồn quyền duy nhất (IPermissionResolver), cùng nguồn với kiểm tra policy.
        var effective = await _permissionResolver.GetAsync(profile.Id, HttpContext.RequestAborted);

        return Ok(ApiResponse<object>.Ok(new
        {
            profile.Id,
            profile.FullName,
            profile.UserName,
            profile.Roles,
            Permissions = effective.Codes.ToArray(),
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
