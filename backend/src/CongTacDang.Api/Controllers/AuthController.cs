using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Services;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly JwtService _jwtService;

    public AuthController(IUserRepository userRepo, JwtService jwtService)
    {
        _userRepo = userRepo;
        _jwtService = jwtService;
    }

    /// <summary>Đăng nhập bằng username/password — JWT được set qua HttpOnly Cookie</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(ApiResponse.Fail("Tên đăng nhập và mật khẩu không được để trống."));

        var member = await _userRepo.GetByUsernameAsync(request.Username.Trim());

        // MVP: so sánh password plain text — cần hash bcrypt khi production
        if (member == null || member.PasswordHash != request.Password)
            return Unauthorized(ApiResponse.Fail("Tên đăng nhập hoặc mật khẩu không đúng."));

        if (!member.IsActive)
            return Unauthorized(ApiResponse.Fail("Tài khoản đã bị vô hiệu hóa."));

        var (token, expiresAt) = _jwtService.GenerateToken(member);
        var roles = JwtService.BuildRoles(member);

        // Set JWT vào HttpOnly Cookie — không lộ qua JavaScript
        Response.Cookies.Append("auth_token", token, new Microsoft.AspNetCore.Http.CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Đặt true khi deploy HTTPS
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });

        var response = new LoginResponseDto
        {
            Id = member.Id,
            FullName = member.FullName,
            UserName = member.Username,
            Roles = roles,
            ExpiresAt = expiresAt
        };

        return Ok(ApiResponse<LoginResponseDto>.Ok(response, "Đăng nhập thành công."));
    }

    /// <summary>Đăng xuất — xóa HttpOnly Cookie khỏi trình duyệt</summary>
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("auth_token");
        return Ok(ApiResponse.Ok("Đã đăng xuất thành công."));
    }

    /// <summary>Lấy thông tin user đang đăng nhập từ JWT Claims</summary>
    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue(ClaimTypes.Name);
        var username = User.FindFirstValue(ClaimTypes.Name) ?? User.Identity?.Name;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            Id = sub,
            FullName = name,
            Roles = roles
        }, "Lấy thông tin phiên đăng nhập thành công."));
    }
}
