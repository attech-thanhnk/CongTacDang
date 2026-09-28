using System.Linq.Expressions;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Task 09 (T-55): resolver theo bản gán có phạm vi/thời hạn, guard (ma trận phạm vi × mã × chủ hồ sơ × ApprovalAuthority
/// × hiệu lực thời gian, xung đột lợi ích) và các chốt chặn quản trị (docs/thiet-ke/phan-quyen.md mục 4, 5).
/// </summary>
public class AuthorizationGuardTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DeptA = Guid.NewGuid();
    private static readonly Guid DeptB = Guid.NewGuid();
    private static readonly Guid CellA = Guid.NewGuid();
    private static readonly Guid CellB = Guid.NewGuid();

    #region Resolver

    [Fact]
    public async Task Resolver_UsesOnlyEffectiveAssignments_ValidToExclusive()
    {
        var store = new AuthzStore();
        var user = store.AddUser();
        var role = store.AddRole("R", PermissionCodes.EvaluationRead);
        store.Assign(user, role, RoleScopeType.Global, validFrom: Now.AddDays(-1));                       // đang hiệu lực
        store.Assign(user, store.AddRole("Future", PermissionCodes.EvaluationAppraise), RoleScopeType.Global, validFrom: Now.AddDays(1));
        store.Assign(user, store.AddRole("Expired", PermissionCodes.EvaluationDecide), RoleScopeType.Global,
            validFrom: Now.AddDays(-10), validTo: Now);                                                     // ValidTo = now → đã hết
        store.Assign(user, store.AddRole("Deleted", PermissionCodes.EvaluationPublish), RoleScopeType.Global,
            validFrom: Now.AddDays(-1), deleted: true);
        var deletedRole = store.AddRole("RoleDeleted", PermissionCodes.EvaluationReopen);
        deletedRole.IsDeleted = true;
        store.Assign(user, deletedRole, RoleScopeType.Global, validFrom: Now.AddDays(-1));

        var result = await NewResolver(store, new ManualClock(Now)).GetAsync(user.Id);

        Assert.True(result.IsActive);
        Assert.Equal(new[] { PermissionCodes.EvaluationRead }, result.Codes.ToArray());
        var grant = Assert.Single(result.Grants);
        Assert.Equal("R", grant.SourceRoleName);
        Assert.NotEqual(Guid.Empty, grant.SourceAssignmentId);
    }

    [Fact]
    public async Task Resolver_SkipsUnknownCodes_AndGlobalOnlyCodesOnScopedAssignments()
    {
        var store = new AuthzStore();
        var user = store.AddUser();
        var role = store.AddRole("Mixed", PermissionCodes.EvaluationRead, PermissionCodes.PeriodManage, "users.read", "ma.la");
        store.Assign(user, role, RoleScopeType.Department, DeptA);
        store.Assign(user, store.AddRole("G", PermissionCodes.CatalogManage), RoleScopeType.Global);

        var result = await NewResolver(store, new ManualClock(Now)).GetAsync(user.Id);

        Assert.True(result.Has(PermissionCodes.EvaluationRead));
        Assert.True(result.Has(PermissionCodes.CatalogManage));
        Assert.False(result.Has(PermissionCodes.PeriodManage)); // không áp dụng phạm vi + gán theo Phòng → bỏ
        Assert.False(result.Has("users.read"));
        Assert.False(result.Has("ma.la"));
        var read = Assert.Single(result.GrantsFor(PermissionCodes.EvaluationRead));
        Assert.Equal(ScopeType.Department, read.ScopeType);
        Assert.Equal(DeptA, read.ScopeId);
    }

    [Fact]
    public async Task Resolver_InactiveOrMissingUser_IsEmptyAndInactive()
    {
        var store = new AuthzStore();
        var user = store.AddUser(active: false);
        store.Assign(user, store.AddRole("R", PermissionCodes.EvaluationRead), RoleScopeType.Global);
        var resolver = NewResolver(store, new ManualClock(Now));

        var inactive = await resolver.GetAsync(user.Id);
        var missing = await resolver.GetAsync(Guid.NewGuid());

        Assert.False(inactive.IsActive);
        Assert.Empty(inactive.Grants);
        Assert.False(missing.IsActive);
        Assert.False(AuthorizationGuard.Evaluate(inactive, PermissionCodes.EvaluationRead, new AccessTarget(user.Id)));
    }

    [Fact]
    public async Task Resolver_Cache_ExpiresAtAssignmentBoundary_AndOnInvalidation()
    {
        var clock = new ManualClock(Now);
        var store = new AuthzStore();
        var user = store.AddUser();
        store.Assign(user, store.AddRole("Short", PermissionCodes.EvaluationRead), RoleScopeType.Global,
            validFrom: Now.AddHours(-1), validTo: Now.AddMinutes(1));
        store.Assign(user, store.AddRole("Later", PermissionCodes.EvaluationAppraise), RoleScopeType.Global,
            validFrom: Now.AddMinutes(2));
        var cache = new PermissionCache(clock, TimeSpan.FromMinutes(5));
        var resolver = new PermissionResolver((IRoleAssignmentRepository)store, cache);

        Assert.True((await resolver.GetAsync(user.Id)).Has(PermissionCodes.EvaluationRead));
        await resolver.GetAsync(user.Id);
        Assert.Equal(1, store.SnapshotLoads);

        clock.Advance(TimeSpan.FromSeconds(61)); // bản gán hết hạn trước TTL 5 phút → phải nạp lại
        Assert.False((await resolver.GetAsync(user.Id)).Has(PermissionCodes.EvaluationRead));
        Assert.Equal(2, store.SnapshotLoads);

        clock.Advance(TimeSpan.FromMinutes(1)); // bản gán mới bắt đầu hiệu lực
        Assert.True((await resolver.GetAsync(user.Id)).Has(PermissionCodes.EvaluationAppraise));
        Assert.Equal(3, store.SnapshotLoads);

        cache.InvalidateUser(user.Id);
        await resolver.GetAsync(user.Id);
        Assert.Equal(4, store.SnapshotLoads);
        cache.InvalidateAll();
        await resolver.GetAsync(user.Id);
        Assert.Equal(5, store.SnapshotLoads);

        clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1))); // TTL
        await resolver.GetAsync(user.Id);
        Assert.Equal(6, store.SnapshotLoads);
    }

    #endregion

    #region Guard — ma trận

    private static EffectivePermissions Perms(Guid userId, params (string Code, ScopeType Type, Guid? Id)[] grants) =>
        new(userId, grants.Select(g => new PermissionGrant(g.Code, g.Type, g.Id, Guid.NewGuid(), "Test")));

    public static IEnumerable<object?[]> ScopeMatrix()
    {
        // scopeType, scopeId, target dept, target cell, expected
        yield return new object?[] { ScopeType.Global, null, DeptA, CellA, true };
        yield return new object?[] { ScopeType.Global, null, null, null, true };
        yield return new object?[] { ScopeType.Department, DeptA, DeptA, CellB, true };
        yield return new object?[] { ScopeType.Department, DeptA, DeptB, CellA, false };
        yield return new object?[] { ScopeType.Department, DeptA, null, CellA, false };
        yield return new object?[] { ScopeType.PartyCell, CellA, DeptB, CellA, true };
        yield return new object?[] { ScopeType.PartyCell, CellA, DeptA, CellB, false };
        yield return new object?[] { ScopeType.PartyCell, CellA, DeptA, null, false };
    }

    [Theory]
    [MemberData(nameof(ScopeMatrix))]
    public void Guard_ScopeMatrix_ForScopedCodes(ScopeType type, Guid? scopeId, Guid? dept, Guid? cell, bool expected)
    {
        var userId = Guid.NewGuid();
        var target = new AccessTarget(Guid.NewGuid(), dept, cell, ApprovalAuthority.CoSo);
        foreach (var code in new[]
                 {
                     PermissionCodes.EvaluationRead, PermissionCodes.EvaluationTasksApprove, PermissionCodes.EvaluationCellConfirm,
                     PermissionCodes.EvaluationCollectiveRecord, PermissionCodes.EvaluationAppraise, PermissionCodes.EvaluationDirectorReview,
                     PermissionCodes.EvaluationDecide, PermissionCodes.EvaluationPublish, PermissionCodes.EvaluationReopen,
                     PermissionCodes.CollectiveManage, PermissionCodes.MeetingRead, PermissionCodes.MeetingManage,
                     PermissionCodes.ReportExport, PermissionCodes.SystemUsersRead, PermissionCodes.SystemUsersManage
                 })
        {
            var permissions = Perms(userId, (code, type, scopeId));
            Assert.True(expected == AuthorizationGuard.Evaluate(permissions, code, target), $"{code} {type}");
            // Có quyền khác mã → không được.
            Assert.False(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationDecideExternal, target));
        }
    }

    [Fact]
    public void Guard_GlobalOnlyCodes_IgnoreScopedGrants()
    {
        var userId = Guid.NewGuid();
        var scoped = Perms(userId, (PermissionCodes.PeriodManage, ScopeType.Department, DeptA));
        var global = Perms(userId, (PermissionCodes.PeriodManage, ScopeType.Global, null));

        Assert.False(AuthorizationGuard.Evaluate(scoped, PermissionCodes.PeriodManage, new AccessTarget(DepartmentId: DeptA)));
        Assert.True(AuthorizationGuard.Evaluate(global, PermissionCodes.PeriodManage, AccessTarget.None));
        Assert.False(AuthorizationGuard.BuildScope(scoped, PermissionCodes.PeriodManage).DepartmentIds.Any());
    }

    [Fact]
    public void Guard_EvaluationSelf_OnlyOwner_ScopeIgnored()
    {
        var userId = Guid.NewGuid();
        var withSelf = Perms(userId, (PermissionCodes.EvaluationSelf, ScopeType.PartyCell, CellB));
        var without = Perms(userId, (PermissionCodes.EvaluationRead, ScopeType.Global, null));

        Assert.True(AuthorizationGuard.Evaluate(withSelf, PermissionCodes.EvaluationSelf, new AccessTarget(userId, DeptA, CellA)));
        Assert.False(AuthorizationGuard.Evaluate(withSelf, PermissionCodes.EvaluationSelf, new AccessTarget(Guid.NewGuid(), DeptA, CellB)));
        Assert.False(AuthorizationGuard.Evaluate(without, PermissionCodes.EvaluationSelf, new AccessTarget(userId)));
        Assert.Equal(ScopeFilter.OwnerOnly(userId), AuthorizationGuard.BuildScope(withSelf, PermissionCodes.EvaluationSelf));
        Assert.True(AuthorizationGuard.BuildScope(without, PermissionCodes.EvaluationSelf).IsEmpty);
    }

    [Fact]
    public void Guard_EvaluationRead_OwnerAlways_OthersByScope()
    {
        var userId = Guid.NewGuid();
        var none = Perms(userId);
        var cell = Perms(userId, (PermissionCodes.EvaluationRead, ScopeType.PartyCell, CellA));

        Assert.True(AuthorizationGuard.Evaluate(none, PermissionCodes.EvaluationRead, new AccessTarget(userId, DeptB, CellB)));
        Assert.False(AuthorizationGuard.Evaluate(none, PermissionCodes.EvaluationRead, new AccessTarget(Guid.NewGuid(), DeptA, CellA)));
        Assert.True(AuthorizationGuard.Evaluate(cell, PermissionCodes.EvaluationRead, new AccessTarget(Guid.NewGuid(), DeptB, CellA)));

        var scope = AuthorizationGuard.BuildScope(cell, PermissionCodes.EvaluationRead);
        Assert.False(scope.IsGlobal);
        Assert.Equal(new[] { CellA }, scope.PartyCellIds);
        Assert.Equal(userId, scope.OwnerId);
        Assert.True(scope.Matches(userId, null, null));
        Assert.True(scope.Matches(Guid.NewGuid(), null, CellA));
        Assert.False(scope.Matches(Guid.NewGuid(), DeptA, CellB));
        Assert.Equal(userId, AuthorizationGuard.BuildScope(none, PermissionCodes.EvaluationRead).OwnerId);
    }

    [Theory]
    [InlineData(ApprovalAuthority.CoSo, true, false)]
    [InlineData(ApprovalAuthority.CapTren, false, true)]
    public void Guard_Decide_FollowsApprovalAuthority(ApprovalAuthority authority, bool local, bool external)
    {
        var userId = Guid.NewGuid();
        var both = Perms(userId,
            (PermissionCodes.EvaluationDecide, ScopeType.Global, null),
            (PermissionCodes.EvaluationDecideExternal, ScopeType.Global, null));
        var target = new AccessTarget(Guid.NewGuid(), DeptA, CellA, authority);

        Assert.Equal(local, AuthorizationGuard.Evaluate(both, PermissionCodes.EvaluationDecide, target));
        Assert.Equal(external, AuthorizationGuard.Evaluate(both, PermissionCodes.EvaluationDecideExternal, target));
        Assert.False(AuthorizationGuard.Evaluate(both, PermissionCodes.EvaluationDecide, new AccessTarget(Guid.NewGuid())));
    }

    [Fact]
    public void Guard_ConflictOfInterest_NoApprovalOnOwnRecord()
    {
        var userId = Guid.NewGuid();
        var codes = AuthorizationGuard.ConflictOfInterestCodes.ToList();
        Assert.Equal(9, codes.Count);
        var permissions = Perms(userId, codes.Select(c => (c, ScopeType.Global, (Guid?)null))
            .Append((PermissionCodes.EvaluationRead, ScopeType.Global, null)).ToArray());

        foreach (var authority in new[] { ApprovalAuthority.CoSo, ApprovalAuthority.CapTren })
        {
            var own = new AccessTarget(userId, DeptA, CellA, authority);
            var other = new AccessTarget(Guid.NewGuid(), DeptA, CellA, authority);
            foreach (var code in codes)
            {
                Assert.False(AuthorizationGuard.Evaluate(permissions, code, own), code);
                var expectedOnOther = code switch
                {
                    PermissionCodes.EvaluationDecide => authority == ApprovalAuthority.CoSo,
                    PermissionCodes.EvaluationDecideExternal => authority == ApprovalAuthority.CapTren,
                    _ => true
                };
                Assert.Equal(expectedOnOther, AuthorizationGuard.Evaluate(permissions, code, other));
            }

            Assert.True(AuthorizationGuard.Evaluate(permissions, PermissionCodes.EvaluationRead, own));
        }
    }

    [Fact]
    public void Guard_Instance_UsesCurrentUser_EnsureMessageUsesDisplayName()
    {
        var store = new AuthzStore();
        var user = store.AddUser();
        store.Assign(user, store.AddRole("Chi ủy", PermissionCodes.EvaluationCellConfirm), RoleScopeType.PartyCell, CellA);
        var guard = new AuthorizationGuard(new FixedCurrentUser(user.Id), NewResolver(store, new ManualClock(Now)));

        Assert.True(guard.HasAny(PermissionCodes.EvaluationCellConfirm));
        Assert.True(guard.Can(PermissionCodes.EvaluationCellConfirm, new AccessTarget(Guid.NewGuid(), null, CellA)));
        var ex = Assert.Throws<ForbiddenException>(() =>
            guard.Ensure(PermissionCodes.EvaluationCellConfirm, new AccessTarget(Guid.NewGuid(), null, CellB)));
        Assert.Contains("Chi bộ xác nhận phiếu tự chấm", ex.Message);
        Assert.DoesNotContain(PermissionCodes.EvaluationCellConfirm, ex.Message);

        var own = Assert.Throws<ForbiddenException>(() =>
            guard.Ensure(PermissionCodes.EvaluationCellConfirm, new AccessTarget(user.Id, null, CellA)));
        Assert.Contains("xung đột lợi ích", own.Message);
    }

    [Fact]
    public void Guard_Anonymous_HasNothing()
    {
        var guard = new AuthorizationGuard(new FixedCurrentUser(null), NewResolver(new AuthzStore(), new ManualClock(Now)));

        Assert.False(guard.HasAny(PermissionCodes.EvaluationRead));
        Assert.False(guard.Can(PermissionCodes.EvaluationRead, AccessTarget.None));
        Assert.True(guard.GetScope(PermissionCodes.EvaluationRead).IsEmpty);
        Assert.Throws<ForbiddenException>(() => guard.Ensure(PermissionCodes.CatalogManage, AccessTarget.None));
    }

    [Fact]
    public void ScopeFilter_Union_CombinesScopes()
    {
        var a = new ScopeFilter(false, new[] { DeptA }, Array.Empty<Guid>());
        var b = new ScopeFilter(false, Array.Empty<Guid>(), new[] { CellA });

        var union = a.Union(b);
        Assert.True(union.Matches(null, DeptA, null));
        Assert.True(union.Matches(null, null, CellA));
        Assert.False(union.Matches(null, DeptB, CellB));
        Assert.True(a.Union(ScopeFilter.Global).IsGlobal);
    }

    #endregion

    #region Chốt chặn — bản gán

    [Fact]
    public void AdministratorInvariant_RequiresBothAdminCodes()
    {
        var roles = new AdministratorGrantRow(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { PermissionCodes.SystemRolesManage });
        var assignments = new AdministratorGrantRow(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new[] { PermissionCodes.SystemAssignmentsManage });

        Assert.True(AdministratorInvariant.Holds(new[] { roles, assignments }));
        Assert.False(AdministratorInvariant.Holds(new[] { roles }));
        Assert.True(AdministratorInvariant.IsBrokenBy(new[] { roles, assignments }, new[] { roles }));
        Assert.False(AdministratorInvariant.IsBrokenBy(new[] { roles }, Array.Empty<AdministratorGrantRow>())); // vốn đã không thỏa
    }

    [Fact]
    public async Task Assign_ToSelf_IsForbidden()
    {
        var world = new ServiceWorld();

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() =>
            world.Assignments.AssignAsync(world.Admin.Id, world.EvaluateeRole.Id, ScopeType.Global, null, null, null, null));
        Assert.Contains("chính mình", ex.Message);
        Assert.DoesNotContain(world.Store.Assignments, a => a.UserId == world.Admin.Id && a.RoleId == world.EvaluateeRole.Id);
    }

    [Fact]
    public async Task Assign_RequiresGlobalAssignmentsManage()
    {
        var world = new ServiceWorld();
        var scopedAdmin = world.Store.AddUser();
        var role = world.Store.AddRole("Gán theo Phòng?", PermissionCodes.SystemUsersRead);
        world.Store.Assign(scopedAdmin, role, RoleScopeType.Department, DeptA);
        var service = world.AssignmentServiceFor(scopedAdmin);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.AssignAsync(world.Store.AddUser().Id, world.EvaluateeRole.Id, ScopeType.Global, null, null, null, null));
    }

    [Fact]
    public async Task Assign_GlobalOnlyRole_WithScope_IsRejected()
    {
        var world = new ServiceWorld();
        var target = world.Store.AddUser();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            world.Assignments.AssignAsync(target.Id, world.AppraisalRole.Id, ScopeType.Department, DeptA, null, null, null));
        Assert.Contains("Toàn công ty", ex.Message);
    }

    [Theory]
    [InlineData(ScopeType.Department, false, "Phòng")]
    [InlineData(ScopeType.PartyCell, false, "Chi bộ")]
    [InlineData(ScopeType.Global, true, "Toàn công ty")]
    public async Task Assign_InvalidScope_IsRejected(ScopeType type, bool withScopeId, string expected)
    {
        var world = new ServiceWorld();
        var target = world.Store.AddUser();
        Guid? scopeId = withScopeId ? DeptA : type == ScopeType.Department ? Guid.NewGuid() : null;

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            world.Assignments.AssignAsync(target.Id, world.ReaderRole.Id, type, scopeId, null, null, null));
        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public async Task Assign_Validates_DatesAndDuplicates_AndInvalidatesCache()
    {
        var world = new ServiceWorld();
        var target = world.Store.AddUser();

        await Assert.ThrowsAsync<ValidationException>(() =>
            world.Assignments.AssignAsync(target.Id, world.ReaderRole.Id, ScopeType.Department, DeptA, Now, Now, null));

        var created = await world.Assignments.AssignAsync(target.Id, world.ReaderRole.Id, ScopeType.Department, DeptA, null, null, " Quyết định 12 ");
        Assert.Equal("Department", created.ScopeType);
        Assert.Equal("Phòng A", created.ScopeName);
        Assert.Equal("Quyết định 12", created.Note);
        Assert.Equal("Active", created.Status);
        Assert.Contains(target.Id, world.Cache.Users);

        var dup = await Assert.ThrowsAsync<ValidationException>(() =>
            world.Assignments.AssignAsync(target.Id, world.ReaderRole.Id, ScopeType.Department, DeptA, Now.AddDays(3), null, null));
        Assert.Contains("trùng", dup.Message);

        // Khác phạm vi → hợp lệ.
        await world.Assignments.AssignAsync(target.Id, world.ReaderRole.Id, ScopeType.Department, DeptB, null, null, null);
    }

    [Fact]
    public async Task End_And_Delete_LastAdministrator_AreRejected()
    {
        var world = new ServiceWorld();
        var adminAssignment = world.Store.Assignments.Single(a => a.UserId == world.OtherAdmin.Id);

        // Người thao tác (world.Admin) cũng là quản trị → gỡ OtherAdmin vẫn còn quản trị → được.
        var ended = await world.Assignments.EndAsync(adminAssignment.Id);
        Assert.Equal("Expired", ended.Status);

        // Một quản trị khác gỡ quản trị cuối cùng (world.Admin) → 409.
        var helper = world.Store.AddUser();
        world.Store.Assign(helper, world.Store.AddRole("Chỉ gán", PermissionCodes.SystemAssignmentsManage), RoleScopeType.Global);
        var service = world.AssignmentServiceFor(helper);
        var last = world.Store.Assignments.Single(a => a.UserId == world.Admin.Id && a.RoleId == world.AdminRole.Id);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.EndAsync(last.Id));
        Assert.Contains("Quản lý vai trò", ex.Message);
        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(last.Id));
        await Assert.ThrowsAsync<ConflictException>(() => service.EnsureAdministratorsRemainWithoutUserAsync(world.Admin.Id));
    }

    [Fact]
    public async Task EffectivePermissions_ListsScopeNamesAndSources()
    {
        var world = new ServiceWorld();
        var target = world.Store.AddUser();
        await world.Assignments.AssignAsync(target.Id, world.ReaderRole.Id, ScopeType.PartyCell, CellA, null, null, null);
        await world.Assignments.AssignAsync(target.Id, world.EvaluateeRole.Id, ScopeType.Global, null, null, null, null);

        var result = await world.Assignments.GetEffectivePermissionsAsync(target.Id);
        var grants = await world.Assignments.GetGrantsAsync(target.Id);

        Assert.True(result.IsActive);
        var read = Assert.Single(result.Permissions, p => p.Code == PermissionCodes.EvaluationRead);
        var source = Assert.Single(read.Sources);
        Assert.Equal("Chi bộ A", source.ScopeName);
        Assert.Equal("Đọc hồ sơ", source.RoleName);
        Assert.Contains(grants, g => g.Code == PermissionCodes.EvaluationSelf && g.ScopeName == RoleAssignmentService.GlobalScopeName);
        Assert.Equal(2, result.Assignments.Count);
    }

    #endregion

    #region Chốt chặn — vai trò

    [Fact]
    public async Task DeleteRole_ProtectedOrAssigned_IsConflict_OtherwiseSoftDeleted()
    {
        var world = new ServiceWorld();

        await Assert.ThrowsAsync<ConflictException>(() => world.Roles.DeleteRoleAsync(world.AdminRole.Id));
        world.Store.Assign(world.Store.AddUser(), world.ReaderRole, RoleScopeType.Global);
        var assigned = await Assert.ThrowsAsync<ConflictException>(() => world.Roles.DeleteRoleAsync(world.ReaderRole.Id));
        Assert.Contains("1 bản gán", assigned.Message);

        var empty = world.Store.AddRole("Trống", PermissionCodes.MeetingRead);
        await world.Roles.DeleteRoleAsync(empty.Id);
        Assert.True(empty.IsDeleted);
    }

    [Fact]
    public async Task UpdateRolePermissions_Guards()
    {
        var world = new ServiceWorld();

        // Vai trò bảo vệ: không gỡ được 2 quyền quản trị (người thao tác không được gán vai trò này).
        var roleManager = world.Store.AddUser();
        world.Store.Assign(roleManager, world.Store.AddRole("Chỉ quản lý vai trò", PermissionCodes.SystemRolesManage), RoleScopeType.Global);
        var otherActor = world.RolesServiceFor(roleManager);
        var protectedEx = await Assert.ThrowsAsync<ConflictException>(() =>
            otherActor.UpdateRolePermissionsAsync(world.AdminRole.Id, new[] { PermissionCodes.SystemRolesManage }));
        Assert.Contains("bảo vệ", protectedEx.Message);

        // Không sửa quyền của vai trò mình đang được gán.
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            world.Roles.UpdateRolePermissionsAsync(world.AdminRole.Id, AdministratorInvariant.Codes));

        // Mã lạ → 400.
        await Assert.ThrowsAsync<ValidationException>(() =>
            world.Roles.UpdateRolePermissionsAsync(world.ReaderRole.Id, new[] { "users.read" }));

        // Vai trò đang gán theo Phòng không nhận quyền chỉ áp dụng Toàn công ty.
        world.Store.Assign(world.Store.AddUser(), world.ReaderRole, RoleScopeType.Department, DeptA);
        await Assert.ThrowsAsync<ValidationException>(() =>
            world.Roles.UpdateRolePermissionsAsync(world.ReaderRole.Id, new[] { PermissionCodes.EvaluationRead, PermissionCodes.PeriodManage }));

        // Hợp lệ → lưu, ghi audit, xóa toàn bộ cache.
        var updated = await world.Roles.UpdateRolePermissionsAsync(world.ReaderRole.Id, new[] { PermissionCodes.EvaluationRead, PermissionCodes.MeetingRead });
        Assert.Equal(new[] { PermissionCodes.EvaluationRead, PermissionCodes.MeetingRead }, updated.PermissionCodes);
        Assert.Equal(1, world.Store.AuditCount);
        Assert.True(world.Cache.AllCount > 0);
    }

    [Fact]
    public async Task UpdateRolePermissions_RemovingLastAdminCodes_IsConflict()
    {
        var world = new ServiceWorld();
        // Vai trò quản trị không bảo vệ, là nguồn duy nhất của system.assignments.manage.
        var soleAssigner = world.Store.AddRole("Gán duy nhất", PermissionCodes.SystemAssignmentsManage);
        world.Store.Assignments.RemoveAll(a => a.RoleId == world.AdminRole.Id && a.UserId == world.OtherAdmin.Id);
        ((List<Permission>)world.AdminRole.Permissions).RemoveAll(p => p.Code == PermissionCodes.SystemAssignmentsManage);
        world.Store.Assign(world.OtherAdmin, soleAssigner, RoleScopeType.Global);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            world.Roles.UpdateRolePermissionsAsync(soleAssigner.Id, new[] { PermissionCodes.SystemAuditRead }));
        Assert.Contains("Gán vai trò", ex.Message);
    }

    [Fact]
    public async Task CreateRole_DuplicateName_IsConflict_AndCodeIsGenerated()
    {
        var world = new ServiceWorld();

        var created = await world.Roles.CreateRoleAsync(new CreateRoleRequestDto
        {
            Name = "  Thư ký  ",
            PermissionCodes = new List<string> { PermissionCodes.MeetingManage }
        });
        Assert.Equal("Thư ký", created.Name);
        Assert.StartsWith("ROLE_", world.Store.Roles.Single(r => r.Id == created.Id).Code);
        await Assert.ThrowsAsync<ConflictException>(() => world.Roles.CreateRoleAsync(new CreateRoleRequestDto { Name = "thư ký" }));
        await Assert.ThrowsAsync<ValidationException>(() => world.Roles.CreateRoleAsync(new CreateRoleRequestDto { Name = " " }));
    }

    [Fact]
    public async Task PermissionCatalog_GroupsByModule_WithAppliesScope()
    {
        var world = new ServiceWorld();
        var catalog = await world.Roles.GetPermissionCatalogAsync();

        Assert.Equal(PermissionCodes.All.Length, catalog.Sum(m => m.Permissions.Count));
        var system = catalog.Single(m => m.Module == "system");
        Assert.Equal("Quản trị hệ thống", system.ModuleName);
        Assert.False(system.Permissions.Single(p => p.Code == PermissionCodes.SystemRolesManage).AppliesScope);
        Assert.True(system.Permissions.Single(p => p.Code == PermissionCodes.SystemUsersRead).AppliesScope);
    }

    #endregion

    #region Hỗ trợ

    private static PermissionResolver NewResolver(AuthzStore store, TimeProvider clock) =>
        new((IRoleAssignmentRepository)store, new PermissionCache(clock));

    /// <summary>Bối cảnh dịch vụ quản trị: 2 quản trị (vai trò bảo vệ), vài vai trò mẫu, Phòng/Chi bộ A-B.</summary>
    private sealed class ServiceWorld
    {
        public AuthzStore Store { get; } = new();
        public RecordingCache Cache { get; } = new();
        public ManualClock Clock { get; } = new(Now);
        public AppRole AdminRole { get; }
        public AppRole EvaluateeRole { get; }
        public AppRole ReaderRole { get; }
        public AppRole AppraisalRole { get; }
        public PartyMemberProfile Admin { get; }
        public PartyMemberProfile OtherAdmin { get; }
        public RoleAssignmentService Assignments { get; }
        public RoleService Roles { get; }

        public ServiceWorld()
        {
            Store.Scopes[DeptA] = (RoleScopeType.Department, "Phòng A");
            Store.Scopes[DeptB] = (RoleScopeType.Department, "Phòng B");
            Store.Scopes[CellA] = (RoleScopeType.PartyCell, "Chi bộ A");
            Store.Scopes[CellB] = (RoleScopeType.PartyCell, "Chi bộ B");

            AdminRole = Store.AddRole("Quản trị hệ thống",
                PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage, PermissionCodes.SystemUsersRead);
            AdminRole.IsProtected = true;
            EvaluateeRole = Store.AddRole("Người được đánh giá", PermissionCodes.EvaluationSelf);
            ReaderRole = Store.AddRole("Đọc hồ sơ", PermissionCodes.EvaluationRead);
            AppraisalRole = Store.AddRole("Thẩm định", PermissionCodes.EvaluationAppraise, PermissionCodes.PeriodManage);

            Admin = Store.AddUser();
            OtherAdmin = Store.AddUser();
            Store.Assign(Admin, AdminRole, RoleScopeType.Global, validFrom: Now.AddDays(-1));
            Store.Assign(OtherAdmin, AdminRole, RoleScopeType.Global, validFrom: Now.AddDays(-1));

            Assignments = AssignmentServiceFor(Admin);
            Roles = RolesServiceFor(Admin);
        }

        private AuthorizationGuard GuardFor(PartyMemberProfile actor) =>
            new(new FixedCurrentUser(actor.Id), new PermissionResolver((IRoleAssignmentRepository)Store, new PermissionCache(Clock)));

        public RoleAssignmentService AssignmentServiceFor(PartyMemberProfile actor) =>
            new(Store, Store, Store, GuardFor(actor), new FixedCurrentUser(actor.Id), Cache,
                new PermissionResolver((IRoleAssignmentRepository)Store, new PermissionCache(Clock)), Clock);

        public RoleService RolesServiceFor(PartyMemberProfile actor) =>
            new(Store, Store, GuardFor(actor), new FixedCurrentUser(actor.Id), Cache, Clock);
    }

    /// <summary>Kho trong bộ nhớ cho vai trò, bản gán, người dùng (mô phỏng query filter xóa mềm).</summary>
    private sealed class AuthzStore : IRoleRepository, IRoleAssignmentRepository, IUserRepository
    {
        public List<PartyMemberProfile> Users { get; } = new();
        public List<AppRole> Roles { get; } = new();
        public List<UserRoleAssignment> Assignments { get; } = new();
        public Dictionary<Guid, (RoleScopeType Type, string Name)> Scopes { get; } = new();
        public int SnapshotLoads { get; private set; }
        public int AuditCount { get; private set; }

        public PartyMemberProfile AddUser(bool active = true)
        {
            var user = new PartyMemberProfile { Id = Guid.NewGuid(), Username = "u" + Guid.NewGuid().ToString("N")[..6], FullName = "Người dùng", IsActive = active };
            Users.Add(user);
            return user;
        }

        public AppRole AddRole(string name, params string[] codes)
        {
            var role = new AppRole
            {
                Id = Guid.NewGuid(),
                Code = "T_" + Guid.NewGuid().ToString("N")[..8],
                Name = name,
                Permissions = codes.Select(c => new Permission { Id = Guid.NewGuid(), Code = c }).ToList()
            };
            Roles.Add(role);
            return role;
        }

        public UserRoleAssignment Assign(PartyMemberProfile user, AppRole role, RoleScopeType scope, Guid? scopeId = null,
            DateTime? validFrom = null, DateTime? validTo = null, bool deleted = false)
        {
            var assignment = new UserRoleAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                User = user,
                RoleId = role.Id,
                Role = role,
                ScopeType = scope,
                ScopeId = scopeId,
                ValidFrom = validFrom ?? Now.AddDays(-1),
                ValidTo = validTo,
                IsDeleted = deleted
            };
            Assignments.Add(assignment);
            return assignment;
        }

        private IEnumerable<UserRoleAssignment> Visible =>
            Assignments.Where(a => !a.IsDeleted && Roles.Any(r => r.Id == a.RoleId && !r.IsDeleted));

        // IRoleAssignmentRepository
        public Task<UserAccessSnapshot?> GetAccessSnapshotAsync(Guid userId, DateTime now, CancellationToken ct = default)
        {
            SnapshotLoads++;
            var user = Users.FirstOrDefault(u => u.Id == userId && !u.IsDeleted);
            if (user == null)
                return Task.FromResult<UserAccessSnapshot?>(null);
            var rows = Visible
                .Where(a => a.UserId == userId && (a.ValidTo == null || a.ValidTo > now))
                .Select(a =>
                {
                    var role = Roles.Single(r => r.Id == a.RoleId);
                    return new AssignmentGrantSource(a.Id, role.Id, role.Name, a.ScopeType, a.ScopeId, a.ValidFrom, a.ValidTo,
                        role.Permissions.Where(p => !p.IsDeleted).Select(p => p.Code).ToList());
                })
                .ToList();
            return Task.FromResult<UserAccessSnapshot?>(new UserAccessSnapshot(user.IsActive, rows));
        }

        public Task<List<UserRoleAssignment>> QueryAsync(RoleAssignmentFilter filter, CancellationToken ct = default) =>
            Task.FromResult(Visible
                .Where(a => filter.Id == null || a.Id == filter.Id)
                .Where(a => filter.UserId == null || a.UserId == filter.UserId)
                .Where(a => filter.RoleId == null || a.RoleId == filter.RoleId)
                .Where(a => filter.ScopeType == null || a.ScopeType == filter.ScopeType)
                .Where(a => filter.ScopeId == null || a.ScopeId == filter.ScopeId)
                .Where(a => filter.ActiveOn == null || (a.ValidFrom <= filter.ActiveOn && (a.ValidTo == null || a.ValidTo > filter.ActiveOn)))
                .ToList());

        public Task<UserRoleAssignment?> GetForUpdateAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Assignments.FirstOrDefault(a => a.Id == id && !a.IsDeleted));

        public Task<List<UserRoleAssignment>> GetCurrentOrFutureForUserAsync(Guid userId, DateTime now, CancellationToken ct = default) =>
            Task.FromResult(Visible.Where(a => a.UserId == userId && (a.ValidTo == null || a.ValidTo > now)).ToList());

        public Task<int> CountCurrentOrFutureByRoleAsync(Guid roleId, DateTime now, CancellationToken ct = default) =>
            Task.FromResult(Visible.Count(a => a.RoleId == roleId && (a.ValidTo == null || a.ValidTo > now)));

        public Task<bool> HasNonGlobalCurrentOrFutureAssignmentsAsync(Guid roleId, DateTime now, CancellationToken ct = default) =>
            Task.FromResult(Visible.Any(a => a.RoleId == roleId && a.ScopeType != RoleScopeType.Global && (a.ValidTo == null || a.ValidTo > now)));

        public Task<List<AdministratorGrantRow>> GetAdministratorGrantsAsync(DateTime now, CancellationToken ct = default) =>
            Task.FromResult(Visible
                .Where(a => a.ScopeType == RoleScopeType.Global && a.IsEffectiveAt(now))
                .Where(a => Users.Any(u => u.Id == a.UserId && u.IsActive && !u.IsDeleted))
                .Select(a => new AdministratorGrantRow(a.Id, a.UserId, a.RoleId,
                    Roles.Single(r => r.Id == a.RoleId).Permissions.Select(p => p.Code).Where(AdministratorInvariant.Codes.Contains).ToList()))
                .Where(r => r.PermissionCodes.Count > 0)
                .ToList());

        public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Users.Any(u => u.Id == userId && !u.IsDeleted));

        public Task<bool> ScopeExistsAsync(RoleScopeType scopeType, Guid scopeId, CancellationToken ct = default) =>
            Task.FromResult(Scopes.TryGetValue(scopeId, out var s) && s.Type == scopeType);

        public Task<Dictionary<Guid, string>> GetScopeNamesAsync(IReadOnlyCollection<Guid> departmentIds, IReadOnlyCollection<Guid> partyCellIds, CancellationToken ct = default) =>
            Task.FromResult(departmentIds.Concat(partyCellIds).Distinct().Where(Scopes.ContainsKey).ToDictionary(id => id, id => Scopes[id].Name));

        public void Add(UserRoleAssignment assignment)
        {
            assignment.User = Users.FirstOrDefault(u => u.Id == assignment.UserId);
            assignment.Role = Roles.FirstOrDefault(r => r.Id == assignment.RoleId);
            Assignments.Add(assignment);
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

        // IRoleRepository
        public Task<List<AppRole>> GetAllRolesWithPermissionsAsync() => Task.FromResult(Roles.Where(r => !r.IsDeleted).ToList());
        public Task<List<Permission>> GetAllPermissionsAsync() => Task.FromResult(new List<Permission>());
        public Task<AppRole?> GetRoleByIdWithPermissionsAsync(Guid roleId) => Task.FromResult(Roles.FirstOrDefault(r => r.Id == roleId && !r.IsDeleted));
        public Task<AppRole?> GetRoleForUpdateAsync(Guid roleId, CancellationToken ct = default) => GetRoleByIdWithPermissionsAsync(roleId);
        public Task<bool> RoleNameExistsAsync(string name, Guid? excludeRoleId, CancellationToken ct = default) =>
            Task.FromResult(Roles.Any(r => !r.IsDeleted && r.Id != excludeRoleId && string.Equals(r.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)));
        public Task<List<Permission>> GetPermissionsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default) =>
            Task.FromResult(codes.Select(c => new Permission { Id = Guid.NewGuid(), Code = c }).ToList());
        public void AddRole(AppRole role) => Roles.Add(role);
        public void AddRolePermissionsAudit(Guid roleId, IReadOnlyCollection<string> previousCodes, IReadOnlyCollection<string> currentCodes) => AuditCount++;

        // IUserRepository (chỉ phần dùng tới)
        public Task<PartyMemberProfile?> GetByIdAsync(Guid id) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id && !u.IsDeleted));
        public Task<List<PartyMemberProfile>> ListAsync() => Task.FromResult(Users.ToList());
        public Task<List<PartyMemberProfile>> FindAsync(Expression<Func<PartyMemberProfile, bool>> predicate) => Task.FromResult(Users.Where(predicate.Compile()).ToList());
        public Task AddAsync(PartyMemberProfile entity) { Users.Add(entity); return Task.CompletedTask; }
        public Task UpdateAsync(PartyMemberProfile entity) => Task.CompletedTask;
        public Task DeleteAsync(PartyMemberProfile entity) { entity.IsDeleted = true; return Task.CompletedTask; }
        public Task<PartyMemberProfile?> GetByUsernameAsync(string username) => Task.FromResult(Users.FirstOrDefault(u => u.Username == username));
        public Task<PartyMemberProfile?> GetFirstMemberAsync() => Task.FromResult(Users.FirstOrDefault());
        public Task<List<PartyMemberProfile>> GetAllWithDetailsAsync() => Task.FromResult(Users.ToList());
        public Task<PartyMemberProfile?> GetWithRolesAndPermissionsAsync(string username) => GetByUsernameAsync(username);
        public Task<PartyMemberProfile?> GetWithRolesAndPermissionsByIdAsync(Guid id) => GetByIdAsync(id);
    }

    private sealed class FixedCurrentUser : ICurrentUserService
    {
        public FixedCurrentUser(Guid? userId) => UserId = userId;
        public Guid? UserId { get; }
        public string UserName => "tester";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now;
        public ManualClock(DateTime now) => _now = new DateTimeOffset(now);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class RecordingCache : IAccessCacheInvalidator
    {
        public int AllCount { get; private set; }
        public List<Guid> Users { get; } = new();
        public void InvalidateUser(Guid userId) => Users.Add(userId);
        public void InvalidateAll() => AllCount++;
    }

    #endregion
}
