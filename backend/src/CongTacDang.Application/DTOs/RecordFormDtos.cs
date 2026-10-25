using System;

namespace CongTacDang.Application.DTOs;

/// <summary>Biểu mẫu cá nhân xuất được cho hồ sơ.</summary>
public class RecordFormDto
{
    /// <summary>01, 02, 04, 05, 09A, 09C, 9D, 10.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Đề nghị thay đổi việc của Mẫu 04/05; null với các mẫu còn lại.</summary>
    public Guid? RequestId { get; set; }
}
