using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Test tích hợp mẫu của task 07 (T-63): đăng nhập, phân quyền theo mã quyền đánh giá từ CSDL mỗi request.
/// Skip khi thiếu biến môi trường CONGTACDANG_TEST_PG.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthzContractIntegrationTests
{
    private readonly ApiFactory _factory;

    public AuthzContractIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    [SkippableFact]
    public async Task Login_WithCorrectPassword_ReturnsPermissionsFromDatabase()
    {
        SkipIfNoDatabase();
        var user = await _factory.CreateUserWithPermissionsAsync(AppPermissions.EvaluationsRead, PermissionCodes.CatalogManage);

        using var client = await _factory.LoginAsAsync(user);
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        var permissions = json.RootElement.GetProperty("data").GetProperty("permissions")
            .EnumerateArray().Select(p => p.GetString()).ToList();
        Assert.Contains(AppPermissions.EvaluationsRead, permissions);
        Assert.Contains(PermissionCodes.CatalogManage, permissions);
    }

    [SkippableFact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        SkipIfNoDatabase();
        var user = await _factory.CreateUserWithPermissionsAsync(AppPermissions.EvaluationsRead);
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username = user.Username, password = "SaiMatKhau123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task PermissionEndpoint_Returns403WithoutPermission_200WithPermission()
    {
        SkipIfNoDatabase();
        var withoutPermission = await _factory.CreateUserWithPermissionsAsync(AppPermissions.EvaluationsRead);
        var withPermission = await _factory.CreateUserWithPermissionsAsync(AppPermissions.RolesManage);

        using var denied = await _factory.LoginAsAsync(withoutPermission);
        var forbidden = await denied.GetAsync("/api/admin/roles");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using (var json = JsonDocument.Parse(await forbidden.Content.ReadAsStringAsync()))
        {
            var message = json.RootElement.GetProperty("message").GetString();
            Assert.Contains("Quản lý vai trò", message); // tên hiển thị của quyền, không nêu mã kỹ thuật
            Assert.DoesNotContain(AppPermissions.RolesManage, message);
        }

        using var allowed = await _factory.LoginAsAsync(withPermission);
        var ok = await allowed.GetAsync("/api/admin/roles");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [SkippableFact]
    public async Task PermissionChange_TakesEffectOnNextRequest_WithoutRelogin()
    {
        SkipIfNoDatabase();
        var admin = await _factory.CreateUserWithPermissionsAsync(AppPermissions.RolesManage);
        var target = await _factory.CreateUserWithPermissionsAsync(AppPermissions.EvaluationsRead);

        using var targetClient = await _factory.LoginAsAsync(target);
        Assert.Equal(HttpStatusCode.Forbidden, (await targetClient.GetAsync("/api/admin/roles")).StatusCode);

        // Quản trị gán thêm vai trò có roles.manage qua API → cache của người đó bị xóa.
        using var adminClient = await _factory.LoginAsAsync(admin);
        var assign = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.Id}/roles",
            new { roleCodes = new[] { target.RoleCode, admin.RoleCode } });
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        // Cùng access token cũ (JWT không đổi) nhưng quyền đã có hiệu lực.
        Assert.Equal(HttpStatusCode.OK, (await targetClient.GetAsync("/api/admin/roles")).StatusCode);

        // Thu hồi → mất quyền ngay.
        var revoke = await adminClient.PostAsJsonAsync(
            $"/api/admin/users/{target.Id}/roles",
            new { roleCodes = new[] { target.RoleCode } });
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await targetClient.GetAsync("/api/admin/roles")).StatusCode);
    }

    [SkippableFact]
    public async Task Anonymous_Returns401()
    {
        SkipIfNoDatabase();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/admin/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
