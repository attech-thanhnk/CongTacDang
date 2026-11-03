using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Đánh dấu / bỏ đánh dấu Chưa xếp loại: điều kiện, trường đặt, trạng thái về.</summary>
public sealed class NotRatedRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 2, 0, 0, DateTimeKind.Utc);

    private static WorkflowProfile Profile(string code = "co-so") => PeriodSettings.Default().FindProfile(code)!;

    private static EvaluationRecord Record(RecordStatus status) => new()
    {
        Status = status,
        Member = new PartyMemberProfile { FullName = "Nguyễn Văn A" }
    };

    [Theory]
    [InlineData(PeriodStatus.Draft)]
    [InlineData(PeriodStatus.Open)]
    public void Mark_DraftOrOpen_BeforeDecision_Allowed(PeriodStatus period)
    {
        Assert.Null(NotRatedRules.MarkBlockReason(Record(RecordStatus.AwaitingRegistration), period));
        Assert.Null(NotRatedRules.MarkBlockReason(Record(RecordStatus.AwaitingDecision), period));
    }

    [Theory]
    [InlineData(PeriodStatus.Locked)]
    [InlineData(PeriodStatus.Closed)]
    public void Mark_LockedOrClosedPeriod_Blocked(PeriodStatus period) =>
        Assert.NotNull(NotRatedRules.MarkBlockReason(Record(RecordStatus.AwaitingRegistration), period));

    [Fact]
    public void Mark_DecidedPublishedOrAlreadyMarked_Blocked()
    {
        var decided = Record(RecordStatus.AwaitingDecision);
        decided.DecisionRecordedAt = Now;
        Assert.Contains("quyết định", NotRatedRules.MarkBlockReason(decided, PeriodStatus.Open));
        Assert.NotNull(NotRatedRules.MarkBlockReason(Record(RecordStatus.AwaitingPublish), PeriodStatus.Open));
        Assert.NotNull(NotRatedRules.MarkBlockReason(Record(RecordStatus.Published), PeriodStatus.Open));

        var marked = Record(RecordStatus.AwaitingRegistration);
        NotRatedRules.Mark(marked, "Nghỉ ốm", null, "Quản lý kỳ", Now);
        Assert.NotNull(NotRatedRules.MarkBlockReason(marked, PeriodStatus.Open));
    }

    [Fact]
    public void Mark_SetsDecisionNotRated_AndAwaitingPublish_EffectiveGradeIgnoresEarlierProposals()
    {
        var record = Record(RecordStatus.AwaitingAppraisal);
        record.SelfScoredAt = Now.AddDays(-3);
        record.SelfProposedGrade = EvaluationGrade.HoanThanhTot;
        record.TotalSelfScore = 88;
        record.ReturnReason = "Sửa lại";

        NotRatedRules.Mark(record, "Đi đào tạo", Guid.NewGuid(), "Quản lý kỳ", Now);

        Assert.True(record.IsMarkedNotRated());
        Assert.Equal(RecordStatus.AwaitingPublish, record.Status);
        Assert.Equal(RecordStatus.AwaitingAppraisal, record.NotRatedFromStatus);
        Assert.Equal("Đi đào tạo", record.NotRatedReason);
        Assert.Equal(EvaluationGrade.ChuaXepLoai, record.FinalGrade);
        Assert.Equal(Now, record.DecisionRecordedAt);
        Assert.Null(record.ReturnReason);
        Assert.Equal(EvaluationGrade.ChuaXepLoai, record.EffectiveGrade());
        Assert.Equal(0, record.EffectiveScore());
    }

    [Fact]
    public void Unmark_RestoresStatusBeforeMark_AndClearsFields()
    {
        var profile = Profile();
        var record = Record(RecordStatus.AwaitingSelfScore);
        NotRatedRules.Mark(record, "Nhầm", null, "Quản lý kỳ", Now);

        Assert.Null(NotRatedRules.UnmarkBlockReason(record, PeriodStatus.Open));
        var status = NotRatedRules.Unmark(record, profile.ActiveSteps());

        Assert.Equal(RecordStatus.AwaitingSelfScore, status);
        Assert.Equal(RecordStatus.AwaitingSelfScore, record.Status);
        Assert.False(record.IsMarkedNotRated());
        Assert.Null(record.NotRatedReason);
        Assert.Null(record.NotRatedFromStatus);
        Assert.Null(record.DecisionRecordedAt);
    }

    [Fact]
    public void Unmark_WithoutKnownStatus_ReturnsToFirstStep()
    {
        var profile = Profile();
        var record = Record(RecordStatus.AwaitingPublish);
        record.NotRatedAt = Now;
        record.NotRatedReason = "Dữ liệu cũ";

        Assert.Equal(RecordStateMachine.Initial(profile.ActiveSteps()), NotRatedRules.Unmark(record, profile.ActiveSteps()));
    }

    [Fact]
    public void Unmark_NotMarkedPublishedOrLockedPeriod_Blocked()
    {
        Assert.NotNull(NotRatedRules.UnmarkBlockReason(Record(RecordStatus.AwaitingPublish), PeriodStatus.Open));

        var marked = Record(RecordStatus.AwaitingRegistration);
        NotRatedRules.Mark(marked, "Nghỉ ốm", null, "Quản lý kỳ", Now);
        Assert.NotNull(NotRatedRules.UnmarkBlockReason(marked, PeriodStatus.Locked));
        marked.Status = RecordStatus.Published;
        Assert.NotNull(NotRatedRules.UnmarkBlockReason(marked, PeriodStatus.Open));
    }

    [Fact]
    public void Progress_MarkedRecord_StepsBeforeDecisionSkipped_DecisionDoneByMarker()
    {
        var record = Record(RecordStatus.AwaitingRegistration);
        NotRatedRules.Mark(record, "Nghỉ thai sản", null, "Quản lý kỳ", Now);

        var progress = EvaluationMapping.Progress(record, Profile(), new DateOnly(2026, 11, 3)).ToDictionary(p => p.Step);

        Assert.Equal("skipped", progress["B1_REGISTER"].State);
        Assert.Equal("skipped", progress["B2_SELF_SCORE"].State);
        Assert.Equal("done", progress["B4_DECISION"].State);
        Assert.Equal("Quản lý kỳ", progress["B4_DECISION"].DoneByName);
        Assert.Equal("current", progress["B5_PUBLISH"].State);
    }
}
