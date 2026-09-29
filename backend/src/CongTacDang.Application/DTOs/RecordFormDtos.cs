namespace CongTacDang.Application.DTOs;

/// <summary>Một biểu mẫu cá nhân xuất được cho hồ sơ đánh giá (task 18 — T-83).</summary>
public class RecordFormDto
{
    /// <summary>Mã biểu mẫu (01, 02, 09A, 09B, 09C, 9D, 10) — dùng ở <c>api/reports/docx/record/{recordId}/{formCode}</c>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; set; } = string.Empty;
}
