using System;

namespace CongTacDang.Application.DTOs;

public class HolidayDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Thêm: có <see cref="EndDate"/> thì thêm mọi ngày trong khoảng, bỏ qua ngày đã có.
/// Sửa: chỉ dùng <see cref="Date"/> và <see cref="Name"/>.</summary>
public class SaveHolidayDto
{
    public DateOnly Date { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Name { get; set; }
}
