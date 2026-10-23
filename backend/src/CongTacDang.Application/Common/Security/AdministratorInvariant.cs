using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Chốt "luôn còn quản trị": mỗi quyền trong <see cref="Codes"/> luôn có ≥ 1 tài khoản hoạt động
/// nắm giữ qua bản gán Global đang hiệu lực.
/// </summary>
public static class AdministratorInvariant
{
    public static readonly string[] Codes = { PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage };

    public static bool Holds(IEnumerable<AdministratorGrantRow> rows)
    {
        var list = rows as IReadOnlyCollection<AdministratorGrantRow> ?? rows.ToList();
        return Codes.All(code => list.Any(row => row.PermissionCodes.Contains(code)));
    }

    /// <summary>Chỉ chặn khi chốt đang thỏa mà thao tác làm mất; chốt vốn không thỏa (CSDL mới) thì không chặn.</summary>
    public static bool IsBrokenBy(IReadOnlyCollection<AdministratorGrantRow> before, IEnumerable<AdministratorGrantRow> after)
        => Holds(before) && !Holds(after);
}
