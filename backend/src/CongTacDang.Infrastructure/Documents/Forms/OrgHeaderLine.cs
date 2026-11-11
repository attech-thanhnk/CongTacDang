namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Dòng 1 tiêu đề trái biểu mẫu cá nhân: cấp trên trực tiếp của đơn vị/tổ chức hồ sơ (cây chính quyền hoặc cây Đảng).</summary>
public static class OrgHeaderLine
{
    /// <summary>In hoa, một dòng; cấp trên là gốc cây hoặc không có → null để mẫu dùng tên gốc (công ty / Đảng bộ) như trước.</summary>
    public static string? DirectParent(string? parentName, bool parentIsRoot) => parentIsRoot ? null : PartyHeader.Upper(parentName);
}
