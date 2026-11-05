using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Việc của bước nội bộ giao cho người có phạm vi gần hồ sơ nhất, cấp trên vẫn làm được: Công ty → Phòng / Trung tâm → Đội; Đảng bộ bộ phận → Chi bộ.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class NearestPerformerIntegrationTests
{
    private readonly ApiFactory _factory;

    public NearestPerformerIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task StepPerformers_QueueNearestOnly_UpperLevelStillPerforms()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6];
        Guid company = Guid.Empty, sales = Guid.Empty, center = Guid.Empty, team = Guid.Empty, bureau = Guid.Empty, cell = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var cty = new AdministrativeDepartment { Code = $"N-CTY-{sfx}", Name = $"Công ty {sfx}" };
            cty.Path = OrgTree.BuildPath(null, cty.Id);
            var kh = new AdministrativeDepartment { Code = $"N-KH-{sfx}", Name = $"Phòng KH {sfx}", ParentId = cty.Id };
            kh.Path = OrgTree.BuildPath(cty.Path, kh.Id);
            var tt = new AdministrativeDepartment { Code = $"N-TT-{sfx}", Name = $"Trung tâm {sfx}", ParentId = cty.Id };
            tt.Path = OrgTree.BuildPath(cty.Path, tt.Id);
            var doi = new AdministrativeDepartment { Code = $"N-DOI-{sfx}", Name = $"Đội {sfx}", ParentId = tt.Id };
            doi.Path = OrgTree.BuildPath(tt.Path, doi.Id);
            var dbbp = new PartyCell { Code = $"N-DB-{sfx}", Name = $"Đảng bộ bộ phận {sfx}" };
            dbbp.Path = OrgTree.BuildPath(null, dbbp.Id);
            var cb = new PartyCell { Code = $"N-CB-{sfx}", Name = $"Chi bộ {sfx}", ParentId = dbbp.Id };
            cb.Path = OrgTree.BuildPath(dbbp.Path, cb.Id);
            db.AddRange(cty, kh, tt, doi, dbbp, cb);
            await db.SaveChangesAsync();
            (company, sales, center, team, bureau, cell) = (cty.Id, kh.Id, tt.Id, doi.Id, dbbp.Id, cb.Id);
        });

        var evaluatee = await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA");
        var manager = await _factory.CreateUserAsync();
        await _factory.AssignAsync(manager.Id, await _factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
        var admin = await _factory.CreateUserAsync();
        await _factory.AssignAsync(admin.Id, await _factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);

        var leaderRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationTasksApprove, PermissionCodes.EvaluationUnitReview)).Id;
        var directorRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationDirectorReview)).Id;
        var cellRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCellConfirm)).Id;
        var secretaryRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCollectiveRecord)).Id;

        // Chủ tịch: nhận xét ở Công ty; Giám đốc: lãnh đạo Công ty, nhận xét ở các đơn vị trực thuộc.
        var chairman = await _factory.CreateUserAsync(company, null, ApprovalAuthority.CapTren, $"N Chủ tịch {sfx}");
        await _factory.AssignAsync(chairman.Id, directorRole, RoleScopeType.Department, company);
        var director = await _factory.CreateUserAsync(company, null, ApprovalAuthority.CapTren, $"N Giám đốc {sfx}");
        await _factory.AssignAsync(director.Id, leaderRole, RoleScopeType.Department, company);
        await _factory.AssignAsync(director.Id, directorRole, RoleScopeType.Department, sales);
        await _factory.AssignAsync(director.Id, directorRole, RoleScopeType.Department, center);
        var deputyDirector = await _factory.CreateUserAsync(company, cell, ApprovalAuthority.CoSo, $"N Phó Giám đốc {sfx}");

        var head = await _factory.CreateUserAsync(sales, cell, ApprovalAuthority.CoSo, $"N Trưởng phòng {sfx}");
        await _factory.AssignAsync(head.Id, leaderRole, RoleScopeType.Department, sales);
        var deputy = await _factory.CreateUserAsync(sales, cell, ApprovalAuthority.CoSo, $"N Phó phòng {sfx}");
        var centerHead = await _factory.CreateUserAsync(center, cell, ApprovalAuthority.CoSo, $"N Trưởng trung tâm {sfx}");
        await _factory.AssignAsync(centerHead.Id, leaderRole, RoleScopeType.Department, center);
        var teamHead = await _factory.CreateUserAsync(team, cell, ApprovalAuthority.CoSo, $"N Đội trưởng {sfx}");
        await _factory.AssignAsync(teamHead.Id, leaderRole, RoleScopeType.Department, team);

        var bureauSec = await _factory.CreateUserAsync(fullName: $"N Đảng bộ bộ phận {sfx}");
        await _factory.AssignAsync(bureauSec.Id, cellRole, RoleScopeType.PartyCell, bureau);
        var cellSec = await _factory.CreateUserAsync(fullName: $"N Chi bộ {sfx}");
        await _factory.AssignAsync(cellSec.Id, cellRole, RoleScopeType.PartyCell, cell);
        var companySecretary = await _factory.CreateUserAsync(fullName: $"N Thư ký Công ty {sfx}");
        await _factory.AssignAsync(companySecretary.Id, secretaryRole, RoleScopeType.Department, company);
        var salesSecretary = await _factory.CreateUserAsync(fullName: $"N Thư ký Phòng {sfx}");
        await _factory.AssignAsync(salesSecretary.Id, secretaryRole, RoleScopeType.Department, sales);

        var members = new[] { deputyDirector, head, deputy, centerHead, teamHead };
        foreach (var m in members)
            await _factory.AssignAsync(m.Id, evaluatee, RoleScopeType.Global, null);

        Task<HttpClient> Login(TestUser u) => _factory.LoginAsAsync(u.Username, u.Password, distinctClientIp: true);
        using var managerC = await Login(manager);
        using var adminC = await Login(admin);
        using var chairmanC = await Login(chairman);
        using var directorC = await Login(director);
        using var headC = await Login(head);
        using var centerHeadC = await Login(centerHead);
        using var bureauC = await Login(bureauSec);
        using var cellC = await Login(cellSec);
        using var companySecC = await Login(companySecretary);
        using var salesSecC = await Login(salesSecretary);

        // Kỳ diện Đảng ủy cơ sở cho mọi người (mọi bước nội bộ).
        var created = await Data(await managerC.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(managerC),
            name = $"Kỳ gần nhất {sfx}", startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        }));
        var periodId = created.GetProperty("id").GetGuid();
        await PeriodGates.DisableAsync(managerC, periodId);
        await Data(await managerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = members.Select(m => m.Id).ToArray() }));
        var participants = (await Data(managerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray().ToList();
        Guid RecordOf(TestUser u) => participants.First(p => p.GetProperty("memberId").GetGuid() == u.Id).GetProperty("recordId").GetGuid();

        // Kiểm tra kẹt luồng: các bước theo đơn vị đều có người.
        var stepCodes = new[] { PermissionCodes.EvaluationTasksApprove, PermissionCodes.EvaluationCellConfirm, PermissionCodes.EvaluationCollectiveRecord, PermissionCodes.EvaluationDirectorReview };
        var readiness = await Data(managerC, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.DoesNotContain(readiness.GetProperty("issues").EnumerateArray(), i => members.Any(m => i.GetProperty("memberId").GetGuid() == m.Id)
            && stepCodes.Contains(i.GetProperty("permission").GetString()));

        var period = await Data(managerC, $"/api/evaluations/periods/{periodId}");
        await Data(await managerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), force = true, reason = "Kiểm thử người gần nhất" }));

        async Task SetStatus(RecordStatus status) => await _factory.WithDbAsync(async db =>
        {
            var ids = members.Select(RecordOf).ToList();
            foreach (var r in await db.EvaluationRecords.Where(r => ids.Contains(r.Id)).ToListAsync())
                r.Status = status;
            await db.SaveChangesAsync();
        });

        // Duyệt danh mục: Giám đốc duyệt cho Trưởng, Phó phòng, Trưởng trung tâm, Đội trưởng; Trưởng phòng, Trưởng trung tâm không duyệt.
        await SetStatus(RecordStatus.AwaitingTaskApproval);
        Assert.False(await CanAsync(headC, RecordOf(deputy), "ApproveTasks"));
        Assert.False(await CanAsync(centerHeadC, RecordOf(teamHead), "ApproveTasks"));
        foreach (var m in new[] { deputy, head, centerHead, teamHead })
            Assert.True(await CanAsync(directorC, RecordOf(m), "ApproveTasks"));
        Assert.True(await CanAsync(chairmanC, RecordOf(deputy), "ApproveTasks"));
        Assert.True(await CanAsync(chairmanC, RecordOf(deputyDirector), "ApproveTasks"));
        Assert.False(await CanAsync(directorC, RecordOf(deputyDirector), "ApproveTasks"));

        // Việc cần xử lý và tên người chờ chỉ có người gần nhất: Giám đốc; Chủ tịch chỉ nhận hồ sơ ở Công ty.
        var queueIds = await QueueAsync(directorC);
        foreach (var m in new[] { deputy, head, centerHead, teamHead })
            Assert.Contains(RecordOf(m), queueIds);
        var chairmanQueue = await QueueAsync(chairmanC);
        Assert.Contains(RecordOf(deputyDirector), chairmanQueue);
        Assert.DoesNotContain(RecordOf(deputy), chairmanQueue);
        Assert.DoesNotContain(RecordOf(deputy), await QueueAsync(headC));
        Assert.DoesNotContain(RecordOf(teamHead), await QueueAsync(centerHeadC));
        Assert.Equal(new[] { $"N Giám đốc {sfx}" }, await PerformerNamesAsync(managerC, RecordOf(deputy)));
        Assert.Equal(new[] { $"N Giám đốc {sfx}" }, await PerformerNamesAsync(managerC, RecordOf(teamHead)));
        Assert.Equal(new[] { $"N Chủ tịch {sfx}" }, await PerformerNamesAsync(managerC, RecordOf(deputyDirector)));

        // Giám đốc duyệt danh mục Phó phòng; lịch sử ghi tên Giám đốc.
        var record = await Data(directorC, $"/api/evaluations/records/{RecordOf(deputy)}");
        var approved = await Data(await directorC.PostAsJsonAsync($"/api/evaluations/records/{RecordOf(deputy)}/tasks/approve",
            new { version = record.GetProperty("version").GetUInt32() }));
        Assert.Equal("AwaitingSelfScore", approved.GetProperty("status").GetString());
        var history = await Data(managerC, $"/api/evaluations/records/{RecordOf(deputy)}/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("step").GetString() == "B1_APPROVE"
            && h.GetProperty("action").GetString() == "Complete" && h.GetProperty("actorName").GetString() == $"N Giám đốc {sfx}");
        Assert.DoesNotContain(RecordOf(deputy), await QueueAsync(headC));

        // Chi bộ xác nhận: chi bộ được giao, Đảng bộ bộ phận vẫn làm được.
        await SetStatus(RecordStatus.AwaitingCellConfirm);
        Assert.True(await CanAsync(cellC, RecordOf(deputy), "ConfirmByCell"));
        Assert.True(await CanAsync(bureauC, RecordOf(deputy), "ConfirmByCell"));
        Assert.Contains(RecordOf(deputy), await QueueAsync(cellC));
        Assert.DoesNotContain(RecordOf(deputy), await QueueAsync(bureauC));

        // Thư ký: thư ký của Phòng được giao, thư ký Công ty vẫn làm được; đơn vị không có thư ký riêng thì giao thư ký Công ty.
        await SetStatus(RecordStatus.AwaitingCollective);
        Assert.True(await CanAsync(salesSecC, RecordOf(deputy), "RecordCollectiveProposal"));
        Assert.True(await CanAsync(companySecC, RecordOf(deputy), "RecordCollectiveProposal"));
        Assert.True(await CanAsync(companySecC, RecordOf(teamHead), "RecordCollectiveProposal"));
        Assert.DoesNotContain(RecordOf(deputy), await QueueAsync(companySecC));
        Assert.Contains(RecordOf(teamHead), await QueueAsync(companySecC));

        // Nhận xét: việc của Trưởng/Phó phòng giao Giám đốc, Chủ tịch ở cấp trên vẫn làm được; Giám đốc không có phạm vi Công ty.
        await SetStatus(RecordStatus.AwaitingDirectorReview);
        Assert.True(await CanAsync(directorC, RecordOf(deputy), "DirectorReview"));
        Assert.True(await CanAsync(directorC, RecordOf(head), "DirectorReview"));
        Assert.True(await CanAsync(chairmanC, RecordOf(head), "DirectorReview"));
        Assert.DoesNotContain(RecordOf(head), await QueueAsync(chairmanC));
        Assert.True(await CanAsync(chairmanC, RecordOf(deputyDirector), "DirectorReview"));
        Assert.False(await CanAsync(directorC, RecordOf(deputyDirector), "DirectorReview"));

        // Người thực hiện tại Phòng: Trưởng phòng được giao, Giám đốc ở cấp trên.
        var performers = await Data(adminC, $"/api/admin/performers?departmentId={sales}");
        var approve = performers.GetProperty("jobs").EnumerateArray().Single(j => j.GetProperty("permission").GetString() == PermissionCodes.EvaluationTasksApprove);
        var people = approve.GetProperty("people").EnumerateArray().ToList();
        Assert.True(people.Single(p => p.GetProperty("userId").GetGuid() == head.Id).GetProperty("isNearest").GetBoolean());
        Assert.False(people.Single(p => p.GetProperty("userId").GetGuid() == director.Id).GetProperty("isNearest").GetBoolean());
    }

    private static async Task<bool> CanAsync(HttpClient client, Guid recordId, string action)
    {
        var data = await Data(client, $"/api/evaluations/records/{recordId}/actions");
        return data.GetProperty("actions").EnumerateArray().Any(a => a.GetProperty("action").GetString() == action);
    }

    private static async Task<List<Guid>> QueueAsync(HttpClient client) =>
        (await Data(client, "/api/evaluations/work-queue")).GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("items").EnumerateArray()).Select(i => i.GetProperty("recordId").GetGuid()).ToList();

    private static async Task<List<string>> PerformerNamesAsync(HttpClient client, Guid recordId) =>
        (await Data(client, $"/api/evaluations/records/{recordId}")).GetProperty("progress").EnumerateArray()
            .Single(p => p.GetProperty("state").GetString() == "current").GetProperty("performerNames").EnumerateArray()
            .Select(n => n.GetString()!).ToList();

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }
}
