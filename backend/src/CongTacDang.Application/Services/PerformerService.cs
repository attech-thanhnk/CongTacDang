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

/// <summary>
/// Màn hình "Người thực hiện": ai làm được việc gì của quy trình đánh giá tại một đơn vị
/// (docs/thiet-ke/phan-quyen.md mục 5). Chỉ đọc — thêm/gỡ người dùng API gán vai trò sẵn có.
/// </summary>
public interface IPerformerService
{
    /// <summary>
    /// Các việc trong quy trình đánh giá và người làm được từng việc tại đơn vị chính quyền <paramref name="departmentId"/>,
    /// tổ chức Đảng <paramref name="partyCellId"/>, hoặc Toàn công ty (cả hai null).
    /// </summary>
    Task<UnitPerformersDto> GetAsync(Guid? departmentId, Guid? partyCellId, CancellationToken ct = default);
}

/// <summary>
/// Triển khai <see cref="IPerformerService"/>. Quyền hiệu lực tính bằng <see cref="IPermissionResolver"/> và kiểm tra bằng
/// <see cref="AuthorizationGuard.Evaluate"/> — cùng luật với kiểm tra thiếu người thực hiện của kỳ.
/// </summary>
public sealed class PerformerService : IPerformerService
{
    /// <summary>
    /// Các việc của quy trình (mã quyền thực hiện bước), theo thứ tự các bước; bước của chủ hồ sơ không thuộc màn hình này.
    /// </summary>
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
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo dịch vụ.</summary>
    public PerformerService(
        IEvaluationWorkflowRepository workflow,
        IRoleAssignmentRepository assignments,
        IRoleRepository roles,
        IPermissionResolver resolver,
        IAuthorizationGuard guard,
        TimeProvider? time = null)
    {
        _workflow = workflow;
        _assignments = assignments;
        _roles = roles;
        _resolver = resolver;
        _guard = guard;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<UnitPerformersDto> GetAsync(Guid? departmentId, Guid? partyCellId, CancellationToken ct = default)
    {
        if (!_guard.Can(PermissionCodes.SystemAssignmentsManage, AccessTarget.None))
        {
            throw new ForbiddenException(
                $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemAssignmentsManage)}\" (phạm vi Toàn công ty). "
                + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }

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

        // Đối tượng kiểm tra: "một hồ sơ bất kỳ thuộc đơn vị này" (không có chủ hồ sơ → không xét xung đột lợi ích).
        // Hồ sơ mang cả Phòng lẫn Chi bộ của cán bộ: xem theo Phòng thì tính cả người giao ở Chi bộ của cán bộ trong Phòng
        // (và ngược lại), để việc giao theo cây kia không hiện là "Chưa có người".
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

        // Ứng viên: tài khoản đang hoạt động có bản gán hiệu lực mà vai trò chứa một trong các quyền; quyền + phạm vi tính bằng resolver.
        var now = _time.GetUtcNow().UtcDateTime;
        var candidateIds = await _workflow.ListActiveUserIdsWithAnyPermissionAsync(JobPermissions, now, ct);
        var effective = new List<EffectivePermissions>();
        foreach (var userId in candidateIds)
            effective.Add(await _resolver.GetAsync(userId, ct));

        // Họ tên, tên đăng nhập, thời hạn theo bản gán đang hiệu lực.
        var activeAssignments = (await _assignments.QueryAsync(new RoleAssignmentFilter(ActiveOn: now), ct))
            .ToDictionary(a => a.Id);
        var grantScopes = effective.SelectMany(e => e.Grants).Where(g => g.ScopeId.HasValue).ToList();
        var scopeNames = await _assignments.GetScopeNamesAsync(
            grantScopes.Where(g => g.ScopeType == ScopeType.Department).Select(g => g.ScopeId!.Value).Distinct().ToList(),
            grantScopes.Where(g => g.ScopeType == ScopeType.PartyCell).Select(g => g.ScopeId!.Value).Distinct().ToList(),
            ct);

        var roles = await _roles.GetAllRolesWithPermissionsAsync();

        foreach (var code in JobPermissions)
        {
            var job = new PerformerJobDto { Permission = code, PermissionName = PermissionCodes.DisplayName(code) };

            foreach (var permissions in effective.Where(p => Covers(p, code)))
            {
                // Bản gán nào bao trùm đơn vị: kiểm tra từng bản cấp riêng bằng đúng luật của guard.
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

            // Vai trò chọn được khi thêm người: có quyền này; gán tại một đơn vị thì không chứa quyền chỉ áp dụng Toàn công ty
            // (cùng chốt với API gán vai trò).
            job.Roles = roles
                .Where(r => r.Permissions.Any(p => p.Code == code))
                .Where(r => scopeType == ScopeType.Global
                    || r.Permissions.All(p => PermissionCodes.Find(p.Code) is not { AppliesScope: false }))
                .OrderBy(r => r.Name, StringComparer.CurrentCulture)
                .Select(r => new PerformerRoleOptionDto { Id = r.Id, Name = r.Name, Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description })
                .ToList();

            result.Jobs.Add(job);
        }

        return result;
    }
}
