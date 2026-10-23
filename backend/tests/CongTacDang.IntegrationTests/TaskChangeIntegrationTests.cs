using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Điều chỉnh việc trong quý (Mẫu 04, 05): đề nghị, duyệt → danh mục việc thay đổi trong cùng một lần lưu.</summary>
[Collection(ApiCollection.Name)]
public sealed class TaskChangeIntegrationTests
{
    private readonly ApiFactory _factory;

    public TaskChangeIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Adjust_Exclude_Urgent_ApproveAppliesToTasks_SelfScoreSkipsExcluded()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "09A");
        var recordId = await RecordOfAsync(w, periodId, w.Owner.Id);

        // Chưa duyệt danh sách việc → chưa đề nghị được.
        await Ok(await Post(w.OwnerC, recordId, "tasks/submit", Tasks(), await VersionAsync(w, recordId)));
        var early = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}/task-changes");
        Assert.True(early.GetProperty("applicable").GetBoolean());
        Assert.False(early.GetProperty("canRequestChange").GetBoolean());
        await Ok(await Post(w.LeadC, recordId, "tasks/approve", new { comment = "Đồng ý" }, await VersionAsync(w, recordId)));

        var block = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}/task-changes");
        Assert.True(block.GetProperty("canRequestChange").GetBoolean());
        Assert.True(block.GetProperty("canRecordUrgent").GetBoolean());
        var record = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}");
        var ids = record.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToArray();

        // 1. Điều chỉnh: tổng sai → 400; đúng → chờ duyệt; thêm đề nghị khi đang chờ → 409.
        var wrongTotal = await CreateAsync(w, w.OwnerC, recordId, new
        {
            type = "Adjust", taskId = ids[0], proposal = new { taskName = "Sản phẩm 1 (điều chỉnh)" },
            weights = new Dictionary<Guid, double> { [ids[0]] = 25 }, reason = "Đổi chỉ tiêu theo chỉ đạo"
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongTotal.StatusCode);
        var created = await Data(await CreateAsync(w, w.OwnerC, recordId, new
        {
            type = "Adjust", taskId = ids[0], proposal = new { taskName = "Sản phẩm 1 (điều chỉnh)", targetOutput = "Kết quả 1 mới" },
            weights = new Dictionary<Guid, double> { [ids[0]] = 25, [ids[2]] = 25 }, reason = "Đổi chỉ tiêu theo chỉ đạo"
        }));
        var pending = created.GetProperty("requests")[0];
        Assert.Equal("Pending", pending.GetProperty("status").GetString());
        Assert.False(pending.GetProperty("canDecide").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await CreateAsync(w, w.OwnerC, recordId, new
        {
            type = "Exclude", taskId = ids[1], weights = new Dictionary<Guid, double> { [ids[0]] = 40, [ids[2]] = 30 }, reason = "x"
        })).StatusCode);

        // Người duyệt thấy việc chờ duyệt trong danh sách việc cần xử lý.
        var queue = await Data(w.LeadC, $"/api/evaluations/work-queue?periodId={periodId}");
        Assert.Contains(queue.GetProperty("groups").EnumerateArray(), g => g.GetProperty("step").GetString() == "TASK_CHANGES"
            && g.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("recordId").GetGuid() == recordId));

        var changeId = pending.GetProperty("id").GetGuid();
        var changeVersion = pending.GetProperty("version").GetUInt32();
        // Không có quyền duyệt → 403; chủ hồ sơ tự duyệt → 403; không duyệt thiếu lý do → 400.
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(w.ReaderC, recordId, changeId, "approve", changeVersion, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Decide(w.OwnerC, recordId, changeId, "approve", changeVersion, null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Decide(w.LeadC, recordId, changeId, "reject", changeVersion, " ")).StatusCode);
        // Thiếu / sai version.
        Assert.Equal(HttpStatusCode.Conflict, (await Decide(w.LeadC, recordId, changeId, "approve", changeVersion + 1000, null)).StatusCode);

        var approved = await Data(await Decide(w.LeadC, recordId, changeId, "approve", changeVersion, "Đồng ý điều chỉnh"));
        Assert.Equal("Approved", approved.GetProperty("requests")[0].GetProperty("status").GetString());
        record = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}");
        var tasks = record.GetProperty("tasks");
        Assert.Equal("Sản phẩm 1 (điều chỉnh)", tasks[0].GetProperty("taskName").GetString());
        Assert.Equal("Kết quả 1 mới", tasks[0].GetProperty("targetOutput").GetString());
        Assert.Equal(new[] { 25.0, 20, 25 }, tasks.EnumerateArray().Select(t => t.GetProperty("weight").GetDouble()));

        // 2. Việc đột xuất do người duyệt (cấp giao việc) ghi nhận, giảm việc 1 để tổng vẫn 70.
        var urgent = (await Data(await CreateAsync(w, w.LeadC, recordId, new
        {
            type = "Urgent",
            proposal = new { taskName = "Xử lý sự cố đột xuất", targetOutput = "Khắc phục trong 10 ngày", axisCode = "T1", weight = 10.0 },
            weights = new Dictionary<Guid, double> { [ids[0]] = 15 }, reason = "Nhiệm vụ phát sinh theo chỉ đạo"
        }))).GetProperty("requests")[0];
        await Data(await Decide(w.LeadC, recordId, urgent.GetProperty("id").GetGuid(), "approve", urgent.GetProperty("version").GetUInt32(), null));

        // 3. Loại trừ việc 2 → không duyệt (có lý do) rồi gửi lại và duyệt.
        var exclude = new
        {
            type = "Exclude", taskId = ids[1], weights = new Dictionary<Guid, double> { [ids[0]] = 25, [ids[2]] = 35 },
            reason = "Dự án tạm dừng do nguyên nhân khách quan"
        };
        var excl = (await Data(await CreateAsync(w, w.OwnerC, recordId, exclude))).GetProperty("requests")[0];
        var rejected = await Data(await Decide(w.LeadC, recordId, excl.GetProperty("id").GetGuid(), "reject", excl.GetProperty("version").GetUInt32(), "Chưa có xác nhận của cấp có thẩm quyền"));
        Assert.Equal("Rejected", rejected.GetProperty("requests")[0].GetProperty("status").GetString());
        excl = (await Data(await CreateAsync(w, w.OwnerC, recordId, exclude))).GetProperty("requests")[0];
        await Data(await Decide(w.LeadC, recordId, excl.GetProperty("id").GetGuid(), "approve", excl.GetProperty("version").GetUInt32(), null));

        record = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}");
        tasks = record.GetProperty("tasks");
        Assert.Equal(4, tasks.GetArrayLength());
        Assert.Equal("Excluded", tasks[1].GetProperty("changeStatus").GetString());
        Assert.Equal(0, tasks[1].GetProperty("weight").GetDouble());
        Assert.Equal("DotXuat", tasks[3].GetProperty("kind").GetString());
        Assert.Equal(70.0, tasks.EnumerateArray().Sum(t => t.GetProperty("weight").GetDouble()), 6);

        // 4. Tự chấm: gửi cả điểm (và đánh dấu vượt chuẩn) của việc đã loại trừ → bỏ qua, điểm 0; tổng điểm nhiệm vụ chỉ tính việc
        // còn hiệu lực. Việc đột xuất chưa tự chấm: phiếu để trống.
        Assert.Equal(JsonValueKind.Null, tasks[3].GetProperty("criteriaA_Ratio").ValueKind);
        var taskScores = tasks.EnumerateArray()
            .Select(t => new
            {
                taskId = t.GetProperty("id").GetGuid(), criteriaA_Ratio = 1.0, criteriaB_Ratio = 1.0, criteriaC_Ratio = 1.0, criteriaD_Ratio = 1.0,
                isExceedStandard = t.GetProperty("changeStatus").ValueKind == JsonValueKind.String
            })
            .ToArray();
        var scored = await Data(await Post(w.OwnerC, recordId, "self-score/submit",
            new { generalScores = CriteriaTestData.General(), taskScores }, await VersionAsync(w, recordId)));
        Assert.Equal("AwaitingCellConfirm", scored.GetProperty("status").GetString());
        var excluded = scored.GetProperty("tasks").EnumerateArray().Single(t => t.GetProperty("changeStatus").GetString() == "Excluded");
        Assert.Equal(0, excluded.GetProperty("selfScore").GetDouble());
        Assert.False(excluded.GetProperty("isExceedStandard").GetBoolean());
        Assert.Equal(70.0, scored.GetProperty("tasksScore").GetDouble(), 6);

        // Sau khi nộp tự chấm không đề nghị được nữa.
        var after = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}/task-changes");
        Assert.False(after.GetProperty("canRequestChange").GetBoolean());

        var history = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}/history");
        Assert.True(history.EnumerateArray().Count(h => h.GetProperty("action").GetString() == "TaskChange") >= 6);
    }

    [SkippableFact]
    public async Task Criteria09B_HasNoTaskChanges()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "09B");
        var recordId = await RecordOfAsync(w, periodId, w.Owner.Id);
        var block = await Data(w.OwnerC, $"/api/evaluations/records/{recordId}/task-changes");
        Assert.False(block.GetProperty("applicable").GetBoolean());
        Assert.False(block.GetProperty("canRequestChange").GetBoolean());
        var create = await CreateAsync(w, w.OwnerC, recordId, new
        {
            type = "Urgent", proposal = new { taskName = "x", targetOutput = "y", axisCode = "T1", weight = 10.0 }, reason = "x"
        });
        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
    }

    #region Hỗ trợ

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private sealed class World
    {
        public Guid Dept;
        public TestUser Owner = null!;
        public HttpClient OwnerC = null!, LeadC = null!, ManagerC = null!, ReaderC = null!;
    }

    private async Task<World> WorldAsync()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        await Gate.WaitAsync();
        try
        {
            if (Worlds.TryGetValue(_factory.DatabaseName, out var existing))
                return existing;

            var w = new World();
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            Guid cell = Guid.Empty;
            await _factory.WithDbAsync(async db =>
            {
                var d = new CongTacDang.Domain.Entities.AdministrativeDepartment { Code = $"TC-P-{suffix}", Name = $"Phòng TC {suffix}" };
                var c = new CongTacDang.Domain.Entities.PartyCell { Code = $"TC-C-{suffix}", Name = $"Chi bộ TC {suffix}" };
                db.AddRange(d, c);
                await db.SaveChangesAsync();
                (w.Dept, cell) = (d.Id, c.Id);
            });

            w.Owner = await _factory.CreateUserAsync(w.Dept, cell, ApprovalAuthority.CoSo, $"TC Chủ hồ sơ {suffix}");
            var lead = await _factory.CreateUserAsync(w.Dept, null, ApprovalAuthority.CoSo, $"TC Lãnh đạo Phòng {suffix}");
            var manager = await _factory.CreateUserAsync(null, null);
            var reader = await _factory.CreateUserAsync(null, null);
            await _factory.AssignAsync(w.Owner.Id, await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
            await _factory.AssignAsync(lead.Id, await _factory.GetRoleIdAsync("LANH_DAO_PHONG"), RoleScopeType.Department, w.Dept);
            await _factory.AssignAsync(manager.Id, await _factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await _factory.AssignAsync(reader.Id, (await _factory.CreateRoleAsync("evaluation.read")).Id, RoleScopeType.Global, null);

            w.OwnerC = await _factory.LoginAsAsync(w.Owner.Username, w.Owner.Password, distinctClientIp: true);
            w.LeadC = await _factory.LoginAsAsync(lead.Username, lead.Password, distinctClientIp: true);
            w.ManagerC = await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true);
            w.ReaderC = await _factory.LoginAsAsync(reader.Username, reader.Password, distinctClientIp: true);
            Worlds[_factory.DatabaseName] = w;
            return w;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static object Tasks() => new
    {
        tasks = new[]
        {
            new { taskName = "Sản phẩm 1", targetOutput = "Kết quả 1", weight = 30.0, axisCode = "T1", kind = "TrongTam", role = "ChuTri", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản", confirmer = "Giám đốc" },
            new { taskName = "Sản phẩm 2", targetOutput = "Kết quả 2", weight = 20.0, axisCode = "T1", kind = "ThuongXuyen", role = "ChuTri", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản", confirmer = "Giám đốc" },
            new { taskName = "Sản phẩm 3", targetOutput = "Kết quả 3", weight = 20.0, axisCode = "T1", kind = "ThuongXuyen", role = "PhoiHop", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản", confirmer = "Giám đốc" }
        }
    };

    private static async Task<Guid> OpenPeriodAsync(World w, string form)
    {
        var body = new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter,
            criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.ManagerC, form),
            name = $"Kỳ TC {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        var periodId = (await Data(await w.ManagerC.PostAsJsonAsync("/api/evaluations/periods", body))).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.Owner.Id } }));
        var period = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử điều chỉnh việc", force = true }));
        return periodId;
    }

    private static async Task<Guid> RecordOfAsync(World w, Guid periodId, Guid memberId) =>
        (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == memberId).GetProperty("recordId").GetGuid();

    private static async Task<uint> VersionAsync(World w, Guid recordId) =>
        (await Data(w.ManagerC, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();

    private static Task<HttpResponseMessage> Post(HttpClient client, Guid recordId, string action, object body, uint version)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["version"] = version;
        return client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    private static async Task<HttpResponseMessage> CreateAsync(World w, HttpClient client, Guid recordId, object body) =>
        await Post(client, recordId, "task-changes", body, await VersionAsync(w, recordId));

    private static Task<HttpResponseMessage> Decide(HttpClient client, Guid recordId, Guid changeId, string action, uint version, string? comment) =>
        client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/task-changes/{changeId}/{action}", new { version, comment });

    private static async Task Ok(HttpResponseMessage response) => await Data(response);

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    #endregion
}
