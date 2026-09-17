using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Dịch vụ quản trị phân quyền và vai trò động
/// </summary>
public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepo;

    public RoleService(IRoleRepository roleRepo)
    {
        _roleRepo = roleRepo;
    }

    /// <summary>Lấy danh sách tất cả các vai trò kèm quyền hạn hiện tại</summary>
    public async Task<List<RoleDto>> GetRolesAsync()
    {
        var roles = await _roleRepo.GetAllRolesWithPermissionsAsync();
        return roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Code = r.Code,
            Name = r.Name,
            Description = r.Description,
            IsSystem = r.IsSystem,
            Permissions = r.Permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Resource = p.Resource,
                Action = p.Action,
                Description = p.Description
            }).ToList()
        }).ToList();
    }

    /// <summary>Lấy danh sách toàn bộ quyền hạn (Permissions) trong hệ thống</summary>
    public async Task<List<PermissionDto>> GetPermissionsAsync()
    {
        var perms = await _roleRepo.GetAllPermissionsAsync();
        return perms.Select(p => new PermissionDto
        {
            Id = p.Id,
            Code = p.Code,
            Name = p.Name,
            Resource = p.Resource,
            Action = p.Action,
            Description = p.Description
        }).ToList();
    }

    /// <summary>Cập nhật danh sách quyền hạn cho một vai trò</summary>
    public async Task UpdateRolePermissionsAsync(Guid roleId, IEnumerable<string> permissionCodes)
    {
        await _roleRepo.UpdateRolePermissionsAsync(roleId, permissionCodes);
    }

    /// <summary>Gán danh sách vai trò cho một cán bộ / người dùng</summary>
    public async Task AssignRolesToUserAsync(Guid userId, IEnumerable<string> roleCodes)
    {
        await _roleRepo.AssignRolesToUserAsync(userId, roleCodes);
    }
}
