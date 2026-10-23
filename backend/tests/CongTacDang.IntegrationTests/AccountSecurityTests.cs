using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>An toàn tài khoản: tài khoản đặc quyền, tự sửa thông tin của mình, đăng nhập khi đang khóa.</summary>
[Collection(AccountCollection.Name)]
public sealed class AccountSecurityTests
{
    private readonly AccountTestFixture _fx;

    public AccountSecurityTests(AccountTestFixture fixture) => _fx = fixture;

    private void SkipIfNoDatabase() => Skip.If(_fx.SkipReason != null, _fx.SkipReason);

    private async Task<Guid> CreateDepartmentAsync()
    {
        var code = $"AS-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var department = new AdministrativeDepartment { Code = code, Name = $"Phòng {code}", IsActive = true };
        await _fx.Base.WithDbAsync(async db =>
        {
            db.AdministrativeDepartments.Add(department);
            await db.SaveChangesAsync();
        });
        return department.Id;
    }

    [SkippableFact]
    public async Task DepartmentScopedManager_CannotManagePrivilegedAccounts_InOwnDepartment()
    {
        SkipIfNoDatabase();
        var dept = await CreateDepartmentAsync();
        var manageRole = await _fx.Base.CreateRoleAsync(PermissionCodes.SystemUsersRead, PermissionCodes.SystemUsersManage);
        var deptManager = await _fx.Base.CreateUserAsync(departmentId: dept);
        await _fx.Base.AssignAsync(deptManager.Id, manageRole.Id, RoleScopeType.Department, dept);

        // Cán bộ cùng đơn vị có bản gán Toàn công ty (vd. giám đốc) và cán bộ giữ quyền system.* phạm vi đơn vị.
        var globalHolder = await _fx.Base.CreateUserAsync(departmentId: dept);
        await _fx.Base.AssignAsync(globalHolder.Id, (await _fx.Base.CreateRoleAsync(PermissionCodes.EvaluationRead)).Id, RoleScopeType.Global, null);
        var systemHolder = await _fx.Base.CreateUserAsync(departmentId: dept);
        await _fx.Base.AssignAsync(systemHolder.Id, (await _fx.Base.CreateRoleAsync(PermissionCodes.SystemAuditRead)).Id, RoleScopeType.Department, dept);
        var ordinary = await _fx.Base.CreateUserAsync(departmentId: dept);

        using var client = await _fx.LoginAsync(deptManager.Username, deptManager.Password);
        foreach (var target in new[] { globalHolder, systemHolder })
        {
            var reset = await client.PostAsync($"/api/users/{target.Id}/reset-password", null);
            Assert.Equal(HttpStatusCode.Forbidden, reset.StatusCode);
            Assert.Contains("Toàn công ty", (await AccountTestFixture.ReadJsonAsync(reset)).GetProperty("message").GetString());
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/users/{target.Id}/deactivate", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/users/{target.Id}/unlock", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/users/{target.Id}", new { fullName = "Đổi tên" })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/users/{target.Id}")).StatusCode);

            // Xem được nhưng giao diện không hiện nút quản lý.
            var detail = await AccountTestFixture.ReadJsonAsync(await client.GetAsync($"/api/users/{target.Id}"));
            Assert.False(detail.GetProperty("data").GetProperty("canManage").GetBoolean());
        }

        // Tài khoản thường trong đơn vị vẫn quản lý được.
        var ok = await client.PostAsync($"/api/users/{ordinary.Id}/reset-password", null);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var ordinaryDetail = await AccountTestFixture.ReadJsonAsync(await client.GetAsync($"/api/users/{ordinary.Id}"));
        Assert.True(ordinaryDetail.GetProperty("data").GetProperty("canManage").GetBoolean());

        // Quản trị phạm vi Toàn công ty vẫn đặt lại được mật khẩu tài khoản đặc quyền.
        var (_, globalAdmin) = await _fx.CreateAccountAdminAsync();
        using (globalAdmin)
            Assert.Equal(HttpStatusCode.OK, (await globalAdmin.PostAsync($"/api/users/{globalHolder.Id}/reset-password", null)).StatusCode);
    }

    [SkippableFact]
    public async Task Manager_CannotChangeOwnWeightFrameUnitPositionsOrAuthority_ButOtherSelfFieldsAllowed()
    {
        SkipIfNoDatabase();
        var dept = await CreateDepartmentAsync();
        var otherDept = await CreateDepartmentAsync();
        var (admin, client) = await _fx.CreateAccountAdminAsync();
        using var _ = client;
        // Tài khoản quản trị có sẵn đơn vị và khung tỷ trọng.
        await _fx.Base.WithDbAsync(async db =>
        {
            var me = await db.PartyMemberProfiles.FindAsync(admin.Id);
            me!.DepartmentId = dept;
            me.WeightFrameCode = "K1";
            await db.SaveChangesAsync();
        });

        async Task AssertSelfForbiddenAsync(HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("chính mình", (await AccountTestFixture.ReadJsonAsync(response)).GetProperty("message").GetString());
        }

        await AssertSelfForbiddenAsync(await client.PutAsJsonAsync($"/api/users/{admin.Id}", new { weightFrameCode = "K2" }));
        await AssertSelfForbiddenAsync(await client.PutAsJsonAsync($"/api/users/{admin.Id}", new { departmentId = otherDept }));
        await AssertSelfForbiddenAsync(await client.PutAsJsonAsync($"/api/users/{admin.Id}", new { partyCellId = Guid.NewGuid() }));
        await AssertSelfForbiddenAsync(await client.PostAsJsonAsync($"/api/users/{admin.Id}/positions", new { positionId = Guid.NewGuid() }));
        await AssertSelfForbiddenAsync(await client.PutAsJsonAsync($"/api/users/{admin.Id}/approval-authority",
            new { @override = "CapTren", reason = "Tự đặt" }));
        await AssertSelfForbiddenAsync(await client.PutAsJsonAsync($"/api/users/{admin.Id}/form-layout", new { layoutId = (Guid?)null }));

        // Gửi lại đúng giá trị hiện tại (biểu mẫu sửa gửi đủ trường) và sửa trường khác của chính mình vẫn được.
        var same = await client.PutAsJsonAsync($"/api/users/{admin.Id}",
            new { fullName = "Quản trị tự đổi tên", weightFrameCode = "K1", departmentId = dept, phoneNumber = "0900000000" });
        Assert.Equal(HttpStatusCode.OK, same.StatusCode);
        var data = (await AccountTestFixture.ReadJsonAsync(same)).GetProperty("data");
        Assert.Equal("Quản trị tự đổi tên", data.GetProperty("fullName").GetString());
        Assert.Equal("K1", data.GetProperty("weightFrameCode").GetString());

        // Người khác vẫn đổi được cho tài khoản này.
        var (_, other) = await _fx.CreateAccountAdminAsync();
        using (other)
            Assert.Equal(HttpStatusCode.OK, (await other.PutAsJsonAsync($"/api/users/{admin.Id}", new { weightFrameCode = "K2" })).StatusCode);
    }

    [SkippableFact]
    public async Task LockedAccount_WrongPassword_ReturnsGenericError_AndDoesNotExtendLockout()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        using var _ = admin;
        var target = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);

        string? genericMessage = null;
        for (var i = 0; i < 5; i++)
        {
            var (c, wrong) = await _fx.TryLoginAsync(target.Username, "SaiMatKhau" + i);
            c.Dispose();
            genericMessage = (await AccountTestFixture.ReadJsonAsync(wrong)).GetProperty("message").GetString();
        }

        DateTime? lockoutEnd = null;
        await _fx.Base.WithDbAsync(async db => lockoutEnd = (await db.PartyMemberProfiles.FindAsync(target.Id))!.LockoutEnd);
        Assert.NotNull(lockoutEnd);

        // Đang khóa + mật khẩu sai: cùng thông báo như sai mật khẩu/không có tài khoản, không lộ trạng thái khóa.
        var (c2, lockedWrong) = await _fx.TryLoginAsync(target.Username, "VanSaiTiep1");
        c2.Dispose();
        Assert.Equal(HttpStatusCode.Unauthorized, lockedWrong.StatusCode);
        var message = (await AccountTestFixture.ReadJsonAsync(lockedWrong)).GetProperty("message").GetString();
        Assert.Equal(genericMessage, message);
        Assert.DoesNotContain("khóa", message);

        var (c3, unknown) = await _fx.TryLoginAsync("khongtontai." + Guid.NewGuid().ToString("N")[..8], "BatKy12345");
        c3.Dispose();
        Assert.Equal(genericMessage, (await AccountTestFixture.ReadJsonAsync(unknown)).GetProperty("message").GetString());

        // Không tăng bộ đếm, không kéo dài thời gian khóa.
        await _fx.Base.WithDbAsync(async db =>
        {
            var member = await db.PartyMemberProfiles.FindAsync(target.Id);
            Assert.Equal(5, member!.FailedLoginCount);
            Assert.Equal(lockoutEnd, member.LockoutEnd);
        });

        // Mật khẩu đúng: biết tài khoản đang bị khóa tạm thời.
        var (c4, lockedRight) = await _fx.TryLoginAsync(target.Username, target.Password);
        c4.Dispose();
        Assert.Contains("khóa tạm thời", (await AccountTestFixture.ReadJsonAsync(lockedRight)).GetProperty("message").GetString());
    }
}
