using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Quản trị vai trò và danh mục quyền (docs/thiet-ke/phan-quyen.md mục 2, 5). Ghi: <c>system.roles.manage</c>;
/// đọc (danh sách/chi tiết vai trò, danh mục quyền): <c>system.roles.manage</c> hoặc <c>system.assignments.manage</c>
/// (người gán vai trò cần chọn vai trò).
/// Lỗi nghiệp vụ trả qua <c>GlobalExceptionMiddleware</c>: 400 / 403 / 404 / 409 kèm <see cref="ApiResponse"/>.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize]
public class AdminRoleController : ControllerBase
{
    private readonly IRoleService _roles;

    /// <summary>Khởi tạo controller.</summary>
    public AdminRoleController(IRoleService roles) => _roles = roles;

    /// <summary>Danh sách vai trò kèm mã quyền và số bản gán chưa hết hạn.</summary>
    [HttpGet("roles")]
    [RequireAnyPermission(PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roles.GetRolesAsync(HttpContext.RequestAborted);
        return Ok(ApiResponse<List<AdminRoleDto>>.Ok(roles, "Lấy danh sách vai trò thành công."));
    }

    /// <summary>Chi tiết vai trò.</summary>
    [HttpGet("roles/{id:guid}")]
    [RequireAnyPermission(PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> GetRole(Guid id)
    {
        var role = await _roles.GetRoleAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<AdminRoleDto>.Ok(role, "Lấy thông tin vai trò thành công."));
    }

    /// <summary>Tạo vai trò.</summary>
    [HttpPost("roles")]
    [RequirePermission(PermissionCodes.SystemRolesManage)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequestDto request)
    {
        var role = await _roles.CreateRoleAsync(request, HttpContext.RequestAborted);
        return Ok(ApiResponse<AdminRoleDto>.Ok(role, "Tạo vai trò thành công."));
    }

    /// <summary>Đổi tên, mô tả vai trò.</summary>
    [HttpPut("roles/{id:guid}")]
    [RequirePermission(PermissionCodes.SystemRolesManage)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequestDto request)
    {
        var role = await _roles.UpdateRoleAsync(id, request, HttpContext.RequestAborted);
        return Ok(ApiResponse<AdminRoleDto>.Ok(role, "Cập nhật vai trò thành công."));
    }

    /// <summary>Đặt lại danh sách quyền của vai trò.</summary>
    [HttpPut("roles/{id:guid}/permissions")]
    [RequirePermission(PermissionCodes.SystemRolesManage)]
    public async Task<IActionResult> UpdateRolePermissions(Guid id, [FromBody] UpdateRolePermissionsDto request)
    {
        var role = await _roles.UpdateRolePermissionsAsync(id, request.PermissionCodes, HttpContext.RequestAborted);
        return Ok(ApiResponse<AdminRoleDto>.Ok(role, "Cập nhật quyền của vai trò thành công."));
    }

    /// <summary>Xóa mềm vai trò (409 nếu được bảo vệ hoặc đang được gán).</summary>
    [HttpDelete("roles/{id:guid}")]
    [RequirePermission(PermissionCodes.SystemRolesManage)]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        await _roles.DeleteRoleAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Đã xóa vai trò."));
    }

    /// <summary>Danh mục quyền nhóm theo phân hệ (mô tả, <c>appliesScope</c>).</summary>
    [HttpGet("permissions")]
    [RequireAnyPermission(PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> GetPermissions()
    {
        var catalog = await _roles.GetPermissionCatalogAsync(HttpContext.RequestAborted);
        return Ok(ApiResponse<List<PermissionModuleDto>>.Ok(catalog, "Lấy danh mục quyền thành công."));
    }
}
