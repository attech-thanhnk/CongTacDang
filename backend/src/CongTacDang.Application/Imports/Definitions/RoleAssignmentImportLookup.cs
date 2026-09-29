using CongTacDang.Application.Common.Security;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>
/// Tra cứu chỉ đọc phục vụ kiểm tra chéo khi nhập bản gán vai trò (<c>role-assignments</c>).
/// Đơn vị chính quyền / tổ chức Đảng tra qua <see cref="IImportLookup"/> của khung import.
/// </summary>
public interface IRoleAssignmentImportLookup
{
    /// <summary>Tài khoản có tên đăng nhập (không phân biệt hoa thường) trong <paramref name="usernames"/>, kể cả đã xóa mềm.</summary>
    Task<IReadOnlyList<AssignmentUserEntry>> FindUsersAsync(IReadOnlyCollection<string> usernames, CancellationToken ct);

    /// <summary>Các vai trò chưa xóa kèm mã quyền.</summary>
    Task<IReadOnlyList<AssignmentRoleEntry>> GetRolesAsync(CancellationToken ct);

    /// <summary>Bản gán chưa xóa, chưa hết hạn tại <paramref name="nowUtc"/> (đang hoặc sắp hiệu lực) của các tài khoản.</summary>
    Task<IReadOnlyList<ExistingAssignmentEntry>> GetCurrentOrFutureAssignmentsAsync(
        IReadOnlyCollection<Guid> userIds, DateTime nowUtc, CancellationToken ct);
}

/// <summary>Tài khoản phục vụ tra cứu.</summary>
public sealed record AssignmentUserEntry(Guid Id, string Username, string FullName, bool IsActive, bool IsDeleted);

/// <summary>Vai trò phục vụ tra cứu.</summary>
public sealed record AssignmentRoleEntry(Guid Id, string Name, IReadOnlyList<string> PermissionCodes);

/// <summary>Bản gán hiện có phục vụ kiểm tra trùng.</summary>
public sealed record ExistingAssignmentEntry(
    Guid UserId, Guid RoleId, ScopeType ScopeType, Guid? ScopeId, DateTime ValidFrom, DateTime? ValidTo);
