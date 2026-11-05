using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Người được giao bước nội bộ của luồng đánh giá trên một hồ sơ: có quyền của bước ở phạm vi bao trùm hồ sơ, không phải chủ hồ sơ,
/// và có phạm vi gần hồ sơ nhất (sâu nhất trong cây; Toàn công ty xa nhất). Chỉ quyết định việc giao cho ai (việc cần xử lý,
/// tên người chờ ở tiến độ, màn hình người thực hiện); người có quyền ở cấp trên vẫn làm được bước.
/// </summary>
public interface IStepPerformers
{
    /// <summary>Tập người được giao bước, đã áp quy tắc gần nhất; rỗng khi không ai làm được.</summary>
    Task<IReadOnlyList<Guid>> ListAsync(string permission, AccessTarget target, CancellationToken ct = default);

    /// <summary>Như trên, bỏ trước người có một trong các quyền <paramref name="excludeHolders"/> trên hồ sơ.</summary>
    Task<IReadOnlyList<Guid>> ListAsync(string permission, AccessTarget target, IReadOnlyCollection<string> excludeHolders, CancellationToken ct = default);

    /// <summary>Người dùng có thuộc tập người được giao bước không.</summary>
    Task<bool> IsPerformerAsync(Guid userId, string permission, AccessTarget target, CancellationToken ct = default);

    /// <summary>Chủ hồ sơ giữ quyền của bước ở phạm vi gần hồ sơ hơn mọi người khác, xem <see cref="StepPerformerRule.OwnerIsNearest"/>.</summary>
    Task<bool> OwnerIsNearestAsync(string permission, AccessTarget target, CancellationToken ct = default);
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

    /// <summary>
    /// Chủ hồ sơ có quyền của bước (bỏ qua xung đột lợi ích) và không ai khác có quyền ở phạm vi gần bằng hoặc gần hơn —
    /// tức chủ hồ sơ là người đứng đầu đơn vị đối với bước này.
    /// </summary>
    public static bool OwnerIsNearest(IEnumerable<EffectivePermissions> candidates, string permission, AccessTarget target, OrgPaths? paths)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(target);
        if (target.OwnerId is not { } ownerId)
            return false;
        var list = candidates.ToList();
        var owner = list.FirstOrDefault(p => p.UserId == ownerId);
        if (owner == null || Distance(owner, permission, target with { OwnerId = null }, paths) is not { } own)
            return false;
        var others = list.Where(p => p.UserId != ownerId)
            .Select(p => Distance(p, permission, target, paths))
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToList();
        return others.Count == 0 || own < others.Min();
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

        var effective = await CandidatesAsync(permission, ct);
        var result = StepPerformerRule.Nearest(effective, permission, target with { RequireAllUnits = false }, _paths);
        _results[key] = result;
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> ListAsync(string permission, AccessTarget target, IReadOnlyCollection<string> excludeHolders, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(excludeHolders);
        var scoped = target with { RequireAllUnits = false };
        var effective = (await CandidatesAsync(permission, ct))
            .Where(p => !excludeHolders.Any(code => AuthorizationGuard.Evaluate(p, code, scoped)))
            .ToList();
        return StepPerformerRule.Nearest(effective, permission, scoped, _paths);
    }

    /// <inheritdoc />
    public async Task<bool> OwnerIsNearestAsync(string permission, AccessTarget target, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        var effective = await CandidatesAsync(permission, ct);
        return StepPerformerRule.OwnerIsNearest(effective, permission, target with { RequireAllUnits = false }, _paths);
    }

    private async Task<List<EffectivePermissions>> CandidatesAsync(string permission, CancellationToken ct)
    {
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
        return effective;
    }

    /// <inheritdoc />
    public async Task<bool> IsPerformerAsync(Guid userId, string permission, AccessTarget target, CancellationToken ct = default) =>
        (await ListAsync(permission, target, ct)).Contains(userId);
}
