using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Hồ sơ tập thể Mẫu 06/07 chấm theo khung tiêu chí tập thể của bộ tiêu chí của kỳ: điểm từng tiêu chí, máy chủ tự cộng điểm
/// nhóm; bộ không có khung thì nhập thẳng hai điểm nhóm; danh mục biểu mẫu theo kỳ trả khung.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CollectiveCriteriaScoresIntegrationTests
{
    private readonly ApiFactory _factory;

    public CollectiveCriteriaScoresIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    [SkippableFact]
    public async Task FrameworkPeriod_ScoresEachCriterion_SumsGroups_RejectsInvalid_AndPlainPeriodKeepsDirectScores()
    {
        SkipIfNoDatabase();
        var s = await CreateScenarioAsync();

        // (d) Danh mục theo kỳ có khung → trả khung, Kind dạng chuỗi; không có kỳ → khung rỗng, 30/70.
        var catalog = await DataAsync(await s.Client.GetAsync($"/api/evaluations/collective-forms/catalog?periodId={s.FrameworkPeriod}"));
        var groups = catalog.GetProperty("collectiveGroups").EnumerateArray().ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal("General", groups[0].GetProperty("kind").GetString());
        Assert.Equal("Task", groups[1].GetProperty("kind").GetString());
        Assert.Equal("I.2", groups[0].GetProperty("items")[1].GetProperty("code").GetString());
        Assert.Equal(20, groups[0].GetProperty("items")[1].GetProperty("maxScore").GetDouble());
        Assert.Equal(30, catalog.GetProperty("generalMaxScore").GetDouble());
        Assert.Equal(70, catalog.GetProperty("taskMaxScore").GetDouble());
        Assert.Equal(4, catalog.GetProperty("form07Strengths").GetArrayLength());

        var plainCatalog = await DataAsync(await s.Client.GetAsync("/api/evaluations/collective-forms/catalog"));
        Assert.Equal(0, plainCatalog.GetProperty("collectiveGroups").GetArrayLength());
        Assert.Equal(30, plainCatalog.GetProperty("generalMaxScore").GetDouble());
        Assert.Equal(HttpStatusCode.NotFound,
            (await s.Client.GetAsync($"/api/evaluations/collective-forms/catalog?periodId={Guid.NewGuid()}")).StatusCode);

        // (a) Chấm từng tiêu chí → điểm nhóm/tổng tự cộng, bỏ qua hai điểm nhóm gửi lên.
        var scores = new Dictionary<string, double> { ["I.1"] = 8.25, ["I.2"] = 17, ["II.1"] = 35.5, ["II.2"] = 25 };
        var created = await DataAsync(await s.Client.PostAsJsonAsync("/api/evaluations/collective-records",
            Body(s, s.FrameworkPeriod, "M07", scores, general: 1, task: 1)));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(25.25, created.GetProperty("generalCriteriaScore").GetDouble());
        Assert.Equal(60.5, created.GetProperty("taskCriteriaScore").GetDouble());
        Assert.Equal(85.75, created.GetProperty("totalScore").GetDouble());

        var fetched = await DataAsync(await s.Client.GetAsync($"/api/evaluations/collective-records/{id}"));
        var stored = fetched.GetProperty("criteriaScores");
        Assert.Equal(4, stored.EnumerateObject().Count());
        Assert.Equal(8.25, stored.GetProperty("I.1").GetDouble());
        Assert.Equal(35.5, stored.GetProperty("II.1").GetDouble());

        // Sửa: điểm mới → cộng lại.
        var update = Body(s, s.FrameworkPeriod, "M07", new Dictionary<string, double> { ["I.1"] = 10, ["I.2"] = 20, ["II.1"] = 40, ["II.2"] = 30 });
        update["version"] = fetched.GetProperty("version").GetUInt32();
        var updated = await DataAsync(await s.Client.PutAsJsonAsync($"/api/evaluations/collective-records/{id}", update));
        Assert.Equal(30, updated.GetProperty("generalCriteriaScore").GetDouble());
        Assert.Equal(70, updated.GetProperty("taskCriteriaScore").GetDouble());
        Assert.Equal(100, updated.GetProperty("totalScore").GetDouble());

        // Mẫu 06 cũng chấm theo khung.
        var m06 = await DataAsync(await s.Client.PostAsJsonAsync("/api/evaluations/collective-records", Body(s, s.FrameworkPeriod, "M06", scores)));
        Assert.Equal(85.75, m06.GetProperty("totalScore").GetDouble());

        // (b) Thiếu tiêu chí / vượt điểm tối đa / mã lạ / không gửi điểm từng tiêu chí → 400.
        var missing = new Dictionary<string, double>(scores);
        missing.Remove("II.2");
        await AssertBadRequestAsync(s, Body(s, s.FrameworkPeriod, "M07", missing), "II.2");
        await AssertBadRequestAsync(s, Body(s, s.FrameworkPeriod, "M07", new Dictionary<string, double>(scores) { ["I.1"] = 10.5 }), "I.1");
        await AssertBadRequestAsync(s, Body(s, s.FrameworkPeriod, "M07", new Dictionary<string, double>(scores) { ["I.1"] = -1 }), "I.1");
        await AssertBadRequestAsync(s, Body(s, s.FrameworkPeriod, "M07", new Dictionary<string, double>(scores) { ["X.9"] = 1 }), "X.9");
        await AssertBadRequestAsync(s, Body(s, s.FrameworkPeriod, "M07", null, general: 25, task: 60), "criteriaScores");

        // Mẫu 08 không theo khung: CriteriaScores rỗng, giữ hai điểm nhập thẳng.
        var m08 = await DataAsync(await s.Client.PostAsJsonAsync("/api/evaluations/collective-records",
            Body(s, s.FrameworkPeriod, "M08", scores, general: 20, task: 50)));
        Assert.Equal(70, m08.GetProperty("totalScore").GetDouble());
        Assert.Empty(m08.GetProperty("criteriaScores").EnumerateObject());

        // (c) Kỳ không có khung → nhập thẳng hai điểm như cũ; điểm từng tiêu chí gửi lên bị bỏ qua.
        var plain = await DataAsync(await s.Client.PostAsJsonAsync("/api/evaluations/collective-records",
            Body(s, s.PlainPeriod, "M07", scores, general: 27, task: 63)));
        Assert.Equal(27, plain.GetProperty("generalCriteriaScore").GetDouble());
        Assert.Equal(63, plain.GetProperty("taskCriteriaScore").GetDouble());
        Assert.Equal(90, plain.GetProperty("totalScore").GetDouble());
        Assert.Empty(plain.GetProperty("criteriaScores").EnumerateObject());
        Assert.Equal(HttpStatusCode.BadRequest, (await s.Client.PostAsJsonAsync("/api/evaluations/collective-records",
            Body(s, s.PlainPeriod, "M07", null, general: 31, task: 63))).StatusCode);
        var plainCatalogByPeriod = await DataAsync(await s.Client.GetAsync($"/api/evaluations/collective-forms/catalog?periodId={s.PlainPeriod}"));
        Assert.Equal(0, plainCatalogByPeriod.GetProperty("collectiveGroups").GetArrayLength());
    }

    private static async Task AssertBadRequestAsync(Scenario s, Dictionary<string, object?> body, string expectedInMessage)
    {
        var response = await s.Client.PostAsJsonAsync("/api/evaluations/collective-records", body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{(int)response.StatusCode}: {text}");
        Assert.Contains(expectedInMessage, text);
    }

    private static Dictionary<string, object?> Body(Scenario s, Guid periodId, string form, Dictionary<string, double>? criteriaScores,
        double general = 0, double task = 0) => new()
    {
        ["periodId"] = periodId, ["form"] = form, ["partyCellId"] = s.Cell, ["subjectName"] = "Chi ủy thử khung tập thể",
        ["generalCriteriaScore"] = general, ["taskCriteriaScore"] = task, ["selfProposedGrade"] = "HoanThanhTot",
        ["criteriaScores"] = criteriaScores
    };

    /// <summary>Khung tập thể thử: nhóm I (chung) I.1 = 10, I.2 = 20; nhóm II (nhiệm vụ) II.1 = 40, II.2 = 30.</summary>
    private static string Snapshot(bool withFramework)
    {
        var content = CriteriaSetDefaults.Build09A();
        content.CollectiveGroups = withFramework
            ? new List<CollectiveCriteriaGroup>
            {
                new()
                {
                    Code = "I", Name = "Nhóm tiêu chí chung", Kind = CollectiveScoreKind.General,
                    Items = new List<CriteriaItem>
                    {
                        new() { Code = "I.1", Text = "Chấp hành nguyên tắc tập trung dân chủ", MaxScore = 10 },
                        new() { Code = "I.2", Text = "Xây dựng tổ chức vững mạnh", MaxScore = 20 }
                    }
                },
                new()
                {
                    Code = "II", Name = "Kết quả thực hiện nhiệm vụ", Kind = CollectiveScoreKind.Task,
                    Items = new List<CriteriaItem>
                    {
                        new() { Code = "II.1", Text = "Hoàn thành chỉ tiêu kế hoạch", MaxScore = 40 },
                        new() { Code = "II.2", Text = "Nhiệm vụ đột xuất", MaxScore = 30 }
                    }
                }
            }
            : new List<CollectiveCriteriaGroup>();
        return new CriteriaSnapshot
        {
            SetId = Guid.Empty, Code = CriteriaSetDefaults.Code09A, Name = CriteriaSetDefaults.Name09A,
            SelfScoreForm = CriteriaSetContent.Form09A, TakenAt = DateTime.UtcNow, Content = content
        }.ToJson();
    }

    private async Task<Scenario> CreateScenarioAsync()
    {
        var s = new Scenario();
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await _factory.WithDbAsync(async db =>
        {
            var cell = new PartyCell { Code = $"KTT-{sfx}", Name = $"Chi bộ khung tập thể {sfx}" };
            cell.Path = OrgTree.BuildPath(null, cell.Id);
            EvaluationPeriod Period(string name, bool framework) => new()
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = framework ? EvaluationQuarter.Quy2 : EvaluationQuarter.Quy1, Name = $"{name} {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                CriteriaSnapshot = Snapshot(framework)
            };
            var framework = Period("Kỳ có khung tập thể", true);
            var plain = Period("Kỳ không khung tập thể", false);
            db.AddRange(cell, framework, plain);
            await db.SaveChangesAsync();
            (s.Cell, s.FrameworkPeriod, s.PlainPeriod) = (cell.Id, framework.Id, plain.Id);
        });

        var user = await _factory.CreateUserWithPermissionsAsync(new[] { PermissionCodes.CollectiveManage, PermissionCodes.EvaluationRead });
        s.Client = await _factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
        return s;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private sealed class Scenario
    {
        public Guid Cell, FrameworkPeriod, PlainPeriod;
        public HttpClient Client = null!;
    }
}
