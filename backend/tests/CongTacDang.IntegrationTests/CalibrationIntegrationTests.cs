using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Biên bản hiệu chuẩn (Mẫu 20): phân quyền theo hồ sơ, lập, chốt áp kết quả, mở lại.</summary>
[Collection(ApiCollection.Name)]
public sealed class CalibrationIntegrationTests
{
    private readonly ApiFactory _factory;

    public CalibrationIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Calibration_CreateConcludeApplyReopen_EnforcesPermissionsAndConflictOfInterest()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync();

        // Người lập không có quyền chốt trên hồ sơ nào thì chỉ lập được; chủ hồ sơ có quyền lập vẫn không lập biên bản chứa hồ sơ mình.
        var ownerAttempt = await w.OwnerManagerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordC));
        Assert.Equal(HttpStatusCode.Forbidden, ownerAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.NobodyC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordB))).StatusCode);

        var created = await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordB)));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal(2, created.GetProperty("records").GetArrayLength());

        // Hồ sơ đã thuộc biên bản còn hiệu lực thì không đưa vào biên bản khác.
        Assert.Equal(HttpStatusCode.Conflict, (await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordB))).StatusCode);
        var candidates = await Data(w.PreparerC, $"/api/calibrations/candidates?periodId={w.PeriodId}");
        Assert.DoesNotContain(candidates.EnumerateArray(), c => c.GetProperty("recordId").GetGuid() == w.RecordA);
        Assert.Contains(candidates.EnumerateArray(), c => c.GetProperty("recordId").GetGuid() == w.RecordC);

        // Chủ hồ sơ trong biên bản không xem được biên bản.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerManagerC.GetAsync($"/api/calibrations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.NobodyC.GetAsync($"/api/calibrations/{id}")).StatusCode);

        // Lưu bản nháp: hồ sơ A hạ xuống Hoàn thành.
        var save = new
        {
            version = created.GetProperty("version").GetUInt32(),
            groupName = "Nhóm kiểm thử", heldAt = DateTime.UtcNow, location = "Phòng họp",
            chairName = "Chủ trì họp", secretaryName = "Thư ký họp",
            recordIds = new[] { w.RecordA, w.RecordB },
            attendees = new[] { new { fullName = "Thành viên 1", capacity = "Ủy viên", dissentNote = (string?)null } },
            summary = new[] { new { gradeCode = "HoanThanhTot", notableDifference = "Chênh lệch giữa hai hồ sơ", preliminaryConclusion = "Rà soát lại" } },
            cases = new[]
            {
                new { recordId = w.RecordA, clarification = "Đã đối chiếu minh chứng", gradeAfter = "HoanThanh", scoreAfter = (double?)78.0,
                      basis = "Biên bản kiểm tra thực tế", responsibleName = "Chủ trì họp", responsibleMemberId = (Guid?)null }
            }
        };
        var saved = await Data(await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}", save));
        var tot = saved.GetProperty("summary").EnumerateArray().Single(s => s.GetProperty("gradeCode").GetString() == "HoanThanhTot");
        Assert.Equal(1, tot.GetProperty("countBefore").GetInt32());
        Assert.Equal(0, tot.GetProperty("countAfter").GetInt32());

        // Cùng một người không vừa lập vừa chốt; người chốt thiếu quyền trên hồ sơ bị chặn.
        var version = saved.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Forbidden, (await w.PreparerC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.NobodyC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version = version + 1000 })).StatusCode);

        var concluded = await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version }));
        Assert.Equal("Concluded", concluded.GetProperty("status").GetString());

        // Kết quả sau hiệu chuẩn nằm trên hồ sơ A và là mặc định cho các bước sau; hồ sơ B không đổi.
        var recordA = await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordA}");
        Assert.Equal("HoanThanh", recordA.GetProperty("calibratedGrade").GetString());
        Assert.Equal(78.0, recordA.GetProperty("calibratedScore").GetDouble(), 6);
        Assert.Equal(78.0, recordA.GetProperty("reviewedScore").GetDouble(), 6);
        Assert.Equal(id, recordA.GetProperty("calibrationSessionId").GetGuid());
        var recordB = await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordB}");
        Assert.Equal(string.Empty, recordB.GetProperty("calibratedGrade").GetString());
        var history = await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordA}/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("action").GetString() == "Calibrate");

        // Đã chốt thì khóa sửa; mở lại bắt buộc lý do và hủy kết quả đã áp.
        Assert.Equal(HttpStatusCode.Conflict, (await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}", save)).StatusCode);
        var concludedVersion = concluded.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/reopen", new { version = concludedVersion, reason = " " })).StatusCode);
        var reopened = await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/reopen",
            new { version = concludedVersion, reason = "Bổ sung minh chứng" }));
        Assert.Equal("Draft", reopened.GetProperty("status").GetString());
        Assert.Equal(2, reopened.GetProperty("revision").GetInt32());
        recordA = await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordA}");
        Assert.Equal(string.Empty, recordA.GetProperty("calibratedGrade").GetString());

        var events = await Data(w.PreparerC, $"/api/calibrations/{id}/history");
        Assert.Equal(new[] { "Create", "Conclude", "Apply", "Reopen" }, events.EnumerateArray().Select(e => e.GetProperty("action").GetString()));

        // Hủy bản nháp (có lý do) giải phóng hồ sơ.
        var reopenedVersion = reopened.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.PreparerC.PostAsJsonAsync($"/api/calibrations/{id}/void", new { version = reopenedVersion, reason = "" })).StatusCode);
        await Data(await w.PreparerC.PostAsJsonAsync($"/api/calibrations/{id}/void", new { version = reopenedVersion, reason = "Lập nhầm nhóm" }));
        await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordB)));
    }

    [SkippableFact]
    public async Task Decision_OfOneRecord_DoesNotLockOthers_ReopenAllowedAfterSomeRecordsDecided()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync(decisionReady: true);
        var decider = await _factory.CreateUserWithPermissionsAsync(WorkflowPermissions.Decide, PermissionCodes.EvaluationRead);
        var deciderC = await _factory.LoginAsAsync(decider.Username, decider.Password, distinctClientIp: true);

        var created = await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordB, w.RecordC)));
        var id = created.GetProperty("id").GetGuid();
        object Save(uint version, Guid[] recordIds, object[] cases, string? extra) => new
        {
            version, groupName = "Nhóm kiểm thử", heldAt = DateTime.UtcNow, location = "Phòng họp",
            chairName = "Chủ trì họp", secretaryName = "Thư ký họp", recordIds, extraConclusion = extra,
            attendees = new[] { new { fullName = "Thành viên 1", capacity = "Ủy viên", dissentNote = (string?)null } },
            summary = Array.Empty<object>(), cases
        };
        var changeA = new
        {
            recordId = w.RecordA, clarification = "Đã đối chiếu minh chứng", gradeAfter = "HoanThanh", scoreAfter = (double?)78.0,
            basis = "Biên bản kiểm tra thực tế", responsibleName = "Chủ trì họp", responsibleMemberId = (Guid?)null
        };
        var saved = await Data(await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}",
            Save(created.GetProperty("version").GetUInt32(), new[] { w.RecordA, w.RecordB, w.RecordC }, new object[] { changeA }, null)));
        await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version = saved.GetProperty("version").GetUInt32() }));

        async Task<HttpResponseMessage> Decide(Guid recordId, double score, string grade)
        {
            var record = await Data(deciderC, $"/api/evaluations/records/{recordId}");
            return await deciderC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/decision",
                new { version = record.GetProperty("version").GetUInt32(), finalScore = score, finalGrade = grade, explanation = "Căn cứ kiểm thử" });
        }

        // Quyết định lần lượt từng hồ sơ trong cùng biên bản đều thành công.
        await Data(await Decide(w.RecordA, 78, "HoanThanh"));
        Assert.Equal(CalibrationGateState.Satisfied, (await GateAsync(w.RecordB)).State);
        await Data(await Decide(w.RecordB, 72, "HoanThanh"));
        Assert.Equal(CalibrationGateState.Satisfied, (await GateAsync(w.RecordC)).State);

        // Hồ sơ còn lại bị trả về thẩm định: chỉ cổng của hồ sơ đó khóa.
        await _factory.WithDbAsync(async db =>
        {
            var record = db.Set<EvaluationRecord>().Single(r => r.Id == w.RecordC);
            record.Status = RecordStatus.AwaitingDirectorReview;
            db.Set<EvaluationRecordHistory>().Add(new EvaluationRecordHistory
            {
                RecordId = record.Id, FromStatus = RecordStatus.AwaitingDecision, ToStatus = RecordStatus.AwaitingDirectorReview,
                Step = WorkflowStep.B3C_DIRECTOR, Action = WorkflowAction.Return, Reason = "Bổ sung", ActorName = "Kiểm thử",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
        Assert.Equal(CalibrationGateState.Satisfied, (await GateAsync(w.RecordA)).State);
        Assert.Equal(CalibrationGateState.Satisfied, (await GateAsync(w.RecordB)).State);
        Assert.Equal(CalibrationGateState.Stale, (await GateAsync(w.RecordC)).State);

        // Mở lại được dù hai hồ sơ đã quyết định; kết quả đã áp cho hồ sơ đã quyết định được giữ nguyên.
        var current = await Data(w.ConcluderC, $"/api/calibrations/{id}");
        var reopened = await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/reopen",
            new { version = current.GetProperty("version").GetUInt32(), reason = "Hồ sơ trả lại thẩm định" }));
        Assert.Equal("Draft", reopened.GetProperty("status").GetString());
        Assert.Equal("HoanThanh", (await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordA}")).GetProperty("calibratedGrade").GetString());

        // Còn hồ sơ đã quyết định trong biên bản thì chưa chốt lại được; gỡ chúng ra rồi chốt.
        Assert.Equal(HttpStatusCode.Conflict, (await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude",
            new { version = reopened.GetProperty("version").GetUInt32() })).StatusCode);
        var trimmed = await Data(await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}",
            Save(reopened.GetProperty("version").GetUInt32(), new[] { w.RecordC }, Array.Empty<object>(), "Không có nhóm tương đồng")));
        Assert.Equal(1, trimmed.GetProperty("records").GetArrayLength());
        await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version = trimmed.GetProperty("version").GetUInt32() }));
        Assert.Equal(CalibrationGateState.Satisfied, (await GateAsync(w.RecordC)).State);
    }

    [SkippableFact]
    public async Task RecordOwner_IsRefusedOnEveryWrite()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync();
        var created = await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordB)));
        var id = created.GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        // Chủ hồ sơ A có quyền lập: mọi thao tác ghi trên biên bản chứa hồ sơ của mình đều bị chặn.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerManagerC.PutAsJsonAsync($"/api/calibrations/{id}",
            new { version, groupName = "Đổi", recordIds = new[] { w.RecordA, w.RecordB } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerManagerC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerManagerC.PostAsJsonAsync($"/api/calibrations/{id}/reopen", new { version, reason = "Lý do" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerManagerC.PostAsJsonAsync($"/api/calibrations/{id}/void", new { version, reason = "Lý do" })).StatusCode);
    }

    private async Task<CalibrationGateResult> GateAsync(Guid recordId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICalibrationGate>().EvaluateAsync(recordId);
    }

    #region Hỗ trợ

    private static object Create(World w, params Guid[] recordIds) => new
    {
        periodId = w.PeriodId, departmentId = w.DepartmentId, groupName = "Nhóm kiểm thử", recordIds
    };

    private sealed class World
    {
        public Guid PeriodId, DepartmentId, RecordA, RecordB, RecordC;
        public HttpClient PreparerC = null!, ConcluderC = null!, OwnerManagerC = null!, NobodyC = null!;
    }

    private async Task<World> SetupAsync(bool decisionReady = false)
    {
        var w = new World();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var owners = new List<TestUser>();
        for (var i = 0; i < 3; i++)
            owners.Add(await _factory.CreateUserAsync(fullName: $"HC Cán bộ {i + 1} {suffix}"));
        await _factory.WithDbAsync(async db =>
        {
            var dept = new AdministrativeDepartment { Code = $"HC-P-{suffix}", Name = $"Phòng HC {suffix}" };
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy2, Name = $"Kỳ HC {suffix}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                Settings = SettingsFor(decisionReady), CriteriaSnapshot = CriteriaTestData.Snapshot(collectiveFramework: false)
            };
            db.AddRange(dept, period);
            await db.SaveChangesAsync();
            (w.DepartmentId, w.PeriodId) = (dept.Id, period.Id);

            var grades = new[] { (EvaluationGrade.HoanThanhTot, 85.0), (EvaluationGrade.HoanThanh, 72.0), (EvaluationGrade.HoanThanhTot, 88.0) };
            var records = owners.Select((o, i) => new EvaluationRecord
            {
                PeriodId = period.Id, MemberId = o.Id, DepartmentId = dept.Id, ApprovalAuthority = ApprovalAuthority.CoSo, WeightFrameCode = "K2",
                Status = decisionReady ? RecordStatus.AwaitingDecision : RecordStatus.AwaitingDirectorReview, AppraisalProposedGrade = grades[i].Item1, AppraisalScore = grades[i].Item2,
                AppraisedAt = DateTime.UtcNow.AddHours(-1), SelfScoredAt = DateTime.UtcNow.AddDays(-1), TotalSelfScore = grades[i].Item2
            }).ToList();
            db.AddRange(records);
            await db.SaveChangesAsync();
            (w.RecordA, w.RecordB, w.RecordC) = (records[0].Id, records[1].Id, records[2].Id);
        });

        var preparer = await _factory.CreateUserAsync(fullName: $"HC Người lập {suffix}");
        var concluder = await _factory.CreateUserAsync(fullName: $"HC Người chốt {suffix}");
        var nobody = await _factory.CreateUserAsync(fullName: $"HC Không quyền {suffix}");
        var preparerRole = await _factory.CreateRoleAsync(
            PermissionCodes.EvaluationCalibrationManage, PermissionCodes.EvaluationCalibrationConclude, PermissionCodes.EvaluationRead);
        var concluderRole = await _factory.CreateRoleAsync(PermissionCodes.EvaluationCalibrationConclude, PermissionCodes.EvaluationRead);
        var managerRole = await _factory.CreateRoleAsync(PermissionCodes.EvaluationCalibrationManage, PermissionCodes.EvaluationRead);
        await _factory.AssignAsync(preparer.Id, preparerRole.Id, RoleScopeType.Global, null);
        await _factory.AssignAsync(concluder.Id, concluderRole.Id, RoleScopeType.Global, null);
        await _factory.AssignAsync(owners[0].Id, managerRole.Id, RoleScopeType.Global, null);

        w.PreparerC = await _factory.LoginAsAsync(preparer.Username, preparer.Password, distinctClientIp: true);
        w.ConcluderC = await _factory.LoginAsAsync(concluder.Username, concluder.Password, distinctClientIp: true);
        w.OwnerManagerC = await _factory.LoginAsAsync(owners[0].Username, owners[0].Password, distinctClientIp: true);
        w.NobodyC = await _factory.LoginAsAsync(nobody.Username, nobody.Password, distinctClientIp: true);
        return w;
    }

    /// <summary>Kỳ kiểm thử ghi nhận quyết định tắt cổng kiểm soát để chỉ xét cổng hiệu chuẩn.</summary>
    private static string SettingsFor(bool controlOff)
    {
        var settings = PeriodSettings.Default();
        if (controlOff)
            foreach (var profile in settings.Profiles)
                profile.Gates[WorkflowGates.Control] = new GateSetting { Mode = GateMode.Off };
        return settings.ToJson();
    }

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
