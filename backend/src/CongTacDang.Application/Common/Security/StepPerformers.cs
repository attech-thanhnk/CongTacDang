using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Người làm bước nội bộ của luồng đánh giá trên một hồ sơ: có quyền của bước ở phạm vi bao trùm hồ sơ, không phải chủ hồ sơ,
/// và có phạm vi gần hồ sơ nhất (sâu nhất trong cây; Toàn công ty xa nhất). Dùng chung cho hành động, việc cần xử lý,
/// kiểm tra kẹt luồng, tiến độ và màn hình người thực hiện.
/// </summary>
public interface IStepPerformers
{
    /// <summary>Tập người làm được bước, đã áp quy tắc gần nhất; rỗng khi không ai làm được.</summary>
    Task<IReadOnlyList<Guid>> ListAsync(string permission, AccessTarget target, CancellationToken ct = default);

    /// <summary>Người dùng có thuộc tập người làm được bước không.</summary>
    Task<bool> IsPerformerAsync(Guid userId, string permission, AccessTarget target, CancellationToken ct = default);
}

/// <summary>Thuật toán chọn người gần nhất, tách riêng để kiểm thử không cần CSDL.</summary>
public static class StepPerformerRule
{
    /// <summary>Khoảng cách của bản gán Toàn công ty: xa nhất.</summary>
    public const int GlobalDistance = int.MaxValue;

    /// <summary>Không xác định được vị trí trong cây: xa hơn mọi đơn vị nhưng gần hơn Toàn công ty.</summary>
    public const int UnknownDistance = int.MaxValue - 1;

    /// <summary>
    /// Số cấp từ nút được gán xuống đơn vị của hồ sơ (0 = gán đúng đơn vị); null khi người không có quyền trên hồ sơ.
    /// </summary>
    public static int? Distance(EffectivePermissions permissions, string permission, AccessTarget target, OrgPaths? paths)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (!permissions.IsActive)
            return null;
        int? best = null;
        foreach (var grant in permissions.GrantsFor(permission))
        {
            // Từng bản gán kiểm tra bằng đúng luật của guard.
            if (!AuthorizationGuard.Evaluate(new EffectivePermissions(permissions.UserId, new[] { grant }), permission, target))
                continue;
            var distance = grant.ScopeType switch
            {
                ScopeType.Global => GlobalDistance,
                ScopeType.Department => UnitDistance(grant.ScopeId, target.DepartmentId, paths?.Departments),
                ScopeType.PartyCell => UnitDistance(grant.ScopeId, target.PartyCellId, paths?.PartyCells),
                _ => GlobalDistance
            };
            if (best is null || distance < best)
                best = distance;
        }
        return best;
    }

    /// <summary>Chủ hồ sơ luôn bị loại; trong số còn lại chỉ giữ người có khoảng cách nhỏ nhất (bằng nhau thì giữ cả).</summary>
    public static IReadOnlyList<Guid> Nearest(IEnumerable<EffectivePermissions> candidates, string permission, AccessTarget target, OrgPaths? paths)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(target);
        var scored = candidates
            .Where(p => !target.OwnerId.HasValue || p.UserId != target.OwnerId.Value)
            .Select(p => (p.UserId, Distance: Distance(p, permission, target, paths)))
            .Where(x => x.Distance.HasValue)
            .ToList();
        if (scored.Count == 0)
            return Array.Empty<Guid>();
        var min = scored.Min(x => x.Distance!.Value);
        return scored.Where(x => x.Distance == min).Select(x => x.UserId).Distinct().ToList();
    }

    private static int UnitDistance(Guid? scopeId, Guid? unitId, IReadOnlyDictionary<Guid, string>? tree)
    {
        if (!scopeId.HasValue || !unitId.HasValue)
            return UnknownDistance;
        if (scopeId.Value == unitId.Value)
            return 0;
        if (tree == null || !tree.TryGetValue(unitId.Value, out var path) || string.IsNullOrEmpty(path))
            return UnknownDistance;
        var chain = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.FindIndex(chain, s => Guid.TryParse(s, out var id) && id == scopeId.Value);
        return index < 0 ? UnknownDistance : chain.Length - 1 - index;
    }
}

/// <summary>Scoped: danh sách ứng viên, quyền, cây đơn vị và kết quả nạp một lần mỗi request.</summary>
public sealed class StepPerformers : IStepPerformers
{
    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IPermissionResolver _resolver;
    private readonly IPositionRepository _positions;
    private readonly Dictionary<string, List<Guid>> _candidates = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, EffectivePermissions> _effective = new();
    private readonly Dictionary<(string, Guid?, Guid?, Guid?), IReadOnlyList<Guid>> _results = new();
    private OrgPaths? _paths;

    public StepPerformers(IEvaluationWorkflowRepository repo, IPermissionResolver resolver, IPositionRepository positions)
    {
        _repo = repo;
        _resolver = resolver;
        _positions = positions;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListAsync(string permission, AccessTarget target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var key = (permission, target.OwnerId, target.DepartmentId, target.PartyCellId);
        if (_results.TryGetValue(key, out var cached))
            return cached;

        if (!_candidates.TryGetValue(permission, out var ids))
        {
            ids = await _repo.ListActiveUserIdsWithAnyPermissionAsync(new[] { permission }, DateTime.UtcNow, ct);
            _candidates[permission] = ids;
        }
        var effective = new List<EffectivePermissions>(ids.Count);
        foreach (var id in ids)
        {
            if (!_effective.TryGetValue(id, out var p))
            {
                p = await _resolver.GetAsync(id, ct);
                _effective[id] = p;
            }
            effective.Add(p);
        }
        _paths ??= await _positions.GetOrgPathsAsync(ct);

        var result = StepPerformerRule.Nearest(effective, permission, target with { RequireAllUnits = false }, _paths);
        _results[key] = result;
        return result;
    }

    /// <inheritdoc />
    public async Task<bool> IsPerformerAsync(Guid userId, string permission, AccessTarget target, CancellationToken ct = default) =>
        (await ListAsync(permission, target, ct)).Contains(userId);
}
