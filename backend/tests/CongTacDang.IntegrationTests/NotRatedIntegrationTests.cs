using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Position = CongTacDang.Domain.Entities.Position;

namespace CongTacDang.IntegrationTests;

/// <summary>Hồ sơ Chưa xếp loại ngay từ đầu: bỏ qua các bước, tính vào cột Chưa xếp loại, công bố cùng mọi người.</summary>
[Collection(ApiCollection.Name)]
public sealed class NotRatedIntegrationTests
{
    private readonly ApiFactory _factory;

    public NotRatedIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task MarkNotRated_SkipsSteps_CountsAsNotRated_Publishes_UnmarkReturnsToStart()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        Guid deptId = Guid.Empty, otherDeptId = Guid.Empty, cellId = Guid.Empty, positionId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var dept = new AdministrativeDepartment { Code = $"NR-D-{sfx}", Name = $"Phòng NR {sfx}" };
            var other = new AdministrativeDepartment { Code = $"NR-O-{sfx}", Name = $"Phòng khác NR {sfx}" };
            var cell = new PartyCell { Code = $"NR-C-{sfx}", Name = $"Chi bộ NR {sfx}" };
            var position = new Position
            {
                Name = $"NR M26 {sfx}", StatCode = "M26", DefaultApprovalAuthority = ApprovalAuthority.CoSo, IsLeadership = true,
                Side = PositionSide.Administrative
            };
            db.AddRange(dept, other, cell, position);
            await db.SaveChangesAsync();
            (deptId, otherDeptId, cellId, positionId) = (dept.Id, other.Id, cell.Id, position.Id);
        });

        var absent = await _factory.CreateUserAsync(deptId, cellId, fullName: $"NR Nghỉ dài ngày {sfx}");
        var working = await _factory.CreateUserAsync(deptId, cellId, fullName: $"NR Đi làm {sfx}");
        await _factory.WithDbAsync(async db =>
        {
            foreach (var user in new[] { absent, working })
                db.Add(new MemberPosition { UserId = user.Id, PositionId = positionId, IsPrimary = true, ValidFrom = DateTime.UtcNow.AddYears(-1) });
            await db.SaveChangesAsync();
        });
        var evaluatee = await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA");
        foreach (var user in new[] { absent, working })
            await _factory.AssignAsync(user.Id, evaluatee, RoleScopeType.Global, null);

        var manager = await _factory.LoginAsAsync(await _factory.CreateUserWithPermissionsAsync(
            PermissionCodes.PeriodManage, PermissionCodes.EvaluationRead, PermissionCodes.EvaluationPublish, PermissionCodes.ReportExport));
        var reader = await _factory.LoginAsAsync(await _factory.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationRead));
        var otherManagerUser = await _factory.CreateUserAsync();
        await _factory.AssignAsync(otherManagerUser.Id, (await _factory.CreateRoleAsync(PermissionCodes.PeriodManage, PermissionCodes.EvaluationRead)).Id,
            RoleScopeType.Department, otherDeptId);
        var otherManager = await _factory.LoginAsAsync(otherManagerUser);
        var absentC = await _factory.LoginAsAsync(absent);

        // Kỳ dự thảo có hai người.
        var created = await Data(await manager.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(manager),
            name = $"Kỳ NR {sfx}", startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        }));
        var periodId = created.GetProperty("id").GetGuid();
        await PeriodGates.DisableAsync(manager, periodId);
        await Data(await manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { absent.Id, working.Id } }));
        var participants = await Data(manager, $"/api/evaluations/periods/{periodId}/participants");
        var recordId = RecordOf(participants, absent.Id).GetProperty("recordId").GetGuid();
        var workingId = RecordOf(participants, working.Id).GetProperty("recordId").GetGuid();
        Assert.True(RecordOf(participants, absent.Id).GetProperty("canMarkNotRated").GetBoolean());
        Assert.False(RecordOf(participants, absent.Id).GetProperty("canUnmarkNotRated").GetBoolean());
        Assert.Contains(await ActionsAsync(manager, recordId), a => a == "MarkNotRated");

        // Không có period.manage, hoặc chỉ có ở đơn vị khác → 403; chính chủ hồ sơ cũng không đánh dấu được.
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(reader, recordId, "not-rated", "Nghỉ ốm")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(otherManager, recordId, "not-rated", "Nghỉ ốm")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(absentC, recordId, "not-rated", "Nghỉ ốm")).StatusCode);
        // Thiếu lý do → 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(manager, recordId, "not-rated", "  ")).StatusCode);

        // Đánh dấu: chờ công bố, có quyết định Chưa xếp loại, bước trước quyết định là bỏ qua.
        var marked = await Data(await PostAsync(manager, recordId, "not-rated", "Nghỉ thai sản từ 02 tháng"));
        Assert.Equal("AwaitingPublish", marked.GetProperty("status").GetString());
        Assert.Equal("ChuaXepLoai", marked.GetProperty("finalGrade").GetString());
        Assert.Equal("Nghỉ thai sản từ 02 tháng", marked.GetProperty("notRatedReason").GetString());
        var progress = marked.GetProperty("progress").EnumerateArray().ToDictionary(p => p.GetProperty("step").GetString()!, p => p.GetProperty("state").GetString());
        Assert.Equal("skipped", progress["B1_REGISTER"]);
        Assert.Equal("skipped", progress["B2_SELF_SCORE"]);
        Assert.Equal("done", progress["B4_DECISION"]);
        Assert.Equal("current", progress["B5_PUBLISH"]);
        var history = await Data(manager, $"/api/evaluations/records/{recordId}/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("action").GetString() == "MarkNotRated"
            && h.GetProperty("reason").GetString() == "Nghỉ thai sản từ 02 tháng");
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(manager, recordId, "not-rated", "Lần hai")).StatusCode);
        Assert.Contains(await ActionsAsync(manager, recordId), a => a == "UnmarkNotRated");

        // Kiểm tra sẵn sàng không báo hồ sơ này kẹt ở bước nào trước công bố.
        var readiness = await Data(manager, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.DoesNotContain(readiness.GetProperty("issues").EnumerateArray(), i =>
            i.GetProperty("recordId").GetGuid() == recordId && i.GetProperty("step").GetString() != "B5_PUBLISH");

        // Mở kỳ: hồ sơ vẫn chờ công bố, không có trong việc cần xử lý của chủ hồ sơ.
        await OpenAsync(manager, periodId);
        Assert.Equal("AwaitingPublish", (await Data(manager, $"/api/evaluations/records/{recordId}")).GetProperty("status").GetString());
        Assert.DoesNotContain(await QueueAsync(absentC), id => id == recordId);
        Assert.Empty(await ActionsAsync(absentC, recordId));

        // Bỏ đánh dấu → về bước đầu, các trường đã đặt bị xóa; đánh dấu lại.
        var unmarked = await Data(await PostAsync(manager, recordId, "not-rated/cancel", "Đánh dấu nhầm"));
        Assert.Equal("AwaitingRegistration", unmarked.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, unmarked.GetProperty("notRatedReason").ValueKind);
        Assert.Contains(await QueueAsync(absentC), id => id == recordId);
        await Data(await PostAsync(manager, recordId, "not-rated", "Đi đào tạo dài hạn"));

        // Hồ sơ đã có quyết định thì không đánh dấu nữa.
        await _factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.SingleAsync(r => r.Id == workingId);
            record.Status = RecordStatus.AwaitingPublish;
            record.FinalGrade = EvaluationGrade.HoanThanhTot;
            record.FinalScore = 85;
            record.DecisionRecordedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        });
        var decided = await PostAsync(manager, workingId, "not-rated", "Nghỉ ốm");
        Assert.Equal(HttpStatusCode.Conflict, decided.StatusCode);
        Assert.Contains("quyết định", await MessageAsync(decided));

        // Công bố cùng mọi người; sau công bố không bỏ đánh dấu được.
        var published = await Data(await PostAsync(manager, recordId, "publish", null));
        Assert.Equal("Published", published.GetProperty("status").GetString());
        await Data(await PostAsync(manager, workingId, "publish", null));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(manager, recordId, "not-rated/cancel", "Nhầm")).StatusCode);

        // Mẫu 15B: M26 có 2 người — 1 Hoàn thành tốt, 1 Chưa xếp loại.
        var response = await manager.GetAsync($"/api/reports/form-15b?periodId={periodId}");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var book = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var m26 = book.Worksheet(1).RowsUsed().Single(r => r.Cell(3).GetString() == "M26");
        Assert.Equal(new[] { 2, 0, 1, 0, 0, 1 }, Enumerable.Range(4, 6).Select(c => m26.Cell(c).GetValue<int>()).ToArray());
    }

    private static JsonElement RecordOf(JsonElement participants, Guid memberId) =>
        participants.EnumerateArray().First(p => p.GetProperty("memberId").GetGuid() == memberId);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, string? reason)
    {
        var version = await VersionAsync(recordId, client);
        return await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", new { version, reason });
    }

    /// <summary>Đọc phiên bản bằng chính người gọi; không đọc được thì gửi 0 (để thấy lỗi quyền, không phải lỗi phiên bản).</summary>
    private static async Task<uint> VersionAsync(Guid recordId, HttpClient client)
    {
        var response = await client.GetAsync($"/api/evaluations/records/{recordId}");
        if (!response.IsSuccessStatusCode)
            return 0;
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("data").GetProperty("version").GetUInt32();
    }

    private static async Task<List<string>> ActionsAsync(HttpClient client, Guid recordId) =>
        (await Data(client, $"/api/evaluations/records/{recordId}/actions")).GetProperty("actions").EnumerateArray()
            .Select(a => a.GetProperty("action").GetString()!).ToList();

    private static async Task<List<Guid>> QueueAsync(HttpClient client) =>
        (await Data(client, "/api/evaluations/work-queue")).GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("items").EnumerateArray())
            .Where(i => i.TryGetProperty("recordId", out var r) && r.ValueKind == JsonValueKind.String)
            .Select(i => i.GetProperty("recordId").GetGuid())
            .ToList();

    private static async Task OpenAsync(HttpClient client, Guid periodId)
    {
        var period = await Data(client, $"/api/evaluations/periods/{periodId}");
        await Data(await client.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử", force = true }));
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }
}
