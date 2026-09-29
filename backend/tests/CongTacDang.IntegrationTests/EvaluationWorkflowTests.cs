using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Imports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 12: luồng đánh giá 9 bước theo cấu hình kỳ trên PostgreSQL thật (W1–W10 trong task file). Mỗi test tự tạo kỳ riêng
/// (năm ngẫu nhiên + tên duy nhất) để không phụ thuộc thứ tự chạy; người dùng và vai trò dùng chung trong một kịch bản.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class EvaluationWorkflowTests
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private readonly ApiFactory _factory;

    public EvaluationWorkflowTests(ApiFactory factory) => _factory = factory;

    #region W1, W2 — đi hết luồng

    [SkippableFact]
    public async Task W1_FullPreset_ManualAndDepartmentParticipants_RecordWalksNineStepsToPublished()
    {
        var w = await WorldAsync();

        // Mẫu cấu hình dựng sẵn.
        var presets = await Data(w.Appraiser, "/api/evaluations/periods/presets");
        Assert.Contains(presets.EnumerateArray(), p => p.GetProperty("code").GetString() == "full");
        Assert.Contains(presets.EnumerateArray(), p => p.GetProperty("code").GetString() == "q3-2026-transition");

        // Tạo kỳ từ mẫu "Đầy đủ theo HD03" → dự thảo.
        var period = await CreatePeriodAsync(w, "full");
        Assert.Equal("Draft", period.GetProperty("status").GetString());
        Assert.Equal("09A", period.GetProperty("settings").GetProperty("selfScoreForm").GetString());
        var periodId = period.GetProperty("id").GetGuid();

        // Thêm người được đánh giá: chọn tay, rồi theo Phòng (người đã có bị bỏ qua).
        var manual = await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Owner1.Id } }));
        Assert.Equal(1, manual.GetProperty("added").GetInt32());
        var byDept = await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { departmentId = w.Dept1 }));
        Assert.True(byDept.GetProperty("added").GetInt32() >= 2); // owner2 + lãnh đạo Phòng (đều thuộc Phòng 1)
        Assert.Contains(byDept.GetProperty("skipped").EnumerateArray(), s => s.GetString()!.Contains("đã có trong danh sách"));
        var participants = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants");
        Assert.Contains(participants.EnumerateArray(), p => p.GetProperty("memberId").GetGuid() == w.Owner2.Id);
        // Chưa mở kỳ → chủ hồ sơ chưa thao tác được.
        var recordId = participants.EnumerateArray().First(p => p.GetProperty("memberId").GetGuid() == w.Owner1.Id).GetProperty("recordId").GetGuid();
        var draft = await PostAsync(w.Owner1C, recordId, "tasks/submit", Tasks(), await VersionAsync(w, recordId));
        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        Assert.Contains("dự thảo", await MessageAsync(draft));

        await TransitionAsync(w.Appraiser, periodId, "open");

        // Người được đánh giá chỉ thấy hồ sơ của mình.
        var mine = await Data(w.Owner1C, $"/api/evaluations/my-record?periodId={periodId}");
        Assert.Equal(recordId, mine.GetProperty("id").GetGuid());
        Assert.Equal("AwaitingRegistration", mine.GetProperty("status").GetString());
        Assert.Equal(9, mine.GetProperty("progress").GetArrayLength());

        // 1. B1_REGISTER — chủ hồ sơ
        await AssertActionsAsync(w.Owner1C, recordId, "SubmitTasks");
        await AssertActionsAsync(w.DeptLeadC, recordId);
        await StepAsync(w.Owner1C, recordId, "tasks/submit", Tasks(), "AwaitingTaskApproval");
        // 2. B1_APPROVE — Lãnh đạo Phòng 1
        await AssertActionsAsync(w.DeptLeadC, recordId, "ApproveTasks", "ReturnTasks");
        await StepAsync(w.DeptLeadC, recordId, "tasks/approve", new { comment = "Đồng ý danh mục" }, "AwaitingSelfScore");
        // 3. B2_SELF_SCORE — 09A theo nhiệm vụ
        var record = await Data(w.Owner1C, $"/api/evaluations/records/{recordId}");
        var taskScores = record.GetProperty("tasks").EnumerateArray()
            .Select(t => new { taskId = t.GetProperty("id").GetGuid(), criteriaA_Ratio = 1.0, criteriaB_Ratio = 0.9, criteriaC_Ratio = 1.0, criteriaD_Ratio = 1.0, isExceedStandard = false })
            .ToArray();
        var scored = await StepAsync(w.Owner1C, recordId, "self-score/submit",
            new { generalScores = new[] { 5.0, 5.0, 5.0, 4.5, 4.5, 5.0 }, taskScores, selfProposedGrade = "HoanThanhTot" }, "AwaitingCellConfirm");
        Assert.Equal(29.0, scored.GetProperty("generalCriteriaScore").GetDouble(), 6);
        Assert.True(scored.GetProperty("totalSelfScore").GetDouble() > 90);
        // 4. B2_CELL_CONFIRM — Chi ủy Chi bộ 1
        await StepAsync(w.CellSecC, recordId, "cell/confirm", new { comment = "Chi bộ xác nhận" }, "AwaitingCollective");
        // 5. B3A_COLLECTIVE — thư ký tập thể phạm vi Phòng 1 lập biên bản (DepartmentId) và ghi kết quả kiểm phiếu tổng hợp.
        var meeting = await Data(await w.SecretaryC.PostAsJsonAsync("/api/evaluations/meetings", new
        {
            periodId, departmentId = w.Dept1, stage = "B3A_COLLECTIVE", formCode = "M12",
            meetingType = "Hội nghị tập thể lãnh đạo Phòng", invitedCount = 9, presentCount = 8, absentCount = 1
        }));
        var meetingId = meeting.GetProperty("id").GetGuid();
        Assert.Equal(w.Dept1, meeting.GetProperty("departmentId").GetGuid());
        var tooMany = await PostAsync(w.SecretaryC, recordId, "collective", new
        {
            proposedGrade = "HoanThanhTot", meetingId, votes = new { votesExcellent = 5, votesGood = 4, votesSatisfactory = 0, votesUnsatisfactory = 0, invalidVotes = 0 }
        }, await VersionAsync(w, recordId));
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        await StepAsync(w.SecretaryC, recordId, "collective", new
        {
            proposedGrade = "HoanThanhTot", comment = "Tập thể đề xuất", meetingId,
            votes = new { votesExcellent = 2, votesGood = 5, votesSatisfactory = 0, votesUnsatisfactory = 0, invalidVotes = 1 }
        }, "AwaitingAppraisal");
        // Thư ký phạm vi Phòng thấy biên bản của Phòng mình (task 09 "Cần phối hợp").
        Assert.Contains(await Ids(w.SecretaryC, $"/api/evaluations/meetings?periodId={periodId}"), id => id == meetingId);
        // 6. B3B_APPRAISAL
        await StepAsync(w.AppraiserC, recordId, "appraisal", new { appraisalScore = 92.5, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");
        // 7. B3C_DIRECTOR
        await StepAsync(w.DirectorC, recordId, "director-review", new { comment = "Nhận xét của Giám đốc", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");
        // 8. B4_DECISION (hồ sơ CoSo)
        await StepAsync(w.OfficeC, recordId, "decision", new { finalGrade = "HoanThanhTot", documentNumber = "12-QĐ/ĐU", authorityName = "Đảng ủy Công ty" }, "AwaitingPublish");
        // 9. B5_PUBLISH
        var published = await StepAsync(w.OfficeC, recordId, "publish", new { }, "Published");
        Assert.Equal("HoanThanhTot", published.GetProperty("finalGrade").GetString());
        Assert.Equal(92.5, published.GetProperty("finalScore").GetDouble(), 6);
        Assert.All(published.GetProperty("progress").EnumerateArray(), p => Assert.Equal("done", p.GetProperty("state").GetString()));

        var history = await Data(w.Owner1C, $"/api/evaluations/records/{recordId}/history");
        var completes = history.EnumerateArray().Where(h => h.GetProperty("action").GetString() == "Complete").Select(h => h.GetProperty("step").GetString()).ToList();
        Assert.Equal(new[] { "B1_REGISTER", "B1_APPROVE", "B2_SELF_SCORE", "B2_CELL_CONFIRM", "B3A_COLLECTIVE", "B3B_APPRAISAL", "B3C_DIRECTOR", "B4_DECISION", "B5_PUBLISH" }, completes);
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("action").GetString() == "Create");

        // B-03: kết quả kiểm phiếu chỉ có tổng hợp; audit của bảng kiểm phiếu chỉ ghi người nhập (thư ký).
        await _factory.WithDbAsync(async db =>
        {
            var summary = await db.EvaluationMeetingVoteSummaries.SingleAsync(v => v.MeetingId == meetingId && v.RecordId == recordId);
            Assert.Equal((2, 5, 1), (summary.VotesExcellent, summary.VotesGood, summary.InvalidVotes));
            var audits = await db.AuditLogs.Where(a => a.EntityType == nameof(EvaluationMeetingVoteSummary)).ToListAsync();
            var mineAudit = audits.Where(a => a.EntityId.Contains(summary.Id.ToString())).ToList();
            Assert.NotEmpty(mineAudit);
            Assert.All(mineAudit, a => Assert.Equal(w.Secretary.Id, a.ActorId));
        });
    }

    [SkippableFact]
    public async Task W2_TransitionPreset_SkipsB1_SelfScores09B_ToPublished()
    {
        var w = await WorldAsync();
        var periodId = (await CreatePeriodAsync(w, "q3-2026-transition")).GetProperty("id").GetGuid();
        var recordId = await AddParticipantAsync(w, periodId, w.Owner1.Id);
        await TransitionAsync(w.Appraiser, periodId, "open");

        var record = await Data(w.Owner1C, $"/api/evaluations/records/{recordId}");
        Assert.Equal("AwaitingSelfScore", record.GetProperty("status").GetString());
        Assert.Equal("09B", record.GetProperty("selfScoreForm").GetString());
        var progress = record.GetProperty("progress").EnumerateArray().ToList();
        Assert.Equal("skipped", progress[0].GetProperty("state").GetString());
        Assert.Equal("skipped", progress[1].GetProperty("state").GetString());
        Assert.Equal("current", progress[2].GetProperty("state").GetString());

        // Bước B1 không áp dụng → gọi thẳng API đăng ký bị 409.
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.Owner1C, recordId, "tasks/submit", Tasks(), await VersionAsync(w, recordId))).StatusCode);
        // 09B: thiếu điểm trục → 400; trục vượt tối đa → 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.Owner1C, recordId, "self-score/submit",
            new { generalScores = new[] { 5.0, 5, 5, 5, 5, 5 } }, await VersionAsync(w, recordId))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.Owner1C, recordId, "self-score/submit",
            new { generalScores = new[] { 5.0, 5, 5, 5, 5, 5 }, axisScores = new[] { 16.0, 10, 10, 15, 10, 10 } }, await VersionAsync(w, recordId))).StatusCode);

        var scored = await StepAsync(w.Owner1C, recordId, "self-score/submit",
            new { generalScores = new[] { 5.0, 5, 5, 5, 4, 5 }, axisScores = new[] { 14.0, 9, 9, 15, 8, 10 } }, "AwaitingCellConfirm");
        Assert.Equal(65.0, scored.GetProperty("tasksScore").GetDouble(), 6);
        Assert.Equal(94.0, scored.GetProperty("totalSelfScore").GetDouble(), 6);
        Assert.Equal("HoanThanhXuatSac", scored.GetProperty("selfProposedGrade").GetString()); // gợi ý theo ngưỡng 90
        Assert.Equal(6, scored.GetProperty("axisScores").GetArrayLength());

        await StepAsync(w.CellSecC, recordId, "cell/confirm", new { }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, recordId, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");
        await StepAsync(w.AppraiserC, recordId, "appraisal", new { appraisalScore = 90.0, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");
        await StepAsync(w.DirectorC, recordId, "director-review", new { comment = "Đồng ý", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");
        await StepAsync(w.OfficeC, recordId, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        await StepAsync(w.OfficeC, recordId, "publish", new { }, "Published");
    }

    #endregion

    #region W3–W8 — quyền, trả lại, thứ tự, cấp quyết định, mở lại, đồng thời

    [SkippableFact]
    public async Task W3_NoPermissionOrOutOfScope_Is403_OwnerCannotApproveOwnRecord()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "full", w.Owner1.Id, w.Owner3.Id, w.DeptLead.Id, w.AppraiserUser.Id);
        var r1 = await RecordOfAsync(w, periodId, w.Owner1.Id);
        var r3 = await RecordOfAsync(w, periodId, w.Owner3.Id);
        var rLead = await RecordOfAsync(w, periodId, w.DeptLead.Id);
        var rAppraiser = await RecordOfAsync(w, periodId, w.AppraiserUser.Id);
        foreach (var id in new[] { r1, r3, rLead })
            await SetStatusAsync(id, RecordStatus.AwaitingTaskApproval);
        await SetStatusAsync(rAppraiser, RecordStatus.AwaitingAppraisal);

        // Không có quyền duyệt danh mục.
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.CellSecC, r1, "tasks/approve", new { }, await VersionAsync(w, r1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.PlainC, r1, "tasks/approve", new { }, await VersionAsync(w, r1))).StatusCode);
        // Có quyền nhưng ngoài phạm vi (Lãnh đạo Phòng 1 — hồ sơ Phòng 2).
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.DeptLeadC, r3, "tasks/approve", new { }, await VersionAsync(w, r3))).StatusCode);
        // Chủ hồ sơ không tự duyệt / tự thẩm định (xung đột lợi ích).
        var ownApprove = await PostAsync(w.DeptLeadC, rLead, "tasks/approve", new { }, await VersionAsync(w, rLead));
        Assert.Equal(HttpStatusCode.Forbidden, ownApprove.StatusCode);
        Assert.Contains("xung đột lợi ích", await MessageAsync(ownApprove));
        var ownAppraise = await PostAsync(w.AppraiserC, rAppraiser, "appraisal",
            new { appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, rAppraiser));
        Assert.Equal(HttpStatusCode.Forbidden, ownAppraise.StatusCode);
        Assert.Contains("xung đột lợi ích", await MessageAsync(ownAppraise));
        // actions: chủ hồ sơ không thấy hành động thẩm định trên hồ sơ của mình.
        await AssertActionsAsync(w.AppraiserC, rAppraiser);
        // Trong phạm vi → được.
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(w.DeptLeadC, r1, "tasks/approve", new { }, await VersionAsync(w, r1))).StatusCode);
        // Người ngoài phạm vi không xem được hành động của hồ sơ.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.CellSecC.GetAsync($"/api/evaluations/records/{r3}/actions")).StatusCode);
    }

    [SkippableFact]
    public async Task W4_ReturnWithReason_AtB1B2B3b_OwnerFixesAndResubmits()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "full", w.Owner2.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner2.Id);
        // Task 15: người diện cấp trên mặc định đi hồ sơ luồng "cap-tren" (thẩm định do cấp trên thực hiện, không trả lại
        // trong hệ thống) → chuyển sang hồ sơ luồng cơ sở để kiểm tra trả lại ở cả 3 bước.
        var participant = (await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("recordId").GetGuid() == id);
        await Data(await w.Appraiser.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{id}/profile",
            new { version = participant.GetProperty("version").GetUInt32(), workflowProfileCode = "co-so", reason = "Kiểm thử trả lại ở bước thẩm định" }));

        await StepAsync(w.Owner2C, id, "tasks/submit", Tasks(), "AwaitingTaskApproval");
        // Trả lại bắt buộc lý do.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.DeptLeadC, id, "tasks/return", new { reason = "" }, await VersionAsync(w, id))).StatusCode);
        var returned = await StepAsync(w.DeptLeadC, id, "tasks/return", new { reason = "Trọng số sản phẩm 1 chưa hợp lý" }, "AwaitingRegistration");
        Assert.Equal("Trọng số sản phẩm 1 chưa hợp lý", returned.GetProperty("returnReason").GetString());
        await StepAsync(w.Owner2C, id, "tasks/submit", Tasks(35, 20, 15), "AwaitingTaskApproval");
        await StepAsync(w.DeptLeadC, id, "tasks/approve", new { }, "AwaitingSelfScore");

        await SelfScoreAsync(w, w.Owner2C, id);
        await StepAsync(w.CellSecC, id, "cell/return", new { reason = "Thiếu minh chứng tiêu chí T3" }, "AwaitingSelfScore");
        await SelfScoreAsync(w, w.Owner2C, id);
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.AppraiserC, id, "appraisal/return", new { reason = " " }, await VersionAsync(w, id))).StatusCode);
        await StepAsync(w.AppraiserC, id, "appraisal/return", new { reason = "Điểm tự chấm chưa khớp minh chứng" }, "AwaitingSelfScore");
        var resubmitted = await SelfScoreAsync(w, w.Owner2C, id);
        Assert.Null(resubmitted.GetProperty("returnReason").GetString());
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");
        await StepAsync(w.AppraiserC, id, "appraisal", new { appraisalScore = 88.0, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");

        var history = await Data(w.Owner2C, $"/api/evaluations/records/{id}/history");
        var returns = history.EnumerateArray().Where(h => h.GetProperty("action").GetString() == "Return").ToList();
        Assert.Equal(new[] { "B1_APPROVE", "B2_CELL_CONFIRM", "B3B_APPRAISAL" }, returns.Select(h => h.GetProperty("step").GetString()));
        Assert.All(returns, h => Assert.False(string.IsNullOrWhiteSpace(h.GetProperty("reason").GetString())));

        // Đợt 6: guard không còn ràng buộc evaluation.decide theo cấp quyết định — hồ sơ CapTren đã chuyển sang hồ sơ luồng
        // cơ sở (B4 Nội bộ) được quyết định trong hệ thống; ghi nhận kết quả cấp trên ở bước Nội bộ → 409.
        await StepAsync(w.DirectorC, id, "director-review", new { comment = "Đồng ý", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "external/B4_DECISION",
            new { authorityName = "BTV Đảng ủy Tổng công ty", grade = "HoanThanhTot" }, await VersionAsync(w, id))).StatusCode);
        var decided = await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        Assert.Equal("CapTren", decided.GetProperty("approvalAuthority").GetString());
    }

    [SkippableFact]
    public async Task W5_OutOfOrder409_MissingVersion400_LockedPeriodBlocksOwner()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "full", w.Owner1.Id, w.Owner3.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);

        // Gọi thẳng API bước sau khi hồ sơ còn ở bước đăng ký → 409.
        var early = await PostAsync(w.AppraiserC, id, "appraisal", new { appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("Hồ sơ đang ở bước", await MessageAsync(early));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "publish", new { }, await VersionAsync(w, id))).StatusCode);
        // Thiếu version với hành động ghi → 400 (T-24).
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(w.Owner1C, id, "tasks/submit", Tasks())).StatusCode);

        // Khóa dữ liệu: chủ hồ sơ không sửa được; bước từ thẩm định trở đi vẫn chạy.
        var other = await RecordOfAsync(w, periodId, w.Owner3.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingSelfScore, withTasks: true);
        await SetStatusAsync(other, RecordStatus.AwaitingAppraisal);
        await TransitionAsync(w.Appraiser, periodId, "lock");
        var locked = await PostAsync(w.Owner1C, id, "self-score/submit", new { generalScores = new[] { 5.0, 5, 5, 5, 5, 5 } }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("khóa dữ liệu", await MessageAsync(locked));
        await AssertActionsAsync(w.Owner1C, id);
        await StepAsync(w.AppraiserC, other, "appraisal", new { appraisalScore = 85.0, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");

        // Khóa dữ liệu → Đang mở cần lý do; chỉ tiến (không mở lại kỳ đang mở).
        Assert.Equal(HttpStatusCode.BadRequest, (await TransitionRawAsync(w.Appraiser, periodId, "unlock", null)).StatusCode);
        await TransitionAsync(w.Appraiser, periodId, "unlock", "Bổ sung minh chứng theo yêu cầu BTV");
        Assert.Equal(HttpStatusCode.Conflict, (await TransitionRawAsync(w.Appraiser, periodId, "open", null)).StatusCode);
        // Đóng kỳ khi còn hồ sơ chưa công bố → 409.
        Assert.Equal(HttpStatusCode.Conflict, (await TransitionRawAsync(w.Appraiser, periodId, "close", null)).StatusCode);
        await SelfScoreAsync(w, w.Owner1C, id);
        // Kỳ đã mở: không bật/tắt bước được nữa (chỉ thời hạn).
        var period = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}");
        var settings = JsonSerializer.Deserialize<Dictionary<string, object>>(period.GetProperty("settings").GetRawText())!;
        settings["selfScoreForm"] = "09B";
        var changeForm = await w.Appraiser.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
            new { version = period.GetProperty("version").GetUInt32(), settings });
        Assert.Equal(HttpStatusCode.Conflict, changeForm.StatusCode);
    }

    [SkippableFact]
    public async Task W6_CapTrenDecisionRecordedAsExternal_CoSoDecidedInternally()
    {
        // Task 15: hồ sơ diện cấp trên (hồ sơ luồng "cap-tren") có B4 do cấp trên thực hiện — chỉ ghi nhận kết quả
        // (evaluation.external.record); hồ sơ cơ sở ghi nhận quyết định trong hệ thống (evaluation.decide).
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "full", w.Owner1.Id, w.Owner2.Id);
        var coSo = await RecordOfAsync(w, periodId, w.Owner1.Id);
        var capTren = await RecordOfAsync(w, periodId, w.Owner2.Id);
        await SetStatusAsync(coSo, RecordStatus.AwaitingDecision);
        await SetStatusAsync(capTren, RecordStatus.AwaitingDecision);
        var decision = new { finalGrade = "HoanThanhTot" };
        var external = new { authorityName = "BTV Đảng ủy Tổng công ty", documentNumber = "15-QĐ/ĐU", grade = "HoanThanhTot" };

        var internalOnExternal = await PostAsync(w.LocalDeciderC, capTren, "decision", decision, await VersionAsync(w, capTren));
        Assert.Equal(HttpStatusCode.Conflict, internalOnExternal.StatusCode);
        Assert.Contains("cấp trên", await MessageAsync(internalOnExternal));
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.LocalDeciderC, capTren, "external/B4_DECISION", external, await VersionAsync(w, capTren))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.ExternalDeciderC, coSo, "external/B4_DECISION", external, await VersionAsync(w, coSo))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.ExternalDeciderC, coSo, "decision", decision, await VersionAsync(w, coSo))).StatusCode);
        await AssertActionsAsync(w.LocalDeciderC, capTren);
        await AssertActionsAsync(w.ExternalDeciderC, capTren, "RecordExternal");
        await AssertActionsAsync(w.ExternalDeciderC, coSo);

        var recorded = await StepAsync(w.ExternalDeciderC, capTren, "external/B4_DECISION", external, "AwaitingPublish");
        Assert.Equal("HoanThanhTot", recorded.GetProperty("finalGrade").GetString());
        Assert.Equal("BTV Đảng ủy Tổng công ty", recorded.GetProperty("decisionAuthorityName").GetString());
        await StepAsync(w.LocalDeciderC, coSo, "decision", decision, "AwaitingPublish");
    }

    [SkippableFact]
    public async Task W7_Published_IsLocked_ReopenWithReason_EditAndRepublish_HistoryKeepsAll()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "full", w.Owner3.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner3.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingDecision);
        await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        await StepAsync(w.OfficeC, id, "publish", new { }, "Published");

        // Đã công bố → sửa bị chặn.
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.AppraiserC, id, "appraisal",
            new { appraisalScore = 80.0, comment = "x", proposedGrade = "HoanThanh" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "publish", new { }, await VersionAsync(w, id))).StatusCode);
        await AssertActionsAsync(w.OfficeC, id, "Reopen");
        await AssertActionsAsync(w.Owner3C, id);

        // Mở lại: bắt buộc lý do; không sớm hơn bước tự chấm.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "reopen", new { targetStep = "B3B_APPRAISAL", reason = "" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "reopen", new { targetStep = "B1_REGISTER", reason = "Đính chính" }, await VersionAsync(w, id))).StatusCode);
        await StepAsync(w.OfficeC, id, "reopen", new { targetStep = "B3B_APPRAISAL", reason = "Kiến nghị của cá nhân được chấp nhận" }, "AwaitingAppraisal");

        await StepAsync(w.AppraiserC, id, "appraisal", new { appraisalScore = 91.0, comment = "Thẩm định lại", proposedGrade = "HoanThanhXuatSac" }, "AwaitingDirectorReview");
        await StepAsync(w.DirectorC, id, "director-review", new { comment = "Đồng ý", proposedGrade = "HoanThanhXuatSac" }, "AwaitingDecision");
        await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhXuatSac" }, "AwaitingPublish");
        var republished = await StepAsync(w.OfficeC, id, "publish", new { }, "Published");
        Assert.Equal("HoanThanhXuatSac", republished.GetProperty("finalGrade").GetString());

        var history = (await Data(w.Owner3C, $"/api/evaluations/records/{id}/history")).EnumerateArray().ToList();
        Assert.Equal(2, history.Count(h => h.GetProperty("step").GetString() == "B5_PUBLISH" && h.GetProperty("action").GetString() == "Complete"));
        var reopen = Assert.Single(history, h => h.GetProperty("action").GetString() == "Reopen");
        Assert.Equal("Kiến nghị của cá nhân được chấp nhận", reopen.GetProperty("reason").GetString());
        Assert.Equal("HoanThanhTot", reopen.GetProperty("gradeBefore").GetString());
        var lastDecision = history.Last(h => h.GetProperty("step").GetString() == "B4_DECISION");
        Assert.Equal("HoanThanhXuatSac", lastDecision.GetProperty("gradeAfter").GetString());
    }

    [SkippableFact]
    public async Task W8_TwoUsersSameRecord_SecondGets409()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "full", w.Owner1.Id, w.Owner3.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingAppraisal);

        // (a) Người quản lý kỳ sửa ảnh chụp (không đổi bước) → phiên bản đổi → người thẩm định dùng phiên bản cũ nhận 409.
        var stale = await VersionAsync(w, id);
        var participant = (await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("recordId").GetGuid() == id);
        var snapshot = await w.Appraiser.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{id}/snapshot", new
        {
            version = participant.GetProperty("version").GetUInt32(),
            departmentId = w.Dept1, partyCellId = w.Cell1, jobGroup = "Khung1_QuanLyDangDoanThe", reason = "Điều chỉnh khung chức danh"
        });
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);
        var conflict = await PostAsync(w.Appraiser2C, id, "appraisal", new { appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("tải lại", await MessageAsync(conflict));

        // (b) Hai người thẩm định gửi cùng lúc với cùng phiên bản → đúng một người thành công, người còn lại 409.
        var version = await VersionAsync(w, id);
        var body = new { appraisalScore = 90.0, comment = "Đồng thời", proposedGrade = "HoanThanhTot" };
        var results = await Task.WhenAll(
            PostAsync(w.AppraiserC, id, "appraisal", body, version),
            PostAsync(w.Appraiser2C, id, "appraisal", body, version));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    #endregion

    #region W9, W10 — việc cần xử lý, import

    [SkippableFact]
    public async Task W9_WorkQueue_ContainsOnlyRecordsWaitingForCurrentUser_InScope()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "full", w.Owner1.Id, w.Owner2.Id, w.Owner3.Id);
        var r1 = await RecordOfAsync(w, periodId, w.Owner1.Id);
        var r2 = await RecordOfAsync(w, periodId, w.Owner2.Id);
        var r3 = await RecordOfAsync(w, periodId, w.Owner3.Id);
        await SetStatusAsync(r1, RecordStatus.AwaitingTaskApproval);
        await SetStatusAsync(r2, RecordStatus.AwaitingCellConfirm);
        await SetStatusAsync(r3, RecordStatus.AwaitingTaskApproval);

        async Task<Dictionary<Guid, string>> Queue(HttpClient client)
        {
            var data = await Data(client, $"/api/evaluations/work-queue?periodId={periodId}");
            return data.GetProperty("groups").EnumerateArray()
                .SelectMany(g => g.GetProperty("items").EnumerateArray().Select(i => (Id: i.GetProperty("recordId").GetGuid(), Step: g.GetProperty("step").GetString()!)))
                .ToDictionary(x => x.Id, x => x.Step);
        }

        Assert.Equal(new Dictionary<Guid, string> { [r1] = "B1_APPROVE" }, await Queue(w.DeptLeadC));     // Phòng 1, không có hồ sơ Phòng 2
        Assert.Equal(new Dictionary<Guid, string> { [r2] = "B2_CELL_CONFIRM" }, await Queue(w.CellSecC)); // Chi bộ 1
        Assert.Empty(await Queue(w.AppraiserC));
        Assert.Empty(await Queue(w.PlainC));
        Assert.Empty(await Queue(w.Owner1C)); // hồ sơ của mình đang chờ người khác

        await SetStatusAsync(r3, RecordStatus.AwaitingRegistration);
        var ownerQueue = await Data(w.Owner3C, $"/api/evaluations/work-queue?periodId={periodId}");
        var item = ownerQueue.GetProperty("groups")[0].GetProperty("items")[0];
        Assert.Equal(r3, item.GetProperty("recordId").GetGuid());
        Assert.True(item.GetProperty("isOwnRecord").GetBoolean());
    }

    [SkippableFact]
    public async Task W10_ImportParticipantsFromExcel()
    {
        var w = await WorldAsync();
        var period = await CreatePeriodAsync(w, "full");
        var periodId = period.GetProperty("id").GetGuid();
        var year = period.GetProperty("year").GetInt32().ToString();
        var quarter = period.GetProperty("quarter").GetInt32().ToString();
        var name = period.GetProperty("name").GetString()!;

        var kinds = await Data(w.Appraiser, "/api/imports/kinds");
        Assert.Contains(kinds.EnumerateArray(), k => k.GetProperty("kind").GetString() == "period-participants");
        Assert.Equal(HttpStatusCode.OK, (await w.Appraiser.GetAsync("/api/imports/period-participants/template")).StatusCode);
        // Không có system.import / period.manage → không thấy loại, không tải được mẫu.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.OfficeC.GetAsync("/api/imports/period-participants/template")).StatusCode);

        string[] headers = { "Năm", "Quý", "Tên kỳ", "Tên đăng nhập" };
        var bad = await PreviewAsync(w.Appraiser, BuildFile(headers,
            new[] { year, quarter, name, w.Owner1.Username },
            new[] { year, quarter, name, "khong_ton_tai_" + Guid.NewGuid().ToString("N")[..6] },
            new[] { year, quarter, name, w.Owner1.Username }));
        Assert.False(bad.GetProperty("canCommit").GetBoolean());
        Assert.Equal(2, bad.GetProperty("summary").GetProperty("error").GetInt32());

        var good = await PreviewAsync(w.Appraiser, BuildFile(headers,
            new[] { year, quarter, name, w.Owner1.Username },
            new[] { year, quarter, name, w.Owner3.Username.ToUpperInvariant() }));
        Assert.True(good.GetProperty("canCommit").GetBoolean(), good.GetRawText());
        var commit = await w.Appraiser.PostAsync($"/api/imports/{good.GetProperty("sessionId").GetGuid()}/commit", null);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        Assert.Equal(2, (await Data(commit)).GetProperty("created").GetInt32());

        var participants = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants");
        Assert.Equal(new[] { w.Owner1.Id, w.Owner3.Id }.OrderBy(x => x),
            participants.EnumerateArray().Select(p => p.GetProperty("memberId").GetGuid()).OrderBy(x => x));
        var owner3 = participants.EnumerateArray().First(p => p.GetProperty("memberId").GetGuid() == w.Owner3.Id);
        Assert.Equal(w.Dept2, owner3.GetProperty("departmentId").GetGuid()); // ảnh chụp Phòng tại thời điểm nhập

        // Nhập lại cùng tệp → lỗi dòng "đã có", không ghi.
        var again = await PreviewAsync(w.Appraiser, BuildFile(headers, new[] { year, quarter, name, w.Owner1.Username }));
        Assert.False(again.GetProperty("canCommit").GetBoolean());
    }

    #endregion

    #region Hỗ trợ

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

    private static object Tasks(double a = 30, double b = 20, double c = 20) => new
    {
        tasks = new[]
        {
            new { taskName = "Sản phẩm 1", targetOutput = "Kết quả 1", weight = a },
            new { taskName = "Sản phẩm 2", targetOutput = "Kết quả 2", weight = b },
            new { taskName = "Sản phẩm 3", targetOutput = "Kết quả 3", weight = c }
        }
    };

    private static async Task<JsonElement> CreatePeriodAsync(World w, string preset)
    {
        var year = 2100 - Random.Shared.Next(1, 90);
        var body = new
        {
            year, quarter = Random.Shared.Next(1, 5), preset,
            name = $"Kỳ W {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        return await Data(await w.Appraiser.PostAsJsonAsync("/api/evaluations/periods", body));
    }

    private static async Task<Guid> AddParticipantAsync(World w, Guid periodId, Guid memberId)
    {
        await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { memberId } }));
        return await RecordOfAsync(w, periodId, memberId);
    }

    private static async Task<Guid> OpenPeriodAsync(World w, string preset, params Guid[] members)
    {
        var periodId = (await CreatePeriodAsync(w, preset)).GetProperty("id").GetGuid();
        await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = members }));
        await TransitionAsync(w.Appraiser, periodId, "open");
        return periodId;
    }

    private static async Task<Guid> RecordOfAsync(World w, Guid periodId, Guid memberId) =>
        (await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == memberId).GetProperty("recordId").GetGuid();

    private static async Task<HttpResponseMessage> TransitionRawAsync(HttpClient client, Guid periodId, string action, string? reason)
    {
        var period = await Data(client, $"/api/evaluations/periods/{periodId}");
        // Task 15: mở kỳ kiểm tra kẹt luồng; thế giới W có người thực hiện theo phạm vi riêng từng Phòng (ví dụ thư ký tập thể
        // chỉ Phòng 1) nên mở bắt buộc kèm lý do — kiểm tra kẹt luồng có test riêng (WorkflowProfileIntegrationTests).
        if (action == "open")
            return await client.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/{action}",
                new { version = period.GetProperty("version").GetUInt32(), reason = reason ?? "Kiểm thử luồng", force = true });
        return await client.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/{action}",
            new { version = period.GetProperty("version").GetUInt32(), reason });
    }

    private static async Task TransitionAsync(HttpClient client, Guid periodId, string action, string? reason = null) =>
        await Data(await TransitionRawAsync(client, periodId, action, reason));

    /// <summary>Phiên bản hiện tại của hồ sơ (đọc bằng người thẩm định phạm vi Toàn công ty).</summary>
    private static async Task<uint> VersionAsync(World w, Guid recordId) =>
        (await Data(w.AppraiserC, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();

    /// <summary>Gửi hành động kèm version (đọc mới nếu không truyền).</summary>
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body, uint? version = null)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (version.HasValue)
            json["version"] = version.Value;
        return client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    /// <summary>Thực hiện một bước với version hiện tại và kiểm tra trạng thái sau.</summary>
    private async Task<JsonElement> StepAsync(HttpClient client, Guid recordId, string action, object body, string expectedStatus)
    {
        var w = await WorldAsync();
        var response = await PostAsync(client, recordId, action, body, await VersionAsync(w, recordId));
        var data = await Data(response);
        Assert.Equal(expectedStatus, data.GetProperty("status").GetString());
        return data;
    }

    private async Task<JsonElement> SelfScoreAsync(World w, HttpClient owner, Guid recordId)
    {
        var record = await Data(owner, $"/api/evaluations/records/{recordId}");
        var taskScores = record.GetProperty("tasks").EnumerateArray()
            .Select(t => new { taskId = t.GetProperty("id").GetGuid(), criteriaA_Ratio = 1.0, criteriaB_Ratio = 1.0, criteriaC_Ratio = 0.9, criteriaD_Ratio = 1.0 })
            .ToArray();
        return await StepAsync(owner, recordId, "self-score/submit",
            new { generalScores = new[] { 4.5, 4.5, 4.5, 4.5, 4.5, 4.5 }, taskScores }, "AwaitingCellConfirm");
    }

    private static async Task AssertActionsAsync(HttpClient client, Guid recordId, params string[] expected)
    {
        var data = await Data(client, $"/api/evaluations/records/{recordId}/actions");
        var actions = data.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("action").GetString()!).OrderBy(x => x).ToList();
        Assert.Equal(expected.OrderBy(x => x).ToList(), actions);
    }

    private async Task SetStatusAsync(Guid recordId, RecordStatus status, bool withTasks = false)
    {
        await _factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.Include(r => r.Tasks).FirstAsync(r => r.Id == recordId);
            record.Status = status;
            if (withTasks && !record.Tasks.Any(t => !t.IsDeleted))
            {
                var order = 1;
                foreach (var weight in new[] { 30.0, 20.0, 20.0 })
                    db.EvaluationTasks.Add(new EvaluationTask { RecordId = recordId, TaskOrder = order, TaskName = $"Sản phẩm {order++}", Weight = weight, SelfScore = weight });
            }
            await db.SaveChangesAsync();
        });
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    private static async Task<List<Guid>> Ids(HttpClient client, string url) =>
        (await Data(client, url)).EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();

    private static async Task<string> MessageAsync(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    private static byte[] BuildFile(string[] headers, params string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(ImportLimits.DataSheetName);
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                sheet.Cell(r + 2, c + 1).Value = rows[r][c];
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static async Task<JsonElement> PreviewAsync(HttpClient client, byte[] file)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(file);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "nguoi-duoc-danh-gia.xlsx");
        return await Data(await client.PostAsync("/api/imports/period-participants/preview", content));
    }

    /// <summary>Người dùng và vai trò dùng chung: 2 Phòng, 2 Chi bộ, người được đánh giá và các vai trò theo từng bước.</summary>
    private sealed class World
    {
        public Guid Dept1, Dept2, Cell1, Cell2;
        public TestUser Owner1 = null!, Owner2 = null!, Owner3 = null!, DeptLead = null!, Secretary = null!, Appraiser2User = null!;
        public TestUser AppraiserUser = null!;
        public HttpClient Owner1C = null!, Owner2C = null!, Owner3C = null!, DeptLeadC = null!, CellSecC = null!, SecretaryC = null!;
        public HttpClient AppraiserC = null!, Appraiser2C = null!, DirectorC = null!, OfficeC = null!, LocalDeciderC = null!, ExternalDeciderC = null!, PlainC = null!;

        /// <summary>Người quản lý kỳ (vai trò "Cơ quan thẩm định" có period.manage, system.import).</summary>
        public HttpClient Appraiser => AppraiserC;

        public static async Task<World> CreateAsync(ApiFactory factory)
        {
            var w = new World();
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            await factory.WithDbAsync(async db =>
            {
                var d1 = new AdministrativeDepartment { Code = $"W-P1-{suffix}", Name = $"Phòng 1 {suffix}" };
                var d2 = new AdministrativeDepartment { Code = $"W-P2-{suffix}", Name = $"Phòng 2 {suffix}" };
                var c1 = new PartyCell { Code = $"W-C1-{suffix}", Name = $"Chi bộ 1 {suffix}" };
                var c2 = new PartyCell { Code = $"W-C2-{suffix}", Name = $"Chi bộ 2 {suffix}" };
                db.AddRange(d1, d2, c1, c2);
                await db.SaveChangesAsync();
                (w.Dept1, w.Dept2, w.Cell1, w.Cell2) = (d1.Id, d2.Id, c1.Id, c2.Id);
            });

            async Task<Guid> Role(string code) => await factory.GetRoleIdAsync(code);
            var evaluatee = await Role("NGUOI_DUOC_DANH_GIA");

            w.Owner1 = await factory.CreateUserAsync(w.Dept1, w.Cell1, ApprovalAuthority.CoSo, $"W Chủ hồ sơ 1 {suffix}");
            w.Owner2 = await factory.CreateUserAsync(w.Dept1, w.Cell1, ApprovalAuthority.CapTren, $"W Chủ hồ sơ 2 {suffix}");
            w.Owner3 = await factory.CreateUserAsync(w.Dept2, w.Cell2, ApprovalAuthority.CoSo, $"W Chủ hồ sơ 3 {suffix}");
            w.DeptLead = await factory.CreateUserAsync(w.Dept1, null, ApprovalAuthority.CoSo, $"W Lãnh đạo Phòng {suffix}");
            var cellSec = await factory.CreateUserAsync(null, null);
            w.Secretary = await factory.CreateUserAsync(null, null);
            w.AppraiserUser = await factory.CreateUserAsync(w.Dept2, w.Cell2, ApprovalAuthority.CoSo, $"W Thẩm định {suffix}");
            w.Appraiser2User = await factory.CreateUserAsync(null, null);
            var director = await factory.CreateUserAsync(null, null);
            var office = await factory.CreateUserAsync(null, null);
            var localDecider = await factory.CreateUserAsync(null, null);
            var externalDecider = await factory.CreateUserAsync(null, null);
            var plain = await factory.CreateUserAsync(w.Dept1, w.Cell1);

            foreach (var user in new[] { w.Owner1, w.Owner2, w.Owner3, w.DeptLead, w.AppraiserUser })
                await factory.AssignAsync(user.Id, evaluatee, RoleScopeType.Global, null);
            await factory.AssignAsync(w.DeptLead.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, w.Dept1);
            await factory.AssignAsync(cellSec.Id, await Role("CHI_UY_CHI_BO"), RoleScopeType.PartyCell, w.Cell1);
            await factory.AssignAsync(w.Secretary.Id, await Role("THU_KY_TAP_THE"), RoleScopeType.Department, w.Dept1);
            await factory.AssignAsync(w.AppraiserUser.Id, await Role("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await factory.AssignAsync(w.Appraiser2User.Id, (await factory.CreateRoleAsync("evaluation.read", "evaluation.appraise")).Id, RoleScopeType.Global, null);
            await factory.AssignAsync(director.Id, await Role("CAP_TRUC_TIEP_SU_DUNG"), RoleScopeType.Global, null);
            await factory.AssignAsync(office.Id, await Role("VAN_PHONG_DANG_UY"), RoleScopeType.Global, null);
            await factory.AssignAsync(localDecider.Id, (await factory.CreateRoleAsync("evaluation.read", "evaluation.decide")).Id, RoleScopeType.Global, null);
            await factory.AssignAsync(externalDecider.Id, (await factory.CreateRoleAsync("evaluation.read", "evaluation.external.record")).Id, RoleScopeType.Global, null);

            Task<HttpClient> Login(TestUser user) => factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            w.Owner1C = await Login(w.Owner1);
            w.Owner2C = await Login(w.Owner2);
            w.Owner3C = await Login(w.Owner3);
            w.DeptLeadC = await Login(w.DeptLead);
            w.CellSecC = await Login(cellSec);
            w.SecretaryC = await Login(w.Secretary);
            w.AppraiserC = await Login(w.AppraiserUser);
            w.Appraiser2C = await Login(w.Appraiser2User);
            w.DirectorC = await Login(director);
            w.OfficeC = await Login(office);
            w.LocalDeciderC = await Login(localDecider);
            w.ExternalDeciderC = await Login(externalDecider);
            w.PlainC = await Login(plain);
            return w;
        }
    }

    #endregion
}
