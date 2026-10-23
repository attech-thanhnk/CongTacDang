using System;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Khung tỷ trọng hiệu lực của hồ sơ: khung gốc, thay bằng tỷ trọng cấp giao nhiệm vụ đã điều chỉnh (nếu có).</summary>
public static class RecordWeightFrames
{
    /// <summary>Đơn vị: điểm phần trăm.</summary>
    public const double SumTolerancePercent = 0.01;

    /// <summary>Phải đủ cả bốn thành phần.</summary>
    public static bool IsAdjusted(EvaluationRecord record) =>
        record.FrameA.HasValue && record.FrameB.HasValue && record.FrameC.HasValue && record.FrameD.HasValue;

    /// <summary>Null nếu bộ tiêu chí không có khung.</summary>
    public static WeightFrame? Base(EvaluationRecord record, CriteriaSetContent? content) => content?.FindFrame(record.WeightFrameCode);

    /// <summary>Null khi bộ tiêu chí không có khung của hồ sơ.</summary>
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

    /// <summary>Tỷ lệ tính bằng phần trăm; mỗi thành phần lệch khung gốc không quá biên độ của bộ, tổng 100%. Trả lỗi hoặc null.</summary>
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

    /// <summary>Tỷ lệ phần trăm đã kiểm tra; trùng khung gốc thì xóa phần điều chỉnh. Trả true nếu tỷ trọng hiệu lực đổi.</summary>
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

    /// <summary>Quay về tỷ trọng gốc của khung.</summary>
    public static void Clear(EvaluationRecord record)
    {
        record.FrameA = null;
        record.FrameB = null;
        record.FrameC = null;
        record.FrameD = null;
    }

    public const string LockedMessage = "Danh mục đã duyệt — trả hồ sơ về bước tự chấm trước khi đổi khung tỷ trọng.";

    /// <summary>Chỉ đổi được khi hồ sơ đang chờ bước tự chấm hoặc bước trước đó.</summary>
    public static bool CanChangeFrame(EvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return WorkflowSteps.StepOf(record.Status) is { } step
            && WorkflowSteps.IndexOf(step) <= WorkflowSteps.IndexOf(WorkflowStep.B2_SELF_SCORE);
    }

    /// <summary>Ví dụ "A 30% · B 30% · C 20% · D 20%".</summary>
    public static string Describe(WeightFrame frame) =>
        Invariant($"A {frame.A * 100:0.##}% · B {frame.B * 100:0.##}% · C {frame.C * 100:0.##}% · D {frame.D * 100:0.##}%");

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}
