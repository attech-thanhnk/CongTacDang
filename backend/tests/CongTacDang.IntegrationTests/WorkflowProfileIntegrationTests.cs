using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Luồng theo diện quyết định: Giám đốc ghi nhận kết quả cấp trên, Bí thư Chi bộ là nhân viên, kẹt luồng khi mở kỳ.</summary>
[Collection(ApiCollection.Name)]
public sealed class WorkflowProfileIntegrationTests
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();
    private static readonly Dictionary<string, CongTacDang.Domain.Evaluation.GeneralItemScore> General = CriteriaTestData.General("2.1", "2.2", "2.3");

    private readonly ApiFactory _factory;

    public WorkflowProfileIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task G1_DirectorRecord_UpperProfile_ExternalStepsRecorded_ToPublished()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Director.Id);
        var id = await RecordOfAsync(w, periodId, w.Director.Id);

        var record = await Data(w.OwnerDirectorC, $"/api/evaluations/records/{id}");
        Assert.Equal("cap-tren", record.GetProperty("workflowProfileCode").GetString());
        var modes = record.GetProperty("progress").EnumerateArray()
            .ToDictionary(p => p.GetProperty("step").GetString()!, p => p.GetProperty("mode").GetString()!);
        Assert.Equal("Internal", modes["B1_REGISTER"]);
        Assert.Equal("External", modes["B1_APPROVE"]);
        Assert.Equal("Internal", modes["B3A_COLLECTIVE"]);
        Assert.Equal("External", modes["B3B_APPRAISAL"]);
        Assert.Equal("External", modes["B3C_DIRECTOR"]);
        Assert.Equal("External", modes["B4_DECISION"]);

        await StepAsync(w.OwnerDirectorC, id, "self-score/submit", new { generalScores = General, taskScores = await RegistrationTestData.RegisterAsync(_factory, id, 62.0 / 70) }, "AwaitingCellConfirm");
        await StepAsync(w.CellSecC, id, "cell/confirm", new { comment = "Xác nhận" }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");

        // B3b do cấp trên thực hiện: cơ quan thẩm định trong hệ thống không có thao tác, gọi API thẩm định → 409; không trả lại được.
        await AssertActionsAsync(w.ManagerC, id);
        var internalAppraisal = await PostAsync(w.ManagerC, id, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Conflict, internalAppraisal.StatusCode);
        Assert.Contains("cấp trên", await MessageAsync(internalAppraisal));
        Assert.Equal(HttpStatusCode.Conflict,
            (await PostAsync(w.ManagerC, id, "appraisal/return", new { reason = "Thiếu" }, await VersionAsync(w, id))).StatusCode);
        await AssertActionsAsync(w.OfficeC, id, "RecordExternal");

        // Chủ hồ sơ có quyền ghi nhận kết quả của cấp trên vẫn không ghi nhận được trên hồ sơ của mình (xung đột lợi ích).
        var own = await PostAsync(w.OwnerDirectorC, id, "external/B3B_APPRAISAL",
            new { authorityName = "Ban TCĐU", grade = "HoanThanhTot" }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Contains("xung đột lợi ích", await MessageAsync(own));
        // Không có quyền → 403 (controller).
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.ManagerC, id, "external/B3B_APPRAISAL",
            new { authorityName = "Ban TCĐU", grade = "HoanThanhTot" }, await VersionAsync(w, id))).StatusCode);
        // Thiếu cơ quan / thiếu mức → 400; bước sai (chưa tới) → 409; mã bước không cho cấp trên → 400.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "external/B3B_APPRAISAL",
            new { authorityName = " ", grade = "HoanThanhTot" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "external/B3B_APPRAISAL",
            new { authorityName = "Ban TCĐU" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await PostAsync(w.OfficeC, id, "external/B4_DECISION",
            new { authorityName = "BTV", grade = "HoanThanhTot" }, await VersionAsync(w, id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(w.OfficeC, id, "external/B5_PUBLISH",
            new { authorityName = "BTV" }, await VersionAsync(w, id))).StatusCode);

        await StepAsync(w.OfficeC, id, "external/B3B_APPRAISAL", new
        {
            authorityName = "Ban Tổ chức Đảng ủy Tổng công ty", documentNumber = "12-TTr/BTCĐU", documentDate = "2026-12-16",
            comment = "Đủ điều kiện", grade = "HoanThanhTot", score = 88.5
        }, "AwaitingDirectorReview");
        await AssertActionsAsync(w.DirectorC, id); // Giám đốc (cấp trực tiếp sử dụng nội bộ) không làm B3c của hồ sơ này
        await StepAsync(w.OfficeC, id, "external/B3C_DIRECTOR",
            new { authorityName = "Hội đồng thành viên Tổng công ty", comment = "Đề nghị HTT", grade = "HoanThanhTot" }, "AwaitingDecision");
        var decided = await StepAsync(w.OfficeC, id, "external/B4_DECISION", new
        {
            authorityName = "BTV Đảng ủy Tổng công ty", documentNumber = "20-QĐ/ĐUTCT", documentDate = "2026-12-20", grade = "HoanThanhXuatSac"
        }, "AwaitingPublish");
        Assert.Equal("HoanThanhXuatSac", decided.GetProperty("finalGrade").GetString());
        Assert.Equal(88.5, decided.GetProperty("finalScore").GetDouble());
        Assert.Equal("Ban Tổ chức Đảng ủy Tổng công ty", decided.GetProperty("appraisedByName").GetString());
        Assert.Equal("BTV Đảng ủy Tổng công ty", decided.GetProperty("decisionAuthorityName").GetString());
        var results = decided.GetProperty("externalResults").EnumerateArray().ToList();
        Assert.Equal(new[] { "B3B_APPRAISAL", "B3C_DIRECTOR", "B4_DECISION" }, results.Select(r => r.GetProperty("step").GetString()));
        Assert.Equal("12-TTr/BTCĐU", results[0].GetProperty("documentNumber").GetString());

        var published = await StepAsync(w.OfficeC, id, "publish", new { }, "Published");
        Assert.Equal("HoanThanhXuatSac", published.GetProperty("finalGrade").GetString());

        var history = await Data(w.OwnerDirectorC, $"/api/evaluations/records/{id}/history");
        var externalSteps = history.EnumerateArray().Where(h => h.GetProperty("action").GetString() == "RecordExternal")
            .Select(h => h.GetProperty("step").GetString()).ToList();
        Assert.Equal(new[] { "B3B_APPRAISAL", "B3C_DIRECTOR", "B4_DECISION" }, externalSteps);

        // Mở lại về B4 rồi ghi nhận lại: cập nhật bản ghi kết quả cũ (không tạo thêm).
        await StepAsync(w.OfficeC, id, "reopen", new { targetStep = "B4_DECISION", reason = "Cấp trên đính chính quyết định" }, "AwaitingDecision");
        var corrected = await StepAsync(w.OfficeC, id, "external/B4_DECISION",
            new { authorityName = "BTV Đảng ủy Tổng công ty", documentNumber = "21-QĐ/ĐUTCT", grade = "HoanThanhTot" }, "AwaitingPublish");
        Assert.Equal(3, corrected.GetProperty("externalResults").GetArrayLength());
        Assert.Equal("HoanThanhTot", corrected.GetProperty("finalGrade").GetString());
    }

    [SkippableFact]
    public async Task G2_CellSecretaryStaffProfile_UnitLeaderProposesInsteadOfDirector()
    {
        var w = await WorldAsync();
        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
        // Thêm người kèm diện quyết định chỉ định.
        var added = await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Staff.Id }, workflowProfileCode = "bi-thu-nhan-vien" }));
        Assert.Equal(1, added.GetProperty("added").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Director.Id }, workflowProfileCode = "khong-co" })).StatusCode);
        await OpenAsync(w, periodId);
        var id = await RecordOfAsync(w, periodId, w.Staff.Id);
        Assert.Equal("bi-thu-nhan-vien", (await Data(w.StaffC, $"/api/evaluations/records/{id}")).GetProperty("workflowProfileCode").GetString());

        await StepAsync(w.StaffC, id, "self-score/submit", new { generalScores = General, taskScores = await RegistrationTestData.RegisterAsync(_factory, id, 62.0 / 70) }, "AwaitingCellConfirm");
        // Nhóm này không họp bỏ phiếu: sau Chi bộ xác nhận là thẩm định.
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingAppraisal");
        await StepAsync(w.ManagerC, id, "appraisal", new { explanation = "Căn cứ thẩm định (test)", appraisalScore = 87.0, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");

        // B3c của nhóm này do Lãnh đạo đơn vị (evaluation.unit.review) thực hiện, không phải Giám đốc.
        await AssertActionsAsync(w.DirectorC, id);
        var director = await PostAsync(w.DirectorC, id, "director-review", new { comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, id));
        Assert.Equal(HttpStatusCode.Forbidden, director.StatusCode);
        Assert.Contains("Lãnh đạo đơn vị đề xuất", await MessageAsync(director));
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.Lead2C, id, "director-review",
            new { comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, id))).StatusCode); // Lãnh đạo Phòng khác
        await AssertActionsAsync(w.Lead1C, id, "DirectorReview");
        var queue = await Data(w.Lead1C, $"/api/evaluations/work-queue?periodId={periodId}");
        Assert.Contains(queue.GetProperty("groups").EnumerateArray(), g => g.GetProperty("step").GetString() == "B3C_DIRECTOR");
        Assert.DoesNotContain((await Data(w.DirectorC, $"/api/evaluations/work-queue?periodId={periodId}")).GetProperty("groups").EnumerateArray(),
            g => g.GetProperty("step").GetString() == "B3C_DIRECTOR");

        await StepAsync(w.Lead1C, id, "director-review", new { comment = "Trưởng phòng đề xuất HTT", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");

        // Đã qua B3c (bước khác nhau giữa hai diện quyết định) → không đổi sang diện quyết định cơ sở được.
        var participant = await ParticipantAsync(w, periodId, id);
        var change = await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{id}/profile",
            new { version = participant.GetProperty("version").GetUInt32(), workflowProfileCode = "co-so", reason = "Nhầm nhóm" });
        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
        Assert.Contains("đã qua bước", await MessageAsync(change));

        await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        await StepAsync(w.OfficeC, id, "publish", new { }, "Published");
    }

    [SkippableFact]
    public async Task G2b_Opinions_RecordedByScopeHolder_NotByOwnerOrOtherUnit_DoNotBlockFlow()
    {
        var w = await WorldAsync();
        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
        await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.Staff.Id } });
        await OpenAsync(w, periodId);
        var id = await RecordOfAsync(w, periodId, w.Staff.Id);
        var url = $"/api/evaluations/records/{id}/opinions";

        var created = await Data(w.Lead1C, url);
        Assert.True(created.GetProperty("canRecord").GetBoolean());
        var saved = await w.Lead1C.PostAsJsonAsync(url, new { kind = "ListMeeting", content = "Đã thống nhất danh mục 5 sản phẩm" });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("Kết quả họp rà soát danh mục", (await Data(w.Lead1C, url)).GetProperty("items")[0].GetProperty("kindName").GetString());

        // Chủ hồ sơ xem được nhưng không ghi; Lãnh đạo Phòng khác không xem, không ghi.
        Assert.False((await Data(w.StaffC, url)).GetProperty("canRecord").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await w.StaffC.PostAsJsonAsync(url, new { kind = "ListCheck", content = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Lead2C.PostAsJsonAsync(url, new { kind = "ListCheck", content = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Lead2C.GetAsync(url)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await w.Lead1C.PostAsJsonAsync(url, new { kind = "ListMeeting", content = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.Lead1C.PostAsJsonAsync(url, new { kind = "khong-co", content = "x" })).StatusCode);

        // Ghi ý kiến không đổi trạng thái hồ sơ.
        Assert.Equal("AwaitingRegistration", (await Data(w.StaffC, $"/api/evaluations/records/{id}")).GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task G3_Readiness_DetectsStuckRecord_OpenReturns409_ForceNeedsReason()
    {
        var w = await WorldAsync();
        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Staff3.Id }, workflowProfileCode = "bi-thu-nhan-vien" }));

        var ready = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.True(ready.GetProperty("ready").GetBoolean(), ready.GetRawText());
        Assert.Equal(1, ready.GetProperty("checkedRecords").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await w.StaffC.GetAsync($"/api/evaluations/periods/{periodId}/readiness")).StatusCode);

        // Thu hồi bản gán Lãnh đạo Phòng 3 (người duy nhất duyệt danh mục, đề xuất của đơn vị) → hồ sơ kẹt ở B1 duyệt và B3c.
        Assert.Equal(HttpStatusCode.OK, (await w.AdminC.DeleteAsync($"/api/admin/assignments/{w.Lead3Assignment}")).StatusCode);
        var stuck = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.False(stuck.GetProperty("ready").GetBoolean());
        var issues = stuck.GetProperty("issues").EnumerateArray().ToList();
        Assert.Equal(new[] { "B1_APPROVE", "B3C_DIRECTOR" }, issues.Select(i => i.GetProperty("step").GetString()));
        var issue = issues.Single(i => i.GetProperty("step").GetString() == "B3C_DIRECTOR");
        Assert.Equal("evaluation.unit.review", issue.GetProperty("permission").GetString());
        Assert.Equal("bi-thu-nhan-vien", issue.GetProperty("workflowProfileCode").GetString());
        Assert.Contains("sẽ kẹt ở bước", issue.GetProperty("message").GetString());
        Assert.Contains(w.Dept3Name, issue.GetProperty("scope").GetString());

        // Mở kỳ → 409 kèm danh sách; mở bắt buộc thiếu lý do → 400; có lý do → mở, ghi lý do vào kỳ.
        var version = (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}")).GetProperty("version").GetUInt32();
        var blocked = await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open", new { version });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using (var json = JsonDocument.Parse(await blocked.Content.ReadAsStringAsync()))
        {
            Assert.Equal("PERIOD_NOT_READY", json.RootElement.GetProperty("code").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("data").GetProperty("issues").GetArrayLength());
            Assert.Equal(2, json.RootElement.GetProperty("errors").GetArrayLength());
        }
        Assert.Equal("Draft", (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}")).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest,
            (await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open", new { version, force = true })).StatusCode);
        var opened = await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version, force = true, reason = "Sẽ phân công Trưởng phòng ngay trong tuần" }));
        Assert.Equal("Open", opened.GetProperty("status").GetString());
        Assert.Contains("bắt buộc", opened.GetProperty("statusReason").GetString());
        Assert.Contains("Sẽ phân công Trưởng phòng", opened.GetProperty("statusReason").GetString());
    }

    [SkippableFact]
    public async Task G4_ProfileConfiguration_ChangeProfile_Bulk_AddWithProfile_AndValidation()
    {
        var w = await WorldAsync();
        var period = await CreatePeriodAsync(w);
        var periodId = period.GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Director.Id, w.Staff.Id } }));
        var participants = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants");
        var profiles = participants.EnumerateArray().ToDictionary(p => p.GetProperty("memberId").GetGuid(), p => p.GetProperty("workflowProfileCode").GetString());
        Assert.Equal("cap-tren", profiles[w.Director.Id]);   // mặc định theo cấp quyết định
        Assert.Equal("co-so", profiles[w.Staff.Id]);

        // Quyền thực hiện bước và danh mục chọn được.
        var options = await Data(w.ManagerC, "/api/evaluations/periods/step-permissions");
        Assert.Contains(options.EnumerateArray(), o => o.GetProperty("code").GetString() == "evaluation.unit.review");
        Assert.DoesNotContain(options.EnumerateArray(), o => o.GetProperty("code").GetString() == "evaluation.self");

        // Cấu hình không hợp lệ qua API → 400; xóa diện quyết định đang dùng → 409.
        async Task<HttpResponseMessage> PutSettings(Action<JsonObject> edit)
        {
            var current = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
            var settings = JsonNode.Parse(current.GetProperty("settings").GetRawText())!.AsObject();
            edit(settings);
            return await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
                new { version = current.GetProperty("version").GetUInt32(), settings });
        }
        JsonObject ProfileNode(JsonObject s, string code) =>
            s["profiles"]!.AsArray().Select(p => p!.AsObject()).First(p => p["code"]!.GetValue<string>() == code);

        var badPermission = await PutSettings(s => ProfileNode(s, "co-so")["steps"]!["B3C_DIRECTOR"]!["permission"] = "system.roles.manage");
        Assert.Equal(HttpStatusCode.BadRequest, badPermission.StatusCode);
        Assert.Contains("quyền thực hiện bước", await MessageAsync(badPermission));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PutSettings(s => ProfileNode(s, "co-so")["steps"]!["B4_DECISION"]!["mode"] = "Off")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await PutSettings(s => ProfileNode(s, "co-so")["steps"]!["B5_PUBLISH"]!["mode"] = "External")).StatusCode);
        var removeUsed = await PutSettings(s =>
        {
            var array = s["profiles"]!.AsArray();
            array.Remove(array.First(p => p!["code"]!.GetValue<string>() == "cap-tren"));
            s["defaultProfiles"]!["CapTren"] = "co-so";
        });
        Assert.Equal(HttpStatusCode.Conflict, removeUsed.StatusCode);
        Assert.Contains("cap-tren", await MessageAsync(removeUsed));

        // Thêm diện quyết định mới (sao chép "co-so", thẩm định không áp dụng) khi kỳ còn dự thảo.
        Assert.Equal(HttpStatusCode.OK, (await PutSettings(s =>
        {
            var copy = JsonNode.Parse(ProfileNode(s, "co-so").ToJsonString())!.AsObject();
            copy["code"] = "khong-tham-dinh";
            copy["name"] = "Không qua thẩm định";
            copy["steps"]!["B3B_APPRAISAL"]!["mode"] = "Off";
            s["profiles"]!.AsArray().Add(copy);
        })).StatusCode);

        // Đổi diện quyết định: thiếu lý do → 400; không tồn tại → 400; hàng loạt (1 hồ sơ sai phiên bản bị bỏ qua).
        var staffRecord = participants.EnumerateArray().First(p => p.GetProperty("memberId").GetGuid() == w.Staff.Id);
        var directorRecord = participants.EnumerateArray().First(p => p.GetProperty("memberId").GetGuid() == w.Director.Id);
        var staffId = staffRecord.GetProperty("recordId").GetGuid();
        var directorId = directorRecord.GetProperty("recordId").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{staffId}/profile",
            new { version = staffRecord.GetProperty("version").GetUInt32(), workflowProfileCode = "cap-tren", reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{staffId}/profile",
            new { version = staffRecord.GetProperty("version").GetUInt32(), workflowProfileCode = "khong-co", reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.StaffC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{staffId}/profile",
            new { version = staffRecord.GetProperty("version").GetUInt32(), workflowProfileCode = "cap-tren", reason = "x" })).StatusCode);

        var bulk = await Data(await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/profile", new
        {
            items = new object[]
            {
                new { recordId = staffId, version = staffRecord.GetProperty("version").GetUInt32() },
                new { recordId = directorId, version = directorRecord.GetProperty("version").GetUInt32() + 1000 }
            },
            workflowProfileCode = "khong-tham-dinh",
            reason = "Nhóm không qua thẩm định"
        }));
        Assert.Equal(1, bulk.GetProperty("updated").GetInt32());
        Assert.Single(bulk.GetProperty("skipped").EnumerateArray());
        var staffAfter = await ParticipantAsync(w, periodId, staffId);
        Assert.Equal("khong-tham-dinh", staffAfter.GetProperty("workflowProfileCode").GetString());
        Assert.Equal("Không qua thẩm định", staffAfter.GetProperty("workflowProfileName").GetString());
        var history = await Data(w.ManagerC, $"/api/evaluations/records/{staffId}/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("action").GetString() == "ChangeProfile"
            && h.GetProperty("reason").GetString() == "Nhóm không qua thẩm định");

        // Thêm người được đánh giá kèm diện quyết định chỉ định; mã không có trong kỳ → 400, không thêm ai.
        var bad = await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Staff3.Id }, workflowProfileCode = "khong-co" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("khong-co", await MessageAsync(bad));
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Staff3.Id }, workflowProfileCode = "bi-thu-nhan-vien" }));
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Other.Id } }));
        var added = (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .ToDictionary(p => p.GetProperty("memberId").GetGuid(), p => p.GetProperty("workflowProfileCode").GetString());
        Assert.Equal("bi-thu-nhan-vien", added[w.Staff3.Id]);
        Assert.Equal("co-so", added[w.Other.Id]);

        // Kỳ đã mở: chỉ sửa được thời hạn (kể cả thời hạn theo diện quyết định); đổi chế độ bước → 409.
        await OpenAsync(w, periodId);
        Assert.Equal(HttpStatusCode.OK,
            (await PutSettings(s => ProfileNode(s, "cap-tren")["steps"]!["B4_DECISION"]!["deadline"] = "2026-12-20")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await PutSettings(s => ProfileNode(s, "co-so")["steps"]!["B3C_DIRECTOR"]!["mode"] = "Off")).StatusCode);
        var deadline = (await Data(w.OwnerDirectorC, $"/api/evaluations/records/{directorId}")).GetProperty("progress").EnumerateArray()
            .First(p => p.GetProperty("step").GetString() == "B4_DECISION").GetProperty("deadline").GetString();
        Assert.Equal("2026-12-20", deadline);
    }

    /// <summary>Văn bản của cấp trên gắn vào kết quả bước cấp trên thực hiện: người ghi nhận tải lên/thay; ai xem được hồ sơ thì xem được.</summary>
    [SkippableFact]
    public async Task G5_ExternalResultAttachment_VisibleToRecordReaders_ManagedByRecorder()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Director.Id);
        var id = await RecordOfAsync(w, periodId, w.Director.Id);
        await StepAsync(w.OwnerDirectorC, id, "self-score/submit", new { generalScores = General, taskScores = await RegistrationTestData.RegisterAsync(_factory, id, 62.0 / 70) }, "AwaitingCellConfirm");
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");

        var fileId = (await Data(await UploadAsync(w.OfficeC))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Lead1C.GetAsync($"/api/attachments/{fileId}")).StatusCode);

        // Không gắn được tệp riêng (chưa gắn hồ sơ) của người khác vào kết quả của cấp trên.
        var ownerFile = (await Data(await UploadAsync(w.OwnerDirectorC))).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(w.OfficeC, id, "external/B3B_APPRAISAL",
            new { authorityName = "Ban TCĐU", grade = "HoanThanhTot", attachmentId = ownerFile }, await VersionAsync(w, id))).StatusCode);

        var recorded = await StepAsync(w.OfficeC, id, "external/B3B_APPRAISAL",
            new { authorityName = "Ban Tổ chức Đảng ủy Tổng công ty", grade = "HoanThanhTot", attachmentId = fileId }, "AwaitingDirectorReview");
        Assert.Equal(fileId, recorded.GetProperty("externalResults")[0].GetProperty("attachmentId").GetGuid());

        // Ai xem được hồ sơ thì xem/tải được: Lãnh đạo Phòng của hồ sơ, chủ hồ sơ, người ghi nhận; ngoài phạm vi → 403.
        foreach (var client in new[] { w.Lead1C, w.OwnerDirectorC, w.OfficeC })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/attachments/{fileId}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Lead2C.GetAsync($"/api/attachments/{fileId}/download")).StatusCode);
        var files = await Data(w.Lead1C, $"/api/attachments?ownerType=EvaluationRecord&ownerId={id}");
        Assert.Contains(files.EnumerateArray(), f => f.GetProperty("id").GetGuid() == fileId);

        // Chủ hồ sơ và người chỉ xem không sửa được văn bản của cấp trên; người ghi nhận thay phiên bản được.
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(w.OwnerDirectorC, $"/api/attachments/{fileId}/versions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(w.Lead1C, $"/api/attachments/{fileId}/versions")).StatusCode);
        var v2 = await Data(await UploadAsync(w.OfficeC, $"/api/attachments/{fileId}/versions"));
        Assert.Equal(2, v2.GetProperty("versionNumber").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await w.Lead1C.GetAsync($"/api/attachments/{v2.GetProperty("id").GetGuid()}/download")).StatusCode);
    }

    /// <summary>Theo dõi tiến độ: bước đang chờ và số ngày quá hạn/còn lại theo diện quyết định; hồ sơ đã công bố không tính hạn.</summary>
    [SkippableFact]
    public async Task G6_Participants_CurrentStepDeadline_OverdueAndDueSoon_PublishedNotCounted()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Director.Id, w.Other.Id);
        var directorId = await RecordOfAsync(w, periodId, w.Director.Id);
        var otherId = await RecordOfAsync(w, periodId, w.Other.Id);
        var directorScores = await RegistrationTestData.RegisterAsync(_factory, directorId, 62.0 / 70);
        await RegistrationTestData.RegisterAsync(_factory, otherId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var yesterday = today.AddDays(-1).ToString("yyyy-MM-dd");
        var current = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
        var settings = JsonNode.Parse(current.GetProperty("settings").GetRawText())!.AsObject();
        JsonObject Steps(string code) => settings["profiles"]!.AsArray().Select(p => p!.AsObject())
            .First(p => p["code"]!.GetValue<string>() == code)["steps"]!.AsObject();
        Steps("cap-tren")["B2_SELF_SCORE"]!["deadline"] = yesterday;
        Steps("cap-tren")["B5_PUBLISH"]!["deadline"] = yesterday;
        // Kỳ mới đã điền sẵn hạn mọi bước; bỏ hạn bước xác nhận để kiểm tra trường hợp không đặt hạn.
        Steps("cap-tren")["B2_CELL_CONFIRM"]!["deadline"] = null;
        Steps("co-so")["B2_SELF_SCORE"]!["deadline"] = today.AddDays(2).ToString("yyyy-MM-dd");
        await Data(await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
            new { version = current.GetProperty("version").GetUInt32(), settings }));

        var director = await ParticipantAsync(w, periodId, directorId);
        Assert.Equal("B2_SELF_SCORE", director.GetProperty("currentStep").GetString());
        Assert.Equal("Tự chấm điểm, đề xuất mức", director.GetProperty("currentStepName").GetString());
        Assert.Equal(yesterday, director.GetProperty("currentStepDeadline").GetString());
        Assert.Equal(1, director.GetProperty("daysOverdue").GetInt32());
        Assert.Equal(JsonValueKind.Null, director.GetProperty("daysLeft").ValueKind);

        var other = await ParticipantAsync(w, periodId, otherId);
        Assert.Equal("B2_SELF_SCORE", other.GetProperty("currentStep").GetString());
        Assert.Equal(2, other.GetProperty("daysLeft").GetInt32());
        Assert.Equal(JsonValueKind.Null, other.GetProperty("daysOverdue").ValueKind);

        // Bước không đặt hạn → không có hạn, không tính quá hạn.
        await StepAsync(w.OwnerDirectorC, directorId, "self-score/submit", new { generalScores = General, taskScores = directorScores }, "AwaitingCellConfirm");
        director = await ParticipantAsync(w, periodId, directorId);
        Assert.Equal("B2_CELL_CONFIRM", director.GetProperty("currentStep").GetString());
        Assert.Equal(JsonValueKind.Null, director.GetProperty("currentStepDeadline").ValueKind);
        Assert.Equal(JsonValueKind.Null, director.GetProperty("daysOverdue").ValueKind);
        Assert.Equal(JsonValueKind.Null, director.GetProperty("daysLeft").ValueKind);

        await StepAsync(w.CellSecC, directorId, "cell/confirm", new { }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, directorId, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");
        await StepAsync(w.OfficeC, directorId, "external/B3B_APPRAISAL", new { authorityName = "Ban TCĐU", grade = "HoanThanhTot" }, "AwaitingDirectorReview");
        await StepAsync(w.OfficeC, directorId, "external/B3C_DIRECTOR", new { authorityName = "HĐTV", grade = "HoanThanhTot" }, "AwaitingDecision");
        await StepAsync(w.OfficeC, directorId, "external/B4_DECISION", new { authorityName = "BTV", grade = "HoanThanhTot" }, "AwaitingPublish");

        director = await ParticipantAsync(w, periodId, directorId);
        Assert.Equal("B5_PUBLISH", director.GetProperty("currentStep").GetString());
        Assert.Equal(1, director.GetProperty("daysOverdue").GetInt32());

        await StepAsync(w.OfficeC, directorId, "publish", new { }, "Published");
        director = await ParticipantAsync(w, periodId, directorId);
        Assert.Equal("Published", director.GetProperty("status").GetString());
        foreach (var field in new[] { "currentStep", "currentStepName", "currentStepDeadline", "daysOverdue", "daysLeft" })
            Assert.Equal(JsonValueKind.Null, director.GetProperty(field).ValueKind);
    }

    #region Hỗ trợ

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string url = "/api/attachments/upload")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 van ban cap tren"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "van-ban-cap-tren.pdf");
        content.Add(new StringContent("CAPTREN"), "formCode");
        return await client.PostAsync(url, content);
    }

    [SkippableFact]
    public async Task G9_DraftSnapshot_FollowsMemberProfile_PersistedOnOpen_DataReviewFlagsCommitteeMembers()
    {
        var w = await WorldAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid dept = Guid.Empty, committee = Guid.Empty, cell = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var d = new AdministrativeDepartment { Code = $"G9-P-{suffix}", Name = $"Phòng G9 {suffix}" };
            var parent = new PartyCell { Code = $"G9-DB-{suffix}", Name = $"Đảng bộ G9 {suffix}" };
            db.AddRange(d, parent);
            await db.SaveChangesAsync();
            var child = new PartyCell { Code = $"G9-CB-{suffix}", Name = $"Chi bộ G9 {suffix}", ParentId = parent.Id };
            db.Add(child);
            await db.SaveChangesAsync();
            (dept, committee, cell) = (d.Id, parent.Id, child.Id);
        });
        var member = await _factory.CreateUserAsync(dept, committee, ApprovalAuthority.CoSo, $"G9 Lãnh đạo {suffix}");
        var noCell = await _factory.CreateUserAsync(dept, null, ApprovalAuthority.CoSo, $"G9 Chưa có Chi bộ {suffix}");

        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { member.Id, noCell.Id } }));
        var recordId = await RecordOfAsync(w, periodId, member.Id);

        // Rà soát: người ghi thẳng vào Đảng bộ (tổ chức có tổ chức con) và hồ sơ thiếu Chi bộ.
        var review = (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/readiness")).GetProperty("dataReview").EnumerateArray().ToList();
        var direct = Assert.Single(review, i => i.GetProperty("kind").GetString() == "DirectInPartyCommittee");
        Assert.Contains(direct.GetProperty("names").EnumerateArray(), n => n.GetString()!.Contains($"G9 Lãnh đạo {suffix}"));
        var missing = Assert.Single(review, i => i.GetProperty("kind").GetString() == "MissingOrg");
        Assert.Contains(missing.GetProperty("names").EnumerateArray(), n => n.GetString()!.Contains($"G9 Chưa có Chi bộ {suffix}"));

        // Kỳ dự thảo: sửa Chi bộ của cán bộ → danh sách và kiểm tra kẹt luồng dùng ngay Chi bộ mới, không cần bỏ ra thêm lại.
        await _factory.WithDbAsync(async db =>
        {
            var profile = await db.PartyMemberProfiles.FindAsync(member.Id);
            profile!.PartyCellId = cell;
            await db.SaveChangesAsync();
        });
        var participant = await ParticipantAsync(w, periodId, recordId);
        Assert.Equal(cell, participant.GetProperty("partyCellId").GetGuid());
        Assert.Equal($"Chi bộ G9 {suffix}", participant.GetProperty("partyCellName").GetString());
        review = (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/readiness")).GetProperty("dataReview").EnumerateArray().ToList();
        Assert.DoesNotContain(review, i => i.GetProperty("kind").GetString() == "DirectInPartyCommittee");

        // Mở kỳ → ảnh chụp mới được lưu, kèm lịch sử "Cập nhật theo hồ sơ cán bộ".
        await OpenAsync(w, periodId);
        await _factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.FindAsync(recordId);
            Assert.Equal(cell, record!.PartyCellId);
            Assert.True(db.EvaluationRecordHistories.Any(h => h.RecordId == recordId
                && h.Action == CongTacDang.Domain.Evaluation.WorkflowAction.SyncSnapshot));
            var profile = await db.PartyMemberProfiles.FindAsync(member.Id);
            profile!.PartyCellId = committee;
            await db.SaveChangesAsync();
        });

        // Kỳ đã mở: ảnh chụp cố định, không đi theo hồ sơ cán bộ nữa.
        Assert.Equal(cell, (await ParticipantAsync(w, periodId, recordId)).GetProperty("partyCellId").GetGuid());
    }

    [SkippableFact]
    public async Task G10_DraftSnapshot_ManualEditIsKept()
    {
        var w = await WorldAsync();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid dept = Guid.Empty, cellA = Guid.Empty, cellB = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var d = new AdministrativeDepartment { Code = $"G10-P-{suffix}", Name = $"Phòng G10 {suffix}" };
            var a = new PartyCell { Code = $"G10-A-{suffix}", Name = $"Chi bộ G10 A {suffix}" };
            var b = new PartyCell { Code = $"G10-B-{suffix}", Name = $"Chi bộ G10 B {suffix}" };
            db.AddRange(d, a, b);
            await db.SaveChangesAsync();
            (dept, cellA, cellB) = (d.Id, a.Id, b.Id);
        });
        var member = await _factory.CreateUserAsync(dept, cellA, ApprovalAuthority.CoSo, $"G10 Cán bộ {suffix}");
        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { member.Id } }));
        var recordId = await RecordOfAsync(w, periodId, member.Id);

        // Sửa tay ảnh chụp sang Chi bộ B (có lý do) → giữ nguyên dù hồ sơ cán bộ vẫn ở Chi bộ A.
        var participant = await ParticipantAsync(w, periodId, recordId);
        await Data(await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{recordId}/snapshot", new
        {
            version = participant.GetProperty("version").GetUInt32(),
            departmentId = dept,
            partyCellId = cellB,
            reason = "Sinh hoạt tạm ở Chi bộ B trong quý"
        }));
        Assert.Equal(cellB, (await ParticipantAsync(w, periodId, recordId)).GetProperty("partyCellId").GetGuid());
        await OpenAsync(w, periodId);
        Assert.Equal(cellB, (await ParticipantAsync(w, periodId, recordId)).GetProperty("partyCellId").GetGuid());
    }

    [SkippableFact]
    public async Task G11_Evidence_OwnerAttachesToRecord_ReviewersSee_OthersCannotAttach()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, w.Staff.Id);
        var recordId = await RecordOfAsync(w, periodId, w.Staff.Id);

        static MultipartFormDataContent Form(Guid recordId)
        {
            var file = new ByteArrayContent(System.Text.Encoding.ASCII.GetBytes("%PDF-1.4\n% minh chung truc T1\n"));
            file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            return new MultipartFormDataContent
            {
                { file, "file", "bao-cao-quy.pdf" },
                { new StringContent("MC-T1"), "formCode" },
                { new StringContent("Minh chứng T1"), "description" },
                { new StringContent("EvaluationRecord"), "ownerType" },
                { new StringContent(recordId.ToString()), "ownerId" }
            };
        }

        var uploaded = await Data(await w.StaffC.PostAsync("/api/attachments/upload", Form(recordId)));
        Assert.Equal("MC-T1", uploaded.GetProperty("formCode").GetString());

        // Người xem được hồ sơ thấy tệp; người khác không gắn được tệp vào hồ sơ này.
        var seen = await Data(w.ManagerC, $"/api/attachments?ownerType=EvaluationRecord&ownerId={recordId}");
        Assert.Contains(seen.EnumerateArray(), f => f.GetProperty("formCode").GetString() == "MC-T1");
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ManagerC.PostAsync("/api/attachments/upload", Form(recordId))).StatusCode);

        // Trước khi nộp phiếu: người khác không gỡ được; chủ hồ sơ gỡ được — tệp không còn hiện nhưng vẫn lưu (xóa mềm).
        var fileId = uploaded.GetProperty("id").GetGuid();
        var removable = (await Data(await w.StaffC.PostAsync("/api/attachments/upload", Form(recordId)))).GetProperty("id").GetGuid();
        var rights = await Data(w.StaffC, $"/api/attachments/evidence-rights?recordId={recordId}");
        Assert.True(rights.GetProperty("canAdd").GetBoolean() && rights.GetProperty("canReplace").GetBoolean() && rights.GetProperty("canRemove").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await w.ManagerC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.StaffC.DeleteAsync($"/api/attachments/{removable}")).StatusCode);
        var after = await Data(w.ManagerC, $"/api/attachments?ownerType=EvaluationRecord&ownerId={recordId}");
        Assert.DoesNotContain(after.EnumerateArray(), f => f.GetProperty("id").GetGuid() == removable);
        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.TaskAttachments.IgnoreQueryFilters().FirstAsync(a => a.Id == removable);
            Assert.True(stored.IsDeleted);
        });

        // Đã nộp phiếu tự chấm: chỉ bổ sung tệp mới; thay, gỡ tệp đã nộp → 409. Người xem khác không có thao tác nào.
        await StepAsync(w.StaffC, recordId, "self-score/submit", new { generalScores = General, taskScores = await RegistrationTestData.RegisterAsync(_factory, recordId, 62.0 / 70) }, "AwaitingCellConfirm");
        Assert.Equal(HttpStatusCode.Conflict, (await w.StaffC.DeleteAsync($"/api/attachments/{fileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await UploadAsync(w.StaffC, $"/api/attachments/{fileId}/versions")).StatusCode);
        var added = await Data(await w.StaffC.PostAsync("/api/attachments/upload", Form(recordId)));
        Assert.Equal(HttpStatusCode.Conflict, (await w.StaffC.DeleteAsync($"/api/attachments/{added.GetProperty("id").GetGuid()}")).StatusCode);
        rights = await Data(w.StaffC, $"/api/attachments/evidence-rights?recordId={recordId}");
        Assert.Equal("AddOnly", rights.GetProperty("stage").GetString());
        Assert.True(rights.GetProperty("canAdd").GetBoolean());
        Assert.False(rights.GetProperty("canReplace").GetBoolean() || rights.GetProperty("canRemove").GetBoolean());
        var managerRights = await Data(w.ManagerC, $"/api/attachments/evidence-rights?recordId={recordId}");
        Assert.False(managerRights.GetProperty("canAdd").GetBoolean());
        var listed = await Data(w.ManagerC, $"/api/attachments?ownerType=EvaluationRecord&ownerId={recordId}");
        Assert.Contains(listed.EnumerateArray(), f => f.GetProperty("id").GetGuid() == fileId);
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

    private static async Task<JsonElement> CreatePeriodAsync(World w)
    {
        var body = new
        {
            year = TestPeriodSlots.NextYear(), quarter = TestPeriodSlots.Quarter, criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.ManagerC),
            name = $"Kỳ G {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        var created = await Data(await w.ManagerC.PostAsJsonAsync("/api/evaluations/periods", body));
        var id = created.GetProperty("id").GetGuid();
        await PeriodGates.DisableAsync(w.ManagerC, id);
        return await Data(w.ManagerC, $"/api/evaluations/periods/{id}");
    }

    private static async Task OpenAsync(World w, Guid periodId)
    {
        var period = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), force = true, reason = "Kiểm thử diện quyết định" }));
    }

    private static async Task<Guid> OpenPeriodAsync(World w, params Guid[] members)
    {
        var periodId = (await CreatePeriodAsync(w)).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = members }));
        await OpenAsync(w, periodId);
        return periodId;
    }

    private static async Task<Guid> RecordOfAsync(World w, Guid periodId, Guid memberId) =>
        (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == memberId).GetProperty("recordId").GetGuid();

    private static async Task<JsonElement> ParticipantAsync(World w, Guid periodId, Guid recordId) =>
        (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("recordId").GetGuid() == recordId);

    private static async Task<uint> VersionAsync(World w, Guid recordId) =>
        (await Data(w.ManagerC, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body, uint? version = null)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (version.HasValue)
            json["version"] = version.Value;
        if (action == "appraisal")
            await AppraisalTestData.AddTaskLevelsAsync(client, recordId, json);
        return await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    private async Task<JsonElement> StepAsync(HttpClient client, Guid recordId, string action, object body, string expectedStatus)
    {
        var w = await WorldAsync();
        var data = await Data(await PostAsync(client, recordId, action, body, await VersionAsync(w, recordId)));
        Assert.Equal(expectedStatus, data.GetProperty("status").GetString());
        return data;
    }

    private static async Task AssertActionsAsync(HttpClient client, Guid recordId, params string[] expected)
    {
        var data = await Data(client, $"/api/evaluations/records/{recordId}/actions");
        var actions = data.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("action").GetString()!).OrderBy(x => x).ToList();
        Assert.Equal(expected.OrderBy(x => x).ToList(), actions);
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
    /// Phòng 1–3, Chi bộ 1 và 3; Giám đốc (diện cấp trên), Bí thư Chi bộ là nhân viên (Phòng 1, Phòng 3); người thực hiện các bước
    /// theo vai trò mặc định với phạm vi riêng. Phòng 3/Chi bộ 3 chỉ có một Lãnh đạo Phòng (để kiểm tra kẹt luồng).
    /// </summary>
    private sealed class World
    {
        public TestUser Director = null!, Staff = null!, Staff3 = null!, Other = null!;
        public HttpClient OwnerDirectorC = null!, StaffC = null!, ManagerC = null!, CellSecC = null!, SecretaryC = null!;
        public HttpClient Lead1C = null!, Lead2C = null!, DirectorC = null!, OfficeC = null!, AdminC = null!;
        public Guid Lead3Assignment;
        public string Dept3Name = string.Empty;

        public static async Task<World> CreateAsync(ApiFactory factory)
        {
            var w = new World();
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            Guid d1 = Guid.Empty, d2 = Guid.Empty, d3 = Guid.Empty, c1 = Guid.Empty, c3 = Guid.Empty;
            w.Dept3Name = $"Phòng G3 {suffix}";
            await factory.WithDbAsync(async db =>
            {
                var dept1 = new AdministrativeDepartment { Code = $"G-P1-{suffix}", Name = $"Phòng G1 {suffix}" };
                var dept2 = new AdministrativeDepartment { Code = $"G-P2-{suffix}", Name = $"Phòng G2 {suffix}" };
                var dept3 = new AdministrativeDepartment { Code = $"G-P3-{suffix}", Name = w.Dept3Name };
                var cell1 = new PartyCell { Code = $"G-C1-{suffix}", Name = $"Chi bộ G1 {suffix}" };
                var cell3 = new PartyCell { Code = $"G-C3-{suffix}", Name = $"Chi bộ G3 {suffix}" };
                db.AddRange(dept1, dept2, dept3, cell1, cell3);
                await db.SaveChangesAsync();
                (d1, d2, d3, c1, c3) = (dept1.Id, dept2.Id, dept3.Id, cell1.Id, cell3.Id);
            });

            async Task<Guid> Role(string code) => await factory.GetRoleIdAsync(code);
            var evaluatee = await Role("NGUOI_DUOC_DANH_GIA");

            w.Director = await factory.CreateUserAsync(d1, c1, ApprovalAuthority.CapTren, $"G Giám đốc {suffix}");
            w.Staff = await factory.CreateUserAsync(d1, c1, ApprovalAuthority.CoSo, $"G Bí thư Chi bộ {suffix}");
            w.Staff3 = await factory.CreateUserAsync(d3, c3, ApprovalAuthority.CoSo, $"G Bí thư Chi bộ 3 {suffix}");
            w.Other = await factory.CreateUserAsync(d2, null, ApprovalAuthority.CoSo, $"G Cán bộ {suffix}");
            var manager = await factory.CreateUserAsync();
            var cellSec = await factory.CreateUserAsync();
            var cellSec3 = await factory.CreateUserAsync();
            var secretary = await factory.CreateUserAsync();
            var secretary3 = await factory.CreateUserAsync();
            var lead1 = await factory.CreateUserAsync(d1);
            var lead2 = await factory.CreateUserAsync(d2);
            var lead3 = await factory.CreateUserAsync(d3);
            var director = await factory.CreateUserAsync();
            var office = await factory.CreateUserAsync();
            var admin = await factory.CreateUserAsync();

            foreach (var user in new[] { w.Director, w.Staff, w.Staff3, w.Other })
                await factory.AssignAsync(user.Id, evaluatee, RoleScopeType.Global, null);
            // Giám đốc cũng được giao ghi nhận kết quả của cấp trên (kiểm tra xung đột lợi ích trên hồ sơ của chính mình).
            await factory.AssignAsync(w.Director.Id, (await factory.CreateRoleAsync("evaluation.external.record")).Id, RoleScopeType.Global, null);
            await factory.AssignAsync(manager.Id, await Role("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await factory.AssignAsync(cellSec.Id, await Role("CHI_UY_CHI_BO"), RoleScopeType.PartyCell, c1);
            await factory.AssignAsync(cellSec3.Id, await Role("CHI_UY_CHI_BO"), RoleScopeType.PartyCell, c3);
            await factory.AssignAsync(secretary.Id, await Role("THU_KY_TAP_THE"), RoleScopeType.Department, d1);
            await factory.AssignAsync(secretary3.Id, await Role("THU_KY_TAP_THE"), RoleScopeType.Department, d3);
            await factory.AssignAsync(lead1.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, d1);
            await factory.AssignAsync(lead2.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, d2);
            w.Lead3Assignment = await factory.AssignAsync(lead3.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, d3);
            await factory.AssignAsync(director.Id, await Role("CAP_TRUC_TIEP_SU_DUNG"), RoleScopeType.Global, null);
            await factory.AssignAsync(office.Id, await Role("VAN_PHONG_DANG_UY"), RoleScopeType.Global, null);
            await factory.AssignAsync(admin.Id, await factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);

            Task<HttpClient> Login(TestUser user) => factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            w.OwnerDirectorC = await Login(w.Director);
            w.StaffC = await Login(w.Staff);
            w.ManagerC = await Login(manager);
            w.CellSecC = await Login(cellSec);
            w.SecretaryC = await Login(secretary);
            w.Lead1C = await Login(lead1);
            w.Lead2C = await Login(lead2);
            w.DirectorC = await Login(director);
            w.OfficeC = await Login(office);
            w.AdminC = await Login(admin);
            return w;
        }
    }

    #endregion
}
