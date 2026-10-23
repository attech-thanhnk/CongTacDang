namespace CongTacDang.Application.DTOs;

/// <summary>Biểu mẫu cá nhân xuất được cho hồ sơ.</summary>
public class RecordFormDto
{
    /// <summary>01, 02, 09A, 09C, 9D, 10.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
