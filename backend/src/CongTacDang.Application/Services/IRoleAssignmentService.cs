using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

public sealed record RoleAssignmentQuery(
    Guid? UserId = null,
    Guid? RoleId = null,
    ScopeType? ScopeType = null,
    Guid? ScopeId = null,
    DateTime? ActiveOn = null);

/// <summary>Bản gán vai trò kèm phạm vi và thời hạn; mọi thay đổi xóa ngay cache quyền của người liên quan.</summary>
public interface IRoleAssignmentService
{
    /// <summary>
    /// <paramref name="validFrom"/> null = ngay; <paramref name="validTo"/> null = không thời hạn (không bao gồm mốc này).
    /// Thời gian không có múi giờ hiểu là UTC. Dùng được trong transaction của <c>IUnitOfWork</c>.
    /// </summary>
    Task<RoleAssignmentDto> AssignAsync(
        Guid userId,
        Guid roleId,
        ScopeType scopeType,
        Guid? scopeId,
        DateTime? validFrom,
        DateTime? validTo,
        string? note,
        CancellationToken ct = default);

    /// <summary><paramref name="validFrom"/> null = giữ nguyên.</summary>
    Task<RoleAssignmentDto> UpdateAsync(Guid assignmentId, DateTime? validFrom, DateTime? validTo, string? note, CancellationToken ct = default);

    Task<RoleAssignmentDto> EndAsync(Guid assignmentId, CancellationToken ct = default);

    Task DeleteAsync(Guid assignmentId, CancellationToken ct = default);

    Task<List<RoleAssignmentDto>> QueryAsync(RoleAssignmentQuery query, CancellationToken ct = default);

    /// <summary>"Người X làm được gì": quyền hiệu lực, phạm vi và bản gán nguồn.</summary>
    Task<UserEffectivePermissionsDto> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Cho trường <c>grants</c> của <c>/api/auth/me</c> và phản hồi đăng nhập.</summary>
    Task<List<AccessGrantDto>> GetGrantsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>409 nếu khóa/xóa <paramref name="userId"/> làm mất quản trị cuối cùng.</summary>
    Task EnsureAdministratorsRemainWithoutUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Có bản gán chưa hết hạn ở phạm vi Toàn công ty hoặc vai trò có quyền <c>system.*</c>; xét cả tài khoản đang khóa.
    /// Quản lý tài khoản đặc quyền cần <c>system.users.manage</c> phạm vi Toàn công ty.
    /// </summary>
    Task<bool> IsPrivilegedAccountAsync(Guid userId, CancellationToken ct = default);
}
