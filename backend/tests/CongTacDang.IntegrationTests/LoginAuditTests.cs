using System.Net;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Luồng L8 của task 08: nhật ký đăng nhập ghi đủ mọi kết quả và tra cứu được qua <c>GET /api/audit/logins</c>.
/// </summary>
[Collection(AccountCollection.Name)]
public sealed class LoginAuditTests
{
    private readonly AccountTestFixture _fx;

    public LoginAuditTests(AccountTestFixture fixture) => _fx = fixture;

    private void SkipIfNoDatabase() => Skip.If(_fx.SkipReason != null, _fx.SkipReason);

    [SkippableFact]
    public async Task L8_EveryLoginOutcomeIsRecorded_AndQueryable()
    {
        SkipIfNoDatabase();
        var (_, admin) = await _fx.CreateAccountAdminAsync();
        var user = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        var disabled = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        var locked = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationSelf);
        var unknown = "khong.ton.tai." + Guid.NewGuid().ToString("N")[..8];

        // Thành công (tên đăng nhập viết hoa vẫn khớp) + sai mật khẩu.
        (await _fx.LoginAsync(user.Username.ToUpperInvariant(), user.Password)).Dispose();
        await AttemptAsync(user.Username, "SaiMatKhau1");
        // Không tồn tại.
        await AttemptAsync(unknown, "BatKy12345");
        // Tài khoản bị khóa bởi quản trị (mật khẩu đúng).
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/users/{disabled.Id}/deactivate", null)).StatusCode);
        await AttemptAsync(disabled.Username, disabled.Password);
        // Khóa tạm thời sau 5 lần sai.
        for (var i = 0; i < 5; i++)
            await AttemptAsync(locked.Username, "Sai" + i + "abcdefg");
        await AttemptAsync(locked.Username, locked.Password);

        var mine = await QueryAsync(admin, $"userId={user.Id}");
        Assert.Contains(mine, e => e.Result == "Success" && e.Username == user.Username);
        Assert.Contains(mine, e => e.Result == "InvalidPassword");
        Assert.All(mine, e => Assert.Equal(user.Id, e.UserId));

        var unknownEvents = await QueryAsync(admin, "result=UnknownUser&pageSize=200");
        var unknownEvent = Assert.Single(unknownEvents, e => e.Username == unknown);
        Assert.Null(unknownEvent.UserId);

        Assert.Contains(await QueryAsync(admin, $"userId={disabled.Id}"), e => e.Result == "Disabled");
        var lockedEvents = await QueryAsync(admin, $"userId={locked.Id}");
        Assert.Equal(5, lockedEvents.Count(e => e.Result == "InvalidPassword"));
        Assert.Contains(lockedEvents, e => e.Result == "LockedOut");

        // Lọc theo thời gian: khoảng tương lai → rỗng; from > to → 400.
        var future = DateTime.UtcNow.AddDays(1).ToString("o");
        Assert.Empty(await QueryAsync(admin, $"userId={user.Id}&from={Uri.EscapeDataString(future)}"));
        var invalidRange = await admin.GetAsync(
            $"/api/audit/logins?from={Uri.EscapeDataString(future)}&to={Uri.EscapeDataString(DateTime.UtcNow.ToString("o"))}");
        Assert.Equal(HttpStatusCode.BadRequest, invalidRange.StatusCode);

        // Lần đăng nhập cuối được cập nhật.
        var detail = await AccountTestFixture.ReadJsonAsync(await admin.GetAsync($"/api/users/{user.Id}"));
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("data").GetProperty("lastLoginAt").ValueKind);
    }

    [SkippableFact]
    public async Task AuditEndpoints_RequireAuditReadPermission()
    {
        SkipIfNoDatabase();
        var plain = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.SystemUsersManage);
        using var client = await _fx.LoginAsync(plain.Username, plain.Password);

        var logins = await client.GetAsync("/api/audit/logins");
        Assert.Equal(HttpStatusCode.Forbidden, logins.StatusCode);
        Assert.Contains("Xem nhật ký", (await AccountTestFixture.ReadJsonAsync(logins)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/audit")).StatusCode);

        var auditor = await _fx.Base.CreateUserWithPermissionsAsync(PermissionCodes.SystemAuditRead);
        using var auditorClient = await _fx.LoginAsync(auditor.Username, auditor.Password);
        Assert.Equal(HttpStatusCode.OK, (await auditorClient.GetAsync("/api/audit/logins")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditorClient.GetAsync("/api/admin/audit")).StatusCode);
    }

    private async Task AttemptAsync(string username, string password)
    {
        var (client, response) = await _fx.TryLoginAsync(username, password);
        client.Dispose();
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<List<(Guid? UserId, string Username, string Result)>> QueryAsync(HttpClient admin, string query)
    {
        var response = await admin.GetAsync("/api/audit/logins?" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await AccountTestFixture.ReadJsonAsync(response);
        return json.GetProperty("data").GetProperty("items").EnumerateArray()
            .Select(e => (
                e.GetProperty("userId").ValueKind == JsonValueKind.Null ? (Guid?)null : e.GetProperty("userId").GetGuid(),
                e.GetProperty("usernameAttempted").GetString()!,
                e.GetProperty("result").GetString()!))
            .ToList();
    }
}
