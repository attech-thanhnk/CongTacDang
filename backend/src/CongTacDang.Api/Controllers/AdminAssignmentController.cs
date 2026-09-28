using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Gán vai trò kèm phạm vi và thời hạn, tra cứu quyền hiệu lực (docs/thiet-ke/phan-quyen.md mục 2, 5).
/// </summary>
[ApiController]
[Route("api/admin")]
public class AdminAssignmentController : ControllerBase
{
    private readonly IRoleAssignmentService _assignments;

    /// <summary>Khởi tạo controller.</summary>
    public AdminAssignmentController(IRoleAssignmentService assignments) => _assignments = assignments;

    /// <summary>Tra cứu bản gán: <c>?userId=&amp;roleId=&amp;scopeType=&amp;scopeId=&amp;activeOn=</c>.</summary>
    [HttpGet("assignments")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Query(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? roleId,
        [FromQuery] string? scopeType,
        [FromQuery] Guid? scopeId,
        [FromQuery] DateTime? activeOn)
    {
        var rows = await _assignments.QueryAsync(
            new RoleAssignmentQuery(userId, roleId, ParseScopeType(scopeType, allowNull: true), scopeId, activeOn),
            HttpContext.RequestAborted);
        return Ok(ApiResponse<List<RoleAssignmentDto>>.Ok(rows, "Lấy danh sách bản gán vai trò thành công."));
    }

    /// <summary>Tạo bản gán.</summary>
    [HttpPost("assignments")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Create([FromBody] CreateRoleAssignmentRequestDto request)
    {
        var row = await _assignments.AssignAsync(
            request.UserId,
            request.RoleId,
            ParseScopeType(request.ScopeType, allowNull: true) ?? ScopeType.Global,
            request.ScopeId,
            request.ValidFrom,
            request.ValidTo,
            request.Note,
            HttpContext.RequestAborted);
        return Ok(ApiResponse<RoleAssignmentDto>.Ok(row, "Gán vai trò thành công."));
    }

    /// <summary>Sửa thời hạn, ghi chú bản gán.</summary>
    [HttpPut("assignments/{id:guid}")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleAssignmentRequestDto request)
    {
        var row = await _assignments.UpdateAsync(id, request.ValidFrom, request.ValidTo, request.Note, HttpContext.RequestAborted);
        return Ok(ApiResponse<RoleAssignmentDto>.Ok(row, "Cập nhật bản gán vai trò thành công."));
    }

    /// <summary>Kết thúc bản gán ngay (<c>ValidTo = now</c>).</summary>
    [HttpPost("assignments/{id:guid}/end")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> End(Guid id)
    {
        var row = await _assignments.EndAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<RoleAssignmentDto>.Ok(row, "Đã kết thúc bản gán vai trò."));
    }

    /// <summary>Xóa mềm bản gán.</summary>
    [HttpDelete("assignments/{id:guid}")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _assignments.DeleteAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse.Ok("Đã xóa bản gán vai trò."));
    }

    /// <summary>"Người này làm được gì": quyền hiệu lực + phạm vi + vai trò/bản gán nguồn.</summary>
    [HttpGet("users/{id:guid}/effective-permissions")]
    [RequireAnyPermission(PermissionCodes.SystemAssignmentsManage, PermissionCodes.SystemUsersRead)]
    public async Task<IActionResult> EffectivePermissions(Guid id)
    {
        var result = await _assignments.GetEffectivePermissionsAsync(id, HttpContext.RequestAborted);
        return Ok(ApiResponse<UserEffectivePermissionsDto>.Ok(result, "Lấy quyền hiệu lực thành công."));
    }

    /// <summary>
    /// Tương thích giao diện cũ (trang <c>/users</c> — task 11 thay): đặt tập vai trò phạm vi Toàn công ty của người dùng
    /// theo <c>roleIds</c>.
    /// </summary>
    [HttpPost("users/{id:guid}/roles")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> SetGlobalRoles(Guid id, [FromBody] SetUserGlobalRolesRequestDto request)
    {
        var rows = await _assignments.SetGlobalRolesAsync(id, request.RoleIds, HttpContext.RequestAborted);
        return Ok(ApiResponse<List<RoleAssignmentDto>>.Ok(rows, "Gán vai trò cho cán bộ thành công."));
    }

    /// <summary>Đọc loại phạm vi: <c>Global</c> | <c>Department</c> | <c>PartyCell</c> (không phân biệt hoa thường) hoặc số 0/1/2.</summary>
    private static ScopeType? ParseScopeType(string? value, bool allowNull)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (allowNull)
                return null;
            throw new ValidationException("Thiếu loại phạm vi.");
        }

        if (Enum.TryParse<ScopeType>(value.Trim(), ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        throw new ValidationException("Loại phạm vi không hợp lệ. Chỉ nhận Global (Toàn công ty), Department (Phòng) hoặc PartyCell (Chi bộ).");
    }
}
