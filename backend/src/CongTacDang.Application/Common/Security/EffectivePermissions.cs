using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Common.Security;

public enum ScopeType
{
    Global = 0,

    /// <summary>Một đơn vị chính quyền và mọi đơn vị con.</summary>
    Department = 1,

    /// <summary>Một tổ chức Đảng và mọi tổ chức con.</summary>
    PartyCell = 2
}

/// <summary>Một quyền hiệu lực của người dùng kèm phạm vi và nguồn cấp.</summary>
/// <param name="ScopeId">Nút gốc của phạm vi; null khi <see cref="ScopeType.Global"/>.</param>
/// <param name="CoveredScopeIds"><see cref="ScopeId"/> và mọi đơn vị con cháu; null → chỉ <see cref="ScopeId"/>.</param>
public sealed record PermissionGrant(
    string Code,
    ScopeType ScopeType,
    Guid? ScopeId,
    Guid SourceAssignmentId,
    string SourceRoleName,
    IReadOnlyCollection<Guid>? CoveredScopeIds = null)
{
    /// <summary>Gồm nút được gán; rỗng khi Global.</summary>
    public IEnumerable<Guid> CoveredIds => CoveredScopeIds
        ?? (ScopeId.HasValue ? new[] { ScopeId.Value } : Array.Empty<Guid>());

    public bool Covers(Guid? unitId)
    {
        if (!unitId.HasValue)
            return false;
        if (ScopeId == unitId)
            return true;
        return CoveredScopeIds != null && CoveredScopeIds.Contains(unitId.Value);
    }
}

/// <summary>Bất biến, an toàn khi dùng chung giữa các request.</summary>
public sealed class EffectivePermissions
{
    private readonly HashSet<string> _codes;
    private readonly HashSet<string> _roleNames;

    public EffectivePermissions(Guid userId, IEnumerable<PermissionGrant> grants, IEnumerable<string>? roleNames = null)
        : this(userId, grants, roleNames, isActive: true)
    {
    }

    private EffectivePermissions(Guid userId, IEnumerable<PermissionGrant> grants, IEnumerable<string>? roleNames, bool isActive)
    {
        UserId = userId;
        IsActive = isActive;
        Grants = grants.ToList().AsReadOnly();
        _codes = new HashSet<string>(Grants.Select(g => g.Code), StringComparer.Ordinal);
        _roleNames = new HashSet<string>(roleNames ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    /// <summary>Người dùng không tồn tại, đã xóa hoặc bị vô hiệu hóa.</summary>
    public static EffectivePermissions Empty(Guid userId) => new(userId, Array.Empty<PermissionGrant>(), null, isActive: false);

    public Guid UserId { get; }

    /// <summary>false → không được làm gì, kể cả quyền của chủ hồ sơ.</summary>
    public bool IsActive { get; }

    public IReadOnlyList<PermissionGrant> Grants { get; }

    public IReadOnlyCollection<string> Codes => _codes;

    /// <summary>Chỉ để hiển thị; <b>không dùng để phân quyền</b>.</summary>
    public IReadOnlyCollection<string> RoleNames => _roleNames;

    /// <summary>Có quyền ở phạm vi bất kỳ.</summary>
    public bool Has(string code) => _codes.Contains(code);

    public bool HasAny(params string[] codes) => codes.Any(_codes.Contains);

    public IEnumerable<PermissionGrant> GrantsFor(string code) => Grants.Where(g => g.Code == code);
}
