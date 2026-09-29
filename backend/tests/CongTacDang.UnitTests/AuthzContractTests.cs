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
/// Kiểm thử khung hợp đồng phân quyền (task 07, T-55): danh mục mã quyền, policy theo mã, cache, tạo tài khoản v0.
/// Luật theo phạm vi/chủ hồ sơ được kiểm trong <c>AuthorizationGuardTests</c>.
/// </summary>
public class AuthzContractTests
{
    private static ClaimsPrincipal IdentityOnlyPrincipal(Guid userId) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "Test"));

    private static IAuthorizationService AuthorizationFor(params (Guid UserId, string[] Codes)[] users)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddAuthorization()
            .AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>()
            .AddSingleton<IPermissionResolver>(new StaticResolver(users.ToDictionary(
                u => u.UserId,
                u => u.Codes.Select(c => new PermissionGrant(c, ScopeType.Global, null, Guid.NewGuid(), "Test")).ToList())))
            .AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>()
            .BuildServiceProvider();
        return services.CreateScope().ServiceProvider.GetRequiredService<IAuthorizationService>();
    }

    [Fact]
    public async Task NewPermissionCodePolicy_UsesResolver_AndRejectsAnonymous()
    {
        var admin = Guid.NewGuid();
        var cadre = Guid.NewGuid();
        var auth = AuthorizationFor((admin, new[] { PermissionCodes.SystemUsersManage }), (cadre, new[] { PermissionCodes.EvaluationSelf }));

        Assert.True((await auth.AuthorizeAsync(IdentityOnlyPrincipal(admin), PermissionCodes.SystemUsersManage)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(IdentityOnlyPrincipal(cadre), PermissionCodes.SystemUsersManage)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), PermissionCodes.SystemUsersManage)).Succeeded);

        // Claim tự thêm vào token (mã quyền, vai trò) bị bỏ qua: người không có quyền trong CSDL vẫn bị từ chối.
        var forged = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, cadre.ToString()),
            new Claim("perm", PermissionCodes.SystemRolesManage),
            new Claim(ClaimTypes.Role, "Quản trị hệ thống")
        }, "Test"));
        Assert.False((await auth.AuthorizeAsync(forged, PermissionCodes.SystemRolesManage)).Succeeded);
    }

    [Fact]
    public async Task AnyPermissionPolicy_SucceedsWithOneOfTheCodes()
    {
        var decideLocal = Guid.NewGuid();
        var decideExternal = Guid.NewGuid();
        var nobody = Guid.NewGuid();
        var auth = AuthorizationFor(
            (decideLocal, new[] { PermissionCodes.EvaluationDecide }),
            (decideExternal, new[] { PermissionCodes.EvaluationExternalRecord }),
            (nobody, new[] { PermissionCodes.EvaluationRead }));
        var policy = new RequireAnyPermissionAttribute(PermissionCodes.EvaluationDecide, PermissionCodes.EvaluationExternalRecord).Policy!;

        Assert.True((await auth.AuthorizeAsync(IdentityOnlyPrincipal(decideLocal), policy)).Succeeded);
        Assert.True((await auth.AuthorizeAsync(IdentityOnlyPrincipal(decideExternal), policy)).Succeeded);
        Assert.False((await auth.AuthorizeAsync(IdentityOnlyPrincipal(nobody), policy)).Succeeded);
    }

    [Fact]
    public async Task PolicyProvider_KnowsPermissionCodes_AnyPolicies_UnknownNamesAreNotPolicies()
    {
        var provider = new PermissionPolicyProvider(Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions()));

        foreach (var code in PermissionCodes.All)
        {
            var policy = await provider.GetPolicyAsync(code);
            Assert.NotNull(policy);
            Assert.Contains(policy!.Requirements, r => r is PermissionRequirement p && p.Permissions.SequenceEqual(new[] { code }));
        }

        var any = await provider.GetPolicyAsync("any:" + PermissionCodes.MeetingRead + "|" + PermissionCodes.MeetingManage);
        Assert.Contains(any!.Requirements, r => r is PermissionRequirement p && p.Permissions.Count == 2);
        Assert.Null(await provider.GetPolicyAsync("any:khong.ton.tai"));

        // Tên không có trong danh mục mã quyền không phải policy.
        Assert.Null(await provider.GetPolicyAsync("catalog.read"));
        Assert.Null(await provider.GetPolicyAsync("Policy_ManagePeriods"));
        Assert.Null(await provider.GetPolicyAsync("khong.ton.tai"));
    }

    [Fact]
    public void RequirePermissionAttribute_UsesCodeAsPolicyName()
    {
        var attribute = new RequirePermissionAttribute(PermissionCodes.CatalogManage);
        Assert.Equal(PermissionCodes.CatalogManage, attribute.Policy);
        Assert.Equal(PermissionCodes.CatalogManage, attribute.Permission);
        Assert.Throws<ArgumentException>(() => new RequirePermissionAttribute(" "));
        Assert.Throws<ArgumentException>(() => new RequireAnyPermissionAttribute());
    }

    [Fact]
    public void PermissionCodes_MatchDesignCatalog()
    {
        var expected = new[]
        {
            "system.users.read", "system.users.manage", "system.roles.manage", "system.assignments.manage",
            "system.audit.read", "system.settings.manage", "system.templates.manage",
            "catalog.manage", "period.manage", "criteria.manage", "evaluation.self",
            "evaluation.read", "evaluation.tasks.approve", "evaluation.cell.confirm", "evaluation.collective.record",
            "evaluation.appraise", "evaluation.director.review", "evaluation.unit.review", "evaluation.decide", "evaluation.external.record",
            "evaluation.publish", "evaluation.reopen", "collective.manage", "meeting.read", "meeting.manage",
            "report.export"
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
            "system.roles.manage", "system.assignments.manage", "system.audit.read",
            "system.settings.manage", "system.templates.manage", "catalog.manage", "period.manage", "criteria.manage"
        }, globalOnly);
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

    private sealed class StaticResolver : IPermissionResolver
    {
        private readonly Dictionary<Guid, List<PermissionGrant>> _grants;
        public StaticResolver(Dictionary<Guid, List<PermissionGrant>> grants) => _grants = grants;

        public Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_grants.TryGetValue(userId, out var grants)
                ? new EffectivePermissions(userId, grants)
                : EffectivePermissions.Empty(userId));
    }

    private sealed class FakeUsers : IUserRepository
    {
        private readonly List<PartyMemberProfile> _users;
        public FakeUsers(params PartyMemberProfile[] users) => _users = users.ToList();
        public IReadOnlyList<PartyMemberProfile> All => _users;

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
        public Task<PartyMemberProfile?> GetWithOrganizationByIdAsync(Guid id) => GetByIdAsync(id);
    }

    #endregion
}
