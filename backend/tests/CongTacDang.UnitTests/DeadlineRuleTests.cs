using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Thời hạn bước tính tự động theo quy tắc quý (kế hoạch quý IV/2026 của Phòng TCCB-LĐ), sửa tay không bị ghi đè.</summary>
public class DeadlineRuleTests
{
    private static DeadlineRule Wd(int n) => new() { Kind = DeadlineRuleKind.WorkingDayOfQuarter, Value = n };
    private static DeadlineRule Day(int d) => new() { Kind = DeadlineRuleKind.DayOfLastMonth, Value = d };

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
        var settings = PeriodSettings.FullPreset();
        var p = settings.Profiles[0];
        p.Step(WorkflowStep.B2_SELF_SCORE).Rule = Day(9);
        p.Step(WorkflowStep.B4_DECISION).Rule = Day(14);
        return settings;
    }

    [Fact]
    public void Presets_CarryNoHardcodedDeadlines()
    {
        var settings = PeriodSettings.FullPreset();
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
        var a = PeriodSettings.FullPreset();
        var b = WithRules();
        b.ApplyDeadlineRules(2026, 4, new[] { new DateOnly(2026, 12, 10) });
        Assert.True(a.DiffersOnlyInDeadlines(b));
    }
}