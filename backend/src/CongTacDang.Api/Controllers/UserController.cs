using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Quản lý hồ sơ cán bộ lãnh đạo, quản lý</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lấy hồ sơ và vai trò của cán bộ đang đăng nhập. Tham số <paramref name="username"/> chỉ dùng được
    /// cho hồ sơ của chính mình hoặc khi người yêu cầu có quyền xem hồ sơ người khác ở phạm vi quản trị.
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] string? username = null)
    {
        try
        {
            var subject = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(subject, out var requesterId))
                throw new UnauthorizedAccessException("Không xác thực được danh tính người dùng hiện tại.");

            var profile = await _userService.GetProfileForRequesterAsync(requesterId, username);
            return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Lấy thông tin hồ sơ cán bộ thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Danh sách toàn bộ cán bộ quản lý</summary>
    [HttpGet("list")]
    [Authorize(Policy = AppPermissions.UsersRead)]
    public async Task<IActionResult> GetUserList()
    {
        var cadres = await _userService.GetCadresAsync();
        return Ok(ApiResponse<List<CadreDto>>.Ok(cadres, "Lấy danh sách cán bộ thành công."));
    }

    /// <summary>Chi tiết hồ sơ cán bộ theo ID</summary>
    [HttpGet("{id}")]
    [Authorize(Policy = AppPermissions.UsersRead)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var cadre = await _userService.GetUserByIdAsync(id);
        if (cadre == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy cán bộ."));

        return Ok(ApiResponse<CadreDto>.Ok(cadre, "Lấy thông tin cán bộ thành công."));
    }

    /// <summary>Danh mục vai trò hệ thống</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetSystemRoles()
    {
        var roles = await _userService.GetRolesAsync();
        return Ok(ApiResponse<List<RoleDto>>.Ok(roles, "Lấy danh mục vai trò thành công."));
    }

    /// <summary>Tạo mới hồ sơ cán bộ</summary>
    [HttpPost("create")]
    [Authorize(Policy = AppPermissions.UsersCreate)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto request)
    {
        try
        {
            var result = await _userService.CreateUserAsync(request);
            return Ok(ApiResponse<CadreDto>.Ok(result, "Thêm mới hồ sơ cán bộ thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Cập nhật hồ sơ cán bộ</summary>
    [HttpPut("{id}")]
    [Authorize(Policy = AppPermissions.UsersUpdate)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserDto request)
    {
        try
        {
            var result = await _userService.UpdateUserAsync(id, request);
            return Ok(ApiResponse<CadreDto>.Ok(result, "Cập nhật hồ sơ cán bộ thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Xóa hồ sơ cán bộ</summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = AppPermissions.UsersDelete)]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        try
        {
            await _userService.DeleteUserAsync(id);
            return Ok(ApiResponse.Ok("Đã xóa hồ sơ cán bộ thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Đặt lại mật khẩu tạm cho cán bộ; mật khẩu chỉ hiển thị một lần.</summary>
    [HttpPost("{id}/reset-password")]
    [Authorize(Policy = AppPermissions.UsersUpdate)]
    public async Task<IActionResult> ResetPassword(Guid id)
    {
        var result = await _userService.ResetPasswordAsync(id);
        return Ok(ApiResponse<ResetPasswordResponseDto>.Ok(result, "Đã đặt lại mật khẩu tạm. Hãy cung cấp mật khẩu này cho cán bộ qua kênh an toàn."));
    }
}
