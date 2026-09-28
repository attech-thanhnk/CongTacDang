using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;

namespace CongTacDang.Application.Accounts;

/// <summary>
/// Chốt chặn quản trị khi khóa/xóa tài khoản (thiết kế phân quyền mục 5):
/// không tự khóa/xóa chính mình; luôn còn ≥ 1 tài khoản hoạt động có <c>system.roles.manage</c>
/// và ≥ 1 có <c>system.assignments.manage</c> (phạm vi Global).
/// </summary>
public interface IAdministratorGuard
{
    /// <summary>
    /// Ném <see cref="ConflictException"/> (409) nếu người thao tác tự khóa/xóa mình, hoặc nếu bỏ tài khoản
    /// <paramref name="targetUserId"/> khỏi nhóm đang hoạt động làm hệ thống mất quản trị viên cuối cùng.
    /// </summary>
    /// <param name="actorUserId">Người thao tác (null: hệ thống).</param>
    /// <param name="targetUserId">Tài khoản bị khóa/xóa.</param>
    /// <param name="actionName">Tên thao tác hiển thị ("khóa", "xóa").</param>
    /// <param name="ct">Hủy.</param>
    Task EnsureCanDisableAsync(Guid? actorUserId, Guid targetUserId, string actionName, CancellationToken ct = default);
}

/// <summary>Triển khai chốt chặn dựa trên <see cref="IPermissionResolver"/>.</summary>
public sealed class AdministratorGuard : IAdministratorGuard
{
    /// <summary>Các mã quyền phải luôn còn người nắm giữ.</summary>
    public static readonly IReadOnlyList<string> CriticalPermissions = new[]
    {
        PermissionCodes.SystemRolesManage,
        PermissionCodes.SystemAssignmentsManage
    };

    private readonly IUserAccountRepository _accounts;
    private readonly IPermissionResolver _permissions;

    /// <summary>Khởi tạo chốt chặn.</summary>
    public AdministratorGuard(IUserAccountRepository accounts, IPermissionResolver permissions)
    {
        _accounts = accounts;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task EnsureCanDisableAsync(Guid? actorUserId, Guid targetUserId, string actionName, CancellationToken ct = default)
    {
        if (actorUserId == targetUserId)
            throw new ConflictException(
                $"Bạn không thể {actionName} tài khoản của chính mình. Hãy nhờ một quản trị viên khác thực hiện nếu thật sự cần.");

        var target = await _permissions.GetAsync(targetUserId, ct);
        var missing = CriticalPermissions.Where(code => HoldsGlobally(target, code)).ToHashSet(StringComparer.Ordinal);
        if (missing.Count == 0)
            return;

        foreach (var userId in await _accounts.GetActiveUserIdsAsync(ct))
        {
            if (userId == targetUserId)
                continue;

            var effective = await _permissions.GetAsync(userId, ct);
            missing.RemoveWhere(code => HoldsGlobally(effective, code));
            if (missing.Count == 0)
                return;
        }

        var names = string.Join("\", \"", missing.Select(PermissionCodes.DisplayName));
        throw new ConflictException(
            $"Không thể {actionName} tài khoản này vì đây là tài khoản đang hoạt động cuối cùng có quyền \"{names}\". "
            + "Hãy gán quyền quản trị cho một tài khoản khác trước.");
    }

    private static bool HoldsGlobally(EffectivePermissions permissions, string code) =>
        permissions.GrantsFor(code).Any(grant => grant.ScopeType == ScopeType.Global);
}
