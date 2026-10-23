using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>Quản trị vai trò và quyền của vai trò (cần <c>system.roles.manage</c>).</summary>
public interface IRoleService
{
    /// <summary>Kèm mã quyền và số bản gán chưa hết hạn.</summary>
    Task<List<AdminRoleDto>> GetRolesAsync(CancellationToken ct = default);

    Task<AdminRoleDto> GetRoleAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>Danh mục quyền lấy từ <c>PermissionCodes</c>, nhóm theo phân hệ.</summary>
    Task<List<PermissionModuleDto>> GetPermissionCatalogAsync(CancellationToken ct = default);

    Task<AdminRoleDto> CreateRoleAsync(CreateRoleRequestDto request, CancellationToken ct = default);

    Task<AdminRoleDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequestDto request, CancellationToken ct = default);

    /// <summary>Đặt lại toàn bộ danh sách quyền.</summary>
    Task<AdminRoleDto> UpdateRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default);

    /// <summary>Không xóa được vai trò bảo vệ hoặc đang được gán.</summary>
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);
}
