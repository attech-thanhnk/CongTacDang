using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Phiếu kiểm soát (Mẫu 19): lập, kết luận, xác nhận khác người lập, hết hiệu lực khi hồ sơ đổi, mỗi hồ sơ một phiếu.</summary>
[Collection(ApiCollection.Name)]
public sealed class ControlCheckIntegrationTests
{
    private readonly ApiFactory _factory;

    public ControlCheckIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Lifecycle_SeparationOfDuty_StaleAfterRecordChange_OneCurrentCheckPerRecord()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w);
        var recordId = await RecordOfAsync(w, periodId, w.Owner.Id);

        // Chủ hồ sơ không có quyền lập phiếu.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm", recordIds = new[] { recordId } })).StatusCode);
        // Thiếu nhóm → 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = " ", recordIds = new[] { recordId } })).StatusCode);

        var created = await Data(await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm chuyên viên Phòng TC", recordIds = new[] { recordId } }));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal(10, created.GetProperty("items").GetArrayLength());
        Assert.Equal(1, created.GetProperty("itemsVersion").GetInt32());
        Assert.Equal(1, created.GetProperty("revision").GetInt32());

        // Hồ sơ chỉ thuộc một phiếu còn hiệu lực.
        Assert.Equal(HttpStatusCode.Conflict, (await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm khác", recordIds = new[] { recordId } })).StatusCode);

        // Chủ hồ sơ không xem được phiếu chứa hồ sơ của mình.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerC.GetAsync($"/api/control-checks/{id}")).StatusCode);

        // Chưa chọn đủ mục / chưa kết luận → không gửi được.
        Assert.Equal(HttpStatusCode.BadRequest, (await Act(w.BothC, id, "submit", await VersionOf(w, id))).StatusCode);

        // Kết luận Đủ điều kiện khi còn mục Chưa đạt → không gửi được (400).
        var failing = Codes().Select(c => new
        {
            itemCode = c, result = c == "K03" ? "Fail" : c == "K07" ? "NotApplicable" : "Pass",
            evidence = c is "K03" or "K07" ? "Có ghi chú" : null
        }).ToList();
        var draft = await Data(await w.BothC.PutAsJsonAsync($"/api/control-checks/{id}",
            new { version = await VersionOf(w, id), items = failing, conclusion = "Eligible" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await Act(w.BothC, id, "submit", draft.GetProperty("version").GetUInt32())).StatusCode);

        // K/AD có lý do không cản kết luận Đủ điều kiện.
        var notApplicableOnly = Codes().Select(c => new
        {
            itemCode = c, result = c == "K07" ? "NotApplicable" : "Pass", evidence = c == "K07" ? "Không có vị trí an toàn trọng yếu" : null
        }).ToList();
        var ready = await Data(await w.BothC.PutAsJsonAsync($"/api/control-checks/{id}",
            new { version = await VersionOf(w, id), items = notApplicableOnly, conclusion = "Eligible" }));
        Assert.True(ready.GetProperty("canSubmit").GetBoolean());
        var submitted = await Data(await Act(w.BothC, id, "submit", ready.GetProperty("version").GetUInt32()));
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        // Đang chờ xác nhận → không sửa được.
        Assert.Equal(HttpStatusCode.Conflict, (await w.BothC.PutAsJsonAsync($"/api/control-checks/{id}",
            new { version = await VersionOf(w, id), groupName = "Đổi tên" })).StatusCode);

        // Người lập vừa có quyền xác nhận vẫn không tự xác nhận.
        Assert.Equal(HttpStatusCode.Forbidden, (await Act(w.BothC, id, "confirm", await VersionOf(w, id))).StatusCode);
        // Người chỉ có quyền lập không xác nhận được.
        Assert.Equal(HttpStatusCode.Forbidden, (await Act(w.PerformerC, id, "confirm", await VersionOf(w, id))).StatusCode);
        // Trả lại thiếu lý do → 400; có lý do → về nháp.
        Assert.Equal(HttpStatusCode.BadRequest, (await Act(w.ConfirmerC, id, "return", await VersionOf(w, id), " ")).StatusCode);
        var returned = await Data(await Act(w.ConfirmerC, id, "return", await VersionOf(w, id), "Bổ sung minh chứng"));
        Assert.Equal("Draft", returned.GetProperty("status").GetString());
        Assert.Equal("Bổ sung minh chứng", returned.GetProperty("returnReason").GetString());
        await Data(await Act(w.BothC, id, "submit", returned.GetProperty("version").GetUInt32()));

        // Sai version → 409.
        Assert.Equal(HttpStatusCode.Conflict, (await Act(w.ConfirmerC, id, "confirm", await VersionOf(w, id) + 1000)).StatusCode);

        var confirmed = await Data(await Act(w.ConfirmerC, id, "confirm", await VersionOf(w, id)));
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
        Assert.False(confirmed.GetProperty("isStale").GetBoolean());
        Assert.Equal(ControlGateState.Satisfied, (await GateAsync(recordId)).State);

        // Hồ sơ đổi sau xác nhận (chủ hồ sơ nộp danh sách việc) → phiếu hết hiệu lực.
        await Task.Delay(50);
        await Ok(await PostRecord(w.OwnerC, recordId, "tasks/submit", Tasks(), await RecordVersionAsync(w, recordId)));
        var stale = await Data(w.ConfirmerC, $"/api/control-checks/{id}");
        Assert.True(stale.GetProperty("isStale").GetBoolean());
        Assert.Equal(ControlGateState.Stale, (await GateAsync(recordId)).State);

        // Lập phiếu mới cho hồ sơ khi phiếu cũ đã hết hiệu lực: phiếu cũ nhả hồ sơ.
        var second = await Data(await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm lập lại", recordIds = new[] { recordId } }));
        var secondId = second.GetProperty("id").GetGuid();
        Assert.Equal(ControlGateState.Pending, (await GateAsync(recordId)).State);

        // Mở lại phiếu chưa xác nhận → 409; hủy cần lý do, hồ sơ được giải phóng.
        Assert.Equal(HttpStatusCode.Conflict, (await Act(w.ConfirmerC, secondId, "reopen", await VersionOf(w, secondId), "Lý do")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Act(w.BothC, secondId, "cancel", await VersionOf(w, secondId))).StatusCode);
        var cancelled = await Data(await Act(w.BothC, secondId, "cancel", await VersionOf(w, secondId), "Lập nhầm nhóm"));
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());
        Assert.Equal(ControlGateState.NoCheck, (await GateAsync(recordId)).State);

        // Phiếu Cần hoàn thiện đã xác nhận không mở cổng; mở lại tăng Revision.
        var third = await Data(await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm cần hoàn thiện", recordIds = new[] { recordId } }));
        var thirdId = third.GetProperty("id").GetGuid();
        var needsWork = await Data(await w.BothC.PutAsJsonAsync($"/api/control-checks/{thirdId}", new
        {
            version = await VersionOf(w, thirdId),
            items = Codes().Select(c => new { itemCode = c, result = c == "K08" ? "Fail" : "Pass", evidence = c == "K08" ? "Chưa giải trình" : null }).ToList(),
            conclusion = "NeedsWork", needsWorkContent = "Giải trình chênh lệch điểm"
        }));
        await Data(await Act(w.BothC, thirdId, "submit", needsWork.GetProperty("version").GetUInt32()));
        await Data(await Act(w.ConfirmerC, thirdId, "confirm", await VersionOf(w, thirdId)));
        Assert.Equal(ControlGateState.NeedsWork, (await GateAsync(recordId)).State);
        var reopened = await Data(await Act(w.ConfirmerC, thirdId, "reopen", await VersionOf(w, thirdId), "Đã bổ sung giải trình"));
        Assert.Equal("Draft", reopened.GetProperty("status").GetString());
        Assert.Equal(2, reopened.GetProperty("revision").GetInt32());
        var history = reopened.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("action").GetString()).ToArray();
        Assert.Contains("Reopen", history);

        // Danh sách chỉ gồm phiếu chưa hủy.
        var list = await Data(w.ConfirmerC, $"/api/control-checks?periodId={periodId}");
        Assert.DoesNotContain(list.EnumerateArray(), c => c.GetProperty("id").GetGuid() == secondId);
        Assert.Contains(list.EnumerateArray(), c => c.GetProperty("id").GetGuid() == thirdId);
    }

    [SkippableFact]
    public async Task Readiness_RequiredGates_NoIssueWhenPreparerAndConfirmerExist()
    {
        var w = await WorldAsync();
        var body = new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter,
            criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.ManagerC),
            name = $"Kỳ KS cổng {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        var periodId = (await Data(await w.ManagerC.PostAsJsonAsync("/api/evaluations/periods", body))).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.Owner.Id } }));

        var readiness = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/readiness");
        var permissions = readiness.GetProperty("issues").EnumerateArray().Select(i => i.GetProperty("permission").GetString()).ToList();
        // Cơ sở dữ liệu kiểm thử dùng chung nên chỉ khẳng định được phía đã có người: người lập, người xác nhận kiểm soát và người lập hiệu chuẩn đều khác chủ hồ sơ.
        // Kiểm soát đã có người lập và người xác nhận là hai tài khoản khác nhau; hiệu chuẩn đã có người lập (vai trò thẩm định) nhưng chưa ai được gán quyền chốt.
        Assert.DoesNotContain(PermissionCodes.EvaluationControlPerform, permissions);
        Assert.DoesNotContain(PermissionCodes.EvaluationControlConfirm, permissions);
        Assert.DoesNotContain(PermissionCodes.EvaluationCalibrationManage, permissions);
    }

    #region Hỗ trợ

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private sealed class World
    {
        public Guid Dept;
        public TestUser Owner = null!;
        public HttpClient OwnerC = null!, ManagerC = null!, PerformerC = null!, ConfirmerC = null!, BothC = null!;
    }

    private static string[] Codes() => Enumerable.Range(1, 10).Select(i => $"K{i:00}").ToArray();

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
                var d = new CongTacDang.Domain.Entities.AdministrativeDepartment { Code = $"KS-P-{suffix}", Name = $"Phòng KS {suffix}" };
                var c = new CongTacDang.Domain.Entities.PartyCell { Code = $"KS-C-{suffix}", Name = $"Chi bộ KS {suffix}" };
                db.AddRange(d, c);
                await db.SaveChangesAsync();
                (w.Dept, cell) = (d.Id, c.Id);
            });

            w.Owner = await _factory.CreateUserAsync(w.Dept, cell, ApprovalAuthority.CoSo, $"KS Chủ hồ sơ {suffix}");
            var manager = await _factory.CreateUserAsync(null, null);
            var performer = await _factory.CreateUserAsync(null, null);
            var confirmer = await _factory.CreateUserAsync(null, null);
            var both = await _factory.CreateUserAsync(null, null);
            var performRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationControlPerform, PermissionCodes.EvaluationRead)).Id;
            var confirmRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationControlConfirm, PermissionCodes.EvaluationRead)).Id;
            await _factory.AssignAsync(w.Owner.Id, await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
            await _factory.AssignAsync(manager.Id, await _factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await _factory.AssignAsync(performer.Id, performRole, RoleScopeType.Global, null);
            await _factory.AssignAsync(confirmer.Id, confirmRole, RoleScopeType.Global, null);
            await _factory.AssignAsync(both.Id, performRole, RoleScopeType.Global, null);
            await _factory.AssignAsync(both.Id, confirmRole, RoleScopeType.Global, null);

            w.OwnerC = await _factory.LoginAsAsync(w.Owner.Username, w.Owner.Password, distinctClientIp: true);
            w.ManagerC = await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true);
            w.PerformerC = await _factory.LoginAsAsync(performer.Username, performer.Password, distinctClientIp: true);
            w.ConfirmerC = await _factory.LoginAsAsync(confirmer.Username, confirmer.Password, distinctClientIp: true);
            w.BothC = await _factory.LoginAsAsync(both.Username, both.Password, distinctClientIp: true);
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

    private static async Task<Guid> OpenPeriodAsync(World w)
    {
        var body = new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter,
            criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.ManagerC),
            name = $"Kỳ KS {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        var periodId = (await Data(await w.ManagerC.PostAsJsonAsync("/api/evaluations/periods", body))).GetProperty("id").GetGuid();
        await PeriodGates.DisableAsync(w.ManagerC, periodId, WorkflowGates.Calibration);
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.Owner.Id } }));
        var period = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử phiếu kiểm soát", force = true }));
        return periodId;
    }

    private static async Task<Guid> RecordOfAsync(World w, Guid periodId, Guid memberId) =>
        (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == memberId).GetProperty("recordId").GetGuid();

    private static async Task<uint> RecordVersionAsync(World w, Guid recordId) =>
        (await Data(w.ManagerC, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();

    private static async Task<uint> VersionOf(World w, Guid checkId) =>
        (await Data(w.ConfirmerC, $"/api/control-checks/{checkId}")).GetProperty("version").GetUInt32();

    private static Task<HttpResponseMessage> PostRecord(HttpClient client, Guid recordId, string action, object body, uint version)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["version"] = version;
        return client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    private static Task<HttpResponseMessage> Act(HttpClient client, Guid checkId, string action, uint version, string? reason = null) =>
        client.PostAsJsonAsync($"/api/control-checks/{checkId}/{action}", new { version, reason });

    private async Task<ControlGateResult> GateAsync(Guid recordId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IControlCheckGate>().EvaluateAsync(recordId);
    }

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
