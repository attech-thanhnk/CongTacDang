using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Mã sản phẩm (cột "Mã SP" Mẫu 01, dẫn chiếu ở Mẫu 04): sản phẩm để trống mã được tự đánh SP01, SP02…</summary>
public static class ProductCodes
{
    public const string Prefix = "SP";

    private static readonly Regex Numbered = new(@"^SP(\d{1,6})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>"SP01", "SP02"… (từ 100 thì đủ số chữ số).</summary>
    public static string Format(int number) => Prefix + number.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Số lớn nhất của các mã dạng SPnn; 0 nếu không có.</summary>
    public static int MaxNumber(IEnumerable<string?> codes) =>
        codes.Select(c => c == null ? null : Numbered.Match(c.Trim()))
            .Where(m => m is { Success: true })
            .Select(m => int.Parse(m!.Groups[1].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();

    /// <summary>Mã đầu tiên bị trùng (không phân biệt hoa thường), null nếu không trùng.</summary>
    public static string? FindDuplicate(IEnumerable<string?> codes)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in codes)
        {
            var trimmed = code?.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !seen.Add(trimmed))
                return trimmed;
        }
        return null;
    }

    /// <summary>
    /// Đánh mã cho sản phẩm còn trống theo thứ tự truyền vào, tiếp sau số SPnn lớn nhất của hồ sơ;
    /// <paramref name="usedCodes"/> gồm cả mã của sản phẩm đã xóa để không dùng lại. Mã đã có giữ nguyên.
    /// </summary>
    public static void AssignMissing(IEnumerable<EvaluationTask> tasksInOrder, IEnumerable<string?>? usedCodes = null)
    {
        var tasks = tasksInOrder.ToList();
        var next = Math.Max(MaxNumber(tasks.Select(t => t.ProductCode)), MaxNumber(usedCodes ?? Array.Empty<string?>()));
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.ProductCode))
                task.ProductCode = Format(++next);
            else
                task.ProductCode = task.ProductCode.Trim();
        }
    }
}
