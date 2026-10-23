using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Kết quả chấm một nhiệm vụ (Mẫu 02): tỷ lệ A–D đã kẹp 0..1, <c>WeightedRatio</c> chưa làm tròn, <c>Score</c> đã làm tròn.</summary>
public sealed record TaskScoreResult(double A, double B, double C, double D, double WeightedRatio, double Score);

public sealed class GeneralItemScore
{
    /// <summary>Bỏ qua khi "Không áp dụng".</summary>
    public double Score { get; set; }

    public bool NotApplicable { get; set; }

    /// <summary>Lý do "Không áp dụng" hoặc căn cứ giảm điểm.</summary>
    public string? Reason { get; set; }
}

/// <summary>Công thức tính điểm. Cấu trúc A/B/C/D cố định trong code; bộ tiêu chí chỉ cung cấp tỷ trọng, ngưỡng,
/// danh mục và quy tắc làm tròn.</summary>
public static class EvaluationScoring
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Làm tròn

    /// <summary>Tính trên <see cref="decimal"/> để 9,55 → 9,6 (nửa lên), 2,355 → 2,36 (nửa về chẵn), không lệch do sai số nhị phân.</summary>
    public static double Round(double value, RoundingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1e12)
            return value;
        var decimals = Math.Clamp(rule.Decimals, 0, 4);
        return rule.Mode switch
        {
            ScoreRoundingMode.HalfEven => (double)Math.Round((decimal)value, decimals, MidpointRounding.ToEven),
            ScoreRoundingMode.Truncate => (double)Math.Round((decimal)value, decimals, MidpointRounding.ToZero),
            _ => (double)Math.Round((decimal)value, decimals, MidpointRounding.AwayFromZero)
        };
    }

    #endregion

    #region Đăng ký nhiệm vụ (Mẫu 01)

    /// <summary>Kiểm số lượng và tổng trọng số; trả lỗi hoặc null.</summary>
    public static string? ValidateTaskRegistration(IReadOnlyList<double> weights, CriteriaParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (weights == null || weights.Count < parameters.MinTasks || weights.Count > parameters.MaxTasks)
            return $"Số lượng nhiệm vụ đăng ký phải từ {parameters.MinTasks} đến {parameters.MaxTasks} nhiệm vụ theo bộ tiêu chí của kỳ.";

        double totalWeight = Math.Round(weights.Sum(), 2);
        if (Math.Abs(totalWeight - parameters.TotalTaskWeight) > parameters.TaskWeightTolerance)
            return string.Create(CultureInfo.InvariantCulture,
                $"Tổng trọng số của các nhiệm vụ phải bằng đúng {parameters.TotalTaskWeight:0.0} điểm. Hiện tại là: {totalWeight} điểm.");

        return null;
    }

    #endregion

    #region Tiêu chí chung

    /// <summary>Trả lỗi hoặc null. <paramref name="partial"/> = lưu nháp: chỉ kiểm định dạng, không bắt chấm đủ hay nêu lý do.</summary>
    public static string? ValidateGeneralScores(CriteriaSetContent criteria, IReadOnlyDictionary<string, GeneralItemScore>? scores,
        bool partial = false)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var items = criteria.AllItems.ToList();
        if (scores == null || scores.Count == 0)
            return partial ? null : $"Hãy chấm đủ {items.Count} tiêu chí con của nhóm tiêu chí chung.";

        var known = new HashSet<string>(items.Select(x => x.Item.Code), StringComparer.OrdinalIgnoreCase);
        var unknown = scores.Keys.FirstOrDefault(k => !known.Contains(k));
        if (unknown != null)
            return $"Tiêu chí \"{unknown}\" không có trong bộ tiêu chí của kỳ.";

        var p = criteria.Parameters;
        foreach (var (group, item) in items)
        {
            var score = Find(scores, item.Code);
            if (score == null)
            {
                if (partial)
                    continue;
                return $"Chưa chấm tiêu chí {item.Code} \"{Short(item.Text)}\".";
            }

            if (score.NotApplicable)
            {
                if (!p.AllowNotApplicable)
                    return $"Bộ tiêu chí của kỳ không cho phép đánh dấu \"Không áp dụng\" (tiêu chí {item.Code}).";
                if (!partial && string.IsNullOrWhiteSpace(score.Reason))
                    return $"Tiêu chí {item.Code} được đánh dấu \"K/AD — Không áp dụng\": hãy nêu lý do.";
                continue;
            }

            if (double.IsNaN(score.Score) || score.Score < 0 || score.Score > item.MaxScore + CriteriaSetContent.Epsilon)
                return string.Create(CultureInfo.InvariantCulture, $"Điểm tiêu chí {item.Code} phải từ 0 đến {item.MaxScore:0.##}.");
            if (group.ScoringMode == CriteriaScoringMode.Binary
                && Math.Abs(score.Score) > CriteriaSetContent.Epsilon && Math.Abs(score.Score - item.MaxScore) > CriteriaSetContent.Epsilon)
            {
                return string.Create(CultureInfo.InvariantCulture,
                    $"Tiêu chí {item.Code} chấm \"Đảm bảo\" ({item.MaxScore:0.##} điểm) hoặc \"Không đảm bảo\" (0 điểm).");
            }
            if (!partial && p.DeductionReasonMinPoints is { } minDeduction
                && item.MaxScore - score.Score + CriteriaSetContent.Epsilon >= minDeduction
                && string.IsNullOrWhiteSpace(score.Reason))
            {
                return string.Create(CultureInfo.InvariantCulture,
                    $"Tiêu chí {item.Code} bị giảm {item.MaxScore - score.Score:0.##} điểm: hãy nêu căn cứ giảm điểm (khoản giảm từ {minDeduction:0.##} điểm trở lên).");
            }
        }

        return null;
    }

    /// <summary>Có "K/AD": GrantFull tính tối đa; ExcludeAndRescale = điểm đạt / tối đa các tiêu chí áp dụng × tối đa nhóm
    /// (không tiêu chí nào áp dụng → điểm tối đa).</summary>
    public static double GeneralCriteriaScore(CriteriaSetContent criteria, IReadOnlyDictionary<string, GeneralItemScore> scores)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(scores);
        var p = criteria.Parameters;
        double achieved = 0, applicableMax = 0, notApplicableMax = 0;
        foreach (var (_, item) in criteria.AllItems)
        {
            var score = Find(scores, item.Code);
            if (score is { NotApplicable: true })
            {
                notApplicableMax += item.MaxScore;
                continue;
            }
            applicableMax += item.MaxScore;
            achieved += Math.Clamp(score?.Score ?? 0, 0, item.MaxScore);
        }

        double result;
        if (notApplicableMax <= 0)
            result = achieved;
        else if (p.NotApplicableRule == NotApplicableRule.GrantFull)
            result = achieved + notApplicableMax;
        else
            result = applicableMax > 0 ? achieved / applicableMax * (applicableMax + notApplicableMax) : applicableMax + notApplicableMax;

        return Round(result, p.Rounding.GeneralTotal);
    }

    #endregion

    #region Nhiệm vụ (Mẫu 02) — công thức A/B/C/D

    /// <summary>Tỷ lệ A-B-C-D kẹp 0..1 × tỷ trọng khung × trọng số, làm tròn theo quy tắc "điểm sản phẩm".</summary>
    public static TaskScoreResult ScoreTask(double weight, double a, double b, double c, double d, WeightFrame frame, RoundingRule rounding)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var ra = Math.Clamp(a, 0.0, 1.0);
        var rb = Math.Clamp(b, 0.0, 1.0);
        var rc = Math.Clamp(c, 0.0, 1.0);
        var rd = Math.Clamp(d, 0.0, 1.0);

        double weightedRatio = (ra * frame.A) +
                               (rb * frame.B) +
                               (rc * frame.C) +
                               (rd * frame.D);

        return new TaskScoreResult(ra, rb, rc, rd, weightedRatio, Round(weight * weightedRatio, rounding));
    }

    /// <summary>Cộng dồn điểm sản phẩm đã làm tròn rồi làm tròn lại.</summary>
    public static double TasksScore(IEnumerable<double> taskScores, RoundingRule rounding)
    {
        double totalTaskScore = 0.0;
        foreach (var score in taskScores)
            totalTaskScore += score;
        return Round(totalTaskScore, rounding);
    }

    /// <summary>Không có nhiệm vụ → null.</summary>
    public static double? ExceedStandardRatio(int exceedCount, int taskCount) =>
        taskCount > 0 ? (double)exceedCount / taskCount : null;

    #endregion

    #region Trục kết quả (Mẫu 09B)

    /// <summary>Trả lỗi hoặc null. <paramref name="partial"/> = lưu nháp: không bắt chấm đủ trục.</summary>
    public static string? ValidateAxisScores(CriteriaSetContent criteria, IReadOnlyDictionary<string, double>? scores, bool partial = false)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (scores == null || scores.Count == 0)
            return partial ? null : $"Tự chấm theo Mẫu 09B phải có điểm đủ {criteria.Axes.Count} trục ({string.Join(", ", criteria.Axes.Select(a => a.Code))}).";
        var unknown = scores.Keys.FirstOrDefault(k => criteria.FindAxis(k) == null);
        if (unknown != null)
            return $"Trục \"{unknown}\" không có trong bộ tiêu chí của kỳ.";
        foreach (var axis in criteria.Axes)
        {
            var found = scores.FirstOrDefault(kv => string.Equals(kv.Key, axis.Code, StringComparison.OrdinalIgnoreCase));
            if (found.Key == null)
            {
                if (partial)
                    continue;
                return $"Chưa chấm trục {axis.Code} \"{Short(axis.Name)}\".";
            }
            if (double.IsNaN(found.Value) || found.Value < 0 || found.Value > axis.MaxScore + CriteriaSetContent.Epsilon)
                return string.Create(CultureInfo.InvariantCulture, $"Điểm trục {axis.Code} phải từ 0 đến {axis.MaxScore:0.##} điểm.");
        }
        return null;
    }

    /// <summary>Tổng điểm các trục (Mẫu 09B).</summary>
    public static double AxisTasksScore(CriteriaSetContent criteria, IReadOnlyDictionary<string, double> scores)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(scores);
        return Round(scores.Values.Sum(), criteria.Parameters.Rounding.TasksTotal);
    }

    #endregion

    #region Tổng, xếp loại, trần, giải trình

    public static double TotalScore(double generalCriteriaScore, double tasksScore, RoundingRule rounding) =>
        Round(generalCriteriaScore + tasksScore, rounding);

    /// <summary>Chỉ theo ngưỡng điểm, không xét điều kiện kèm theo.</summary>
    public static EvaluationGrade GradeByScore(CriteriaSetContent criteria, double score)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        foreach (var grade in CriteriaSetContent.RankedGrades)
        {
            if (criteria.FindGrade(grade) is { } rule && score + CriteriaSetContent.Epsilon >= rule.MinScore)
                return grade;
        }
        return EvaluationGrade.KhongHoanThanh;
    }

    /// <summary>Mức cao nhất đạt ngưỡng điểm và tỷ lệ nhiệm vụ vượt chuẩn (chỉ xét khi <paramref name="exceedStandardRatio"/> khác null).</summary>
    public static EvaluationGrade SuggestGrade(CriteriaSetContent criteria, double score, double? exceedStandardRatio)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        foreach (var grade in CriteriaSetContent.RankedGrades)
        {
            var rule = criteria.FindGrade(grade);
            if (rule == null || score + CriteriaSetContent.Epsilon < rule.MinScore)
                continue;
            if (rule.MinExceedStandardRatio is { } minRatio && exceedStandardRatio is { } ratio && ratio + CriteriaSetContent.Epsilon < minRatio)
                continue;
            return grade;
        }
        return EvaluationGrade.KhongHoanThanh;
    }

    /// <summary>Làm tròn tỷ lệ × số "Hoàn thành tốt", 0,5 lên 1 (8 × 20% = 1,6 → 2; 10 × 25% = 2,5 → 3).
    /// Dùng <see cref="decimal"/> để không lệch ở mốc 0,5.</summary>
    public static int ExcellentQuota(int goodCount, double ratio)
    {
        if (goodCount <= 0 || !double.IsFinite(ratio) || ratio <= 0)
            return 0;
        return (int)Math.Round((decimal)ratio * goodCount, 0, MidpointRounding.AwayFromZero);
    }

    public static int ExcellentQuota(int goodCount, ExcellentQuotaRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return ExcellentQuota(goodCount, rule.EffectiveRatio);
    }

    /// <summary>So mức đề xuất với mức theo điểm (<see cref="SuggestGrade"/>): dương = cao hơn bấy nhiêu bậc, âm = thấp hơn,
    /// 0 = khớp hoặc chưa đề xuất.</summary>
    public static int CompareWithScoreGrade(CriteriaSetContent criteria, EvaluationGrade proposed, double score, double? exceedStandardRatio = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var proposedRank = Rank(proposed);
        if (proposedRank < 0)
            return 0;
        return Rank(SuggestGrade(criteria, score, exceedStandardRatio)) - proposedRank;
    }

    /// <summary>Đề xuất mức khác mức theo điểm (nâng hay hạ đều là làm đổi mức) phải nêu căn cứ khi bộ bật
    /// <see cref="CriteriaParameters.ExplanationOnGradeChange"/>.</summary>
    public static bool ProposalRequiresExplanation(CriteriaSetContent criteria, EvaluationGrade proposed, double score, double? exceedStandardRatio = null)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return criteria.Parameters.ExplanationOnGradeChange && CompareWithScoreGrade(criteria, proposed, score, exceedStandardRatio) != 0;
    }

    /// <summary>0 = cao nhất; -1 = không thuộc bốn mức.</summary>
    private static int Rank(EvaluationGrade grade)
    {
        for (var i = 0; i < CriteriaSetContent.RankedGrades.Count; i++)
        {
            if (CriteriaSetContent.RankedGrades[i] == grade)
                return i;
        }
        return -1;
    }

    /// <summary>|tự chấm − thẩm định| ≥ ngưỡng, hoặc (nếu bộ bật) chênh lệch làm đổi mức theo ngưỡng điểm.</summary>
    public static bool RequiresExplanation(CriteriaSetContent criteria, double selfScore, double? appraisalScore)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (appraisalScore is not { } appraisal)
            return false;
        var p = criteria.Parameters;
        if (Math.Abs(selfScore - appraisal) + CriteriaSetContent.Epsilon >= p.ExplanationThreshold && p.ExplanationThreshold > 0)
            return true;
        return p.ExplanationOnGradeChange && GradeByScore(criteria, selfScore) != GradeByScore(criteria, appraisal);
    }

    #endregion

    #region Lưu điểm trên hồ sơ (jsonb)

    /// <summary>Khóa = mã tiêu chí con.</summary>
    public static Dictionary<string, GeneralItemScore> ParseGeneralScores(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, GeneralItemScore>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, GeneralItemScore>(
                JsonSerializer.Deserialize<Dictionary<string, GeneralItemScore>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Theo thứ tự tiêu chí của bộ.</summary>
    public static string GeneralScoresToJson(CriteriaSetContent criteria, IReadOnlyDictionary<string, GeneralItemScore> scores)
    {
        var ordered = new Dictionary<string, GeneralItemScore>(StringComparer.Ordinal);
        foreach (var (_, item) in criteria.AllItems)
        {
            var score = Find(scores, item.Code);
            if (score == null)
                continue;
            ordered[item.Code] = new GeneralItemScore
            {
                Score = score.NotApplicable ? 0 : score.Score,
                NotApplicable = score.NotApplicable,
                Reason = string.IsNullOrWhiteSpace(score.Reason) ? null : score.Reason.Trim()
            };
        }
        return JsonSerializer.Serialize(ordered, JsonOptions);
    }

    /// <summary>Khóa = mã trục; null nếu chưa chấm theo trục.</summary>
    public static Dictionary<string, double>? ParseAxisScores(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : new Dictionary<string, double>(JsonSerializer.Deserialize<Dictionary<string, double>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Theo thứ tự trục của bộ.</summary>
    public static string AxisScoresToJson(CriteriaSetContent criteria, IReadOnlyDictionary<string, double> scores)
    {
        var ordered = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var axis in criteria.Axes)
        {
            var found = scores.FirstOrDefault(kv => string.Equals(kv.Key, axis.Code, StringComparison.OrdinalIgnoreCase));
            if (found.Key != null)
                ordered[axis.Code] = found.Value;
        }
        return JsonSerializer.Serialize(ordered, JsonOptions);
    }

    #endregion

    private static GeneralItemScore? Find(IReadOnlyDictionary<string, GeneralItemScore> scores, string code) =>
        scores.TryGetValue(code, out var direct)
            ? direct
            : scores.FirstOrDefault(kv => string.Equals(kv.Key, code, StringComparison.OrdinalIgnoreCase)).Value;

    private static string Short(string? text) =>
        string.IsNullOrEmpty(text) ? string.Empty : text.Length <= 60 ? text : text[..57] + "...";
}
