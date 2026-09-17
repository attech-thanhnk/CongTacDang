using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Đăng nhập bằng username/password — cấp Access Token (15 phút) và Refresh Token (7 ngày)</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _authService.LoginAsync(request, ipAddress);
            var isSecure = Request.IsHttps || string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

            // Ghi Access Token vào HttpOnly Cookie "auth_token"
            Response.Cookies.Append("auth_token", result.AccessToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Expires = result.AccessTokenExpiresAt
            });

            // Ghi Refresh Token vào HttpOnly Cookie "refresh_token"
            Response.Cookies.Append("refresh_token", result.RefreshToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Expires = result.RefreshTokenExpiresAt
            });

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
            var isSecure = Request.IsHttps || string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

            Response.Cookies.Append("auth_token", result.AccessToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Expires = result.AccessTokenExpiresAt
            });

            Response.Cookies.Append("refresh_token", result.RefreshToken, new Microsoft.AspNetCore.Http.CookieOptions
            {
                HttpOnly = true,
                Secure = isSecure,
                SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
                Expires = result.RefreshTokenExpiresAt
            });

            return Ok(ApiResponse<LoginResponseDto>.Ok(result.UserResponse, "Làm mới phiên làm việc thành công."));
        }
        catch (UnauthorizedAccessException ex)
        {
            Response.Cookies.Delete("auth_token");
            Response.Cookies.Delete("refresh_token");
            return Unauthorized(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Đăng xuất — thu hồi Refresh Token trong DB và xóa toàn bộ Cookie</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        Request.Cookies.TryGetValue("refresh_token", out var tokenValue);
        await _authService.LogoutAsync(tokenValue);

        Response.Cookies.Delete("auth_token");
        Response.Cookies.Delete("refresh_token");
        return Ok(ApiResponse.Ok("Đã đăng xuất thành công."));
    }

    /// <summary>Lấy thông tin user đang đăng nhập từ JWT Claims</summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue(ClaimTypes.Name);
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();
        var permissions = User.FindAll("perm").Select(c => c.Value).ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            Id = sub,
            FullName = name,
            Roles = roles,
            Permissions = permissions
        }, "Lấy thông tin phiên đăng nhập thành công."));
    }
}
