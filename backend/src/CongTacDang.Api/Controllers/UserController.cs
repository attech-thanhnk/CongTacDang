using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/users")]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lay ho so 2 vai va quyen han cua can bo dang dang nhap
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] string? username = null)
    {
        try
        {
            var profile = await _userService.GetProfileAsync(username);
            return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Lấy thông tin hồ sơ cán bộ thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Danh sach toan bo can bo quan ly ATTECH
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetUserList()
    {
        var cadres = await _userService.GetCadresAsync();
        return Ok(ApiResponse<List<CadreDto>>.Ok(cadres, "Lấy danh sách cán bộ thành công."));
    }

    /// <summary>
    /// Tao moi ho so can bo lanh dao / quan ly
    /// </summary>
    [HttpPost("create")]
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

    /// <summary>
    /// Chi tiet ho so can bo theo ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var cadre = await _userService.GetUserByIdAsync(id);
        if (cadre == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy cán bộ."));

        return Ok(ApiResponse<CadreDto>.Ok(cadre, "Lấy thông tin cán bộ thành công."));
    }

    /// <summary>
    /// Cap nhat ho so can bo lanh dao / quan ly
    /// </summary>
    [HttpPut("{id}")]
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

    /// <summary>
    /// Xoa ho so can bo khoi he thong
    /// </summary>
    [HttpDelete("{id}")]
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

    /// <summary>
    /// Danh muc 5 Role he thong theo quy trinh 03-HD/TVDU
    /// </summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetSystemRoles()
    {
        var roles = await _userService.GetRolesAsync();
        return Ok(ApiResponse<List<RoleDto>>.Ok(roles, "Lấy danh mục vai trò thành công."));
    }
}
