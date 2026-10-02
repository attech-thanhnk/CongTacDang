using System;

namespace CongTacDang.Application.DTOs;

/// <summary>Một ngày nghỉ trong danh mục.</summary>
public class HolidayDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Thêm/sửa ngày nghỉ. Thêm: có <see cref="EndDate"/> thì thêm mỗi ngày trong khoảng <see cref="Date"/>–<see cref="EndDate"/>
/// (ví dụ nghỉ Tết nhiều ngày), ngày đã có trong danh mục bị bỏ qua. Sửa: chỉ dùng <see cref="Date"/> và <see cref="Name"/>.
/// </summary>
public class SaveHolidayDto
{
    public DateOnly Date { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Name { get; set; }
}
