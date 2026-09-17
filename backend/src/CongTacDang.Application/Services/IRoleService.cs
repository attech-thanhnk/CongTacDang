using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện dịch vụ quản trị vai trò và quyền hạn (Dynamic RBAC)
/// </summary>
public interface IRoleService
{
    /// <summary>Lấy danh sách tất cả các vai trò kèm quyền hạn</summary>
    Task<List<RoleDto>> GetRolesAsync();

    /// <summary>Lấy danh sách tất cả các quyền hạn trong hệ thống</summary>
    Task<List<PermissionDto>> GetPermissionsAsync();

    /// <summary>Cập nhật danh sách quyền hạn cho một vai trò</summary>
    Task UpdateRolePermissionsAsync(Guid roleId, IEnumerable<string> permissionCodes);

    /// <summary>Gán danh sách vai trò cho một cán bộ / người dùng</summary>
    Task AssignRolesToUserAsync(Guid userId, IEnumerable<string> roleCodes);
}
