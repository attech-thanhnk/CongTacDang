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

/// <summary>
/// Quản trị động Vai trò (Roles) và Quyền hạn (Permissions) trong hệ thống
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = AppPermissions.RolesManage)]
public class RoleController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RoleController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    /// <summary>Lấy danh sách tất cả các vai trò kèm quyền hạn hiện tại</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleService.GetRolesAsync();
        return Ok(ApiResponse<List<RoleDto>>.Ok(roles, "Lấy danh sách vai trò thành công."));
    }

    /// <summary>Lấy danh sách toàn bộ quyền hạn (Permissions) trong hệ thống</summary>
    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var perms = await _roleService.GetPermissionsAsync();
        return Ok(ApiResponse<List<PermissionDto>>.Ok(perms, "Lấy danh sách quyền hạn thành công."));
    }

    /// <summary>Cập nhật danh sách quyền hạn cho một vai trò</summary>
    [HttpPut("roles/{roleId}/permissions")]
    public async Task<IActionResult> UpdateRolePermissions(Guid roleId, [FromBody] UpdateRolePermissionsDto request)
    {
        try
        {
            await _roleService.UpdateRolePermissionsAsync(roleId, request.PermissionCodes);
            return Ok(ApiResponse.Ok("Cập nhật quyền hạn cho vai trò thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"Không thể cập nhật quyền hạn: {ex.Message}"));
        }
    }

    /// <summary>Gán danh sách vai trò cho một cán bộ / người dùng</summary>
    [HttpPost("users/{userId}/roles")]
    public async Task<IActionResult> AssignUserRoles(Guid userId, [FromBody] AssignUserRolesDto request)
    {
        try
        {
            await _roleService.AssignRolesToUserAsync(userId, request.RoleCodes);
            return Ok(ApiResponse.Ok("Gán vai trò cho cán bộ thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Fail($"Không thể gán vai trò: {ex.Message}"));
        }
    }
}
