namespace CongTacDang.Domain.Enums;

/// <summary>Phải trùng giá trị với <c>Application.Common.Security.ScopeType</c>.</summary>
public enum RoleScopeType
{
    /// <summary>ScopeId = null.</summary>
    Global = 0,

    /// <summary>Đơn vị và mọi đơn vị con.</summary>
    Department = 1,

    /// <summary>Tổ chức Đảng và mọi tổ chức con.</summary>
    PartyCell = 2
}
