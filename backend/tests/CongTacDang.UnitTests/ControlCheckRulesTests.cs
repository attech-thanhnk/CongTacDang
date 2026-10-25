using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Phiếu kiểm soát (Mẫu 19): mười mục cố định, luật kết luận và hiệu lực sau khi hồ sơ đổi.</summary>
public sealed class ControlCheckRulesTests
{
    private static List<(string Code, ControlItemResult? Result, string? Evidence)> Items(
        ControlItemResult? result = ControlItemResult.Pass, params (string Code, ControlItemResult? Result, string? Evidence)[] overrides)
    {
        var items = ControlCheckRules.Items.Select(i => (i.Code, result, (string?)null)).ToList();
        foreach (var o in overrides)
            items[items.FindIndex(i => i.Code == o.Code)] = o;
        return items;
    }

    [Fact]
    public void Catalog_HasTenFixedItems_K01ToK10()
    {
        Assert.Equal(Enumerable.Range(1, 10).Select(i => $"K{i:00}"), ControlCheckRules.Items.Select(i => i.Code));
        Assert.All(ControlCheckRules.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.Text)));
        Assert.True(ControlCheckRules.IsKnownItem("K10"));
        Assert.False(ControlCheckRules.IsKnownItem("K11"));
        Assert.Equal(1, ControlCheckRules.ItemsVersion);
    }

    [Fact]
    public void Submit_AllPass_Eligible_IsValid() =>
        Assert.Null(ControlCheckRules.ValidateForSubmit(Items(), ControlConclusion.Eligible, null));

    [Fact]
    public void Submit_RequiresAllTenItemsChosen()
    {
        var error = ControlCheckRules.ValidateForSubmit(Items(overrides: ("K05", null, null)), ControlConclusion.Eligible, null);
        Assert.Contains("K05", error);

        var missing = Items().Where(i => i.Code != "K10").ToList();
        Assert.Contains("K10", ControlCheckRules.ValidateForSubmit(missing, ControlConclusion.Eligible, null));
    }

    [Fact]
    public void Submit_RequiresConclusion() =>
        Assert.NotNull(ControlCheckRules.ValidateForSubmit(Items(), null, null));

    [Fact]
    public void Eligible_ForbiddenWhenAnyItemFails_ButNeedsWorkAllowed()
    {
        var failing = Items(overrides: ("K03", ControlItemResult.Fail, "Thiếu minh chứng"));
        var error = ControlCheckRules.ValidateForSubmit(failing, ControlConclusion.Eligible, null);
        Assert.NotNull(error);
        Assert.Contains("chưa đạt", error);

        Assert.Null(ControlCheckRules.ValidateForSubmit(failing, ControlConclusion.NeedsWork, "Bổ sung minh chứng K03"));
    }

    [Fact]
    public void NotApplicable_NeedsReason_ButDoesNotBlockEligible()
    {
        var withoutReason = Items(overrides: ("K07", ControlItemResult.NotApplicable, " "));
        Assert.Contains("K07", ControlCheckRules.ValidateForSubmit(withoutReason, ControlConclusion.Eligible, null));

        var withReason = Items(overrides: ("K07", ControlItemResult.NotApplicable, "Không có vị trí an toàn trọng yếu"));
        Assert.Null(ControlCheckRules.ValidateForSubmit(withReason, ControlConclusion.Eligible, null));
    }

    [Fact]
    public void Fail_NeedsEvidence()
    {
        var error = ControlCheckRules.ValidateForSubmit(
            Items(overrides: ("K02", ControlItemResult.Fail, null)), ControlConclusion.NeedsWork, "Hoàn thiện K02");
        Assert.Contains("K02", error);
    }

    [Fact]
    public void NeedsWork_NeedsContent()
    {
        Assert.NotNull(ControlCheckRules.ValidateForSubmit(Items(), ControlConclusion.NeedsWork, "  "));
        Assert.Null(ControlCheckRules.ValidateForSubmit(Items(), ControlConclusion.NeedsWork, "Cập nhật kế hoạch phát triển"));
    }

    private static EvaluationRecordHistory History(DateTime at, WorkflowAction action, double? before = 80, double? after = 80,
        EvaluationGrade? gradeBefore = null, EvaluationGrade? gradeAfter = null) => new()
    {
        Action = action, CreatedAt = at, ScoreBefore = before, ScoreAfter = after, GradeBefore = gradeBefore, GradeAfter = gradeAfter
    };

    [Fact]
    public void Stale_WhenRecordStepOrResultChangesAfterConfirm()
    {
        var confirmed = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);

        Assert.False(ControlCheckRules.IsStale(confirmed, Array.Empty<EvaluationRecordHistory>()));
        // Thao tác trước lúc xác nhận không làm hết hiệu lực.
        Assert.False(ControlCheckRules.IsStale(confirmed, new[] { History(confirmed.AddMinutes(-1), WorkflowAction.Return) }));
        foreach (var action in new[]
                 {
                     WorkflowAction.Complete, WorkflowAction.Return, WorkflowAction.Reopen, WorkflowAction.RecordExternal,
                     WorkflowAction.ChangeProfile, WorkflowAction.EditSnapshot, WorkflowAction.RevokeTaskApproval
                 })
            Assert.True(ControlCheckRules.IsStale(confirmed, new[] { History(confirmed.AddMinutes(1), action) }), action.ToString());
    }

    [Fact]
    public void Stale_WhenScoreOrGradeChanges_NotForNeutralActions()
    {
        var confirmed = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        var later = confirmed.AddHours(1);

        Assert.False(ControlCheckRules.IsStale(confirmed, new[]
        {
            History(later, WorkflowAction.TaskChange), History(later, WorkflowAction.GrantUnlock), History(later, WorkflowAction.SyncSnapshot)
        }));
        Assert.True(ControlCheckRules.IsStale(confirmed, new[] { History(later, WorkflowAction.TaskChange, before: 80, after: 82) }));
        Assert.True(ControlCheckRules.IsStale(confirmed, new[]
        {
            History(later, WorkflowAction.Recalculate, gradeBefore: EvaluationGrade.HoanThanh, gradeAfter: EvaluationGrade.HoanThanhTot)
        }));
    }
}
