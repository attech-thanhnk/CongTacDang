using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Tiêu đề trái văn bản Đảng (chữ in hoa, null = giữ mặc định); UnitName là tên dùng giữa câu.</summary>
public sealed record PartyHeader(string? Parent, string? Organization, string? UnitName)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>unitName null → dùng organization (xem <see cref="CollectiveFormText.NameInSentence"/>).</summary>
    public static PartyHeader Of(string? parent, string? organization, string? unitName = null) => new(
        Upper(parent), Upper(organization), FormText.OrNull(unitName?.Trim()) ?? FormText.OrNull(organization?.Trim()));

    public static string? Upper(string? value) => FormText.OrNull(value?.Trim())?.ToUpper(Vietnamese);

    /// <summary>Tên dài hơn mức này (ký tự) không vừa một dòng ở cột tiêu đề trái.</summary>
    private const int MaxLineLength = 30;

    /// <summary>Tên dài ngắt thành hai dòng cân nhau tại khoảng trắng, tránh Word ngắt lửng một chữ.</summary>
    public static string? Lines(string? value)
    {
        if (value == null || value.Length <= MaxLineLength || value.Contains('\n'))
            return value;
        var best = -1;
        for (var i = value.IndexOf(' '); i > 0; i = value.IndexOf(' ', i + 1))
        {
            if (best < 0 || Math.Abs(value.Length - 2 * i) < Math.Abs(value.Length - 2 * best))
                best = i;
        }
        return best < 0 ? value : value[..best] + "\n" + value[(best + 1)..];
    }
}

public static class CollectiveFormText
{
    /// <summary>UTC+7, không đổi giờ theo mùa; CSDL lưu UTC.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>"Quý III/2026".</summary>
    public static string RomanQuarter(EvaluationQuarter quarter) => (int)quarter switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => ((int)quarter).ToString(CultureInfo.InvariantCulture)
    };

    public static DateTime ToVietnamTime(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc.ToUniversalTime();
        return value + VietnamOffset;
    }

    /// <summary>"08h30", giờ Việt Nam.</summary>
    public static string Time(DateTime utc) => ToVietnamTime(utc).ToString("HH'h'mm", CultureInfo.InvariantCulture);

    /// <summary>"15/09/2026", giờ Việt Nam.</summary>
    public static string Date(DateTime utc) => ToVietnamTime(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>"ngày 15 tháng 09 năm 2026" (giờ Việt Nam); null = giữ dòng chấm.</summary>
    public static string? DateLine(DateTime? utc) => utc.HasValue
        ? ToVietnamTime(utc.Value).ToString("'ngày 'dd' tháng 'MM' năm 'yyyy", CultureInfo.InvariantCulture)
        : null;

    /// <summary>
    /// Tên viết HOA TOÀN BỘ đổi về chữ thường, giữ cách viết của tên riêng khớp được ("ĐẢNG BỘ CÔNG TY TNHH A" → "Đảng bộ Công ty TNHH A").
    /// Không khớp tên riêng nào, hoặc tên đã có chữ thường, thì giữ nguyên.
    /// </summary>
    public static string? NameInSentence(string? name, params string?[] properNames)
    {
        var text = FormText.OrNull(name?.Trim());
        if (text == null || text.Any(char.IsLower))
            return text;
        var lower = text.ToLower(Vietnamese);
        var matched = false;
        foreach (var proper in properNames.Select(p => FormText.OrNull(p?.Trim())).OfType<string>().OrderByDescending(p => p.Length))
        {
            var index = lower.IndexOf(proper.ToLower(Vietnamese), StringComparison.Ordinal);
            if (index < 0)
                continue;
            lower = string.Concat(lower.AsSpan(0, index), proper, lower.AsSpan(index + proper.Length));
            matched = true;
        }
        return matched ? char.ToUpper(lower[0], Vietnamese) + lower[1..] : text;
    }

    public static string? PartyCommitteeInSentence(OrganizationSettingsDto org)
    {
        ArgumentNullException.ThrowIfNull(org);
        return NameInSentence(org.PartyCommitteeName, org.CompanyName, org.ParentCompanyName);
    }

    /// <summary>"Đảng bộ X" → "ĐẢNG ỦY X"; tên khác giữ nguyên.</summary>
    public static string? CommitteeUpper(string? partyOrganization)
    {
        const string partyBody = "ĐẢNG BỘ ";
        var upper = PartyHeader.Upper(partyOrganization);
        return upper != null && upper.StartsWith(partyBody, StringComparison.Ordinal) ? "ĐẢNG ỦY " + upper[partyBody.Length..] : upper;
    }

    /// <summary>"Đảng bộ X" → "Đảng ủy X"; tên khác giữ nguyên.</summary>
    public static string? CommitteeInSentence(string? partyOrganization)
    {
        const string partyBody = "Đảng bộ ";
        var text = FormText.OrNull(partyOrganization?.Trim());
        if (text == null || !text.StartsWith(partyBody, true, Vietnamese))
            return text;
        var prefix = text[..partyBody.Length];
        return (prefix == prefix.ToUpper(Vietnamese) ? "ĐẢNG ỦY " : "Đảng ủy ") + text[partyBody.Length..];
    }

    /// <summary>"Đảng bộ X" → "Ban Chấp hành Đảng bộ X"; tên khác giữ nguyên.</summary>
    public static string? ExecutiveInSentence(string? partyOrganization)
    {
        var text = FormText.OrNull(partyOrganization?.Trim());
        return text != null && text.StartsWith("Đảng bộ ", true, Vietnamese) ? "Ban Chấp hành " + text : text;
    }

    /// <summary>"T/M ĐẢNG ỦY/CHI ỦY/CHI BỘ"; không nhận ra → null (giữ mặc định).</summary>
    public static string? SignOnBehalf(string? committeeUpper)
    {
        foreach (var body in new[] { "ĐẢNG ỦY", "CHI ỦY", "CHI BỘ" })
        {
            if (committeeUpper != null && committeeUpper.StartsWith(body, StringComparison.Ordinal))
                return "T/M " + body;
        }
        return null;
    }

    /// <summary>Nhiệm kỳ 5 năm từ 2025 ("2025-2030"); năm đại hội (trừ 2025) không xác định được → null.</summary>
    public static string? PartyTerm(int year)
    {
        const int firstTerm = 2025, length = 5;
        var offset = ((year - firstTerm) % length + length) % length;
        if (offset == 0 && year != firstTerm)
            return null;
        var start = year - offset;
        return $"{start.ToString(CultureInfo.InvariantCulture)}-{(start + length).ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Điểm dạng "28,5".</summary>
    public static string Score(double value) => FormText.Number(value, value % 1 == 0 ? 0 : 1);

    /// <summary>Nhiều hơn một dòng thì mỗi dòng bắt đầu bằng "- ".</summary>
    public static string? Lines(IEnumerable<string?> values)
    {
        var list = values.Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToList();
        if (list.Count == 0)
            return null;
        return list.Count == 1 ? list[0] : string.Join("\n", list.Select(v => "- " + v));
    }

    /// <summary>Chỗ điền của mẫu gốc liền với dấu "…" nên thêm khoảng trắng đầu.</summary>
    public static string? Spaced(string? value) => FormText.OrNull(value) is { } text ? " " + text.Trim() : null;
}
