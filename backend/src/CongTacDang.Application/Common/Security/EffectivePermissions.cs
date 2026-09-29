using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Common.Security;

/// <summary>Loại phạm vi của một lần gán vai trò.</summary>
public enum ScopeType
{
    /// <summary>Toàn công ty.</summary>
    Global = 0,

    /// <summary>Một đơn vị chính quyền và mọi đơn vị con (ScopeId = Id đơn vị). Tên hiển thị: "Đơn vị chính quyền".</summary>
    Department = 1,

    /// <summary>Một tổ chức Đảng và mọi tổ chức con (ScopeId = Id tổ chức Đảng). Tên hiển thị: "Tổ chức Đảng".</summary>
    PartyCell = 2
}

/// <summary>
/// Một quyền hiệu lực của người dùng kèm phạm vi và nguồn cấp.
/// </summary>
/// <param name="Code">Mã quyền.</param>
/// <param name="ScopeType">Loại phạm vi.</param>
/// <param name="ScopeId">Id đơn vị được gán (nút gốc của phạm vi); null khi <see cref="ScopeType.Global"/>.</param>
/// <param name="SourceAssignmentId">Id bản gán vai trò nguồn.</param>
/// <param name="SourceRoleName">Tên vai trò cấp quyền (để tra cứu "người X làm được gì").</param>
/// <param name="CoveredScopeIds">
/// Id mọi đơn vị thuộc phạm vi: <see cref="ScopeId"/> và toàn bộ đơn vị con cháu (cây đơn vị, task 14).
/// null → chỉ <see cref="ScopeId"/>.
/// </param>
public sealed record PermissionGrant(
    string Code,
    ScopeType ScopeType,
    Guid? ScopeId,
    Guid SourceAssignmentId,
    string SourceRoleName,
    IReadOnlyCollection<Guid>? CoveredScopeIds = null)
{
    /// <summary>Các đơn vị thuộc phạm vi (gồm nút được gán); rỗng khi Global.</summary>
    public IEnumerable<Guid> CoveredIds => CoveredScopeIds
        ?? (ScopeId.HasValue ? new[] { ScopeId.Value } : Array.Empty<Guid>());

    /// <summary>Đơn vị <paramref name="unitId"/> thuộc phạm vi của bản cấp (nút được gán hoặc con cháu).</summary>
    public bool Covers(Guid? unitId)
    {
        if (!unitId.HasValue)
            return false;
        if (ScopeId == unitId)
            return true;
        return CoveredScopeIds != null && CoveredScopeIds.Contains(unitId.Value);
    }
}

/// <summary>
/// Tập quyền hiệu lực của một người dùng tại thời điểm tính (bất biến, an toàn khi dùng chung giữa các request).
/// </summary>
public sealed class EffectivePermissions
{
    private readonly HashSet<string> _codes;
    private readonly HashSet<string> _roleNames;

    /// <summary>Khởi tạo tập quyền hiệu lực của một tài khoản đang hoạt động.</summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="grants">Các quyền kèm phạm vi.</param>
    /// <param name="roleNames">Tên vai trò đang hiệu lực (chỉ để hiển thị, xem <see cref="RoleNames"/>).</param>
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

    /// <summary>Tập rỗng — người dùng không tồn tại, đã xóa hoặc bị vô hiệu hóa (<see cref="IsActive"/> = false).</summary>
    public static EffectivePermissions Empty(Guid userId) => new(userId, Array.Empty<PermissionGrant>(), null, isActive: false);

    /// <summary>Người dùng sở hữu tập quyền.</summary>
    public Guid UserId { get; }

    /// <summary>
    /// Tài khoản tồn tại, chưa xóa và đang hoạt động. false → không được thực hiện gì, kể cả quyền của chủ hồ sơ
    /// (docs/thiet-ke/phan-quyen.md mục 4, điều kiện 1).
    /// </summary>
    public bool IsActive { get; }

    /// <summary>Danh sách quyền kèm phạm vi và nguồn cấp.</summary>
    public IReadOnlyList<PermissionGrant> Grants { get; }

    /// <summary>Các mã quyền (không trùng) mà người dùng có ở ít nhất một phạm vi.</summary>
    public IReadOnlyCollection<string> Codes => _codes;

    /// <summary>
    /// Tên các vai trò từ bản gán đang hiệu lực (kể cả vai trò chưa có quyền nào). Chỉ để hiển thị
    /// (trường <c>roles</c> của phiên đăng nhập); <b>không được dùng để phân quyền</b> — dùng <see cref="Has"/>/<see cref="Grants"/>.
    /// </summary>
    public IReadOnlyCollection<string> RoleNames => _roleNames;

    /// <summary>Có quyền <paramref name="code"/> ở phạm vi bất kỳ.</summary>
    public bool Has(string code) => _codes.Contains(code);

    /// <summary>Có ít nhất một trong các quyền.</summary>
    public bool HasAny(params string[] codes) => codes.Any(_codes.Contains);

    /// <summary>Các bản cấp của một quyền (để dựng bộ lọc phạm vi).</summary>
    public IEnumerable<PermissionGrant> GrantsFor(string code) => Grants.Where(g => g.Code == code);
}
