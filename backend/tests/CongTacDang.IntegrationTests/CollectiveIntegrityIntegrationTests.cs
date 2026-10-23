using System.Net;
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

/// <summary>Toàn vẹn hồ sơ tập thể và biên bản: trùng hồ sơ, kỳ đóng, phiên bản, cờ <c>canAttach</c>, số có mặt.</summary>
[Collection(ApiCollection.Name)]
public sealed class CollectiveIntegrityIntegrationTests
{
    private readonly ApiFactory _factory;

    public CollectiveIntegrityIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    private sealed record Setup(Guid PeriodId, Guid CellId, Guid RecordId, HttpClient Manager, HttpClient Reader);

    private async Task<Setup> CreateAsync()
    {
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid periodId = Guid.Empty, cellId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var cell = new PartyCell { Code = $"CI-{sfx}", Name = $"Chi bộ CI {sfx}" };
            cell.Path = OrgTree.BuildPath(null, cell.Id);
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy2, Name = $"Kỳ CI {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                CriteriaSnapshot = CriteriaTestData.Snapshot(collectiveFramework: false)
            };
            db.AddRange(cell, period);
            await db.SaveChangesAsync();
            (periodId, cellId) = (period.Id, cell.Id);
        });

        var member = await _factory.CreateUserAsync(partyCellId: cellId);
        var recordId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var record = new EvaluationRecord
            {
                PeriodId = periodId, MemberId = member.Id, PartyCellId = cellId, ApprovalAuthority = ApprovalAuthority.CoSo,
                Status = RecordStatus.AwaitingCollective, WeightFrameCode = "K2"
            };
            db.Add(record);
            await db.SaveChangesAsync();
            recordId = record.Id;
        });

        var managerRole = await _factory.CreateRoleAsync(
            PermissionCodes.CollectiveManage, PermissionCodes.EvaluationRead, PermissionCodes.MeetingManage, PermissionCodes.MeetingRead);
        var readerRole = await _factory.CreateRoleAsync(PermissionCodes.EvaluationRead);
        var manager = await _factory.CreateUserAsync();
        var reader = await _factory.CreateUserAsync();
        await _factory.AssignAsync(manager.Id, managerRole.Id, RoleScopeType.PartyCell, cellId);
        await _factory.AssignAsync(reader.Id, readerRole.Id, RoleScopeType.PartyCell, cellId);
        return new Setup(periodId, cellId, recordId,
            await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true),
            await _factory.LoginAsAsync(reader.Username, reader.Password, distinctClientIp: true));
    }

    private static Dictionary<string, object?> Collective(Setup s, string form) => new()
    {
        ["periodId"] = s.PeriodId, ["form"] = form, ["partyCellId"] = s.CellId, ["subjectName"] = "Chi ủy kiểm tra",
        ["generalCriteriaScore"] = 25, ["taskCriteriaScore"] = 60, ["selfProposedGrade"] = "HoanThanhTot"
    };

    [SkippableFact]
    public async Task CollectiveRecord_UniquePerOrg_VersionRequired_ClosedPeriodBlocked_CanAttachFollowsAttachmentRule()
    {
        SkipIfNoDatabase();
        var s = await CreateAsync();

        var created = await DataAsync(await s.Manager.PostAsJsonAsync("/api/evaluations/collective-records", Collective(s, "M07")));
        var id = created.GetProperty("id").GetGuid();
        Assert.True(created.GetProperty("canAttach").GetBoolean());

        // Tạo trùng Mẫu 07 cho cùng tổ chức (DepartmentId luôn NULL) → 409 với thông báo rõ ràng.
        var duplicate = await s.Manager.PostAsJsonAsync("/api/evaluations/collective-records", Collective(s, "M07"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("đã có hồ sơ", await MessageAsync(duplicate));

        // CSDL cũng chặn (chỉ mục duy nhất coi NULL là bằng nhau).
        await _factory.WithDbAsync(async db =>
        {
            db.Add(new CollectiveEvaluationRecord
            {
                PeriodId = s.PeriodId, Form = CollectiveEvaluationForm.M07, PartyCellId = s.CellId, SubjectName = "Trùng trực tiếp"
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });

        // Danh sách và chi tiết: người chỉ có quyền xem không đính kèm được.
        var readerList = await DataAsync(await s.Reader.GetAsync($"/api/evaluations/collective-records?periodId={s.PeriodId}"));
        Assert.False(Assert.Single(readerList.EnumerateArray()).GetProperty("canAttach").GetBoolean());
        var managerList = await DataAsync(await s.Manager.GetAsync($"/api/evaluations/collective-records?periodId={s.PeriodId}"));
        Assert.True(Assert.Single(managerList.EnumerateArray()).GetProperty("canAttach").GetBoolean());

        // Sửa không gửi phiên bản → 400; gửi phiên bản → được.
        var noVersion = await s.Manager.PutAsJsonAsync($"/api/evaluations/collective-records/{id}", Collective(s, "M07"));
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        Assert.Contains("phiên bản", await MessageAsync(noVersion));
        var body = Collective(s, "M07");
        body["version"] = created.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.OK, (await s.Manager.PutAsJsonAsync($"/api/evaluations/collective-records/{id}", body)).StatusCode);

        // Kỳ đã đóng: không tạo hồ sơ mới, không còn đính kèm được.
        await _factory.WithDbAsync(async db =>
        {
            var period = await db.EvaluationPeriods.SingleAsync(p => p.Id == s.PeriodId);
            period.Status = PeriodStatus.Closed;
            await db.SaveChangesAsync();
        });
        var closedCreate = await s.Manager.PostAsJsonAsync("/api/evaluations/collective-records", Collective(s, "M08"));
        Assert.Equal(HttpStatusCode.Conflict, closedCreate.StatusCode);
        Assert.Contains("đã đóng", await MessageAsync(closedCreate));
        var closedDetail = await DataAsync(await s.Manager.GetAsync($"/api/evaluations/collective-records/{id}"));
        Assert.False(closedDetail.GetProperty("canAttach").GetBoolean());
    }

    [SkippableFact]
    public async Task Meeting_Form12_WithVoteResults_PresentCountCannotDropBelowBallots()
    {
        SkipIfNoDatabase();
        var s = await CreateAsync();
        var request = new Dictionary<string, object?>
        {
            ["periodId"] = s.PeriodId, ["partyCellId"] = s.CellId, ["stage"] = "B3A_COLLECTIVE", ["formCode"] = "M12",
            ["meetingType"] = "Hội nghị đề xuất", ["location"] = "Phòng họp",
            ["startedAt"] = new DateTime(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc),
            ["invitedCount"] = 7, ["presentCount"] = 6, ["absentCount"] = 1
        };
        var created = await DataAsync(await s.Manager.PostAsJsonAsync("/api/evaluations/meetings", request));
        var meetingId = created.GetProperty("id").GetGuid();

        // Kết quả kiểm phiếu do bước đề xuất của tập thể ghi vào biên bản Mẫu 12: 5 phiếu.
        await _factory.WithDbAsync(async db =>
        {
            db.Add(new EvaluationMeetingVoteSummary { MeetingId = meetingId, RecordId = s.RecordId, VotesGood = 4, VotesSatisfactory = 1 });
            await db.SaveChangesAsync();
        });
        var current = await DataAsync(await s.Manager.GetAsync($"/api/evaluations/meetings/{meetingId}"));

        request["version"] = current.GetProperty("version").GetUInt32();
        request["presentCount"] = 4;
        var tooFew = await s.Manager.PutAsJsonAsync($"/api/evaluations/meetings/{meetingId}", request);
        Assert.Equal(HttpStatusCode.BadRequest, tooFew.StatusCode);
        Assert.Contains("tổng số phiếu", await MessageAsync(tooFew));

        request["presentCount"] = 5;
        Assert.Equal(HttpStatusCode.OK, (await s.Manager.PutAsJsonAsync($"/api/evaluations/meetings/{meetingId}", request)).StatusCode);
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }
}
