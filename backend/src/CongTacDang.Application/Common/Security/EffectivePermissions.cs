using System;
using System.Collections.Generic;
using System.Linq;

namespace CongTacDang.Application.Common.Security;

/// <summary>Loại phạm vi của một lần gán vai trò.</summary>
public enum ScopeType
{
    /// <summary>Toàn công ty.</summary>
    Global = 0,

    /// <summary>Một Phòng / đơn vị chuyên môn (ScopeId = Id Phòng).</summary>
    Department = 1,

    /// <summary>Một Chi bộ (ScopeId = Id Chi bộ).</summary>
    PartyCell = 2
}

/// <summary>
/// Một quyền hiệu lực của người dùng kèm phạm vi và nguồn cấp.
/// </summary>
/// <param name="Code">Mã quyền.</param>
/// <param name="ScopeType">Loại phạm vi.</param>
/// <param name="ScopeId">Id Phòng/Chi bộ; null khi <see cref="ScopeType.Global"/>.</param>
/// <param name="SourceAssignmentId">Id bản gán vai trò nguồn; <see cref="Guid.Empty"/> ở mô hình cũ (user ↔ role nhiều-nhiều).</param>
/// <param name="SourceRoleName">Tên vai trò cấp quyền (để tra cứu "người X làm được gì").</param>
public sealed record PermissionGrant(string Code, ScopeType ScopeType, Guid? ScopeId, Guid SourceAssignmentId, string SourceRoleName);

/// <summary>
/// Tập quyền hiệu lực của một người dùng tại thời điểm tính (bất biến, an toàn khi dùng chung giữa các request).
/// </summary>
public sealed class EffectivePermissions
{
    private readonly HashSet<string> _codes;
    private readonly HashSet<string> _legacyRoleCodes;

    /// <summary>Khởi tạo tập quyền hiệu lực.</summary>
    /// <param name="userId">Người dùng.</param>
    /// <param name="grants">Các quyền kèm phạm vi.</param>
    /// <param name="legacyRoleCodes">Mã vai trò theo mô hình cũ (chỉ dùng cho policy chuyển tiếp, xem <see cref="LegacyRoleCodes"/>).</param>
    public EffectivePermissions(Guid userId, IEnumerable<PermissionGrant> grants, IEnumerable<string>? legacyRoleCodes = null)
    {
        UserId = userId;
        Grants = grants.ToList().AsReadOnly();
        _codes = new HashSet<string>(Grants.Select(g => g.Code), StringComparer.Ordinal);
        _legacyRoleCodes = new HashSet<string>(legacyRoleCodes ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    /// <summary>Tập rỗng — người dùng không tồn tại, đã xóa hoặc bị vô hiệu hóa.</summary>
    public static EffectivePermissions Empty(Guid userId) => new(userId, Array.Empty<PermissionGrant>());

    /// <summary>Người dùng sở hữu tập quyền.</summary>
    public Guid UserId { get; }

    /// <summary>Danh sách quyền kèm phạm vi và nguồn cấp.</summary>
    public IReadOnlyList<PermissionGrant> Grants { get; }

    /// <summary>Các mã quyền (không trùng) mà người dùng có ở ít nhất một phạm vi.</summary>
    public IReadOnlyCollection<string> Codes => _codes;

    /// <summary>
    /// Mã vai trò theo mô hình cũ. <b>Chỉ</b> dùng để giữ nguyên kết quả các policy composite/role cũ trong giai đoạn chuyển tiếp;
    /// sẽ bị bỏ ở task 09 (code không được biết tên vai trò).
    /// </summary>
    public IReadOnlyCollection<string> LegacyRoleCodes => _legacyRoleCodes;

    /// <summary>Có quyền <paramref name="code"/> ở phạm vi bất kỳ.</summary>
    public bool Has(string code) => _codes.Contains(code);

    /// <summary>Có ít nhất một trong các quyền.</summary>
    public bool HasAny(params string[] codes) => codes.Any(_codes.Contains);

    /// <summary>Các bản cấp của một quyền (để dựng bộ lọc phạm vi).</summary>
    public IEnumerable<PermissionGrant> GrantsFor(string code) => Grants.Where(g => g.Code == code);

    /// <summary>Có vai trò theo mã cũ (chỉ dùng cho policy chuyển tiếp — xem <see cref="LegacyRoleCodes"/>).</summary>
    public bool HasLegacyRole(string roleCode) => _legacyRoleCodes.Contains(roleCode);
}
