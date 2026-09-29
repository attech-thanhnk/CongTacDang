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
            Assert.Equal("09B", active.GetProperty("criteria").GetProperty("selfScoreForm").GetString());

            // Kỳ mẫu theo kiểu kỳ dựng sẵn đủ hồ sơ luồng; kiểm tra kẹt luồng sạch (mọi bước còn lại đều có người thực hiện).
            var profiles = active.GetProperty("settings").GetProperty("profiles").EnumerateArray()
                .Select(p => p.GetProperty("code").GetString()).ToList();
            Assert.Equal(new[] { "co-so", "cap-tren", "bi-thu-nhan-vien" }, profiles);
            var periodId = active.GetProperty("id").GetGuid();
            var readiness = await DataAsync(await appraiser.GetAsync($"/api/evaluations/periods/{periodId}/readiness"));
            Assert.True(readiness.GetProperty("ready").GetBoolean(), readiness.ToString());
            Assert.Empty(readiness.GetProperty("issues").EnumerateArray());
            Assert.Equal(5, readiness.GetProperty("checkedRecords").GetInt32()); // 6 hồ sơ, trừ hồ sơ Giám đốc đã công bố

            // Kỳ mẫu gắn bộ "Mẫu 09B — Quý III/2026" đã chụp (tích hợp đợt 7).
            var criteria = active.GetProperty("criteria");
            Assert.Equal(CriteriaSetDefaults.Code09B, criteria.GetProperty("code").GetString());
            Assert.Equal(CriteriaSetDefaults.Name09B, criteria.GetProperty("name").GetString());

            // Thông tin đơn vị mặc định có sẵn (công khai và khi đã đăng nhập).
            var publicInfo = await DataAsync(await Client(host).GetAsync("/api/settings/organization/public"));
            Assert.Equal(DataSeeder.DefaultOrganizationSettings().SystemName, publicInfo.GetProperty("systemName").GetString());
            var orgSettings = await DataAsync(await owner.GetAsync("/api/settings/organization"));
            Assert.Equal(DataSeeder.DefaultOrganizationSettings().PartyCommitteeName, orgSettings.GetProperty("partyCommitteeName").GetString());

            await factory.WithDbAsync(async db => await AssertSampleOrganizationAsync(db, periodId));
            await factory.WithDbAsync(async db => await AssertSampleCriteriaScoresAsync(db, periodId));
            await AssertWave8SampleAsync(host, factory, periodId);
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

            await factory.WithDbAsync(async db => Assert.Equal(10, await db.PartyMemberProfiles.CountAsync()));
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

    /// <summary>
    /// Dữ liệu chấm mẫu theo bộ tiêu chí của kỳ (đợt 7): kỳ có ảnh chụp bộ 09B mặc định; cán bộ mẫu có khung tỷ trọng mặc định,
    /// hồ sơ chụp lại khung (có trong bộ); hồ sơ đã tự chấm có điểm theo đủ mã tiêu chí con và mã trục của bộ.
    /// </summary>
    private static async Task AssertSampleCriteriaScoresAsync(CongTacDangDbContext db, Guid periodId)
    {
        var period = await db.EvaluationPeriods.SingleAsync(p => p.Id == periodId);
        var set = await db.Set<CriteriaSet>().SingleAsync(s => s.Code == CriteriaSetDefaults.Code09B);
        Assert.Equal(set.Id, period.CriteriaSetId);
        var snapshot = period.GetCriteria();
        Assert.NotNull(snapshot);
        Assert.Equal(CriteriaSetContent.Form09B, snapshot!.SelfScoreForm);
        var content = snapshot.Content;
        var itemCodes = content.AllItems.Select(x => x.Item.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var axisCodes = content.Axes.Select(a => a.Code).OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.Equal(17, itemCodes.Count);
        Assert.Equal(6, axisCodes.Count);

        var members = await db.PartyMemberProfiles.ToDictionaryAsync(m => m.Id);
        Assert.All(members.Values, m => Assert.NotNull(content.FindFrame(m.WeightFrameCode)));

        var records = await db.EvaluationRecords.Where(r => r.PeriodId == periodId).ToListAsync();
        Assert.All(records, r => Assert.Equal(members[r.MemberId].WeightFrameCode, r.WeightFrameCode));
        var scored = records.Where(r => r.SelfScoredAt != null).ToList();
        Assert.True(scored.Count >= 4, $"Số hồ sơ đã tự chấm: {scored.Count}");
        foreach (var record in scored)
        {
            Assert.Equal(CriteriaSetContent.Form09B, record.SelfScoreForm);
            using var general = JsonDocument.Parse(record.GeneralScores);
            Assert.Equal(itemCodes, general.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(c => c, StringComparer.Ordinal).ToList());
            using var axes = JsonDocument.Parse(record.AxisScores!);
            Assert.Equal(axisCodes, axes.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(c => c, StringComparer.Ordinal).ToList());
            Assert.Equal(record.GeneralCriteriaScore + record.TasksScore, record.TotalSelfScore, 6);
        }
    }

    /// <summary>
    /// Dữ liệu mẫu đủ thử chức năng đợt 8: hồ sơ cá nhân có 09C/9D (xuất được), hồ sơ tập thể Mẫu 07/08 có mục, biên bản Mẫu 12
    /// có mục 3.2/chức vụ, biên bản kiểm phiếu Mẫu 13 (Tổ kiểm phiếu, số phiếu, mục I/II), hồ sơ công bố mức C có kế hoạch Mẫu 17
    /// đang lập, một kiến nghị chờ xử lý, bản nháp Mẫu 16.
    /// </summary>
    private static async Task AssertWave8SampleAsync(WebApplicationFactory<Program> host, ApiFactory factory, Guid periodId)
    {
        // Hồ sơ đã tự chấm có Mẫu 09C (mục I), 9D (một dòng mỗi trục), phần tự luận theo trục 09B; xuất được 09C/9D.
        using var officer = await LoginAndChangePasswordAsync(host, "vanphong");
        var records = await DataAsync(await officer.GetAsync($"/api/evaluations/records?periodId={periodId}"));
        var hung = records.EnumerateArray().Single(r => r.GetProperty("fullName").GetString() == "Nguyễn Văn Hùng");
        var hungId = hung.GetProperty("id").GetGuid();
        var hungRecord = await DataAsync(await officer.GetAsync($"/api/evaluations/records/{hungId}"));
        Assert.Contains("Mức 1", hungRecord.GetProperty("selfAssessment").GetProperty("I").GetString());
        Assert.Equal(6, hungRecord.GetProperty("taskResults").GetArrayLength());
        Assert.Equal(6, hungRecord.GetProperty("axisNotes").EnumerateObject().Count());
        foreach (var code in new[] { "09B", "09C", "9D" })
            Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/reports/docx/record/{hungId}/{code}")).StatusCode);

        // Hồ sơ tập thể Mẫu 07 (đủ mục I.1–I.4) và Mẫu 08 (có dòng nhiệm vụ theo nhóm) xuất được Word/Excel.
        var collective = (await DataAsync(await officer.GetAsync($"/api/evaluations/collective-records?periodId={periodId}"))).EnumerateArray().ToList();
        var m07 = Assert.Single(collective, c => c.GetProperty("form").GetString() == "M07");
        Assert.Equal(4, m07.GetProperty("sections").EnumerateObject().Count());
        var m08 = Assert.Single(collective, c => c.GetProperty("form").GetString() == "M08");
        Assert.True(m08.GetProperty("items").GetArrayLength() >= 4);
        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/reports/docx/mau-07/{m07.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/reports/form-08/{m08.GetProperty("id").GetGuid()}")).StatusCode);

        // Biên bản Mẫu 12 (B3a) có mục 3.2, chức vụ chủ trì; biên bản kiểm phiếu B4 xuất Mẫu 13.
        var meetings = (await DataAsync(await officer.GetAsync($"/api/evaluations/meetings?periodId={periodId}"))).EnumerateArray().ToList();
        var m12 = Assert.Single(meetings, m => m.GetProperty("formCode").GetString() == "M12");
        Assert.Single(m12.GetProperty("details").GetProperty("attendees").EnumerateArray());
        Assert.False(string.IsNullOrEmpty(m12.GetProperty("details").GetProperty("chairTitle").GetString()));
        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/reports/docx/mau-12/{m12.GetProperty("id").GetGuid()}")).StatusCode);
        var m13 = Assert.Single(meetings, m => m.GetProperty("formCode").GetString() == "M13");
        Assert.Equal(2, m13.GetProperty("details").GetProperty("countingCommittee").GetArrayLength());
        Assert.Equal(7, m13.GetProperty("details").GetProperty("ballotsIssued").GetInt32());
        Assert.Equal(2, m13.GetProperty("voteSummaries").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/reports/docx/mau-13/{m13.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await officer.GetAsync($"/api/reports/docx/mau-13/{m12.GetProperty("id").GetGuid()}")).StatusCode);

        // Hồ sơ công bố mức C: kế hoạch 30-60-90 ngày đang lập (bắt buộc), kiến nghị chờ xử lý.
        var mai = records.EnumerateArray().Single(r => r.GetProperty("fullName").GetString() == "Nguyễn Thị Mai");
        var maiId = mai.GetProperty("id").GetGuid();
        Assert.Equal("HoanThanh", mai.GetProperty("finalGrade").GetString());
        var plan = await DataAsync(await officer.GetAsync($"/api/evaluations/records/{maiId}/improvement-plan"));
        Assert.True(plan.GetProperty("required").GetBoolean());
        Assert.Equal("Draft", plan.GetProperty("plan").GetProperty("status").GetString());
        var appeals = await DataAsync(await officer.GetAsync($"/api/evaluations/records/{maiId}/appeals"));
        Assert.True(appeals.GetProperty("underReview").GetBoolean());
        Assert.Contains(appeals.GetProperty("appeals").EnumerateArray(), a => a.GetProperty("status").GetString() == "Submitted");

        // Văn phòng Đảng ủy (xử lý kiến nghị) thấy nhóm "Kiến nghị chờ xử lý"; Giám đốc (lập kế hoạch) thấy nhóm kế hoạch cần lập/duyệt.
        var officeQueue = await DataAsync(await officer.GetAsync("/api/evaluations/work-queue"));
        Assert.Contains(officeQueue.GetProperty("groups").EnumerateArray(), g => g.GetProperty("step").GetString() == "APPEALS");
        using var director = await LoginAndChangePasswordAsync(host, "giamdoc");
        var directorQueue = await DataAsync(await director.GetAsync("/api/evaluations/work-queue"));
        Assert.Contains(directorQueue.GetProperty("groups").EnumerateArray(), g => g.GetProperty("step").GetString() == "IMPROVEMENT_PLANS");

        // Bản nháp Mẫu 16 toàn Đảng bộ đã lưu.
        var draft = await DataAsync(await officer.GetAsync($"/api/reports/mau-16/draft?periodId={periodId}"));
        Assert.Equal("Số 15-BC/ĐU", draft.GetProperty("content").GetProperty("documentNumber").GetString());
        Assert.NotEqual(JsonValueKind.Null, draft.GetProperty("version").ValueKind);

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Set<ImprovementPlan>().CountAsync());
            Assert.Equal(1, await db.Set<EvaluationAppeal>().CountAsync(a => a.Status == AppealStatus.Submitted));
        });
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
