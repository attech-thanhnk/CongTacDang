using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Điểm kiểm tra quyền duy nhất theo đối tượng (docs/thiet-ke/phan-quyen.md mục 4). Người dùng được thực hiện quyền P
/// trên đối tượng T khi:
/// <list type="number">
/// <item>tài khoản đang hoạt động (<see cref="EffectivePermissions.IsActive"/>);</item>
/// <item>có bản gán đang hiệu lực mà vai trò có P và phạm vi bao trùm T (Global → mọi đối tượng; đơn vị chính quyền d →
/// T.DepartmentId là d hoặc đơn vị con cháu của d; tổ chức Đảng c → T.PartyCellId là c hoặc con cháu của c);
/// quyền "không áp dụng phạm vi" chỉ tính bản gán Global;</item>
/// <item>luật riêng theo mã: <c>evaluation.self</c> chỉ trên hồ sơ của mình; <c>evaluation.read</c> luôn đúng với chủ hồ sơ;
/// các quyền duyệt/xác nhận/đề xuất/ghi nhận/quyết định không áp dụng trên hồ sơ của chính mình (xung đột lợi ích).</item>
/// </list>
/// Guard không xét cấp quyết định (<c>ApprovalAuthority</c>): bước nào làm trong hệ thống, bước nào do cấp trên thực hiện
/// và quyền thực hiện từng bước là cấu hình hồ sơ luồng của hồ sơ (<c>PeriodSettings.profiles</c>), service luồng kiểm tra
/// chế độ bước trước khi gọi guard.
/// Đăng ký scoped: tập quyền của người dùng hiện tại được nạp một lần mỗi request (qua <see cref="IPermissionResolver"/>, có cache).
/// </summary>
public sealed class AuthorizationGuard : IAuthorizationGuard
{
    /// <summary>
    /// Các quyền không áp dụng khi người thực hiện là chủ hồ sơ (xung đột lợi ích, HD03 tr.4).
    /// </summary>
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
        PermissionCodes.EvaluationReopen
    };

    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionResolver _resolver;
    private EffectivePermissions? _permissions;
    private bool _loaded;

    /// <summary>Khởi tạo guard cho request hiện tại.</summary>
    public AuthorizationGuard(ICurrentUserService currentUser, IPermissionResolver resolver)
    {
        _currentUser = currentUser;
        _resolver = resolver;
    }

    /// <summary>Guard trên tập quyền cho sẵn (dùng trong test hoặc khi đã nạp quyền).</summary>
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
                // Chỉ trên hồ sơ của chính mình; phạm vi gán được bỏ qua.
                return isOwner && permissions.Has(PermissionCodes.EvaluationSelf);
            case PermissionCodes.EvaluationRead when isOwner:
                // HD03: quyền được biết — chủ hồ sơ luôn xem được hồ sơ của mình.
                return true;
        }

        if (isOwner && ConflictOfInterestCodes.Contains(permission))
            return false;

        var appliesScope = PermissionCodes.Find(permission)?.AppliesScope ?? true;
        foreach (var grant in permissions.GrantsFor(permission))
        {
            switch (grant.ScopeType)
            {
                case ScopeType.Global:
                    return true;
                // Phạm vi đơn vị bao trùm cả cây con (task 14): gán ở cha → có quyền ở mọi đơn vị con cháu.
                case ScopeType.Department when appliesScope && grant.ScopeId.HasValue && grant.Covers(target.DepartmentId):
                    return true;
                case ScopeType.PartyCell when appliesScope && grant.ScopeId.HasValue && grant.Covers(target.PartyCellId):
                    return true;
            }
        }

        return false;
    }

    /// <summary>Bộ lọc phạm vi trên tập quyền cho sẵn.</summary>
    public static ScopeFilter BuildScope(EffectivePermissions permissions, string permission)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        if (!permissions.IsActive)
            return ScopeFilter.None;

        var userId = permissions.UserId;
        if (permission == PermissionCodes.EvaluationSelf)
            return permissions.Has(permission) ? ScopeFilter.OwnerOnly(userId) : ScopeFilter.None;

        var appliesScope = PermissionCodes.Find(permission)?.AppliesScope ?? true;
        var grants = permissions.GrantsFor(permission).ToList();
        var isGlobal = grants.Any(g => g.ScopeType == ScopeType.Global);
        // Danh sách Id đã mở rộng xuống cây con (resolver tính sẵn) → service dựng WHERE ... IN (...) như cũ.
        var departments = appliesScope && !isGlobal
            ? grants.Where(g => g.ScopeType == ScopeType.Department && g.ScopeId.HasValue).SelectMany(g => g.CoveredIds).Distinct().ToList()
            : new List<Guid>();
        var cells = appliesScope && !isGlobal
            ? grants.Where(g => g.ScopeType == ScopeType.PartyCell && g.ScopeId.HasValue).SelectMany(g => g.CoveredIds).Distinct().ToList()
            : new List<Guid>();

        // Chủ hồ sơ luôn xem được hồ sơ của mình (HD03: quyền được biết).
        Guid? owner = permission == PermissionCodes.EvaluationRead ? userId : null;
        return new ScopeFilter(isGlobal, departments, cells, owner);
    }

    /// <inheritdoc />
    public bool HasAny(string permission)
    {
        var permissions = Permissions();
        return permissions != null && permissions.IsActive && permissions.Has(permission);
    }

    /// <inheritdoc />
    public bool Can(string permission, AccessTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var permissions = Permissions();
        return permissions != null && Evaluate(permissions, permission, target);
    }

    /// <inheritdoc />
    public void Ensure(string permission, AccessTarget target)
    {
        if (!Can(permission, target))
            throw Forbidden(permission, target);
    }

    /// <inheritdoc />
    public ScopeFilter GetScope(string permission)
    {
        var permissions = Permissions();
        return permissions == null ? ScopeFilter.None : BuildScope(permissions, permission);
    }

    /// <summary>Lỗi 403 nêu tên quyền hiển thị và lý do (RULES 8.5).</summary>
    public ForbiddenException Forbidden(string permission, AccessTarget target)
    {
        var name = PermissionCodes.DisplayName(permission);
        var userId = _currentUser.UserId;
        var isOwner = userId.HasValue && target.OwnerId == userId;

        if (isOwner && ConflictOfInterestCodes.Contains(permission))
        {
            return new ForbiddenException(
                $"Bạn không được thực hiện \"{name}\" trên hồ sơ của chính mình (xung đột lợi ích theo Hướng dẫn 03-HD/TVĐU).");
        }

        if (permission == PermissionCodes.EvaluationSelf && !isOwner)
        {
            return new ForbiddenException(
                $"Chỉ chủ hồ sơ mới được thực hiện \"{name}\". Bạn không thể thao tác trên hồ sơ của người khác.");
        }

        return new ForbiddenException(
            $"Bạn không có quyền \"{name}\" trên đối tượng này (ngoài phạm vi được gán). "
            + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
    }

    /// <summary>Tập quyền hiệu lực của người dùng hiện tại; null nếu chưa đăng nhập.</summary>
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
