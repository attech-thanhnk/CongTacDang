using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Data;
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

            // Kỳ mẫu theo kiểu kỳ dựng sẵn đủ hồ sơ luồng; kiểm tra kẹt luồng sạch (mọi bước còn lại đều có người thực hiện).
            var profiles = active.GetProperty("settings").GetProperty("profiles").EnumerateArray()
                .Select(p => p.GetProperty("code").GetString()).ToList();
            Assert.Equal(new[] { "co-so", "cap-tren", "bi-thu-nhan-vien" }, profiles);
            var periodId = active.GetProperty("id").GetGuid();
            var readiness = await DataAsync(await appraiser.GetAsync($"/api/evaluations/periods/{periodId}/readiness"));
            Assert.True(readiness.GetProperty("ready").GetBoolean(), readiness.ToString());
            Assert.Empty(readiness.GetProperty("issues").EnumerateArray());
            Assert.Equal(5, readiness.GetProperty("checkedRecords").GetInt32()); // 6 hồ sơ, trừ hồ sơ Giám đốc đã công bố

            await factory.WithDbAsync(async db => await AssertSampleOrganizationAsync(db, periodId));
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

    /// <summary>
    /// Mô hình tổ chức mẫu (task 14) khớp luồng mẫu (task 15): cây đơn vị hai cấp mỗi bên có loại đơn vị, mọi tài khoản có chức vụ
    /// (một ca kiêm nhiệm), thẩm quyền suy ra từ chức vụ (Giám đốc → cấp trên), hồ sơ Giám đốc đi hồ sơ luồng cấp trên với
    /// kết quả của cấp trên ở B3b/B3c/B4.
    /// </summary>
    private static async Task AssertSampleOrganizationAsync(CongTacDangDbContext db, Guid periodId)
    {
        var departments = await db.AdministrativeDepartments.Include(d => d.UnitType).ToListAsync();
        var company = Assert.Single(departments, d => d.ParentId == null);
        Assert.Equal("ATTECH", company.Code);
        Assert.Equal("Công ty", company.UnitType?.Name);
        var units = departments.Where(d => d.ParentId == company.Id).ToList();
        Assert.Contains(units, d => d.Code == "BGD");
        Assert.True(units.Count(d => d.UnitType?.Name == "Phòng") >= 2);
        Assert.All(departments, d => Assert.NotNull(d.UnitTypeId));
        Assert.All(units, d => Assert.StartsWith(company.Path, d.Path));

        var cells = await db.PartyCells.Include(c => c.UnitType).ToListAsync();
        var committee = Assert.Single(cells, c => c.ParentId == null);
        Assert.Equal("Đảng ủy", committee.UnitType?.Name);
        Assert.Equal(2, cells.Count(c => c.ParentId == committee.Id && c.UnitType?.Name == "Chi bộ"));

        var members = await db.PartyMemberProfiles.ToListAsync();
        var held = await db.MemberPositions.Include(p => p.Position).ToListAsync();
        Assert.All(members, m => Assert.Contains(held, p => p.UserId == m.Id && p.IsPrimary));
        Assert.Contains(members, m => held.Count(p => p.UserId == m.Id) >= 2);

        var director = Assert.Single(members, m => m.Username == "giamdoc");
        Assert.Equal(ApprovalAuthority.CapTren, director.ApprovalAuthority);
        Assert.Null(director.ApprovalAuthorityOverride);
        Assert.Contains(held, p => p.UserId == director.Id && p.Position!.Name == "Giám đốc" && p.DepartmentId == company.Id);
        Assert.Contains(held, p => p.UserId == director.Id && p.Position!.Name == "Bí thư Đảng ủy" && p.PartyCellId == committee.Id);
        Assert.All(members.Where(m => m.Id != director.Id), m => Assert.Equal(ApprovalAuthority.CoSo, m.ApprovalAuthority));

        var records = await db.EvaluationRecords.Where(r => r.PeriodId == periodId).ToListAsync();
        var directorRecord = Assert.Single(records, r => r.MemberId == director.Id);
        Assert.Equal("cap-tren", directorRecord.WorkflowProfileCode);
        Assert.Equal(ApprovalAuthority.CapTren, directorRecord.ApprovalAuthority);
        Assert.Equal(RecordStatus.Published, directorRecord.Status);
        var external = await db.Set<EvaluationExternalResult>().Where(x => x.RecordId == directorRecord.Id)
            .OrderBy(x => x.Step).Select(x => x.Step).ToListAsync();
        Assert.Equal(new[] { WorkflowStep.B3B_APPRAISAL, WorkflowStep.B3C_DIRECTOR, WorkflowStep.B4_DECISION }, external);
        Assert.All(records.Where(r => r.Id != directorRecord.Id), r => Assert.Equal("co-so", r.WorkflowProfileCode));
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
