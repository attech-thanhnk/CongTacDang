using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Task 12: máy trạng thái hồ sơ (mọi trạng thái × hành động × cấu hình bật/tắt), <see cref="PeriodSettings"/>.
/// Tính điểm: xem CriteriaSetUnitTests (task 16).
/// </summary>
public sealed class EvaluationWorkflowUnitTests
{
    private static readonly WorkflowStep[] Optional =
    {
        WorkflowStep.B1_REGISTER, WorkflowStep.B1_APPROVE, WorkflowStep.B2_CELL_CONFIRM, WorkflowStep.B3A_COLLECTIVE, WorkflowStep.B3B_APPRAISAL, WorkflowStep.B3C_DIRECTOR
    };

    /// <summary>Mọi tổ hợp bật/tắt của 6 bước tùy chọn (64 cấu hình), bước bắt buộc luôn bật (task 15: thẩm định tùy chọn).</summary>
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

        Assert.Equal(64 * 10 * 27, checkedCases);
    }

    /// <summary>Luật thiết kế mục 2 viết lại theo cách khác (theo số thứ tự) để đối chiếu.</summary>
    private static RecordStatus? Oracle(RecordStatus status, List<WorkflowStep> order, WorkflowCommand command)
    {
        static RecordStatus Waiting(WorkflowStep s) => (RecordStatus)(int)s; // B1_REGISTER=1 → 1 … B5_PUBLISH=9 → 9
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
        var enabled = Profile(PeriodSettings.FullPreset(), PeriodSettings.ProfileBase).ActiveSteps();
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
        var enabled = Profile(PeriodSettings.TransitionQ3Preset(), PeriodSettings.ProfileBase).ActiveSteps();
        Assert.Equal(RecordStatus.AwaitingSelfScore, RecordStateMachine.Initial(enabled));
        var b1 = RecordStateMachine.Apply(RecordStatus.AwaitingRegistration, enabled, WorkflowCommand.Complete(WorkflowStep.B1_REGISTER));
        Assert.False(b1.Succeeded);
        Assert.Contains("không áp dụng", b1.Error);
    }

    [Fact]
    public void StateMachine_OutOfOrder_MessageNamesCurrentStep()
    {
        var result = RecordStateMachine.Apply(RecordStatus.AwaitingRegistration, Profile(PeriodSettings.FullPreset(), PeriodSettings.ProfileBase).ActiveSteps(),
            WorkflowCommand.Complete(WorkflowStep.B3B_APPRAISAL));
        Assert.False(result.Succeeded);
        Assert.Equal("Hồ sơ đang ở bước \"Chờ đăng ký sản phẩm\", không thể thực hiện \"Thẩm định\".", result.Error);
    }

    [Fact]
    public void StateMachine_DisabledSteps_AreSkippedWhenComputingNext()
    {
        // Task 15: thẩm định không còn là bước bắt buộc (bắt buộc: tự chấm, quyết định, công bố).
        var enabled = new HashSet<WorkflowStep>(WorkflowSteps.Mandatory) { WorkflowStep.B3B_APPRAISAL };
        Assert.Equal(RecordStatus.AwaitingAppraisal, RecordStateMachine.NextAfter(WorkflowStep.B2_SELF_SCORE, enabled));
        Assert.Equal(RecordStatus.AwaitingDecision, RecordStateMachine.NextAfter(WorkflowStep.B3B_APPRAISAL, enabled));
        Assert.Equal(RecordStatus.Published, RecordStateMachine.NextAfter(WorkflowStep.B5_PUBLISH, enabled));
        Assert.Equal(new[] { WorkflowStep.B2_SELF_SCORE, WorkflowStep.B3B_APPRAISAL, WorkflowStep.B4_DECISION, WorkflowStep.B5_PUBLISH },
            RecordStateMachine.ReopenTargets(enabled));
    }

    private static WorkflowProfile Profile(PeriodSettings settings, string code) => settings.FindProfile(code)!;

    #endregion

    // Task 15: kiểm thử PeriodSettings (hồ sơ luồng, kiểu kỳ, kiểm tra cấu hình, JSON, thời hạn) chuyển sang WorkflowProfileUnitTests.

    // Task 16: kiểm thử EvaluationScoring (so với code cũ, TC-1/TC-2, làm tròn, K/AD) chuyển sang CriteriaSetUnitTests — công thức
    // nhận bộ tiêu chí thay cho tham số kỳ.
}
