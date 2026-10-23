using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Luồng đánh giá theo cấu hình kỳ; mỗi test tự tạo kỳ riêng (năm ngẫu nhiên) để không phụ thuộc thứ tự chạy.</summary>
[Collection(ApiCollection.Name)]
public sealed class EvaluationWorkflowTests
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private readonly ApiFactory _factory;

    public EvaluationWorkflowTests(ApiFactory factory) => _factory = factory;

    #region W1, W2 — đi hết luồng

    [SkippableFact]
    public async Task W1_Criteria09A_ManualAndDepartmentParticipants_RecordWalksNineStepsToPublished()
    {
        var w = await WorldAsync();

        // Tạo kỳ → dự thảo, mọi hồ sơ luồng có bước đăng ký sản phẩm.
        var period = await CreatePeriodAsync(w);
        Assert.Equal("Draft", period.GetProperty("status").GetString());
        Assert.All(period.GetProperty("settings").GetProperty("profiles").EnumerateArray(), p =>
            Assert.Equal("Internal", p.GetProperty("steps").GetProperty("B1_REGISTER").GetProperty("mode").GetString()));
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
        // Mẫu 01: thiếu cột bắt buộc → 400 nêu dòng và cột; việc đột xuất không đăng ký đầu kỳ.
        var incomplete = await PostAsync(w.Owner1C, recordId, "tasks/submit", new
        {
            tasks = new[]
            {
                new { taskName = "Sản phẩm 1", targetOutput = "Kết quả 1", weight = 30.0, kind = "TrongTam" },
                new { taskName = "Sản phẩm 2", targetOutput = "Kết quả 2", weight = 20.0, kind = "TrongTam" },
                new { taskName = "Sản phẩm 3", targetOutput = "Kết quả 3", weight = 20.0, kind = "TrongTam" }
            }
        }, await VersionAsync(w, recordId));
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        Assert.Contains("Dòng 1: chưa nhập vai trò, trục kết quả, tiêu chí vượt chuẩn, nguồn minh chứng, thẩm quyền xác nhận", await MessageAsync(incomplete));
        var unplanned = await PostAsync(w.Owner1C, recordId, "tasks/submit", new
        {
            tasks = new[]
            {
                new { taskName = "Sản phẩm 1", targetOutput = "Kết quả 1", weight = 30.0, axisCode = "T1", kind = "DotXuat", role = "ChuTri", exceedCriteria = "x", evidenceSource = "x", confirmer = "x" },
                new { taskName = "Sản phẩm 2", targetOutput = "Kết quả 2", weight = 20.0, axisCode = "T1", kind = "TrongTam", role = "ChuTri", exceedCriteria = "x", evidenceSource = "x", confirmer = "x" },
                new { taskName = "Sản phẩm 3", targetOutput = "Kết quả 3", weight = 20.0, axisCode = "T1", kind = "TrongTam", role = "ChuTri", exceedCriteria = "x", evidenceSource = "x", confirmer = "x" }
            }
        }, await VersionAsync(w, recordId));
        Assert.Equal(HttpStatusCode.BadRequest, unplanned.StatusCode);
        Assert.Contains("Dòng 1: loại việc khi đăng ký", await MessageAsync(unplanned));

        var submitted = await StepAsync(w.Owner1C, recordId, "tasks/submit", Tasks(), "AwaitingTaskApproval");
        var firstTask = submitted.GetProperty("tasks")[0];
        Assert.Equal("TrongTam", firstTask.GetProperty("kind").GetString());
        Assert.Equal("ChuTri", firstTask.GetProperty("role").GetString());
        Assert.Equal("T1", firstTask.GetProperty("axisCode").GetString());
        Assert.Equal("Giám đốc", firstTask.GetProperty("confirmer").GetString());
        // 2. B1_APPROVE — Lãnh đạo Phòng 1
        await AssertActionsAsync(w.DeptLeadC, recordId, "ApproveTasks", "ReturnTasks");
        await StepAsync(w.DeptLeadC, recordId, "tasks/approve", new { comment = "Đồng ý danh mục" }, "AwaitingSelfScore");
        // 3. B2_SELF_SCORE — 09A theo nhiệm vụ
        var record = await Data(w.Owner1C, $"/api/evaluations/records/{recordId}");
        var taskScores = record.GetProperty("tasks").EnumerateArray()
            .Select(t => new { taskId = t.GetProperty("id").GetGuid(), criteriaA_Ratio = 1.0, criteriaB_Ratio = 0.9, criteriaC_Ratio = 1.0, criteriaD_Ratio = 1.0, isExceedStandard = false })
            .ToArray();
        var scored = await StepAsync(w.Owner1C, recordId, "self-score/submit",
            new { generalScores = CriteriaTestData.General("2.1"), taskScores, selfProposedGrade = "HoanThanhTot" }, "AwaitingCellConfirm");
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
        // Thư ký phạm vi Phòng thấy biên bản của Phòng mình.
        Assert.Contains(await Ids(w.SecretaryC, $"/api/evaluations/meetings?periodId={periodId}"), id => id == meetingId);
        // 6. B3B_APPRAISAL
        await StepAsync(w.AppraiserC, recordId, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 92.5, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");
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

        // Kết quả kiểm phiếu chỉ có tổng hợp; audit bảng kiểm phiếu chỉ ghi người nhập (thư ký).
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
    public async Task W1b_Period_DuplicateQuarterRejected_EmptyPeriodCannotOpen_JsonTypeErrorInVietnamese()
    {
        var w = await WorldAsync();
        var period = await CreatePeriodAsync(w);
        var periodId = period.GetProperty("id").GetGuid();

        // Hai kỳ cùng năm, quý → 409.
        var duplicate = await w.Appraiser.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = period.GetProperty("year").GetInt32(), quarter = TestPeriodSlots.Quarter,
            criteriaSetId = period.GetProperty("criteriaSetId").GetGuid(),
            name = "Kỳ trùng quý", startDate = DateTime.UtcNow, endDate = DateTime.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("Mỗi quý chỉ có một kỳ", await MessageAsync(duplicate));

        // Tạo kỳ phải chọn bộ tiêu chí.
        var noCriteria = await w.Appraiser.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter,
            name = "Kỳ chưa chọn bộ", startDate = DateTime.UtcNow, endDate = DateTime.UtcNow.AddDays(30)
        });
        Assert.Equal(HttpStatusCode.BadRequest, noCriteria.StatusCode);
        Assert.Contains("Hãy chọn bộ tiêu chí", await MessageAsync(noCriteria));

        // Kỳ chưa có người được đánh giá → không mở được (kể cả mở bắt buộc).
        var open = await TransitionRawAsync(w.Appraiser, periodId, "open", "Thử mở kỳ rỗng");
        Assert.Equal(HttpStatusCode.BadRequest, open.StatusCode);
        Assert.Contains("chưa có người được đánh giá", await MessageAsync(open));

        // Hồ sơ mới thêm bắt đầu ở bước đăng ký sản phẩm.
        var recordId = await AddParticipantAsync(w, periodId, w.Owner1.Id);
        var participants = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants");
        Assert.Equal("AwaitingRegistration",
            participants.EnumerateArray().Single(p => p.GetProperty("recordId").GetGuid() == recordId).GetProperty("status").GetString());

        // JSON sai kiểu → thông báo tiếng Việt nêu tên trường, không lộ thông báo kỹ thuật tiếng Anh.
        var badJson = await w.Appraiser.PostAsJsonAsync("/api/evaluations/periods", new { year = "abc", quarter = 1, name = "x" });
        var badText = await MessageAsync(badJson);
        Assert.Equal(HttpStatusCode.BadRequest, badJson.StatusCode);
        Assert.Contains("không đúng kiểu dữ liệu", badText);
        Assert.DoesNotContain("The JSON value", badText);
    }

    #endregion

    #region W3–W8 — quyền, trả lại, thứ tự, cấp quyết định, mở lại, đồng thời

    [SkippableFact]
    public async Task W3_NoPermissionOrOutOfScope_Is403_OwnerCannotApproveOwnRecord()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id, w.Owner3.Id, w.DeptLead.Id, w.AppraiserUser.Id);
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
            new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, rAppraiser));
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
        var periodId = await OpenPeriodAsync(w, w.Owner2.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner2.Id);
        // Người diện cấp trên mặc định đi luồng "cap-tren" (không trả lại trong hệ thống) → chuyển sang luồng cơ sở để thử trả lại.
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
        await StepAsync(w.AppraiserC, id, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 88.0, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");

        var history = await Data(w.Owner2C, $"/api/evaluations/records/{id}/history");
        var returns = history.EnumerateArray().Where(h => h.GetProperty("action").GetString() == "Return").ToList();
        Assert.Equal(new[] { "B1_APPROVE", "B2_CELL_CONFIRM", "B3B_APPRAISAL" }, returns.Select(h => h.GetProperty("step").GetString()));
        Assert.All(returns, h => Assert.False(string.IsNullOrWhiteSpace(h.GetProperty("reason").GetString())));

        // Hồ sơ đã chuyển sang luồng cơ sở (B4 nội bộ) được quyết định trong hệ thống; ghi nhận kết quả cấp trên → 409.
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
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id, w.Owner3.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);

        // Gọi thẳng API bước sau khi hồ sơ còn ở bước đăng ký → 409.
        var early = await PostAsync(w.AppraiserC, id, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("Hồ sơ đang ở bước", await MessageAsync(early));
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "publish", new { }, await VersionAsync(w, id))).StatusCode);
        // Thiếu version với hành động ghi → 400.
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(w.Owner1C, id, "tasks/submit", Tasks())).StatusCode);

        // Khóa dữ liệu: chủ hồ sơ không sửa được; bước từ thẩm định trở đi vẫn chạy.
        var other = await RecordOfAsync(w, periodId, w.Owner3.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingSelfScore, withTasks: true);
        await SetStatusAsync(other, RecordStatus.AwaitingAppraisal);
        await TransitionAsync(w.Appraiser, periodId, "lock");
        var locked = await PostAsync(w.Owner1C, id, "self-score/submit", new { generalScores = CriteriaTestData.General() }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("khóa dữ liệu", await MessageAsync(locked));
        await AssertActionsAsync(w.Owner1C, id);
        await StepAsync(w.AppraiserC, other, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 85.0, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");

        // Khóa dữ liệu → Đang mở cần lý do; chỉ tiến (không mở lại kỳ đang mở).
        Assert.Equal(HttpStatusCode.BadRequest, (await TransitionRawAsync(w.Appraiser, periodId, "unlock", null)).StatusCode);
        await TransitionAsync(w.Appraiser, periodId, "unlock", "Bổ sung minh chứng theo yêu cầu BTV");
        Assert.Equal(HttpStatusCode.Conflict, (await TransitionRawAsync(w.Appraiser, periodId, "open", null)).StatusCode);
        // Đóng kỳ khi còn hồ sơ chưa công bố → 409.
        Assert.Equal(HttpStatusCode.Conflict, (await TransitionRawAsync(w.Appraiser, periodId, "close", null)).StatusCode);
        await SelfScoreAsync(w, w.Owner1C, id);
        // Kỳ đã mở: không đổi được bộ tiêu chí đã chụp.
        var period = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}");
        var clone = await Data(await w.Appraiser.PostAsJsonAsync($"/api/criteria-sets/{period.GetProperty("criteriaSetId").GetGuid()}/clone", new { }));
        var otherSet = await Data(await w.Appraiser.PostAsJsonAsync($"/api/criteria-sets/{clone.GetProperty("id").GetGuid()}/publish",
            new { version = clone.GetProperty("version").GetUInt32() }));
        var changeSet = await w.Appraiser.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
            new { version = period.GetProperty("version").GetUInt32(), criteriaSetId = otherSet.GetProperty("id").GetGuid() });
        Assert.Equal(HttpStatusCode.Conflict, changeSet.StatusCode);
        // Dọn: bộ vừa xuất bản không được là "bộ mới nhất" cho test khác.
        await Data(await w.Appraiser.PostAsJsonAsync($"/api/criteria-sets/{otherSet.GetProperty("id").GetGuid()}/archive",
            new { version = otherSet.GetProperty("version").GetUInt32() }));
    }

    [SkippableFact]
    public async Task W6_CapTrenDecisionRecordedAsExternal_CoSoDecidedInternally()
    {
        // Luồng "cap-tren": B4 chỉ ghi nhận kết quả (evaluation.external.record); hồ sơ cơ sở dùng evaluation.decide.
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id, w.Owner2.Id);
        var coSo = await RecordOfAsync(w, periodId, w.Owner1.Id);
        var capTren = await RecordOfAsync(w, periodId, w.Owner2.Id);
        await SetStatusAsync(coSo, RecordStatus.AwaitingDecision);
        await SetStatusAsync(capTren, RecordStatus.AwaitingDecision);
        var decision = new { finalGrade = "HoanThanhTot" };
        var external = new { authorityName = "BTV Đảng ủy Tổng công ty", documentNumber = "15-QĐ/ĐU", grade = "HoanThanhTot", cadreWorkProposal = "Đề xuất đưa vào quy hoạch (test)" };

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
        // Cột 13 Mẫu 14: đề xuất về công tác cán bộ ghi ở bước quyết định.
        Assert.Equal("Đề xuất đưa vào quy hoạch (test)", recorded.GetProperty("cadreWorkProposal").GetString());
        var decided = await StepAsync(w.LocalDeciderC, coSo, "decision",
            new { finalGrade = "HoanThanhTot", cadreWorkProposal = "Đề xuất luân chuyển (test)" }, "AwaitingPublish");
        Assert.Equal("Đề xuất luân chuyển (test)", decided.GetProperty("cadreWorkProposal").GetString());
    }

    [SkippableFact]
    public async Task W7_Published_IsLocked_ReopenWithReason_EditAndRepublish_HistoryKeepsAll()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner3.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner3.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingDecision);
        await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        await StepAsync(w.OfficeC, id, "publish", new { }, "Published");

        // Đã công bố → sửa bị chặn.
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.AppraiserC, id, "appraisal",
            new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 80.0, comment = "x", proposedGrade = "HoanThanh" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "publish", new { }, await VersionAsync(w, id))).StatusCode);
        await AssertActionsAsync(w.OfficeC, id, "Reopen");
        await AssertActionsAsync(w.Owner3C, id);

        // Mở lại: bắt buộc lý do; không sớm hơn bước tự chấm.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "reopen", new { targetStep = "B3B_APPRAISAL", reason = "" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "reopen", new { targetStep = "B1_REGISTER", reason = "Đính chính" }, await VersionAsync(w, id))).StatusCode);
        await StepAsync(w.OfficeC, id, "reopen", new { targetStep = "B3B_APPRAISAL", reason = "Kiến nghị của cá nhân được chấp nhận" }, "AwaitingAppraisal");

        await StepAsync(w.AppraiserC, id, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 91.0, comment = "Thẩm định lại", proposedGrade = "HoanThanhXuatSac" }, "AwaitingDirectorReview");
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
    public async Task W7b_LockedPeriod_ReopenToSelfScore_UnlocksOnlyThatRecord_ClearedAfterResubmission()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id, w.Owner3.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);
        var other = await RecordOfAsync(w, periodId, w.Owner3.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingDecision, withTasks: true);
        await SetStatusAsync(other, RecordStatus.AwaitingSelfScore, withTasks: true);
        await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        await StepAsync(w.OfficeC, id, "publish", new { }, "Published");
        await TransitionAsync(w.Appraiser, periodId, "lock");

        // Hành động mở lại báo bước nào cần hạn mở riêng.
        var reopenAction = (await Data(w.OfficeC, $"/api/evaluations/records/{id}/actions")).GetProperty("actions").EnumerateArray()
            .Single(a => a.GetProperty("action").GetString() == "Reopen");
        Assert.True(reopenAction.GetProperty("requiresUnlockDeadline").GetBoolean());
        var unlockSteps = reopenAction.GetProperty("unlockDeadlineSteps").EnumerateArray().Select(s => s.GetString()).ToList();
        Assert.Contains("B2_SELF_SCORE", unlockSteps);
        Assert.DoesNotContain("B3B_APPRAISAL", unlockSteps);

        // Thiếu hạn / hạn đã qua / quá dài → 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "reopen",
            new { targetStep = "B2_SELF_SCORE", reason = "Bổ sung minh chứng" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "reopen",
            new { targetStep = "B2_SELF_SCORE", reason = "Bổ sung minh chứng", unlockUntil = DateTime.UtcNow.AddHours(-1) }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "reopen",
            new { targetStep = "B2_SELF_SCORE", reason = "Bổ sung minh chứng", unlockUntil = DateTime.UtcNow.AddDays(90) }, await VersionAsync(w, id))).StatusCode);

        var reopened = await StepAsync(w.OfficeC, id, "reopen",
            new { targetStep = "B2_SELF_SCORE", reason = "Bổ sung minh chứng", unlockUntil = DateTime.UtcNow.AddDays(7) }, "AwaitingSelfScore");
        Assert.True(reopened.GetProperty("unlockActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, reopened.GetProperty("unlockedUntil").ValueKind);

        // Chủ hồ sơ thao tác được, hồ sơ có trong việc cần xử lý; hồ sơ khác trong kỳ vẫn khóa.
        await AssertActionsAsync(w.Owner1C, id, "SubmitSelfScore");
        Assert.Contains(id, await QueueAsync(w.Owner1C));
        await AssertActionsAsync(w.Owner3C, other);
        Assert.DoesNotContain(other, await QueueAsync(w.Owner3C));
        var blocked = await PostAsync(w.Owner3C, other, "self-score/submit", new { generalScores = CriteriaTestData.General() }, await VersionAsync(w, other));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("khóa dữ liệu", await MessageAsync(blocked));

        // Nộp lại → Chi bộ xác nhận → tập thể vẫn trong thời gian mở riêng; tới thẩm định thì kết thúc mở riêng.
        await SelfScoreAsync(w, w.Owner1C, id);
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        var atAppraisal = await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");
        Assert.Equal(JsonValueKind.Null, atAppraisal.GetProperty("unlockedUntil").ValueKind);
        Assert.False(atAppraisal.GetProperty("unlockActive").GetBoolean());

        var history = (await Data(w.Owner1C, $"/api/evaluations/records/{id}/history")).EnumerateArray().ToList();
        var grant = Assert.Single(history, h => h.GetProperty("action").GetString() == "GrantUnlock");
        Assert.Equal("Bổ sung minh chứng", grant.GetProperty("reason").GetString());
        Assert.Single(history, h => h.GetProperty("action").GetString() == "ClearUnlock");
    }

    [SkippableFact]
    public async Task W7c_LockedPeriod_ReturnByAppraiser_UnlockExpires_ExtendRestoresOwner()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingAppraisal, withTasks: true);
        await TransitionAsync(w.Appraiser, periodId, "lock");

        var returnAction = (await Data(w.AppraiserC, $"/api/evaluations/records/{id}/actions")).GetProperty("actions").EnumerateArray()
            .Single(a => a.GetProperty("action").GetString() == "ReturnByAppraiser");
        Assert.True(returnAction.GetProperty("requiresUnlockDeadline").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.AppraiserC, id, "appraisal/return",
            new { reason = "Điểm tự chấm chưa khớp minh chứng" }, await VersionAsync(w, id))).StatusCode);

        var returned = await StepAsync(w.AppraiserC, id, "appraisal/return",
            new { reason = "Điểm tự chấm chưa khớp minh chứng", unlockUntil = DateTime.UtcNow.AddDays(3) }, "AwaitingSelfScore");
        Assert.True(returned.GetProperty("unlockActive").GetBoolean());
        await AssertActionsAsync(w.Owner1C, id, "SubmitSelfScore");

        // Hết hạn → hồ sơ khóa lại như các hồ sơ khác.
        await _factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.FirstAsync(r => r.Id == id);
            record.UnlockedUntil = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        });
        await AssertActionsAsync(w.Owner1C, id);
        Assert.DoesNotContain(id, await QueueAsync(w.Owner1C));
        var expired = await PostAsync(w.Owner1C, id, "self-score/submit", new { generalScores = CriteriaTestData.General() }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Conflict, expired.StatusCode);
        Assert.Contains("Thời hạn mở riêng", await MessageAsync(expired));
        Assert.False((await Data(w.Owner1C, $"/api/evaluations/records/{id}")).GetProperty("unlockActive").GetBoolean());

        // Gia hạn: chủ hồ sơ không tự gia hạn; người thẩm định thấy hành động và gia hạn (bắt buộc lý do).
        var extendAction = (await Data(w.AppraiserC, $"/api/evaluations/records/{id}/actions")).GetProperty("actions").EnumerateArray()
            .Single(a => a.GetProperty("action").GetString() == "ExtendUnlock");
        Assert.True(extendAction.GetProperty("requiresUnlockDeadline").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.Owner1C, id, "unlock/extend",
            new { reason = "Tự gia hạn", unlockUntil = DateTime.UtcNow.AddDays(2) }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.AppraiserC, id, "unlock/extend",
            new { reason = " ", unlockUntil = DateTime.UtcNow.AddDays(2) }, await VersionAsync(w, id))).StatusCode);
        var extended = await StepAsync(w.AppraiserC, id, "unlock/extend",
            new { reason = "Gia hạn bổ sung minh chứng", unlockUntil = DateTime.UtcNow.AddDays(2) }, "AwaitingSelfScore");
        Assert.True(extended.GetProperty("unlockActive").GetBoolean());
        await AssertActionsAsync(w.Owner1C, id, "SubmitSelfScore");
        Assert.Contains(id, await QueueAsync(w.Owner1C));

        var history = (await Data(w.Owner1C, $"/api/evaluations/records/{id}/history")).EnumerateArray().ToList();
        Assert.Equal(2, history.Count(h => h.GetProperty("action").GetString() == "GrantUnlock"));
    }

    [SkippableFact]
    public async Task W8_TwoUsersSameRecord_SecondGets409()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id, w.Owner3.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);
        await SetStatusAsync(id, RecordStatus.AwaitingAppraisal);

        // (a) Người quản lý kỳ sửa ảnh chụp (không đổi bước) → phiên bản đổi → người thẩm định dùng phiên bản cũ nhận 409.
        var stale = await VersionAsync(w, id);
        var participant = (await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("recordId").GetGuid() == id);
        var snapshot = await w.Appraiser.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{id}/snapshot", new
        {
            version = participant.GetProperty("version").GetUInt32(),
            // Hồ sơ đã qua bước tự chấm: khung tỷ trọng khóa, sửa cấp quyết định.
            departmentId = w.Dept1, partyCellId = w.Cell1,
            approvalAuthority = participant.GetProperty("approvalAuthority").GetString() == "CoSo" ? "CapTren" : "CoSo",
            reason = "Điều chỉnh cấp quyết định"
        });
        Assert.Equal(HttpStatusCode.OK, snapshot.StatusCode);
        var conflict = await PostAsync(w.Appraiser2C, id, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("tải lại", await MessageAsync(conflict));

        // (b) Hai người thẩm định gửi cùng lúc với cùng phiên bản → đúng một người thành công, người còn lại 409.
        var version = await VersionAsync(w, id);
        var body = new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 90.0, comment = "Đồng thời", proposedGrade = "HoanThanhTot" };
        var results = await Task.WhenAll(
            PostAsync(w.AppraiserC, id, "appraisal", body, version),
            PostAsync(w.Appraiser2C, id, "appraisal", body, version));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
    }

    #endregion

    #region W9, W10 — việc cần xử lý, thêm người được đánh giá

    [SkippableFact]
    public async Task W9_WorkQueue_ContainsOnlyRecordsWaitingForCurrentUser_InScope()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id, w.Owner2.Id, w.Owner3.Id);
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
    public async Task W10_AddParticipants_SnapshotAndGuards()
    {
        var w = await WorldAsync();
        var period = await CreatePeriodAsync(w);
        var periodId = period.GetProperty("id").GetGuid();

        // Không có period.manage → không thêm được người được đánh giá.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await w.OfficeC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.Owner1.Id } })).StatusCode);

        // Cán bộ không tồn tại → bỏ qua kèm lý do; người hợp lệ vẫn được thêm.
        var first = await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Owner1.Id, Guid.NewGuid() } }));
        Assert.Equal(1, first.GetProperty("added").GetInt32());
        Assert.Contains(first.GetProperty("skipped").EnumerateArray(), s => s.GetString()!.Contains("Không tìm thấy cán bộ"));
        var second = await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Owner3.Id } }));
        Assert.Equal(1, second.GetProperty("added").GetInt32());

        var participants = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants");
        Assert.Equal(new[] { w.Owner1.Id, w.Owner3.Id }.OrderBy(x => x),
            participants.EnumerateArray().Select(p => p.GetProperty("memberId").GetGuid()).OrderBy(x => x));
        var owner3 = participants.EnumerateArray().First(p => p.GetProperty("memberId").GetGuid() == w.Owner3.Id);
        Assert.Equal(w.Dept2, owner3.GetProperty("departmentId").GetGuid()); // ảnh chụp Phòng tại thời điểm thêm

        // Thêm lại người đã có → bỏ qua "đã có", không tạo thêm hồ sơ.
        var again = await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Owner1.Id } }));
        Assert.Equal(0, again.GetProperty("added").GetInt32());
        Assert.Contains(again.GetProperty("skipped").EnumerateArray(), s => s.GetString()!.Contains("đã có trong danh sách"));
    }

    #endregion

    #region Thẩm định theo nhóm điểm và mức từng nhiệm vụ

    [SkippableFact]
    public async Task Appraisal_09A_RequiresLevelForEveryTask_SumsGroupScores_FillsMau10()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);
        await StepAsync(w.Owner1C, id, "tasks/submit", Tasks(), "AwaitingTaskApproval");
        await StepAsync(w.DeptLeadC, id, "tasks/approve", new { }, "AwaitingSelfScore");
        await SelfScoreAsync(w, w.Owner1C, id);
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");
        var taskIds = (await Data(w.AppraiserC, $"/api/evaluations/records/{id}")).GetProperty("tasks").EnumerateArray()
            .Select(t => t.GetProperty("id").GetGuid()).ToList();
        Assert.Equal(3, taskIds.Count);

        // Thiếu mức của "Sản phẩm 2", "Sản phẩm 3" → 400 nêu tên việc.
        var missing = await PostAsync(w.AppraiserC, id, "appraisal", new
        {
            generalScore = 27.0, tasksScore = 60.5, explanation = "Căn cứ thẩm định (test)", proposedGrade = "HoanThanhTot",
            taskLevels = new[] { new { taskId = taskIds[0], level = 1 } }
        }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        var message = await MessageAsync(missing);
        Assert.Contains("\"Sản phẩm 2\"", message);
        Assert.Contains("\"Sản phẩm 3\"", message);
        Assert.DoesNotContain("\"Sản phẩm 1\"", message);

        object Levels(int first) => taskIds.Select((t, i) => new { taskId = t, level = i == 0 ? first : 1, comment = i == 0 ? "Chậm tiến độ" : null }).ToArray();

        // Mức ngoài 1..3 → 400; chỉ gửi một nhóm điểm → 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.AppraiserC, id, "appraisal", new
        {
            generalScore = 27.0, tasksScore = 60.5, explanation = "Căn cứ", proposedGrade = "HoanThanhTot", taskLevels = Levels(4)
        }, await VersionAsync(w, id))).StatusCode);
        var oneGroup = await PostAsync(w.AppraiserC, id, "appraisal", new
        {
            generalScore = 27.0, explanation = "Căn cứ", proposedGrade = "HoanThanhTot", taskLevels = Levels(1)
        }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.BadRequest, oneGroup.StatusCode);
        Assert.Contains("nhóm kết quả thực hiện nhiệm vụ", await MessageAsync(oneGroup));

        // Nhiệm vụ trọng tâm "Sản phẩm 1" Mức 3 mà đề xuất Hoàn thành tốt → vẫn lưu (chỉ cảnh báo); tổng = chung + nhiệm vụ.
        var appraised = await StepAsync(w.AppraiserC, id, "appraisal", new
        {
            generalScore = 27.0, tasksScore = 60.5, appraisalScore = 1.0, explanation = "Căn cứ thẩm định (test)", comment = "Đạt",
            proposedGrade = "HoanThanhTot", taskLevels = Levels(3)
        }, "AwaitingDirectorReview");
        Assert.Equal(27.0, appraised.GetProperty("appraisalGeneralScore").GetDouble(), 6);
        Assert.Equal(60.5, appraised.GetProperty("appraisalTasksScore").GetDouble(), 6);
        Assert.Equal(87.5, appraised.GetProperty("appraisalScore").GetDouble(), 6);
        var tasks = appraised.GetProperty("tasks").EnumerateArray().ToList();
        Assert.Equal(3, tasks[0].GetProperty("appraisalLevel").GetInt32());
        Assert.Equal("Chậm tiến độ", tasks[0].GetProperty("appraisalComment").GetString());
        Assert.All(tasks.Skip(1), t => Assert.Equal(1, t.GetProperty("appraisalLevel").GetInt32()));
        Assert.Contains(appraised.GetProperty("gradeWarnings").EnumerateArray(), x => x.GetString()!.Contains("Mức 3"));
        Assert.Equal(0.30, appraised.GetProperty("excellentMinExceedRatio").GetDouble(), 6);

        // Mẫu 10 có điểm thẩm định theo nhóm và tổng.
        var docx = await w.AppraiserC.GetAsync($"/api/reports/docx/mau-10/{id}");
        var bytes = await docx.Content.ReadAsByteArrayAsync();
        Assert.True(docx.IsSuccessStatusCode, $"{(int)docx.StatusCode}: {System.Text.Encoding.UTF8.GetString(bytes)}");
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(bytes), System.IO.Compression.ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var text = System.Text.RegularExpressions.Regex.Replace(await reader.ReadToEndAsync(), "<[^>]+>", string.Empty);
        Assert.Contains("60,5", text);
        Assert.Contains("87,5", text);
    }

    #endregion

    #region Mức đề xuất so với mức theo điểm, kiểm phiếu, cách xét duyệt theo chức vụ

    [SkippableFact]
    public async Task W20_ProposalAboveScoreGrade_EveryStep_RequiresReason_ReturnsWarnings()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);
        var registered = await RegisterAsync(w, id);
        // 29 + 57 (70 × 57/70) = 86 điểm → mức theo điểm "Hoàn thành tốt" (Xuất sắc từ 90).
        var selfScore = new { generalScores = CriteriaTestData.General("2.1"), taskScores = CriteriaTestData.TaskScores(registered, 57.0 / 70), selfProposedGrade = "HoanThanhXuatSac" };

        // Lỗi cũ: tự đề xuất Xuất sắc với 86 điểm vẫn được nhận, không cảnh báo.
        var noReason = await PostAsync(w.Owner1C, id, "self-score/submit", selfScore, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var message = await MessageAsync(noReason);
        Assert.Contains("cao hơn mức theo điểm", message);
        Assert.Contains("86 điểm", message);
        var scored = await StepAsync(w.Owner1C, id, "self-score/submit", new
        {
            selfScore.generalScores, selfScore.taskScores, selfScore.selfProposedGrade, selfGradeExplanation = "Có sáng kiến được Tổng công ty khen"
        }, "AwaitingCellConfirm");
        Assert.Equal("Có sáng kiến được Tổng công ty khen", scored.GetProperty("selfGradeExplanation").GetString());
        var deviation = Assert.Single(scored.GetProperty("gradeDeviations").EnumerateArray());
        Assert.Equal("B2_SELF_SCORE", deviation.GetProperty("step").GetString());
        Assert.Equal("Higher", deviation.GetProperty("direction").GetString());
        Assert.Equal("HoanThanhTot", deviation.GetProperty("scoreGrade").GetString());
        Assert.Contains(scored.GetProperty("gradeWarnings").EnumerateArray(), x => x.GetString()!.Contains("cao hơn mức theo điểm"));

        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        // Tập thể: Xuất sắc cần nhận xét nêu lý do; khớp mức theo điểm thì không bắt buộc.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhXuatSac" }, await VersionAsync(w, id))).StatusCode);
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");

        // Lỗi cũ: thẩm định 30 + 57 = 87 (chênh 1 điểm, cùng mức) đề xuất Xuất sắc không phải giải trình.
        var appraisal = new { generalScore = 30.0, tasksScore = 57.0, comment = "Đạt", proposedGrade = "HoanThanhXuatSac" };
        var appraiseNoReason = await PostAsync(w.AppraiserC, id, "appraisal", appraisal, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.BadRequest, appraiseNoReason.StatusCode);
        Assert.Contains("87 điểm", await MessageAsync(appraiseNoReason));
        var appraised = await StepAsync(w.AppraiserC, id, "appraisal", new
        {
            appraisal.generalScore, appraisal.tasksScore, appraisal.comment, appraisal.proposedGrade, explanation = "Kết quả vượt trội sản phẩm 1"
        }, "AwaitingDirectorReview");
        Assert.Contains(appraised.GetProperty("gradeWarnings").EnumerateArray(), x => x.GetString()!.StartsWith("Mức thẩm định đề xuất"));

        // Cấp trực tiếp sử dụng hạ về mức theo điểm: không bắt buộc nhận xét; hạ thấp hơn mức theo điểm thì phải nêu căn cứ.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.DirectorC, id, "director-review", new { proposedGrade = "HoanThanh" }, await VersionAsync(w, id))).StatusCode);
        await StepAsync(w.DirectorC, id, "director-review", new { proposedGrade = "HoanThanhTot" }, "AwaitingDecision");

        var decideNoReason = await PostAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhXuatSac" }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.BadRequest, decideNoReason.StatusCode);
        Assert.Contains("Mức được quyết định", await MessageAsync(decideNoReason));
        var decided = await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhXuatSac", explanation = "Hội nghị xét thành tích đột xuất" }, "AwaitingPublish");
        Assert.Equal("Hội nghị xét thành tích đột xuất", decided.GetProperty("decisionExplanation").GetString());
        Assert.Contains(decided.GetProperty("gradeDeviations").EnumerateArray(), d => d.GetProperty("step").GetString() == "B4_DECISION");
    }

    [SkippableFact]
    public async Task W21_CollectiveProposal_MustMatchVoteMajority()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Owner1.Id);
        var id = await RecordOfAsync(w, periodId, w.Owner1.Id);
        // 30 + 64 = 94 điểm, không sản phẩm vượt chuẩn → mức theo điểm Hoàn thành tốt; kiểm tra riêng quy tắc kiểm phiếu.
        var registered = await RegisterAsync(w, id);
        await StepAsync(w.Owner1C, id, "self-score/submit",
            new { generalScores = CriteriaTestData.General(), taskScores = CriteriaTestData.TaskScores(registered, 64.0 / 70) }, "AwaitingCellConfirm");
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        var meetingId = (await Data(await w.SecretaryC.PostAsJsonAsync("/api/evaluations/meetings", new
        {
            periodId, departmentId = w.Dept1, stage = "B3A_COLLECTIVE", formCode = "M12", meetingType = "Hội nghị tập thể lãnh đạo Phòng",
            invitedCount = 5, presentCount = 5
        }))).GetProperty("id").GetGuid();
        var votes = new { votesExcellent = 1, votesGood = 4, votesSatisfactory = 0, votesUnsatisfactory = 0, votesNotRated = 0, invalidVotes = 0 };

        // Lỗi cũ: phiếu 1 Xuất sắc / 4 Tốt vẫn ghi nhận được tập thể đề xuất Xuất sắc.
        var mismatch = await PostAsync(w.SecretaryC, id, "collective",
            new { proposedGrade = "HoanThanhXuatSac", meetingId, votes }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        var message = await MessageAsync(mismatch);
        Assert.Contains("Xuất sắc 1, Tốt 4", message);
        Assert.Contains("\"Hoàn thành tốt nhiệm vụ\" (5/5 phiếu)", message);

        // Không mức nào quá nửa (phiếu "Chưa đánh giá" chiếm đa số) → từ chối, nêu số phiếu.
        var noMajority = await PostAsync(w.SecretaryC, id, "collective", new
        {
            proposedGrade = "HoanThanhTot", comment = "Hạ mức", meetingId,
            votes = new { votesExcellent = 1, votesGood = 1, votesSatisfactory = 0, votesUnsatisfactory = 0, votesNotRated = 3, invalidVotes = 0 }
        }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.BadRequest, noMajority.StatusCode);
        Assert.Contains("Không mức nào đạt quá nửa", await MessageAsync(noMajority));

        // Kết quả kiểm phiếu đã lưu trên biên bản (không gửi lại phiếu) vẫn được đối chiếu.
        await _factory.WithDbAsync(async db =>
        {
            db.EvaluationMeetingVoteSummaries.Add(new EvaluationMeetingVoteSummary { MeetingId = meetingId, RecordId = id, VotesExcellent = 1, VotesGood = 4 });
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PostAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhXuatSac", meetingId }, await VersionAsync(w, id))).StatusCode);
        await StepAsync(w.SecretaryC, id, "collective",
            new { proposedGrade = "HoanThanhTot", comment = "Theo kết quả biểu quyết của hội nghị", meetingId, votes }, "AwaitingAppraisal");
    }

    [SkippableFact]
    public async Task W22_DeputyDirector_DefaultProfileFromPosition_ReadinessWarnsWhenDifferent()
    {
        var w = await WorldAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6];
        var deputy = await _factory.CreateUserAsync(w.Dept1, w.Cell1, ApprovalAuthority.CapTren, $"W Phó Giám đốc {suffix}");
        var director = await _factory.CreateUserAsync(w.Dept1, w.Cell1, ApprovalAuthority.CapTren, $"W Giám đốc {suffix}");
        await _factory.WithDbAsync(async db =>
        {
            // Danh mục mặc định đánh dấu sẵn chức vụ Phó Giám đốc (DataSeeder / migration).
            var deputyPosition = await db.Positions.SingleAsync(p => p.Name == "Phó Giám đốc");
            Assert.Equal("pho-giam-doc", deputyPosition.DefaultWorkflowProfileCode);
            var directorPosition = await db.Positions.SingleAsync(p => p.Name == "Giám đốc");
            Assert.Null(directorPosition.DefaultWorkflowProfileCode);
            db.MemberPositions.Add(new MemberPosition { UserId = deputy.Id, PositionId = deputyPosition.Id, DepartmentId = w.Dept1, IsPrimary = true, ValidFrom = DateTime.UtcNow.AddDays(-1) });
            db.MemberPositions.Add(new MemberPosition { UserId = director.Id, PositionId = directorPosition.Id, DepartmentId = w.Dept1, IsPrimary = true, ValidFrom = DateTime.UtcNow.AddDays(-1) });
            await db.SaveChangesAsync();
        });

        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
        await Data(await w.Appraiser.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { deputy.Id, director.Id } }));
        var participants = (await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray().ToList();
        var deputyRow = participants.First(p => p.GetProperty("memberId").GetGuid() == deputy.Id);
        // Lỗi cũ: Phó Giám đốc (thẩm quyền cấp trên) rơi vào "cap-tren".
        Assert.Equal("pho-giam-doc", deputyRow.GetProperty("workflowProfileCode").GetString());
        Assert.Equal("cap-tren", participants.First(p => p.GetProperty("memberId").GetGuid() == director.Id).GetProperty("workflowProfileCode").GetString());

        var clean = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.DoesNotContain(clean.GetProperty("dataReview").EnumerateArray(), i => i.GetProperty("kind").GetString() == "ProfileMismatch");

        // Đổi tay sang cách xét duyệt khác → kiểm tra sẵn sàng nhắc (không chặn mở kỳ).
        await Data(await w.Appraiser.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{deputyRow.GetProperty("recordId").GetGuid()}/profile",
            new { version = deputyRow.GetProperty("version").GetUInt32(), workflowProfileCode = "cap-tren", reason = "Kiểm thử cảnh báo" }));
        var readiness = await Data(w.Appraiser, $"/api/evaluations/periods/{periodId}/readiness");
        var mismatch = readiness.GetProperty("dataReview").EnumerateArray().Single(i => i.GetProperty("kind").GetString() == "ProfileMismatch");
        Assert.Contains(mismatch.GetProperty("names").EnumerateArray(), n => n.GetString()!.Contains($"W Phó Giám đốc {suffix}") && n.GetString()!.Contains("Phó Giám đốc"));
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
            new { taskName = "Sản phẩm 1", targetOutput = "Kết quả 1", weight = a, axisCode = "T1", kind = "TrongTam", role = "ChuTri", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc" },
            new { taskName = "Sản phẩm 2", targetOutput = "Kết quả 2", weight = b, axisCode = "T1", kind = "TrongTam", role = "ChuTri", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc" },
            new { taskName = "Sản phẩm 3", targetOutput = "Kết quả 3", weight = c, axisCode = "T1", kind = "ThuongXuyen", role = "PhoiHop", exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc" }
        }
    };

    /// <summary>Chủ hồ sơ Owner1 đăng ký danh mục, Lãnh đạo Phòng duyệt; trả hồ sơ ở bước tự chấm.</summary>
    private async Task<JsonElement> RegisterAsync(World w, Guid recordId)
    {
        await StepAsync(w.Owner1C, recordId, "tasks/submit", Tasks(), "AwaitingTaskApproval");
        return await StepAsync(w.DeptLeadC, recordId, "tasks/approve", new { }, "AwaitingSelfScore");
    }

    private static async Task<JsonElement> CreatePeriodAsync(World w)
    {
        var year = TestPeriodSlots.NextYear();
        var body = new
        {
            year, quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.Appraiser),
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

    private static async Task<Guid> OpenPeriodAsync(World w, params Guid[] members)
    {
        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
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
        // Có bước chỉ người theo phạm vi từng Phòng thực hiện → mở bắt buộc kèm lý do (kẹt luồng có test riêng).
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
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body, uint? version = null)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (version.HasValue)
            json["version"] = version.Value;
        if (action == "appraisal")
            await AppraisalTestData.AddTaskLevelsAsync(client, recordId, json);
        return await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
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
            new { generalScores = CriteriaTestData.General("2.1", "2.2", "2.3"), taskScores }, "AwaitingCellConfirm");
    }

    /// <summary>Hồ sơ trong việc cần xử lý (các bước của luồng) của người dùng.</summary>
    private static async Task<List<Guid>> QueueAsync(HttpClient client) =>
        (await Data(client, "/api/evaluations/work-queue")).GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("items").EnumerateArray())
            .Where(i => i.TryGetProperty("recordId", out var r) && r.ValueKind == JsonValueKind.String)
            .Select(i => i.GetProperty("recordId").GetGuid())
            .ToList();

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

    /// <summary>Người dùng và vai trò dùng chung: 2 Phòng, 2 Chi bộ, người được đánh giá và các vai trò theo từng bước.</summary>
    private sealed class World
    {
        public Guid Dept1, Dept2, Cell1, Cell2;
        public TestUser Owner1 = null!, Owner2 = null!, Owner3 = null!, DeptLead = null!, Secretary = null!, Appraiser2User = null!;
        public TestUser AppraiserUser = null!;
        public HttpClient Owner1C = null!, Owner2C = null!, Owner3C = null!, DeptLeadC = null!, CellSecC = null!, SecretaryC = null!;
        public HttpClient AppraiserC = null!, Appraiser2C = null!, DirectorC = null!, OfficeC = null!, LocalDeciderC = null!, ExternalDeciderC = null!, PlainC = null!;

        /// <summary>Người quản lý kỳ (vai trò "Cơ quan thẩm định" có period.manage).</summary>
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
