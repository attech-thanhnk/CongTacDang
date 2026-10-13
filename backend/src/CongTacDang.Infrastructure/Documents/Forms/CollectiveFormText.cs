using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Dòng tiêu đề trái của văn bản Đảng: "ĐẢNG BỘ …" (tổ chức cấp trên) và "ĐẢNG ỦY (CHI BỘ) …" (tổ chức lập văn bản).</summary>
/// <param name="Parent">Dòng "ĐẢNG BỘ …" (chữ in hoa); null = giữ chữ mặc định.</param>
/// <param name="Organization">Dòng "ĐẢNG ỦY (CHI BỘ) …" (chữ in hoa); null = giữ chữ mặc định.</param>
/// <param name="UnitName">Tên tổ chức lập văn bản dùng giữa câu (viết thường, không in hoa toàn bộ).</param>
public sealed record PartyHeader(string? Parent, string? Organization, string? UnitName)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>
    /// Dựng từ tên tổ chức (chuỗi rỗng → null). <paramref name="unitName"/>: tên dùng giữa câu khi tên khai báo viết HOA TOÀN BỘ
    /// (xem <see cref="CollectiveFormText.NameInSentence"/>); null → dùng <paramref name="organization"/>.
    /// </summary>
    public static PartyHeader Of(string? parent, string? organization, string? unitName = null) => new(
        Upper(parent), Upper(organization), FormText.OrNull(unitName?.Trim()) ?? FormText.OrNull(organization?.Trim()));

    /// <summary>Chữ in hoa theo tiếng Việt; rỗng → null.</summary>
    public static string? Upper(string? value) => FormText.OrNull(value?.Trim())?.ToUpper(Vietnamese);

    /// <summary>Tên dài hơn mức này (ký tự) không vừa một dòng ở cột tiêu đề trái.</summary>
    private const int MaxLineLength = 30;

    /// <summary>
    /// Dòng tiêu đề trái in trên biểu mẫu: tên dài ngắt thành hai dòng cân nhau tại khoảng trắng
    /// ("ĐẢNG BỘ TỔNG CÔNG TY" / "QUẢN LÝ BAY VIỆT NAM") thay vì để Word ngắt lửng một chữ xuống dòng.
    /// </summary>
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

/// <summary>Định dạng riêng của các biểu mẫu tập thể, biên bản và báo cáo (Mẫu 07, 08, 12, 16).</summary>
public static class CollectiveFormText
{
    /// <summary>Giờ Việt Nam (UTC+7, không đổi giờ theo mùa) — thời điểm lưu trong CSDL là UTC.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Số quý dạng La Mã như văn bản HD03 ("Quý III/2026").</summary>
    public static string RomanQuarter(EvaluationQuarter quarter) => (int)quarter switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        _ => ((int)quarter).ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>Đổi thời điểm UTC sang giờ Việt Nam.</summary>
    public static DateTime ToVietnamTime(DateTime utc)
    {
        var value = utc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(utc, DateTimeKind.Utc) : utc.ToUniversalTime();
        return value + VietnamOffset;
    }

    /// <summary>Giờ dạng "08h30" (giờ Việt Nam).</summary>
    public static string Time(DateTime utc) => ToVietnamTime(utc).ToString("HH'h'mm", CultureInfo.InvariantCulture);

    /// <summary>Ngày dạng "15/09/2026" (giờ Việt Nam).</summary>
    public static string Date(DateTime utc) => ToVietnamTime(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Phần "ngày 15 tháng 09 năm 2026" của dòng "Địa danh, ngày … tháng … năm …" (giờ Việt Nam); null = giữ dòng chấm của biểu mẫu.
    /// </summary>
    public static string? DateLine(DateTime? utc) => utc.HasValue
        ? ToVietnamTime(utc.Value).ToString("'ngày 'dd' tháng 'MM' năm 'yyyy", CultureInfo.InvariantCulture)
        : null;

    /// <summary>
    /// Tên tổ chức dùng giữa câu. Tên khai báo viết HOA TOÀN BỘ (như dòng tiêu đề, ví dụ tên Đảng bộ trong cài đặt đơn vị) được
    /// viết lại dạng thường, phần trùng tên riêng đã biết (tên công ty, đơn vị cấp trên) giữ đúng cách viết của tên riêng:
    /// "ĐẢNG BỘ CÔNG TY TNHH A" + "Công ty TNHH A" → "Đảng bộ Công ty TNHH A". Không khớp tên riêng nào thì giữ nguyên (không
    /// đoán cách viết hoa); tên đã có chữ thường giữ nguyên.
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

    /// <summary>Tên Đảng bộ (cài đặt đơn vị) dùng giữa câu, ví dụ "Đảng bộ Công ty TNHH Kỹ thuật Quản lý bay".</summary>
    public static string? PartyCommitteeInSentence(OrganizationSettingsDto org)
    {
        ArgumentNullException.ThrowIfNull(org);
        return NameInSentence(org.PartyCommitteeName, org.CompanyName, org.ParentCompanyName);
    }

    /// <summary>
    /// Cấp ủy của tổ chức Đảng, chữ in hoa (tiêu đề "… THẨM QUYỀN … CỦA ĐẢNG ỦY/CHI ỦY CƠ SỞ" của Mẫu 13): "Đảng bộ X" →
    /// "ĐẢNG ỦY X"; tên khác (Chi bộ, Đảng ủy …) giữ nguyên tên tổ chức. Rỗng → null.
    /// </summary>
    public static string? CommitteeUpper(string? partyOrganization)
    {
        const string partyBody = "ĐẢNG BỘ ";
        var upper = PartyHeader.Upper(partyOrganization);
        return upper != null && upper.StartsWith(partyBody, StringComparison.Ordinal) ? "ĐẢNG ỦY " + upper[partyBody.Length..] : upper;
    }

    /// <summary>
    /// Cấp ủy của tổ chức Đảng dùng giữa câu: "Đảng bộ X" → "Đảng ủy X"; tên khác (Chi bộ, Đảng ủy …) giữ nguyên. Rỗng → null.
    /// </summary>
    public static string? CommitteeInSentence(string? partyOrganization)
    {
        const string partyBody = "Đảng bộ ";
        var text = FormText.OrNull(partyOrganization?.Trim());
        if (text == null || !text.StartsWith(partyBody, true, Vietnamese))
            return text;
        var prefix = text[..partyBody.Length];
        return (prefix == prefix.ToUpper(Vietnamese) ? "ĐẢNG ỦY " : "Đảng ủy ") + text[partyBody.Length..];
    }

    /// <summary>
    /// Cơ quan lãnh đạo của tổ chức Đảng dùng giữa câu: "Đảng bộ X" → "Ban Chấp hành Đảng bộ X"; tên khác giữ nguyên. Rỗng → null.
    /// </summary>
    public static string? ExecutiveInSentence(string? partyOrganization)
    {
        var text = FormText.OrNull(partyOrganization?.Trim());
        return text != null && text.StartsWith("Đảng bộ ", true, Vietnamese) ? "Ban Chấp hành " + text : text;
    }

    /// <summary>Dòng "T/M …" của chữ ký theo cấp ủy chữ in hoa (ĐẢNG ỦY / CHI ỦY / CHI BỘ); không nhận ra → null (giữ mặc định).</summary>
    public static string? SignOnBehalf(string? committeeUpper)
    {
        foreach (var body in new[] { "ĐẢNG ỦY", "CHI ỦY", "CHI BỘ" })
        {
            if (committeeUpper != null && committeeUpper.StartsWith(body, StringComparison.Ordinal))
                return "T/M " + body;
        }
        return null;
    }

    /// <summary>
    /// Nhiệm kỳ cấp ủy (5 năm, bắt đầu từ năm 2025) chứa năm của kỳ, dạng "2025-2030". Năm đại hội (bắt đầu nhiệm kỳ mới, trừ
    /// 2025) không xác định được kỳ thuộc nhiệm kỳ nào → null (giữ chỗ chấm để ghi tay).
    /// </summary>
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

    /// <summary>Ghép nhiều dòng; nhiều hơn một dòng thì mỗi dòng bắt đầu bằng "- " (giống dòng "- Nhiệm vụ 1: …" của biểu mẫu).</summary>
    public static string? Lines(IEnumerable<string?> values)
    {
        var list = values.Select(v => v?.Trim()).Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToList();
        if (list.Count == 0)
            return null;
        return list.Count == 1 ? list[0] : string.Join("\n", list.Select(v => "- " + v));
    }

    /// <summary>Thêm khoảng trắng đầu (chỗ điền của biểu mẫu gốc không có khoảng trắng trước dấu "…").</summary>
    public static string? Spaced(string? value) => FormText.OrNull(value) is { } text ? " " + text.Trim() : null;
}
