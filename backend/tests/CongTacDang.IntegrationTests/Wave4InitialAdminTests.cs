using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Tài khoản quản trị ban đầu (<c>Seed:InitialAdmin:*</c>) trên CSDL trống, không dữ liệu mẫu: mỗi host
/// <see cref="WebApplicationFactory{TEntryPoint}.WithWebHostBuilder"/> là một lần khởi động ứng dụng (seeder chạy lại).
/// Dùng <see cref="ApiFactory"/> riêng để bảo đảm CSDL chưa có quản trị nào.
/// </summary>
public sealed class Wave4InitialAdminTests
{
    private const string Username = "quantri.bandau";
    private const string InitialPassword = "BanDau2026x";
    private const string NewPassword = "DaDoi2026xy";

    [SkippableFact]
    public async Task EmptyDatabase_WithConfig_CreatesAdminOnce_MustChangePassword_WithAdminPermissions()
    {
        var factory = new ApiFactory();
        await factory.InitializeAsync();
        try
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);

            // Lần khởi động đầu của ApiFactory (không cấu hình) không tạo tài khoản nào.
            await factory.WithDbAsync(async db => Assert.Equal(0, await db.PartyMemberProfiles.CountAsync()));

            // Khởi động với cấu hình → tạo tài khoản quản trị ban đầu.
            await using (var host = WithInitialAdmin(factory, Username, InitialPassword))
            {
                using var client = Client(host);
                var login = await client.PostAsJsonAsync("/api/auth/login", new { username = Username, password = InitialPassword });
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                var session = await DataAsync(login);
                Assert.True(session.GetProperty("mustChangePassword").GetBoolean());
                var grants = session.GetProperty("grants").EnumerateArray()
                    .Select(g => (g.GetProperty("code").GetString(), g.GetProperty("scopeType").GetString())).ToList();
                Assert.Contains((PermissionCodes.SystemRolesManage, "Global"), grants);
                Assert.Contains((PermissionCodes.SystemAssignmentsManage, "Global"), grants);

                // Bị buộc đổi mật khẩu trước khi dùng chức năng quản trị.
                var blocked = await client.GetAsync("/api/admin/roles");
                Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
                Assert.Contains("PASSWORD_CHANGE_REQUIRED", await blocked.Content.ReadAsStringAsync());
                Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/change-password",
                    new { currentPassword = InitialPassword, newPassword = NewPassword })).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/roles")).StatusCode);
            }

            // Khởi động lại với cùng cấu hình: không tạo thêm, không đặt lại mật khẩu.
            await using (var host = WithInitialAdmin(factory, Username, InitialPassword))
            {
                using var client = Client(host);
                Assert.Equal(HttpStatusCode.OK,
                    (await client.PostAsJsonAsync("/api/auth/login", new { username = Username, password = NewPassword })).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await client.PostAsJsonAsync("/api/auth/login", new { username = Username, password = InitialPassword })).StatusCode);
            }

            // Đã có quản trị: cấu hình tài khoản khác cũng không được tạo.
            await using (var host = WithInitialAdmin(factory, "quantri.khac", InitialPassword))
                _ = host.Server;

            await factory.WithDbAsync(async db =>
            {
                Assert.Equal(1, await db.PartyMemberProfiles.CountAsync(m => m.Username == Username));
                Assert.False(await db.PartyMemberProfiles.IgnoreQueryFilters().AnyAsync(m => m.Username == "quantri.khac"));
            });
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task EmptyDatabase_WithPasswordViolatingPolicy_DoesNotCreateAdmin()
    {
        var factory = new ApiFactory();
        await factory.InitializeAsync();
        try
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);

            await using (var host = WithInitialAdmin(factory, Username, "ngan1"))
                _ = host.Server;

            await factory.WithDbAsync(async db => Assert.Equal(0, await db.PartyMemberProfiles.CountAsync()));
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    private static WebApplicationFactory<Program> WithInitialAdmin(ApiFactory factory, string username, string password) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Seed:InitialAdmin:Username", username);
            builder.UseSetting("Seed:InitialAdmin:FullName", "Quản trị ban đầu");
            builder.UseSetting("Seed:InitialAdmin:Password", password);
        });

    private static HttpClient Client(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var bytes = RandomNumberGenerator.GetBytes(3);
        client.DefaultRequestHeaders.Add(ApiFactory.TestClientIpHeader, $"10.{bytes[0]}.{bytes[1]}.{bytes[2]}");
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").Clone();
    }
}
