using System;
using System.Linq;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Triển khai v0 của <see cref="IAuthorizationGuard"/> (adapter, task 07):
/// <list type="bullet">
/// <item><see cref="HasAny"/>: dùng <see cref="IPermissionResolver"/>; mã mới được coi là có nếu người dùng có mã mới đó
/// hoặc một mã cũ ánh xạ sang nó (<see cref="LegacyPermissionMap"/>).</item>
/// <item><see cref="Can"/>/<see cref="Ensure"/>: ánh xạ sang <see cref="IAccessPolicy"/> hiện có cho các mã có tương đương;
/// mã chưa có tương đương → <see cref="HasAny"/> (mọi quyền ở v0 đều phạm vi Global).</item>
/// <item><see cref="GetScope"/>: Global nếu có quyền; <c>evaluation.read</c>/<c>evaluation.self</c> luôn kèm chủ hồ sơ.</item>
/// </list>
/// Chưa service nào dùng guard ở task 07; task 09 thay bằng bản tính theo bản gán có phạm vi.
/// Đăng ký scoped (mỗi request); dữ liệu người dùng được nạp một lần và dùng lại trong request.
/// </summary>
public sealed class LegacyAuthorizationGuard : IAuthorizationGuard
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPermissionResolver _resolver;
    private readonly IUserRepository _users;
    private readonly IAccessPolicy _accessPolicy;
    private EffectivePermissions? _permissions;
    private PartyMemberProfile? _user;
    private bool _userLoaded;

    /// <summary>Khởi tạo guard cho request hiện tại.</summary>
    public LegacyAuthorizationGuard(
        ICurrentUserService currentUser,
        IPermissionResolver resolver,
        IUserRepository users,
        IAccessPolicy accessPolicy)
    {
        _currentUser = currentUser;
        _resolver = resolver;
        _users = users;
        _accessPolicy = accessPolicy;
    }

    /// <inheritdoc />
    public bool HasAny(string permission)
    {
        var permissions = Permissions();
        if (permissions == null)
            return false;

        return permissions.Has(permission)
            || LegacyPermissionMap.LegacyCodesFor(permission).Any(permissions.Has);
    }

    /// <inheritdoc />
    public bool Can(string permission, AccessTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Permissions() == null)
            return false;

        switch (permission)
        {
            case PermissionCodes.EvaluationRead:
                return WithUser(user => _accessPolicy.CanAccessRecord(user, RecordOf(target), AccessOperation.Read));
            case PermissionCodes.EvaluationSelf:
                return WithUser(user => _accessPolicy.CanAccessRecord(user, RecordOf(target), AccessOperation.Update));
            case PermissionCodes.EvaluationCellConfirm:
                return WithUser(user => _accessPolicy.CanAccessRecord(user, RecordOf(target), AccessOperation.BranchReview));
            case PermissionCodes.EvaluationDecide:
                return target.ApprovalAuthority == ApprovalAuthority.CoSo
                    && WithUser(user => _accessPolicy.CanAccessRecord(user, RecordOf(target), AccessOperation.Approve));
            case PermissionCodes.EvaluationDecideExternal:
                return target.ApprovalAuthority == ApprovalAuthority.CapTren
                    && WithUser(user => _accessPolicy.CanAccessRecord(user, RecordOf(target), AccessOperation.Approve));
            case PermissionCodes.CollectiveManage:
                return WithUser(user => _accessPolicy.CanAccessCollective(user, target.PartyCellId, target.DepartmentId, AccessOperation.Update));
            case PermissionCodes.MeetingRead:
                return WithUser(user => _accessPolicy.CanAccessMeeting(user, target.PartyCellId, AccessOperation.Read));
            case PermissionCodes.MeetingManage:
                return WithUser(user => _accessPolicy.CanAccessMeeting(user, target.PartyCellId, AccessOperation.Update));
            case PermissionCodes.ReportExport:
                return WithUser(user => _accessPolicy.CanAccessBranch(user, target.PartyCellId, AccessOperation.Export));
            case PermissionCodes.SystemUsersRead when target.OwnerId.HasValue:
                return WithUser(user => _accessPolicy.CanAccessProfile(user, new PartyMemberProfile { Id = target.OwnerId.Value }, AccessOperation.Read));
            case PermissionCodes.SystemUsersManage when target.OwnerId.HasValue:
                return WithUser(user => _accessPolicy.CanAccessProfile(user, new PartyMemberProfile { Id = target.OwnerId.Value }, AccessOperation.Update));
            default:
                return HasAny(permission);
        }
    }

    /// <inheritdoc />
    public void Ensure(string permission, AccessTarget target)
    {
        if (!Can(permission, target))
        {
            throw new ForbiddenException(
                $"Bạn không có quyền \"{PermissionCodes.DisplayName(permission)}\" trên đối tượng này. "
                + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }
    }

    /// <inheritdoc />
    public ScopeFilter GetScope(string permission)
    {
        var userId = _currentUser.UserId;
        if (userId == null || Permissions() == null)
            return ScopeFilter.None;

        if (permission == PermissionCodes.EvaluationSelf)
            return HasAny(permission) ? ScopeFilter.OwnerOnly(userId.Value) : ScopeFilter.None;

        if (HasAny(permission))
        {
            return permission == PermissionCodes.EvaluationRead
                ? ScopeFilter.Global with { OwnerId = userId.Value }
                : ScopeFilter.Global;
        }

        // Chủ hồ sơ luôn xem được hồ sơ của mình (HD03: quyền được biết).
        return permission == PermissionCodes.EvaluationRead
            ? ScopeFilter.OwnerOnly(userId.Value)
            : ScopeFilter.None;
    }

    /// <summary>Tập quyền hiệu lực của người dùng hiện tại; null nếu chưa đăng nhập.</summary>
    private EffectivePermissions? Permissions()
    {
        if (_permissions != null)
            return _permissions;

        var userId = _currentUser.UserId;
        if (userId == null)
            return null;

        // Interface guard đồng bộ theo thiết kế; request có [Authorize(Policy)] đã nạp sẵn cache nên thường không chờ I/O.
        _permissions = _resolver.GetAsync(userId.Value).GetAwaiter().GetResult();
        return _permissions;
    }

    /// <summary>Đánh giá luật cũ trên người dùng hiện tại (nạp kèm Roles và Permissions); false nếu không nạp được.</summary>
    private bool WithUser(Func<PartyMemberProfile, bool> rule)
    {
        if (!_userLoaded)
        {
            var userId = _currentUser.UserId;
            _user = userId == null ? null : _users.GetWithRolesAndPermissionsByIdAsync(userId.Value).GetAwaiter().GetResult();
            _userLoaded = true;
        }

        return _user != null && _user.IsActive && rule(_user);
    }

    /// <summary>Dựng hồ sơ đánh giá tạm từ <see cref="AccessTarget"/> để gọi luật cũ của <see cref="IAccessPolicy"/>.</summary>
    private static EvaluationRecord RecordOf(AccessTarget target)
    {
        var ownerId = target.OwnerId ?? Guid.Empty;
        var authority = target.ApprovalAuthority ?? ApprovalAuthority.CoSo;
        return new EvaluationRecord
        {
            MemberId = ownerId,
            PartyCellId = target.PartyCellId,
            DepartmentId = target.DepartmentId,
            ApprovalAuthority = authority,
            Member = new PartyMemberProfile
            {
                Id = ownerId,
                PartyCellId = target.PartyCellId,
                DepartmentId = target.DepartmentId,
                ApprovalAuthority = authority
            }
        };
    }
}
