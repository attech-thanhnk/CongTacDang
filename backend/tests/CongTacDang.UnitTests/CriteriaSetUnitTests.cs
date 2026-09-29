using System.Globalization;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Task 16: bộ tiêu chí (kiểm tra nội dung, 2 bộ mặc định theo bản trích xuất HD03), tính điểm theo bộ (tiêu chí chung có K/AD,
/// làm tròn theo từng kiểu, trục 09B, gợi ý mức, trần xuất sắc, giải trình chênh lệch), TC-1/TC-2 của trích xuất mục 9 và
/// công thức A/B/C/D cho kết quả <b>bằng đúng</b> code trước khi có bộ tiêu chí (bản sao nguyên văn ở cuối file).
/// </summary>
public sealed class CriteriaSetUnitTests
{
    private static Dictionary<string, GeneralItemScore> AllAssured(CriteriaSetContent c) =>
        c.AllItems.ToDictionary(x => x.Item.Code, x => new GeneralItemScore { Score = x.Item.MaxScore });

    #region Bộ mặc định, kiểm tra nội dung

    [Fact]
    public void DefaultSets_FollowHd03Extraction_AndAreValid()
    {
        var b = CriteriaSetDefaults.Build09B();
        var a = CriteriaSetDefaults.Build09A();
        Assert.Empty(b.Validate(CriteriaSetContent.Form09B));
        Assert.Empty(a.Validate(CriteriaSetContent.Form09A));

        // Trích xuất mục 3.2: 3 nhóm 18/4/8, 17 tiêu chí con, tổng 30.
        Assert.Equal(new[] { 18.0, 4, 8 }, b.GeneralGroups.Select(g => g.MaxScore));
        Assert.Equal(17, b.AllItems.Count());
        Assert.Equal(new[] { "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "2.1", "2.2", "2.3", "2.4", "3.1", "3.2", "3.3", "3.4" },
            b.AllItems.Select(x => x.Item.Code));
        Assert.All(b.GeneralGroups, g => Assert.Equal(CriteriaScoringMode.Binary, g.ScoringMode));
        Assert.Equal(30.0, b.AllItems.Sum(x => x.Item.MaxScore));

        // Mục 3.3 + 8.3: 6 trục, điểm tối đa 09B 15/10/10/15/10/10 = 70; bộ 09A trục không có điểm riêng.
        Assert.Equal(new[] { "T1", "T2", "T3", "T4", "T5", "T6" }, b.Axes.Select(x => x.Code));
        Assert.Equal(new[] { 15.0, 10, 10, 15, 10, 10 }, b.Axes.Select(x => x.MaxScore));
        Assert.All(a.Axes, x => Assert.Equal(0.0, x.MaxScore));

        // Mục 3.6: 4 khung tỷ trọng; 3.7: 6 mức thang quy đổi; 4.1: ngưỡng 90/70/50/0, xuất sắc cần ≥ 30% vượt chuẩn.
        Assert.Equal(new[] { "K1", "K2", "K3", "K4" }, a.WeightFrames.Select(f => f.Code));
        var k2 = a.FindFrame("k2")!;
        Assert.Equal((0.15, 0.50, 0.15, 0.20), (k2.A, k2.B, k2.C, k2.D));
        Assert.Equal(new[] { 95.0, 90, 80, 65, 50, 0 }, a.ConversionScale.Select(s => s.MinPercent));
        Assert.Equal("90% - dưới 95%", a.BandFor(94.9)!.Label);
        Assert.Equal("95 - 100%", a.BandFor(95)!.Label);
        Assert.Equal(new[] { 90.0, 70, 50, 0 }, CriteriaSetContent.RankedGrades.Select(g => a.FindGrade(g)!.MinScore));
        Assert.Equal(0.30, a.FindGrade(EvaluationGrade.HoanThanhXuatSac)!.MinExceedStandardRatio);

        // Tham số: 3–7 sản phẩm, 70 điểm, ngưỡng giải trình 5, trần 20% số "Hoàn thành tốt", làm tròn 1 chữ số nửa lên.
        var p = a.Parameters;
        Assert.Equal((3, 7, 70.0, 30.0, 5.0), (p.MinTasks, p.MaxTasks, p.TotalTaskWeight, p.GeneralMaxScore, p.ExplanationThreshold));
        Assert.Equal((0.20, QuotaDenominator.GoodOnly, ScoreRoundingMode.HalfUp), (p.ExcellentQuota.Ratio, p.ExcellentQuota.Denominator, p.ExcellentQuota.Rounding));
        Assert.Equal(NotApplicableRule.ExcludeAndRescale, p.NotApplicableRule);
        Assert.All(new[] { p.Rounding.TaskScore, p.Rounding.TasksTotal, p.Rounding.GeneralTotal, p.Rounding.Total },
            r => Assert.Equal((1, ScoreRoundingMode.HalfUp), (r.Decimals, r.Mode)));
    }

    [Fact]
    public void Validate_RejectsInvalidContent()
    {
        static List<string> Errors(Action<CriteriaSetContent> change, string form = CriteriaSetContent.Form09B)
        {
            var content = CriteriaSetDefaults.Build09B();
            change(content);
            return content.Normalize().Validate(form);
        }

        Assert.Contains(Errors(c => c.GeneralGroups[0].Items[1].Code = "1.1"), e => e.Contains("\"1.1\" bị trùng", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.GeneralGroups[0].Items[0].Code = "1 1"), e => e.Contains("không hợp lệ", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.GeneralGroups[0].Items[0].MaxScore = 3), e => e.Contains("(31)", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.GeneralGroups[0].Items[0].MaxScore = 0), e => e.Contains("lớn hơn 0", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.GeneralGroups[1].Items.Clear()), e => e.Contains("ít nhất một tiêu chí con", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.GeneralGroups.Clear()), e => e.Contains("nhóm tiêu chí chung", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Axes[0].MaxScore = 16), e => e.Contains("tổng điểm tối đa các trục (71)", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Axes[0].MaxScore = 0), e => e.Contains("phải lớn hơn 0", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Axes[1].Code = "T1"), e => e.Contains("trục kết quả \"T1\" bị trùng", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.WeightFrames[0].A = 0.3), e => e.Contains("khung \"K1\"", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.WeightFrames.Clear(), CriteriaSetContent.Form09A), e => e.Contains("ít nhất một khung", StringComparison.Ordinal));
        Assert.Empty(Errors(c => { c.WeightFrames.Clear(); c.Parameters.DefaultWeightFrameCode = null; }));
        Assert.Contains(Errors(c => c.Parameters.DefaultWeightFrameCode = "K9"), e => e.Contains("\"K9\"", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.ConversionScale.RemoveAll(s => s.MinPercent == 0)), e => e.Contains("từ 0%", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Grades.RemoveAt(1)), e => e.Contains("Hoàn thành tốt nhiệm vụ", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Grades[1].MinScore = 95), e => e.Contains("giảm dần", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Grades[3].MinScore = 10), e => e.Contains("phải là 0", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Grades.Add(new GradeRule { Grade = EvaluationGrade.ChuaXepLoai })), e => e.Contains("không dùng được", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Parameters.MaxTasks = 2), e => e.Contains("tối đa", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Parameters.Rounding.Total.Decimals = 5), e => e.Contains("Làm tròn tổng điểm", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.Parameters.ExcellentQuota.Ratio = 1.5), e => e.Contains("Trần tỷ lệ", StringComparison.Ordinal));
        Assert.Contains(Errors(c => c.SchemaVersion = 2), e => e.Contains("Phiên bản nội dung", StringComparison.Ordinal));
        Assert.Contains(Errors(_ => { }, "09C"), e => e.Contains("Mẫu tự chấm", StringComparison.Ordinal));
    }

    [Fact]
    public void Content_And_Snapshot_RoundTripJson_WithEnumsAsStrings()
    {
        var content = CriteriaSetDefaults.Build09A();
        var json = content.ToJson();
        Assert.Contains("\"scoringMode\":\"Binary\"", json);
        Assert.Contains("\"grade\":\"HoanThanhXuatSac\"", json);
        Assert.Contains("\"notApplicableRule\":\"ExcludeAndRescale\"", json);
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Equal(json, CriteriaSetContent.Parse(json).ToJson());
        Assert.Throws<FormatException>(() => CriteriaSetContent.Parse("{sai"));

        var snapshot = new CriteriaSnapshot { SetId = Guid.NewGuid(), Code = "X", Name = "Bộ X", SelfScoreForm = "09B", TakenAt = DateTime.UnixEpoch, Content = content };
        var parsed = CriteriaSnapshot.Parse(snapshot.ToJson())!;
        Assert.True(parsed.UsesAxisScoring);
        Assert.Equal(content.ToJson(), parsed.Content.ToJson());
        Assert.Null(CriteriaSnapshot.Parse(null));
    }

    #endregion

    #region Tiêu chí chung, K/AD

    [Fact]
    public void GeneralScores_Binary_AllAssured_Is30_AndValidationMessages()
    {
        var c = CriteriaSetDefaults.Build09B();
        var scores = AllAssured(c);
        Assert.Null(EvaluationScoring.ValidateGeneralScores(c, scores));
        Assert.Equal(30.0, EvaluationScoring.GeneralCriteriaScore(c, scores));

        Assert.Contains("17 tiêu chí con", EvaluationScoring.ValidateGeneralScores(c, new Dictionary<string, GeneralItemScore>()));
        var missing = AllAssured(c);
        missing.Remove("3.4");
        Assert.Contains("Chưa chấm tiêu chí 3.4", EvaluationScoring.ValidateGeneralScores(c, missing));
        var unknown = AllAssured(c);
        unknown["9.9"] = new GeneralItemScore { Score = 1 };
        Assert.Contains("\"9.9\"", EvaluationScoring.ValidateGeneralScores(c, unknown));

        // "Đảm bảo" = tối đa, "Không đảm bảo" = 0; giá trị khác bị từ chối.
        var half = AllAssured(c);
        half["1.1"] = new GeneralItemScore { Score = 1, Reason = "x" };
        Assert.Contains("\"Đảm bảo\"", EvaluationScoring.ValidateGeneralScores(c, half));

        // Khoản giảm ≥ 1 điểm phải có căn cứ (HD03 tr.10).
        var notAssured = AllAssured(c);
        notAssured["1.1"] = new GeneralItemScore { Score = 0 };
        Assert.Contains("căn cứ", EvaluationScoring.ValidateGeneralScores(c, notAssured));
        notAssured["1.1"].Reason = "Có vi phạm được kết luận";
        Assert.Null(EvaluationScoring.ValidateGeneralScores(c, notAssured));
        Assert.Equal(28.0, EvaluationScoring.GeneralCriteriaScore(c, notAssured));
        c.Parameters.DeductionReasonMinPoints = null;
        notAssured["1.1"].Reason = null;
        Assert.Null(EvaluationScoring.ValidateGeneralScores(c, notAssured));

        // Chấm theo khoảng: nhận 0..tối đa.
        c.GeneralGroups[0].ScoringMode = CriteriaScoringMode.Range;
        half["1.1"] = new GeneralItemScore { Score = 1.5 };
        Assert.Null(EvaluationScoring.ValidateGeneralScores(c, half));
        Assert.Equal(29.5, EvaluationScoring.GeneralCriteriaScore(c, half));
        half["1.1"] = new GeneralItemScore { Score = 2.5 };
        Assert.Contains("từ 0 đến 2", EvaluationScoring.ValidateGeneralScores(c, half));
    }

    [Fact]
    public void GeneralScores_NotApplicable_ExcludeAndRescale_Or_GrantFull()
    {
        var c = CriteriaSetDefaults.Build09B();
        var scores = AllAssured(c);
        scores["1.1"] = new GeneralItemScore { NotApplicable = true };
        Assert.Contains("nêu lý do", EvaluationScoring.ValidateGeneralScores(c, scores));
        scores["1.1"].Reason = "Không thuộc phạm vi chức trách";
        Assert.Null(EvaluationScoring.ValidateGeneralScores(c, scores));

        // K/AD 1.1, còn lại đạt tối đa → 28/28 × 30 = 30.
        Assert.Equal(30.0, EvaluationScoring.GeneralCriteriaScore(c, scores));
        // Thêm 1.2 không đảm bảo (có căn cứ) → 26/28 × 30 = 27,857… → 27,9 (1 chữ số, nửa lên).
        scores["1.2"] = new GeneralItemScore { Score = 0, Reason = "Căn cứ" };
        Assert.Equal(27.9, EvaluationScoring.GeneralCriteriaScore(c, scores));

        c.Parameters.NotApplicableRule = NotApplicableRule.GrantFull;
        Assert.Equal(28.0, EvaluationScoring.GeneralCriteriaScore(c, scores));

        c.Parameters.AllowNotApplicable = false;
        Assert.Contains("không cho phép", EvaluationScoring.ValidateGeneralScores(c, scores));

        // Mọi tiêu chí K/AD → không có gì để trừ: điểm tối đa nhóm.
        c.Parameters.NotApplicableRule = NotApplicableRule.ExcludeAndRescale;
        var allNa = c.AllItems.ToDictionary(x => x.Item.Code, _ => new GeneralItemScore { NotApplicable = true, Reason = "r" });
        Assert.Equal(30.0, EvaluationScoring.GeneralCriteriaScore(c, allNa));

        // Lưu JSON theo thứ tự bộ, lý do cắt khoảng trắng; đọc lại không phân biệt hoa thường.
        var json = EvaluationScoring.GeneralScoresToJson(c, scores);
        Assert.StartsWith("{\"1.1\":{\"score\":0,\"notApplicable\":true,\"reason\":\"Không thuộc phạm vi chức trách\"}", json);
        Assert.True(EvaluationScoring.ParseGeneralScores(json)["1.2"].Score == 0);
    }

    #endregion

    #region Làm tròn

    [Theory]
    [InlineData(29.25, 1, ScoreRoundingMode.HalfUp, 29.3)]
    [InlineData(29.25, 1, ScoreRoundingMode.HalfEven, 29.2)]
    [InlineData(29.25, 1, ScoreRoundingMode.Truncate, 29.2)]
    [InlineData(9.55, 1, ScoreRoundingMode.HalfUp, 9.6)]
    [InlineData(9.55, 1, ScoreRoundingMode.HalfEven, 9.6)]
    [InlineData(9.45, 1, ScoreRoundingMode.HalfEven, 9.4)]
    [InlineData(4.575, 2, ScoreRoundingMode.HalfUp, 4.58)]
    [InlineData(4.575, 2, ScoreRoundingMode.HalfEven, 4.58)]
    [InlineData(0.125, 2, ScoreRoundingMode.HalfEven, 0.12)]
    [InlineData(0.125, 2, ScoreRoundingMode.HalfUp, 0.13)]
    [InlineData(4.579, 2, ScoreRoundingMode.Truncate, 4.57)]
    [InlineData(67.475, 1, ScoreRoundingMode.HalfUp, 67.5)]
    [InlineData(0.5, 0, ScoreRoundingMode.HalfUp, 1)]
    [InlineData(2.5, 0, ScoreRoundingMode.HalfEven, 2)]
    [InlineData(-1.25, 1, ScoreRoundingMode.HalfUp, -1.3)]
    public void Round_ByDecimalsAndMode(double value, int decimals, ScoreRoundingMode mode, double expected)
    {
        Assert.Equal(expected, EvaluationScoring.Round(value, new RoundingRule(decimals, mode)));
    }

    [Fact]
    public void Round_NoBinaryNoise_OnComputedProducts()
    {
        // 10 × (1×0,15 + 0,95×0,5 + 1×0,15 + 0,9×0,2) = 9,55 (tính trên double có thể là 9,549999…) → 9,6 như văn bản.
        var frame = new WeightFrame { Code = "K2", A = 0.15, B = 0.50, C = 0.15, D = 0.20 };
        Assert.Equal(9.6, EvaluationScoring.ScoreTask(10, 1.0, 0.95, 1.0, 0.90, frame, new RoundingRule(1, ScoreRoundingMode.HalfUp)).Score);
        Assert.Equal(4.6, EvaluationScoring.ScoreTask(5, 1.0, 0.90, 0.90, 0.90, frame, new RoundingRule(1, ScoreRoundingMode.HalfUp)).Score);
    }

    #endregion

    #region TC-1, TC-2 (trích xuất HD03 mục 9) với bộ mặc định

    /// <summary>
    /// TC-1 (PL II mục VI, Khung 2): với bộ mặc định (làm tròn điểm sản phẩm 1 chữ số nửa lên, cộng các điểm đã làm tròn) kết quả
    /// <b>trùng văn bản</b>: 29,3 / 14,1 / 10,0 / 9,6 / 4,6 → 67,6; tổng 30 + 67,6 = 97,6; vượt chuẩn 2/5 = 40% ≥ 30% → HTXS.
    /// </summary>
    [Fact]
    public void TC1_DefaultSet_MatchesHd03Example()
    {
        var c = CriteriaSetDefaults.Build09A();
        var frame = c.FindFrame("K2")!;
        var r = c.Parameters.Rounding;
        var sp = new (double W, double A, double B, double C, double D, double Pct, double Score, bool Exceed)[]
        {
            (30, 1.00, 0.95, 1.00, 1.00, 97.5, 29.3, true),
            (15, 1.00, 0.90, 1.00, 0.95, 94.0, 14.1, false),
            (10, 1, 1, 1, 1, 100.0, 10.0, true),
            (10, 1.00, 0.95, 1.00, 0.90, 95.5, 9.6, false),
            (5, 1.00, 0.90, 0.90, 0.90, 91.5, 4.6, false)
        };
        var results = sp.Select(s => EvaluationScoring.ScoreTask(s.W, s.A, s.B, s.C, s.D, frame, r.TaskScore)).ToList();
        for (var i = 0; i < sp.Length; i++)
        {
            Assert.Equal(sp[i].Pct, Math.Round(results[i].WeightedRatio * 100, 1));
            Assert.Equal(sp[i].Score, results[i].Score);
        }

        var tasks = EvaluationScoring.TasksScore(results.Select(x => x.Score), r.TasksTotal);
        Assert.Equal(67.6, tasks);
        var general = EvaluationScoring.GeneralCriteriaScore(c, AllAssured(c));
        Assert.Equal(30.0, general);
        var total = EvaluationScoring.TotalScore(general, tasks, r.Total);
        Assert.Equal(97.6, total);

        var exceed = EvaluationScoring.ExceedStandardRatio(sp.Count(s => s.Exceed), sp.Length);
        Assert.Equal(0.4, exceed);
        Assert.Equal(EvaluationGrade.HoanThanhXuatSac, EvaluationScoring.SuggestGrade(c, total, exceed));
    }

    /// <summary>TC-2: A = B = C = D = x → kết quả sản phẩm = x với mọi khung của bộ (tỷ trọng mỗi khung có tổng 1) và khung 25%.</summary>
    [Fact]
    public void TC2_EqualCriteria_GiveWeightTimesRatio_ForEveryFrame()
    {
        var c = CriteriaSetDefaults.Build09A();
        var frames = c.WeightFrames.Append(new WeightFrame { Code = "TW", A = 0.25, B = 0.25, C = 0.25, D = 0.25 });
        foreach (var frame in frames)
        {
            foreach (var x in new[] { 0.0, 0.45, 0.5, 0.8, 0.95, 1.0 })
            {
                var result = EvaluationScoring.ScoreTask(20, x, x, x, x, frame, c.Parameters.Rounding.TaskScore);
                Assert.Equal(x, result.WeightedRatio, 12);
                Assert.Equal(EvaluationScoring.Round(20 * x, c.Parameters.Rounding.TaskScore), result.Score);
            }
        }
    }

    #endregion

    #region Công thức A/B/C/D = code cũ

    /// <summary>
    /// Cấu trúc công thức giữ nguyên: với tỷ trọng khung bằng bảng cũ và làm tròn 2 chữ số nửa về chẵn (như <c>Math.Round</c> cũ),
    /// điểm sản phẩm, tổng nhiệm vụ, tổng điểm <b>bằng đúng</b> code trước khi có bộ tiêu chí; tỷ lệ có trọng số trùng từng bit.
    /// </summary>
    [Fact]
    public void AbcdFormula_WithOldWeightsAndRounding_IsIdenticalToOldCode_OnRandomInputs()
    {
        var old2 = new RoundingRule(2, ScoreRoundingMode.HalfEven);
        // Chỉ số 0 = tỷ trọng dự phòng 25% của code cũ; 1..4 = Khung 1..4.
        var frames = new[]
        {
            new WeightFrame { Code = "0", A = 0.25, B = 0.25, C = 0.25, D = 0.25 },
            new WeightFrame { Code = "1", A = 0.25, B = 0.35, C = 0.20, D = 0.20 },
            new WeightFrame { Code = "2", A = 0.15, B = 0.50, C = 0.15, D = 0.20 },
            new WeightFrame { Code = "3", A = 0.20, B = 0.30, C = 0.35, D = 0.15 },
            new WeightFrame { Code = "4", A = 0.15, B = 0.30, C = 0.20, D = 0.35 }
        };
        // Bảng tỷ trọng của bộ mặc định = bảng cũ (khung 1..4).
        var defaults = CriteriaSetDefaults.Build09A().WeightFrames;
        for (var i = 0; i < 4; i++)
            Assert.Equal((frames[i + 1].A, frames[i + 1].B, frames[i + 1].C, frames[i + 1].D), (defaults[i].A, defaults[i].B, defaults[i].C, defaults[i].D));

        var random = new Random(20260929);
        for (var i = 0; i < 20000; i++)
        {
            var group = random.Next(0, 5);
            var count = random.Next(1, 8);
            var tasks = Enumerable.Range(0, count).Select(_ => (
                Weight: Math.Round(random.NextDouble() * 40, random.Next(0, 3)),
                A: random.NextDouble() * 1.4 - 0.2, B: random.NextDouble() * 1.4 - 0.2,
                C: random.NextDouble() * 1.4 - 0.2, D: random.NextDouble() * 1.4 - 0.2)).ToList();

            var oldScores = tasks.Select(t => Old.TaskScore(t.Weight, t.A, t.B, t.C, t.D, group)).ToList();
            var results = tasks.Select(t => EvaluationScoring.ScoreTask(t.Weight, t.A, t.B, t.C, t.D, frames[group], old2)).ToList();
            Assert.Equal(tasks.Select(t => Old.WeightedRatio(t.A, t.B, t.C, t.D, group)), results.Select(r => r.WeightedRatio));
            Assert.Equal(oldScores, results.Select(r => r.Score));

            var general = Math.Round(random.NextDouble() * 30, random.Next(0, 3));
            var (oldTasks, oldTotal) = Old.Totals(general, oldScores);
            var newTasks = EvaluationScoring.TasksScore(results.Select(r => r.Score), old2);
            Assert.Equal(oldTasks, newTasks);
            Assert.Equal(oldTotal, EvaluationScoring.TotalScore(general, newTasks, old2));
        }
    }

    [Fact]
    public void Grades_And_Quota_WithOldParameters_EqualOldCode()
    {
        var c = CriteriaSetDefaults.Build09A();
        for (var score = -100; score <= 10100; score++)
            Assert.Equal(Old.Grade(score / 100.0), EvaluationScoring.GradeByScore(c, score / 100.0));

        // Cách tính cũ = mẫu số "tốt trở lên", làm tròn xuống.
        var oldRule = new ExcellentQuotaRule { Ratio = 0.20, Denominator = QuotaDenominator.GoodOrBetter, Rounding = ScoreRoundingMode.Truncate };
        for (var n = 0; n <= 1000; n++)
            Assert.Equal(Old.Quota(n), EvaluationScoring.ExcellentQuota(n, n / 3, oldRule));
    }

    #endregion

    #region Gợi ý mức, trần xuất sắc, giải trình, trục 09B, đăng ký

    [Fact]
    public void SuggestGrade_UsesThresholds_AndExceedStandardCondition()
    {
        var c = CriteriaSetDefaults.Build09A();
        Assert.Equal(EvaluationGrade.HoanThanhXuatSac, EvaluationScoring.SuggestGrade(c, 95, 0.3));
        Assert.Equal(EvaluationGrade.HoanThanhTot, EvaluationScoring.SuggestGrade(c, 95, 0.2)); // thiếu điều kiện vượt chuẩn → mức kế tiếp
        Assert.Equal(EvaluationGrade.HoanThanhXuatSac, EvaluationScoring.SuggestGrade(c, 90, null)); // 09B: không có dữ liệu nhiệm vụ
        Assert.Equal(EvaluationGrade.HoanThanhTot, EvaluationScoring.SuggestGrade(c, 89.9, 1));
        Assert.Equal(EvaluationGrade.HoanThanh, EvaluationScoring.SuggestGrade(c, 50, 0));
        Assert.Equal(EvaluationGrade.KhongHoanThanh, EvaluationScoring.SuggestGrade(c, 49.9, 0));

        c.Grades.Single(g => g.Grade == EvaluationGrade.HoanThanhTot).MinScore = 75;
        Assert.Equal(EvaluationGrade.HoanThanh, EvaluationScoring.SuggestGrade(c, 72, null));
    }

    /// <summary>TC-3 (trích xuất mục 9): trần = làm tròn(20% × số "Hoàn thành tốt"), 0,5 lên 1.</summary>
    [Theory]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(13, 3)]
    [InlineData(12, 2)]
    public void ExcellentQuota_Tc3_DefaultRule(int goodCount, int expected)
    {
        var rule = CriteriaSetDefaults.Build09A().Parameters.ExcellentQuota;
        // Mẫu số "Hoàn thành tốt" = tốt trở lên − xuất sắc: kết quả không phụ thuộc số xuất sắc đề xuất.
        Assert.Equal(expected, EvaluationScoring.ExcellentQuota(goodCount, 0, rule));
        Assert.Equal(expected, EvaluationScoring.ExcellentQuota(goodCount + 4, 4, rule));
        rule.Ratio = 0.25;
        if (goodCount == 12)
            Assert.Equal(3, EvaluationScoring.ExcellentQuota(goodCount, 0, rule));
    }

    /// <summary>TC-5 (trích xuất mục 9): chênh ≥ 5 hoặc làm đổi mức → bắt buộc giải trình.</summary>
    [Fact]
    public void RequiresExplanation_Tc5()
    {
        var c = CriteriaSetDefaults.Build09A();
        Assert.True(EvaluationScoring.RequiresExplanation(c, 88, 83));
        Assert.True(EvaluationScoring.RequiresExplanation(c, 83, 88));
        Assert.True(EvaluationScoring.RequiresExplanation(c, 91, 89));
        Assert.False(EvaluationScoring.RequiresExplanation(c, 88, 85));
        Assert.False(EvaluationScoring.RequiresExplanation(c, 88, null));
        c.Parameters.ExplanationOnGradeChange = false;
        Assert.False(EvaluationScoring.RequiresExplanation(c, 91, 89));
        c.Parameters.ExplanationThreshold = 3;
        Assert.True(EvaluationScoring.RequiresExplanation(c, 88, 85));
    }

    [Fact]
    public void AxisScores_09B_ValidateAgainstSet_AndSum()
    {
        var c = CriteriaSetDefaults.Build09B();
        var full = new Dictionary<string, double> { ["T1"] = 15, ["T2"] = 10, ["T3"] = 10, ["T4"] = 15, ["T5"] = 10, ["t6"] = 10 };
        Assert.Null(EvaluationScoring.ValidateAxisScores(c, full));
        Assert.Equal(70.0, EvaluationScoring.AxisTasksScore(c, full));
        Assert.Equal("{\"T1\":15,\"T2\":10,\"T3\":10,\"T4\":15,\"T5\":10,\"T6\":10}", EvaluationScoring.AxisScoresToJson(c, full));

        Assert.Contains("T1 phải từ 0 đến 15", EvaluationScoring.ValidateAxisScores(c, new Dictionary<string, double>(full) { ["T1"] = 15.5 }));
        var missing = new Dictionary<string, double>(full);
        missing.Remove("T3");
        Assert.Contains("Chưa chấm trục T3", EvaluationScoring.ValidateAxisScores(c, missing));
        Assert.Contains("\"T7\"", EvaluationScoring.ValidateAxisScores(c, new Dictionary<string, double>(full) { ["T7"] = 1 }));
        Assert.Contains("đủ 6 trục", EvaluationScoring.ValidateAxisScores(c, null));
    }

    [Fact]
    public void TaskRegistration_UsesSetParameters()
    {
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var p = CriteriaSetDefaults.Build09A().Parameters;
            Assert.Null(EvaluationScoring.ValidateTaskRegistration(new[] { 30.0, 20, 20 }, p));
            Assert.Null(EvaluationScoring.ValidateTaskRegistration(new[] { 30.0, 20, 19.96 }, p));
            Assert.Contains("từ 3 đến 7", EvaluationScoring.ValidateTaskRegistration(new[] { 70.0 }, p));
            Assert.Contains("70.0", EvaluationScoring.ValidateTaskRegistration(new[] { 30.0, 20, 19.9 }, p));
            p.MinTasks = 1;
            p.TotalTaskWeight = 60;
            Assert.Null(EvaluationScoring.ValidateTaskRegistration(new[] { 60.0 }, p));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    #endregion

    /// <summary>Bản sao nguyên văn công thức trước task 16 (EvaluationScoring/EvaluationParameters của task 12, commit d65d4df).</summary>
    private static class Old
    {
        private static (double wa, double wb, double wc, double wd) Weights(int group) => group switch
        {
            1 => (0.25, 0.35, 0.20, 0.20),
            2 => (0.15, 0.50, 0.15, 0.20),
            3 => (0.20, 0.30, 0.35, 0.15),
            4 => (0.15, 0.30, 0.20, 0.35),
            _ => (0.25, 0.25, 0.25, 0.25)
        };

        public static double WeightedRatio(double a, double b, double c, double d, int group)
        {
            var (wA, wB, wC, wD) = Weights(group == 0 ? 99 : group);
            var ra = Math.Clamp(a, 0.0, 1.0);
            var rb = Math.Clamp(b, 0.0, 1.0);
            var rc = Math.Clamp(c, 0.0, 1.0);
            var rd = Math.Clamp(d, 0.0, 1.0);
            double weightedRatio = (ra * wA) +
                                   (rb * wB) +
                                   (rc * wC) +
                                   (rd * wD);
            return weightedRatio;
        }

        public static double TaskScore(double weight, double a, double b, double c, double d, int group) =>
            Math.Round(weight * WeightedRatio(a, b, c, d, group), 2);

        public static (double Tasks, double Total) Totals(double general, List<double> taskScores)
        {
            double totalTaskScore = 0.0;
            foreach (var score in taskScores)
                totalTaskScore += score;
            var tasks = Math.Round(totalTaskScore, 2);
            return (tasks, Math.Round(general + tasks, 2));
        }

        public static EvaluationGrade Grade(double score)
        {
            if (score >= 90.0) return EvaluationGrade.HoanThanhXuatSac;
            if (score >= 70.0) return EvaluationGrade.HoanThanhTot;
            if (score >= 50.0) return EvaluationGrade.HoanThanh;
            return EvaluationGrade.KhongHoanThanh;
        }

        public static int Quota(int goodOrBetter) => (int)Math.Floor(goodOrBetter * 0.20);
    }
}
