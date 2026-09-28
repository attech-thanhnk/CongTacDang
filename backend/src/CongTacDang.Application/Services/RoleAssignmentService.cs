using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>Triển khai <see cref="IRoleAssignmentService"/> kèm các chốt chặn quản trị (docs/thiet-ke/phan-quyen.md mục 5).</summary>
public sealed class RoleAssignmentService : IRoleAssignmentService
{
    /// <summary>Tên phạm vi toàn công ty.</summary>
    public const string GlobalScopeName = "Toàn công ty";

    private const int MaxNoteLength = 1000;

    private readonly IRoleAssignmentRepository _assignments;
    private readonly IRoleRepository _roles;
    private readonly IUserRepository _users;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccessCacheInvalidator _cache;
    private readonly IPermissionResolver _resolver;
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo dịch vụ.</summary>
    public RoleAssignmentService(
        IRoleAssignmentRepository assignments,
        IRoleRepository roles,
        IUserRepository users,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IAccessCacheInvalidator cache,
        IPermissionResolver resolver,
        TimeProvider? time = null)
    {
        _assignments = assignments;
        _roles = roles;
        _users = users;
        _guard = guard;
        _currentUser = currentUser;
        _cache = cache;
        _resolver = resolver;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <inheritdoc />
    public async Task<RoleAssignmentDto> AssignAsync(
        Guid userId,
        Guid roleId,
        ScopeType scopeType,
        Guid? scopeId,
        DateTime? validFrom,
        DateTime? validTo,
        string? note,
        CancellationToken ct = default)
    {
        EnsureCanManageAssignments();
        EnsureNotSelf(userId, "gán vai trò cho");

        var now = Now;
        var from = ToUtc(validFrom) ?? now;
        var to = ToUtc(validTo);
        ValidatePeriod(from, to);
        var trimmedNote = NormalizeNote(note);

        if (!Enum.IsDefined(scopeType))
            throw new ValidationException("Loại phạm vi không hợp lệ. Chọn Toàn công ty, Phòng hoặc Chi bộ.");
        var domainScope = (RoleScopeType)(int)scopeType;

        if (!await _assignments.UserExistsAsync(userId, ct))
            throw new ValidationException("Không tìm thấy tài khoản cần gán vai trò (tài khoản không tồn tại hoặc đã bị xóa).");

        var role = await _roles.GetRoleByIdWithPermissionsAsync(roleId)
            ?? throw new ValidationException("Không tìm thấy vai trò cần gán (vai trò không tồn tại hoặc đã bị xóa).");

        scopeId = await ValidateScopeAsync(domainScope, scopeId, ct);
        EnsureRoleFitsScope(role, domainScope);

        // Trùng bản gán (cùng người – vai trò – phạm vi) chồng lấn thời gian.
        var existing = await _assignments.GetCurrentOrFutureForUserAsync(userId, now, ct);
        var overlap = existing.FirstOrDefault(a => a.RoleId == roleId
            && a.ScopeType == domainScope
            && a.ScopeId == scopeId
            && Overlaps(a.ValidFrom, a.ValidTo, from, to));
        if (overlap != null)
        {
            throw new ValidationException(
                $"Người dùng đã có vai trò \"{role.Name}\" ở phạm vi này trong khoảng thời gian trùng lặp "
                + $"(bản gán từ {overlap.ValidFrom:dd/MM/yyyy}{(overlap.ValidTo.HasValue ? $" đến {overlap.ValidTo:dd/MM/yyyy}" : string.Empty)}). "
                + "Hãy sửa thời hạn bản gán hiện có thay vì tạo bản gán mới.");
        }

        var assignment = new UserRoleAssignment
        {
            UserId = userId,
            RoleId = roleId,
            ScopeType = domainScope,
            ScopeId = scopeId,
            ValidFrom = from,
            ValidTo = to,
            Note = trimmedNote
        };
        _assignments.Add(assignment);
        await _assignments.SaveChangesAsync(ct);
        _cache.InvalidateUser(userId);

        return await GetDtoAsync(assignment.Id, ct);
    }

    /// <inheritdoc />
    public async Task<RoleAssignmentDto> UpdateAsync(Guid assignmentId, DateTime? validFrom, DateTime? validTo, string? note, CancellationToken ct = default)
    {
        EnsureCanManageAssignments();
        var assignment = await _assignments.GetForUpdateAsync(assignmentId, ct)
            ?? throw new NotFoundException("Không tìm thấy bản gán vai trò (có thể đã bị xóa).");
        EnsureNotSelf(assignment.UserId, "sửa bản gán vai trò của");

        var from = ToUtc(validFrom) ?? assignment.ValidFrom;
        var to = ToUtc(validTo);
        ValidatePeriod(from, to);

        var now = Now;
        var existing = await _assignments.GetCurrentOrFutureForUserAsync(assignment.UserId, now, ct);
        if (existing.Any(a => a.Id != assignment.Id
                && a.RoleId == assignment.RoleId
                && a.ScopeType == assignment.ScopeType
                && a.ScopeId == assignment.ScopeId
                && Overlaps(a.ValidFrom, a.ValidTo, from, to)))
        {
            throw new ValidationException(
                "Thời hạn mới chồng lấn với một bản gán khác cùng vai trò và phạm vi của người này. Hãy chọn thời hạn khác.");
        }

        var stillEffective = from <= now && (to == null || now < to.Value);
        if (!stillEffective)
            await EnsureAdministratorsRemainAsync(rows => rows.Where(r => r.AssignmentId != assignment.Id), ct);

        assignment.ValidFrom = from;
        assignment.ValidTo = to;
        assignment.Note = NormalizeNote(note);
        await _assignments.SaveChangesAsync(ct);
        _cache.InvalidateUser(assignment.UserId);

        return await GetDtoAsync(assignment.Id, ct);
    }

    /// <inheritdoc />
    public async Task<RoleAssignmentDto> EndAsync(Guid assignmentId, CancellationToken ct = default)
    {
        EnsureCanManageAssignments();
        var assignment = await _assignments.GetForUpdateAsync(assignmentId, ct)
            ?? throw new NotFoundException("Không tìm thấy bản gán vai trò (có thể đã bị xóa).");
        EnsureNotSelf(assignment.UserId, "thu hồi vai trò của");

        var now = Now;
        if (assignment.ValidTo.HasValue && assignment.ValidTo.Value <= now)
            throw new ValidationException("Bản gán này đã hết hiệu lực trước đó.");

        await EnsureAdministratorsRemainAsync(rows => rows.Where(r => r.AssignmentId != assignment.Id), ct);

        // Bản gán chưa tới ngày hiệu lực: kết thúc = không bao giờ hiệu lực (ValidTo = ValidFrom).
        assignment.ValidTo = assignment.ValidFrom > now ? assignment.ValidFrom : now;
        await _assignments.SaveChangesAsync(ct);
        _cache.InvalidateUser(assignment.UserId);

        return await GetDtoAsync(assignment.Id, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid assignmentId, CancellationToken ct = default)
    {
        EnsureCanManageAssignments();
        var assignment = await _assignments.GetForUpdateAsync(assignmentId, ct)
            ?? throw new NotFoundException("Không tìm thấy bản gán vai trò (có thể đã bị xóa).");
        EnsureNotSelf(assignment.UserId, "xóa bản gán vai trò của");

        await EnsureAdministratorsRemainAsync(rows => rows.Where(r => r.AssignmentId != assignment.Id), ct);

        assignment.IsDeleted = true;
        assignment.DeletedAt = Now;
        assignment.DeletedBy = _currentUser.UserId;
        await _assignments.SaveChangesAsync(ct);
        _cache.InvalidateUser(assignment.UserId);
    }

    /// <inheritdoc />
    public async Task<List<RoleAssignmentDto>> QueryAsync(RoleAssignmentQuery query, CancellationToken ct = default)
    {
        EnsureCanManageAssignments();
        var rows = await _assignments.QueryAsync(new RoleAssignmentFilter(
            query.UserId,
            query.RoleId,
            query.ScopeType.HasValue ? (RoleScopeType)(int)query.ScopeType.Value : null,
            query.ScopeId,
            ToUtc(query.ActiveOn)), ct);
        return await MapAsync(rows, ct);
    }

    /// <inheritdoc />
    public async Task<UserEffectivePermissionsDto> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy tài khoản.");

        var target = new AccessTarget(user.Id, user.DepartmentId, user.PartyCellId);
        if (!_guard.Can(PermissionCodes.SystemAssignmentsManage, AccessTarget.None)
            && !_guard.Can(PermissionCodes.SystemUsersRead, target))
        {
            throw new ForbiddenException(
                $"Bạn cần quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemAssignmentsManage)}\" hoặc "
                + $"\"{PermissionCodes.DisplayName(PermissionCodes.SystemUsersRead)}\" trong phạm vi của tài khoản này để tra cứu quyền.");
        }

        var effective = await _resolver.GetAsync(userId, ct);
        var names = await ScopeNamesAsync(effective.Grants.Select(g => (g.ScopeType, g.ScopeId)), ct);

        var items = effective.Grants
            .GroupBy(g => g.Code, StringComparer.Ordinal)
            .Select(group =>
            {
                var definition = PermissionCodes.Find(group.Key);
                return new
                {
                    Order = definition == null ? int.MaxValue : IndexOf(group.Key),
                    Item = new EffectivePermissionItemDto
                    {
                        Code = group.Key,
                        Name = definition?.Name ?? group.Key,
                        Module = definition?.Module ?? string.Empty,
                        Sources = group.Select(g => new EffectiveGrantSourceDto
                        {
                            ScopeType = g.ScopeType.ToString(),
                            ScopeId = g.ScopeId,
                            ScopeName = ScopeName(g.ScopeType, g.ScopeId, names),
                            RoleName = g.SourceRoleName,
                            AssignmentId = g.SourceAssignmentId
                        }).ToList()
                    }
                };
            })
            .OrderBy(x => x.Order)
            .Select(x => x.Item)
            .ToList();

        var assignments = await _assignments.QueryAsync(new RoleAssignmentFilter(UserId: userId, ActiveOn: Now), ct);
        return new UserEffectivePermissionsDto
        {
            UserId = userId,
            IsActive = effective.IsActive,
            Permissions = items,
            Assignments = await MapAsync(assignments, ct)
        };
    }

    /// <inheritdoc />
    public async Task<List<AccessGrantDto>> GetGrantsAsync(Guid userId, CancellationToken ct = default)
    {
        var effective = await _resolver.GetAsync(userId, ct);
        var names = await ScopeNamesAsync(effective.Grants.Select(g => (g.ScopeType, g.ScopeId)), ct);
        return effective.Grants
            .Select(g => new { g.Code, g.ScopeType, g.ScopeId })
            .Distinct()
            .OrderBy(g => IndexOf(g.Code))
            .ThenBy(g => g.ScopeType)
            .Select(g => new AccessGrantDto
            {
                Code = g.Code,
                ScopeType = g.ScopeType.ToString(),
                ScopeId = g.ScopeId,
                ScopeName = ScopeName(g.ScopeType, g.ScopeId, names)
            })
            .ToList();
    }

    /// <inheritdoc />
    public Task EnsureAdministratorsRemainWithoutUserAsync(Guid userId, CancellationToken ct = default)
        => EnsureAdministratorsRemainAsync(rows => rows.Where(r => r.UserId != userId), ct);

    /// <inheritdoc />
    public async Task<List<RoleAssignmentDto>> SetGlobalRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default)
    {
        EnsureCanManageAssignments();
        EnsureNotSelf(userId, "gán vai trò cho");

        var now = Now;
        var wanted = roleIds.Where(id => id != Guid.Empty).Distinct().ToHashSet();
        var current = (await _assignments.GetCurrentOrFutureForUserAsync(userId, now, ct))
            .Where(a => a.ScopeType == RoleScopeType.Global && a.IsEffectiveAt(now))
            .ToList();

        foreach (var assignment in current.Where(a => !wanted.Contains(a.RoleId)))
            await EndAsync(assignment.Id, ct);

        var have = current.Select(a => a.RoleId).ToHashSet();
        foreach (var roleId in wanted.Where(id => !have.Contains(id)))
            await AssignAsync(userId, roleId, ScopeType.Global, null, null, null, "Gán từ màn hình quản lý cán bộ (tương thích giao diện cũ).", ct);

        return await QueryAsync(new RoleAssignmentQuery(UserId: userId, ActiveOn: Now), ct);
    }

    #region Kiểm tra

    private void EnsureCanManageAssignments()
    {
        if (!_guard.Can(PermissionCodes.SystemAssignmentsManage, AccessTarget.None))
        {
            throw new ForbiddenException(
                $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemAssignmentsManage)}\" (phạm vi Toàn công ty). "
                + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }
    }

    /// <summary>Chốt "không tự nâng quyền": không tạo/sửa/thu hồi bản gán của chính mình.</summary>
    private void EnsureNotSelf(Guid userId, string action)
    {
        if (_currentUser.UserId.HasValue && _currentUser.UserId.Value == userId)
        {
            throw new ForbiddenException(
                $"Bạn không thể tự {action} chính mình. Hãy nhờ một quản trị viên khác thực hiện.");
        }
    }

    /// <summary>Chốt "luôn còn quản trị": từ chối (409) nếu thao tác làm mất người nắm quyền quản trị cuối cùng.</summary>
    private async Task EnsureAdministratorsRemainAsync(
        Func<IEnumerable<AdministratorGrantRow>, IEnumerable<AdministratorGrantRow>> apply,
        CancellationToken ct)
    {
        var before = await _assignments.GetAdministratorGrantsAsync(Now, ct);
        if (AdministratorInvariant.IsBrokenBy(before, apply(before)))
        {
            throw new ConflictException(
                "Không thể thực hiện vì thao tác này làm mất quản trị viên cuối cùng: hệ thống sẽ không còn tài khoản đang hoạt động nào giữ quyền "
                + $"\"{PermissionCodes.DisplayName(PermissionCodes.SystemRolesManage)}\" hoặc "
                + $"\"{PermissionCodes.DisplayName(PermissionCodes.SystemAssignmentsManage)}\" (phạm vi Toàn công ty). "
                + "Hãy gán vai trò quản trị cho người khác trước.");
        }
    }

    private async Task<Guid?> ValidateScopeAsync(RoleScopeType scopeType, Guid? scopeId, CancellationToken ct)
    {
        if (scopeType == RoleScopeType.Global)
        {
            if (scopeId.HasValue && scopeId.Value != Guid.Empty)
                throw new ValidationException("Phạm vi Toàn công ty không kèm Phòng/Chi bộ. Hãy bỏ trống đối tượng phạm vi.");
            return null;
        }

        var label = scopeType == RoleScopeType.Department ? "Phòng/đơn vị" : "Chi bộ";
        if (!scopeId.HasValue || scopeId.Value == Guid.Empty)
            throw new ValidationException($"Hãy chọn {label} cho phạm vi gán.");
        if (!await _assignments.ScopeExistsAsync(scopeType, scopeId.Value, ct))
            throw new ValidationException($"{label} được chọn không tồn tại hoặc đã ngừng hoạt động.");
        return scopeId;
    }

    /// <summary>Chốt "kiểm tra phạm vi hợp lệ": quyền không áp dụng phạm vi chỉ nằm trong vai trò được gán Global.</summary>
    private static void EnsureRoleFitsScope(AppRole role, RoleScopeType scopeType)
    {
        if (scopeType == RoleScopeType.Global)
            return;

        var globalOnly = role.Permissions
            .Select(p => PermissionCodes.Find(p.Code))
            .Where(d => d != null && !d.AppliesScope)
            .Select(d => d!.Name)
            .ToList();
        if (globalOnly.Count > 0)
        {
            throw new ValidationException(
                $"Vai trò \"{role.Name}\" có quyền chỉ áp dụng cho Toàn công ty ({string.Join(", ", globalOnly)}), "
                + "nên chỉ được gán với phạm vi Toàn công ty.");
        }
    }

    private static void ValidatePeriod(DateTime from, DateTime? to)
    {
        if (to.HasValue && to.Value <= from)
            throw new ValidationException("Ngày kết thúc hiệu lực phải sau ngày bắt đầu.");
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed != null && trimmed.Length > MaxNoteLength)
            throw new ValidationException($"Ghi chú tối đa {MaxNoteLength} ký tự.");
        return trimmed;
    }

    #endregion

    #region Tiện ích

    private static bool Overlaps(DateTime aFrom, DateTime? aTo, DateTime bFrom, DateTime? bTo)
        => (aTo == null || bFrom < aTo.Value) && (bTo == null || aFrom < bTo.Value);

    /// <summary>Chuẩn hóa thời điểm về UTC (không có múi giờ → coi là UTC).</summary>
    private static DateTime? ToUtc(DateTime? value)
    {
        if (!value.HasValue)
            return null;
        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
    }

    private static int IndexOf(string code)
    {
        var index = Array.IndexOf(PermissionCodes.All, code);
        return index < 0 ? int.MaxValue : index;
    }

    private async Task<Dictionary<Guid, string>> ScopeNamesAsync(IEnumerable<(ScopeType Type, Guid? Id)> scopes, CancellationToken ct)
    {
        var list = scopes.Where(s => s.Id.HasValue).ToList();
        return await _assignments.GetScopeNamesAsync(
            list.Where(s => s.Type == ScopeType.Department).Select(s => s.Id!.Value).ToList(),
            list.Where(s => s.Type == ScopeType.PartyCell).Select(s => s.Id!.Value).ToList(),
            ct);
    }

    private static string ScopeName(ScopeType type, Guid? id, IReadOnlyDictionary<Guid, string> names)
    {
        if (type == ScopeType.Global || !id.HasValue)
            return GlobalScopeName;
        return names.TryGetValue(id.Value, out var name) ? name : "(không xác định)";
    }

    private async Task<RoleAssignmentDto> GetDtoAsync(Guid assignmentId, CancellationToken ct)
    {
        var rows = await _assignments.QueryAsync(new RoleAssignmentFilter(Id: assignmentId), ct);
        var row = rows.FirstOrDefault()
            ?? throw new NotFoundException("Không tìm thấy bản gán vai trò.");
        return (await MapAsync(new List<UserRoleAssignment> { row }, ct))[0];
    }

    private async Task<List<RoleAssignmentDto>> MapAsync(IReadOnlyCollection<UserRoleAssignment> rows, CancellationToken ct)
    {
        var names = await ScopeNamesAsync(rows.Select(r => ((ScopeType)(int)r.ScopeType, r.ScopeId)), ct);
        var now = Now;
        return rows.Select(r => new RoleAssignmentDto
        {
            Id = r.Id,
            UserId = r.UserId,
            Username = r.User?.Username ?? string.Empty,
            FullName = r.User?.FullName ?? string.Empty,
            RoleId = r.RoleId,
            RoleName = r.Role?.Name ?? string.Empty,
            ScopeType = r.ScopeType.ToString(),
            ScopeId = r.ScopeId,
            ScopeName = ScopeName((ScopeType)(int)r.ScopeType, r.ScopeId, names),
            ValidFrom = r.ValidFrom,
            ValidTo = r.ValidTo,
            Note = r.Note,
            Status = r.ValidFrom > now ? "Future" : r.ValidTo.HasValue && r.ValidTo.Value <= now ? "Expired" : "Active"
        }).ToList();
    }

    #endregion
}
