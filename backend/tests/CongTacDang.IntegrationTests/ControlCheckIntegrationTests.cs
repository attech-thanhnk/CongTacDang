using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
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
    public async Task Create_AndAddRecords_OnlyForRecordsAwaitingDecision()
    {
        var w = await WorldAsync();
        var early = await _factory.CreateUserAsync(w.Dept, null, ApprovalAuthority.CoSo, "KS Hồ sơ chưa tới quyết định");
        var periodId = await OpenPeriodAsync(w, early.Id);
        var ready = await RecordAwaitingDecisionAsync(w, periodId, w.Owner.Id);
        var notYet = await RecordOfAsync(w, periodId, early.Id);

        var refused = await w.BothC.PostAsJsonAsync("/api/control-checks", new { periodId, groupName = "Nhóm", recordIds = new[] { notYet } });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("Lập phiếu kiểm soát không thành công: hồ sơ của KS Hồ sơ chưa tới quyết định chưa tới bước chờ quyết định.", await MessageOf(refused));

        var created = await Data(await w.BothC.PostAsJsonAsync("/api/control-checks", new { periodId, groupName = "Nhóm", recordIds = new[] { ready } }));
        var id = created.GetProperty("id").GetGuid();
        var added = await w.BothC.PutAsJsonAsync($"/api/control-checks/{id}",
            new { version = created.GetProperty("version").GetUInt32(), recordIds = new[] { ready, notYet } });
        Assert.Equal(HttpStatusCode.Conflict, added.StatusCode);
        Assert.Contains("chưa tới bước chờ quyết định", await MessageOf(added));
    }

    private static async Task<string> MessageOf(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    [SkippableFact]
    public async Task Lifecycle_SeparationOfDuty_StaleAfterRecordChange_OneCurrentCheckPerRecord()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w);
        var recordId = await RecordAwaitingDecisionAsync(w, periodId, w.Owner.Id);

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

        // Hồ sơ đổi sau xác nhận (trả về thẩm định rồi thẩm định lại) → phiếu hết hiệu lực.
        await Task.Delay(50);
        await _factory.WithDbAsync(async db =>
        {
            db.Set<EvaluationRecordHistory>().Add(new EvaluationRecordHistory
            {
                RecordId = recordId, FromStatus = RecordStatus.AwaitingDecision, ToStatus = RecordStatus.AwaitingAppraisal,
                Step = WorkflowStep.B3B_APPRAISAL, Action = WorkflowAction.Return, Reason = "Bổ sung", ActorName = "Kiểm thử",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        });
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
    public async Task RecordOwner_IsRefusedOnEveryWrite_EvenWithControlPermissions()
    {
        var w = await WorldAsync();
        var other = await _factory.CreateUserAsync(w.Dept, null, ApprovalAuthority.CoSo, "KS Chủ hồ sơ thứ hai");
        var role = await _factory.CreateRoleAsync(PermissionCodes.EvaluationControlPerform, PermissionCodes.EvaluationControlConfirm, PermissionCodes.EvaluationRead);
        await _factory.AssignAsync(other.Id, await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
        await _factory.AssignAsync(other.Id, role.Id, RoleScopeType.Global, null);
        var otherC = await _factory.LoginAsAsync(other.Username, other.Password, distinctClientIp: true);
        var periodId = await OpenPeriodAsync(w, other.Id);
        var own = await RecordAwaitingDecisionAsync(w, periodId, other.Id);
        var foreign = await RecordAwaitingDecisionAsync(w, periodId, w.Owner.Id);

        // Lập phiếu có hồ sơ của chính mình bị chặn dù có quyền lập.
        Assert.Equal(HttpStatusCode.Forbidden, (await otherC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm", recordIds = new[] { own, foreign } })).StatusCode);

        // Phiếu do người khác lập và gửi xác nhận, chứa hồ sơ của người này.
        var created = await Data(await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm chéo", recordIds = new[] { own, foreign } }));
        var id = created.GetProperty("id").GetGuid();
        var allPass = Codes().Select(c => new { itemCode = c, result = "Pass", evidence = (string?)null }).ToList();
        var ready = await Data(await w.BothC.PutAsJsonAsync($"/api/control-checks/{id}",
            new { version = created.GetProperty("version").GetUInt32(), items = allPass, conclusion = "Eligible" }));
        await Data(await Act(w.BothC, id, "submit", ready.GetProperty("version").GetUInt32()));

        // Mọi thao tác ghi đều bị chặn với chủ hồ sơ trong phiếu.
        var version = await VersionOf(w, id);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherC.PutAsJsonAsync($"/api/control-checks/{id}", new { version, groupName = "Đổi" })).StatusCode);
        foreach (var action in new[] { "submit", "confirm", "return", "reopen", "cancel" })
            Assert.Equal(HttpStatusCode.Forbidden, (await Act(otherC, id, action, version, "Lý do")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherC.GetAsync($"/api/control-checks/{id}")).StatusCode);

        // Người khác có quyền vẫn xác nhận bình thường: phiếu không bị hỏng bởi các lần thử trên.
        var confirmed = await Data(await Act(w.ConfirmerC, id, "confirm", version));
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await Act(otherC, id, "reopen", await VersionOf(w, id), "Lý do")).StatusCode);
    }

    [SkippableFact]
    public async Task Decision_OfOneRecord_DoesNotLockOthers_ReturnLocksOnlyThatRecord()
    {
        var w = await WorldAsync();
        var members = new List<TestUser>();
        for (var i = 0; i < 3; i++)
            members.Add(await _factory.CreateUserAsync(w.Dept, null, ApprovalAuthority.CoSo, $"KS Nhóm quyết định {i + 1}"));
        var decider = await _factory.CreateUserWithPermissionsAsync(WorkflowPermissions.Decide, PermissionCodes.EvaluationRead);
        var deciderC = await _factory.LoginAsAsync(decider.Username, decider.Password, distinctClientIp: true);
        var periodId = await OpenPeriodAsync(w, members.Select(m => m.Id).ToArray());
        var records = new List<Guid>();
        foreach (var m in members)
            records.Add(await RecordOfAsync(w, periodId, m.Id));
        await _factory.WithDbAsync(async db =>
        {
            foreach (var record in db.Set<EvaluationRecord>().Where(r => records.Contains(r.Id)))
            {
                record.Status = RecordStatus.AwaitingDecision;
                record.AppraisalProposedGrade = EvaluationGrade.HoanThanhTot;
                record.AppraisalScore = 85;
                record.AppraisedAt = DateTime.UtcNow.AddHours(-1);
                record.SelfScoredAt = DateTime.UtcNow.AddDays(-1);
                record.TotalSelfScore = 85;
            }
            await db.SaveChangesAsync();
        });

        var created = await Data(await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm ba hồ sơ", recordIds = records }));
        var id = created.GetProperty("id").GetGuid();
        var allPass = Codes().Select(c => new { itemCode = c, result = "Pass", evidence = (string?)null }).ToList();
        var ready = await Data(await w.BothC.PutAsJsonAsync($"/api/control-checks/{id}",
            new { version = created.GetProperty("version").GetUInt32(), items = allPass, conclusion = "Eligible" }));
        await Data(await Act(w.BothC, id, "submit", ready.GetProperty("version").GetUInt32()));
        await Data(await Act(w.ConfirmerC, id, "confirm", await VersionOf(w, id)));

        async Task<HttpResponseMessage> Decide(Guid recordId) => await deciderC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/decision",
            new { version = await RecordVersionAsync(w, recordId), finalScore = 85.0, finalGrade = "HoanThanhTot", explanation = "Căn cứ kiểm thử" });

        // Quyết định lần lượt từng hồ sơ trong cùng phiếu đều thành công.
        await Ok(await Decide(records[0]));
        Assert.Equal(ControlGateState.Satisfied, (await GateAsync(records[1])).State);
        await Ok(await Decide(records[1]));
        Assert.False((await Data(w.ConfirmerC, $"/api/control-checks/{id}")).GetProperty("isStale").GetBoolean());

        // Hồ sơ còn lại bị trả về thẩm định: chỉ cổng của hồ sơ đó khóa.
        await _factory.WithDbAsync(async db =>
        {
            var record = db.Set<EvaluationRecord>().Single(r => r.Id == records[2]);
            record.Status = RecordStatus.AwaitingAppraisal;
            db.Set<EvaluationRecordHistory>().Add(new EvaluationRecordHistory
            {
                RecordId = record.Id, FromStatus = RecordStatus.AwaitingDecision, ToStatus = RecordStatus.AwaitingAppraisal,
                Step = WorkflowStep.B3B_APPRAISAL, Action = WorkflowAction.Return, Reason = "Bổ sung", ActorName = "Kiểm thử",
                CreatedAt = DateTime.UtcNow.AddSeconds(1)
            });
            await db.SaveChangesAsync();
        });
        Assert.Equal(ControlGateState.Satisfied, (await GateAsync(records[0])).State);
        Assert.Equal(ControlGateState.Satisfied, (await GateAsync(records[1])).State);
        Assert.Equal(ControlGateState.Stale, (await GateAsync(records[2])).State);
        var detail = await Data(w.ConfirmerC, $"/api/control-checks/{id}");
        Assert.True(detail.GetProperty("isStale").GetBoolean());
        var staleRows = detail.GetProperty("records").EnumerateArray().Where(r => r.GetProperty("isStale").GetBoolean()).ToList();
        Assert.Equal(records[2], Assert.Single(staleRows).GetProperty("recordId").GetGuid());
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

    /// <summary>Tệp đính kèm phiếu kiểm soát: người lập tải lên/thay/gỡ, người xác nhận chỉ xem, chủ hồ sơ trong phiếu bị chặn, gửi/xác nhận hoặc kỳ đóng thì khóa.</summary>
    [SkippableFact]
    public async Task ControlCheck_Attachments_ScopeOwnerAndLocking()
    {
        var w = await WorldAsync();
        const string type = "ControlCheck";
        var member = await _factory.CreateUserAsync(w.Dept, null, ApprovalAuthority.CoSo, "KS Chủ hồ sơ đính kèm");
        var memberRole = (await _factory.CreateRoleAsync(PermissionCodes.EvaluationControlPerform, PermissionCodes.EvaluationControlConfirm, PermissionCodes.EvaluationRead)).Id;
        await _factory.AssignAsync(member.Id, memberRole, RoleScopeType.Global, null);
        var memberC = await _factory.LoginAsAsync(member.Username, member.Password, distinctClientIp: true);

        var periodId = await OpenPeriodAsync(w, member.Id);
        var recordId = await RecordAwaitingDecisionAsync(w, periodId, w.Owner.Id);
        var memberRecord = await RecordAwaitingDecisionAsync(w, periodId, member.Id);
        var created = await Data(await w.BothC.PostAsJsonAsync("/api/control-checks",
            new { periodId, groupName = "Nhóm đính kèm", recordIds = new[] { recordId, memberRecord } }));
        var id = created.GetProperty("id").GetGuid();

        var fileId = await FormAttachmentClient.UploadOkAsync(w.PerformerC, type, id);
        foreach (var client in new[] { w.ConfirmerC, w.OwnerC, memberC })
            Assert.Equal(HttpStatusCode.Forbidden, (await FormAttachmentClient.UploadAsync(client, type, id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await FormAttachmentClient.UploadAsync(w.PerformerC, type, Guid.NewGuid())).StatusCode);

        foreach (var client in new[] { w.PerformerC, w.ConfirmerC, w.BothC })
            await FormAttachmentClient.AssertCanReadAsync(client, type, id, fileId);
        foreach (var client in new[] { w.OwnerC, memberC })
            await FormAttachmentClient.AssertCannotReadAsync(client, type, id, fileId);

        Assert.Equal(HttpStatusCode.Forbidden, (await FormAttachmentClient.ReplaceAsync(w.ConfirmerC, fileId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ConfirmerC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await memberC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        var second = await FormAttachmentClient.UploadOkAsync(w.BothC, type, id);
        Assert.Equal(HttpStatusCode.OK, (await FormAttachmentClient.ReplaceAsync(w.PerformerC, fileId)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.BothC.DeleteAsync($"/api/attachments/{second}")).StatusCode);
        Assert.DoesNotContain(second, await FormAttachmentClient.ListAsync(w.BothC, type, id));

        // Đã gửi xác nhận: khóa tải lên/thay/gỡ, vẫn xem được.
        var items = Codes().Select(c => new { itemCode = c, result = "Pass", evidence = (string?)null }).ToList();
        var ready = await Data(await w.BothC.PutAsJsonAsync($"/api/control-checks/{id}",
            new { version = await VersionOf(w, id), items, conclusion = "Eligible" }));
        var submitted = await Data(await Act(w.BothC, id, "submit", ready.GetProperty("version").GetUInt32()));
        Assert.Equal(HttpStatusCode.Conflict, (await FormAttachmentClient.UploadAsync(w.PerformerC, type, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await FormAttachmentClient.ReplaceAsync(w.PerformerC, fileId)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.PerformerC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        await FormAttachmentClient.AssertCanReadAsync(w.ConfirmerC, type, id, fileId);

        // Đã xác nhận vẫn khóa; mở lại về nháp thì sửa được, nhưng kỳ đóng thì khóa lại.
        var confirmed = await Data(await Act(w.ConfirmerC, id, "confirm", submitted.GetProperty("version").GetUInt32()));
        Assert.Equal(HttpStatusCode.Conflict, (await FormAttachmentClient.UploadAsync(w.PerformerC, type, id)).StatusCode);
        var reopened = await Data(await Act(w.ConfirmerC, id, "reopen", confirmed.GetProperty("version").GetUInt32(), "Bổ sung căn cứ"));
        Assert.Equal("Draft", reopened.GetProperty("status").GetString());
        await FormAttachmentClient.UploadOkAsync(w.PerformerC, type, id);
        await _factory.WithDbAsync(async db =>
        {
            db.Set<EvaluationPeriod>().Single(p => p.Id == periodId).Status = PeriodStatus.Closed;
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Conflict, (await FormAttachmentClient.UploadAsync(w.PerformerC, type, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.PerformerC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        await FormAttachmentClient.AssertCanReadAsync(w.PerformerC, type, id, fileId);
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

    private static async Task<Guid> OpenPeriodAsync(World w, params Guid[] extraMembers)
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
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.Owner.Id }.Concat(extraMembers).ToArray() }));
        var period = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử phiếu kiểm soát", force = true }));
        return periodId;
    }

    /// <summary>Hồ sơ được đưa tới bước chờ quyết định, đúng điều kiện lập phiếu kiểm soát.</summary>
    private async Task<Guid> RecordAwaitingDecisionAsync(World w, Guid periodId, Guid memberId)
    {
        var recordId = await RecordOfAsync(w, periodId, memberId);
        await _factory.WithDbAsync(async db =>
        {
            db.Set<EvaluationRecord>().Single(r => r.Id == recordId).Status = RecordStatus.AwaitingDecision;
            await db.SaveChangesAsync();
        });
        return recordId;
    }

    private static async Task<Guid> RecordOfAsync(World w, Guid periodId, Guid memberId) =>
        (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == memberId).GetProperty("recordId").GetGuid();

    private static async Task<uint> RecordVersionAsync(World w, Guid recordId) =>
        (await Data(w.ManagerC, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();

    private static async Task<uint> VersionOf(World w, Guid checkId) =>
        (await Data(w.ConfirmerC, $"/api/control-checks/{checkId}")).GetProperty("version").GetUInt32();

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
