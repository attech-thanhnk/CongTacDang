using System;
using System.Globalization;
using System.Text;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common;

/// <summary>Đặt tên tệp xuất theo quy cách "Mau bao cao_Ten viet tat co quan, don vi_Ky bao cao" (không dấu).</summary>
public static class ExportFileName
{
    /// <summary>Độ dài tối đa của phần tên, không kể đuôi tệp.</summary>
    public const int MaxBaseLength = 120;

    private const int MinSegmentLength = 8;

    /// <summary>Bỏ dấu tiếng Việt (kể cả đ/Đ); ký tự ngoài chữ-số ASCII và dấu gạch ngang thành khoảng trắng rồi gộp.</summary>
    public static string Ascii(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var normalized = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-')
            {
                if (pendingSpace && builder.Length > 0)
                    builder.Append(' ');
                pendingSpace = false;
                builder.Append(ch);
            }
            else
            {
                pendingSpace = true;
            }
        }
        return builder.ToString().Trim('-', ' ');
    }

    /// <summary>
    /// Phần đơn vị của tên tệp: tên viết tắt của đơn vị / tổ chức Đảng, rồi mã, rồi tên viết tắt trong cài đặt đơn vị.
    /// </summary>
    public static string? Unit(string? unitShortName, string? unitCode, string? orgShortName)
    {
        foreach (var candidate in new[] { unitShortName, unitCode, orgShortName })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate.Trim();
        }
        return null;
    }

    /// <inheritdoc cref="Unit(string?, string?, string?)"/>
    public static string? Unit(IOrgUnit? unit, string? orgShortName) => Unit(unit?.ShortName, unit?.Code, orgShortName);

    /// <summary>Phiếu cá nhân: tên viết tắt của tổ chức Đảng, rồi của đơn vị chính quyền của hồ sơ; không có thì tên viết tắt trong cài đặt đơn vị (không dùng mã).</summary>
    public static string? PersonalUnit(IOrgUnit? partyCell, IOrgUnit? department, string? orgShortName) =>
        Unit(partyCell?.ShortName ?? department?.ShortName, null, orgShortName);

    /// <summary>"Quy III-2026"; thiếu quý hoặc năm thì null.</summary>
    public static string? Period(int? quarter, int? year)
    {
        if (quarter is not { } q || year is not { } y)
            return null;
        var roman = q switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => q.ToString(CultureInfo.InvariantCulture) };
        return $"Quy {roman}-{y.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// <c>{form}_{unit}[_{qualifier}][_{period}]{extension}</c>, ví dụ "Mau 16_QLBMB_Quy III-2026.docx".
    /// Tên đơn vị bỏ khoảng trắng (viết tắt liền); <paramref name="qualifier"/> (thường là họ tên) giữ khoảng trắng.
    /// </summary>
    public static string Build(string form, string? unit, string? qualifier, int? quarter, int? year, string extension)
    {
        var formPart = Ascii(form);
        if (formPart.Length == 0)
            formPart = "Mau";
        var unitPart = Ascii(unit).Replace(" ", string.Empty);
        if (unitPart.Length == 0)
            unitPart = "DonVi";
        var qualifierPart = Ascii(qualifier);
        var periodPart = Period(quarter, year);

        // Quá dài thì cắt họ tên rồi đến đơn vị; mẫu và kỳ giữ nguyên.
        var fixedLength = formPart.Length + (periodPart?.Length ?? 0) + 3;
        var room = Math.Max(MaxBaseLength - fixedLength, MinSegmentLength * 2);
        if (unitPart.Length + qualifierPart.Length > room)
        {
            qualifierPart = Truncate(qualifierPart, Math.Max(room - unitPart.Length, MinSegmentLength));
            if (unitPart.Length + qualifierPart.Length > room)
                unitPart = Truncate(unitPart, Math.Max(room - qualifierPart.Length, MinSegmentLength));
        }

        var name = new StringBuilder(formPart).Append('_').Append(unitPart);
        if (qualifierPart.Length > 0)
            name.Append('_').Append(qualifierPart);
        if (periodPart != null)
            name.Append('_').Append(periodPart);
        return name + (extension ?? string.Empty);
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length].TrimEnd(' ', '-');
}
