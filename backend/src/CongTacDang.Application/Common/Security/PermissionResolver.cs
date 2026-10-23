using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Chỉ tính bản gán đang hiệu lực (<c>ValidFrom ≤ now &lt; ValidTo</c>); mã quyền lạ bị bỏ qua (cảnh báo một lần).
/// Đổi cấu trúc cây đơn vị → phải xóa toàn bộ cache.
/// </summary>
public sealed class PermissionResolver : IPermissionResolver
{
    private static readonly ConcurrentDictionary<string, byte> WarnedUnknownCodes = new(StringComparer.Ordinal);

    private readonly Func<Guid, DateTime, CancellationToken, Task<UserAccessSnapshot?>> _load;
    private readonly PermissionCache _cache;
    private readonly ILogger<PermissionResolver> _logger;

    public PermissionResolver(IRoleAssignmentRepository assignments, PermissionCache cache, ILogger<PermissionResolver>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        _load = assignments.GetAccessSnapshotAsync;
        _cache = cache;
        _logger = logger ?? NullLogger<PermissionResolver>.Instance;
    }

    public async Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default)
    {
        if (_cache.TryGet(userId, out var cached))
            return cached;

        ct.ThrowIfCancellationRequested();
        var generation = _cache.Generation;
        var nowOffset = _cache.UtcNow;
        var now = nowOffset.UtcDateTime;
        var snapshot = await _load(userId, now, ct);

        EffectivePermissions result;
        DateTimeOffset? notAfter = null;
        if (snapshot == null || !snapshot.IsActive)
        {
            result = EffectivePermissions.Empty(userId);
        }
        else
        {
            var effective = snapshot.Assignments
                .Where(a => a.ValidFrom <= now && (a.ValidTo == null || now < a.ValidTo.Value))
                .ToList();
            result = new EffectivePermissions(userId, BuildGrants(effective), effective.Select(a => a.RoleName).Distinct());

            // Mục cache hết hạn đúng lúc có bản gán bắt đầu hoặc kết thúc hiệu lực.
            var boundaries = snapshot.Assignments
                .SelectMany(a => new[] { a.ValidFrom > now ? a.ValidFrom : (DateTime?)null, a.ValidTo })
                .Where(t => t.HasValue && t.Value > now)
                .Select(t => t!.Value)
                .ToList();
            if (boundaries.Count > 0)
                notAfter = new DateTimeOffset(DateTime.SpecifyKind(boundaries.Min(), DateTimeKind.Utc));
        }

        _cache.Set(userId, result, generation, notAfter);
        return result;
    }

    /// <summary>Loại mã lạ và quyền gán sai phạm vi.</summary>
    private IEnumerable<PermissionGrant> BuildGrants(IEnumerable<AssignmentGrantSource> assignments)
    {
        var grants = new List<PermissionGrant>();
        foreach (var assignment in assignments)
        {
            var scopeType = (ScopeType)(int)assignment.ScopeType;
            var scopeId = assignment.ScopeType == RoleScopeType.Global ? null : assignment.ScopeId;
            if (scopeType != ScopeType.Global && scopeId == null)
                continue; // dữ liệu hỏng: phạm vi đơn vị chính quyền/tổ chức Đảng thiếu Id → bỏ qua, không mở rộng thành Global

            foreach (var code in assignment.PermissionCodes.Distinct(StringComparer.Ordinal))
            {
                var definition = PermissionCodes.Find(code);
                if (definition == null)
                {
                    if (WarnedUnknownCodes.TryAdd(code, 0))
                    {
                        _logger.LogWarning(
                            "Bỏ qua mã quyền {Code} của vai trò {Role}: mã không còn trong danh mục PermissionCodes.",
                            code, assignment.RoleName);
                    }
                    continue;
                }

                if (!definition.AppliesScope && scopeType != ScopeType.Global)
                    continue; // quyền không áp dụng phạm vi chỉ có nghĩa khi gán Global

                grants.Add(new PermissionGrant(code, scopeType, scopeId, assignment.AssignmentId, assignment.RoleName,
                    scopeType == ScopeType.Global ? null : assignment.CoveredScopeIds));
            }
        }

        return grants;
    }
}
