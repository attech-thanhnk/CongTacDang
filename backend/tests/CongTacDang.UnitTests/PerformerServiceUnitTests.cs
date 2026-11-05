using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Màn hình "Người thực hiện": danh sách việc khớp quyền thực hiện các bước của quy trình.</summary>
public sealed class PerformerServiceUnitTests
{
    [Fact]
    public void JobPermissions_CoverEveryNonOwnerStepPermission()
    {
        var jobs = PerformerService.JobPermissions;

        // Mọi quyền mặc định của bước không phải của chủ hồ sơ, cộng quyền "Lãnh đạo đơn vị đề xuất" và ghi nhận kết quả cấp trên.
        var expected = WorkflowSteps.Ordered
            .Where(s => !WorkflowSteps.OwnerSteps.Contains(s))
            .Select(WorkflowPermissions.DefaultFor)
            .Append(WorkflowPermissions.UnitReview)
            .Append(WorkflowPermissions.ExternalRecord)
            .Distinct()
            .ToList();
        Assert.Equal(expected.OrderBy(c => c), jobs.OrderBy(c => c));
        Assert.Equal(jobs.Count, jobs.Distinct().Count());
        Assert.DoesNotContain(PermissionCodes.EvaluationSelf, jobs);
        Assert.All(jobs, code => Assert.True(WorkflowActions.IsAssignableStepPermission(code)));
        Assert.All(jobs, code => Assert.NotEqual(code, PermissionCodes.DisplayName(code)));
    }

    [Fact]
    public void JobPermissions_FollowWorkflowOrder()
    {
        var jobs = PerformerService.JobPermissions;
        Assert.Equal(PermissionCodes.EvaluationTasksForward, jobs[0]);
        Assert.Equal(PermissionCodes.EvaluationTasksReview, jobs[1]);
        Assert.Equal(PermissionCodes.EvaluationTasksApprove, jobs[2]);
        Assert.True(jobs.ToList().IndexOf(PermissionCodes.EvaluationAppraise) < jobs.ToList().IndexOf(PermissionCodes.EvaluationDecide));
        Assert.True(jobs.ToList().IndexOf(PermissionCodes.EvaluationDecide) < jobs.ToList().IndexOf(PermissionCodes.EvaluationPublish));
    }

    [Fact]
    public void Jobs_FollowStepPermissionOfPeriodProfiles()
    {
        // Kỳ mặc định: duyệt danh mục dùng quyền nhận xét của cấp trực tiếp sử dụng nên không có việc theo quyền duyệt danh mục.
        var jobs = PerformerService.BuildJobs(PeriodSettings.Default().Profiles);
        var approve = Assert.Single(jobs, j => j.Step == WorkflowStep.B1_APPROVE);
        Assert.Equal(PermissionCodes.EvaluationDirectorReview, approve.Permission);
        Assert.True(approve.InUse);
        Assert.Equal(WorkflowSteps.DisplayName(WorkflowStep.B1_APPROVE), approve.StepName);
        Assert.DoesNotContain(jobs, j => j.Permission == PermissionCodes.EvaluationTasksApprove);

        // Hai bước mới của bước 1 có trên màn hình, theo thứ tự quy trình.
        var steps = jobs.Where(j => j.Step != null).Select(j => j.Step!.Value).Distinct().ToList();
        Assert.Equal(new[] { WorkflowStep.B1_UNIT_SUBMIT, WorkflowStep.B1_HR_REVIEW, WorkflowStep.B1_APPROVE }, steps.Take(3));
        Assert.Equal(PermissionCodes.EvaluationTasksForward, jobs.Single(j => j.Step == WorkflowStep.B1_UNIT_SUBMIT).Permission);
        Assert.Equal(PermissionCodes.EvaluationTasksReview, jobs.Single(j => j.Step == WorkflowStep.B1_HR_REVIEW).Permission);

        // Bước 3a: tập thể ghi nhận và Trưởng đơn vị đề xuất là hai việc riêng.
        var collective = jobs.Where(j => j.Step == WorkflowStep.B3A_COLLECTIVE).ToList();
        Assert.Contains(collective, j => j.Permission == PermissionCodes.EvaluationCollectiveRecord);
        Assert.Contains(collective, j => j.Permission == PermissionCodes.EvaluationUnitReview && j.StepName == WorkflowProfile.UnitLeaderProposalName);
        Assert.All(jobs.Where(j => j.Step != null), j => Assert.NotEmpty(j.ProfileNames));

        var external = Assert.Single(jobs, j => j.Step == null);
        Assert.Equal(PermissionCodes.EvaluationExternalRecord, external.Permission);
        Assert.True(external.InUse);
    }

    [Fact]
    public void Jobs_StepOffEverywhere_ShowsDefaultPermissionNotInUse()
    {
        var settings = PeriodSettings.Default();
        foreach (var profile in settings.Profiles)
            profile.Steps[WorkflowSteps.Code(WorkflowStep.B1_APPROVE)] = new ProfileStepSetting { Mode = StepMode.Off };
        var approve = Assert.Single(PerformerService.BuildJobs(settings.Profiles), j => j.Step == WorkflowStep.B1_APPROVE);
        Assert.Equal(PermissionCodes.EvaluationTasksApprove, approve.Permission);
        Assert.False(approve.InUse);

        // Chưa có kỳ: theo cấu hình mặc định, mọi việc đều dùng, không nêu diện.
        var none = PerformerService.BuildJobs(null);
        Assert.All(none, j => Assert.True(j.InUse));
        Assert.All(none, j => Assert.Empty(j.ProfileNames));
    }

    [Fact]
    public void ParentGrant_CoversChildUnit_ButNotOtherTree()
    {
        // Luật dùng cho màn hình là luật của guard: gán ở đơn vị cha bao trùm đơn vị con, không bao trùm tổ chức Đảng.
        var user = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var child = Guid.NewGuid();
        var grant = new PermissionGrant(PermissionCodes.EvaluationAppraise, ScopeType.Department, parent, Guid.NewGuid(), "Thẩm định", new[] { parent, child });
        var permissions = new EffectivePermissions(user, new[] { grant });

        Assert.True(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationAppraise, new AccessTarget(null, child, null)));
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationAppraise, new AccessTarget(null, null, child)));
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationAppraise, AccessTarget.None));
        Assert.False(AuthorizationGuard.Evaluate(EffectivePermissions.Empty(user), PermissionCodes.EvaluationAppraise, new AccessTarget(null, child, null)));
    }
}
