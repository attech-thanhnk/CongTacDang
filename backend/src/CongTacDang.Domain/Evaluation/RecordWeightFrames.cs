using System;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Khung tỷ trọng A-B-C-D hiệu lực của một hồ sơ: khung gốc (<see cref="EvaluationRecord.WeightFrameCode"/> trong bộ tiêu chí
/// của kỳ) với tỷ trọng đã được cấp giao nhiệm vụ điều chỉnh khi duyệt danh mục sản phẩm (nếu có). Mọi chỗ chấm điểm/hiển thị
/// tỷ trọng của hồ sơ dùng <see cref="Effective"/>; công thức chấm (<see cref="EvaluationScoring.ScoreTask"/>) không đổi.
/// </summary>
public static class RecordWeightFrames
{
    /// <summary>Sai số cho phép của tổng tỷ lệ (điểm phần trăm).</summary>
    public const double SumTolerancePercent = 0.01;

    /// <summary>Hồ sơ có lưu tỷ trọng điều chỉnh (đủ bốn thành phần).</summary>
    public static bool IsAdjusted(EvaluationRecord record) =>
        record.FrameA.HasValue && record.FrameB.HasValue && record.FrameC.HasValue && record.FrameD.HasValue;

    /// <summary>Khung gốc của hồ sơ trong bộ tiêu chí; null nếu không có.</summary>
    public static WeightFrame? Base(EvaluationRecord record, CriteriaSetContent? content) => content?.FindFrame(record.WeightFrameCode);

    /// <summary>
    /// Khung hiệu lực của hồ sơ: khung gốc, thay tỷ trọng A-B-C-D bằng phần đã điều chỉnh nếu có. null khi bộ tiêu chí không
    /// có khung của hồ sơ.
    /// </summary>
    public static WeightFrame? Effective(EvaluationRecord record, CriteriaSetContent? content)
    {
        var frame = Base(record, content);
        if (frame == null || !IsAdjusted(record))
            return frame;
        return new WeightFrame
        {
            Code = frame.Code,
            Name = frame.Name,
            A = record.FrameA!.Value,
            B = record.FrameB!.Value,
            C = record.FrameC!.Value,
            D = record.FrameD!.Value
        };
    }

    /// <summary>
    /// Kiểm tra tỷ lệ điều chỉnh (phần trăm) so với khung gốc: bộ cho phép điều chỉnh; không âm; mỗi thành phần lệch khung gốc
    /// không quá biên độ của bộ; tổng bằng 100%. Trả thông báo lỗi hoặc null nếu hợp lệ.
    /// </summary>
    public static string? ValidateAdjustment(CriteriaSetContent content, WeightFrame? baseFrame, double a, double b, double c, double d)
    {
        ArgumentNullException.ThrowIfNull(content);
        var limit = content.Parameters.FrameAdjustLimit;
        if (!content.Parameters.AllowsFrameAdjustment || limit == null)
            return "Bộ tiêu chí của kỳ không cho điều chỉnh tỷ lệ A-B-C-D theo từng hồ sơ.";
        if (baseFrame == null)
            return "Hồ sơ chưa có khung tỷ trọng A-B-C-D trong bộ tiêu chí của kỳ nên chưa điều chỉnh được tỷ lệ.";

        var parts = new[] { ("A", a, baseFrame.A), ("B", b, baseFrame.B), ("C", c, baseFrame.C), ("D", d, baseFrame.D) };
        if (parts.Any(x => double.IsNaN(x.Item2) || double.IsInfinity(x.Item2)))
            return "Tỷ lệ A-B-C-D phải là số.";
        foreach (var (name, value, original) in parts)
        {
            if (value < 0)
                return $"Tỷ lệ {name} không được âm.";
            var basePercent = original * 100;
            if (Math.Abs(value - basePercent) > limit.Value + CriteriaSetContent.Epsilon)
            {
                return Invariant(
                    $"Tỷ lệ {name} ({value:0.##}%) lệch quá {limit.Value:0.##} điểm phần trăm so với khung {baseFrame.Code} ({basePercent:0.##}%).");
            }
        }
        var sum = a + b + c + d;
        if (Math.Abs(sum - 100) > SumTolerancePercent)
            return Invariant($"Tổng tỷ lệ A-B-C-D phải bằng 100% (đang là {sum:0.##}%).");
        return null;
    }

    /// <summary>
    /// Áp tỷ lệ điều chỉnh (phần trăm, đã kiểm tra) vào hồ sơ; trùng khung gốc thì xóa phần điều chỉnh. Trả true nếu tỷ trọng
    /// hiệu lực thay đổi.
    /// </summary>
    public static bool Apply(EvaluationRecord record, WeightFrame baseFrame, double a, double b, double c, double d)
    {
        var before = (record.FrameA, record.FrameB, record.FrameC, record.FrameD);
        var ratios = new[] { a, b, c, d }.Select(x => Math.Round(x / 100, 6)).ToArray();
        var same = Math.Abs(ratios[0] - baseFrame.A) < CriteriaSetContent.Epsilon && Math.Abs(ratios[1] - baseFrame.B) < CriteriaSetContent.Epsilon
            && Math.Abs(ratios[2] - baseFrame.C) < CriteriaSetContent.Epsilon && Math.Abs(ratios[3] - baseFrame.D) < CriteriaSetContent.Epsilon;
        if (same)
            Clear(record);
        else
            (record.FrameA, record.FrameB, record.FrameC, record.FrameD) = (ratios[0], ratios[1], ratios[2], ratios[3]);
        return before != (record.FrameA, record.FrameB, record.FrameC, record.FrameD);
    }

    /// <summary>Xóa phần điều chỉnh (quay về tỷ trọng gốc của khung).</summary>
    public static void Clear(EvaluationRecord record)
    {
        record.FrameA = null;
        record.FrameB = null;
        record.FrameC = null;
        record.FrameD = null;
    }

    /// <summary>Thông báo khi đổi khung tỷ trọng của hồ sơ đã qua bước tự chấm.</summary>
    public const string LockedMessage = "Danh mục đã duyệt — trả hồ sơ về bước tự chấm trước khi đổi khung tỷ trọng.";

    /// <summary>
    /// Đổi được khung tỷ trọng (mã khung / tỷ lệ điều chỉnh) của hồ sơ: hồ sơ đang chờ bước tự chấm hoặc bước trước đó (chưa duyệt
    /// danh mục, hoặc đã duyệt nhưng đang/được trả về bước tự chấm). Sau bước tự chấm, khung bị khóa.
    /// </summary>
    public static bool CanChangeFrame(EvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return WorkflowSteps.StepOf(record.Status) is { } step
            && WorkflowSteps.IndexOf(step) <= WorkflowSteps.IndexOf(WorkflowStep.B2_SELF_SCORE);
    }

    /// <summary>Mô tả tỷ lệ của khung, ví dụ "A 30% · B 30% · C 20% · D 20%".</summary>
    public static string Describe(WeightFrame frame) =>
        Invariant($"A {frame.A * 100:0.##}% · B {frame.B * 100:0.##}% · C {frame.C * 100:0.##}% · D {frame.D * 100:0.##}%");

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
