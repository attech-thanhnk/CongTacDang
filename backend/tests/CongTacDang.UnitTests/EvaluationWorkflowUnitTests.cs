using System.Globalization;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Task 12: máy trạng thái hồ sơ (mọi trạng thái × hành động × cấu hình bật/tắt), <see cref="PeriodSettings"/>,
/// và <see cref="EvaluationScoring"/> cho kết quả <b>bằng đúng</b> code tính điểm trước khi tách (bản sao nguyên văn ở cuối file).
/// </summary>
public sealed class EvaluationWorkflowUnitTests
{
    private static readonly WorkflowStep[] Optional =
    {
        WorkflowStep.B1_REGISTER, WorkflowStep.B1_APPROVE, WorkflowStep.B2_CELL_CONFIRM, WorkflowStep.B3A_COLLECTIVE, WorkflowStep.B3C_DIRECTOR
    };

    /// <summary>Mọi tổ hợp bật/tắt của 5 bước tùy chọn (32 cấu hình), bước bắt buộc luôn bật.</summary>
    public static IEnumerable<IReadOnlySet<WorkflowStep>> AllConfigurations()
    {
        for (var mask = 0; mask < 1 << Optional.Length; mask++)
        {
            var set = new HashSet<WorkflowStep>(WorkflowSteps.Mandatory);
            for (var i = 0; i < Optional.Length; i++)
                if ((mask & (1 << i)) != 0)
                    set.Add(Optional[i]);
            yield return set;
        }
    }

    #region Máy trạng thái

    [Fact]
    public void StateMachine_EveryStatus_X_EveryCommand_X_EveryConfiguration_MatchesDesignRules()
    {
        var commands = WorkflowSteps.Ordered.SelectMany(step => new[]
        {
            WorkflowCommand.Complete(step),
            WorkflowCommand.Return(step),
            WorkflowCommand.Reopen(step)
        }).ToList();
        var checkedCases = 0;

        foreach (var enabled in AllConfigurations())
        {
            // Thứ tự các bước bật (chỉ số tăng dần) — oracle độc lập với code: dò mảng 1..9.
            var order = Enumerable.Range(1, 9).Select(i => (WorkflowStep)i).Where(enabled.Contains).ToList();
            foreach (var status in Enum.GetValues<RecordStatus>())
            {
                foreach (var command in commands)
                {
                    var actual = RecordStateMachine.Apply(status, enabled, command);
                    var expected = Oracle(status, order, command);
                    Assert.True(expected.HasValue == actual.Succeeded,
                        $"{status} × {command.Action} {command.Step}/{command.ReopenTarget} × [{string.Join(",", order)}]: {actual.Error}");
                    if (expected.HasValue)
                        Assert.Equal(expected.Value, actual.Status);
                    else
                    {
                        Assert.Equal(status, actual.Status);
                        Assert.False(string.IsNullOrWhiteSpace(actual.Error));
                    }
                    checkedCases++;
                }
            }
        }

        Assert.Equal(32 * 10 * 27, checkedCases);
    }

    /// <summary>Luật thiết kế mục 2 viết lại theo cách khác (theo số thứ tự) để đối chiếu.</summary>
    private static RecordStatus? Oracle(RecordStatus status, List<WorkflowStep> order, WorkflowCommand command)
    {
        static RecordStatus Waiting(WorkflowStep s) => (RecordStatus)(9 + (int)s); // B1_REGISTER=1 → 10 … B5_PUBLISH=9 → 18
        switch (command.Action)
        {
            case WorkflowAction.Complete:
                if (!order.Contains(command.Step) || status != Waiting(command.Step))
                    return null;
                var next = order.FirstOrDefault(s => (int)s > (int)command.Step);
                return next == default ? RecordStatus.Published : Waiting(next);
            case WorkflowAction.Return:
                WorkflowStep? target = command.Step switch
                {
                    WorkflowStep.B1_APPROVE => WorkflowStep.B1_REGISTER,
                    WorkflowStep.B2_CELL_CONFIRM or WorkflowStep.B3B_APPRAISAL => WorkflowStep.B2_SELF_SCORE,
                    _ => null
                };
                if (target == null || !order.Contains(command.Step) || !order.Contains(target.Value) || status != Waiting(command.Step))
                    return null;
                return Waiting(target.Value);
            case WorkflowAction.Reopen:
                var t = command.ReopenTarget!.Value;
                if (status != RecordStatus.Published || !order.Contains(t) || (int)t < (int)WorkflowStep.B2_SELF_SCORE)
                    return null;
                return Waiting(t);
            default:
                return null;
        }
    }

    [Fact]
    public void StateMachine_FullPreset_WalksNineSteps_InOrder()
    {
        var enabled = PeriodSettings.FullPreset().EnabledSteps();
        var status = RecordStateMachine.Initial(enabled);
        Assert.Equal(RecordStatus.AwaitingRegistration, status);
        foreach (var step in WorkflowSteps.Ordered)
        {
            var result = RecordStateMachine.Apply(status, enabled, WorkflowCommand.Complete(step));
            Assert.True(result.Succeeded, result.Error);
            status = result.Status;
        }
        Assert.Equal(RecordStatus.Published, status);
    }

    [Fact]
    public void StateMachine_TransitionPreset_StartsAtSelfScore_AndRejectsB1()
    {
        var enabled = PeriodSettings.TransitionQ3Preset().EnabledSteps();
        Assert.Equal(RecordStatus.AwaitingSelfScore, RecordStateMachine.Initial(enabled));
        var b1 = RecordStateMachine.Apply(RecordStatus.AwaitingRegistration, enabled, WorkflowCommand.Complete(WorkflowStep.B1_REGISTER));
        Assert.False(b1.Succeeded);
        Assert.Contains("không áp dụng", b1.Error);
    }

    [Fact]
    public void StateMachine_OutOfOrder_MessageNamesCurrentStep()
    {
        var result = RecordStateMachine.Apply(RecordStatus.AwaitingRegistration, PeriodSettings.FullPreset().EnabledSteps(),
            WorkflowCommand.Complete(WorkflowStep.B3B_APPRAISAL));
        Assert.False(result.Succeeded);
        Assert.Equal("Hồ sơ đang ở bước \"Chờ đăng ký sản phẩm\", không thể thực hiện \"Thẩm định\".", result.Error);
    }

    [Fact]
    public void StateMachine_DisabledSteps_AreSkippedWhenComputingNext()
    {
        var enabled = new HashSet<WorkflowStep>(WorkflowSteps.Mandatory);
        Assert.Equal(RecordStatus.AwaitingAppraisal, RecordStateMachine.NextAfter(WorkflowStep.B2_SELF_SCORE, enabled));
        Assert.Equal(RecordStatus.AwaitingDecision, RecordStateMachine.NextAfter(WorkflowStep.B3B_APPRAISAL, enabled));
        Assert.Equal(RecordStatus.Published, RecordStateMachine.NextAfter(WorkflowStep.B5_PUBLISH, enabled));
        Assert.Equal(new[] { WorkflowStep.B2_SELF_SCORE, WorkflowStep.B3B_APPRAISAL, WorkflowStep.B4_DECISION, WorkflowStep.B5_PUBLISH },
            RecordStateMachine.ReopenTargets(enabled));
    }

    #endregion

    #region PeriodSettings

    [Fact]
    public void Presets_AreValid_AndMatchDesign()
    {
        var full = PeriodSettings.FullPreset();
        Assert.Empty(full.Validate());
        Assert.Equal(9, full.EnabledSteps().Count);
        Assert.Equal("09A", full.SelfScoreForm);

        var transition = PeriodSettings.TransitionQ3Preset();
        Assert.Empty(transition.Validate());
        Assert.False(transition.IsEnabled(WorkflowStep.B1_REGISTER));
        Assert.False(transition.IsEnabled(WorkflowStep.B1_APPROVE));
        Assert.Equal(7, transition.EnabledSteps().Count);
        Assert.Equal("09B", transition.SelfScoreForm);
        Assert.True(transition.UsesAxisScoring);

        Assert.Equal(new[] { "full", "q3-2026-transition" }, PeriodSettings.Presets.Select(p => p.Code));
        Assert.NotNull(PeriodSettings.FindPreset("FULL"));
        Assert.Null(PeriodSettings.FindPreset("khac"));
    }

    [Theory]
    [InlineData(WorkflowStep.B2_SELF_SCORE)]
    [InlineData(WorkflowStep.B3B_APPRAISAL)]
    [InlineData(WorkflowStep.B4_DECISION)]
    [InlineData(WorkflowStep.B5_PUBLISH)]
    public void Validate_MandatoryStepsCannotBeDisabled(WorkflowStep step)
    {
        var settings = PeriodSettings.FullPreset();
        settings.Steps[step.ToString()].Enabled = false;
        Assert.Contains(settings.Validate(), e => e.Contains("bước bắt buộc", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsInconsistentConfigurations()
    {
        var approveWithoutRegister = PeriodSettings.FullPreset();
        approveWithoutRegister.Steps["B1_REGISTER"].Enabled = false;
        approveWithoutRegister.SelfScoreForm = "09B";
        Assert.Contains(approveWithoutRegister.Validate(), e => e.Contains("duyệt danh mục", StringComparison.Ordinal));

        var aWithoutRegister = PeriodSettings.TransitionQ3Preset();
        aWithoutRegister.SelfScoreForm = "09A";
        Assert.Contains(aWithoutRegister.Validate(), e => e.Contains("09A", StringComparison.Ordinal));

        var badForm = PeriodSettings.FullPreset();
        badForm.SelfScoreForm = "09C";
        Assert.Contains(badForm.Validate(), e => e.Contains("Mẫu tự chấm", StringComparison.Ordinal));

        var version = PeriodSettings.FullPreset();
        version.SchemaVersion = 2;
        Assert.Contains(version.Validate(), e => e.Contains("Phiên bản cấu hình", StringComparison.Ordinal));

        var unknown = PeriodSettings.Parse("{\"steps\":{\"B9_UNKNOWN\":{\"enabled\":true}}}");
        Assert.Contains(unknown.Validate(), e => e.Contains("B9_UNKNOWN", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Parameters()
    {
        var settings = PeriodSettings.FullPreset();
        settings.Parameters.MaxTasks = 2;
        settings.Parameters.GoodMinScore = 95;
        settings.Parameters.ExcellentQuotaRatio = 1.5;
        settings.Parameters.JobGroupWeights["Khung1_QuanLyDangDoanThe"] = new CriteriaWeights(0.5, 0.5, 0.5, 0.5);
        settings.Parameters.AxisMaxScores = new[] { 10.0 };
        var errors = settings.Validate();
        Assert.Contains(errors, e => e.Contains("tối đa phải lớn hơn", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("giảm dần", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Trần tỷ lệ", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("Khung1_QuanLyDangDoanThe", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("6 trục", StringComparison.Ordinal));
    }

    [Fact]
    public void Json_RoundTrip_Normalizes_AndEmptyMeansFullPreset()
    {
        var json = PeriodSettings.TransitionQ3Preset().ToJson();
        Assert.Contains("\"schemaVersion\":1", json);
        Assert.Contains("\"B1_REGISTER\":{\"enabled\":false", json);
        var parsed = PeriodSettings.Parse(json);
        Assert.Equal(json, parsed.ToJson());

        Assert.Equal(PeriodSettings.FullPreset().ToJson(), PeriodSettings.Parse(null).ToJson());
        Assert.Equal(PeriodSettings.FullPreset().ToJson(), PeriodSettings.Parse("  ").ToJson());

        // Bước thiếu được bổ sung (bật), khóa không phân biệt hoa thường.
        var partial = PeriodSettings.Parse("{\"schemaVersion\":1,\"steps\":{\"b1_register\":{\"enabled\":false,\"deadline\":\"2026-10-05\"}},\"selfScoreForm\":\"09b\"}");
        Assert.Equal(9, partial.Steps.Count);
        Assert.False(partial.IsEnabled(WorkflowStep.B1_REGISTER));
        Assert.Equal(new DateOnly(2026, 10, 5), partial.Deadline(WorkflowStep.B1_REGISTER));
        Assert.Equal("09B", partial.SelfScoreForm);

        Assert.Throws<FormatException>(() => PeriodSettings.Parse("{không phải json"));
    }

    [Fact]
    public void DiffersOnlyInDeadlines()
    {
        var current = PeriodSettings.FullPreset();
        var deadlines = current.Clone();
        deadlines.Steps["B2_SELF_SCORE"].Deadline = new DateOnly(2026, 12, 11);
        Assert.True(deadlines.DiffersOnlyInDeadlines(current));

        var toggled = current.Clone();
        toggled.Steps["B3C_DIRECTOR"].Enabled = false;
        Assert.False(toggled.DiffersOnlyInDeadlines(current));

        var parameters = current.Clone();
        parameters.Parameters.MinTasks = 2;
        Assert.False(parameters.DiffersOnlyInDeadlines(current));
    }

    #endregion

    #region EvaluationScoring = code cũ

    [Fact]
    public void DefaultParameters_EqualOldConstants()
    {
        var p = new EvaluationParameters();
        Assert.Equal((3, 7, 70.0, 0.05, 5.0), (p.MinTasks, p.MaxTasks, p.TotalTaskWeight, p.TaskWeightTolerance, p.GeneralCriterionMaxScore));
        Assert.Equal((90.0, 70.0, 50.0, 0.20), (p.ExcellentMinScore, p.GoodMinScore, p.SatisfactoryMinScore, p.ExcellentQuotaRatio));
        Assert.Equal((30.0, 70.0), (p.CollectiveGeneralMaxScore, p.CollectiveTaskMaxScore));
        foreach (var group in Enum.GetValues<JobGroup>())
            Assert.Equal(Old.GetJobGroupWeights(group), EvaluationScoring.JobGroupWeights(group, p));
        Assert.Equal(Old.GetJobGroupWeights((JobGroup)99), EvaluationScoring.JobGroupWeights((JobGroup)99, p));
    }

    [Fact]
    public void TaskScores_AndTotals_AreBitIdenticalToOldCode_OnRandomInputs()
    {
        var p = new EvaluationParameters();
        var random = new Random(20260928);
        for (var i = 0; i < 20000; i++)
        {
            var group = (JobGroup)random.Next(0, 6); // gồm cả giá trị ngoài bảng → tỷ trọng mặc định
            var count = random.Next(1, 8);
            var tasks = Enumerable.Range(0, count).Select(_ => (
                Weight: Math.Round(random.NextDouble() * 40, random.Next(0, 3)),
                A: random.NextDouble() * 1.4 - 0.2, B: random.NextDouble() * 1.4 - 0.2,
                C: random.NextDouble() * 1.4 - 0.2, D: random.NextDouble() * 1.4 - 0.2)).ToList();
            var general = Enumerable.Range(0, 6).Select(_ => Math.Round(random.NextDouble() * 5, random.Next(0, 3))).ToArray();

            var oldTaskScores = tasks.Select(t => Old.TaskScore(t.Weight, t.A, t.B, t.C, t.D, group)).ToList();
            var newTaskScores = tasks.Select(t => EvaluationScoring.ScoreTask(t.Weight, t.A, t.B, t.C, t.D, group, p).Score).ToList();
            Assert.Equal(oldTaskScores, newTaskScores);

            var (oldGeneral, oldTasks, oldTotal) = Old.Totals(general, oldTaskScores);
            var newGeneral = EvaluationScoring.GeneralCriteriaScore(general);
            var newTasks = EvaluationScoring.TasksScore(newTaskScores);
            Assert.Equal(oldGeneral, newGeneral);
            Assert.Equal(oldTasks, newTasks);
            Assert.Equal(oldTotal, EvaluationScoring.TotalScore(newGeneral, newTasks));
            Assert.Equal(Old.CalculateGradeFromScore(oldTotal), EvaluationScoring.GradeFromScore(oldTotal, p));
        }
    }

    [Fact]
    public void Grades_Quota_AndValidationMessages_EqualOldCode()
    {
        var p = new EvaluationParameters();
        for (var score = -100; score <= 10100; score++)
            Assert.Equal(Old.CalculateGradeFromScore(score / 100.0), EvaluationScoring.GradeFromScore(score / 100.0, p));
        for (var n = 0; n <= 1000; n++)
            Assert.Equal(Old.Quota(n), EvaluationScoring.ExcellentQuota(n, p));

        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var cases = new List<double[]>
            {
                Array.Empty<double>(), new[] { 70.0 }, new[] { 30.0, 40.0 }, new[] { 30.0, 20, 20 }, new[] { 30.0, 20, 19.96 },
                new[] { 30.0, 20, 19.94 }, new[] { 30.0, 20, 20.05 }, new[] { 30.0, 20, 20.06 }, new[] { 10.0, 10, 10, 10, 10, 10, 10 },
                new[] { 10.0, 10, 10, 10, 10, 10, 5, 5 }, new[] { 23.333, 23.333, 23.334 }
            };
            foreach (var weights in cases)
                Assert.Equal(Old.ValidateRegistration(weights), EvaluationScoring.ValidateTaskRegistration(weights, p));

            foreach (var scores in new[] { new[] { 5.0, 5, 5, 5, 5, 5 }, new[] { 5.0, 5, 5, 5, 5, 5.1 }, new[] { -0.1, 5, 5, 5, 5, 5 }, new[] { 5.0 } })
                Assert.Equal(Old.ValidateGeneral(scores), EvaluationScoring.ValidateGeneralScores(scores, p));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    /// <summary>
    /// TC-1 (trích xuất HD03 mục 9, PL II mục VI) — <b>chỉ để so sánh</b>, không sửa công thức: code làm tròn điểm từng sản phẩm
    /// 2 chữ số và cộng dồn (≈ 67,47), văn bản làm tròn 1 chữ số rồi cộng (67,6). Tổng 97,47 vẫn ≥ 90 → HTXS như văn bản.
    /// Lệch ghi ở "Phát hiện thêm" của báo cáo task 12.
    /// </summary>
    [Fact]
    public void TC1_ComparedWithHd03Example()
    {
        var p = new EvaluationParameters();
        var sp = new (double W, double A, double B, double C, double D)[]
        {
            (30, 1.00, 0.95, 1.00, 1.00), (15, 1.00, 0.90, 1.00, 0.95), (10, 1, 1, 1, 1), (10, 1.00, 0.95, 1.00, 0.90), (5, 1.00, 0.90, 0.90, 0.90)
        };
        var scores = sp.Select(s => EvaluationScoring.ScoreTask(s.W, s.A, s.B, s.C, s.D, JobGroup.Khung2_AnToanKyThuat, p).Score).ToList();
        Assert.Equal(29.25, scores[0], 10);
        Assert.Equal(14.1, scores[1], 10);
        Assert.Equal(10.0, scores[2], 10);
        Assert.Equal(9.55, scores[3], 10);
        Assert.InRange(scores[4], 4.57, 4.58);   // HD03: 4,575 → 4,6
        var tasks = EvaluationScoring.TasksScore(scores);
        Assert.InRange(tasks, 67.47, 67.48);      // HD03: 67,6 (làm tròn từng dòng 1 chữ số)
        var total = EvaluationScoring.TotalScore(30.0, tasks);
        Assert.Equal(EvaluationGrade.HoanThanhXuatSac, EvaluationScoring.GradeFromScore(total, p));
    }

    /// <summary>TC-2: A = B = C = D = x → điểm = trọng số × x với mọi khung (tỷ trọng mỗi khung có tổng 1).</summary>
    [Fact]
    public void TC2_EqualCriteria_GiveWeightTimesRatio_ForEveryJobGroup()
    {
        var p = new EvaluationParameters();
        foreach (var group in Enum.GetValues<JobGroup>())
            foreach (var x in new[] { 0.0, 0.5, 0.8, 0.95, 1.0 })
                Assert.Equal(Math.Round(20 * x, 2), EvaluationScoring.ScoreTask(20, x, x, x, x, group, p).Score, 10);
    }

    [Fact]
    public void AxisScoring09B_ValidatesMaxPerAxis_AndSums()
    {
        var p = new EvaluationParameters();
        Assert.Null(EvaluationScoring.ValidateAxisScores(new[] { 15.0, 10, 10, 15, 10, 10 }, p));
        Assert.Contains("Trục 1", EvaluationScoring.ValidateAxisScores(new[] { 15.5, 10, 10, 15, 10, 10 }, p));
        Assert.Contains("6 trục", EvaluationScoring.ValidateAxisScores(new[] { 15.0 }, p));
        Assert.Equal(70.0, EvaluationScoring.AxisTasksScore(new[] { 15.0, 10, 10, 15, 10, 10 }));
    }

    /// <summary>Bản sao nguyên văn công thức trong <c>EvaluationService</c> trước task 12 (commit 473665f).</summary>
    private static class Old
    {
        public static (double wa, double wb, double wc, double wd) GetJobGroupWeights(JobGroup group)
        {
            return group switch
            {
                JobGroup.Khung1_QuanLyDangDoanThe => (0.25, 0.35, 0.20, 0.20),
                JobGroup.Khung2_AnToanKyThuat => (0.15, 0.50, 0.15, 0.20),
                JobGroup.Khung3_DuAnDauTu => (0.20, 0.30, 0.35, 0.15),
                JobGroup.Khung4_KhcnChuyenDoiSo => (0.15, 0.30, 0.20, 0.35),
                _ => (0.25, 0.25, 0.25, 0.25)
            };
        }

        public static double TaskScore(double weight, double a, double b, double c, double d, JobGroup group)
        {
            var (wA, wB, wC, wD) = GetJobGroupWeights(group);
            var ra = Math.Clamp(a, 0.0, 1.0);
            var rb = Math.Clamp(b, 0.0, 1.0);
            var rc = Math.Clamp(c, 0.0, 1.0);
            var rd = Math.Clamp(d, 0.0, 1.0);
            double weightedRatio = (ra * wA) +
                                   (rb * wB) +
                                   (rc * wC) +
                                   (rd * wD);
            return Math.Round(weight * weightedRatio, 2);
        }

        public static (double General, double Tasks, double Total) Totals(double[] generalScores, List<double> taskScores)
        {
            var general = Math.Round(generalScores.Sum(), 2);
            double totalTaskScore = 0.0;
            foreach (var score in taskScores)
                totalTaskScore += score;
            var tasks = Math.Round(totalTaskScore, 2);
            return (general, tasks, Math.Round(general + tasks, 2));
        }

        public static EvaluationGrade CalculateGradeFromScore(double score)
        {
            if (score >= 90.0) return EvaluationGrade.HoanThanhXuatSac;
            if (score >= 70.0) return EvaluationGrade.HoanThanhTot;
            if (score >= 50.0) return EvaluationGrade.HoanThanh;
            return EvaluationGrade.KhongHoanThanh;
        }

        public static int Quota(int goodOrBetter) => (int)Math.Floor(goodOrBetter * 0.20);

        public static string? ValidateRegistration(double[] weights)
        {
            if (weights == null || weights.Length < 3 || weights.Length > 7)
                return "Số lượng nhiệm vụ đăng ký phải từ 3 đến 7 nhiệm vụ theo Hướng dẫn 03-HD/TVĐU.";
            double totalWeight = Math.Round(weights.Sum(), 2);
            if (Math.Abs(totalWeight - 70.0) > 0.05)
                return $"Tổng trọng số của các nhiệm vụ phải bằng đúng 70.0 điểm. Hiện tại là: {totalWeight} điểm.";
            return null;
        }

        public static string? ValidateGeneral(double[] scores)
        {
            if (scores == null || scores.Length != 6)
                return "Điểm tiêu chí chung phải bao gồm đúng 6 tiêu chí (T1 đến T6).";
            for (int i = 0; i < 6; i++)
            {
                if (scores[i] < 0 || scores[i] > 5.0)
                    return $"Điểm tiêu chí T{i + 1} phải từ 0.0 đến 5.0 điểm.";
            }
            return null;
        }
    }

    #endregion
}
