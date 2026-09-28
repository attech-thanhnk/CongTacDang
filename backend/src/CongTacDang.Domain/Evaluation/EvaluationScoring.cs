using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Kết quả chấm điểm một nhiệm vụ (Mẫu 02).</summary>
/// <param name="A">Tỷ lệ tiêu chí A sau khi giới hạn 0..1.</param>
/// <param name="B">Tỷ lệ tiêu chí B sau khi giới hạn 0..1.</param>
/// <param name="C">Tỷ lệ tiêu chí C sau khi giới hạn 0..1.</param>
/// <param name="D">Tỷ lệ tiêu chí D sau khi giới hạn 0..1.</param>
/// <param name="Score">Điểm của nhiệm vụ.</param>
public sealed record TaskScoreResult(double A, double B, double C, double D, double Score);

/// <summary>
/// Công thức tính điểm tách nguyên văn từ <c>EvaluationService</c> (trước task 12) — thuần, nhận <see cref="EvaluationParameters"/>.
/// <b>Không đổi công thức</b>: với tham số mặc định, kết quả trùng từng bit với code cũ (có unit test so sánh).
/// </summary>
public static class EvaluationScoring
{
    /// <summary>Số tiêu chí chung T1..T6 (cố định theo cấu trúc dữ liệu hồ sơ, không phải tham số).</summary>
    public const int GeneralCriteriaCount = 6;

    /// <summary>Số trục kết quả của Mẫu 09B.</summary>
    public const int AxisCount = 6;

    /// <summary>
    /// Kiểm tra danh mục đăng ký (số lượng, tổng trọng số). Trả thông báo lỗi hoặc null nếu hợp lệ.
    /// Gốc: <c>EvaluationService.RegisterTasksAsync</c> (kiểm tra 3–7 việc, tổng 70,0 ± 0,05 sau khi làm tròn 2 chữ số).
    /// </summary>
    public static string? ValidateTaskRegistration(IReadOnlyList<double> weights, EvaluationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (weights == null || weights.Count < parameters.MinTasks || weights.Count > parameters.MaxTasks)
            return $"Số lượng nhiệm vụ đăng ký phải từ {parameters.MinTasks} đến {parameters.MaxTasks} nhiệm vụ theo Hướng dẫn 03-HD/TVĐU.";

        double totalWeight = Math.Round(weights.Sum(), 2);
        if (Math.Abs(totalWeight - parameters.TotalTaskWeight) > parameters.TaskWeightTolerance)
            return string.Create(CultureInfo.InvariantCulture,
                $"Tổng trọng số của các nhiệm vụ phải bằng đúng {parameters.TotalTaskWeight:0.0} điểm. Hiện tại là: {totalWeight} điểm.");

        return null;
    }

    /// <summary>
    /// Kiểm tra điểm 6 tiêu chí chung. Trả thông báo lỗi hoặc null nếu hợp lệ.
    /// </summary>
    public static string? ValidateGeneralScores(IReadOnlyList<double>? scores, EvaluationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (scores == null || scores.Count != GeneralCriteriaCount)
            return "Điểm tiêu chí chung phải bao gồm đúng 6 tiêu chí (T1 đến T6).";

        for (int i = 0; i < GeneralCriteriaCount; i++)
        {
            if (scores[i] < 0 || scores[i] > parameters.GeneralCriterionMaxScore)
                return string.Create(CultureInfo.InvariantCulture, $"Điểm tiêu chí T{i + 1} phải từ 0.0 đến {parameters.GeneralCriterionMaxScore:0.0} điểm.");
        }

        return null;
    }

    /// <summary>Tổng điểm nhóm tiêu chí chung (làm tròn 2 chữ số).</summary>
    public static double GeneralCriteriaScore(IReadOnlyList<double> scores) => Math.Round(scores.Sum(), 2);

    /// <summary>Tỷ trọng A-B-C-D của khung chức danh.</summary>
    public static (double wa, double wb, double wc, double wd) JobGroupWeights(JobGroup group, EvaluationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var w = parameters.WeightsFor(group);
        return (w.A, w.B, w.C, w.D);
    }

    /// <summary>
    /// Điểm một nhiệm vụ: tỷ lệ A-B-C-D giới hạn 0..1, nhân tỷ trọng khung chức danh, nhân trọng số, làm tròn 2 chữ số.
    /// </summary>
    public static TaskScoreResult ScoreTask(double weight, double a, double b, double c, double d, JobGroup group, EvaluationParameters parameters)
    {
        var (wA, wB, wC, wD) = JobGroupWeights(group, parameters);
        var ra = Math.Clamp(a, 0.0, 1.0);
        var rb = Math.Clamp(b, 0.0, 1.0);
        var rc = Math.Clamp(c, 0.0, 1.0);
        var rd = Math.Clamp(d, 0.0, 1.0);

        double weightedRatio = (ra * wA) +
                               (rb * wB) +
                               (rc * wC) +
                               (rd * wD);

        return new TaskScoreResult(ra, rb, rc, rd, Math.Round(weight * weightedRatio, 2));
    }

    /// <summary>Tổng điểm sản phẩm chuyên môn: cộng dồn theo thứ tự nhiệm vụ rồi làm tròn 2 chữ số.</summary>
    public static double TasksScore(IEnumerable<double> taskScores)
    {
        double totalTaskScore = 0.0;
        foreach (var score in taskScores)
            totalTaskScore += score;
        return Math.Round(totalTaskScore, 2);
    }

    /// <summary>Tổng điểm tự chấm = chung + chuyên môn (làm tròn 2 chữ số).</summary>
    public static double TotalScore(double generalCriteriaScore, double tasksScore) =>
        Math.Round(generalCriteriaScore + tasksScore, 2);

    /// <summary>Mức xếp loại gợi ý từ tổng điểm (khi cá nhân không tự chọn mức).</summary>
    public static EvaluationGrade GradeFromScore(double score, EvaluationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (score >= parameters.ExcellentMinScore) return EvaluationGrade.HoanThanhXuatSac;
        if (score >= parameters.GoodMinScore) return EvaluationGrade.HoanThanhTot;
        if (score >= parameters.SatisfactoryMinScore) return EvaluationGrade.HoanThanh;
        return EvaluationGrade.KhongHoanThanh;
    }

    /// <summary>Số Hoàn thành xuất sắc tối đa được phép = làm tròn xuống (số Hoàn thành tốt trở lên × trần tỷ lệ).</summary>
    public static int ExcellentQuota(int goodOrBetterCount, EvaluationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return (int)Math.Floor(goodOrBetterCount * parameters.ExcellentQuotaRatio);
    }

    /// <summary>
    /// Kiểm tra điểm 6 trục (Mẫu 09B): đủ 6 giá trị, mỗi trục trong khoảng 0..điểm tối đa. Trả thông báo lỗi hoặc null.
    /// </summary>
    public static string? ValidateAxisScores(IReadOnlyList<double>? scores, EvaluationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (scores == null || scores.Count != AxisCount)
            return "Tự chấm theo Mẫu 09B phải có điểm đủ 6 trục (Trục 1 đến Trục 6).";
        for (int i = 0; i < AxisCount; i++)
        {
            var max = parameters.AxisMaxScores[i];
            if (scores[i] < 0 || scores[i] > max)
                return string.Create(CultureInfo.InvariantCulture, $"Điểm Trục {i + 1} phải từ 0 đến {max:0.#} điểm.");
        }
        return null;
    }

    /// <summary>Điểm nhóm kết quả thực hiện nhiệm vụ theo Mẫu 09B = tổng điểm 6 trục (làm tròn 2 chữ số).</summary>
    public static double AxisTasksScore(IReadOnlyList<double> scores) => Math.Round(scores.Sum(), 2);
}
