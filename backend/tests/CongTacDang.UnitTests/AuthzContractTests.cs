using System.Linq.Expressions;
using System.Security.Claims;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Kiểm thử khung hợp đồng phân quyền (task 07, T-55): danh mục mã quyền, resolver + cache,
/// policy động giữ nguyên kết quả của policy cũ dựa trên claim, guard v0 và tạo tài khoản v0.
/// </summary>
public class AuthzContractTests
{
    #region Ma trận vai trò mặc định (bản sao DataSeeder.DefaultRoles — mã cũ)

    private static readonly string[] CanBoPerms =
    {
        AppPermissions.UsersRead, AppPermissions.BranchesRead, AppPermissions.AttachmentsRead,
        AppPermissions.AttachmentsUpload, AppPermissions.ReportsExport, AppPermissions.EvaluationsRead,
        AppPermissions.EvaluationsRegister, AppPermissions.EvaluationsSelfScore
    };

    private static readonly Dictionary<string, string[]> DefaultRolePermissions = new()
    {
        [AppRoles.CAN_BO] = CanBoPerms,
        [AppRoles.BI_THU_CHI_BO] = CanBoPerms.Concat(new[] { AppPermissions.BranchesUpdate, AppPermissions.EvaluationsBranchVote }).ToArray(),
        [AppRoles.TO_THAM_DINH] = CanBoPerms.Concat(new[] { AppPermissions.AttachmentsDelete, AppPermissions.EvaluationsAppraise }).ToArray(),
        [AppRoles.BAN_THUONG_VU] = CanBoPerms.Concat(new[]
        {
            AppPermissions.BranchesCreate, AppPermissions.BranchesUpdate, AppPermissions.BranchesDelete,
            AppPermissions.AttachmentsDelete, AppPermissions.EvaluationsBranchVote,
            AppPermissions.EvaluationsAppraise, AppPermissions.EvaluationsApprove
        }).ToArray(),
        [AppRoles.DANG_UY_CO_SO] = new[] { AppPermissions.EvaluationsRead, AppPermissions.EvaluationsApprove, AppPermissions.ReportsExport },
        [AppRoles.QUAN_TRI_HE_THONG] = new[]
        {
            AppPermissions.UsersRead, AppPermissions.UsersCreate, AppPermissions.UsersUpdate, AppPermissions.UsersDelete,
            AppPermissions.BranchesRead, AppPermissions.BranchesCreate, AppPermissions.BranchesUpdate, AppPermissions.BranchesDelete,
            AppPermissions.AttachmentsRead, AppPermissions.AttachmentsUpload, AppPermissions.AttachmentsDelete,
            AppPermissions.ReportsExport, AppPermissions.RolesManage, AppPermissions.EvaluationsRead
        }.Concat(new[]
        {
            PermissionCodes.SystemUsersRead, PermissionCodes.SystemUsersManage, PermissionCodes.SystemRolesManage,
            PermissionCodes.SystemAssignmentsManage, PermissionCodes.SystemAuditRead, PermissionCodes.SystemImport,
            PermissionCodes.CatalogManage, PermissionCodes.AttachmentGeneralManage
        }).ToArray(),
    };

    private static AppRole Role(string code, IEnumerable<string> permissions) => new()
    {
        Code = code,
        Name = "Vai trò " + code,
        Permissions = permissions.Select(p => new Permission { Code = p }).ToList()
    };

    private static PartyMemberProfile UserWithRoles(IEnumerable<string> roleCodes, Guid? cellId = null) => new()
    {
        Id = Guid.NewGuid(),
        Username = "u" + Guid.NewGuid().ToString("N")[..8],
        PartyCellId = cellId,
        Roles = roleCodes.Select(code => Role(code, DefaultRolePermissions[code])).ToList()
    };

    /// <summary>Mọi tổ hợp vai trò mặc định (2^6 = 64), kèm một số tổ hợp quyền rời.</summary>
    private static IEnumerable<PartyMemberProfile> AllRoleCombinations()
    {
        var roles = DefaultRolePermissions.Keys.ToArray();
        for (var mask = 0; mask < 1 << roles.Length; mask++)
            yield return UserWithRoles(roles.Where((_, i) => (mask & (1 << i)) != 0));

        // Vai trò tự tạo chỉ có một quyền (kiểm tra từng mã cũ riêng lẻ).
        foreach (var perm in AppPermissions.All)
        {
            yield return new PartyMemberProfile
            {
                Id = Guid.NewGuid(),
                Username = "custom",
                Roles = new List<AppRole> { Role("CUSTOM", new[] { perm }) }
            };
        }
    }

    #endregion

    #region Policy cũ (bản sao nguyên văn khối AddAuthorization trong Program.cs trước task 07)

    private static void AddOldPolicies(AuthorizationOptions options)
    {
        foreach (var perm in AppPermissions.All)
            options.AddPolicy(perm, p => p.RequireClaim("perm", perm));

        options.AddPolicy("RequireCaNBo", p => p.RequireRole(AppRoles.CAN_BO));
        options.AddPolicy("RequireBiThuChiBo", p =>
            p.RequireRole(AppRoles.BI_THU_CHI_BO, AppRoles.BAN_THUONG_VU, AppRoles.QUAN_TRI_HE_THONG));
        options.AddPolicy("RequireBanThuongVu", p =>
            p.RequireRole(AppRoles.BAN_THUONG_VU, AppRoles.QUAN_TRI_HE_THONG));
        options.AddPolicy("RequireQuanTriHeTong", p =>
            p.RequireRole(AppRoles.QUAN_TRI_HE_THONG));

        options.AddPolicy(AppPermissions.PolicyEvaluationsAppraiseOrApprove, p =>
            p.RequireAssertion(ctx =>
                ctx.User.HasClaim("perm", AppPermissions.EvaluationsAppraise) ||
                ctx.User.HasClaim("perm", AppPermissions.EvaluationsApprove) ||
                ctx.User.IsInRole(AppRoles.QUAN_TRI_HE_THONG)));

        options.AddPolicy(AppPermissions.PolicyEvaluationsBranchView, p =>
            p.RequireAssertion(ctx =>
                ctx.User.HasClaim("perm", AppPermissions.EvaluationsBranchVote) ||
                ctx.User.HasClaim("perm", AppPermissions.EvaluationsAppraise) ||
                ctx.User.HasClaim("perm", AppPermissions.EvaluationsApprove) ||
                ctx.User.IsInRole(AppRoles.QUAN_TRI_HE_THONG)));

        options.AddPolicy(AppPermissions.PolicyManagePeriods, p =>
            p.RequireAssertion(ctx =>
                ctx.User.HasClaim("perm", AppPermissions.EvaluationsApprove) ||
                ctx.User.IsInRole(AppRoles.QUAN_TRI_HE_THONG) ||
                ctx.User.IsInRole(AppRoles.BAN_THUONG_VU)));
    }

    /// <summary>ClaimsPrincipal như JWT cũ sinh ra (role + perm claims tính từ member.Roles).</summary>
    private static ClaimsPrincipal OldJwtPrincipal(PartyMemberProfile user)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id.ToString()) };
        claims.AddRange(user.Roles.Select(r => r.Code).Distinct().Select(r => new Claim(ClaimTypes.Role, r)));
        claims.AddRange(user.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().Select(p => new Claim("perm", p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
    }

    /// <summary>ClaimsPrincipal chỉ chứa danh tính — chứng minh policy mới không đọc role/perm trong JWT.</summary>
    private static ClaimsPrincipal IdentityOnlyPrincipal(PartyMemberProfile user) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) }, "Test"));

    private static readonly string[] OldPolicyNames = AppPermissions.All
        .Concat(new[]
        {
            "RequireCaNBo", "RequireBiThuChiBo", "RequireBanThuongVu", "RequireQuanTriHeTong",
            AppPermissions.PolicyEvaluationsAppraiseOrApprove, AppPermissions.PolicyEvaluationsBranchView,
            AppPermissions.PolicyManagePeriods
        })
        .ToArray();

    #endregion

    [Fact]
    public async Task NewPolicies_MatchOldClaimPolicies_ForEveryRoleCombination()
    {
        var users = new FakeUsers();
        var cache = new PermissionCache();

        var oldServices = new ServiceCollection().AddLogging().AddAuthorization(AddOldPolicies).BuildServiceProvider();
        var newServices = new ServiceCollection()
            .AddLogging()
            .AddAuthorization()
            .AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
            .AddSingleton<IUserRepository>(users)
            .AddSingleton(cache)
            .AddScoped<IPermissionResolver, PermissionResolver>()
            .AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>()
            .BuildServiceProvider();

        var oldAuth = oldServices.GetRequiredService<IAuthorizationService>();
        var checkedCases = 0;
        foreach (var user in AllRoleCombinations())
        {
            users.Add(user);
            using var scope = newServices.CreateScope();
            var newAuth = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
            foreach (var policy in OldPolicyNames)
            {
                var expected = (await oldAuth.AuthorizeAsync(OldJwtPrincipal(user), policy)).Succeeded;
                var actual = (await newAuth.AuthorizeAsync(IdentityOnlyPrincipal(user), policy)).Succeeded;
                Assert.True(expected == actual,
                    $"Policy {policy} lệch với vai trò [{string.Join(",", user.Roles.Select(r => r.Code))}]: cũ={expected}, mới={actual}");
                checkedCases++;
            }
        }

        Assert.True(checkedCases > 1000);
    }

    [Fact]
    public async Task NewPermissionCodePolicy_UsesResolver_AndRejectsAnonymous()
    {
        var admin = UserWithRoles(new[] { AppRoles.QUAN_TRI_HE_THONG });
        var canBo = UserWithRoles(new[] { AppRoles.CAN_BO });
        var users = new FakeUsers(admin, canBo);
        var services = new ServiceCollection()
            .AddLogging()
            .AddAuthorization()
            .AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
            .AddSingleton<IUserRepository>(users)
            .AddSingleton(new PermissionCache())
            .AddScoped<IPermissionResolver, PermissionResolver>()
            .AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>()
            .BuildServiceProvider();
        var auth = services.CreateScope().ServiceProvider.GetRequiredService<IAuthorizationService>();

        Assert.True((await auth.AuthorizeAsync(IdentityOnlyPrincipal(admin), PermissionCodes.SystemUsersManage)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(IdentityOnlyPrincipal(canBo), PermissionCodes.SystemUsersManage)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), PermissionCodes.SystemUsersManage)).Succeeded);

        // Claim perm trong token bị bỏ qua: người không có quyền trong CSDL vẫn bị từ chối.
        var forged = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, canBo.Id.ToString()),
            new Claim("perm", AppPermissions.RolesManage),
            new Claim(ClaimTypes.Role, AppRoles.QUAN_TRI_HE_THONG)
        }, "Test"));
        Assert.False((await auth.AuthorizeAsync(forged, AppPermissions.RolesManage)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(forged, AppPermissions.PolicyManagePeriods)).Succeeded);
    }

    [Fact]
    public async Task PolicyProvider_KnowsOldAndNewCodes_FallsBackForUnknownNames()
    {
        var provider = new PermissionPolicyProvider(Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()));

        foreach (var code in PermissionCodes.All.Concat(AppPermissions.All))
        {
            var policy = await provider.GetPolicyAsync(code);
            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements, r => r is PermissionRequirement p && p.Permission == code);
        }

        Assert.NotNull(await provider.GetPolicyAsync(AppPermissions.PolicyManagePeriods));
        Assert.Null(await provider.GetPolicyAsync("khong.ton.tai"));
    }

    [Fact]
    public void RequirePermissionAttribute_UsesCodeAsPolicyName()
    {
        var attribute = new RequirePermissionAttribute(PermissionCodes.CatalogManage);
        Assert.Equal(PermissionCodes.CatalogManage, attribute.Policy);
        Assert.Equal(PermissionCodes.CatalogManage, attribute.Permission);
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute(" "));
    }

    [Fact]
    public void PermissionCodes_MatchDesignCatalog()
    {
        var expected = new[]
        {
            "system.users.read", "system.users.manage", "system.roles.manage", "system.assignments.manage",
            "system.audit.read", "system.import", "catalog.manage", "period.manage", "evaluation.self",
            "evaluation.read", "evaluation.tasks.approve", "evaluation.cell.confirm", "evaluation.collective.record",
            "evaluation.appraise", "evaluation.director.review", "evaluation.decide", "evaluation.decide.external",
            "evaluation.publish", "evaluation.reopen", "collective.manage", "meeting.read", "meeting.manage",
            "report.export", "attachment.general.manage"
        };
        Assert.Equal(expected, PermissionCodes.All);
        Assert.Equal(PermissionCodes.All.Length, PermissionCodes.All.Distinct().Count());
        Assert.All(PermissionCodes.Definitions, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.StartsWith(d.Module + ".", d.Code);
        });

        var globalOnly = PermissionCodes.Definitions.Where(d => !d.AppliesScope).Select(d => d.Code).ToHashSet();
        Assert.Equal(new HashSet<string>
        {
            "system.roles.manage", "system.assignments.manage", "system.audit.read", "system.import",
            "catalog.manage", "period.manage", "attachment.general.manage"
        }, globalOnly);

        // Mọi mã cũ đều có mặt trong bảng ánh xạ, và mọi mã đích đều thuộc danh mục mới.
        Assert.All(AppPermissions.All, old => Assert.True(LegacyPermissionMap.OldToNew.ContainsKey(old), old));
        Assert.All(LegacyPermissionMap.OldToNew.Values.SelectMany(v => v), code => Assert.True(PermissionCodes.IsDefined(code), code));
    }

    [Fact]
    public async Task Resolver_MapsRolesToGlobalGrants_AndSkipsInactiveOrDeleted()
    {
        var user = UserWithRoles(new[] { AppRoles.BI_THU_CHI_BO });
        user.Roles.Add(new AppRole
        {
            Code = "DA_XOA",
            Name = "Vai trò đã xóa",
            IsDeleted = true,
            Permissions = new List<Permission> { new() { Code = AppPermissions.RolesManage } }
        });
        var users = new FakeUsers(user);
        var resolver = new PermissionResolver(users, new PermissionCache());

        var result = await resolver.GetAsync(user.Id);

        Assert.True(result.Has(AppPermissions.EvaluationsBranchVote));
        Assert.False(result.Has(AppPermissions.RolesManage));
        Assert.All(result.Grants, g =>
        {
            Assert.Equal(ScopeType.Global, g.ScopeType);
            Assert.Null(g.ScopeId);
            Assert.Equal(Guid.Empty, g.SourceAssignmentId);
            Assert.Equal("Vai trò " + AppRoles.BI_THU_CHI_BO, g.SourceRoleName);
        });
        Assert.Equal(new[] { AppRoles.BI_THU_CHI_BO }, result.LegacyRoleCodes);

        var inactive = UserWithRoles(new[] { AppRoles.QUAN_TRI_HE_THONG });
        inactive.IsActive = false;
        users.Add(inactive);
        Assert.Empty((await resolver.GetAsync(inactive.Id)).Grants);
        Assert.Empty((await resolver.GetAsync(Guid.NewGuid())).Grants);
    }

    [Fact]
    public async Task Resolver_CachesPerUser_UntilInvalidatedOrExpired()
    {
        var clock = new ManualClock();
        var cache = new PermissionCache(clock, TimeSpan.FromMinutes(5));
        var user = UserWithRoles(new[] { AppRoles.CAN_BO });
        var users = new FakeUsers(user);
        var resolver = new PermissionResolver(users, cache);

        await resolver.GetAsync(user.Id);
        await resolver.GetAsync(user.Id);
        Assert.Equal(1, users.Loads);

        // Đổi quyền trong "CSDL": chưa xóa cache → vẫn dùng bản cũ.
        user.Roles.Add(Role(AppRoles.QUAN_TRI_HE_THONG, DefaultRolePermissions[AppRoles.QUAN_TRI_HE_THONG]));
        Assert.False((await resolver.GetAsync(user.Id)).Has(AppPermissions.RolesManage));

        cache.InvalidateUser(user.Id);
        Assert.True((await resolver.GetAsync(user.Id)).Has(AppPermissions.RolesManage));
        Assert.Equal(2, users.Loads);

        cache.InvalidateAll();
        await resolver.GetAsync(user.Id);
        Assert.Equal(3, users.Loads);

        clock.Advance(TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(1)));
        await resolver.GetAsync(user.Id);
        Assert.Equal(4, users.Loads);
    }

    [Fact]
    public void Cache_DoesNotStoreResultLoadedBeforeInvalidation()
    {
        var cache = new PermissionCache();
        var userId = Guid.NewGuid();
        var generation = cache.Generation;

        cache.InvalidateAll(); // có thay đổi phân quyền trong lúc đang nạp
        cache.Set(userId, EffectivePermissions.Empty(userId), generation);

        Assert.False(cache.TryGet(userId, out _));
    }

    [Fact]
    public async Task RoleService_InvalidatesCache_OnPermissionAndAssignmentChanges()
    {
        var invalidator = new RecordingInvalidator();
        var service = new RoleService(new NoopRoleRepository(), invalidator);
        var userId = Guid.NewGuid();

        await service.UpdateRolePermissionsAsync(Guid.NewGuid(), new[] { AppPermissions.UsersRead });
        await service.AssignRolesToUserAsync(userId, new[] { AppRoles.CAN_BO });

        Assert.Equal(1, invalidator.AllCount);
        Assert.Equal(new[] { userId }, invalidator.Users);
    }

    #region Guard v0

    private static (LegacyAuthorizationGuard Guard, PartyMemberProfile User) GuardFor(IEnumerable<string> roles, Guid? cellId = null)
    {
        var user = UserWithRoles(roles, cellId);
        var users = new FakeUsers(user);
        var guard = new LegacyAuthorizationGuard(
            new FixedCurrentUser(user.Id),
            new PermissionResolver(users, new PermissionCache()),
            users,
            new AccessPolicy());
        return (guard, user);
    }

    [Fact]
    public void Guard_HasAny_UsesLegacyMapping()
    {
        var (guard, _) = GuardFor(new[] { AppRoles.BI_THU_CHI_BO });

        Assert.True(guard.HasAny(PermissionCodes.EvaluationCellConfirm)); // evaluations.branch_vote
        Assert.True(guard.HasAny(PermissionCodes.MeetingManage));
        Assert.True(guard.HasAny(PermissionCodes.EvaluationSelf));         // evaluations.register
        Assert.False(guard.HasAny(PermissionCodes.EvaluationAppraise));
        Assert.False(guard.HasAny(PermissionCodes.SystemRolesManage));
    }

    [Fact]
    public void Guard_Can_MatchesAccessPolicy_ForRecords()
    {
        var cellA = Guid.NewGuid();
        var cellB = Guid.NewGuid();
        var (guard, user) = GuardFor(new[] { AppRoles.BI_THU_CHI_BO, AppRoles.CAN_BO }, cellA);
        var own = new AccessTarget(user.Id, null, cellA, ApprovalAuthority.CoSo);
        var sameCell = new AccessTarget(Guid.NewGuid(), null, cellA, ApprovalAuthority.CoSo);
        var otherCell = new AccessTarget(Guid.NewGuid(), null, cellB, ApprovalAuthority.CoSo);

        Assert.True(guard.Can(PermissionCodes.EvaluationRead, own));
        Assert.True(guard.Can(PermissionCodes.EvaluationRead, sameCell));
        Assert.False(guard.Can(PermissionCodes.EvaluationRead, otherCell));
        Assert.True(guard.Can(PermissionCodes.EvaluationSelf, own));
        Assert.False(guard.Can(PermissionCodes.EvaluationSelf, sameCell));
        Assert.True(guard.Can(PermissionCodes.EvaluationCellConfirm, sameCell));
        Assert.False(guard.Can(PermissionCodes.EvaluationCellConfirm, otherCell));
        Assert.False(guard.Can(PermissionCodes.EvaluationDecide, sameCell));

        var ex = Assert.Throws<ForbiddenException>(() => guard.Ensure(PermissionCodes.EvaluationRead, otherCell));
        Assert.Contains("Xem hồ sơ đánh giá", ex.Message);
    }

    [Fact]
    public void Guard_Decide_RespectsApprovalAuthority()
    {
        var (coSo, _) = GuardFor(new[] { AppRoles.DANG_UY_CO_SO });
        var (capTren, _) = GuardFor(new[] { AppRoles.BAN_THUONG_VU });
        var recordCoSo = new AccessTarget(Guid.NewGuid(), null, null, ApprovalAuthority.CoSo);
        var recordCapTren = new AccessTarget(Guid.NewGuid(), null, null, ApprovalAuthority.CapTren);

        Assert.True(coSo.Can(PermissionCodes.EvaluationDecide, recordCoSo));
        Assert.False(coSo.Can(PermissionCodes.EvaluationDecide, recordCapTren));
        Assert.False(coSo.Can(PermissionCodes.EvaluationDecideExternal, recordCapTren));
        Assert.True(capTren.Can(PermissionCodes.EvaluationDecideExternal, recordCapTren));
        Assert.False(capTren.Can(PermissionCodes.EvaluationDecide, recordCoSo));
    }

    [Fact]
    public void Guard_GetScope_GlobalWhenGranted_OwnerAlwaysForRead()
    {
        var (admin, adminUser) = GuardFor(new[] { AppRoles.QUAN_TRI_HE_THONG });
        var (canBo, canBoUser) = GuardFor(new[] { AppRoles.CAN_BO });
        var (dangUy, _) = GuardFor(new[] { AppRoles.DANG_UY_CO_SO });

        Assert.True(admin.GetScope(PermissionCodes.CatalogManage).IsGlobal);
        Assert.True(canBo.GetScope(PermissionCodes.CatalogManage).IsEmpty);
        Assert.Equal(ScopeFilter.OwnerOnly(canBoUser.Id), canBo.GetScope(PermissionCodes.EvaluationSelf));

        var read = canBo.GetScope(PermissionCodes.EvaluationRead);
        Assert.True(read.IsGlobal);
        Assert.Equal(canBoUser.Id, read.OwnerId);

        Assert.Equal(adminUser.Id, admin.GetScope(PermissionCodes.EvaluationSelf).OwnerId); // attachments.upload → evaluation.self
        Assert.True(dangUy.GetScope(PermissionCodes.EvaluationSelf).IsEmpty);
    }

    [Fact]
    public void Guard_Anonymous_HasNothing()
    {
        var guard = new LegacyAuthorizationGuard(new FixedCurrentUser(null), new PermissionResolver(new FakeUsers(), new PermissionCache()), new FakeUsers(), new AccessPolicy());

        Assert.False(guard.HasAny(PermissionCodes.EvaluationRead));
        Assert.False(guard.Can(PermissionCodes.EvaluationRead, AccessTarget.None));
        Assert.True(guard.GetScope(PermissionCodes.EvaluationRead).IsEmpty);
        Assert.Throws<ForbiddenException>(() => guard.Ensure(PermissionCodes.CatalogManage, AccessTarget.None));
    }

    #endregion

    #region Tạo tài khoản v0

    [Fact]
    public async Task UserAccountService_CreatesAccountWithTemporaryPassword()
    {
        var users = new FakeUsers();
        var service = new UserAccountService(users);
        var cell = Guid.NewGuid();

        var created = await service.CreateAsync(new CreateAccountCommand(
            " nguyenvana ", " Nguyễn Văn A ", "a@attech.com.vn", "093-001", "Trưởng phòng", null, cell, ApprovalAuthority.CapTren));

        var saved = Assert.Single(users.All);
        Assert.Equal("nguyenvana", created.Username);
        Assert.Equal(saved.Id, created.UserId);
        Assert.Equal("Nguyễn Văn A", saved.FullName);
        Assert.Equal(cell, saved.PartyCellId);
        Assert.Equal(ApprovalAuthority.CapTren, saved.ApprovalAuthority);
        Assert.True(saved.MustChangePassword);
        Assert.True(saved.IsPartyMember);
        Assert.False(string.IsNullOrWhiteSpace(saved.SecurityStamp));
        Assert.Equal(12, created.TemporaryPassword.Length);
        Assert.Contains(created.TemporaryPassword, char.IsLetter);
        Assert.Contains(created.TemporaryPassword, char.IsDigit);
        Assert.NotEqual(created.TemporaryPassword, saved.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(created.TemporaryPassword, saved.PasswordHash));
    }

    [Theory]
    [InlineData("", "Tên")]
    [InlineData("   ", "Tên")]
    [InlineData("co khoang", "Tên")]
    [InlineData("hople", "")]
    public async Task UserAccountService_RejectsInvalidInput(string username, string fullName)
    {
        var service = new UserAccountService(new FakeUsers());
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new CreateAccountCommand(username, fullName, null, null, null, null, null, ApprovalAuthority.CoSo)));
    }

    [Fact]
    public async Task UserAccountService_RejectsDuplicateUsername_CaseInsensitive()
    {
        var existing = new PartyMemberProfile { Username = "NguyenVanA", FullName = "A" };
        var service = new UserAccountService(new FakeUsers(existing));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(
            new CreateAccountCommand("nguyenvana", "Nguyễn Văn A", null, null, null, null, null, ApprovalAuthority.CoSo)));
        Assert.Contains("đã được dùng", ex.Message);
    }

    [Fact]
    public void TemporaryPasswords_AreRandom()
    {
        var passwords = Enumerable.Range(0, 200).Select(_ => UserAccountService.GenerateTemporaryPassword()).ToList();
        Assert.Equal(passwords.Count, passwords.Distinct().Count());
        Assert.All(passwords, p => Assert.True(p.Any(char.IsLetter) && p.Any(char.IsDigit)));
    }

    #endregion

    #region Fakes

    private sealed class FakeUsers : IUserRepository
    {
        private readonly List<PartyMemberProfile> _users;
        public FakeUsers(params PartyMemberProfile[] users) => _users = users.ToList();
        public int Loads { get; private set; }
        public IReadOnlyList<PartyMemberProfile> All => _users;
        public void Add(PartyMemberProfile user) => _users.Add(user);

        public Task AddAsync(PartyMemberProfile entity) { _users.Add(entity); return Task.CompletedTask; }
        public Task DeleteAsync(PartyMemberProfile entity) { _users.Remove(entity); return Task.CompletedTask; }
        public Task<List<PartyMemberProfile>> FindAsync(Expression<Func<PartyMemberProfile, bool>> predicate) =>
            Task.FromResult(_users.Where(predicate.Compile()).ToList());
        public Task<PartyMemberProfile?> GetByIdAsync(Guid id) => Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
        public Task<List<PartyMemberProfile>> ListAsync() => Task.FromResult(_users.ToList());
        public Task UpdateAsync(PartyMemberProfile entity) => Task.CompletedTask;
        public Task<PartyMemberProfile?> GetByUsernameAsync(string username) => Task.FromResult(_users.FirstOrDefault(u => u.Username == username));
        public Task<PartyMemberProfile?> GetFirstMemberAsync() => Task.FromResult(_users.FirstOrDefault());
        public Task<List<PartyMemberProfile>> GetAllWithDetailsAsync() => Task.FromResult(_users.ToList());
        public Task<PartyMemberProfile?> GetWithRolesAndPermissionsAsync(string username) => GetByUsernameAsync(username);
        public Task<PartyMemberProfile?> GetWithRolesAndPermissionsByIdAsync(Guid id)
        {
            Loads++;
            return GetByIdAsync(id);
        }
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
        private DateTimeOffset _now = new(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private sealed class RecordingInvalidator : IAccessCacheInvalidator
    {
        public int AllCount { get; private set; }
        public List<Guid> Users { get; } = new();
        public void InvalidateUser(Guid userId) => Users.Add(userId);
        public void InvalidateAll() => AllCount++;
    }

    private sealed class NoopRoleRepository : IRoleRepository
    {
        public Task<List<AppRole>> GetAllRolesWithPermissionsAsync() => Task.FromResult(new List<AppRole>());
        public Task<List<Permission>> GetAllPermissionsAsync() => Task.FromResult(new List<Permission>());
        public Task<AppRole?> GetRoleByIdWithPermissionsAsync(Guid roleId) => Task.FromResult<AppRole?>(null);
        public Task<AppRole?> GetRoleByCodeAsync(string roleCode) => Task.FromResult<AppRole?>(null);
        public Task UpdateRolePermissionsAsync(Guid roleId, IEnumerable<string> permissionCodes) => Task.CompletedTask;
        public Task AssignRolesToUserAsync(Guid userId, IEnumerable<string> roleCodes) => Task.CompletedTask;
    }

    #endregion
}
