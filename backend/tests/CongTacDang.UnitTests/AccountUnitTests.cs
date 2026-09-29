using System.Text.Json;
using CongTacDang.Api.Middlewares;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Test đơn vị task 08: quy tắc tên đăng nhập/email/mật khẩu, chốt chặn quản trị, middleware đổi mật khẩu.</summary>
public sealed class AccountUnitTests
{
    #region Tên đăng nhập, email, mật khẩu

    [Theory]
    [InlineData(" NguyenVanA ", "nguyenvana")]
    [InlineData("Nguyen.Van-A_1", "nguyen.van-a_1")]
    [InlineData("abc", "abc")]
    public void Username_IsTrimmedAndLowercased(string input, string expected)
    {
        Assert.Equal(expected, AccountRules.NormalizeAndValidateUsername(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("co khoang")]
    [InlineData("nguyễn")]
    [InlineData("a@b.vn")]
    [InlineData("ten/dang")]
    public void Username_RejectsInvalidValues(string input)
    {
        var ex = Assert.Throws<ValidationException>(() => AccountRules.NormalizeAndValidateUsername(input));
        Assert.Contains("Tên đăng nhập", ex.Message);
    }

    [Fact]
    public void Username_LengthBoundaries()
    {
        Assert.Equal(new string('a', 50), AccountRules.NormalizeAndValidateUsername(new string('A', 50)));
        Assert.Throws<ValidationException>(() => AccountRules.NormalizeAndValidateUsername(new string('a', 51)));
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("  ", "")]
    [InlineData(" a.b@attech.com.vn ", "a.b@attech.com.vn")]
    public void Email_OptionalAndTrimmed(string? input, string expected)
    {
        Assert.Equal(expected, AccountRules.NormalizeOptionalEmail(input));
    }

    [Theory]
    [InlineData("khong-co-a-cong")]
    [InlineData("a@b")]
    [InlineData("a b@attech.vn")]
    [InlineData("@attech.vn")]
    [InlineData("a@@attech.vn")]
    public void Email_RejectsInvalidFormat(string input)
    {
        Assert.Throws<ValidationException>(() => AccountRules.NormalizeOptionalEmail(input));
    }

    [Fact]
    public void PasswordPolicy_UsesConfiguredMinLength_NeverBelowEight()
    {
        Assert.Equal(8, new PasswordPolicy(4).MinLength);
        var policy = new PasswordPolicy(10);
        Assert.Equal(10, policy.MinLength);

        policy.Validate("abcde12345");
        var ex = Assert.Throws<ValidationException>(() => policy.Validate("abcd1234"));
        Assert.Contains("10 ký tự", ex.Message);
        Assert.Throws<ValidationException>(() => policy.Validate("abcdefghijk"));
        Assert.Throws<ValidationException>(() => policy.Validate("12345678901"));
    }

    [Fact]
    public void AccountState_AcceptsOnlyActiveUndeletedMatchingStamp()
    {
        var id = Guid.NewGuid();
        var state = new AccountState(id, true, false, "stamp1", false);
        Assert.True(state.AcceptsSession("stamp1"));
        Assert.False(state.AcceptsSession("stamp2"));
        Assert.False(state.AcceptsSession(null));
        Assert.False((state with { IsActive = false }).AcceptsSession("stamp1"));
        Assert.False((state with { IsDeleted = true }).AcceptsSession("stamp1"));
    }

    [Fact]
    public void AccountStateCache_InvalidateDropsEntry_AndBlocksStaleWrite()
    {
        var cache = new AccountStateCache();
        var id = Guid.NewGuid();
        var generation = cache.Generation;
        cache.Set(id, new AccountState(id, true, false, "s", false), generation);
        Assert.True(cache.TryGet(id, out _));

        var before = cache.Generation;
        cache.Invalidate(id);
        Assert.False(cache.TryGet(id, out _));

        // Dữ liệu nạp trước lệnh xóa không được ghi lại vào cache.
        cache.Set(id, new AccountState(id, true, false, "old", false), before);
        Assert.False(cache.TryGet(id, out _));
    }

    #endregion

    #region Chốt chặn L9

    // Đợt 4 (tích hợp): khóa/xóa tài khoản dùng chung chốt "luôn còn quản trị" với thu hồi bản gán
    // (AdministratorInvariant trên bản gán Global đang hiệu lực của tài khoản đang hoạt động — lọc ở repository,
    // kiểm chứng bằng AccountFlowTests.L9_*).

    [Fact]
    public async Task AdminGuard_RejectsSelf()
    {
        var self = Guid.NewGuid();
        var service = new UserAccountService(
            null!, new FakeAccounts(new PartyMemberProfile { Id = self, Username = "self", IsActive = true }),
            null!, new FakeCurrentUser(self), null!, null!, null!, new AllowAllGuard());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.SetActiveAsync(self, false));
        Assert.Contains("chính mình", ex.Message);
        ex = await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(self));
        Assert.Contains("chính mình", ex.Message);
    }

    [Fact]
    public void AdminGuard_RejectsLastHolderOfAdministrationPermissions()
    {
        var lastAdmin = Guid.NewGuid();
        var before = new[] { Row(lastAdmin, PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage) };

        Assert.True(AdministratorInvariant.IsBrokenBy(before, Without(before, lastAdmin)));
    }

    [Fact]
    public void AdminGuard_RequiresBothPermissionsToRemainHeld()
    {
        var actor = Guid.NewGuid();
        var target = Guid.NewGuid();
        // Còn người khác có system.roles.manage nhưng không ai khác có system.assignments.manage → vẫn chặn.
        var before = new[]
        {
            Row(target, PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage),
            Row(actor, PermissionCodes.SystemRolesManage)
        };

        Assert.True(AdministratorInvariant.IsBrokenBy(before, Without(before, target)));
    }

    [Fact]
    public void AdminGuard_AllowsWhenAnotherActiveAdministratorRemains()
    {
        var actor = Guid.NewGuid();
        var target = Guid.NewGuid();
        var before = new[]
        {
            Row(target, PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage),
            Row(actor, PermissionCodes.SystemRolesManage),
            Row(actor, PermissionCodes.SystemAssignmentsManage)
        };

        Assert.False(AdministratorInvariant.IsBrokenBy(before, Without(before, target)));
    }

    [Fact]
    public void AdminGuard_AllowsDisablingNonAdministratorEvenWithoutAnyAdministrator()
    {
        var target = Guid.NewGuid();
        var before = Array.Empty<AdministratorGrantRow>();

        Assert.False(AdministratorInvariant.IsBrokenBy(before, Without(before, target)));
    }

    private static AdministratorGrantRow Row(Guid userId, params string[] codes) =>
        new(Guid.NewGuid(), userId, Guid.NewGuid(), codes);

    private static IEnumerable<AdministratorGrantRow> Without(IEnumerable<AdministratorGrantRow> rows, Guid userId) =>
        rows.Where(r => r.UserId != userId);

    #endregion

    #region Middleware bắt buộc đổi mật khẩu

    [Theory]
    [InlineData("GET", "/api/auth/me", true)]
    [InlineData("get", "/API/Auth/Me/", true)]
    [InlineData("POST", "/api/auth/change-password", true)]
    [InlineData("POST", "/api/auth/logout", true)]
    [InlineData("POST", "/api/auth/refresh-token", true)]
    [InlineData("POST", "/api/auth/login", true)]
    [InlineData("POST", "/api/auth/me", false)]
    [InlineData("GET", "/api/auth/change-password", false)]
    [InlineData("GET", "/api/users", false)]
    [InlineData("GET", "/api/evaluations", false)]
    [InlineData("GET", "/api/auth/me/extra", false)]
    [InlineData("GET", "", false)]
    public void PasswordGate_AllowList(string method, string path, bool allowed)
    {
        Assert.Equal(allowed, PasswordChangeRequiredMiddleware.IsAllowed(method, path));
    }

    [Fact]
    public async Task PasswordGate_Blocks_WhenMustChangePassword()
    {
        var nextCalled = false;
        var middleware = new PasswordChangeRequiredMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var context = AuthenticatedContext("GET", "/api/users", mustChange: true);

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(403, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(PasswordChangeRequiredMiddleware.Code, json.RootElement.GetProperty("code").GetString());
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData("GET", "/api/auth/me", true)]
    [InlineData("GET", "/api/users", false)]
    public async Task PasswordGate_PassesAllowListedOrNormalRequests(string method, string path, bool mustChange)
    {
        var nextCalled = false;
        var middleware = new PasswordChangeRequiredMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(AuthenticatedContext(method, path, mustChange));

        Assert.True(nextCalled);
    }

    [Fact]
    public void ApiResponse_OmitsCodeWhenNull()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        Assert.DoesNotContain("\"code\"", JsonSerializer.Serialize(ApiResponse.Fail("x"), options));
        Assert.Contains("\"code\":\"X\"", JsonSerializer.Serialize(ApiResponse.FailWithCode("X", "y"), options));
    }

    private static DefaultHttpContext AuthenticatedContext(string method, string path, bool mustChange)
    {
        var id = Guid.NewGuid();
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", id.ToString()) }, "test"));
        context.Items[PasswordChangeRequiredMiddleware.AccountStateItemKey] = new AccountState(id, true, false, "s", mustChange);
        return context;
    }

    #endregion

    #region Fakes

    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public FakeCurrentUser(Guid id) => UserId = id;
        public Guid? UserId { get; }
        public string UserName => "test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    private sealed class AllowAllGuard : IAuthorizationGuard
    {
        public bool Can(string permission, AccessTarget target) => true;
        public void Ensure(string permission, AccessTarget target) { }
        public bool HasAny(string permission) => true;
        public ScopeFilter GetScope(string permission) => ScopeFilter.Global;
    }

    private sealed class FakeAccounts : IUserAccountRepository
    {
        private readonly Dictionary<Guid, PartyMemberProfile> _members;
        public FakeAccounts(params PartyMemberProfile[] members) => _members = members.ToDictionary(m => m.Id);

        public Task<PartyMemberProfile?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_members.GetValueOrDefault(id));

        public Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PartyMemberProfile?> FindByUsernameAsync(string username, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DepartmentIsActiveAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PartyCellIsActiveAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<AccountState?> GetStateAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PagedResult<PartyMemberProfile>> SearchAsync(AccountSearchCriteria criteria, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(PartyMemberProfile member, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task SoftDeleteAsync(PartyMemberProfile member, CancellationToken ct = default) => throw new NotSupportedException();
    }

    #endregion
}
