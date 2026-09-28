namespace CongTacDang.Domain.Enums;

/// <summary>
/// Cấp có thẩm quyền quyết định kết quả đánh giá, xếp loại của cán bộ.
/// </summary>
public enum ApprovalAuthority
{
    /// <summary>Đảng ủy cơ sở (Đảng ủy ATTECH) quyết định.</summary>
    CoSo = 1,

    /// <summary>Cấp trên (Ban Thường vụ Đảng ủy Tổng công ty) quyết định.</summary>
    CapTren = 2
}
