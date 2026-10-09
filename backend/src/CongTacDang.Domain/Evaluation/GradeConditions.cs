using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Dữ liệu một nhiệm vụ dùng để xét điều kiện xếp loại theo mức thẩm định.</summary>
/// <param name="Kind">Loại nhiệm vụ; null (dữ liệu cũ) coi như không phải nhiệm vụ trọng tâm.</param>
/// <param name="AppraisalLevel">Mức thẩm định 1/2/3; null = chưa xếp mức (không xét).</param>
/// <param name="IsExceedStandard">Nhiệm vụ vượt chuẩn.</param>
public sealed record GradeConditionTask(TaskKind? Kind, int? AppraisalLevel, bool IsExceedStandard);

/// <summary>
/// Điều kiện xếp loại cá nhân theo mức thẩm định từng nhiệm vụ (chấm theo danh mục nhiệm vụ — Mẫu 09A):
/// <list type="bullet">
/// <item>Có nhiệm vụ trọng tâm Mức 3 → chỉ xếp được Không hoàn thành nhiệm vụ.</item>
/// <item>Hoàn thành xuất sắc: không có nhiệm vụ trọng tâm Mức 2 hoặc 3, tỷ lệ nhiệm vụ vượt chuẩn đạt ngưỡng của bộ tiêu chí
/// (<see cref="GradeRule.MinExceedStandardRatio"/> của mức Xuất sắc; bộ không đặt thì không xét).</item>
/// <item>Hoàn thành tốt, Hoàn thành: không có nhiệm vụ trọng tâm Mức 3.</item>
/// </list>
/// Chỉ trả cảnh báo — không chặn thao tác. Lớp thuần, không đổi công thức tính điểm.
/// </summary>
public static class GradeConditions
{
    /// <summary>Mức thẩm định thấp nhất (tốt nhất).</summary>
    public const int MinLevel = 1;

    /// <summary>Mức thẩm định cao nhất (không đáp ứng).</summary>
    public const int MaxLevel = 3;

    /// <summary>Mức thẩm định hợp lệ (1..3).</summary>
    public static bool IsValidLevel(int level) => level is >= MinLevel and <= MaxLevel;

    /// <summary>
    /// Danh sách cảnh báo khi mức <paramref name="proposed"/> không thỏa điều kiện theo các nhiệm vụ. Không có nhiệm vụ hoặc
    /// chưa có mức đề xuất → rỗng. <paramref name="minExceedStandardRatio"/>: tỷ lệ vượt chuẩn tối thiểu (0..1) của mức Xuất
    /// sắc theo bộ tiêu chí; null = không xét.
    /// </summary>
    public static IReadOnlyList<string> Check(
        IReadOnlyCollection<GradeConditionTask> tasks, EvaluationGrade proposed, double? minExceedStandardRatio)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        var warnings = new List<string>();
        if (tasks.Count == 0 || proposed == EvaluationGrade.ChuaXepLoai)
            return warnings;

        var focus = tasks.Where(t => t.Kind == TaskKind.TrongTam).ToList();
        var focusLevel3 = focus.Count(t => t.AppraisalLevel == 3);
        var focusLevel2 = focus.Count(t => t.AppraisalLevel == 2);

        if (focusLevel3 > 0 && proposed != EvaluationGrade.KhongHoanThanh)
        {
            warnings.Add($"Có {focusLevel3} nhiệm vụ trọng tâm xếp Mức 3: chỉ xếp được "
                + $"\"{CriteriaSetContent.GradeName(EvaluationGrade.KhongHoanThanh)}\".");
        }

        if (proposed == EvaluationGrade.HoanThanhXuatSac)
        {
            if (focusLevel2 > 0)
            {
                warnings.Add($"Có {focusLevel2} nhiệm vụ trọng tâm xếp Mức 2: chưa đủ điều kiện "
                    + $"\"{CriteriaSetContent.GradeName(EvaluationGrade.HoanThanhXuatSac)}\".");
            }

            if (minExceedStandardRatio is { } minRatio && minRatio > 0)
            {
                var exceed = tasks.Count(t => t.IsExceedStandard);
                var ratio = (double)exceed / tasks.Count;
                if (ratio + CriteriaSetContent.Epsilon < minRatio)
                {
                    warnings.Add(string.Create(CultureInfo.InvariantCulture,
                        $"Mới có {exceed}/{tasks.Count} nhiệm vụ vượt chuẩn ({Percent(ratio)}), cần từ {Percent(minRatio)}: chưa đủ điều kiện "
                        + $"\"{CriteriaSetContent.GradeName(EvaluationGrade.HoanThanhXuatSac)}\"."));
                }
            }
        }

        return warnings;
    }

    /// <summary>Tỷ lệ vượt chuẩn tối thiểu của mức Xuất sắc theo bộ tiêu chí (null = bộ không đặt).</summary>
    public static double? ExcellentMinExceedRatio(CriteriaSetContent? criteria) =>
        criteria?.FindGrade(EvaluationGrade.HoanThanhXuatSac)?.MinExceedStandardRatio;

    private static string Percent(double ratio) =>
        (Math.Round(ratio * 100, 1)).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + "%";
}
