using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Nhiệm vụ dùng xét điều kiện xếp loại; <c>Kind</c> null coi như không trọng tâm, <c>AppraisalLevel</c> null thì không xét.</summary>
public sealed record GradeConditionTask(TaskKind? Kind, int? AppraisalLevel, bool IsExceedStandard);

/// <summary>Điều kiện xếp loại theo mức thẩm định nhiệm vụ (09A): trọng tâm Mức 3 → chỉ Không hoàn thành; Xuất sắc cần
/// không có trọng tâm Mức 2/3 và đủ tỷ lệ vượt chuẩn. Chỉ cảnh báo, không chặn thao tác.</summary>
public static class GradeConditions
{
    /// <summary>Tốt nhất.</summary>
    public const int MinLevel = 1;

    /// <summary>Không đáp ứng.</summary>
    public const int MaxLevel = 3;

    public static bool IsValidLevel(int level) => level is >= MinLevel and <= MaxLevel;

    /// <summary>Không có nhiệm vụ hoặc chưa có mức đề xuất → rỗng; <paramref name="minExceedStandardRatio"/> null = không xét.</summary>
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

    /// <summary>Null = bộ không đặt.</summary>
    public static double? ExcellentMinExceedRatio(CriteriaSetContent? criteria) =>
        criteria?.FindGrade(EvaluationGrade.HoanThanhXuatSac)?.MinExceedStandardRatio;

    private static string Percent(double ratio) =>
        (Math.Round(ratio * 100, 1)).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + "%";
}
