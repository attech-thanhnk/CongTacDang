using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Lưu nháp phiếu tự chấm: không đổi trạng thái, không ghi lịch sử, giữ nội dung dở dang; sai định dạng vẫn 400.</summary>
[Collection(ApiCollection.Name)]
public sealed class SelfScoreDraftIntegrationTests
{
    private readonly ApiFactory _factory;

    public SelfScoreDraftIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task D1_Draft_KeepsStatus_KeepsContent_ThenSubmit()
    {
        var w = await World.CreateAsync(_factory);
        var recordId = await OpenRecordAsync(w);
        var historyBefore = (await Data(w.Owner, $"/api/evaluations/records/{recordId}/history")).GetArrayLength();

        // Phiếu dở dang: hai tiêu chí chung (một tiêu chí giảm điểm chưa nêu căn cứ), một trục, mục 09C trống, một dòng nhiệm vụ trống.
        var draftBody = new
        {
            generalScores = new Dictionary<string, object>
            {
                ["1.1"] = new { score = 2 },
                ["2.1"] = new { score = 0 }
            },
            axisScores = new Dictionary<string, double> { ["T1"] = 12 },
            selfAssessment = new Dictionary<string, string>(),
            axisReports = new Dictionary<string, object>
            {
                ["T1"] = new { tasks = new object[] { new { content = "Việc nháp T1 (IT)", level = 1 }, new { content = "" } }, status = "Đang làm (IT)" }
            },
            axisNotes = new Dictionary<string, object> { ["T1"] = new { target = "Mục tiêu nháp (IT)" } },
            generalGroupNotes = new Dictionary<string, string> { ["1"] = "Nhận xét nháp (IT)" }
        };
        var draft = await Data(await PostAsync(w.Owner, recordId, "self-score/draft", draftBody, w.Manager));
        Assert.Equal("AwaitingSelfScore", draft.GetProperty("status").GetString());
        Assert.True(!draft.TryGetProperty("selfScoredAt", out var scoredAt) || scoredAt.ValueKind == JsonValueKind.Null);
        Assert.Equal(12, draft.GetProperty("axisScores").GetProperty("T1").GetDouble());
        Assert.Equal(12, draft.GetProperty("tasksScore").GetDouble());
        Assert.Equal("Việc nháp T1 (IT)", draft.GetProperty("axisReports").GetProperty("T1").GetProperty("tasks")[0].GetProperty("content").GetString());
        Assert.Equal(1, draft.GetProperty("axisReports").GetProperty("T1").GetProperty("tasks").GetArrayLength());
        Assert.Equal("Mục tiêu nháp (IT)", draft.GetProperty("axisNotes").GetProperty("T1").GetProperty("target").GetString());
        Assert.Equal("Nhận xét nháp (IT)", draft.GetProperty("generalGroupNotes").GetProperty("1").GetString());
        Assert.Equal(2, draft.GetProperty("generalScores").EnumerateObject().Count());

        // Nạp lại: nội dung còn nguyên, không thêm dòng lịch sử.
        var reloaded = await Data(w.Owner, $"/api/evaluations/records/{recordId}");
        Assert.Equal("AwaitingSelfScore", reloaded.GetProperty("status").GetString());
        Assert.Equal("Nhận xét nháp (IT)", reloaded.GetProperty("generalGroupNotes").GetProperty("1").GetString());
        Assert.Equal(historyBefore, (await Data(w.Owner, $"/api/evaluations/records/{recordId}/history")).GetArrayLength());

        // Sai định dạng vẫn bị từ chối: điểm trục vượt tối đa, mã tiêu chí lạ, mức không có.
        var overMax = await PostAsync(w.Owner, recordId, "self-score/draft", new { axisScores = new Dictionary<string, double> { ["T1"] = 999 } }, w.Manager);
        Assert.Equal(HttpStatusCode.BadRequest, overMax.StatusCode);
        var unknownItem = await PostAsync(w.Owner, recordId, "self-score/draft",
            new { generalScores = new Dictionary<string, object> { ["9.9"] = new { score = 1 } } }, w.Manager);
        Assert.Equal(HttpStatusCode.BadRequest, unknownItem.StatusCode);
        var badGrade = await PostAsync(w.Owner, recordId, "self-score/draft", new { selfProposedGrade = "KhongCo" }, w.Manager);
        Assert.Equal(HttpStatusCode.BadRequest, badGrade.StatusCode);

        // Thiếu phiên bản → 400; phiên bản cũ → 409.
        var noVersion = await w.Owner.PostAsJsonAsync($"/api/evaluations/records/{recordId}/self-score/draft", new { });
        Assert.Equal(HttpStatusCode.BadRequest, noVersion.StatusCode);
        var stale = await w.Owner.PostAsJsonAsync($"/api/evaluations/records/{recordId}/self-score/draft", new { version = 1u });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Người khác (kể cả người cũng có quyền tự chấm hồ sơ của mình) → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.OtherSelf, recordId, "self-score/draft", draftBody, w.Manager)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.CellSec, recordId, "self-score/draft", draftBody, w.Manager)).StatusCode);

        // Nộp phiếu đầy đủ sau khi lưu nháp.
        var submitted = await Data(await PostAsync(w.Owner, recordId, "self-score/submit", FullBody(), w.Manager));
        Assert.Equal("AwaitingCellConfirm", submitted.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, submitted.GetProperty("selfScoredAt").ValueKind);
        Assert.Equal(historyBefore + 1, (await Data(w.Owner, $"/api/evaluations/records/{recordId}/history")).GetArrayLength());

        // Đã qua bước tự chấm → không lưu nháp được (409), nội dung đã nộp giữ nguyên.
        var afterSubmit = await PostAsync(w.Owner, recordId, "self-score/draft", draftBody, w.Manager);
        Assert.Equal(HttpStatusCode.Conflict, afterSubmit.StatusCode);
        var final = await Data(w.Owner, $"/api/evaluations/records/{recordId}");
        Assert.Equal("Tự đánh giá mục I (IT)", final.GetProperty("selfAssessment").GetProperty("I").GetString());
    }

    #region Hỗ trợ

    private static object FullBody() => new
    {
        generalScores = CriteriaTestData.General("2.1"),
        axisScores = CriteriaTestData.Axis(14, 9, 9, 14, 9, 9),
        selfProposedGrade = "HoanThanhTot",
        selfAssessment = new Dictionary<string, string> { ["I"] = "Tự đánh giá mục I (IT)" },
        axisReports = new Dictionary<string, object>
        {
            ["T1"] = new { tasks = new[] { new { content = "Việc T1 (IT)", level = 1 } }, status = "Hoàn thành (IT)" }
        }
    };

    private static async Task<Guid> OpenRecordAsync(World w)
    {
        var body = new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.Manager, "09B"),
            name = $"Kỳ nháp {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        var periodId = (await Data(await w.Manager.PostAsJsonAsync("/api/evaluations/periods", body))).GetProperty("id").GetGuid();
        await Data(await w.Manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.OwnerId } }));
        var period = await Data(w.Manager, $"/api/evaluations/periods/{periodId}");
        await Data(await w.Manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử lưu nháp", force = true }));
        return (await Data(w.Manager, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == w.OwnerId).GetProperty("recordId").GetGuid();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body, HttpClient reader)
    {
        var version = (await Data(reader, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["version"] = version;
        return await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    /// <summary>Chủ hồ sơ, người khác cũng có quyền tự chấm, Chi ủy Chi bộ, người quản lý kỳ.</summary>
    private sealed class World
    {
        public Guid OwnerId;
        public HttpClient Owner = null!, OtherSelf = null!, CellSec = null!, Manager = null!;

        public static async Task<World> CreateAsync(ApiFactory factory)
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var w = new World();
            Guid dept = Guid.Empty, cell = Guid.Empty;
            await factory.WithDbAsync(async db =>
            {
                var d = new AdministrativeDepartment { Code = $"N-P1-{suffix}", Name = $"Phòng nháp {suffix}" };
                var c = new PartyCell { Code = $"N-C1-{suffix}", Name = $"Chi bộ nháp {suffix}" };
                db.AddRange(d, c);
                await db.SaveChangesAsync();
                (dept, cell) = (d.Id, c.Id);
            });

            var owner = await factory.CreateUserAsync(dept, cell, ApprovalAuthority.CoSo, $"N Chủ hồ sơ {suffix}");
            w.OwnerId = owner.Id;
            var otherSelf = await factory.CreateUserAsync(dept, cell);
            var cellSec = await factory.CreateUserAsync(null, null);
            var manager = await factory.CreateUserAsync(null, null);

            var selfRole = await factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA");
            await factory.AssignAsync(owner.Id, selfRole, RoleScopeType.Global, null);
            await factory.AssignAsync(otherSelf.Id, selfRole, RoleScopeType.Global, null);
            await factory.AssignAsync(cellSec.Id, await factory.GetRoleIdAsync("CHI_UY_CHI_BO"), RoleScopeType.PartyCell, cell);
            await factory.AssignAsync(manager.Id, await factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);

            Task<HttpClient> Login(TestUser user) => factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            w.Owner = await Login(owner);
            w.OtherSelf = await Login(otherSelf);
            w.CellSec = await Login(cellSec);
            w.Manager = await Login(manager);
            return w;
        }
    }

    #endregion
}
