using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Dữ liệu mẫu (<c>Database:SeedSampleData=true</c>) trên CSDL trống: ứng dụng khởi động, tài khoản mẫu đăng nhập được
/// (bắt buộc đổi mật khẩu) và thấy đúng <c>work-queue</c> theo vai trò/phạm vi được gán mẫu.
/// Mỗi test dùng <see cref="ApiFactory"/> riêng (CSDL tạm riêng, chưa có tài khoản nào).
/// </summary>
public sealed class SampleDataSeedTests
{
    private const string SamplePassword = "MauThu2026x";
    private const string NewPassword = "DaDoi2026xy";

    [SkippableFact]
    public async Task EmptyDatabase_SeedSampleData_LoginShowsWorkQueueByRoleAndScope()
    {
        var factory = new ApiFactory();
        await factory.InitializeAsync();
        try
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            await factory.WithDbAsync(async db => Assert.Equal(0, await db.PartyMemberProfiles.CountAsync()));

            await using var host = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:SeedSampleData", "true");
                builder.UseSetting("Seed:SamplePassword", SamplePassword);
            });

            // Chi ủy Chi bộ Khối Kỹ thuật: chỉ thấy hồ sơ đang chờ Chi bộ xác nhận trong Chi bộ của mình.
            using var secretary = await LoginAndChangePasswordAsync(host, "bithu.kt");
            var queue = await DataAsync(await secretary.GetAsync("/api/evaluations/work-queue"));
            var group = Assert.Single(queue.GetProperty("groups").EnumerateArray());
            Assert.Equal("B2_CELL_CONFIRM", group.GetProperty("step").GetString());
            var item = Assert.Single(group.GetProperty("items").EnumerateArray());
            Assert.Equal("Hoàng Văn Nam", item.GetProperty("fullName").GetString());
            Assert.False(item.GetProperty("isOwnRecord").GetBoolean());
            var actions = await DataAsync(await secretary.GetAsync($"/api/evaluations/records/{item.GetProperty("recordId").GetGuid()}/actions"));
            Assert.Contains(actions.GetProperty("actions").EnumerateArray(), a => a.GetProperty("action").GetString() == "ConfirmByCell");

            // Người được đánh giá đang ở bước tự chấm: thấy hồ sơ của chính mình.
            using var owner = await LoginAndChangePasswordAsync(host, "canbo.kt1");
            var ownQueue = await DataAsync(await owner.GetAsync("/api/evaluations/work-queue"));
            var ownGroup = Assert.Single(ownQueue.GetProperty("groups").EnumerateArray());
            Assert.Equal("B2_SELF_SCORE", ownGroup.GetProperty("step").GetString());
            Assert.True(Assert.Single(ownGroup.GetProperty("items").EnumerateArray()).GetProperty("isOwnRecord").GetBoolean());

            // Cơ quan thẩm định (Toàn công ty): hồ sơ chờ thẩm định; quản trị kỹ thuật: không có việc đánh giá nào.
            using var appraiser = await LoginAndChangePasswordAsync(host, "thamdinh");
            var appraisal = Assert.Single((await DataAsync(await appraiser.GetAsync("/api/evaluations/work-queue")))
                .GetProperty("groups").EnumerateArray());
            Assert.Equal("B3B_APPRAISAL", appraisal.GetProperty("step").GetString());
            using var admin = await LoginAndChangePasswordAsync(host, "admin");
            Assert.Equal(0, (await DataAsync(await admin.GetAsync("/api/evaluations/work-queue"))).GetProperty("total").GetInt32());

            // Kỳ mẫu đang mở, là kỳ hiện hành.
            var active = await DataAsync(await owner.GetAsync("/api/evaluations/periods/active"));
            Assert.Equal("Open", active.GetProperty("status").GetString());
            Assert.Equal("09B", active.GetProperty("settings").GetProperty("selfScoreForm").GetString());
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task EmptyDatabase_SeedSampleData_WithoutConfiguredPassword_GeneratesRandomPasswordLoggedOnce()
    {
        var factory = new ApiFactory();
        await factory.InitializeAsync();
        try
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);

            var logs = new CapturingLoggerProvider();
            await using (var host = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:SeedSampleData", "true");
                builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(logs));
            }))
            {
                _ = host.Server;
                var warning = Assert.Single(logs.Messages, m => m.Level == LogLevel.Warning && m.Text.Contains("Mật khẩu tạm chung"));
                var password = Regex.Match(warning.Text, @"lần đăng nhập đầu\): (\S+)\.").Groups[1].Value;
                Assert.NotEqual("123456", password);
                Assert.Matches("^[A-Za-z0-9]{12}$", password);

                using var client = Client(host);
                var login = await client.PostAsJsonAsync("/api/auth/login", new { username = "canbo.kt2", password });
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                Assert.True((await DataAsync(login)).GetProperty("mustChangePassword").GetBoolean());
                Assert.Equal(HttpStatusCode.Unauthorized,
                    (await client.PostAsJsonAsync("/api/auth/login", new { username = "canbo.kt2", password = "123456" })).StatusCode);
            }

            // Khởi động lại: không tạo lại dữ liệu mẫu, không ghi lại mật khẩu.
            var again = new CapturingLoggerProvider();
            await using (var host = factory.WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Database:SeedSampleData", "true");
                builder.ConfigureServices(services => services.AddSingleton<ILoggerProvider>(again));
            }))
            {
                _ = host.Server;
                Assert.DoesNotContain(again.Messages, m => m.Text.Contains("Mật khẩu tạm chung"));
            }

            await factory.WithDbAsync(async db => Assert.Equal(9, await db.PartyMemberProfiles.CountAsync()));
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }

    private static async Task<HttpClient> LoginAndChangePasswordAsync(WebApplicationFactory<Program> host, string username)
    {
        var client = Client(host);
        var login = await client.PostAsJsonAsync("/api/auth/login", new { username, password = SamplePassword });
        Assert.True(login.StatusCode == HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        Assert.True((await DataAsync(login)).GetProperty("mustChangePassword").GetBoolean());
        var blocked = await client.GetAsync("/api/evaluations/work-queue");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var changed = await client.PostAsJsonAsync("/api/auth/change-password", new { currentPassword = SamplePassword, newPassword = NewPassword });
        Assert.True(changed.StatusCode == HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());
        return client;
    }

    private static HttpClient Client(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var bytes = RandomNumberGenerator.GetBytes(3);
        client.DefaultRequestHeaders.Add(ApiFactory.TestClientIpHeader, $"10.{bytes[0]}.{bytes[1]}.{bytes[2]}");
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {body}");
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("data").Clone();
    }

    /// <summary>Ghi lại log của host để kiểm tra thông báo của seeder.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<(LogLevel Level, string Text)> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(this);

        public void Dispose()
        {
        }

        private sealed class Logger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => owner.Messages.Enqueue((logLevel, formatter(state, exception)));
        }
    }
}
