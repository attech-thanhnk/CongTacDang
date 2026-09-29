using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClosedXML.Excel;
using CongTacDang.Application.Imports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 15: luồng theo hồ sơ luồng (nhóm đối tượng) trên PostgreSQL thật — (a) hồ sơ Giám đốc (CapTren) tới Đã công bố với
/// B3b/B3c/B4 ghi nhận kết quả của cấp trên; (b) hồ sơ "Bí thư/Phó bí thư Chi bộ là nhân viên": Trưởng phòng đề xuất thay
/// Giám đốc; (c) kiểm tra kẹt luồng + mở kỳ 409; đổi hồ sơ luồng, import cột hồ sơ luồng, kiểm tra cấu hình qua API.
/// (d) hồ sơ cơ sở đi như cũ: <c>EvaluationWorkflowTests.W1</c>.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class WorkflowProfileIntegrationTests
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();
    private static readonly double[] General = { 4.5, 4.5, 4.5, 4.5, 4.5, 4.5 };
    private static readonly double[] Axis = { 13, 9, 9, 13, 9, 9 };

    private readonly ApiFactory _factory;

    public WorkflowProfileIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task G1_DirectorRecord_UpperProfile_ExternalStepsRecorded_ToPublished()
    {
        var w = await WorldAsync();
        var periodId = await OpenPeriodAsync(w, "q3-2026-transition", w.Director.Id);
        var id = await RecordOfAsync(w, periodId, w.Director.Id);

        var record = await Data(w.OwnerDirectorC, $"/api/evaluations/records/{id}");
        Assert.Equal("cap-tren", record.GetProperty("workflowProfileCode").GetString());
        var modes = record.GetProperty("progress").EnumerateArray()
            .ToDictionary(p => p.GetProperty("step").GetString()!, p => p.GetProperty("mode").GetString()!);
        Assert.Equal("Off", modes["B1_REGISTER"]);
        Assert.Equal("Internal", modes["B3A_COLLECTIVE"]);
        Assert.Equal("External", modes["B3B_APPRAISAL"]);
        Assert.Equal("External", modes["B3C_DIRECTOR"]);
        Assert.Equal("External", modes["B4_DECISION"]);

        await StepAsync(w.OwnerDirectorC, id, "self-score/submit", new { generalScores = General, axisScores = Axis }, "AwaitingCellConfirm");
        await StepAsync(w.CellSecC, id, "cell/confirm", new { comment = "Xác nhận" }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");

        // B3b do cấp trên thực hiện: cơ quan thẩm định trong hệ thống không có thao tác, gọi API thẩm định → 409; không trả lại được.
        await AssertActionsAsync(w.ManagerC, id);
        var internalAppraisal = await PostAsync(w.ManagerC, id, "appraisal", new { appraisalScore = 90.0, comment = "x", proposedGrade = "HoanThanhTot" }, await VersionAsync(w, id));
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
        var periodId = (await CreatePeriodAsync(w, "q3-2026-transition")).GetProperty("id").GetGuid();
        // Thêm người kèm hồ sơ luồng chỉ định.
        var added = await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Staff.Id }, workflowProfileCode = "bi-thu-nhan-vien" }));
        Assert.Equal(1, added.GetProperty("added").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Director.Id }, workflowProfileCode = "khong-co" })).StatusCode);
        await OpenAsync(w, periodId);
        var id = await RecordOfAsync(w, periodId, w.Staff.Id);
        Assert.Equal("bi-thu-nhan-vien", (await Data(w.StaffC, $"/api/evaluations/records/{id}")).GetProperty("workflowProfileCode").GetString());

        await StepAsync(w.StaffC, id, "self-score/submit", new { generalScores = General, axisScores = Axis }, "AwaitingCellConfirm");
        await StepAsync(w.CellSecC, id, "cell/confirm", new { }, "AwaitingCollective");
        await StepAsync(w.SecretaryC, id, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");
        await StepAsync(w.ManagerC, id, "appraisal", new { appraisalScore = 87.0, comment = "Đạt", proposedGrade = "HoanThanhTot" }, "AwaitingDirectorReview");

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

        // Đã qua B3c (bước khác nhau giữa hai hồ sơ luồng) → không đổi sang hồ sơ luồng cơ sở được.
        var participant = await ParticipantAsync(w, periodId, id);
        var change = await w.ManagerC.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{id}/profile",
            new { version = participant.GetProperty("version").GetUInt32(), workflowProfileCode = "co-so", reason = "Nhầm nhóm" });
        Assert.Equal(HttpStatusCode.Conflict, change.StatusCode);
        Assert.Contains("đã qua bước", await MessageAsync(change));

        await StepAsync(w.OfficeC, id, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        await StepAsync(w.OfficeC, id, "publish", new { }, "Published");
    }

    [SkippableFact]
    public async Task G3_Readiness_DetectsStuckRecord_OpenReturns409_ForceNeedsReason()
    {
        var w = await WorldAsync();
        var periodId = (await CreatePeriodAsync(w, "q3-2026-transition")).GetProperty("id").GetGuid();
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Staff3.Id }, workflowProfileCode = "bi-thu-nhan-vien" }));

        var ready = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.True(ready.GetProperty("ready").GetBoolean(), ready.GetRawText());
        Assert.Equal(1, ready.GetProperty("checkedRecords").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await w.StaffC.GetAsync($"/api/evaluations/periods/{periodId}/readiness")).StatusCode);

        // Thu hồi bản gán của người duy nhất có evaluation.unit.review trong Phòng 3 → hồ sơ kẹt ở B3c.
        Assert.Equal(HttpStatusCode.OK, (await w.AdminC.DeleteAsync($"/api/admin/assignments/{w.Lead3Assignment}")).StatusCode);
        var stuck = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.False(stuck.GetProperty("ready").GetBoolean());
        var issue = Assert.Single(stuck.GetProperty("issues").EnumerateArray());
        Assert.Equal("B3C_DIRECTOR", issue.GetProperty("step").GetString());
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
            Assert.Single(json.RootElement.GetProperty("data").GetProperty("issues").EnumerateArray());
            Assert.Single(json.RootElement.GetProperty("errors").EnumerateArray());
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
    public async Task G4_ProfileConfiguration_ChangeProfile_Bulk_Import_AndValidation()
    {
        var w = await WorldAsync();
        var period = await CreatePeriodAsync(w, "full");
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

        // Cấu hình không hợp lệ qua API → 400; xóa hồ sơ luồng đang dùng → 409.
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

        // Thêm hồ sơ luồng mới (sao chép "co-so", thẩm định không áp dụng) khi kỳ còn dự thảo.
        Assert.Equal(HttpStatusCode.OK, (await PutSettings(s =>
        {
            var copy = JsonNode.Parse(ProfileNode(s, "co-so").ToJsonString())!.AsObject();
            copy["code"] = "khong-tham-dinh";
            copy["name"] = "Không qua thẩm định";
            copy["steps"]!["B3B_APPRAISAL"]!["mode"] = "Off";
            s["profiles"]!.AsArray().Add(copy);
        })).StatusCode);

        // Đổi hồ sơ luồng: thiếu lý do → 400; không tồn tại → 400; hàng loạt (1 hồ sơ sai phiên bản bị bỏ qua).
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

        // Import cột "Hồ sơ luồng" (mã hoặc tên); sai → lỗi dòng.
        var year = period.GetProperty("year").GetInt32().ToString();
        var quarter = period.GetProperty("quarter").GetInt32().ToString();
        var name = period.GetProperty("name").GetString()!;
        string[] headers = { "Năm", "Quý", "Tên kỳ", "Tên đăng nhập", "Hồ sơ luồng" };
        var bad = await PreviewAsync(w.ManagerC, BuildFile(headers, new[] { year, quarter, name, w.Staff3.Username, "khong-co" }));
        Assert.False(bad.GetProperty("canCommit").GetBoolean());
        var good = await PreviewAsync(w.ManagerC, BuildFile(headers,
            new[] { year, quarter, name, w.Staff3.Username, "Bí thư/Phó bí thư Chi bộ là nhân viên" },
            new[] { year, quarter, name, w.Other.Username, "" }));
        Assert.True(good.GetProperty("canCommit").GetBoolean(), good.GetRawText());
        Assert.Equal(HttpStatusCode.OK, (await w.ManagerC.PostAsync($"/api/imports/{good.GetProperty("sessionId").GetGuid()}/commit", null)).StatusCode);
        var imported = (await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .ToDictionary(p => p.GetProperty("memberId").GetGuid(), p => p.GetProperty("workflowProfileCode").GetString());
        Assert.Equal("bi-thu-nhan-vien", imported[w.Staff3.Id]);
        Assert.Equal("co-so", imported[w.Other.Id]);

        // Kỳ đã mở: chỉ sửa được thời hạn (kể cả thời hạn theo hồ sơ luồng); đổi chế độ bước → 409.
        await OpenAsync(w, periodId);
        Assert.Equal(HttpStatusCode.OK,
            (await PutSettings(s => ProfileNode(s, "cap-tren")["steps"]!["B4_DECISION"]!["deadline"] = "2026-12-20")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await PutSettings(s => ProfileNode(s, "co-so")["steps"]!["B3C_DIRECTOR"]!["mode"] = "Off")).StatusCode);
        var deadline = (await Data(w.OwnerDirectorC, $"/api/evaluations/records/{directorId}")).GetProperty("progress").EnumerateArray()
            .First(p => p.GetProperty("step").GetString() == "B4_DECISION").GetProperty("deadline").GetString();
        Assert.Equal("2026-12-20", deadline);
    }

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

    private static async Task<JsonElement> CreatePeriodAsync(World w, string preset)
    {
        var body = new
        {
            year = 2100 - Random.Shared.Next(1, 90), quarter = Random.Shared.Next(1, 5), preset,
            name = $"Kỳ G {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        return await Data(await w.ManagerC.PostAsJsonAsync("/api/evaluations/periods", body));
    }

    private static async Task OpenAsync(World w, Guid periodId)
    {
        var period = await Data(w.ManagerC, $"/api/evaluations/periods/{periodId}");
        await Data(await w.ManagerC.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), force = true, reason = "Kiểm thử hồ sơ luồng" }));
    }

    private static async Task<Guid> OpenPeriodAsync(World w, string preset, params Guid[] members)
    {
        var periodId = (await CreatePeriodAsync(w, preset)).GetProperty("id").GetGuid();
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

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body, uint? version = null)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (version.HasValue)
            json["version"] = version.Value;
        return client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
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
