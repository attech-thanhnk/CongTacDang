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
        Assert.Equal(PermissionCodes.EvaluationTasksApprove, jobs[0]);
        Assert.True(jobs.ToList().IndexOf(PermissionCodes.EvaluationAppraise) < jobs.ToList().IndexOf(PermissionCodes.EvaluationDecide));
        Assert.True(jobs.ToList().IndexOf(PermissionCodes.EvaluationDecide) < jobs.ToList().IndexOf(PermissionCodes.EvaluationPublish));
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
