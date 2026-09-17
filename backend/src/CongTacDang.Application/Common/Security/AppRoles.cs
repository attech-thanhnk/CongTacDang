namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Hằng số Mã vai trò chuẩn của hệ thống Công tác Đảng ATTECH
/// </summary>
public static class AppRoles
{
    /// <summary>Cán bộ, Đảng viên cơ sở</summary>
    public const string CAN_BO = "CAN_BO";

    /// <summary>Bí thư / Phó Bí thư Chi bộ</summary>
    public const string BI_THU_CHI_BO = "BI_THU_CHI_BO";

    /// <summary>Tổ Thẩm định Đảng ủy</summary>
    public const string TO_THAM_DINH = "TO_THAM_DINH";

    /// <summary>Ủy viên Ban Thường vụ Đảng ủy</summary>
    public const string BAN_THUONG_VU = "BAN_THUONG_VU";

    /// <summary>Quản trị hệ thống toàn quyền</summary>
    public const string QUAN_TRI_HE_THONG = "QUAN_TRI_HE_THONG";

    /// <summary>Danh sách tất cả các mã vai trò trong hệ thống</summary>
    public static readonly string[] All =
    {
        CAN_BO,
        BI_THU_CHI_BO,
        TO_THAM_DINH,
        BAN_THUONG_VU,
        QUAN_TRI_HE_THONG
    };
}
