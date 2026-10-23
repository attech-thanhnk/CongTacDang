using System;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Mở riêng hồ sơ khi kỳ khóa dữ liệu: luật trạng thái kỳ xét hạn mở riêng của hồ sơ.</summary>
public sealed class RecordUnlockUnitTests
{
    private static readonly DateTime Now = new(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void LockedPeriod_WithoutUnlock_BlocksOwnerSteps_AllowsAppraisal()
    {
        Assert.NotNull(WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.SubmitSelfScore), null, Now));
        Assert.NotNull(WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.ConfirmByCell), null, Now));
        Assert.Null(WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.Appraise), null, Now));
    }

    [Fact]
    public void LockedPeriod_ActiveUnlock_AllowsStepsBeforeAppraisal_ExceptRevoke()
    {
        var until = Now.AddDays(3);
        Assert.Null(WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.SubmitSelfScore), until, Now));
        Assert.Null(WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.ConfirmByCell), until, Now));
        Assert.Null(WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.RecordCollectiveProposal), until, Now));
        Assert.NotNull(WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.RevokeTaskApproval), until, Now));
    }

    [Fact]
    public void LockedPeriod_ExpiredUnlock_BlocksWithDeadlineMessage()
    {
        var reason = WorkflowActions.PeriodBlockReason(PeriodStatus.Locked, WorkflowActions.Get(WorkflowActions.SubmitSelfScore), Now.AddMinutes(-1), Now);
        Assert.NotNull(reason);
        Assert.Contains("Thời hạn mở riêng hồ sơ đã hết", reason);
    }

    [Fact]
    public void UnlockDoesNotAffectOtherPeriodStates()
    {
        var until = Now.AddDays(3);
        Assert.NotNull(WorkflowActions.PeriodBlockReason(PeriodStatus.Closed, WorkflowActions.Get(WorkflowActions.SubmitSelfScore), until, Now));
        Assert.NotNull(WorkflowActions.PeriodBlockReason(PeriodStatus.Draft, WorkflowActions.Get(WorkflowActions.SubmitSelfScore), until, Now));
        Assert.Null(WorkflowActions.PeriodBlockReason(PeriodStatus.Open, WorkflowActions.Get(WorkflowActions.SubmitSelfScore), null, Now));
    }

    [Fact]
    public void IsLockedStep_AndRecordUnlockedAt()
    {
        Assert.True(WorkflowSteps.IsLockedStep(WorkflowStep.B2_SELF_SCORE));
        Assert.True(WorkflowSteps.IsLockedStep(WorkflowStep.B3A_COLLECTIVE));
        Assert.False(WorkflowSteps.IsLockedStep(WorkflowStep.B3B_APPRAISAL));

        var record = new EvaluationRecord { UnlockedUntil = Now.AddHours(1) };
        Assert.True(record.IsUnlockedAt(Now));
        Assert.False(record.IsUnlockedAt(Now.AddHours(2)));
        Assert.False(new EvaluationRecord().IsUnlockedAt(Now));
    }
}
