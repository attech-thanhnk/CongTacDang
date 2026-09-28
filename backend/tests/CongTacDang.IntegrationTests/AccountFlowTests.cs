using System.Net;
using System.Net.Http.Json;
using CongTacDang.Application.Common.Security;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Luồng tài khoản & phiên L1–L7, L9 của task 08 trên PostgreSQL thật.
/// Skip khi thiếu biến môi trường CONGTACDANG_TEST_PG.
/// </summary>
[Collection(AccountCollection.Name)]
public sealed class AccountFlowTests
{
    private const string PasswordChangeRequired = "PASSWORD_CHANGE_REQUIRED";
    private readonly AccountTestFixture _fx;

    public AccountFlowTests(AccountTestFixture fixture) => _fx = fixture;

    private void SkipIfNoDatabase() => Skip.If(_fx.SkipReason != null, _fx.SkipReason);

    [SkippableFact]
    public async Task L1_AdminCreatesAccount_UserCanLoginImmediately()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var requested = AccountTestFixture.NewUsername("L1.User");

        var (id, username, temporaryPassword) = await AccountTestFixture.CreateViaApiAsync(admin, requested);

        Assert.Equal(requested.ToLowerInvariant(), username);
        Assert.Equal(12, temporaryPassword.Length);

        // Đăng nhập được ngay (tên đăng nhập không phân biệt hoa thường), bị yêu cầu đổi mật khẩu.
        var (client, login) = await _fx.TryLoginAsync(requested.ToUpperInvariant(), temporaryPassword);
        using (client)
        {
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var data = (await AccountTestFixture.ReadJsonAsync(login)).GetProperty("data");
            Assert.Equal(id, data.GetProperty("id").GetGuid());
            Assert.True(data.GetProperty("mustChangePassword").GetBoolean());
        }

        // Mật khẩu tạm không được trả lại ở bất kỳ API đọc nào.
        var detail = await admin.GetAsync($"/api/users/{id}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.DoesNotContain(temporaryPassword, await detail.Content.ReadAsStringAsync());

        // Trùng tên (khác hoa thường) → 409; tên sai mẫu, email sai, Phòng không tồn tại → 400.
        var duplicate = await admin.PostAsJsonAsync("/api/users", new { username = requested.ToUpperInvariant(), fullName = "Trùng" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var badName = await admin.PostAsJsonAsync("/api/users", new { username = "co khoang", fullName = "Sai" });
        Assert.Equal(HttpStatusCode.BadRequest, badName.StatusCode);
        var badEmail = await admin.PostAsJsonAsync("/api/users",
            new { username = AccountTestFixture.NewUsername("l1.mail"), fullName = "Sai", email = "khong-hop-le" });
        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        var badDepartment = await admin.PostAsJsonAsync("/api/users",
            new { username = AccountTestFixture.NewUsername("l1.dept"), fullName = "Sai", departmentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, badDepartment.StatusCode);
        Assert.Contains("Phòng", (await AccountTestFixture.ReadJsonAsync(badDepartment)).GetProperty("message").GetString());

        // Thiếu tên đăng nhập → 400.
        var noUsername = await admin.PostAsJsonAsync("/api/users", new { fullName = "Không có tên đăng nhập" });
        Assert.Equal(HttpStatusCode.BadRequest, noUsername.StatusCode);
    }

    [SkippableFact]
    public async Task L2_FirstLogin_MustChangePassword_BlocksApisUntilChanged()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var (_, username, temporaryPassword) = await AccountTestFixture.CreateViaApiAsync(admin, AccountTestFixture.NewUsername("l2"));
        using var user = await _fx.LoginAsync(username, temporaryPassword);

        // Danh sách cho phép.
        var me = await user.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.True((await AccountTestFixture.ReadJsonAsync(me)).GetProperty("data").GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync("/api/auth/refresh-token", null)).StatusCode);

        // Mọi API khác → 403 PASSWORD_CHANGE_REQUIRED (kể cả API chỉ cần đăng nhập).
        foreach (var path in new[] { "/api/users/profile", "/api/users", "/api/admin/audit" })
        {
            var blocked = await user.GetAsync(path);
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            Assert.Equal(PasswordChangeRequired, (await AccountTestFixture.ReadJsonAsync(blocked)).GetProperty("code").GetString());
        }

        // Sai mật khẩu hiện tại → 400 (không phải 401), vẫn bị chặn.
        var wrong = await user.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = "Sai12345678", newPassword = "MatKhauMoi123" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        var weak = await user.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = temporaryPassword, newPassword = "ngan1" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var changed = await user.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = temporaryPassword, newPassword = "MatKhauMoi123" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // Cùng phiên (cookie được cấp lại) gọi API bình thường.
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/users/profile")).StatusCode);
        var meAfter = await AccountTestFixture.ReadJsonAsync(await user.GetAsync("/api/auth/me"));
        Assert.False(meAfter.GetProperty("data").GetProperty("mustChangePassword").GetBoolean());
    }

    [SkippableFact]
    public async Task L3_ResetPassword_RevokesSessionsImmediately_ThenFirstLoginFlow()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var target = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        using var session = await _fx.LoginAsync(target.Username, target.Password);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/users/profile")).StatusCode);

        var reset = await admin.PostAsync($"/api/users/{target.Id}/reset-password", null);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var temporaryPassword = (await AccountTestFixture.ReadJsonAsync(reset)).GetProperty("data").GetProperty("temporaryPassword").GetString()!;

        // Access token cũ bị từ chối ngay ở request kế tiếp; refresh cũng bị từ chối.
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/users/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.PostAsync("/api/auth/refresh-token", null)).StatusCode);

        // Mật khẩu cũ hết hiệu lực; mật khẩu tạm đăng nhập được và quay về L2.
        var (oldClient, old) = await _fx.TryLoginAsync(target.Username, target.Password);
        oldClient.Dispose();
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        using var fresh = await _fx.LoginAsync(target.Username, temporaryPassword);
        var blocked = await fresh.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(PasswordChangeRequired, (await AccountTestFixture.ReadJsonAsync(blocked)).GetProperty("code").GetString());
    }

    [SkippableFact]
    public async Task L4_Deactivate_RejectsNextRequestAndRefresh_ActivateAllowsLoginAgain()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var target = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        using var session = await _fx.LoginAsync(target.Username, target.Password);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/users/profile")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{target.Id}/deactivate", null)).StatusCode);

        var rejected = await session.GetAsync("/api/users/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.False((await AccountTestFixture.ReadJsonAsync(rejected)).GetProperty("success").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.PostAsync("/api/auth/refresh-token", null)).StatusCode);

        var (blockedClient, blockedLogin) = await _fx.TryLoginAsync(target.Username, target.Password);
        blockedClient.Dispose();
        Assert.Equal(HttpStatusCode.Unauthorized, blockedLogin.StatusCode);

        var list = await AccountTestFixture.ReadJsonAsync(await admin.GetAsync($"/api/users?q={target.Username}&isActive=false"));
        Assert.Equal(1, list.GetProperty("data").GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{target.Id}/activate", null)).StatusCode);
        using var again = await _fx.LoginAsync(target.Username, target.Password);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/users/profile")).StatusCode);
    }

    [SkippableFact]
    public async Task L5_Delete_RevokesSessions_BlocksLogin_UsernameNotReusable()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var target = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        using var session = await _fx.LoginAsync(target.Username, target.Password);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/users/{target.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.PostAsync("/api/auth/refresh-token", null)).StatusCode);
        var (loginClient, login) = await _fx.TryLoginAsync(target.Username, target.Password);
        loginClient.Dispose();
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/users/{target.Id}")).StatusCode);
        var reuse = await admin.PostAsJsonAsync("/api/users", new { username = target.Username.ToUpperInvariant(), fullName = "Tái sử dụng" });
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
    }

    [SkippableFact]
    public async Task L6_FiveWrongPasswords_LockOut_AdminUnlockRestoresLogin()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var target = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);

        for (var i = 0; i < 5; i++)
        {
            var (c, wrong) = await _fx.TryLoginAsync(target.Username, "SaiMatKhau" + i);
            c.Dispose();
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }

        // Đang khóa: mật khẩu đúng cũng bị từ chối.
        var (lockedClient, locked) = await _fx.TryLoginAsync(target.Username, target.Password);
        lockedClient.Dispose();
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("khóa tạm thời", (await AccountTestFixture.ReadJsonAsync(locked)).GetProperty("message").GetString());

        var detail = await AccountTestFixture.ReadJsonAsync(await admin.GetAsync($"/api/users/{target.Id}"));
        Assert.True(detail.GetProperty("data").GetProperty("isLockedOut").GetBoolean());

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{target.Id}/unlock", null)).StatusCode);
        using var session = await _fx.LoginAsync(target.Username, target.Password);
        var me = await AccountTestFixture.ReadJsonAsync(await session.GetAsync("/api/auth/me"));
        Assert.False(me.GetProperty("data").GetProperty("mustChangePassword").GetBoolean());
    }

    [SkippableFact]
    public async Task L7_ChangeOwnPassword_RevokesOtherSessions_KeepsCurrent()
    {
        SkipIfNoDatabase();
        var target = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        using var current = await _fx.LoginAsync(target.Username, target.Password);
        using var other = await _fx.LoginAsync(target.Username, target.Password);

        var changed = await current.PostAsJsonAsync("/api/auth/change-password",
            new { currentPassword = target.Password, newPassword = "DoiMatKhau2026" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        // Phiên hiện tại giữ nguyên (access token cấp lại, refresh token cũ còn dùng được).
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/users/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await current.PostAsync("/api/auth/refresh-token", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await current.GetAsync("/api/users/profile")).StatusCode);

        // Phiên khác bị thu hồi.
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/users/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.PostAsync("/api/auth/refresh-token", null)).StatusCode);

        using var withNew = await _fx.LoginAsync(target.Username, "DoiMatKhau2026");
        Assert.Equal(HttpStatusCode.OK, (await withNew.GetAsync("/api/users/profile")).StatusCode);
    }

    [SkippableFact]
    public async Task L9_CannotDisableSelf_OrLastAdministrator()
    {
        SkipIfNoDatabase();
        var (adminUser, admin) = await _fx.CreateAccountAdminAsync();

        // Không tự khóa/xóa chính mình.
        var selfLock = await admin.PostAsync($"/api/users/{adminUser.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, selfLock.StatusCode);
        Assert.Contains("chính mình", (await AccountTestFixture.ReadJsonAsync(selfLock)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/users/{adminUser.Id}")).StatusCode);

        // Collection riêng: chỉ test này tạo người giữ system.roles.manage / system.assignments.manage.
        var first = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage);
        var lastLock = await admin.PostAsync($"/api/users/{first.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Conflict, lastLock.StatusCode);
        Assert.Contains("cuối cùng", (await AccountTestFixture.ReadJsonAsync(lastLock)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/users/{first.Id}")).StatusCode);

        // Có quản trị viên thứ hai → khóa được người thứ nhất; khi đó người thứ hai thành người cuối cùng.
        var second = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.SystemRolesManage, PermissionCodes.SystemAssignmentsManage);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{first.Id}/deactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/users/{second.Id}/deactivate", null)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{first.Id}/activate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/users/{second.Id}")).StatusCode);
    }

    [SkippableFact]
    public async Task UsersList_IsPagedAndSearchable_RequiresReadPermission()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var prefix = "pg" + Guid.NewGuid().ToString("N")[..6];
        for (var i = 0; i < 3; i++)
            await AccountTestFixture.CreateViaApiAsync(admin, $"{prefix}.{i}");

        var page = await AccountTestFixture.ReadJsonAsync(await admin.GetAsync($"/api/users?q={prefix}&page=2&pageSize=2"));
        var data = page.GetProperty("data");
        Assert.Equal(3, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, data.GetProperty("page").GetInt32());
        var item = Assert.Single(data.GetProperty("items").EnumerateArray());
        Assert.StartsWith(prefix, item.GetProperty("username").GetString());
        Assert.True(item.TryGetProperty("lastLoginAt", out _));
        Assert.True(item.TryGetProperty("isLockedOut", out _));

        var clamped = await AccountTestFixture.ReadJsonAsync(await admin.GetAsync($"/api/users?q={prefix}&pageSize=1000"));
        Assert.Equal(200, clamped.GetProperty("data").GetProperty("pageSize").GetInt32());

        var plain = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        using var plainClient = await _fx.LoginAsync(plain.Username, plain.Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await plainClient.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await plainClient.PostAsJsonAsync("/api/users", new { username = "khongduoc", fullName = "x" })).StatusCode);
    }
}
