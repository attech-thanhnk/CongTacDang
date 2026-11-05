using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Máy trạng thái hồ sơ (mọi trạng thái × hành động × cấu hình bật/tắt); tính điểm xem CriteriaSetUnitTests.</summary>
public sealed class EvaluationWorkflowUnitTests
{
    private static readonly WorkflowStep[] Optional =
    {
        WorkflowStep.B1_REGISTER, WorkflowStep.B1_UNIT_SUBMIT, WorkflowStep.B1_HR_REVIEW, WorkflowStep.B1_APPROVE,
        WorkflowStep.B2_CELL_CONFIRM, WorkflowStep.B3A_COLLECTIVE, WorkflowStep.B3B_APPRAISAL, WorkflowStep.B3C_DIRECTOR
    };

    /// <summary>Thứ tự luồng và trạng thái chờ viết tay, độc lập với WorkflowSteps.</summary>
    private static readonly (WorkflowStep Step, RecordStatus Waiting)[] Flow =
    {
        (WorkflowStep.B1_REGISTER, RecordStatus.AwaitingRegistration),
        (WorkflowStep.B1_UNIT_SUBMIT, RecordStatus.AwaitingUnitSubmit),
        (WorkflowStep.B1_HR_REVIEW, RecordStatus.AwaitingTaskReview),
        (WorkflowStep.B1_APPROVE, RecordStatus.AwaitingTaskApproval),
        (WorkflowStep.B2_SELF_SCORE, RecordStatus.AwaitingSelfScore),
        (WorkflowStep.B2_CELL_CONFIRM, RecordStatus.AwaitingCellConfirm),
        (WorkflowStep.B3A_COLLECTIVE, RecordStatus.AwaitingCollective),
        (WorkflowStep.B3B_APPRAISAL, RecordStatus.AwaitingAppraisal),
        (WorkflowStep.B3C_DIRECTOR, RecordStatus.AwaitingDirectorReview),
        (WorkflowStep.B4_DECISION, RecordStatus.AwaitingDecision),
        (WorkflowStep.B5_PUBLISH, RecordStatus.AwaitingPublish)
    };

    private static int Pos(WorkflowStep step) => Array.FindIndex(Flow, f => f.Step == step);

    /// <summary>Mọi tổ hợp bật/tắt của 8 bước tùy chọn (256 cấu hình); bước bắt buộc luôn bật.</summary>
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
            // Thứ tự các bước bật theo bảng Flow viết tay.
            var order = Flow.Select(f => f.Step).Where(enabled.Contains).ToList();
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

        Assert.Equal(256 * 12 * 33, checkedCases);
    }

    /// <summary>Luật thiết kế viết lại theo số thứ tự để đối chiếu.</summary>
    private static RecordStatus? Oracle(RecordStatus status, List<WorkflowStep> order, WorkflowCommand command)
    {
        static RecordStatus Waiting(WorkflowStep s) => Flow[Pos(s)].Waiting;
        switch (command.Action)
        {
            case WorkflowAction.Complete:
                if (!order.Contains(command.Step) || status != Waiting(command.Step))
                    return null;
                var next = order.FirstOrDefault(s => Pos(s) > Pos(command.Step));
                return next == default ? RecordStatus.Published : Waiting(next);
            case WorkflowAction.Return:
                WorkflowStep? target = command.Step switch
                {
                    WorkflowStep.B1_UNIT_SUBMIT or WorkflowStep.B1_HR_REVIEW or WorkflowStep.B1_APPROVE => WorkflowStep.B1_REGISTER,
                    WorkflowStep.B2_CELL_CONFIRM or WorkflowStep.B3B_APPRAISAL => WorkflowStep.B2_SELF_SCORE,
                    _ => null
                };
                if (target == null || !order.Contains(command.Step) || !order.Contains(target.Value) || status != Waiting(command.Step))
                    return null;
                return Waiting(target.Value);
            case WorkflowAction.Reopen:
                var t = command.ReopenTarget!.Value;
                if (status != RecordStatus.Published || !order.Contains(t) || Pos(t) < Pos(WorkflowStep.B2_SELF_SCORE))
                    return null;
                return Waiting(t);
            default:
                return null;
        }
    }

    [Fact]
    public void StateMachine_WithRegistration_WalksNineSteps_InOrder()
    {
        var enabled = Profile(PeriodSettings.Default(), PeriodSettings.ProfileBase).ActiveSteps();
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
    public void StateMachine_WithoutApproval_GoesFromRegistrationToSelfScore_AndRejectsApprove()
    {
        var profile = Profile(PeriodSettings.Default(), PeriodSettings.ProfileBase);
        profile.Step(WorkflowStep.B1_UNIT_SUBMIT).Mode = StepMode.Off;
        profile.Step(WorkflowStep.B1_HR_REVIEW).Mode = StepMode.Off;
        profile.Step(WorkflowStep.B1_APPROVE).Mode = StepMode.Off;
        var enabled = profile.ActiveSteps();
        Assert.Equal(RecordStatus.AwaitingRegistration, RecordStateMachine.Initial(enabled));
        var registered = RecordStateMachine.Apply(RecordStatus.AwaitingRegistration, enabled, WorkflowCommand.Complete(WorkflowStep.B1_REGISTER));
        Assert.Equal(RecordStatus.AwaitingSelfScore, registered.Status);
        var approve = RecordStateMachine.Apply(RecordStatus.AwaitingTaskApproval, enabled, WorkflowCommand.Complete(WorkflowStep.B1_APPROVE));
        Assert.False(approve.Succeeded);
        Assert.Contains("không áp dụng", approve.Error);
    }

    [Fact]
    public void StateMachine_OutOfOrder_MessageNamesCurrentStep()
    {
        var result = RecordStateMachine.Apply(RecordStatus.AwaitingRegistration, Profile(PeriodSettings.Default(), PeriodSettings.ProfileBase).ActiveSteps(),
            WorkflowCommand.Complete(WorkflowStep.B3B_APPRAISAL));
        Assert.False(result.Succeeded);
        Assert.Equal("Hồ sơ đang ở bước \"Chờ đăng ký sản phẩm\", không thể thực hiện \"Thẩm định\".", result.Error);
    }

    [Fact]
    public void StateMachine_DisabledSteps_AreSkippedWhenComputingNext()
    {
        // Bước bắt buộc: tự chấm, quyết định, công bố.
        var enabled = new HashSet<WorkflowStep>(WorkflowSteps.Mandatory) { WorkflowStep.B3B_APPRAISAL };
        Assert.Equal(RecordStatus.AwaitingAppraisal, RecordStateMachine.NextAfter(WorkflowStep.B2_SELF_SCORE, enabled));
        Assert.Equal(RecordStatus.AwaitingDecision, RecordStateMachine.NextAfter(WorkflowStep.B3B_APPRAISAL, enabled));
        Assert.Equal(RecordStatus.Published, RecordStateMachine.NextAfter(WorkflowStep.B5_PUBLISH, enabled));
        Assert.Equal(new[] { WorkflowStep.B2_SELF_SCORE, WorkflowStep.B3B_APPRAISAL, WorkflowStep.B4_DECISION, WorkflowStep.B5_PUBLISH },
            RecordStateMachine.ReopenTargets(enabled));
    }

    private static WorkflowProfile Profile(PeriodSettings settings, string code) => settings.FindProfile(code)!;

    #endregion
}
