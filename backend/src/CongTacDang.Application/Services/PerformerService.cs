using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Màn hình "Người thực hiện": ai làm được việc gì tại một đơn vị. Chỉ đọc; thêm/gỡ dùng API gán vai trò.</summary>
public interface IPerformerService
{
    /// <summary>Cả hai đơn vị null = Toàn công ty.</summary>
    Task<UnitPerformersDto> GetAsync(Guid? departmentId, Guid? partyCellId, CancellationToken ct = default);

    /// <summary>
    /// Bản gán cần xem lại: gán không thời hạn tại đơn vị mà người được giao không giữ chức vụ cũng không thuộc đơn vị đó,
    /// hoặc giao tạm sắp hết hạn.
    /// </summary>
    Task<List<PerformerReviewItemDto>> GetReviewAsync(Guid? userId, CancellationToken ct = default);
}

/// <summary>Cùng luật với kiểm tra thiếu người thực hiện của kỳ (<see cref="AuthorizationGuard.Evaluate"/>).</summary>
public sealed class PerformerService : IPerformerService
{
    /// <summary>Theo thứ tự các bước; bước của chủ hồ sơ không thuộc màn hình này.</summary>
    public static readonly IReadOnlyList<string> JobPermissions = new[]
    {
        WorkflowPermissions.TasksApprove,
        WorkflowPermissions.CellConfirm,
        WorkflowPermissions.CollectiveRecord,
        WorkflowPermissions.Appraise,
        WorkflowPermissions.DirectorReview,
        WorkflowPermissions.UnitReview,
        WorkflowPermissions.Decide,
        WorkflowPermissions.Publish,
        WorkflowPermissions.ExternalRecord
    }.Where(WorkflowActions.IsAssignableStepPermission).ToList();

    private readonly IEvaluationWorkflowRepository _workflow;
    private readonly IRoleAssignmentRepository _assignments;
    private readonly IRoleRepository _roles;
    private readonly IPermissionResolver _resolver;
    private readonly IAuthorizationGuard _guard;
    private readonly IPositionRepository _positions;
    private readonly TimeProvider _time;

    /// <summary>Người được giao không giữ chức vụ nhưng là cán bộ của nơi giao (hoặc đơn vị con).</summary>
    public const string MemberOfUnitText = "Cán bộ của đơn vị";

    /// <summary>Bản giao tạm còn ít ngày hơn số này thì cần xem lại.</summary>
    public const int ExpiringSoonDays = 7;

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public PerformerService(
        IEvaluationWorkflowRepository workflow,
        IRoleAssignmentRepository assignments,
        IRoleRepository roles,
        IPermissionResolver resolver,
        IAuthorizationGuard guard,
        IPositionRepository positions,
        TimeProvider? time = null)
    {
        _workflow = workflow;
        _assignments = assignments;
        _roles = roles;
        _resolver = resolver;
        _guard = guard;
        _positions = positions;
        _time = time ?? TimeProvider.System;
    }

    private void EnsureCanManage()
    {
        if (!_guard.Can(PermissionCodes.SystemAssignmentsManage, AccessTarget.None))
        {
            throw new ForbiddenException(
                $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemAssignmentsManage)}\" (phạm vi Toàn công ty). "
                + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }
    }

    /// <inheritdoc />
    public async Task<UnitPerformersDto> GetAsync(Guid? departmentId, Guid? partyCellId, CancellationToken ct = default)
    {
        EnsureCanManage();

        if (departmentId == Guid.Empty)
            departmentId = null;
        if (partyCellId == Guid.Empty)
            partyCellId = null;
        if (departmentId.HasValue && partyCellId.HasValue)
            throw new ValidationException("Chỉ chọn một đơn vị: đơn vị chính quyền hoặc tổ chức Đảng.");

        var scopeType = departmentId.HasValue ? ScopeType.Department : partyCellId.HasValue ? ScopeType.PartyCell : ScopeType.Global;
        var scopeId = departmentId ?? partyCellId;
        var result = new UnitPerformersDto
        {
            ScopeType = scopeType.ToString(),
            ScopeId = scopeId,
            ScopeName = RoleAssignmentService.GlobalScopeName
        };

        if (scopeId.HasValue)
        {
            var names = await _assignments.GetScopeNamesAsync(
                departmentId.HasValue ? new[] { departmentId.Value } : Array.Empty<Guid>(),
                partyCellId.HasValue ? new[] { partyCellId.Value } : Array.Empty<Guid>(),
                ct);
            if (!names.TryGetValue(scopeId.Value, out var name))
            {
                throw new NotFoundException(scopeType == ScopeType.Department
                    ? "Không tìm thấy đơn vị chính quyền được chọn."
                    : "Không tìm thấy tổ chức Đảng được chọn.");
            }
            result.ScopeName = name;
            result.ScopeActive = await _assignments.ScopeExistsAsync((RoleScopeType)(int)scopeType, scopeId.Value, ct);
        }

        // "Một hồ sơ bất kỳ thuộc đơn vị" (không có chủ → không xét xung đột lợi ích). Hồ sơ mang cả Phòng lẫn Chi bộ,
        // nên tính cả người giao theo cây kia để không hiện nhầm "Chưa có người".
        var targets = new List<AccessTarget>();
        switch (scopeType)
        {
            case ScopeType.Department:
                targets.Add(new AccessTarget(null, scopeId, null));
                foreach (var cellId in (await _workflow.SearchMembersAsync(scopeId, null, null, ct))
                             .Where(m => m.PartyCellId.HasValue).Select(m => m.PartyCellId).Distinct())
                    targets.Add(new AccessTarget(null, scopeId, cellId));
                break;
            case ScopeType.PartyCell:
                targets.Add(new AccessTarget(null, null, scopeId));
                foreach (var deptId in (await _workflow.SearchMembersAsync(null, scopeId, null, ct))
                             .Where(m => m.DepartmentId.HasValue).Select(m => m.DepartmentId).Distinct())
                    targets.Add(new AccessTarget(null, deptId, scopeId));
                break;
            default:
                targets.Add(AccessTarget.None);
                break;
        }
        bool Covers(EffectivePermissions p, string code) => targets.Any(t => AuthorizationGuard.Evaluate(p, code, t));

        var now = _time.GetUtcNow().UtcDateTime;

        // Gợi ý khi giao việc: quyền không tự đi theo chức vụ.
        if (scopeId.HasValue)
        {
            result.Holders = (await _positions.ListActiveHoldingsAsync(null, departmentId, partyCellId, now, ct))
                .Where(h => h.UserActive)
                .OrderBy(h => h.PositionSortOrder)
                .ThenBy(h => h.PositionName, StringComparer.CurrentCulture)
                .ThenBy(h => h.FullName, StringComparer.CurrentCulture)
                .Select(h => new PositionHolderDto { UserId = h.UserId, FullName = h.FullName, Username = h.Username, PositionName = h.PositionName })
                .ToList();
        }

        var candidateIds = await _workflow.ListActiveUserIdsWithAnyPermissionAsync(JobPermissions, now, ct);
        var effective = new List<EffectivePermissions>();
        foreach (var userId in candidateIds)
            effective.Add(await _resolver.GetAsync(userId, ct));

        var activeAssignments = (await _assignments.QueryAsync(new RoleAssignmentFilter(ActiveOn: now), ct))
            .ToDictionary(a => a.Id);
        var grantScopes = effective.SelectMany(e => e.Grants).Where(g => g.ScopeId.HasValue).ToList();
        var scopeNames = await _assignments.GetScopeNamesAsync(
            grantScopes.Where(g => g.ScopeType == ScopeType.Department).Select(g => g.ScopeId!.Value).Distinct().ToList(),
            grantScopes.Where(g => g.ScopeType == ScopeType.PartyCell).Select(g => g.ScopeId!.Value).Distinct().ToList(),
            ct);

        var roles = await _roles.GetAllRolesWithPermissionsAsync();
        var used = await UsedPermissionsAsync(ct);

        foreach (var code in JobPermissions)
        {
            var job = new PerformerJobDto
            {
                Permission = code,
                PermissionName = PermissionCodes.DisplayName(code),
                InUse = used == null || used.Contains(code)
            };

            foreach (var permissions in effective.Where(p => Covers(p, code)))
            {
                // Kiểm tra từng bản gán bằng đúng luật của guard.
                var covering = permissions.GrantsFor(code)
                    .Where(g => Covers(new EffectivePermissions(permissions.UserId, new[] { g }), code))
                    .GroupBy(g => g.SourceAssignmentId)
                    .Select(g => g.First());
                foreach (var grant in covering)
                {
                    activeAssignments.TryGetValue(grant.SourceAssignmentId, out var assignment);
                    job.People.Add(new PerformerDto
                    {
                        UserId = permissions.UserId,
                        FullName = assignment?.User?.FullName ?? string.Empty,
                        Username = assignment?.User?.Username ?? string.Empty,
                        RoleName = grant.SourceRoleName,
                        ScopeType = grant.ScopeType.ToString(),
                        ScopeId = grant.ScopeId,
                        ScopeText = grant.ScopeType == ScopeType.Global || !grant.ScopeId.HasValue
                            ? RoleAssignmentService.GlobalScopeName
                            : scopeNames.TryGetValue(grant.ScopeId.Value, out var n) ? n : "(không xác định)",
                        AssignmentId = grant.SourceAssignmentId,
                        AssignedHere = grant.ScopeType == scopeType && grant.ScopeId == scopeId,
                        ValidTo = assignment?.ValidTo
                    });
                }
            }

            job.People = job.People
                .OrderByDescending(p => p.AssignedHere)
                .ThenBy(p => p.FullName, StringComparer.CurrentCulture)
                .ThenBy(p => p.RoleName, StringComparer.CurrentCulture)
                .ToList();

            // Gán tại một đơn vị thì không chứa quyền chỉ áp dụng Toàn công ty (cùng chốt với API gán vai trò).
            job.Roles = roles
                .Where(r => r.Permissions.Any(p => p.Code == code))
                .Where(r => scopeType == ScopeType.Global
                    || r.Permissions.All(p => PermissionCodes.Find(p.Code) is not { AppliesScope: false }))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture)
                .Select(r => new PerformerRoleOptionDto { Id = r.Id, Name = r.Name, Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description })
                .ToList();

            result.Jobs.Add(job);
        }

        var allPeople = result.Jobs.SelectMany(j => j.People).ToList();
        if (allPeople.Count > 0)
        {
            var relations = await ScopeRelations.LoadAsync(_positions, allPeople.Select(p => p.UserId).ToList(), now, ct);
            foreach (var person in allPeople)
                person.PositionAtScope = relations.Describe(person.UserId, ParseScope(person.ScopeType), person.ScopeId);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<List<PerformerReviewItemDto>> GetReviewAsync(Guid? userId, CancellationToken ct = default)
    {
        EnsureCanManage();
        if (userId == Guid.Empty)
            userId = null;

        var now = _time.GetUtcNow().UtcDateTime;
        var soon = now.AddDays(ExpiringSoonDays);
        var jobRoles = (await _roles.GetAllRolesWithPermissionsAsync())
            .Where(r => r.Permissions.Any(p => JobPermissions.Contains(p.Code)))
            .Select(r => r.Id)
            .ToHashSet();

        var assignments = (await _assignments.QueryAsync(new RoleAssignmentFilter(UserId: userId, ActiveOn: now), ct))
            .Where(a => jobRoles.Contains(a.RoleId) && a.User is { IsActive: true, IsDeleted: false })
            .ToList();
        if (assignments.Count == 0)
            return new List<PerformerReviewItemDto>();

        var relations = await ScopeRelations.LoadAsync(_positions, assignments.Select(a => a.UserId).ToList(), now, ct);
        var flagged = new List<(Domain.Entities.UserRoleAssignment Assignment, string Code, string Reason)>();
        foreach (var a in assignments)
        {
            var scope = (ScopeType)(int)a.ScopeType;
            if (a.ValidTo.HasValue)
            {
                if (a.ValidTo.Value <= soon)
                    flagged.Add((a, "ExpiringSoon", $"Giao tạm, hết hạn ngày {LastDayText(a.ValidTo.Value)}"));
            }
            else if (scope != ScopeType.Global && a.ScopeId.HasValue && relations.Describe(a.UserId, scope, a.ScopeId) == null)
            {
                flagged.Add((a, "NoPositionAtScope", "Không còn giữ chức vụ hay sinh hoạt ở nơi được giao"));
            }
        }
        if (flagged.Count == 0)
            return new List<PerformerReviewItemDto>();

        var scoped = flagged.Where(f => f.Assignment.ScopeId.HasValue && f.Assignment.ScopeType != RoleScopeType.Global).ToList();
        var names = await _assignments.GetScopeNamesAsync(
            scoped.Where(f => f.Assignment.ScopeType == RoleScopeType.Department).Select(f => f.Assignment.ScopeId!.Value).Distinct().ToList(),
            scoped.Where(f => f.Assignment.ScopeType == RoleScopeType.PartyCell).Select(f => f.Assignment.ScopeId!.Value).Distinct().ToList(),
            ct);

        return flagged
            .Select(f => new PerformerReviewItemDto
            {
                AssignmentId = f.Assignment.Id,
                UserId = f.Assignment.UserId,
                FullName = f.Assignment.User?.FullName ?? string.Empty,
                Username = f.Assignment.User?.Username ?? string.Empty,
                RoleName = f.Assignment.Role?.Name ?? string.Empty,
                ScopeType = ((ScopeType)(int)f.Assignment.ScopeType).ToString(),
                ScopeId = f.Assignment.ScopeId,
                ScopeText = f.Assignment.ScopeType == RoleScopeType.Global || !f.Assignment.ScopeId.HasValue
                    ? RoleAssignmentService.GlobalScopeName
                    : names.TryGetValue(f.Assignment.ScopeId.Value, out var n) ? n : "(không xác định)",
                ValidTo = f.Assignment.ValidTo,
                ReasonCode = f.Code,
                Reason = f.Reason
            })
            .OrderBy(i => i.FullName, StringComparer.CurrentCulture)
            .ThenBy(i => i.RoleName, StringComparer.CurrentCulture)
            .ThenBy(i => i.ScopeText, StringComparer.CurrentCulture)
            .ToList();
    }

    /// <summary>Quyền mà các kỳ dự thảo/mở/khóa cần đến; null khi không có kỳ nào (coi mọi việc đều dùng). Kỳ cấu hình lỗi bỏ qua.</summary>
    private async Task<HashSet<string>?> UsedPermissionsAsync(CancellationToken ct)
    {
        var current = (await _workflow.ListPeriodsAsync(ct))
            .Where(p => p.Status is PeriodStatus.Draft or PeriodStatus.Open or PeriodStatus.Locked)
            .ToList();
        if (current.Count == 0)
            return null;

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var period in current)
        {
            PeriodSettings settings;
            try
            {
                settings = period.GetSettings();
            }
            catch (FormatException)
            {
                continue;
            }
            foreach (var profile in settings.Profiles)
            {
                foreach (var step in WorkflowSteps.Ordered)
                {
                    if (profile.PermissionFor(step) is { } permission)
                        used.Add(permission);
                }
            }
        }
        return used;
    }

    private static ScopeType ParseScope(string value)
        => Enum.TryParse<ScopeType>(value, out var scope) ? scope : ScopeType.Global;

    /// <summary>Mốc hết hạn không bao gồm → ngày cuối còn hiệu lực theo giờ Việt Nam.</summary>
    private static string LastDayText(DateTime validToExclusive)
    {
        var utc = validToExclusive.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(validToExclusive, DateTimeKind.Utc)
            : validToExclusive.ToUniversalTime();
        return (utc.AddTicks(-1) + VietnamOffset).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Chức vụ tại đơn vị hoặc đơn vị cấp trên của nó; không có thì cán bộ thuộc đơn vị đó (hoặc đơn vị con).</summary>
    private sealed class ScopeRelations
    {
        private readonly ILookup<Guid, PositionHolding> _holdings;
        private readonly IReadOnlyDictionary<Guid, MemberUnits> _units;
        private readonly OrgPaths _paths;

        private ScopeRelations(ILookup<Guid, PositionHolding> holdings, IReadOnlyDictionary<Guid, MemberUnits> units, OrgPaths paths)
        {
            _holdings = holdings;
            _units = units;
            _paths = paths;
        }

        public static async Task<ScopeRelations> LoadAsync(IPositionRepository positions, IReadOnlyCollection<Guid> userIds, DateTime now, CancellationToken ct)
        {
            var ids = userIds.Distinct().ToList();
            var holdings = await positions.ListActiveHoldingsAsync(ids, null, null, now, ct);
            var units = await positions.GetMemberUnitsAsync(ids, ct);
            var paths = await positions.GetOrgPathsAsync(ct);
            return new ScopeRelations(holdings.ToLookup(h => h.UserId), units, paths);
        }

        /// <summary>Null khi bản gán Toàn công ty hoặc không có quan hệ nào.</summary>
        public string? Describe(Guid userId, ScopeType scopeType, Guid? scopeId)
        {
            if (scopeType == ScopeType.Global || !scopeId.HasValue)
                return null;
            var isDepartment = scopeType == ScopeType.Department;
            var tree = isDepartment ? _paths.Departments : _paths.PartyCells;

            // Đơn vị không còn trong cây: chỉ chính nó.
            var chain = new HashSet<Guid> { scopeId.Value };
            if (tree.TryGetValue(scopeId.Value, out var scopePath))
            {
                foreach (var segment in scopePath.Split('/', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (Guid.TryParse(segment, out var id))
                        chain.Add(id);
                }
            }

            var names = _holdings[userId]
                .Where(h => (isDepartment ? h.DepartmentId : h.PartyCellId) is Guid unit && chain.Contains(unit))
                .OrderBy(h => h.PositionSortOrder)
                .ThenBy(h => h.PositionName, StringComparer.CurrentCulture)
                .Select(h => h.PositionName)
                .Distinct()
                .ToList();
            if (names.Count > 0)
                return string.Join(", ", names);

            if (_units.TryGetValue(userId, out var member))
            {
                var own = isDepartment ? member.DepartmentId : member.PartyCellId;
                if (own.HasValue
                    && (own.Value == scopeId.Value
                        || (tree.TryGetValue(own.Value, out var ownPath) && Organization.OrgTree.IsSelfOrDescendant(ownPath, scopeId.Value))))
                    return MemberOfUnitText;
            }
            return null;
        }
    }
}
