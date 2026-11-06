using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Tổng quan: tiến độ chỉ trong phạm vi người xem, đếm quá hạn / sắp đến hạn theo bước hiện tại; việc quản trị theo quyền.</summary>
[Collection(ApiCollection.Name)]
public sealed class DashboardIntegrationTests
{
    private readonly ApiFactory _factory;

    public DashboardIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Overview_ScopeFilteredAndCountsOverdue()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6];
        Guid deptA = Guid.Empty, deptB = Guid.Empty, cellA = Guid.Empty, cellB = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var a = new AdministrativeDepartment { Code = $"D-A-{sfx}", Name = $"Phòng A {sfx}" };
            a.Path = OrgTree.BuildPath(null, a.Id);
            var b = new AdministrativeDepartment { Code = $"D-B-{sfx}", Name = $"Phòng B {sfx}" };
            b.Path = OrgTree.BuildPath(null, b.Id);
            var ca = new PartyCell { Code = $"D-CA-{sfx}", Name = $"Chi bộ A {sfx}" };
            ca.Path = OrgTree.BuildPath(null, ca.Id);
            var cb = new PartyCell { Code = $"D-CB-{sfx}", Name = $"Chi bộ B {sfx}" };
            cb.Path = OrgTree.BuildPath(null, cb.Id);
            db.AddRange(a, b, ca, cb);
            await db.SaveChangesAsync();
            (deptA, deptB, cellA, cellB) = (a.Id, b.Id, ca.Id, cb.Id);
        });

        var evaluatee = await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA");
        var manager = await _factory.CreateUserAsync();
        await _factory.AssignAsync(manager.Id, await _factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
        var readerRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead)).Id;
        var leader = await _factory.CreateUserAsync(deptA, null, ApprovalAuthority.CoSo, $"D Trưởng phòng {sfx}");
        await _factory.AssignAsync(leader.Id, readerRole, RoleScopeType.Department, deptA);

        var late = await _factory.CreateUserAsync(deptA, cellA, ApprovalAuthority.CoSo, $"D Chậm {sfx}");
        var soon = await _factory.CreateUserAsync(deptA, cellA, ApprovalAuthority.CoSo, $"D Sắp hạn {sfx}");
        var other = await _factory.CreateUserAsync(deptB, cellB, ApprovalAuthority.CoSo, $"D Phòng khác {sfx}");
        var members = new[] { late, soon, other };
        foreach (var m in members)
            await _factory.AssignAsync(m.Id, evaluatee, RoleScopeType.Global, null);

        using var managerC = await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true);
        using var leaderC = await _factory.LoginAsAsync(leader.Username, leader.Password, distinctClientIp: true);
        using var lateC = await _factory.LoginAsAsync(late.Username, late.Password, distinctClientIp: true);

        var created = await Data(await managerC.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(managerC),
            name = $"Kỳ tổng quan {sfx}", startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        }));
        var periodId = created.GetProperty("id").GetGuid();
        await PeriodGates.DisableAsync(managerC, periodId);
        await Data(await managerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = members.Select(m => m.Id).ToArray() }));
        var period = await Data(managerC, $"/api/evaluations/periods/{periodId}");
        await Data(await managerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), force = true, reason = "Kiểm thử tổng quan" }));

        // Hạn đăng ký đã qua 3 ngày, hạn tự chấm là hôm nay.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        await _factory.WithDbAsync(async db =>
        {
            var p = await db.EvaluationPeriods.SingleAsync(x => x.Id == periodId);
            var settings = p.GetSettings();
            foreach (var profile in settings.Profiles)
            {
                foreach (var setting in profile.Steps.Values)
                {
                    setting.Rule = null;
                    setting.Deadline = null;
                }
                profile.Step(WorkflowStep.B1_REGISTER).Deadline = today.AddDays(-3);
                profile.Step(WorkflowStep.B2_SELF_SCORE).Deadline = today;
                profile.Step(WorkflowStep.B5_PUBLISH).Deadline = today.AddDays(30);
            }
            p.Settings = settings.ToJson();
            var ids = members.Select(m => m.Id).ToList();
            foreach (var r in await db.EvaluationRecords.Where(r => r.PeriodId == periodId && ids.Contains(r.MemberId)).ToListAsync())
                r.Status = r.MemberId == soon.Id ? RecordStatus.AwaitingSelfScore : RecordStatus.AwaitingRegistration;
            await db.SaveChangesAsync();
        });

        // Trưởng phòng A: chỉ thấy hai hồ sơ của phòng A; một quá hạn, một đến hạn hôm nay.
        var leaderView = await Data(leaderC, $"/api/dashboard/overview?periodId={periodId}");
        var scope = leaderView.GetProperty("scope");
        Assert.Equal(2, scope.GetProperty("total").GetInt32());
        Assert.Equal(1, scope.GetProperty("overdue").GetInt32());
        Assert.Equal(1, scope.GetProperty("dueSoon").GetInt32());
        var steps = scope.GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal(new[] { "B1_REGISTER", "B2_SELF_SCORE" }, steps.Select(s => s.GetProperty("step").GetString()));
        Assert.Equal(1, steps[0].GetProperty("overdue").GetInt32());
        var lateRows = scope.GetProperty("late").EnumerateArray().ToList();
        var lateRow = Assert.Single(lateRows);
        Assert.Equal($"D Chậm {sfx}", lateRow.GetProperty("fullName").GetString());
        Assert.Equal(3, lateRow.GetProperty("daysOverdue").GetInt32());
        Assert.Equal($"D Chậm {sfx}", lateRow.GetProperty("performer").GetString());
        var group = Assert.Single(scope.GetProperty("groups").EnumerateArray());
        Assert.Equal(cellA, group.GetProperty("partyCellId").GetGuid());
        Assert.Equal(1, group.GetProperty("overdue").GetInt32());

        // Lịch kỳ: đăng ký đã qua, tự chấm đang diễn ra.
        var timeline = leaderView.GetProperty("timeline").GetProperty("steps").EnumerateArray().ToList();
        Assert.Equal("done", timeline.Single(s => s.GetProperty("step").GetString() == "B1_REGISTER").GetProperty("state").GetString());
        Assert.Equal("current", timeline.Single(s => s.GetProperty("step").GetString() == "B2_SELF_SCORE").GetProperty("state").GetString());

        // Toàn công ty thấy cả phòng B.
        var managerScope = (await Data(managerC, $"/api/dashboard/overview?periodId={periodId}")).GetProperty("scope");
        Assert.Equal(3, managerScope.GetProperty("total").GetInt32());
        Assert.Equal(2, managerScope.GetProperty("overdue").GetInt32());
        Assert.Equal(2, managerScope.GetProperty("groups").GetArrayLength());

        // Cán bộ chỉ có hồ sơ của mình: có lịch kỳ, không có tiến độ phạm vi, không xem được việc quản trị.
        var ownView = await Data(lateC, $"/api/dashboard/overview?periodId={periodId}");
        Assert.Equal(JsonValueKind.Null, ownView.GetProperty("scope").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, ownView.GetProperty("timeline").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await lateC.GetAsync("/api/dashboard/admin")).StatusCode);
    }

    [SkippableFact]
    public async Task AdminSummary_CountsLockedAccounts_FilterMatchesUsersPage()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var admin = await _factory.CreateUserAsync();
        await _factory.AssignAsync(admin.Id, await _factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);
        var locked = await _factory.CreateUserAsync();
        await _factory.WithDbAsync(async db =>
        {
            var m = await db.PartyMemberProfiles.SingleAsync(x => x.Id == locked.Id);
            m.LockoutEnd = DateTime.UtcNow.AddMinutes(30);
            await db.SaveChangesAsync();
        });

        using var adminC = await _factory.LoginAsAsync(admin.Username, admin.Password, distinctClientIp: true);
        var summary = await Data(adminC, "/api/dashboard/admin");
        var item = summary.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("code").GetString() == "locked");
        Assert.True(item.GetProperty("count").GetInt32() >= 1);
        Assert.Equal("/admin/users?flag=locked", item.GetProperty("link").GetString());
        Assert.DoesNotContain(summary.GetProperty("items").EnumerateArray(), i => i.GetProperty("count").GetInt32() == 0);
        var usage = summary.GetProperty("usage");
        Assert.True(usage.GetProperty("recentLogins").GetInt32() >= 1);

        var page = await Data(adminC, "/api/users?flag=locked&pageSize=200");
        Assert.Equal(item.GetProperty("count").GetInt32(), page.GetProperty("totalCount").GetInt32());
        Assert.Contains(page.GetProperty("items").EnumerateArray(), u => u.GetProperty("id").GetGuid() == locked.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminC.GetAsync("/api/users?flag=khong-co")).StatusCode);
    }

    [SkippableFact]
    public async Task Overview_TimelineIncludesOwnProfile_LastResultFromEarlierPeriod()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6];
        var dept = await CreateDepartmentAsync(sfx);
        var evaluatee = await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA");
        var manager = await _factory.CreateUserAsync();
        await _factory.AssignAsync(manager.Id, await _factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
        var leader = await _factory.CreateUserAsync(dept, null, ApprovalAuthority.CoSo, $"D Lãnh đạo {sfx}");
        await _factory.AssignAsync(leader.Id, (await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead)).Id, RoleScopeType.Department, dept);
        var member = await _factory.CreateUserAsync(dept, null, ApprovalAuthority.CoSo, $"D Cán bộ {sfx}");
        foreach (var u in new[] { leader, member })
            await _factory.AssignAsync(u.Id, evaluatee, RoleScopeType.Global, null);

        using var managerC = await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true);
        using var leaderC = await _factory.LoginAsAsync(leader.Username, leader.Password, distinctClientIp: true);

        // Hai năm liền nhau để kỳ trước nằm trong số kỳ gần nhất được xét.
        int earlierYear, laterYear;
        do (earlierYear, laterYear) = (TestPeriodSlots.NextYear(), TestPeriodSlots.NextYear());
        while (laterYear != earlierYear + 1);

        var earlierId = await CreatePeriodAsync(managerC, earlierYear, $"Kỳ trước {sfx}");
        await Data(await managerC.PostAsJsonAsync($"/api/evaluations/periods/{earlierId}/participants", new { memberIds = new[] { leader.Id } }));
        await _factory.WithDbAsync(async db =>
        {
            var r = await db.EvaluationRecords.SingleAsync(x => x.PeriodId == earlierId && x.MemberId == leader.Id);
            r.Status = RecordStatus.Published;
            r.FinalGrade = EvaluationGrade.HoanThanhTot;
            r.FinalScore = 85;
            await db.SaveChangesAsync();
        });

        var periodId = await CreatePeriodAsync(managerC, laterYear, $"Kỳ này {sfx}");
        await PeriodGates.DisableAsync(managerC, periodId);
        await Data(await managerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { leader.Id, member.Id } }));
        var period = await Data(managerC, $"/api/evaluations/periods/{periodId}");
        await Data(await managerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), force = true, reason = "Kiểm thử tổng quan" }));

        // Hồ sơ của tôi và hồ sơ trong phạm vi thuộc hai diện khác hạn tự chấm.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        await _factory.WithDbAsync(async db =>
        {
            var p = await db.EvaluationPeriods.SingleAsync(x => x.Id == periodId);
            var settings = p.GetSettings();
            Assert.True(settings.Profiles.Count >= 2);
            foreach (var profile in settings.Profiles)
                foreach (var setting in profile.Steps.Values)
                {
                    setting.Rule = null;
                    setting.Deadline = null;
                }
            settings.Profiles[0].Step(WorkflowStep.B2_SELF_SCORE).Deadline = today.AddDays(5);
            settings.Profiles[1].Step(WorkflowStep.B2_SELF_SCORE).Deadline = today.AddDays(9);
            p.Settings = settings.ToJson();
            foreach (var r in await db.EvaluationRecords.Where(r => r.PeriodId == periodId).ToListAsync())
            {
                r.WorkflowProfileCode = r.MemberId == leader.Id ? settings.Profiles[1].Code : settings.Profiles[0].Code;
                r.Status = RecordStatus.AwaitingSelfScore;
            }
            await db.SaveChangesAsync();
        });

        var view = await Data(leaderC, $"/api/dashboard/overview?periodId={periodId}");
        var selfScore = view.GetProperty("timeline").GetProperty("steps").EnumerateArray()
            .Single(s => s.GetProperty("step").GetString() == "B2_SELF_SCORE");
        Assert.Equal(today.AddDays(5).ToString("yyyy-MM-dd"), selfScore.GetProperty("deadlineFrom").GetString());
        Assert.Equal(today.AddDays(9).ToString("yyyy-MM-dd"), selfScore.GetProperty("deadlineTo").GetString());

        var last = view.GetProperty("lastResult");
        Assert.Equal($"Kỳ trước {sfx}", last.GetProperty("periodName").GetString());
        Assert.Equal("HoanThanhTot", last.GetProperty("grade").GetString());
        Assert.Equal(85, last.GetProperty("score").GetDouble());

        // Người chưa có kết quả kỳ trước thì không có mục này.
        using var memberC = await _factory.LoginAsAsync(member.Username, member.Password, distinctClientIp: true);
        Assert.Equal(JsonValueKind.Null, (await Data(memberC, $"/api/dashboard/overview?periodId={periodId}")).GetProperty("lastResult").ValueKind);
    }

    [SkippableFact]
    public async Task AdminSummary_PeriodManagerWithoutUserRead_GetsNoAccountItems()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var manager = await _factory.CreateUserAsync();
        await _factory.AssignAsync(manager.Id, (await _factory.CreateRoleAsync(PermissionCodes.PeriodManage)).Id, RoleScopeType.Global, null);

        using var managerC = await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true);
        var summary = await Data(managerC, "/api/dashboard/admin");
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("usage").ValueKind);
        Assert.All(summary.GetProperty("items").EnumerateArray(), i => Assert.Equal("missingPerformers", i.GetProperty("code").GetString()));
    }

    [SkippableFact]
    public async Task AccountFlags_FilterMatchingAccounts()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6];
        var dept = await CreateDepartmentAsync(sfx);
        Guid parentCell = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var parent = new PartyCell { Code = $"D-P-{sfx}", Name = $"Đảng bộ bộ phận {sfx}" };
            parent.Path = OrgTree.BuildPath(null, parent.Id);
            var child = new PartyCell { Code = $"D-C-{sfx}", Name = $"Chi bộ con {sfx}", ParentId = parent.Id };
            child.Path = OrgTree.BuildPath(parent.Path, child.Id);
            db.AddRange(parent, child);
            await db.SaveChangesAsync();
            parentCell = parent.Id;
        });

        var admin = await _factory.CreateUserAsync();
        await _factory.AssignAsync(admin.Id, await _factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);
        var fresh = await _factory.CreateUserAsync(dept, null);
        var inCellWithChildren = await _factory.CreateUserAsync(dept, parentCell);
        var expiring = await _factory.CreateUserAsync(dept, parentCell);
        var inactive = await _factory.CreateUserAsync(dept, null);
        var assignment = await _factory.AssignAsync(expiring.Id, await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
        await _factory.WithDbAsync(async db =>
        {
            var a = await db.Set<UserRoleAssignment>().SingleAsync(x => x.Id == assignment);
            a.ValidTo = DateTime.UtcNow.AddDays(10);
            foreach (var m in await db.PartyMemberProfiles.Where(x => x.Id == expiring.Id || x.Id == inactive.Id).ToListAsync())
            {
                if (m.Id == expiring.Id)
                    m.LastLoginAt = DateTime.UtcNow.AddDays(-1);
                else
                    m.IsActive = false;
            }
            await db.SaveChangesAsync();
        });

        using var adminC = await _factory.LoginAsAsync(admin.Username, admin.Password, distinctClientIp: true);
        async Task<HashSet<Guid>> Ids(string flag) =>
            (await Data(adminC, $"/api/users?flag={flag}&departmentId={dept}&pageSize=200")).GetProperty("items").EnumerateArray()
                .Select(u => u.GetProperty("id").GetGuid()).ToHashSet();

        Assert.Equal(new HashSet<Guid> { fresh.Id, inCellWithChildren.Id }, await Ids("neverLoggedIn"));
        Assert.Equal(new HashSet<Guid> { expiring.Id }, await Ids("recentLogin"));
        Assert.Equal(new HashSet<Guid> { expiring.Id }, await Ids("roleExpiring"));
        Assert.Equal(new HashSet<Guid> { fresh.Id }, await Ids("noPartyCell"));
        Assert.Equal(new HashSet<Guid> { inCellWithChildren.Id, expiring.Id }, await Ids("cellWithChildren"));
        Assert.Equal(new HashSet<Guid> { fresh.Id, inCellWithChildren.Id, expiring.Id }, await Ids("noPosition"));
    }

    private async Task<Guid> CreateDepartmentAsync(string sfx)
    {
        var id = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var d = new AdministrativeDepartment { Code = $"D-{sfx}", Name = $"Phòng {sfx}" };
            d.Path = OrgTree.BuildPath(null, d.Id);
            db.Add(d);
            await db.SaveChangesAsync();
            id = d.Id;
        });
        return id;
    }

    private static async Task<Guid> CreatePeriodAsync(HttpClient client, int year, string name)
    {
        var created = await Data(await client.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year, quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(client),
            name, startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        }));
        return created.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }
}
