using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Quản trị vai trò và quyền của vai trò (docs/thiet-ke/phan-quyen.md mục 2, 5). Yêu cầu <c>system.roles.manage</c>.
/// Lỗi: 400 (<c>ValidationException</c>) dữ liệu sai, 403 sửa quyền của vai trò mình đang được gán,
/// 404 không tìm thấy, 409 trùng tên / vai trò bảo vệ / đang được gán / mất quản trị cuối cùng.
/// </summary>
public interface IRoleService
{
    /// <summary>Danh sách vai trò (chưa xóa) kèm mã quyền và số bản gán chưa hết hạn.</summary>
    Task<List<AdminRoleDto>> GetRolesAsync(CancellationToken ct = default);

    /// <summary>Chi tiết một vai trò.</summary>
    Task<AdminRoleDto> GetRoleAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>Danh mục quyền nhóm theo phân hệ (lấy từ code — <c>PermissionCodes</c>).</summary>
    Task<List<PermissionModuleDto>> GetPermissionCatalogAsync(CancellationToken ct = default);

    /// <summary>Tạo vai trò mới.</summary>
    Task<AdminRoleDto> CreateRoleAsync(CreateRoleRequestDto request, CancellationToken ct = default);

    /// <summary>Đổi tên, mô tả vai trò.</summary>
    Task<AdminRoleDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequestDto request, CancellationToken ct = default);

    /// <summary>Đặt lại toàn bộ danh sách quyền của vai trò.</summary>
    Task<AdminRoleDto> UpdateRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default);

    /// <summary>Xóa mềm vai trò (không xóa được vai trò bảo vệ hoặc đang được gán).</summary>
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);
}
