namespace CongTacDang.Domain.Enums;

/// <summary>
/// Loại phạm vi của một lần gán vai trò (docs/thiet-ke/phan-quyen.md mục 2).
/// Giá trị trùng với <c>CongTacDang.Application.Common.Security.ScopeType</c>.
/// </summary>
public enum RoleScopeType
{
    /// <summary>Toàn công ty (ScopeId = null).</summary>
    Global = 0,

    /// <summary>Một Phòng / đơn vị chuyên môn (ScopeId = Id Phòng).</summary>
    Department = 1,

    /// <summary>Một Chi bộ (ScopeId = Id Chi bộ).</summary>
    PartyCell = 2
}
