using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Trạng thái tự chấm A-B-C-D của từng nhiệm vụ (Mẫu 09A) và tính lại điểm nhiệm vụ khi khung tỷ trọng của hồ sơ đổi.
/// <para>
/// Nhiệm vụ mới (đăng ký Mẫu 01 hoặc việc đột xuất Mẫu 04) được tạo với tỷ lệ A-B-C-D = 1.0 và điểm 0. Một khi chủ hồ sơ đã nhập
/// (lưu nháp hoặc nộp) tỷ lệ cho nhiệm vụ, điểm được tính bằng <see cref="EvaluationScoring.ScoreTask"/>: nhập 100% cả bốn mặt cho
/// điểm bằng trọng số (&gt; 0), nhập tỷ lệ khác cho tỷ lệ khác 1.0. Vì vậy trạng thái "tỷ lệ đều 1.0, điểm 0, trọng số &gt; 0" chỉ
/// xảy ra khi nhiệm vụ chưa được nhập — không cần thêm cột để phân biệt "chưa nhập" với "nhập 100%".
/// Mọi chỗ tính lại điểm nhiệm vụ phải giữ nguyên nhiệm vụ chưa nhập (điểm 0) để trạng thái này không bị xóa.
/// </para>
/// </summary>
public static class TaskSelfScores
{
    private const double Tolerance = 1e-9;

    /// <summary>
    /// Nhiệm vụ còn hiệu lực mà chủ hồ sơ chưa nhập tỷ lệ A-B-C-D (vẫn ở giá trị khởi tạo). Việc đã loại trừ / chuyển tiếp không
    /// chấm nên không tính là "chưa nhập".
    /// </summary>
    public static bool IsPending(EvaluationTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return TaskChangeRules.IsActive(task)
            && task.Weight > Tolerance
            && Math.Abs(task.SelfScore) < Tolerance
            && IsOne(task.CriteriaA_Ratio) && IsOne(task.CriteriaB_Ratio)
            && IsOne(task.CriteriaC_Ratio) && IsOne(task.CriteriaD_Ratio);
    }

    /// <summary>
    /// Tính lại điểm tự chấm các nhiệm vụ đã nhập theo khung hiệu lực mới (tỷ lệ đã lưu giữ nguyên); nhiệm vụ chưa nhập hoặc không
    /// còn hiệu lực giữ nguyên. Trả số nhiệm vụ đã tính lại.
    /// </summary>
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
