using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Kết quả chấm điểm một nhiệm vụ (Mẫu 02).</summary>
/// <param name="A">Tỷ lệ tiêu chí A sau khi giới hạn 0..1.</param>
/// <param name="B">Tỷ lệ tiêu chí B sau khi giới hạn 0..1.</param>
/// <param name="C">Tỷ lệ tiêu chí C sau khi giới hạn 0..1.</param>
/// <param name="D">Tỷ lệ tiêu chí D sau khi giới hạn 0..1.</param>
/// <param name="WeightedRatio">Kết quả thực hiện sản phẩm (tỷ lệ, chưa làm tròn) = A×tA + B×tB + C×tC + D×tD.</param>
/// <param name="Score">Điểm của nhiệm vụ (đã làm tròn theo bộ tiêu chí).</param>
public sealed record TaskScoreResult(double A, double B, double C, double D, double WeightedRatio, double Score);

/// <summary>Điểm một tiêu chí con của nhóm tiêu chí chung trên hồ sơ.</summary>
public sealed class GeneralItemScore
{
    /// <summary>Điểm đạt (bỏ qua khi "Không áp dụng").</summary>
    public double Score { get; set; }

    /// <summary>"K/AD — Không áp dụng".</summary>
    public bool NotApplicable { get; set; }

    /// <summary>Lý do "Không áp dụng" / căn cứ giảm điểm.</summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Công thức tính điểm — thuần, nhận bộ tiêu chí của kỳ (<see cref="CriteriaSetContent"/>). <b>Cấu trúc công thức A/B/C/D giữ
/// nguyên</b> như trước khi có bộ tiêu chí (tỷ lệ giới hạn 0..1 × tỷ trọng khung × trọng số; tổng nhiệm vụ = cộng dồn điểm sản
/// phẩm đã làm tròn; tổng = chung + nhiệm vụ); bộ tiêu chí chỉ cung cấp tỷ trọng, ngưỡng, danh mục và quy tắc làm tròn.
/// </summary>
public static class EvaluationScoring
{
    /// <summary>Tùy chọn JSON cho điểm trên hồ sơ (camelCase).</summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #region Làm tròn

    /// <summary>
    /// Làm tròn theo quy tắc. <see cref="ScoreRoundingMode.HalfUp"/> và <see cref="ScoreRoundingMode.Truncate"/> tính trên
    /// <see cref="decimal"/> (chuyển từ double lấy 15 chữ số có nghĩa) để 9,55 → 9,6 như ví dụ HD03, không bị sai số nhị phân.
    /// <see cref="ScoreRoundingMode.HalfEven"/> giữ đúng <c>Math.Round(double, n)</c> của phiên bản trước (làm tròn ngân hàng trên
    /// giá trị nhị phân) để cấu hình "2 chữ số, nửa về chẵn" cho kết quả trùng từng bit với code cũ.
    /// </summary>
    public static double Round(double value, RoundingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > 1e12)
            return value;
        var decimals = Math.Clamp(rule.Decimals, 0, 4);
        return rule.Mode switch
        {
            ScoreRoundingMode.HalfEven => Math.Round(value, decimals, MidpointRounding.ToEven),
            ScoreRoundingMode.Truncate => (double)Math.Round((decimal)value, decimals, MidpointRounding.ToZero),
            _ => (double)Math.Round((decimal)value, decimals, MidpointRounding.AwayFromZero)
        };
    }

    #endregion

    #region Đăng ký nhiệm vụ (Mẫu 01)

    /// <summary>
    /// Kiểm tra danh mục đăng ký (số lượng, tổng trọng số). Trả thông báo lỗi hoặc null nếu hợp lệ.
    /// </summary>
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

    /// <summary>
    /// Kiểm tra điểm tiêu chí chung theo bộ: đủ mọi tiêu chí con, không có mã lạ; "Đảm bảo/Không đảm bảo" chỉ nhận 0 hoặc điểm
    /// tối đa; chấm theo khoảng nhận 0..tối đa; "K/AD" chỉ khi bộ cho phép và phải có lý do; khoản giảm ≥ ngưỡng phải có căn cứ.
    /// Trả thông báo lỗi hoặc null.
    /// </summary>
    public static string? ValidateGeneralScores(CriteriaSetContent criteria, IReadOnlyDictionary<string, GeneralItemScore>? scores)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var items = criteria.AllItems.ToList();
        if (scores == null || scores.Count == 0)
            return $"Hãy chấm đủ {items.Count} tiêu chí con của nhóm tiêu chí chung.";

        var known = new HashSet<string>(items.Select(x => x.Item.Code), StringComparer.OrdinalIgnoreCase);
        var unknown = scores.Keys.FirstOrDefault(k => !known.Contains(k));
        if (unknown != null)
            return $"Tiêu chí \"{unknown}\" không có trong bộ tiêu chí của kỳ.";

        var p = criteria.Parameters;
        foreach (var (group, item) in items)
        {
            var score = Find(scores, item.Code);
            if (score == null)
                return $"Chưa chấm tiêu chí {item.Code} \"{Short(item.Text)}\".";

            if (score.NotApplicable)
            {
                if (!p.AllowNotApplicable)
                    return $"Bộ tiêu chí của kỳ không cho phép đánh dấu \"Không áp dụng\" (tiêu chí {item.Code}).";
                if (string.IsNullOrWhiteSpace(score.Reason))
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
            if (p.DeductionReasonMinPoints is { } minDeduction
                && item.MaxScore - score.Score + CriteriaSetContent.Epsilon >= minDeduction
                && string.IsNullOrWhiteSpace(score.Reason))
            {
                return string.Create(CultureInfo.InvariantCulture,
                    $"Tiêu chí {item.Code} bị giảm {item.MaxScore - score.Score:0.##} điểm: hãy nêu căn cứ giảm điểm (khoản giảm từ {minDeduction:0.##} điểm trở lên).");
            }
        }

        return null;
    }

    /// <summary>
    /// Điểm nhóm tiêu chí chung = tổng điểm tiêu chí con; có "K/AD": <see cref="NotApplicableRule.GrantFull"/> tính tối đa,
    /// <see cref="NotApplicableRule.ExcludeAndRescale"/> = điểm đạt / tổng tối đa các tiêu chí áp dụng × điểm tối đa nhóm
    /// (không tiêu chí nào áp dụng → điểm tối đa). Làm tròn theo quy tắc "tổng điểm tiêu chí chung".
    /// </summary>
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

    /// <summary>
    /// Điểm một nhiệm vụ: tỷ lệ A-B-C-D giới hạn 0..1, nhân tỷ trọng khung, nhân trọng số, làm tròn theo quy tắc "điểm sản phẩm".
    /// </summary>
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

    /// <summary>Tổng điểm nhóm nhiệm vụ: cộng dồn điểm sản phẩm (đã làm tròn) theo thứ tự rồi làm tròn.</summary>
    public static double TasksScore(IEnumerable<double> taskScores, RoundingRule rounding)
    {
        double totalTaskScore = 0.0;
        foreach (var score in taskScores)
            totalTaskScore += score;
        return Round(totalTaskScore, rounding);
    }

    /// <summary>Tỷ lệ nhiệm vụ vượt chuẩn (0..1); không có nhiệm vụ → null.</summary>
    public static double? ExceedStandardRatio(int exceedCount, int taskCount) =>
        taskCount > 0 ? (double)exceedCount / taskCount : null;

    #endregion

    #region Trục kết quả (Mẫu 09B)

    /// <summary>Kiểm tra điểm theo trục (Mẫu 09B): đủ mọi trục của bộ, không có mã lạ, mỗi trục 0..điểm tối đa. Trả lỗi hoặc null.</summary>
    public static string? ValidateAxisScores(CriteriaSetContent criteria, IReadOnlyDictionary<string, double>? scores)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        if (scores == null || scores.Count == 0)
            return $"Tự chấm theo Mẫu 09B phải có điểm đủ {criteria.Axes.Count} trục ({string.Join(", ", criteria.Axes.Select(a => a.Code))}).";
        var unknown = scores.Keys.FirstOrDefault(k => criteria.FindAxis(k) == null);
        if (unknown != null)
            return $"Trục \"{unknown}\" không có trong bộ tiêu chí của kỳ.";
        foreach (var axis in criteria.Axes)
        {
            var found = scores.FirstOrDefault(kv => string.Equals(kv.Key, axis.Code, StringComparison.OrdinalIgnoreCase));
            if (found.Key == null)
                return $"Chưa chấm trục {axis.Code} \"{Short(axis.Name)}\".";
            if (double.IsNaN(found.Value) || found.Value < 0 || found.Value > axis.MaxScore + CriteriaSetContent.Epsilon)
                return string.Create(CultureInfo.InvariantCulture, $"Điểm trục {axis.Code} phải từ 0 đến {axis.MaxScore:0.##} điểm.");
        }
        return null;
    }

    /// <summary>Điểm nhóm kết quả theo Mẫu 09B = tổng điểm các trục (làm tròn theo quy tắc "tổng điểm nhiệm vụ").</summary>
    public static double AxisTasksScore(CriteriaSetContent criteria, IReadOnlyDictionary<string, double> scores)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(scores);
        return Round(scores.Values.Sum(), criteria.Parameters.Rounding.TasksTotal);
    }

    #endregion

    #region Tổng, xếp loại, trần, giải trình

    /// <summary>Tổng điểm tự chấm = chung + nhiệm vụ (làm tròn theo quy tắc "tổng điểm").</summary>
    public static double TotalScore(double generalCriteriaScore, double tasksScore, RoundingRule rounding) =>
        Round(generalCriteriaScore + tasksScore, rounding);

    /// <summary>Mức theo ngưỡng điểm của bộ (không xét điều kiện kèm theo).</summary>
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

    /// <summary>
    /// Mức gợi ý: xét từ mức cao xuống, lấy mức đầu tiên đạt ngưỡng điểm và điều kiện định lượng (tỷ lệ nhiệm vụ vượt chuẩn —
    /// chỉ khi có dữ liệu nhiệm vụ, <paramref name="exceedStandardRatio"/> khác null).
    /// </summary>
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

    /// <summary>
    /// Số Hoàn thành xuất sắc tối đa = làm tròn (tỷ lệ × mẫu số), mẫu số theo bộ: số "Hoàn thành tốt" (= tốt trở lên − xuất sắc)
    /// hoặc số "Hoàn thành tốt" trở lên.
    /// </summary>
    public static int ExcellentQuota(int goodOrBetterCount, int excellentCount, ExcellentQuotaRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var denominator = rule.Denominator == QuotaDenominator.GoodOrBetter
            ? goodOrBetterCount
            : Math.Max(0, goodOrBetterCount - excellentCount);
        return (int)Round(denominator * rule.Ratio, new RoundingRule(0, rule.Rounding));
    }

    /// <summary>
    /// Bắt buộc giải trình chênh lệch khi thẩm định: |tự chấm − thẩm định| ≥ ngưỡng, hoặc (nếu bộ bật) chênh lệch làm đổi mức
    /// theo ngưỡng điểm.
    /// </summary>
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

    /// <summary>Đọc điểm tiêu chí chung đã lưu (khóa = mã tiêu chí con).</summary>
    public static Dictionary<string, GeneralItemScore> ParseGeneralScores(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? new Dictionary<string, GeneralItemScore>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, GeneralItemScore>(
                JsonSerializer.Deserialize<Dictionary<string, GeneralItemScore>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Ghi điểm tiêu chí chung ra JSON theo thứ tự tiêu chí của bộ (lý do đã cắt khoảng trắng).</summary>
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

    /// <summary>Đọc điểm theo trục đã lưu (khóa = mã trục); null nếu chưa chấm theo trục.</summary>
    public static Dictionary<string, double>? ParseAxisScores(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : new Dictionary<string, double>(JsonSerializer.Deserialize<Dictionary<string, double>>(json, JsonOptions) ?? new(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Ghi điểm theo trục ra JSON theo thứ tự trục của bộ.</summary>
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
