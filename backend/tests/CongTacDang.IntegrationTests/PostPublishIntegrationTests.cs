using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 20 — sau công bố trên PostgreSQL thật: công khai kết quả theo phạm vi (P1), kiến nghị + xung đột lợi ích + mở lại theo
/// kiến nghị (P2), kế hoạch 30-60-90 ngày + xuất Mẫu 17 (P3), nhắc việc (P4). Kỳ tạo qua API (hồ sơ luồng thật), hồ sơ được đưa
/// thẳng tới "Đã công bố" trong CSDL (luồng 9 bước có test riêng).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class PostPublishIntegrationTests
{
    private const string SecretComment = "Ý kiến thẩm định nội bộ không công khai";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private readonly ApiFactory _factory;

    public PostPublishIntegrationTests(ApiFactory factory) => _factory = factory;

    #region P1 — công khai kết quả (T-86)

    [SkippableFact]
    public async Task P1_Results_OnlyGradeWithinAssignmentScope_NoRecordDetails()
    {
        var w = await WorldAsync();

        // Phạm vi Phòng 1: chỉ hồ sơ đã công bố của Phòng 1 (không có hồ sơ chưa công bố, không có Phòng 2).
        var dept = await Data(w.DeptViewerC, $"/api/results?periodId={w.PeriodId}");
        var deptItems = dept.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { w.OwnerA.Id }, deptItems.Select(i => MemberOf(w, i)).ToArray());
        var a = deptItems[0];
        Assert.Equal("HoanThanh", a.GetProperty("finalGrade").GetString());
        Assert.Equal("Hoàn thành nhiệm vụ", a.GetProperty("finalGradeName").GetString());
        Assert.Equal(JsonValueKind.Null, a.GetProperty("finalScore").ValueKind); // mặc định chỉ công khai mức
        Assert.Equal(JsonValueKind.Null, a.GetProperty("recordId").ValueKind); // không có quyền xem hồ sơ
        Assert.Equal("Trưởng phòng thử nghiệm", a.GetProperty("positionTitle").GetString());
        Assert.False(dept.GetProperty("showsScores").GetBoolean());

        // Không lộ chi tiết hồ sơ: ý kiến, điểm tự chấm, minh chứng.
        var raw = await (await w.DeptViewerC.GetAsync($"/api/results?periodId={w.PeriodId}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(SecretComment, raw);
        Assert.DoesNotContain("totalSelfScore", raw);
        Assert.DoesNotContain("appraisal", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tasks", raw);

        // Phạm vi Toàn công ty (vai trò mặc định "Người được đánh giá"): mọi hồ sơ đã công bố; hồ sơ của mình có Id để mở.
        var all = (await Data(w.OwnerAC, $"/api/results?periodId={w.PeriodId}")).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { w.OwnerA.Id, w.OwnerB.Id }.OrderBy(x => x), all.Select(i => MemberOf(w, i)).OrderBy(x => x));
        var own = all.Single(i => MemberOf(w, i) == w.OwnerA.Id);
        Assert.True(own.GetProperty("isOwn").GetBoolean());
        Assert.Equal(w.RecordA, own.GetProperty("recordId").GetGuid());
        Assert.Equal(JsonValueKind.Null, all.Single(i => MemberOf(w, i) == w.OwnerB.Id).GetProperty("recordId").ValueKind);

        // Lọc theo mức, theo đơn vị.
        var good = (await Data(w.OwnerAC, $"/api/results?periodId={w.PeriodId}&grade=HoanThanhTot")).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { w.OwnerB.Id }, good.Select(i => MemberOf(w, i)).ToArray());
        var byDept = (await Data(w.OwnerAC, $"/api/results?periodId={w.PeriodId}&departmentId={w.Dept1}")).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(new[] { w.OwnerA.Id }, byDept.Select(i => MemberOf(w, i)).ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await w.OwnerAC.GetAsync($"/api/results?grade=Sai")).StatusCode);

        // Không có quyền xem kết quả → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.PlainC.GetAsync("/api/results")).StatusCode);

        // Bộ tiêu chí của kỳ bật công khai điểm → có điểm.
        await _factory.WithDbAsync(async db =>
        {
            var period = await db.EvaluationPeriods.FirstAsync(p => p.Id == w.PeriodId);
            var snapshot = CriteriaSnapshot.Parse(period.CriteriaSnapshot)!;
            snapshot.Content.Parameters.PublishScores = true;
            period.CriteriaSnapshot = snapshot.ToJson();
            await db.SaveChangesAsync();
        });
        try
        {
            var scored = await Data(w.DeptViewerC, $"/api/results?periodId={w.PeriodId}");
            Assert.Equal(72.5, scored.GetProperty("items")[0].GetProperty("finalScore").GetDouble(), 6);
            Assert.True(scored.GetProperty("showsScores").GetBoolean());
        }
        finally
        {
            await _factory.WithDbAsync(async db =>
            {
                var period = await db.EvaluationPeriods.FirstAsync(p => p.Id == w.PeriodId);
                var snapshot = CriteriaSnapshot.Parse(period.CriteriaSnapshot)!;
                snapshot.Content.Parameters.PublishScores = false;
                period.CriteriaSnapshot = snapshot.ToJson();
                await db.SaveChangesAsync();
            });
        }
    }

    #endregion

    #region P2 — kiến nghị (T-87)

    [SkippableFact]
    public async Task P2_Appeal_SubmitReviewResolveWithBasis_ConflictBlocked_UnderReviewLabel_ReopenLinked()
    {
        var w = await WorldAsync();
        var recordId = w.RecordAppeal;

        var block = await Data(w.OwnerAC, $"/api/evaluations/records/{recordId}/appeals");
        Assert.True(block.GetProperty("canSubmit").GetBoolean());
        Assert.False(block.GetProperty("underReview").GetBoolean());
        Assert.Contains(block.GetProperty("stepOptions").EnumerateArray(), s => s.GetProperty("step").GetString() == "B3B_APPRAISAL");

        // Hồ sơ chưa công bố → 409; người khác gửi trên hồ sơ không phải của mình → 403; thiếu nội dung → 400.
        Assert.Equal(HttpStatusCode.Conflict, (await w.OwnerCC.PostAsJsonAsync($"/api/evaluations/records/{w.RecordC}/appeals",
            new { content = "Kiến nghị khi chưa công bố" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerBC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/appeals",
            new { content = "Kiến nghị hộ" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.OwnerAC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/appeals",
            new { content = "  " })).StatusCode);

        var appeal = await Data(await w.OwnerAC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/appeals",
            new { content = "Đề nghị xem xét lại điểm trục 1: minh chứng bổ sung kèm theo.", concernedSteps = new[] { "B3B_APPRAISAL" } }));
        var appealId = appeal.GetProperty("id").GetGuid();
        Assert.Equal("Submitted", appeal.GetProperty("status").GetString());
        Assert.False(appeal.GetProperty("canResolve").GetBoolean()); // chủ hồ sơ không tự xử lý

        // Không trùng khi đang xử lý.
        Assert.Equal(HttpStatusCode.Conflict, (await w.OwnerAC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/appeals",
            new { content = "Kiến nghị thứ hai" })).StatusCode);

        // Nhãn "đang xem xét" trên hồ sơ và trên danh sách kết quả.
        Assert.True((await Data(w.OwnerAC, $"/api/evaluations/records/{recordId}/appeals")).GetProperty("underReview").GetBoolean());
        var results = (await Data(w.OwnerAC, $"/api/results?periodId={w.PeriodAppealId}")).GetProperty("items").EnumerateArray();
        Assert.True(results.Single(i => i.GetProperty("recordId").ValueKind != JsonValueKind.Null && i.GetProperty("recordId").GetGuid() == recordId)
            .GetProperty("underReview").GetBoolean());

        // Tệp kèm kiến nghị gắn đối tượng EvaluationAppeal: chủ hồ sơ tải lên, người xử lý (xem được hồ sơ) xem được.
        using (var form = new MultipartFormDataContent())
        {
            form.Add(new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4 minh chung")), "file", "minh-chung.pdf");
            form.Add(new StringContent("KN"), "formCode");
            form.Add(new StringContent("EvaluationAppeal"), "ownerType");
            form.Add(new StringContent(appealId.ToString()), "ownerId");
            await Data(await w.OwnerAC.PostAsync("/api/attachments/upload", form));
        }
        var files = await Data(w.ResolverC, $"/api/attachments?ownerType=EvaluationAppeal&ownerId={appealId}");
        Assert.Single(files.EnumerateArray());
        Assert.Empty((await Data(w.PlainC, $"/api/attachments?ownerType=EvaluationAppeal&ownerId={appealId}")).EnumerateArray());

        // Hàng đợi: người xử lý thấy nhóm "Kiến nghị chờ xử lý"; người có xung đột lợi ích (đã thẩm định) không thấy.
        Assert.Contains(await QueueRecordsAsync(w.ResolverC, "APPEALS"), id => id == recordId);
        Assert.DoesNotContain(await QueueRecordsAsync(w.ConflictedC, "APPEALS"), id => id == recordId);
        Assert.Equal(1, (await Data(w.ResolverC, "/api/notifications/summary")).GetProperty("appeals").GetInt32());

        // Người đã thẩm định hồ sơ (kiến nghị liên quan tới bước thẩm định) bị chặn xử lý.
        var version = appeal.GetProperty("version").GetUInt32();
        var conflicted = await w.ConflictedC.PostAsJsonAsync($"/api/appeals/{appealId}/start-review", new { version });
        Assert.Equal(HttpStatusCode.Forbidden, conflicted.StatusCode);
        Assert.Contains("xung đột lợi ích", await MessageAsync(conflicted));
        var conflictedView = (await Data(w.ConflictedC, $"/api/evaluations/records/{recordId}/appeals")).GetProperty("appeals")[0];
        Assert.False(conflictedView.GetProperty("canResolve").GetBoolean());
        Assert.Contains("xung đột lợi ích", conflictedView.GetProperty("resolveBlockedReason").GetString());
        // Chủ hồ sơ không có quyền xử lý → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OwnerAC.PostAsJsonAsync($"/api/appeals/{appealId}/start-review", new { version })).StatusCode);

        // Người có quyền, không xung đột: nhận xem xét → trả lời bắt buộc căn cứ.
        var review = await Data(await w.ResolverC.PostAsJsonAsync($"/api/appeals/{appealId}/start-review", new { version }));
        Assert.Equal("UnderReview", review.GetProperty("status").GetString());
        version = review.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await w.ResolverC.PostAsJsonAsync($"/api/appeals/{appealId}/start-review", new { version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ResolverC.PostAsJsonAsync($"/api/appeals/{appealId}/resolve",
            new { version, accepted = true, response = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.ResolverC.PostAsJsonAsync($"/api/appeals/{appealId}/resolve",
            new { version = version + 1000, accepted = true, response = "Căn cứ" })).StatusCode);

        var resolved = await Data(await w.ResolverC.PostAsJsonAsync($"/api/appeals/{appealId}/resolve",
            new { version, accepted = true, response = "Chấp nhận: minh chứng bổ sung hợp lệ theo HD03 mục II.1." }));
        Assert.Equal("Accepted", resolved.GetProperty("status").GetString());
        Assert.Equal("Chấp nhận: minh chứng bổ sung hợp lệ theo HD03 mục II.1.", resolved.GetProperty("response").GetString());
        Assert.True(resolved.GetProperty("canReopen").GetBoolean());

        // Nhãn tắt sau khi trả lời; gửi kiến nghị mới được lại.
        var after = await Data(w.OwnerAC, $"/api/evaluations/records/{recordId}/appeals");
        Assert.False(after.GetProperty("underReview").GetBoolean());
        Assert.True(after.GetProperty("canSubmit").GetBoolean());
        Assert.Equal(0, (await Data(w.ResolverC, "/api/notifications/summary")).GetProperty("appeals").GetInt32());

        // Chấp nhận → mở lại (không tự động) bằng thao tác "Mở lại" hiện có; lý do dẫn chiếu kiến nghị trong lịch sử hồ sơ.
        var record = await Data(w.ResolverC, $"/api/evaluations/records/{recordId}");
        Assert.Equal("Published", record.GetProperty("status").GetString());
        var reopened = await Data(await w.ResolverC.PostAsJsonAsync($"/api/appeals/{appealId}/reopen", new
        {
            recordVersion = record.GetProperty("version").GetUInt32(),
            targetStep = "B4_DECISION",
            reason = "Đính chính mức xếp loại theo kiến nghị"
        }));
        Assert.NotEqual(JsonValueKind.Null, reopened.GetProperty("reopenedAt").ValueKind);
        Assert.False(reopened.GetProperty("canReopen").GetBoolean());
        Assert.Equal("AwaitingDecision", (await Data(w.ResolverC, $"/api/evaluations/records/{recordId}")).GetProperty("status").GetString());
        var history = await Data(w.ResolverC, $"/api/evaluations/records/{recordId}/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("action").GetString() == "Reopen"
            && h.GetProperty("reason").GetString()!.Contains("Theo kiến nghị") && h.GetProperty("reason").GetString()!.Contains(appealId.ToString()));
        Assert.Equal(HttpStatusCode.Conflict, (await w.ResolverC.PostAsJsonAsync($"/api/appeals/{appealId}/reopen", new
        {
            recordVersion = record.GetProperty("version").GetUInt32(), targetStep = "B4_DECISION", reason = "Lần 2"
        })).StatusCode);
    }

    #endregion

    #region P3 — kế hoạch 30-60-90 ngày, Mẫu 17 (T-88)

    [SkippableFact]
    public async Task P3_ImprovementPlan_RequiredForGradeC_CreateApproveAcknowledgeResults_ExportMau17()
    {
        var w = await WorldAsync();
        var recordId = w.RecordA;

        var block = await Data(w.ManagerC, $"/api/evaluations/records/{recordId}/improvement-plan");
        Assert.True(block.GetProperty("required").GetBoolean());
        Assert.True(block.GetProperty("canManage").GetBoolean());
        Assert.Equal(JsonValueKind.Null, block.GetProperty("plan").ValueKind);
        // Hồ sơ Hoàn thành tốt → không bắt buộc.
        Assert.False((await Data(w.OwnerBC, $"/api/evaluations/records/{w.RecordB}/improvement-plan")).GetProperty("required").GetBoolean());

        // Cảnh báo: hồ sơ bắt buộc chưa có kế hoạch nằm trong hàng đợi của thủ trưởng đơn vị (phạm vi Phòng 1), không của Phòng 2.
        Assert.Contains(await QueueRecordsAsync(w.ManagerC, "IMPROVEMENT_PLANS"), id => id == recordId);
        Assert.DoesNotContain(await QueueRecordsAsync(w.OutsideManagerC, "IMPROVEMENT_PLANS"), id => id == recordId);
        Assert.True((await Data(w.ManagerC, "/api/notifications/summary")).GetProperty("improvementPlans").GetInt32() >= 1);

        // Ngoài phạm vi → 403; tự lập kế hoạch cho hồ sơ của mình → 403 (xung đột lợi ích).
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OutsideManagerC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/improvement-plan", new { })).StatusCode);
        var selfPlan = await w.OwnerBC.PostAsJsonAsync($"/api/evaluations/records/{w.RecordB}/improvement-plan", new { });
        Assert.Equal(HttpStatusCode.Forbidden, selfPlan.StatusCode);
        Assert.Contains("chính mình", await MessageAsync(selfPlan));

        // Lập (chưa đủ) → duyệt bị chặn → sửa đủ → duyệt.
        var plan = await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/improvement-plan", new
        {
            supporterName = "Người hỗ trợ thử nghiệm",
            milestones = new[] { new { code = "M30", limitation = "Chậm tiến độ văn bản", target = "Hoàn thành văn bản tồn đọng" } }
        }));
        var planId = plan.GetProperty("id").GetGuid();
        Assert.Equal("Draft", plan.GetProperty("status").GetString());
        Assert.Equal(3, plan.GetProperty("milestones").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await w.ManagerC.PostAsJsonAsync($"/api/evaluations/records/{recordId}/improvement-plan", new { })).StatusCode);
        var incomplete = await w.ManagerC.PostAsJsonAsync($"/api/improvement-plans/{planId}/approve", new { version = plan.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("Mốc 60 ngày", await MessageAsync(incomplete));

        plan = await Data(await w.ManagerC.PutAsJsonAsync($"/api/improvement-plans/{planId}", new
        {
            version = plan.GetProperty("version").GetUInt32(),
            supporterName = "Người hỗ trợ thử nghiệm",
            supporterTitle = "Phó Trưởng phòng",
            startDate = "2026-10-01",
            milestones = new[] { "M30", "M60", "M90" }.Select(code => new
            {
                code, limitation = $"Hạn chế {code}", target = $"Mục tiêu {code}", measures = $"Biện pháp {code}", coordination = $"Phối hợp {code}"
            })
        }));
        plan = await Data(await w.ManagerC.PostAsJsonAsync($"/api/improvement-plans/{planId}/approve", new { version = plan.GetProperty("version").GetUInt32() }));
        Assert.Equal("Approved", plan.GetProperty("status").GetString());
        Assert.Equal("2026-12-30", plan.GetProperty("milestones")[2].GetProperty("dueDate").GetString());
        Assert.DoesNotContain(await QueueRecordsAsync(w.ManagerC, "IMPROVEMENT_PLANS"), id => id == recordId);
        Assert.Equal(HttpStatusCode.Conflict, (await w.ManagerC.PutAsJsonAsync($"/api/improvement-plans/{planId}", new
        {
            version = plan.GetProperty("version").GetUInt32(), supporterName = "Sửa sau duyệt"
        })).StatusCode);

        // Chủ hồ sơ xác nhận (người khác không xác nhận được).
        Assert.Contains(await QueueRecordsAsync(w.OwnerAC, "IMPROVEMENT_PLAN_ACK"), id => id == recordId);
        Assert.Equal(1, (await Data(w.OwnerAC, "/api/notifications/summary")).GetProperty("plansToAcknowledge").GetInt32());
        Assert.True((await Data(w.OwnerAC, $"/api/evaluations/records/{recordId}/improvement-plan")).GetProperty("canAcknowledge").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ManagerC.PostAsJsonAsync($"/api/improvement-plans/{planId}/acknowledge",
            new { version = plan.GetProperty("version").GetUInt32() })).StatusCode);
        plan = await Data(await w.OwnerAC.PostAsJsonAsync($"/api/improvement-plans/{planId}/acknowledge",
            new { version = plan.GetProperty("version").GetUInt32(), comment = "Cam kết khắc phục" }));
        Assert.Equal("Acknowledged", plan.GetProperty("status").GetString());

        // Kết quả mốc; mốc 90 ngày đóng kế hoạch.
        plan = await Data(await w.ManagerC.PostAsJsonAsync($"/api/improvement-plans/{planId}/milestone-result",
            new { version = plan.GetProperty("version").GetUInt32(), milestone = "M30", result = "Achieved", note = "Đã xử lý tồn đọng" }));
        Assert.Equal("Đạt yêu cầu", plan.GetProperty("milestones")[0].GetProperty("resultName").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerC.PostAsJsonAsync($"/api/improvement-plans/{planId}/milestone-result",
            new { version = plan.GetProperty("version").GetUInt32(), milestone = "M45", result = "Achieved" })).StatusCode);
        plan = await Data(await w.ManagerC.PostAsJsonAsync($"/api/improvement-plans/{planId}/milestone-result",
            new { version = plan.GetProperty("version").GetUInt32(), milestone = "M90", result = "NotAchieved" }));
        Assert.Equal("Closed", plan.GetProperty("status").GetString());
        Assert.Equal("Không đạt (Xem xét nhân sự)", plan.GetProperty("milestones")[2].GetProperty("resultName").GetString());

        // Xuất Mẫu 17: chủ hồ sơ được; người chỉ xem kết quả công khai không được.
        var export = await w.OwnerAC.GetAsync($"/api/improvement-plans/{planId}/mau-17");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", export.Content.Headers.ContentType?.MediaType);
        using (var doc = WordprocessingDocument.Open(new MemoryStream(await export.Content.ReadAsByteArrayAsync()), false))
        {
            var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.Contains("KẾ HOẠCH HỖ TRỢ, KHẮC PHỤC VÀ PHÁT TRIỂN 30-60-90 NGÀY", text);
            Assert.Contains(w.OwnerAName, text);
            Assert.Contains("Người hỗ trợ thử nghiệm", text);
            Assert.Contains("Hạn chế M60", text);
            Assert.Contains("Hoàn thành nhiệm vụ - Mức C", text);
            Assert.Equal(2, text.Count(c => c == '☒'));
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await w.DeptViewerC.GetAsync($"/api/improvement-plans/{planId}/mau-17")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.OwnerAC.GetAsync($"/api/improvement-plans/{planId}/mau-17?format=xls")).StatusCode);
    }

    #endregion

    #region P4 — nhắc việc (T-89)

    [SkippableFact]
    public async Task P4_NotificationSummary_RequiresLogin_CountsWorkQueue()
    {
        var w = await WorldAsync();
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications/summary")).StatusCode);

        // Chủ hồ sơ C đang ở bước tự chấm (hồ sơ luồng kỳ chuyển tiếp) → 1 việc; tổng = hàng đợi.
        var summary = await Data(w.OwnerCC, "/api/notifications/summary");
        var queue = await Data(w.OwnerCC, "/api/evaluations/work-queue");
        Assert.Equal(queue.GetProperty("total").GetInt32(), summary.GetProperty("total").GetInt32());
        Assert.True(summary.GetProperty("pendingSteps").GetInt32() >= 1);
        Assert.Equal(2, summary.GetProperty("dueSoonDays").GetInt32());
        Assert.Contains(summary.GetProperty("items").EnumerateArray(), i => i.GetProperty("link").GetString() == $"/evaluations/{w.RecordC}");

        // Người không có việc gì → 0.
        var plain = await Data(w.PlainC, "/api/notifications/summary");
        Assert.Equal(0, plain.GetProperty("total").GetInt32());
        Assert.Empty(plain.GetProperty("items").EnumerateArray());
    }

    #endregion

    #region Hỗ trợ

    private static Guid MemberOf(World w, JsonElement item) =>
        item.GetProperty("fullName").GetString() == w.OwnerAName ? w.OwnerA.Id
        : item.GetProperty("fullName").GetString() == w.OwnerBName ? w.OwnerB.Id
        : Guid.Empty;

    private static async Task<List<Guid>> QueueRecordsAsync(HttpClient client, string group)
    {
        var queue = await Data(client, "/api/evaluations/work-queue");
        return queue.GetProperty("groups").EnumerateArray()
            .Where(g => g.GetProperty("step").GetString() == group)
            .SelectMany(g => g.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("recordId").GetGuid()))
            .ToList();
    }

    private async Task<World> WorldAsync()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        await Gate.WaitAsync();
        try
        {
            if (!Worlds.TryGetValue(_factory.DatabaseName, out var world))
            {
                world = await World.CreateAsync(_factory);
                Worlds[_factory.DatabaseName] = world;
            }
            return world;
        }
        finally
        {
            Gate.Release();
        }
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

    /// <summary>
    /// Hai Phòng/Chi bộ; kỳ chuyển tiếp mở qua API; hồ sơ A (Phòng 1, Hoàn thành nhiệm vụ — Mức C), B (Phòng 2, Hoàn thành tốt) đã
    /// công bố, C (Phòng 1) chưa công bố; kỳ thứ hai cho kiến nghị (hồ sơ của A, đã công bố, có người thẩm định = người có xung đột).
    /// </summary>
    private sealed class World
    {
        public Guid Dept1, Dept2, Cell1, Cell2, PeriodId, PeriodAppealId, RecordA, RecordB, RecordC, RecordAppeal;
        public TestUser OwnerA = null!, OwnerB = null!, OwnerC = null!;
        public string OwnerAName = string.Empty, OwnerBName = string.Empty;
        public HttpClient OwnerAC = null!, OwnerBC = null!, OwnerCC = null!, DeptViewerC = null!, ResolverC = null!, ConflictedC = null!;
        public HttpClient ManagerC = null!, OutsideManagerC = null!, PlainC = null!;

        public static async Task<World> CreateAsync(ApiFactory factory)
        {
            var w = new World();
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            await factory.WithDbAsync(async db =>
            {
                var d1 = new AdministrativeDepartment { Code = $"PP-P1-{suffix}", Name = $"Phòng KQ 1 {suffix}" };
                var d2 = new AdministrativeDepartment { Code = $"PP-P2-{suffix}", Name = $"Phòng KQ 2 {suffix}" };
                var c1 = new PartyCell { Code = $"PP-C1-{suffix}", Name = $"Chi bộ KQ 1 {suffix}" };
                var c2 = new PartyCell { Code = $"PP-C2-{suffix}", Name = $"Chi bộ KQ 2 {suffix}" };
                db.AddRange(d1, d2, c1, c2);
                await db.SaveChangesAsync();
                (w.Dept1, w.Dept2, w.Cell1, w.Cell2) = (d1.Id, d2.Id, c1.Id, c2.Id);
            });

            async Task<Guid> Role(string code) => await factory.GetRoleIdAsync(code);
            var evaluatee = await Role("NGUOI_DUOC_DANH_GIA");

            w.OwnerAName = $"KQ Chủ hồ sơ A {suffix}";
            w.OwnerBName = $"KQ Chủ hồ sơ B {suffix}";
            w.OwnerA = await factory.CreateUserAsync(w.Dept1, w.Cell1, ApprovalAuthority.CoSo, w.OwnerAName);
            w.OwnerB = await factory.CreateUserAsync(w.Dept2, w.Cell2, ApprovalAuthority.CoSo, w.OwnerBName);
            w.OwnerC = await factory.CreateUserAsync(w.Dept1, w.Cell1, ApprovalAuthority.CoSo, $"KQ Chủ hồ sơ C {suffix}");
            var periodManager = await factory.CreateUserAsync();
            var deptViewer = await factory.CreateUserAsync(w.Dept1, w.Cell1);
            var resolver = await factory.CreateUserAsync();
            var conflicted = await factory.CreateUserAsync();
            var manager = await factory.CreateUserAsync(w.Dept1, null);
            var outsideManager = await factory.CreateUserAsync(w.Dept2, null);
            var plain = await factory.CreateUserAsync(w.Dept1, w.Cell1);

            foreach (var owner in new[] { w.OwnerA, w.OwnerB, w.OwnerC })
                await factory.AssignAsync(owner.Id, evaluatee, RoleScopeType.Global, null);
            await factory.AssignAsync(periodManager.Id, await Role("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await factory.AssignAsync(deptViewer.Id, (await factory.CreateRoleAsync("evaluation.results.view")).Id, RoleScopeType.Department, w.Dept1);
            await factory.AssignAsync(resolver.Id, await Role("VAN_PHONG_DANG_UY"), RoleScopeType.Global, null);
            await factory.AssignAsync(conflicted.Id, (await factory.CreateRoleAsync("evaluation.read", "evaluation.appeal.resolve")).Id, RoleScopeType.Global, null);
            await factory.AssignAsync(manager.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, w.Dept1);
            await factory.AssignAsync(outsideManager.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, w.Dept2);
            // Chủ hồ sơ B cũng có quyền lập kế hoạch toàn công ty — để kiểm tra không tự lập kế hoạch cho mình.
            await factory.AssignAsync(w.OwnerB.Id, (await factory.CreateRoleAsync("evaluation.read", "evaluation.improvement.manage")).Id, RoleScopeType.Global, null);

            Task<HttpClient> Login(TestUser user) => factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            var periodManagerC = await Login(periodManager);
            w.OwnerAC = await Login(w.OwnerA);
            w.OwnerBC = await Login(w.OwnerB);
            w.OwnerCC = await Login(w.OwnerC);
            w.DeptViewerC = await Login(deptViewer);
            w.ResolverC = await Login(resolver);
            w.ConflictedC = await Login(conflicted);
            w.ManagerC = await Login(manager);
            w.OutsideManagerC = await Login(outsideManager);
            w.PlainC = await Login(plain);

            w.PeriodId = await OpenPeriodAsync(periodManagerC, w.OwnerA.Id, w.OwnerB.Id, w.OwnerC.Id);
            w.PeriodAppealId = await OpenPeriodAsync(periodManagerC, w.OwnerA.Id);
            var records = await RecordsAsync(periodManagerC, w.PeriodId);
            (w.RecordA, w.RecordB, w.RecordC) = (records[w.OwnerA.Id], records[w.OwnerB.Id], records[w.OwnerC.Id]);
            w.RecordAppeal = (await RecordsAsync(periodManagerC, w.PeriodAppealId))[w.OwnerA.Id];

            await factory.WithDbAsync(async db =>
            {
                async Task Publish(Guid recordId, EvaluationGrade grade, double score, Guid? appraisedBy = null)
                {
                    var record = await db.EvaluationRecords.FirstAsync(r => r.Id == recordId);
                    record.Status = RecordStatus.Published;
                    record.FinalGrade = grade;
                    record.FinalScore = score;
                    record.TotalSelfScore = score + 5;
                    record.AppraisalScore = score;
                    record.AppraisalComment = SecretComment;
                    record.AppraisedById = appraisedBy;
                    record.AppraisedByName = appraisedBy.HasValue ? "Người thẩm định (test)" : null;
                    record.AppraisedAt = appraisedBy.HasValue ? DateTime.UtcNow : null;
                    record.DecisionRecordedAt = DateTime.UtcNow;
                    record.DecisionAuthorityName = "Đảng ủy (test)";
                    record.PublishedAt = DateTime.UtcNow;
                    record.PublishedByName = "Văn phòng (test)";
                }

                await Publish(w.RecordA, EvaluationGrade.HoanThanh, 72.5);
                await Publish(w.RecordB, EvaluationGrade.HoanThanhTot, 85);
                await Publish(w.RecordAppeal, EvaluationGrade.HoanThanh, 74, conflicted.Id);
                var ownerA = await db.PartyMemberProfiles.FirstAsync(m => m.Id == w.OwnerA.Id);
                ownerA.PositionTitle = "Trưởng phòng thử nghiệm";
                await db.SaveChangesAsync();
            });
            return w;
        }

        private static async Task<Guid> OpenPeriodAsync(HttpClient manager, params Guid[] members)
        {
            var created = await Data(await manager.PostAsJsonAsync("/api/evaluations/periods", new
            {
                year = 2100 - Random.Shared.Next(1, 90), quarter = Random.Shared.Next(1, 5), preset = "q3-2026-transition",
                name = $"Kỳ KQ {Guid.NewGuid().ToString("N")[..8]}",
                startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
            }));
            var periodId = created.GetProperty("id").GetGuid();
            await Data(await manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = members }));
            var period = await Data(manager, $"/api/evaluations/periods/{periodId}");
            await Data(await manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
                new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử sau công bố", force = true }));
            return periodId;
        }

        private static async Task<Dictionary<Guid, Guid>> RecordsAsync(HttpClient manager, Guid periodId) =>
            (await Data(manager, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
                .ToDictionary(p => p.GetProperty("memberId").GetGuid(), p => p.GetProperty("recordId").GetGuid());
    }

    #endregion
}
