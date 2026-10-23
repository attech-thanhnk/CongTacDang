using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Trạng thái tự chấm A-B-C-D của nhiệm vụ (09A) và tính lại điểm khi khung tỷ trọng đổi.
/// "Tỷ lệ đều 1.0, điểm 0, trọng số &gt; 0" nghĩa là chưa nhập; mọi chỗ tính lại phải giữ nguyên trạng thái này.</summary>
public static class TaskSelfScores
{
    private const double Tolerance = 1e-9;

    /// <summary>Nhiệm vụ còn hiệu lực chưa nhập tỷ lệ; việc đã loại trừ / chuyển tiếp không tính.</summary>
    public static bool IsPending(EvaluationTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return TaskChangeRules.IsActive(task)
            && task.Weight > Tolerance
            && Math.Abs(task.SelfScore) < Tolerance
            && IsOne(task.CriteriaA_Ratio) && IsOne(task.CriteriaB_Ratio)
            && IsOne(task.CriteriaC_Ratio) && IsOne(task.CriteriaD_Ratio);
    }

    /// <summary>Tính lại nhiệm vụ đã nhập theo khung mới (giữ tỷ lệ đã lưu); trả số nhiệm vụ đã tính lại.</summary>
    public static int Rescore(IEnumerable<EvaluationTask> tasks, WeightFrame frame, RoundingRule rounding)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(frame);
        var count = 0;
        foreach (var task in tasks.Where(t => !t.IsDeleted && TaskChangeRules.IsActive(t) && !IsPending(t)))
        {
            task.SelfScore = EvaluationScoring.ScoreTask(task.Weight, task.CriteriaA_Ratio, task.CriteriaB_Ratio,
                task.CriteriaC_Ratio, task.CriteriaD_Ratio, frame, rounding).Score;
            count++;
        }
        return count;
    }

    private static bool IsOne(double value) => Math.Abs(value - 1.0) < Tolerance;
}
