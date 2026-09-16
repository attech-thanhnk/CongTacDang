namespace CongTacDang.Application.Services;

/// <summary>Hằng số Role Code dùng trong [Authorize(Roles = ...)] và Authorization Policy</summary>
public static class AppRoles
{
    public const string CAN_BO = "CAN_BO";
    public const string BI_THU_CHI_BO = "BI_THU_CHI_BO";
    public const string BAN_THUONG_VU = "BAN_THUONG_VU";
    public const string QUAN_TRI_HE_THONG = "QUAN_TRI_HE_THONG";
}
