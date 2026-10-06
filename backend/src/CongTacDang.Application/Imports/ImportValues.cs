using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CongTacDang.Application.Imports;

/// <summary>Đọc giá trị ô trong file nhập: ngày dd/MM/yyyy, Có/Không, danh sách phân tách bằng ";".</summary>
public static class ImportValues
{
    /// <summary>Giá trị "Có".</summary>
    public const string Yes = "Có";

    /// <summary>Giá trị "Không".</summary>
    public const string No = "Không";

    /// <summary>Danh sách giá trị Có/Không.</summary>
    public static readonly IReadOnlyList<string> YesNo = new[] { Yes, No };

    /// <summary>Lệch múi giờ Việt Nam (UTC+7, không đổi giờ) — ngày trong file hiểu theo giờ Việt Nam.</summary>
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static readonly string[] DateFormats = { "dd/MM/yyyy", "d/M/yyyy", "dd/M/yyyy", "d/MM/yyyy" };

    /// <summary>Đọc ngày dạng dd/MM/yyyy; trống → null, <paramref name="ok"/> = true; sai → null, <paramref name="ok"/> = false.</summary>
    public static DateOnly? ParseDate(string value, out bool ok)
    {
        ok = true;
        var text = value.Trim();
        if (text.Length == 0)
            return null;
        if (DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        ok = false;
        return null;
    }

    /// <summary>Đầu ngày (00:00 giờ Việt Nam) đổi sang UTC.</summary>
    public static DateTime StartOfDayUtc(DateOnly date)
        => DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);

    /// <summary>
    /// Đọc Có/Không (không phân biệt hoa thường, chấp nhận không dấu "co"/"khong"); trống → null, <paramref name="ok"/> = true.
    /// </summary>
    public static bool? ParseYesNo(string value, out bool ok)
    {
        ok = true;
        var key = Fold(value);
        if (key.Length == 0)
            return null;
        if (key is "co" or "c" or "x" or "1")
            return true;
        if (key is "khong" or "k" or "0")
            return false;
        ok = false;
        return null;
    }

    /// <summary>Tách danh sách phân tách bằng ";" (cắt khoảng trắng, bỏ phần tử rỗng, bỏ trùng không phân biệt hoa thường).</summary>
    public static List<string> SplitList(string value)
        => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>Chuẩn hóa để so khớp tên: bỏ dấu tiếng Việt, chữ thường, gộp khoảng trắng.</summary>
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var lastSpace = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            var c = ch is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(ch);
            if (char.IsWhiteSpace(c))
            {
                if (!lastSpace)
                    builder.Append(' ');
                lastSpace = true;
                continue;
            }
            lastSpace = false;
            builder.Append(c);
        }
        return builder.ToString();
    }

    /// <summary>Hai chuỗi bằng nhau không phân biệt hoa thường (giữ dấu).</summary>
    public static bool SameText(string? a, string? b)
        => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Đọc số nguyên (thứ tự); trống → null.</summary>
    public static int? ParseInt(string value, out bool ok)
    {
        ok = true;
        var text = value.Trim();
        if (text.Length == 0)
            return null;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return number;
        ok = false;
        return null;
    }
}
