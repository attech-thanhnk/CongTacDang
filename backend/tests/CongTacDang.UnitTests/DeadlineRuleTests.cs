using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Thời hạn bước tính tự động theo quy tắc quý; sửa tay không bị ghi đè.</summary>
public class DeadlineRuleTests
{
    private static DeadlineRule Wd(int n) => new() { Kind = DeadlineRuleKind.WorkingDayOfQuarter, Value = n };
    private static DeadlineRule Day(int d) => new() { Kind = DeadlineRuleKind.DayOfLastMonth, Value = d };
    private static DeadlineRule PrevWd(int n) => new() { Kind = DeadlineRuleKind.WorkingDaysBeforeQuarter, Value = n };

    [Fact]
    public void WorkingDaysBeforeQuarter_CountsBackwardsCorrectly()
    {
        // Quý 4/2026 bắt đầu 01/10 (thứ Năm); đếm lùi ngày làm việc, bỏ qua 26–27/09 (cuối tuần).
        Assert.Equal(new DateOnly(2026, 9, 30), DeadlineCalculator.Compute(PrevWd(1), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 9, 29), DeadlineCalculator.Compute(PrevWd(2), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 9, 28), DeadlineCalculator.Compute(PrevWd(3), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 9, 25), DeadlineCalculator.Compute(PrevWd(4), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 9, 24), DeadlineCalculator.Compute(PrevWd(5), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 9, 23), DeadlineCalculator.Compute(PrevWd(6), 2026, 4, null));

        // Nếu 30/09 là ngày nghỉ → lùi thêm 1 ngày.
        Assert.Equal(new DateOnly(2026, 9, 29), DeadlineCalculator.Compute(PrevWd(1), 2026, 4, new[] { new DateOnly(2026, 9, 30) }));
    }

    [Fact]
    public void WorkingDayOfQuarter_SkipsWeekendsAndHolidays()
    {
        // 01/10/2026 là thứ Năm: ngày làm việc 1 = 01/10, 2 = 02/10, 3 = 05/10 (thứ Hai), 5 = 07/10.
        Assert.Equal(new DateOnly(2026, 10, 1), DeadlineCalculator.Compute(Wd(1), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 10, 5), DeadlineCalculator.Compute(Wd(3), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 10, 7), DeadlineCalculator.Compute(Wd(5), 2026, 4, null));
        // 05/10 nghỉ → ngày làm việc 3 lùi sang 06/10.
        Assert.Equal(new DateOnly(2026, 10, 6), DeadlineCalculator.Compute(Wd(3), 2026, 4, new[] { new DateOnly(2026, 10, 5) }));
    }

    [Fact]
    public void DayOfLastMonth_RollsForwardToWorkingDay()
    {
        Assert.Equal(new DateOnly(2026, 12, 9), DeadlineCalculator.Compute(Day(9), 2026, 4, null));
        // 12/12/2026 là thứ Bảy → thứ Hai 14/12.
        Assert.Equal(new DateOnly(2026, 12, 14), DeadlineCalculator.Compute(Day(12), 2026, 4, null));
        Assert.Equal(new DateOnly(2026, 3, 16), DeadlineCalculator.Compute(Day(15), 2026, 1, null));
    }

    private static PeriodSettings WithRules()
    {
        var settings = PeriodSettings.Default();
        var p = settings.Profiles[0];
        p.Step(WorkflowStep.B2_SELF_SCORE).Rule = Day(9);
        p.Step(WorkflowStep.B4_DECISION).Rule = Day(14);
        return settings;
    }

    [Fact]
    public void Default_CarriesNoHardcodedDeadlines()
    {
        var settings = PeriodSettings.Default();
        Assert.All(settings.Profiles.SelectMany(p => p.Steps.Values), s =>
        {
            Assert.Null(s.Rule);
            Assert.Null(s.Deadline);
        });
    }

    [Fact]
    public void ApplyDeadlineRules_FillsDates_AndDatesStayFreelyEditable()
    {
        var settings = WithRules();
        var p = settings.Profiles[0];
        Assert.Null(p.Deadline(WorkflowStep.B2_SELF_SCORE)); // chưa áp dụng thì chưa có ngày

        settings.ApplyDeadlineRules(2026, 4);
        Assert.Equal(new DateOnly(2026, 12, 9), p.Deadline(WorkflowStep.B2_SELF_SCORE));
        Assert.Equal(new DateOnly(2026, 12, 14), p.Deadline(WorkflowStep.B4_DECISION));
        Assert.Null(p.Deadline(WorkflowStep.B3B_APPRAISAL)); // không quy tắc → không đổi

        // Sửa ngày tự do; chuẩn hóa/đọc lại không tính lại ngầm.
        p.Step(WorkflowStep.B2_SELF_SCORE).Deadline = new DateOnly(2026, 12, 11);
        var reloaded = PeriodSettings.Parse(settings.ToJson());
        Assert.Equal(new DateOnly(2026, 12, 11), reloaded.Profiles[0].Deadline(WorkflowStep.B2_SELF_SCORE));

        // Áp dụng lại theo yêu cầu thì điền lại theo quy tắc.
        reloaded.ApplyDeadlineRules(2026, 4);
        Assert.Equal(new DateOnly(2026, 12, 9), reloaded.Profiles[0].Deadline(WorkflowStep.B2_SELF_SCORE));
    }

    [Fact]
    public void ApplyDeadlineRules_UsesHolidays()
    {
        var settings = WithRules();
        settings.ApplyDeadlineRules(2026, 4, new[] { new DateOnly(2026, 12, 9) });
        Assert.Equal(new DateOnly(2026, 12, 10), settings.Profiles[0].Deadline(WorkflowStep.B2_SELF_SCORE));
    }

    [Fact]
    public void Validate_RejectsBadRule_AndRoundTripsThroughJson()
    {
        Assert.NotNull(Day(31).Validate());
        Assert.NotNull(Wd(0).Validate());
        var settings = WithRules();
        var clone = settings.Clone();
        Assert.Equal(DeadlineRuleKind.DayOfLastMonth, clone.Profiles[0].Step(WorkflowStep.B2_SELF_SCORE).Rule!.Kind);

        settings.Profiles[0].Step(WorkflowStep.B3A_COLLECTIVE).Rule = Day(40);
        Assert.Contains(settings.Validate(), e => e.Contains("quy tắc thời hạn"));
    }

    [Fact]
    public void DeadlineOnlyChanges_AreAllowedAfterOpen()
    {
        var a = PeriodSettings.Default();
        var b = WithRules();
        b.ApplyDeadlineRules(2026, 4, new[] { new DateOnly(2026, 12, 10) });
        Assert.True(a.DiffersOnlyInDeadlines(b));
    }

    [Fact]
    public void RealisticQuarterSchedule_ComputesExactDatesFromDocument()
    {
        var settings = PeriodSettings.Default();
        var baseProfile = settings.Profiles.First(p => p.Code == PeriodSettings.ProfileBase);
        // Trưởng/Phó phòng: B1 từ -5 đến -1 trước quý, B2 ngày 9, B3a ngày 10, B3b ngày 11, B3c ngày 14, B4 ngày 14
        baseProfile.Step(WorkflowStep.B1_REGISTER).Rule = new DeadlineRule { Kind = DeadlineRuleKind.WorkingDaysBeforeQuarter, Value = 6 };
        baseProfile.Step(WorkflowStep.B1_APPROVE).Rule = new DeadlineRule { Kind = DeadlineRuleKind.WorkingDaysBeforeQuarter, Value = 2 };
        baseProfile.Step(WorkflowStep.B2_SELF_SCORE).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 9 };
        baseProfile.Step(WorkflowStep.B2_CELL_CONFIRM).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 9 };
        baseProfile.Step(WorkflowStep.B3A_COLLECTIVE).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 10 };
        baseProfile.Step(WorkflowStep.B3B_APPRAISAL).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 11 };
        baseProfile.Step(WorkflowStep.B3C_DIRECTOR).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 14 };
        baseProfile.Step(WorkflowStep.B4_DECISION).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 14 };

        var upperProfile = settings.Profiles.First(p => p.Code == PeriodSettings.ProfileUpper);
        // Cấp trên: B1 ngày làm việc thứ 5, B2 ngày 9, B3a ngày 14, B3b ngày 15, B3c ngày 17, B4 ngày 18
        upperProfile.Step(WorkflowStep.B1_REGISTER).Rule = new DeadlineRule { Kind = DeadlineRuleKind.WorkingDayOfQuarter, Value = 1 };
        upperProfile.Step(WorkflowStep.B1_APPROVE).Rule = new DeadlineRule { Kind = DeadlineRuleKind.WorkingDayOfQuarter, Value = 5 };
        upperProfile.Step(WorkflowStep.B2_SELF_SCORE).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 9 };
        upperProfile.Step(WorkflowStep.B2_CELL_CONFIRM).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 9 };
        upperProfile.Step(WorkflowStep.B3A_COLLECTIVE).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 14 };
        upperProfile.Step(WorkflowStep.B3B_APPRAISAL).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 15 };
        upperProfile.Step(WorkflowStep.B3C_DIRECTOR).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 17 };
        upperProfile.Step(WorkflowStep.B4_DECISION).Rule = new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = 18 };

        settings.ApplyDeadlineRules(2026, 4);

        // B1: cá nhân nộp ngày cuối quý trước -5 (23/09/2026), Giám đốc duyệt ngày cuối quý trước -1 (29/09/2026)
        Assert.Equal(new DateOnly(2026, 9, 23), baseProfile.Deadline(WorkflowStep.B1_REGISTER));
        Assert.Equal(new DateOnly(2026, 9, 29), baseProfile.Deadline(WorkflowStep.B1_APPROVE));
        // B2: tự chấm ngày 9 tháng cuối quý (09/12/2026)
        Assert.Equal(new DateOnly(2026, 12, 9), baseProfile.Deadline(WorkflowStep.B2_SELF_SCORE));
        // B3: B3a ngày 10, B3b ngày 11, B3c ngày 14
        Assert.Equal(new DateOnly(2026, 12, 10), baseProfile.Deadline(WorkflowStep.B3A_COLLECTIVE));
        Assert.Equal(new DateOnly(2026, 12, 11), baseProfile.Deadline(WorkflowStep.B3B_APPRAISAL));
        Assert.Equal(new DateOnly(2026, 12, 14), baseProfile.Deadline(WorkflowStep.B3C_DIRECTOR));
        // B4: Đảng ủy Công ty quyết định ngày 14
        Assert.Equal(new DateOnly(2026, 12, 14), baseProfile.Deadline(WorkflowStep.B4_DECISION));

        // Cấp trên: B1 phê duyệt ngày làm việc thứ 5 đầu quý (07/10/2026)
        Assert.Equal(new DateOnly(2026, 10, 7), upperProfile.Deadline(WorkflowStep.B1_APPROVE));
        // B3: B3a ngày 14, B3b ngày 15, B3c ngày 17
        Assert.Equal(new DateOnly(2026, 12, 14), upperProfile.Deadline(WorkflowStep.B3A_COLLECTIVE));
        Assert.Equal(new DateOnly(2026, 12, 15), upperProfile.Deadline(WorkflowStep.B3B_APPRAISAL));
        Assert.Equal(new DateOnly(2026, 12, 17), upperProfile.Deadline(WorkflowStep.B3C_DIRECTOR));
        // B4: BTV Đảng ủy TCT quyết định ngày 18
        Assert.Equal(new DateOnly(2026, 12, 18), upperProfile.Deadline(WorkflowStep.B4_DECISION));
    }
}