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

        // Người lập không có quyền chốt trên hồ sơ nào thì chỉ lập được; chủ hồ sơ có quyền lập: hồ sơ của mình tự bỏ, chỉ còn hồ sơ đó thì không lập được.
        var ownerAttempt = await w.OwnerManagerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA));
        Assert.Equal(HttpStatusCode.BadRequest, ownerAttempt.StatusCode);
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

        // Trước đây phải gỡ hồ sơ đã quyết định ra mới chốt lại được; người dùng chấp nhận đề xuất giữ nguyên dòng của họ,
        // chỉ áp kết quả cho hồ sơ đang chờ.
        var keptRows = reopened.GetProperty("records").EnumerateArray().ToList();
        Assert.True(keptRows.Single(r => r.GetProperty("recordId").GetGuid() == w.RecordA).GetProperty("kept").GetBoolean());
        Assert.False(keptRows.Single(r => r.GetProperty("recordId").GetGuid() == w.RecordC).GetProperty("kept").GetBoolean());

        // Lưu lại dù danh sách thiếu hồ sơ đã quyết định và sửa trường hợp của họ: dòng, trường hợp vẫn giữ nguyên.
        var changeAEdited = new
        {
            recordId = w.RecordA, clarification = "Sửa sau quyết định", gradeAfter = "HoanThanhTot", scoreAfter = (double?)90.0,
            basis = "Căn cứ mới", responsibleName = "Người khác", responsibleMemberId = (Guid?)null
        };
        var resaved = await Data(await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}",
            Save(reopened.GetProperty("version").GetUInt32(), new[] { w.RecordC }, new object[] { changeAEdited }, null)));
        Assert.Equal(3, resaved.GetProperty("records").GetArrayLength());
        var keptCase = resaved.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("recordId").GetGuid() == w.RecordA);
        Assert.Equal("HoanThanh", keptCase.GetProperty("gradeAfter").GetString());
        Assert.Equal("Đã đối chiếu minh chứng", keptCase.GetProperty("clarification").GetString());

        var calibrateBefore = (await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordA}/history")).EnumerateArray()
            .Count(h => h.GetProperty("action").GetString() == "Calibrate");
        var reconcluded = await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude",
            new { version = resaved.GetProperty("version").GetUInt32() }));
        Assert.Equal("Concluded", reconcluded.GetProperty("status").GetString());
        Assert.Equal(3, reconcluded.GetProperty("records").GetArrayLength());

        // Hồ sơ đã quyết định không bị áp lại; hồ sơ đang chờ qua cổng.
        var recordA = await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordA}");
        Assert.Equal("HoanThanh", recordA.GetProperty("calibratedGrade").GetString());
        Assert.Equal("AwaitingPublish", recordA.GetProperty("status").GetString());
        Assert.Equal(calibrateBefore, (await Data(w.PreparerC, $"/api/evaluations/records/{w.RecordA}/history")).EnumerateArray()
            .Count(h => h.GetProperty("action").GetString() == "Calibrate"));
        Assert.Equal(CalibrationGateState.Satisfied, (await GateAsync(w.RecordC)).State);
    }

    [SkippableFact]
    public async Task ReopenedRecord_GivesReconcludeTasks_ToManagerAndConcluder_NotToPreparerForConclusion()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync(decisionReady: true);
        var decider = await _factory.CreateUserWithPermissionsAsync(WorkflowPermissions.Decide, PermissionCodes.EvaluationRead);
        var deciderC = await _factory.LoginAsAsync(decider.Username, decider.Password, distinctClientIp: true);

        var created = await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordB, w.RecordC)));
        var id = created.GetProperty("id").GetGuid();
        var saved = await Data(await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}", new
        {
            version = created.GetProperty("version").GetUInt32(), groupName = "Nhóm việc", heldAt = DateTime.UtcNow, location = "Phòng họp",
            chairName = "Chủ trì họp", secretaryName = "Thư ký họp", recordIds = new[] { w.RecordA, w.RecordB, w.RecordC },
            attendees = new[] { new { fullName = "Thành viên 1", capacity = "Ủy viên", dissentNote = (string?)null } },
            summary = Array.Empty<object>(), cases = Array.Empty<object>()
        }));
        await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version = saved.GetProperty("version").GetUInt32() }));

        // Chưa có hồ sơ nào mở lại: không ai có việc với biên bản.
        Assert.Null(await TaskAsync(w.ConcluderC, id));
        Assert.Null(await TaskAsync(w.PreparerC, id));

        foreach (var (recordId, score, grade) in new[] { (w.RecordA, 85.0, "HoanThanhTot"), (w.RecordB, 72.0, "HoanThanh") })
        {
            var record = await Data(deciderC, $"/api/evaluations/records/{recordId}");
            await Data(await deciderC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/decision",
                new { version = record.GetProperty("version").GetUInt32(), finalScore = score, finalGrade = grade, explanation = "Căn cứ" }));
        }

        // Hồ sơ C bị mở lại về thẩm định: còn đang làm lại thì chưa có việc.
        await MoveAsync(w.RecordC, RecordStatus.AwaitingAppraisal, WorkflowAction.Reopen);
        Assert.Null(await TaskAsync(w.ConcluderC, id));

        // Thẩm định lại xong (điểm đổi): người chốt nhận việc mở lại biên bản.
        await MoveAsync(w.RecordC, RecordStatus.AwaitingDecision, WorkflowAction.Complete, appraisalScore: 70, grade: EvaluationGrade.HoanThanh);
        var reopenTask = await TaskAsync(w.ConcluderC, id);
        Assert.NotNull(reopenTask);
        Assert.Equal("ReopenNeeded", reopenTask!.Value.GetProperty("status").GetString());
        Assert.Equal(w.RecordC, reopenTask.Value.GetProperty("recordId").GetGuid());
        var bell = await Data(w.ConcluderC, "/api/notifications/summary");
        Assert.Contains(bell.GetProperty("items").EnumerateArray(), i => i.GetProperty("link").GetString() == $"/quality-control/calibrations/{id}");

        var current = await Data(w.ConcluderC, $"/api/calibrations/{id}");
        await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/reopen",
            new { version = current.GetProperty("version").GetUInt32(), reason = "Hồ sơ mở lại theo kiến nghị" }));

        // Mốc trước của C đã đổi: người có quyền lập nhận việc cập nhật biên bản, người chốt chưa có việc.
        var updateTask = await TaskAsync(w.PreparerC, id);
        Assert.Equal("UpdateNeeded", updateTask?.GetProperty("status").GetString());
        Assert.Null(await TaskAsync(w.ConcluderC, id));

        var draft = await Data(w.PreparerC, $"/api/calibrations/{id}");
        await Data(await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}", new
        {
            version = draft.GetProperty("version").GetUInt32(), groupName = "Nhóm việc", heldAt = DateTime.UtcNow, location = "Phòng họp",
            chairName = "Chủ trì họp", secretaryName = "Thư ký họp", recordIds = new[] { w.RecordA, w.RecordB, w.RecordC },
            attendees = new[] { new { fullName = "Thành viên 1", capacity = "Ủy viên", dissentNote = (string?)null } },
            summary = Array.Empty<object>(), cases = Array.Empty<object>()
        }));

        // Đủ nội dung: người chốt nhận việc chốt lại; người lập (dù có quyền chốt) không nhận.
        var concludeTask = await TaskAsync(w.ConcluderC, id);
        Assert.Equal("ConcludeNeeded", concludeTask?.GetProperty("status").GetString());
        Assert.Null(await TaskAsync(w.PreparerC, id));

        var ready = await Data(w.ConcluderC, $"/api/calibrations/{id}");
        await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version = ready.GetProperty("version").GetUInt32() }));
        Assert.Null(await TaskAsync(w.ConcluderC, id));
        Assert.Null(await TaskAsync(w.PreparerC, id));
        Assert.Equal(CalibrationGateState.Satisfied, (await GateAsync(w.RecordC)).State);
    }

    /// <summary>Việc "Họp thống nhất" của biên bản trong hàng đợi người dùng; null nếu không có.</summary>
    private static async Task<JsonElement?> TaskAsync(HttpClient client, Guid sessionId)
    {
        var queue = await Data(client, "/api/evaluations/work-queue");
        foreach (var group in queue.GetProperty("groups").EnumerateArray().Where(g => g.GetProperty("step").GetString() == CalibrationService.WorkQueueGroup))
            foreach (var item in group.GetProperty("items").EnumerateArray())
                if (item.TryGetProperty("formId", out var formId) && formId.ValueKind == JsonValueKind.String && formId.GetGuid() == sessionId)
                    return item;
        return null;
    }

    /// <summary>Đưa hồ sơ sang trạng thái khác và ghi lịch sử như luồng thật.</summary>
    private async Task MoveAsync(Guid recordId, RecordStatus to, WorkflowAction action, double? appraisalScore = null, EvaluationGrade? grade = null)
    {
        await _factory.WithDbAsync(async db =>
        {
            var record = db.Set<EvaluationRecord>().Single(r => r.Id == recordId);
            var from = record.Status;
            record.Status = to;
            if (appraisalScore is { } score)
                record.AppraisalScore = score;
            if (grade is { } g)
                record.AppraisalProposedGrade = g;
            db.Set<EvaluationRecordHistory>().Add(new EvaluationRecordHistory
            {
                RecordId = record.Id, FromStatus = from, ToStatus = to, Step = WorkflowStep.B3B_APPRAISAL, Action = action,
                Reason = "Kiểm thử", ActorName = "Kiểm thử", CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
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

    [SkippableFact]
    public async Task Create_WithoutUnit_TakesCommonUnitOfRecords_AsTheUiDoes()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync();

        // Giao diện chỉ gửi kỳ, tên nhóm và hồ sơ: đơn vị lấy từ các hồ sơ đã chọn.
        var created = await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations",
            new { periodId = w.PeriodId, groupName = "Nhóm không chọn đơn vị", recordIds = new[] { w.RecordA, w.RecordB } }));
        Assert.Equal(w.DepartmentId, created.GetProperty("departmentId").GetGuid());

        // Thiếu tên nhóm vẫn bị từ chối rõ ràng.
        var noName = await w.PreparerC.PostAsJsonAsync("/api/calibrations",
            new { periodId = w.PeriodId, recordIds = new[] { w.RecordC } });
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
    }

    /// <summary>Tệp đính kèm biên bản hiệu chuẩn: người lập tải lên/thay/gỡ, người chỉ chốt chỉ xem, chủ hồ sơ bị chặn, chốt hoặc kỳ đóng thì khóa.</summary>
    [SkippableFact]
    public async Task Calibration_Attachments_ScopeOwnerAndLocking()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync();
        const string type = "CalibrationSession";
        var created = await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA, w.RecordB)));
        var id = created.GetProperty("id").GetGuid();

        // Tải lên: người có quyền lập; người chỉ chốt, chủ hồ sơ trong biên bản (dù có quyền lập), người không quyền → 403.
        var fileId = await FormAttachmentClient.UploadOkAsync(w.PreparerC, type, id);
        foreach (var client in new[] { w.ConcluderC, w.OwnerManagerC, w.NobodyC })
            Assert.Equal(HttpStatusCode.Forbidden, (await FormAttachmentClient.UploadAsync(client, type, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FormAttachmentClient.UploadAsync(w.PreparerC, type, Guid.NewGuid())).StatusCode);

        Assert.True((await Data(w.PreparerC, $"/api/calibrations/{id}")).GetProperty("canAttach").GetBoolean());
        Assert.False((await Data(w.ConcluderC, $"/api/calibrations/{id}")).GetProperty("canAttach").GetBoolean());
        foreach (var client in new[] { w.PreparerC, w.ConcluderC })
            await FormAttachmentClient.AssertCanReadAsync(client, type, id, fileId);
        foreach (var client in new[] { w.OwnerManagerC, w.NobodyC })
            await FormAttachmentClient.AssertCannotReadAsync(client, type, id, fileId);

        Assert.Equal(HttpStatusCode.Forbidden, (await FormAttachmentClient.ReplaceAsync(w.ConcluderC, fileId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ConcluderC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerManagerC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        var second = await FormAttachmentClient.UploadOkAsync(w.PreparerC, type, id);
        Assert.Equal(HttpStatusCode.OK, (await FormAttachmentClient.ReplaceAsync(w.PreparerC, fileId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.PreparerC.DeleteAsync($"/api/attachments/{second}")).StatusCode);

        // Đã chốt: khóa; mở lại thì sửa được; kỳ đóng thì khóa lại.
        var save = new
        {
            version = created.GetProperty("version").GetUInt32(), groupName = "Nhóm kiểm thử", heldAt = DateTime.UtcNow, location = "Phòng họp",
            chairName = "Chủ trì họp", secretaryName = "Thư ký họp", recordIds = new[] { w.RecordA, w.RecordB },
            attendees = new[] { new { fullName = "Thành viên 1", capacity = "Ủy viên", dissentNote = (string?)null } },
            summary = new[] { new { gradeCode = "HoanThanhTot", notableDifference = "Chênh lệch", preliminaryConclusion = "Rà soát lại" } },
            cases = new[]
            {
                new { recordId = w.RecordA, clarification = "Đã đối chiếu", gradeAfter = "HoanThanh", scoreAfter = (double?)78.0,
                      basis = "Biên bản kiểm tra", responsibleName = "Chủ trì họp", responsibleMemberId = (Guid?)null }
            }
        };
        var saved = await Data(await w.PreparerC.PutAsJsonAsync($"/api/calibrations/{id}", save));
        var concluded = await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/conclude", new { version = saved.GetProperty("version").GetUInt32() }));
        Assert.Equal(HttpStatusCode.Conflict, (await FormAttachmentClient.UploadAsync(w.PreparerC, type, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await FormAttachmentClient.ReplaceAsync(w.PreparerC, fileId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.PreparerC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        await FormAttachmentClient.AssertCanReadAsync(w.ConcluderC, type, id, fileId);

        await Data(await w.ConcluderC.PostAsJsonAsync($"/api/calibrations/{id}/reopen",
            new { version = concluded.GetProperty("version").GetUInt32(), reason = "Bổ sung căn cứ" }));
        await FormAttachmentClient.UploadOkAsync(w.PreparerC, type, id);
        await _factory.WithDbAsync(async db =>
        {
            db.Set<EvaluationPeriod>().Single(p => p.Id == w.PeriodId).Status = PeriodStatus.Closed;
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Conflict, (await FormAttachmentClient.UploadAsync(w.PreparerC, type, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.PreparerC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        await FormAttachmentClient.AssertCanReadAsync(w.PreparerC, type, id, fileId);
    }

    [SkippableFact]
    public async Task OwnRecord_LeftOutSilently_WhenPreparerPicksWholeGroup_StillVisibleToOthers()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync();

        // Danh sách chọn của người có hồ sơ trong nhóm không có hồ sơ của họ; người khác vẫn thấy.
        var mine = await Data(w.OwnerManagerC, $"/api/calibrations/candidates?periodId={w.PeriodId}");
        Assert.DoesNotContain(mine.EnumerateArray(), c => c.GetProperty("recordId").GetGuid() == w.RecordA);
        Assert.Contains(mine.EnumerateArray(), c => c.GetProperty("recordId").GetGuid() == w.RecordB);
        var others = await Data(w.PreparerC, $"/api/calibrations/candidates?periodId={w.PeriodId}");
        Assert.Contains(others.EnumerateArray(), c => c.GetProperty("recordId").GetGuid() == w.RecordA);

        // Chọn cả nhóm (kể cả hồ sơ của mình): lập được, hồ sơ của mình tự bỏ.
        var created = await Data(await w.OwnerManagerC.PostAsJsonAsync("/api/calibrations", new
        {
            periodId = w.PeriodId, groupName = "Cả nhóm", recordIds = new[] { w.RecordA, w.RecordB, w.RecordC }
        }));
        var ids = created.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("recordId").GetGuid()).ToList();
        Assert.Equal(new[] { w.RecordB, w.RecordC }.OrderBy(x => x), ids.OrderBy(x => x));

        // Sửa biên bản thêm lại hồ sơ của mình: cũng tự bỏ.
        var id = created.GetProperty("id").GetGuid();
        var saved = await Data(await w.OwnerManagerC.PutAsJsonAsync($"/api/calibrations/{id}", new
        {
            version = created.GetProperty("version").GetUInt32(), groupName = "Cả nhóm", recordIds = new[] { w.RecordA, w.RecordB }
        }));
        Assert.Equal(new[] { w.RecordB }, saved.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("recordId").GetGuid()));

        // Hồ sơ đó vẫn vào biên bản của người khác bình thường.
        var other = await Data(await w.PreparerC.PostAsJsonAsync("/api/calibrations", Create(w, w.RecordA)));
        Assert.Single(other.GetProperty("records").EnumerateArray());
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
