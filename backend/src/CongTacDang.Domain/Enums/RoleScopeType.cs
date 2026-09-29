namespace CongTacDang.Domain.Enums;

/// <summary>
/// Loại phạm vi của một lần gán vai trò (docs/thiet-ke/phan-quyen.md mục 2).
/// Giá trị trùng với <c>CongTacDang.Application.Common.Security.ScopeType</c>.
/// </summary>
public enum RoleScopeType
{
    /// <summary>Toàn công ty (ScopeId = null).</summary>
    Global = 0,

    /// <summary>Một đơn vị chính quyền và mọi đơn vị con (ScopeId = Id đơn vị).</summary>
    Department = 1,

    /// <summary>Một tổ chức Đảng và mọi tổ chức con (ScopeId = Id tổ chức Đảng).</summary>
    PartyCell = 2
}
