using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Chốt "luôn còn quản trị" (docs/thiet-ke/phan-quyen.md mục 5): luôn còn ≥ 1 tài khoản hoạt động có
/// <c>system.roles.manage</c> <b>và</b> ≥ 1 tài khoản hoạt động có <c>system.assignments.manage</c>
/// (bản gán Global đang hiệu lực).
/// </summary>
public static class AdministratorInvariant
{
    /// <summary>Hai quyền quản trị phải luôn còn người nắm giữ.</summary>
    public static readonly string[] Codes = { PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage };

    /// <summary>Tập bản gán thỏa chốt.</summary>
    public static bool Holds(IEnumerable<AdministratorGrantRow> rows)
    {
        var list = rows as IReadOnlyCollection<AdministratorGrantRow> ?? rows.ToList();
        return Codes.All(code => list.Any(row => row.PermissionCodes.Contains(code)));
    }

    /// <summary>
    /// Thao tác làm vi phạm chốt: trước thao tác chốt đang thỏa, sau thao tác không còn thỏa.
    /// (Nếu chốt vốn đã không thỏa — ví dụ CSDL mới — thao tác không bị chặn vì không phải nguyên nhân.)
    /// </summary>
    public static bool IsBrokenBy(IReadOnlyCollection<AdministratorGrantRow> before, IEnumerable<AdministratorGrantRow> after)
        => Holds(before) && !Holds(after);
}
