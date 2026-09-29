using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Repository quản trị vai trò và danh mục quyền (docs/thiet-ke/phan-quyen.md mục 2).
/// Mọi truy vấn bỏ qua vai trò/quyền đã xóa mềm (query filter).
/// </summary>
public interface IRoleRepository
{
    /// <summary>Lấy danh sách tất cả các vai trò (chưa xóa) kèm quyền hạn, không theo dõi thay đổi.</summary>
    Task<List<AppRole>> GetAllRolesWithPermissionsAsync();

    /// <summary>Lấy danh sách tất cả các quyền hạn (chưa xóa) trong CSDL.</summary>
    Task<List<Permission>> GetAllPermissionsAsync();

    /// <summary>Lấy vai trò theo Id kèm quyền hạn (không theo dõi thay đổi); null nếu không có hoặc đã xóa.</summary>
    Task<AppRole?> GetRoleByIdWithPermissionsAsync(Guid roleId);

    /// <summary>Lấy vai trò theo Id kèm quyền hạn để cập nhật (có theo dõi thay đổi).</summary>
    Task<AppRole?> GetRoleForUpdateAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>Đã có vai trò chưa xóa mang tên <paramref name="name"/> (không phân biệt hoa thường), trừ <paramref name="excludeRoleId"/>.</summary>
    Task<bool> RoleNameExistsAsync(string name, Guid? excludeRoleId, CancellationToken ct = default);

    /// <summary>Lấy bản ghi quyền theo mã (có theo dõi thay đổi, để gán vào vai trò).</summary>
    Task<List<Permission>> GetPermissionsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default);

    /// <summary>Thêm vai trò mới (chưa lưu).</summary>
    void AddRole(AppRole role);

    /// <summary>Ghi audit thay đổi danh sách quyền của vai trò (chưa lưu). Audit tự động không ghi được thay đổi bảng nối.</summary>
    void AddRolePermissionsAudit(Guid roleId, IReadOnlyCollection<string> previousCodes, IReadOnlyCollection<string> currentCodes);

    /// <summary>Lưu thay đổi (audit entity tự động qua DbContext).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Một bản gán đang hiệu lực hoặc sắp hiệu lực kèm mã quyền của vai trò (nguồn tính quyền).</summary>
/// <param name="AssignmentId">Id bản gán.</param>
/// <param name="RoleId">Vai trò.</param>
/// <param name="RoleName">Tên vai trò.</param>
/// <param name="ScopeType">Loại phạm vi.</param>
/// <param name="ScopeId">Id Phòng/Chi bộ (null khi Global).</param>
/// <param name="ValidFrom">Hiệu lực từ.</param>
/// <param name="ValidTo">Hiệu lực đến (không bao gồm).</param>
/// <param name="PermissionCodes">Mã quyền (chưa xóa) của vai trò.</param>
/// <param name="CoveredScopeIds">
/// Id đơn vị thuộc phạm vi: nút được gán và mọi nút con cháu trong cây đơn vị (task 14). null → chỉ <paramref name="ScopeId"/>.
/// </param>
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

/// <summary>Trạng thái tài khoản và các bản gán chưa hết hạn của một người dùng.</summary>
/// <param name="IsActive">Tài khoản đang hoạt động.</param>
/// <param name="Assignments">Bản gán chưa xóa, chưa hết hạn, vai trò chưa xóa (gồm cả bản gán chưa tới ngày hiệu lực).</param>
public sealed record UserAccessSnapshot(bool IsActive, IReadOnlyList<AssignmentGrantSource> Assignments);

/// <summary>Bản gán Global đang hiệu lực của người dùng đang hoạt động, vai trò có quyền quản trị.</summary>
/// <param name="AssignmentId">Id bản gán.</param>
/// <param name="UserId">Người dùng.</param>
/// <param name="RoleId">Vai trò.</param>
/// <param name="PermissionCodes">Mã quyền quản trị (<c>system.roles.manage</c>, <c>system.assignments.manage</c>) vai trò đang có.</param>
public sealed record AdministratorGrantRow(Guid AssignmentId, Guid UserId, Guid RoleId, IReadOnlyList<string> PermissionCodes);

/// <summary>Điều kiện tra cứu bản gán vai trò.</summary>
/// <param name="UserId">Lọc theo người.</param>
/// <param name="RoleId">Lọc theo vai trò.</param>
/// <param name="ScopeType">Lọc theo loại phạm vi.</param>
/// <param name="ScopeId">Lọc theo Phòng/Chi bộ.</param>
/// <param name="ActiveOn">Chỉ lấy bản gán hiệu lực tại thời điểm này.</param>
/// <param name="Id">Lọc theo Id bản gán.</param>
public sealed record RoleAssignmentFilter(
    Guid? UserId = null,
    Guid? RoleId = null,
    RoleScopeType? ScopeType = null,
    Guid? ScopeId = null,
    DateTime? ActiveOn = null,
    Guid? Id = null);

/// <summary>Repository bản gán vai trò có phạm vi và thời hạn.</summary>
public interface IRoleAssignmentRepository
{
    /// <summary>
    /// Nạp trạng thái tài khoản và các bản gán chưa hết hạn tại <paramref name="now"/> kèm mã quyền của vai trò.
    /// Null nếu người dùng không tồn tại hoặc đã xóa.
    /// </summary>
    Task<UserAccessSnapshot?> GetAccessSnapshotAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Tra cứu bản gán (không theo dõi thay đổi), kèm vai trò và người dùng.</summary>
    Task<List<UserRoleAssignment>> QueryAsync(RoleAssignmentFilter filter, CancellationToken ct = default);

    /// <summary>Lấy bản gán để cập nhật (có theo dõi thay đổi), kèm vai trò + quyền.</summary>
    Task<UserRoleAssignment?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Bản gán chưa hết hạn (đang/sắp hiệu lực) của người dùng, kèm vai trò (không theo dõi thay đổi).</summary>
    Task<List<UserRoleAssignment>> GetCurrentOrFutureForUserAsync(Guid userId, DateTime now, CancellationToken ct = default);

    /// <summary>Số bản gán chưa hết hạn của vai trò.</summary>
    Task<int> CountCurrentOrFutureByRoleAsync(Guid roleId, DateTime now, CancellationToken ct = default);

    /// <summary>Vai trò có bản gán chưa hết hạn với phạm vi khác Global hay không.</summary>
    Task<bool> HasNonGlobalCurrentOrFutureAssignmentsAsync(Guid roleId, DateTime now, CancellationToken ct = default);

    /// <summary>Bản gán Global đang hiệu lực của người dùng đang hoạt động mà vai trò có quyền quản trị (chốt "luôn còn quản trị").</summary>
    Task<List<AdministratorGrantRow>> GetAdministratorGrantsAsync(DateTime now, CancellationToken ct = default);

    /// <summary>Người dùng tồn tại và chưa xóa.</summary>
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Phòng/Chi bộ tồn tại, chưa xóa và đang hoạt động.</summary>
    Task<bool> ScopeExistsAsync(RoleScopeType scopeType, Guid scopeId, CancellationToken ct = default);

    /// <summary>Tên Phòng/Chi bộ theo Id (kể cả đã ngừng hoạt động).</summary>
    Task<Dictionary<Guid, string>> GetScopeNamesAsync(IReadOnlyCollection<Guid> departmentIds, IReadOnlyCollection<Guid> partyCellIds, CancellationToken ct = default);

    /// <summary>Thêm bản gán (chưa lưu).</summary>
    void Add(UserRoleAssignment assignment);

    /// <summary>Lưu thay đổi (audit tự động qua DbContext).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
