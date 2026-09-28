using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Triển khai v0 của <see cref="IPermissionResolver"/> trên mô hình hiện tại (user ↔ role nhiều-nhiều):
/// mọi quyền của mọi vai trò được gán → <see cref="PermissionGrant"/> phạm vi <see cref="ScopeType.Global"/>,
/// <c>SourceAssignmentId = Guid.Empty</c>. Task 09 thay bằng bản tính theo bản gán vai trò có phạm vi/thời hạn.
/// </summary>
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly IUserRepository _users;
    private readonly PermissionCache _cache;

    /// <summary>Khởi tạo resolver dùng repository người dùng và cache dùng chung.</summary>
    public PermissionResolver(IUserRepository users, PermissionCache cache)
    {
        _users = users;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default)
    {
        if (_cache.TryGet(userId, out var cached))
            return cached;

        ct.ThrowIfCancellationRequested();
        var generation = _cache.Generation;
        var user = await _users.GetWithRolesAndPermissionsByIdAsync(userId);

        EffectivePermissions result;
        if (user == null || user.IsDeleted || !user.IsActive)
        {
            result = EffectivePermissions.Empty(userId);
        }
        else
        {
            var roles = user.Roles.Where(role => !role.IsDeleted).ToList();
            var grants = roles
                .SelectMany(role => role.Permissions
                    .Where(permission => !permission.IsDeleted)
                    .Select(permission => new PermissionGrant(
                        permission.Code, ScopeType.Global, null, Guid.Empty, role.Name)))
                .Distinct();
            result = new EffectivePermissions(userId, grants, roles.Select(role => role.Code));
        }

        _cache.Set(userId, result, generation);
        return result;
    }
}
