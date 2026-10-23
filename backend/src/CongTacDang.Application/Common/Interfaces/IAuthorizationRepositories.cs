using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Mọi truy vấn bỏ qua vai trò/quyền đã xóa mềm.</summary>
public interface IRoleRepository
{
    /// <summary>Không theo dõi.</summary>
    Task<List<AppRole>> GetAllRolesWithPermissionsAsync();

    Task<List<Permission>> GetAllPermissionsAsync();

    /// <summary>Không theo dõi; null nếu không có hoặc đã xóa.</summary>
    Task<AppRole?> GetRoleByIdWithPermissionsAsync(Guid roleId);

    /// <summary>Kèm quyền, được theo dõi.</summary>
    Task<AppRole?> GetRoleForUpdateAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>Không phân biệt hoa thường.</summary>
    Task<bool> RoleNameExistsAsync(string name, Guid? excludeRoleId, CancellationToken ct = default);

    /// <summary>Được theo dõi, để gán vào vai trò.</summary>
    Task<List<Permission>> GetPermissionsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default);

    void AddRole(AppRole role);

    /// <summary>Audit tự động không ghi được thay đổi bảng nối nên ghi riêng (chưa lưu).</summary>
    void AddRolePermissionsAudit(Guid roleId, IReadOnlyCollection<string> previousCodes, IReadOnlyCollection<string> currentCodes);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Một bản gán đang hoặc sắp hiệu lực kèm mã quyền của vai trò (nguồn tính quyền).</summary>
/// <param name="ScopeId">Null khi Global.</param>
/// <param name="ValidTo">Không bao gồm.</param>
/// <param name="CoveredScopeIds">Nút được gán và mọi nút con cháu; null → chỉ <paramref name="ScopeId"/>.</param>
public sealed record AssignmentGrantSource(
    Guid AssignmentId,
    Guid RoleId,
    string RoleName,
    RoleScopeType ScopeType,
    Guid? ScopeId,
    DateTime ValidFrom,
    DateTime? ValidTo,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<Guid>? CoveredScopeIds = null);

/// <param name="Assignments">Bản gán chưa xóa, chưa hết hạn, vai trò chưa xóa (gồm cả bản gán chưa tới ngày hiệu lực).</param>
public sealed record UserAccessSnapshot(bool IsActive, IReadOnlyList<AssignmentGrantSource> Assignments);

/// <summary>Bản gán Global đang hiệu lực của người dùng đang hoạt động, vai trò có quyền quản trị.</summary>
/// <param name="PermissionCodes">Mã quyền quản trị (<c>system.roles.manage</c>, <c>system.assignments.manage</c>) vai trò đang có.</param>
public sealed record AdministratorGrantRow(Guid AssignmentId, Guid UserId, Guid RoleId, IReadOnlyList<string> PermissionCodes);

/// <param name="ActiveOn">Chỉ lấy bản gán hiệu lực tại thời điểm này.</param>
public sealed record RoleAssignmentFilter(
    Guid? UserId = null,
    Guid? RoleId = null,
    RoleScopeType? ScopeType = null,
    Guid? ScopeId = null,
    DateTime? ActiveOn = null,
    Guid? Id = null);

public interface IRoleAssignmentRepository
{
    /// <summary>Bản gán chưa hết hạn tại <paramref name="now"/> kèm mã quyền; null nếu người dùng không tồn tại/đã xóa.</summary>
    Task<UserAccessSnapshot?> GetAccessSnapshotAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Không theo dõi, kèm vai trò và người dùng.</summary>
    Task<List<UserRoleAssignment>> QueryAsync(RoleAssignmentFilter filter, CancellationToken ct = default);

    /// <summary>Được theo dõi, kèm vai trò + quyền.</summary>
    Task<UserRoleAssignment?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Gồm bản gán đang và sắp hiệu lực; không theo dõi.</summary>
    Task<List<UserRoleAssignment>> GetCurrentOrFutureForUserAsync(Guid userId, DateTime now, CancellationToken ct = default);

    Task<int> CountCurrentOrFutureByRoleAsync(Guid roleId, DateTime now, CancellationToken ct = default);

    Task<bool> HasNonGlobalCurrentOrFutureAssignmentsAsync(Guid roleId, DateTime now, CancellationToken ct = default);

    /// <summary>Dùng cho chốt "luôn còn quản trị".</summary>
    Task<List<AdministratorGrantRow>> GetAdministratorGrantsAsync(DateTime now, CancellationToken ct = default);

    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tồn tại, chưa xóa và đang hoạt động.</summary>
    Task<bool> ScopeExistsAsync(RoleScopeType scopeType, Guid scopeId, CancellationToken ct = default);

    /// <summary>Kể cả đơn vị đã ngừng hoạt động.</summary>
    Task<Dictionary<Guid, string>> GetScopeNamesAsync(IReadOnlyCollection<Guid> departmentIds, IReadOnlyCollection<Guid> partyCellIds, CancellationToken ct = default);

    void Add(UserRoleAssignment assignment);

    Task SaveChangesAsync(CancellationToken ct = default);
}
