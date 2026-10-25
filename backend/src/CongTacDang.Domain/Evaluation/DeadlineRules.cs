using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CongTacDang.Domain.Evaluation;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeadlineRuleKind
{
    /// <summary>Ngày làm việc thứ N kể từ đầu quý.</summary>
    WorkingDayOfQuarter = 0,

    /// <summary>Ngày D của tháng cuối quý; rơi vào ngày nghỉ thì lùi sang ngày làm việc kế tiếp.</summary>
    DayOfLastMonth = 1,

    /// <summary>N ngày làm việc trước đầu quý (1 = ngày làm việc cuối của quý trước).</summary>
    WorkingDaysBeforeQuarter = 2,

    /// <summary>Ngày làm việc cuối cùng trước ngày D của tháng cuối quý ("trước ngày D").</summary>
    BeforeDayOfLastMonth = 3
}

/// <summary>Quy tắc tính thời hạn của một bước theo quý.</summary>
public sealed class DeadlineRule
{
    public const int MaxWorkingDay = 40;

    public DeadlineRuleKind Kind { get; set; }

    /// <summary>N (1–<see cref="MaxWorkingDay"/>) hoặc D (ngày trong tháng, 1–28).</summary>
    public int Value { get; set; } = 1;

    /// <summary>Lỗi cấu hình hoặc null.</summary>
    public string? Validate() => Kind switch
    {
        DeadlineRuleKind.WorkingDayOfQuarter when Value is < 1 or > MaxWorkingDay =>
            $"ngày làm việc thứ N phải từ 1 đến {MaxWorkingDay}.",
        DeadlineRuleKind.DayOfLastMonth when Value is < 1 or > 28 => "ngày trong tháng cuối quý phải từ 1 đến 28.",
        DeadlineRuleKind.BeforeDayOfLastMonth when Value is < 2 or > 28 => "ngày trong tháng cuối quý phải từ 2 đến 28.",
        DeadlineRuleKind.WorkingDaysBeforeQuarter when Value is < 1 or > MaxWorkingDay =>
            $"số ngày làm việc trước quý phải từ 1 đến {MaxWorkingDay}.",
        _ => null
    };
}

/// <summary>Thứ Bảy, Chủ nhật và ngày nghỉ khai báo không phải ngày làm việc.</summary>
public static class DeadlineCalculator
{
    public static bool IsWorkingDay(DateOnly date, IReadOnlyCollection<DateOnly>? holidays) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && (holidays == null || !holidays.Contains(date));

    /// <summary>Kể cả chính ngày đó; lùi về trước.</summary>
    public static DateOnly PreviousWorkingDay(DateOnly date, IReadOnlyCollection<DateOnly>? holidays)
    {
        for (var i = 0; i < 366 && !IsWorkingDay(date, holidays); i++)
            date = date.AddDays(-1);
        return date;
    }

    /// <summary>Kể cả chính ngày đó.</summary>
    public static DateOnly NextWorkingDay(DateOnly date, IReadOnlyCollection<DateOnly>? holidays)
    {
        // Giới hạn vòng lặp phòng khai báo ngày nghỉ liên tiếp quá dài.
        for (var i = 0; i < 366 && !IsWorkingDay(date, holidays); i++)
            date = date.AddDays(1);
        return date;
    }

    public static DateOnly Compute(DeadlineRule rule, int year, int quarter, IReadOnlyCollection<DateOnly>? holidays)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (quarter is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(quarter));

        switch (rule.Kind)
        {
            case DeadlineRuleKind.WorkingDayOfQuarter:
            {
                var day = new DateOnly(year, (quarter - 1) * 3 + 1, 1);
                var counted = 0;
                for (var i = 0; i < 400; i++, day = day.AddDays(1))
                {
                    if (IsWorkingDay(day, holidays) && ++counted == Math.Max(1, rule.Value))
                        return day;
                }
                return day;
            }
            case DeadlineRuleKind.WorkingDaysBeforeQuarter:
            {
                var day = new DateOnly(year, (quarter - 1) * 3 + 1, 1).AddDays(-1);
                var counted = 0;
                for (var i = 0; i < 400; i++, day = day.AddDays(-1))
                {
                    if (IsWorkingDay(day, holidays) && ++counted == Math.Max(1, rule.Value))
                        return day;
                }
                return day;
            }
            case DeadlineRuleKind.BeforeDayOfLastMonth:
                return PreviousWorkingDay(new DateOnly(year, quarter * 3, Math.Clamp(rule.Value, 2, 28)).AddDays(-1), holidays);
            default:
                return NextWorkingDay(new DateOnly(year, quarter * 3, Math.Clamp(rule.Value, 1, 28)), holidays);
        }
    }
}
