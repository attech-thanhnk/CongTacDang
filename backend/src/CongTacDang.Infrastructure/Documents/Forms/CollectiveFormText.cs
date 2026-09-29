using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Dòng tiêu đề trái của văn bản Đảng: "ĐẢNG BỘ …" (tổ chức cấp trên) và "ĐẢNG ỦY (CHI BỘ) …" (tổ chức lập văn bản).</summary>
/// <param name="Parent">Dòng "ĐẢNG BỘ …" (chữ in hoa); null = giữ chữ mặc định.</param>
/// <param name="Organization">Dòng "ĐẢNG ỦY (CHI BỘ) …" (chữ in hoa); null = giữ chữ mặc định.</param>
/// <param name="UnitName">Tên tổ chức lập văn bản, viết thường như đã khai báo (dùng trong câu).</param>
public sealed record PartyHeader(string? Parent, string? Organization, string? UnitName)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Dựng từ tên tổ chức (chuỗi rỗng → null).</summary>
    public static PartyHeader Of(string? parent, string? organization) => new(
        Upper(parent), Upper(organization), FormText.OrNull(organization?.Trim()));

    /// <summary>Chữ in hoa theo tiếng Việt; rỗng → null.</summary>
    public static string? Upper(string? value) => FormText.OrNull(value?.Trim())?.ToUpper(Vietnamese);
}

/// <summary>Định dạng riêng của các biểu mẫu tập thể, biên bản và báo cáo (Mẫu 07, 08, 12, 16).</summary>
public static class CollectiveFormText
{
    /// <summary>Giờ Việt Nam (UTC+7, không đổi giờ theo mùa) — thời điểm lưu trong CSDL là UTC.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

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
