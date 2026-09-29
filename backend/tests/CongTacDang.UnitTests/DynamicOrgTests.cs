using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Task 14 (T-71, T-72): cây đơn vị (đường dẫn, chống vòng), phạm vi gán bao trùm cây con,
/// suy ra thẩm quyền phê duyệt và mã chức danh thống kê từ chức vụ.
/// </summary>
public class DynamicOrgTests
{
    // Cây:  Root ── Mid ── Leaf
    //            └─ Sibling
    private static readonly Guid Root = Guid.NewGuid();
    private static readonly Guid Mid = Guid.NewGuid();
    private static readonly Guid Leaf = Guid.NewGuid();
    private static readonly Guid Sibling = Guid.NewGuid();

    // ===================== Cây đơn vị =====================

    [Fact]
    public void BuildPaths_ComputesMaterializedPaths_ParentBeforeChildIrrelevant()
    {
        var result = OrgTree.BuildPaths(new[]
        {
            new OrgNodeLink(Leaf, Mid), // con đứng trước cha
            new OrgNodeLink(Mid, Root),
            new OrgNodeLink(Root, null),
            new OrgNodeLink(Sibling, Root)
        });

        Assert.True(result.IsValid);
        Assert.Equal($"/{Root}/", result.Paths[Root]);
        Assert.Equal($"/{Root}/{Mid}/{Leaf}/", result.Paths[Leaf]);
        Assert.True(OrgTree.IsSelfOrDescendant(result.Paths[Leaf], Root));
        Assert.True(OrgTree.IsSelfOrDescendant(result.Paths[Leaf], Mid));
        Assert.False(OrgTree.IsSelfOrDescendant(result.Paths[Sibling], Mid));
    }

    [Fact]
    public void BuildPaths_DetectsCycle_AndMissingParent()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var child = Guid.NewGuid();
        var orphan = Guid.NewGuid();
        var result = OrgTree.BuildPaths(new[]
        {
            new OrgNodeLink(a, b),
            new OrgNodeLink(b, a),
            new OrgNodeLink(child, a),         // treo dưới vòng
            new OrgNodeLink(orphan, Guid.NewGuid()),
            new OrgNodeLink(Root, null)
        });

        Assert.False(result.IsValid);
        Assert.Contains(a, result.CycleNodes);
        Assert.Contains(b, result.CycleNodes);
        Assert.Contains(child, result.CycleNodes);
        Assert.Contains(orphan, result.MissingParentNodes);
        Assert.True(result.Paths.ContainsKey(Root));
    }

    [Fact]
    public void RecomputePaths_CycleThrows400_AndValidTreeUpdatesPaths()
    {
        var root = new AdministrativeDepartment { Code = "ROOT", Name = "Gốc" };
        var child = new AdministrativeDepartment { Code = "CON", Name = "Con", ParentId = root.Id };
        var units = new List<AdministrativeDepartment> { child, root };
        OrganizationService.RecomputePaths(units, "đơn vị");
        Assert.Equal($"/{root.Id}/{child.Id}/", child.Path);

        root.ParentId = child.Id; // tạo vòng
        var ex = Assert.Throws<ValidationException>(() => OrganizationService.RecomputePaths(units, "đơn vị"));
        Assert.Contains("vòng", ex.Message);
    }

    [Fact]
    public void OrderAsTree_ParentsBeforeChildren_SortOrderWithinLevel()
    {
        var root = new PartyCell { Code = "DU", Name = "Đảng ủy", SortOrder = 0 };
        var b = new PartyCell { Code = "CB-B", Name = "B", SortOrder = 2, ParentId = root.Id };
        var a = new PartyCell { Code = "CB-A", Name = "A", SortOrder = 1, ParentId = root.Id };
        var grand = new PartyCell { Code = "TO", Name = "Tổ", ParentId = b.Id };

        var ordered = OrganizationService.OrderAsTree(new List<PartyCell> { grand, b, a, root });

        Assert.Equal(new[] { root.Id, a.Id, b.Id, grand.Id }, ordered.Select(c => c.Id));
    }

    // ===================== Phạm vi bao trùm cây con =====================

    private static EffectivePermissions User(Guid userId, ScopeType type, Guid scopeId, params Guid[] covered)
        => new(userId, new[]
        {
            new PermissionGrant(PermissionCodes.EvaluationRead, type, scopeId, Guid.NewGuid(), "Lãnh đạo", covered),
            new PermissionGrant(PermissionCodes.EvaluationTasksApprove, type, scopeId, Guid.NewGuid(), "Lãnh đạo", covered)
        });

    [Fact]
    public void Guard_DepartmentGrantAtParent_CoversGrandchild_NotSibling()
    {
        var userId = Guid.NewGuid();
        // Resolver mở rộng phạm vi Mid thành {Mid, Leaf} (cây con).
        var permissions = User(userId, ScopeType.Department, Mid, Mid, Leaf);

        Assert.True(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationTasksApprove,
            new AccessTarget(Guid.NewGuid(), DepartmentId: Leaf)));
        Assert.True(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationTasksApprove,
            new AccessTarget(Guid.NewGuid(), DepartmentId: Mid)));
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationTasksApprove,
            new AccessTarget(Guid.NewGuid(), DepartmentId: Sibling)));
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationTasksApprove,
            new AccessTarget(Guid.NewGuid(), DepartmentId: Root)));
    }

    [Fact]
    public void Guard_PartyCellGrant_CoversSubtree_AndScopeFilterListsExpandedIds()
    {
        var userId = Guid.NewGuid();
        var permissions = User(userId, ScopeType.PartyCell, Mid, Mid, Leaf);

        Assert.True(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationRead, new AccessTarget(Guid.NewGuid(), PartyCellId: Leaf)));
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationRead, new AccessTarget(Guid.NewGuid(), PartyCellId: Sibling)));

        var scope = AuthorizationGuard.BuildScope(permissions, PermissionCodes.EvaluationRead);
        Assert.False(scope.IsGlobal);
        Assert.Equal(new[] { Leaf, Mid }.OrderBy(x => x), scope.PartyCellIds.OrderBy(x => x));
        Assert.Empty(scope.DepartmentIds);
        Assert.True(scope.Matches(null, null, Leaf));
        Assert.False(scope.Matches(null, null, Sibling));
    }

    [Fact]
    public void Guard_GrantWithoutExpansion_KeepsExactScope()
    {
        var userId = Guid.NewGuid();
        var permissions = new EffectivePermissions(userId, new[]
        {
            new PermissionGrant(PermissionCodes.EvaluationRead, ScopeType.Department, Mid, Guid.NewGuid(), "Lãnh đạo")
        });

        Assert.True(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationRead, new AccessTarget(Guid.NewGuid(), DepartmentId: Mid)));
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationRead, new AccessTarget(Guid.NewGuid(), DepartmentId: Leaf)));
    }

    [Fact]
    public void Guard_SubtreeScope_StillBlocksConflictOfInterest()
    {
        var userId = Guid.NewGuid();
        var permissions = User(userId, ScopeType.Department, Mid, Mid, Leaf);
        Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationTasksApprove,
            new AccessTarget(userId, DepartmentId: Leaf)));
    }

    // ===================== Chức vụ: thẩm quyền, mã thống kê =====================

    [Fact]
    public void DeriveApprovalAuthority_CapTrenWhenAnyPositionIsCapTren()
    {
        var concurrent = new[]
        {
            new HeldPosition("Trưởng phòng", "M26", ApprovalAuthority.CoSo),
            new HeldPosition("Bí thư Đảng ủy", "M8", ApprovalAuthority.CapTren)
        };
        Assert.Equal(ApprovalAuthority.CapTren, PositionRules.DeriveApprovalAuthority(concurrent));
        Assert.Equal(ApprovalAuthority.CoSo, PositionRules.DeriveApprovalAuthority(new[] { concurrent[0] }));
        Assert.Equal(ApprovalAuthority.CoSo, PositionRules.DeriveApprovalAuthority(Array.Empty<HeldPosition>()));
        Assert.Equal(ApprovalAuthority.CoSo, PositionRules.DeriveApprovalAuthority(new[] { new HeldPosition("Chuyên viên", null, null) }));
    }

    [Fact]
    public void EffectiveApprovalAuthority_ManualOverrideWins()
    {
        var capTren = new[] { new HeldPosition("Giám đốc", "M14", ApprovalAuthority.CapTren) };
        Assert.Equal(ApprovalAuthority.CoSo, PositionRules.EffectiveApprovalAuthority(ApprovalAuthority.CoSo, capTren));
        Assert.Equal(ApprovalAuthority.CapTren, PositionRules.EffectiveApprovalAuthority(null, capTren));
    }

    [Fact]
    public void PersonStatCode_SmallestNumericCodeWins()
    {
        var held = new[]
        {
            new HeldPosition("Trưởng phòng", "M26", ApprovalAuthority.CoSo),
            new HeldPosition("Phó Giám đốc", "M14", ApprovalAuthority.CapTren),
            new HeldPosition("Phó Bí thư Đảng ủy", "M9", ApprovalAuthority.CapTren), // "M9" < "M14" theo số, không theo chữ
            new HeldPosition("Chuyên viên", null, null)
        };
        Assert.Equal("M9", PositionRules.PersonStatCode(held));
        Assert.Null(PositionRules.PersonStatCode(new[] { new HeldPosition("Chuyên viên", null, null) }));
    }

    [Theory]
    [InlineData(" m7 ", "M7")]
    [InlineData("M026", "M26")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeStatCode_Valid(string? input, string? expected)
        => Assert.Equal(expected, PositionRules.NormalizeStatCode(input));

    [Theory]
    [InlineData("X1")]
    [InlineData("M")]
    [InlineData("M0")]
    [InlineData("M1a")]
    public void NormalizeStatCode_Invalid_ThrowsWithVietnameseMessage(string input)
    {
        var ex = Assert.Throws<ArgumentException>(() => PositionRules.NormalizeStatCode(input));
        Assert.Contains("Mã chức danh thống kê", ex.Message);
    }

    [Fact]
    public void BuildInUseMessage_ListsChildrenAssignmentsAndPositions()
    {
        var message = OrganizationService.BuildInUseMessage("đơn vị", "Phòng A",
            new CongTacDang.Application.Common.Interfaces.CatalogUsage(0, 0, 0, 0, Children: 2, Assignments: 1, Positions: 3), "đơn vị khác");
        Assert.Contains("2 đơn vị cấp dưới", message);
        Assert.Contains("1 bản gán vai trò", message);
        Assert.Contains("3 chức vụ", message);
    }
}
