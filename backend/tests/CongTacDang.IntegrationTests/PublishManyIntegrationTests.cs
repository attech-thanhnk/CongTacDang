using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Công bố nhiều hồ sơ: kiểm tra từng hồ sơ như công bố lẻ, hồ sơ lỗi không làm dừng cả lô.</summary>
[Collection(ApiCollection.Name)]
public sealed class PublishManyIntegrationTests
{
    private readonly ApiFactory _factory;

    public PublishManyIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task PublishMany_PublishesValidRecords_ReportsEachFailure()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6];
        var publisher = await _factory.CreateUserAsync(fullName: $"CB Người công bố {sfx}");
        await _factory.AssignAsync(publisher.Id,
            (await _factory.CreateRoleAsync(PermissionCodes.EvaluationPublish, PermissionCodes.EvaluationRead)).Id, RoleScopeType.Global, null);
        var owners = new List<TestUser>();
        for (var i = 0; i < 4; i++)
            owners.Add(await _factory.CreateUserAsync(fullName: $"CB Cán bộ {i + 1} {sfx}"));

        var ids = new List<Guid>();
        Guid ownId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy2, Name = $"Kỳ CB {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                Settings = PeriodGates.DefaultSettingsGatesOff(), CriteriaSnapshot = CriteriaTestData.Snapshot(collectiveFramework: false)
            };
            db.Add(period);
            EvaluationRecord Make(Guid member, RecordStatus status) => new()
            {
                PeriodId = period.Id, MemberId = member, ApprovalAuthority = ApprovalAuthority.CoSo, WeightFrameCode = "K2", Status = status,
                FinalGrade = EvaluationGrade.HoanThanhTot, DecisionRecordedAt = DateTime.UtcNow.AddHours(-1),
                SelfScoredAt = DateTime.UtcNow.AddDays(-2), TotalSelfScore = 85
            };
            var records = new[]
            {
                Make(owners[0].Id, RecordStatus.AwaitingPublish),
                Make(owners[1].Id, RecordStatus.AwaitingPublish),
                Make(owners[2].Id, RecordStatus.AwaitingPublish),
                Make(owners[3].Id, RecordStatus.AwaitingDecision),
                Make(publisher.Id, RecordStatus.AwaitingPublish)
            };
            db.AddRange(records);
            await db.SaveChangesAsync();
            ids.AddRange(records.Select(r => r.Id));
            ownId = records[4].Id;
        });

        using var client = await _factory.LoginAsAsync(publisher.Username, publisher.Password, distinctClientIp: true);
        async Task<uint> Version(Guid id) => (await Data(client, $"/api/evaluations/records/{id}")).GetProperty("version").GetUInt32();

        var missing = Guid.NewGuid();
        var items = new object[]
        {
            new { recordId = ids[0], version = await Version(ids[0]) },
            new { recordId = ids[1], version = await Version(ids[1]) + 1000 }, // phiên bản cũ
            new { recordId = ids[2], version = await Version(ids[2]) },
            new { recordId = ids[3], version = await Version(ids[3]) }, // chưa tới bước công bố
            new { recordId = ownId, version = await Version(ownId) }, // hồ sơ của chính mình
            new { recordId = missing, version = 1u }
        };
        var result = await Data(await client.PostAsJsonAsync("/api/evaluations/records/publish-many", new { items }));
        Assert.Equal(2, result.GetProperty("publishedCount").GetInt32());
        Assert.Equal(4, result.GetProperty("failedCount").GetInt32());
        var rows = result.GetProperty("items").EnumerateArray().ToDictionary(r => r.GetProperty("recordId").GetGuid());
        Assert.True(rows[ids[0]].GetProperty("succeeded").GetBoolean());
        Assert.True(rows[ids[2]].GetProperty("succeeded").GetBoolean());
        foreach (var failedId in new[] { ids[1], ids[3], ownId, missing })
        {
            Assert.False(rows[failedId].GetProperty("succeeded").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(rows[failedId].GetProperty("message").GetString()));
        }
        Assert.Equal($"CB Cán bộ 2 {sfx}", rows[ids[1]].GetProperty("fullName").GetString());

        // Hồ sơ lỗi không bị lưu kèm; hồ sơ hợp lệ đã công bố.
        await _factory.WithDbAsync(async db =>
        {
            var status = await db.EvaluationRecords.Where(r => ids.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Status);
            Assert.Equal(RecordStatus.Published, status[ids[0]]);
            Assert.Equal(RecordStatus.AwaitingPublish, status[ids[1]]);
            Assert.Equal(RecordStatus.Published, status[ids[2]]);
            Assert.Equal(RecordStatus.AwaitingDecision, status[ids[3]]);
            Assert.Equal(RecordStatus.AwaitingPublish, status[ownId]);
        });

        // Danh sách rỗng → 400; người không có quyền công bố: từng hồ sơ báo không thành công.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/evaluations/records/publish-many", new { items = Array.Empty<object>() })).StatusCode);
        var reader = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationRead);
        using var readerC = await _factory.LoginAsAsync(reader.Username, reader.Password, distinctClientIp: true);
        var denied = await Data(await readerC.PostAsJsonAsync("/api/evaluations/records/publish-many",
            new { items = new[] { new { recordId = ids[1], version = await Version(ids[1]) } } }));
        Assert.Equal(0, denied.GetProperty("publishedCount").GetInt32());
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
