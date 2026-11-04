using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Quy tắc giao bước cho người gần hồ sơ nhất: Công ty → Trung tâm → Đội; Đảng bộ bộ phận → Chi bộ.</summary>
public sealed class StepPerformerRuleTests
{
    private const string Approve = PermissionCodes.EvaluationTasksApprove;

    private static readonly Guid Company = Guid.NewGuid(), Center = Guid.NewGuid(), Team = Guid.NewGuid(), Other = Guid.NewGuid();
    private static readonly Guid Bureau = Guid.NewGuid(), Cell = Guid.NewGuid();

    private static readonly OrgPaths Paths = new(
        new Dictionary<Guid, string>
        {
            [Company] = $"/{Company}/",
            [Center] = $"/{Company}/{Center}/",
            [Team] = $"/{Company}/{Center}/{Team}/",
            [Other] = $"/{Company}/{Other}/"
        },
        new Dictionary<Guid, string>
        {
            [Bureau] = $"/{Bureau}/",
            [Cell] = $"/{Bureau}/{Cell}/"
        });

    private static EffectivePermissions User(Guid id, string code, ScopeType type, Guid? scope, params Guid[] covered) =>
        new(id, new[] { new PermissionGrant(code, type, scope, Guid.NewGuid(), "Vai trò", scope.HasValue ? covered.Prepend(scope.Value).ToList() : null) });

    private static EffectivePermissions CompanyLeader(Guid id, string code = Approve) => User(id, code, ScopeType.Department, Company, Center, Team, Other);

    [Fact]
    public void Nearest_KeepsDeepestScope_ExcludesOwner()
    {
        var company = Guid.NewGuid();
        var center = Guid.NewGuid();
        var candidates = new[] { CompanyLeader(company), User(center, Approve, ScopeType.Department, Center, Team) };

        // Hồ sơ ở Đội: Trung tâm gần hơn Công ty.
        Assert.Equal(new[] { center }, StepPerformerRule.Nearest(candidates, Approve, new AccessTarget(Guid.NewGuid(), Team), Paths));
        // Hồ sơ của chính lãnh đạo Trung tâm: lên Công ty.
        Assert.Equal(new[] { company }, StepPerformerRule.Nearest(candidates, Approve, new AccessTarget(center, Team), Paths));
        // Hồ sơ ở nhánh khác: chỉ Công ty bao trùm.
        Assert.Equal(new[] { company }, StepPerformerRule.Nearest(candidates, Approve, new AccessTarget(Guid.NewGuid(), Other), Paths));
    }

    [Fact]
    public void Nearest_GlobalIsFarthest_SameDepthAllKept()
    {
        var global = Guid.NewGuid();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var candidates = new[] { User(global, Approve, ScopeType.Global, null), CompanyLeader(a), CompanyLeader(b) };
        var result = StepPerformerRule.Nearest(candidates, Approve, new AccessTarget(Guid.NewGuid(), Center), Paths);
        Assert.Equal(new[] { a, b }.OrderBy(x => x), result.OrderBy(x => x));

        // Chỉ có Toàn công ty: mọi người cùng mức.
        var g2 = Guid.NewGuid();
        var globals = new[] { User(global, Approve, ScopeType.Global, null), User(g2, Approve, ScopeType.Global, null) };
        Assert.Equal(2, StepPerformerRule.Nearest(globals, Approve, new AccessTarget(Guid.NewGuid(), Team), Paths).Count);
    }

    [Fact]
    public void Nearest_PartyCellTree_AndPersonWithSeveralGrants()
    {
        var bureau = Guid.NewGuid();
        var cell = Guid.NewGuid();
        var confirm = PermissionCodes.EvaluationCellConfirm;
        var candidates = new[]
        {
            User(bureau, confirm, ScopeType.PartyCell, Bureau, Cell),
            User(cell, confirm, ScopeType.PartyCell, Cell)
        };
        Assert.Equal(new[] { cell }, StepPerformerRule.Nearest(candidates, confirm, new AccessTarget(Guid.NewGuid(), Team, Cell), Paths));

        // Một người có cả bản gán Toàn công ty và bản gán tại đơn vị: tính theo bản gần nhất.
        var both = Guid.NewGuid();
        var multi = new EffectivePermissions(both, new[]
        {
            new PermissionGrant(Approve, ScopeType.Global, null, Guid.NewGuid(), "A"),
            new PermissionGrant(Approve, ScopeType.Department, Team, Guid.NewGuid(), "B", new[] { Team })
        });
        Assert.Equal(new[] { both }, StepPerformerRule.Nearest(new[] { CompanyLeader(Guid.NewGuid()), multi }, Approve, new AccessTarget(Guid.NewGuid(), Team), Paths));
    }

    [Fact]
    public void Distance_NullWhenNotCovered_OrInactive()
    {
        var user = User(Guid.NewGuid(), Approve, ScopeType.Department, Center, Team);
        Assert.Null(StepPerformerRule.Distance(user, Approve, new AccessTarget(Guid.NewGuid(), Other), Paths));
        Assert.Equal(1, StepPerformerRule.Distance(user, Approve, new AccessTarget(Guid.NewGuid(), Team), Paths));
        Assert.Equal(0, StepPerformerRule.Distance(user, Approve, new AccessTarget(Guid.NewGuid(), Center), Paths));
        Assert.Null(StepPerformerRule.Distance(EffectivePermissions.Empty(user.UserId), Approve, new AccessTarget(null, Center), Paths));
        Assert.Empty(StepPerformerRule.Nearest(new[] { user }, Approve, new AccessTarget(Guid.NewGuid(), Other), Paths));
    }
}
