using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>Danh mục ngày nghỉ (quyền kiểm tra ở controller).</summary>
public interface IHolidayService
{
    Task<List<HolidayDto>> GetByYearAsync(int year, CancellationToken ct = default);

    /// <summary>Thêm một ngày hoặc một khoảng ngày.</summary>
    Task<List<HolidayDto>> CreateAsync(SaveHolidayDto input, CancellationToken ct = default);

    Task<HolidayDto> UpdateAsync(Guid id, SaveHolidayDto input, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class HolidayService : IHolidayService
{
    public const int MaxNameLength = 200;
    public const int MaxRangeDays = 31;
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    private readonly IHolidayRepository _repo;
    private readonly IUnitOfWork _unitOfWork;

    public HolidayService(IHolidayRepository repo, IUnitOfWork unitOfWork)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<List<HolidayDto>> GetByYearAsync(int year, CancellationToken ct = default)
    {
        if (year < MinYear || year > MaxYear)
            throw new ValidationException($"Năm phải từ {MinYear} đến {MaxYear}.");
        var items = await _repo.ListAsync(new DateOnly(year, 1, 1), new DateOnly(year, 12, 31), ct);
        return items.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<HolidayDto>> CreateAsync(SaveHolidayDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var name = ValidateName(input.Name);
        var start = ValidateDate(input.Date);
        var end = ValidateDate(input.EndDate ?? start);
        if (end < start)
            throw new ValidationException("Ngày kết thúc phải sau hoặc trùng ngày bắt đầu.");
        if (end.DayNumber - start.DayNumber + 1 > MaxRangeDays)
            throw new ValidationException($"Mỗi lần chỉ thêm tối đa {MaxRangeDays} ngày liên tiếp.");

        var existing = await _repo.ExistingDatesAsync(start, end, null, ct);
        var created = new List<Holiday>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (existing.Contains(day))
                continue;
            var holiday = new Holiday { Date = day, Name = name };
            _repo.Add(holiday);
            created.Add(holiday);
        }
        if (created.Count == 0)
            throw new ConflictException(start == end
                ? $"Ngày {start:dd/MM/yyyy} đã có trong danh mục ngày nghỉ."
                : "Mọi ngày trong khoảng đã chọn đều đã có trong danh mục ngày nghỉ.");

        await _unitOfWork.SaveChangesAsync(ct);
        return created.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<HolidayDto> UpdateAsync(Guid id, SaveHolidayDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var holiday = await _repo.FindAsync(id, ct)
            ?? throw new NotFoundException("Không tìm thấy ngày nghỉ cần sửa. Hãy tải lại danh sách.");
        var name = ValidateName(input.Name);
        var date = ValidateDate(input.Date);
        if (date != holiday.Date && (await _repo.ExistingDatesAsync(date, date, id, ct)).Contains(date))
            throw new ConflictException($"Ngày {date:dd/MM/yyyy} đã có trong danh mục ngày nghỉ.");

        holiday.Date = date;
        holiday.Name = name;
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(holiday);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var holiday = await _repo.FindAsync(id, ct)
            ?? throw new NotFoundException("Không tìm thấy ngày nghỉ cần xóa. Hãy tải lại danh sách.");
        _repo.Remove(holiday);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private static string ValidateName(string? value)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxNameLength)
            throw new ValidationException($"Tên ngày nghỉ phải có từ 1 đến {MaxNameLength} ký tự.");
        return name;
    }

    private static DateOnly ValidateDate(DateOnly date)
    {
        if (date.Year < MinYear || date.Year > MaxYear)
            throw new ValidationException($"Ngày nghỉ phải thuộc năm {MinYear}–{MaxYear}.");
        return date;
    }

    private static HolidayDto ToDto(Holiday h) => new() { Id = h.Id, Date = h.Date, Name = h.Name };
}
