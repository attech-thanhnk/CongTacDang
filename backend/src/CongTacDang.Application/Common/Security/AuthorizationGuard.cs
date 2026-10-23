using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Không xét cấp quyết định hay chế độ bước — service luồng kiểm tra hồ sơ luồng trước khi gọi guard.
/// Scoped: tập quyền nạp một lần mỗi request.
/// </summary>
public sealed class AuthorizationGuard : IAuthorizationGuard
{
    /// <summary>Không áp dụng khi người thực hiện là chủ hồ sơ (xung đột lợi ích).</summary>
    public static readonly IReadOnlySet<string> ConflictOfInterestCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        PermissionCodes.EvaluationTasksApprove,
        PermissionCodes.EvaluationCellConfirm,
        PermissionCodes.EvaluationCollectiveRecord,
        PermissionCodes.EvaluationAppraise,
        PermissionCodes.EvaluationDirectorReview,
        PermissionCodes.EvaluationUnitReview,
        PermissionCodes.EvaluationDecide,
        PermissionCodes.EvaluationExternalRecord,
        PermissionCodes.EvaluationPublish,
        PermissionCodes.EvaluationReopen,
        PermissionCodes.EvaluationAppealResolve,
        PermissionCodes.EvaluationImprovementManage
    };

    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionResolver _resolver;
    private EffectivePermissions? _permissions;
    private bool _loaded;

    public AuthorizationGuard(ICurrentUserService currentUser, IPermissionResolver resolver)
    {
        _currentUser = currentUser;
        _resolver = resolver;
    }

    /// <summary>Kiểm tra trên tập quyền cho sẵn.</summary>
    public static bool Evaluate(EffectivePermissions permissions, string permission, AccessTarget target)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(target);
        if (!permissions.IsActive)
            return false;

        var userId = permissions.UserId;
        var isOwner = target.OwnerId.HasValue && target.OwnerId.Value == userId;

        switch (permission)
        {
            case PermissionCodes.EvaluationSelf:
            case PermissionCodes.EvaluationAppealSubmit:
                // Chỉ trên hồ sơ của chính mình; phạm vi gán được bỏ qua.
                return isOwner && permissions.Has(permission);
            case PermissionCodes.EvaluationRead when isOwner:
                // Chủ hồ sơ luôn xem được hồ sơ của mình.
                return true;
        }

        if (isOwner && ConflictOfInterestCodes.Contains(permission))
            return false;

        var appliesScope = PermissionCodes.Find(permission)?.AppliesScope ?? true;
        if (target.RequireAllUnits)
            return EvaluateAllUnits(permissions, permission, target, appliesScope);

        foreach (var grant in permissions.GrantsFor(permission))
        {
            switch (grant.ScopeType)
            {
                case ScopeType.Global:
                    return true;
                // Gán ở cha → có quyền ở mọi đơn vị con cháu.
                case ScopeType.Department when appliesScope && grant.ScopeId.HasValue && grant.Covers(target.DepartmentId):
                    return true;
                case ScopeType.PartyCell when appliesScope && grant.ScopeId.HasValue && grant.Covers(target.PartyCellId):
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Chế độ ghi: mỗi đơn vị khác null phải được một bản gán bao trùm (có thể hai bản gán khác nhau);
    /// đối tượng không gắn đơn vị chỉ phạm vi Global làm được.
    /// </summary>
    private static bool EvaluateAllUnits(EffectivePermissions permissions, string permission, AccessTarget target, bool appliesScope)
    {
        var grants = permissions.GrantsFor(permission).ToList();
        if (grants.Any(g => g.ScopeType == ScopeType.Global))
            return true;
        if (!appliesScope || (!target.DepartmentId.HasValue && !target.PartyCellId.HasValue))
            return false;

        var departmentCovered = !target.DepartmentId.HasValue
            || grants.Any(g => g.ScopeType == ScopeType.Department && g.ScopeId.HasValue && g.Covers(target.DepartmentId));
        var cellCovered = !target.PartyCellId.HasValue
            || grants.Any(g => g.ScopeType == ScopeType.PartyCell && g.ScopeId.HasValue && g.Covers(target.PartyCellId));
        return departmentCovered && cellCovered;
    }

    public static ScopeFilter BuildScope(EffectivePermissions permissions, string permission)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (!permissions.IsActive)
            return ScopeFilter.None;

        var userId = permissions.UserId;
        if (permission is PermissionCodes.EvaluationSelf or PermissionCodes.EvaluationAppealSubmit)
            return permissions.Has(permission) ? ScopeFilter.OwnerOnly(userId) : ScopeFilter.None;

        var appliesScope = PermissionCodes.Find(permission)?.AppliesScope ?? true;
        var grants = permissions.GrantsFor(permission).ToList();
        var isGlobal = grants.Any(g => g.ScopeType == ScopeType.Global);
        // Id đã được resolver mở rộng xuống cây con.
        var departments = appliesScope && !isGlobal
            ? grants.Where(g => g.ScopeType == ScopeType.Department && g.ScopeId.HasValue).SelectMany(g => g.CoveredIds).Distinct().ToList()
            : new List<Guid>();
        var cells = appliesScope && !isGlobal
            ? grants.Where(g => g.ScopeType == ScopeType.PartyCell && g.ScopeId.HasValue).SelectMany(g => g.CoveredIds).Distinct().ToList()
            : new List<Guid>();

        // Chủ hồ sơ luôn xem được hồ sơ của mình.
        Guid? owner = permission == PermissionCodes.EvaluationRead ? userId : null;
        return new ScopeFilter(isGlobal, departments, cells, owner);
    }

    public bool HasAny(string permission)
    {
        var permissions = Permissions();
        return permissions != null && permissions.IsActive && permissions.Has(permission);
    }

    public bool Can(string permission, AccessTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var permissions = Permissions();
        return permissions != null && Evaluate(permissions, permission, target);
    }

    public void Ensure(string permission, AccessTarget target)
    {
        if (!Can(permission, target))
            throw Forbidden(permission, target);
    }

    public ScopeFilter GetScope(string permission)
    {
        var permissions = Permissions();
        return permissions == null ? ScopeFilter.None : BuildScope(permissions, permission);
    }

    /// <summary>Lỗi 403 nêu tên quyền hiển thị và lý do.</summary>
    public ForbiddenException Forbidden(string permission, AccessTarget target)
    {
        var name = PermissionCodes.DisplayName(permission);
        var userId = _currentUser.UserId;
        var isOwner = userId.HasValue && target.OwnerId == userId;

        if (isOwner && ConflictOfInterestCodes.Contains(permission))
        {
            return new ForbiddenException(
                $"Bạn không được thực hiện \"{name}\" trên hồ sơ của chính mình (xung đột lợi ích).");
        }

        if ((permission is PermissionCodes.EvaluationSelf or PermissionCodes.EvaluationAppealSubmit) && !isOwner)
        {
            return new ForbiddenException(
                $"Chỉ chủ hồ sơ mới được thực hiện \"{name}\". Bạn không thể thao tác trên hồ sơ của người khác.");
        }

        if (target.RequireAllUnits && target.DepartmentId.HasValue && target.PartyCellId.HasValue)
        {
            return new ForbiddenException(
                $"Bạn cần quyền \"{name}\" trên cả đơn vị chính quyền và tổ chức Đảng đã chọn; ít nhất một nơi nằm ngoài phạm vi được giao. "
                + "Hãy chọn đơn vị trong phạm vi hoặc liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }

        return new ForbiddenException(
            $"Bạn không có quyền \"{name}\" trên đối tượng này (ngoài phạm vi được gán). "
            + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
    }

    /// <summary>Null nếu chưa đăng nhập.</summary>
    private EffectivePermissions? Permissions()
    {
        if (_loaded)
            return _permissions;

        var userId = _currentUser.UserId;
        // Interface guard đồng bộ theo thiết kế; request có [RequirePermission] đã nạp sẵn cache nên thường không chờ I/O.
        _permissions = userId == null ? null : _resolver.GetAsync(userId.Value).GetAwaiter().GetResult();
        _loaded = true;
        return _permissions;
    }
}
