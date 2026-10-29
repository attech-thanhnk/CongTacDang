using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Gọi API đúng thân yêu cầu và đường dẫn mà giao diện gửi (lấy từ mã frontend): trường để trống gửi null/chuỗi rỗng,
/// trường không có ô nhập thì không gửi. Bắt lỗi giao diện thiếu trường mà test gọi API trực tiếp với đủ trường không thấy.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class UiContractIntegrationTests
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string XlsxType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private readonly ApiFactory _factory;

    public UiContractIntegrationTests(ApiFactory factory) => _factory = factory;

    #region U1 — hồ sơ đi hết luồng bằng thân yêu cầu của giao diện

    [SkippableFact]
    public async Task U1_RecordFlow_BodiesAsSentByUi_WalksToPublished_QueueFollowsEachStep_ReopenAndReturn()
    {
        var w = await WorldAsync();
        var s = await Scenario.StartAsync(w, _factory, gates: GateSet.AllOff);

        await s.RegisterAsync();
        await s.ApproveAsync();
        await s.SelfScoreDraftThenSubmitAsync();
        await s.CellConfirmAsync();
        await s.CollectiveAsync();
        await s.AppraiseAsync();
        await s.DirectorReviewAsync();
        await s.DecideAsync(expectOk: true);
        await s.PublishAsync();

        var record = await Data(s.Owner, $"/api/evaluations/records/{s.RecordId}");
        Assert.Equal("Published", record.GetProperty("status").GetString());
        Assert.Equal(s.Grade, record.GetProperty("finalGrade").GetString());
        Assert.All(record.GetProperty("progress").EnumerateArray(), p => Assert.Equal("done", p.GetProperty("state").GetString()));
        // Trường mới của hồ sơ có đủ để giao diện đọc, chưa hiệu chuẩn thì rỗng.
        Assert.Equal(JsonValueKind.Null, record.GetProperty("calibratedScore").ValueKind);
        Assert.True(record.TryGetProperty("calibratedGrade", out _));
        Assert.True(record.TryGetProperty("reviewedScore", out var reviewed) && reviewed.ValueKind == JsonValueKind.Number);
        Assert.False(string.IsNullOrEmpty(record.GetProperty("reviewedGrade").GetString()));

        // Mở lại về bước quyết định như hộp thoại lý do của giao diện (không gửi unlockUntil), ghi nhận lại, công bố lại.
        var actions = await Data(s.Office, $"/api/evaluations/records/{s.RecordId}/actions");
        var reopen = Assert.Single(actions.GetProperty("actions").EnumerateArray(), a => a.GetProperty("action").GetString() == "Reopen");
        Assert.True(reopen.GetProperty("requiresReason").GetBoolean());
        Assert.Contains(reopen.GetProperty("targetSteps").EnumerateArray(), t => t.GetString() == "B4_DECISION");
        var reopened = await Data(await s.ActAsync(s.Office, "reopen", new { reason = "Đính chính số văn bản", targetStep = "B4_DECISION" }));
        Assert.Equal("AwaitingDecision", reopened.GetProperty("status").GetString());
        await s.DecideAsync(expectOk: true);
        await s.PublishAsync();

        // Trả lại như giao diện: Chi bộ trả phiếu tự chấm về cho chủ hồ sơ (chỉ gửi reason, unlockUntil không gửi).
        var second = await Scenario.StartAsync(w, _factory, gates: GateSet.AllOff);
        await second.RegisterAsync();
        await second.ApproveAsync();
        await second.SelfScoreDraftThenSubmitAsync();
        var returned = await Data(await second.ActAsync(w.CellSec, "cell/return", new { reason = "Bổ sung minh chứng" }));
        Assert.Equal("AwaitingSelfScore", returned.GetProperty("status").GetString());
        Assert.Equal("Bổ sung minh chứng", returned.GetProperty("returnReason").GetString());
        await second.AssertQueueAsync(second.Owner, "B2_SELF_SCORE");
        var queueItem = await second.QueueItemAsync(second.Owner, "B2_SELF_SCORE");
        Assert.Equal("Bổ sung minh chứng", queueItem.GetProperty("returnReason").GetString());
    }

    #endregion

    #region U2 — cổng ở bước quyết định: khóa, chỉ ghi nhận

    [SkippableFact]
    public async Task U2_DecisionGates_RequiredBlocksWithReason_RecordedWarnsAndRecords()
    {
        var w = await WorldAsync();

        // Đặt hiệu chuẩn và kiểm soát bắt buộc (mặc định của kỳ mới là chỉ ghi nhận).
        var blocked = await Scenario.StartAsync(w, _factory, gates: GateSet.Required);
        await blocked.DriveToDecisionAsync();
        var actions = (await Data(w.Office, $"/api/evaluations/records/{blocked.RecordId}/actions")).GetProperty("actions").EnumerateArray().ToList();
        var decide = Assert.Single(actions, a => a.GetProperty("action").GetString() == "RecordDecision");
        Assert.Equal("CALIBRATION_REQUIRED", decide.GetProperty("blockedBy").GetString());
        Assert.False(string.IsNullOrWhiteSpace(decide.GetProperty("blockedReason").GetString()));
        var refused = await blocked.DecideAsync(expectOk: false);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var message = await MessageAsync(refused);
        Assert.Contains("không thành công", message);
        Assert.Contains("biên bản họp thống nhất", message);
        // Việc vẫn nằm trong danh sách việc của người quyết định để họ biết cần làm gì.
        await blocked.AssertQueueAsync(w.Office, "B4_DECISION");

        // Chế độ "Chỉ ghi nhận": không khóa, hành động kèm cảnh báo; quyết định ghi nhận được và lịch sử nêu phần thiếu.
        var recorded = await Scenario.StartAsync(w, _factory, gates: GateSet.Recorded);
        await recorded.DriveToDecisionAsync();
        actions = (await Data(w.Office, $"/api/evaluations/records/{recorded.RecordId}/actions")).GetProperty("actions").EnumerateArray().ToList();
        decide = Assert.Single(actions, a => a.GetProperty("action").GetString() == "RecordDecision");
        Assert.Equal(JsonValueKind.Null, decide.GetProperty("blockedBy").ValueKind);
        var warnings = decide.GetProperty("warnings").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.NotEmpty(warnings);
        await recorded.DecideAsync(expectOk: true);
        var history = await Data(recorded.Owner, $"/api/evaluations/records/{recorded.RecordId}/history");
        Assert.Contains(history.EnumerateArray(), h => h.GetProperty("comment").GetString()!.Contains("Ghi nhận thiếu"));
    }

    #endregion

    #region U3 — kỳ: lưu cấu hình có cổng khi dự thảo và đang mở, chuyển trạng thái

    [SkippableFact]
    public async Task U3_PeriodSettings_SaveWithGates_DraftAndOpen_Transitions()
    {
        var w = await WorldAsync();
        var periodId = await CreatePeriodAsUiAsync(w, GateSet.Default);
        var period = await Data(w.Manager, $"/api/evaluations/periods/{periodId}");

        // Cấu hình máy chủ trả về luôn có đủ ba cổng cho mọi diện quyết định.
        var profiles = period.GetProperty("settings").GetProperty("profiles").EnumerateArray().ToList();
        Assert.All(profiles, p =>
        {
            var gates = p.GetProperty("gates");
            foreach (var code in new[] { "CROSSCHECK", "CALIBRATION", "CONTROL" })
                Assert.True(gates.TryGetProperty(code, out var gate) && !string.IsNullOrEmpty(gate.GetProperty("mode").GetString()));
        });

        // Nút Lưu khi chưa sửa gì (đọc rồi gửi lại nguyên văn): chấp nhận, cổng giữ nguyên.
        var before = period.GetProperty("settings").GetRawText();
        var saved = await Data(await PutPeriodAsync(w.Manager, periodId, period.GetProperty("version").GetUInt32(), JsonNode.Parse(before)!));
        Assert.Equal(before, saved.GetProperty("settings").GetRawText());

        // Sửa cổng, mô tả để trống, thời hạn nhập tay (rule null) như bảng cấu hình diện quyết định.
        var edited = JsonNode.Parse(saved.GetProperty("settings").GetRawText())!.AsObject();
        var first = edited["profiles"]![0]!.AsObject();
        first["description"] = "";
        first["gates"]!["CALIBRATION"] = new JsonObject { ["mode"] = "Recorded" };
        first["gates"]!["CROSSCHECK"] = new JsonObject { ["mode"] = "Off" };
        first["steps"]!["B3B_APPRAISAL"]!["deadline"] = "2030-03-14";
        first["steps"]!["B3B_APPRAISAL"]!["rule"] = null;
        var afterEdit = await Data(await PutPeriodAsync(w.Manager, periodId, saved.GetProperty("version").GetUInt32(), edited));
        var firstSaved = afterEdit.GetProperty("settings").GetProperty("profiles")[0];
        Assert.Equal("Recorded", firstSaved.GetProperty("gates").GetProperty("CALIBRATION").GetProperty("mode").GetString());
        Assert.Equal("Off", firstSaved.GetProperty("gates").GetProperty("CROSSCHECK").GetProperty("mode").GetString());
        Assert.Equal("2030-03-14", firstSaved.GetProperty("steps").GetProperty("B3B_APPRAISAL").GetProperty("deadline").GetString());

        // Chế độ cổng sai bị từ chối bằng thông báo tiếng Việt (đối soát không có "Bắt buộc").
        var invalid = JsonNode.Parse(afterEdit.GetProperty("settings").GetRawText())!.AsObject();
        invalid["profiles"]![0]!["gates"]!["CROSSCHECK"] = new JsonObject { ["mode"] = "Required" };
        var rejected = await PutPeriodAsync(w.Manager, periodId, afterEdit.GetProperty("version").GetUInt32(), invalid);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("chế độ của cổng", await MessageAsync(rejected));

        // Thêm người như giao diện (chỉ gửi memberIds), kiểm tra kẹt luồng rồi mở kỳ (thân yêu cầu: version, force=false).
        var owner = await w.NewOwnerAsync(_factory);
        await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { owner.User.Id } }));
        var readiness = await Data(w.Manager, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.True(readiness.GetProperty("ready").GetBoolean(), readiness.GetRawText());
        var draft = await Data(w.Manager, $"/api/evaluations/periods/{periodId}");
        var opened = await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/open",
            new { version = draft.GetProperty("version").GetUInt32(), force = false }));
        Assert.Equal("Open", opened.GetProperty("status").GetString());

        // Kỳ đang mở: chỉ sửa được thời hạn; nút Lưu thời hạn và "Tính lại thời hạn" chấp nhận, sửa cổng bị từ chối.
        var open = JsonNode.Parse(opened.GetProperty("settings").GetRawText())!.AsObject();
        open["profiles"]![0]!["steps"]!["B2_SELF_SCORE"]!["deadline"] = "2030-04-01";
        var deadlineOnly = await Data(await PutPeriodAsync(w.Manager, periodId, opened.GetProperty("version").GetUInt32(), open));
        Assert.Equal("Recorded", deadlineOnly.GetProperty("settings").GetProperty("profiles")[0].GetProperty("gates").GetProperty("CALIBRATION").GetProperty("mode").GetString());
        var gateChange = JsonNode.Parse(deadlineOnly.GetProperty("settings").GetRawText())!.AsObject();
        gateChange["profiles"]![0]!["gates"]!["CONTROL"] = new JsonObject { ["mode"] = "Off" };
        var locked = await PutPeriodAsync(w.Manager, periodId, deadlineOnly.GetProperty("version").GetUInt32(), gateChange);
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("chỉ được sửa thời hạn", await MessageAsync(locked));
        var recomputed = await Data(await PutPeriodAsync(w.Manager, periodId, deadlineOnly.GetProperty("version").GetUInt32(),
            JsonNode.Parse(deadlineOnly.GetProperty("settings").GetRawText())!, applyDeadlineRules: true));
        Assert.Equal("Open", recomputed.GetProperty("status").GetString());

        // Khóa dữ liệu → vẫn lưu được thời hạn → mở lại cần lý do → đóng khi còn hồ sơ chưa công bố bị từ chối.
        var lockedPeriod = await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/lock",
            new { version = recomputed.GetProperty("version").GetUInt32() }));
        Assert.Equal("Locked", lockedPeriod.GetProperty("status").GetString());
        var lockedSettings = JsonNode.Parse(lockedPeriod.GetProperty("settings").GetRawText())!.AsObject();
        lockedSettings["profiles"]![0]!["steps"]!["B2_SELF_SCORE"]!["deadline"] = "2030-04-02";
        var stillSaves = await Data(await PutPeriodAsync(w.Manager, periodId, lockedPeriod.GetProperty("version").GetUInt32(), lockedSettings));
        var noReason = await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/unlock", new { version = stillSaves.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var reopened = await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/unlock",
            new { version = stillSaves.GetProperty("version").GetUInt32(), reason = "Bổ sung hồ sơ" }));
        Assert.Equal("Open", reopened.GetProperty("status").GetString());
        var close = await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/close", new { version = reopened.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);
        Assert.Contains("chưa công bố", await MessageAsync(close));
    }

    [SkippableFact]
    public async Task U3b_OpenPeriod_NotReady_Returns409WithReadinessForForceOpen()
    {
        var w = await WorldAsync();
        var periodId = await CreatePeriodAsUiAsync(w, GateSet.AllOff);
        // Chi bộ không ai xác nhận: chủ hồ sơ ở Chi bộ riêng, chưa có người thực hiện các bước của Chi bộ.
        var lonelyCell = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var cell = new PartyCell { Code = $"U3B-{Guid.NewGuid():N}"[..12], Name = $"Chi bộ U3B {Guid.NewGuid():N}"[..20] };
            db.Add(cell);
            await db.SaveChangesAsync();
            lonelyCell = cell.Id;
        });
        var user = await _factory.CreateUserAsync(w.Dept, lonelyCell, ApprovalAuthority.CoSo, "U3B Cán bộ");
        await _factory.AssignAsync(user.Id, await _factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
        await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { user.Id } }));
        var period = await Data(w.Manager, $"/api/evaluations/periods/{periodId}");

        // Giao diện đọc mã PERIOD_NOT_READY và danh sách cảnh báo trong body để hiện bảng và hộp mở bắt buộc.
        var response = await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/open", new { version = period.GetProperty("version").GetUInt32(), force = false });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("PERIOD_NOT_READY", body.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("message").GetString()));
        var issues = body.RootElement.GetProperty("data").GetProperty("issues").EnumerateArray().ToList();
        Assert.NotEmpty(issues);
        foreach (var field in new[] { "recordId", "fullName", "step", "stepName", "permission", "permissionName", "roleNames", "scopeName", "message" })
            Assert.True(issues[0].TryGetProperty(field, out _), field);

        // Mở bắt buộc thiếu lý do → 400; có lý do → mở.
        var noReason = await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/open", new { version = period.GetProperty("version").GetUInt32(), force = true });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        var forced = await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Sẽ bổ sung người thực hiện", force = true }));
        Assert.Equal("Open", forced.GetProperty("status").GetString());
    }

    #endregion

    #region U4 — hồ sơ tập thể (Mẫu 06, 07, 08) theo thân yêu cầu của form

    [SkippableFact]
    public async Task U4_CollectiveRecords_M07_M06_M08_AsSentByForm_CreateUpdateExport()
    {
        var w = await WorldAsync();
        var periodId = await CreatePeriodAsUiAsync(w, GateSet.AllOff);
        var catalog = await Data(w.CellSec, $"/api/evaluations/collective-forms/catalog?periodId={periodId}");
        var groups = catalog.GetProperty("collectiveGroups").EnumerateArray().ToList();
        Assert.NotEmpty(groups); // bộ tiêu chí mặc định có khung tập thể: form chấm từng tiêu chí
        var criteriaScores = new JsonObject();
        foreach (var item in groups.SelectMany(g => g.GetProperty("items").EnumerateArray()))
            criteriaScores[item.GetProperty("code").GetString()!] = item.GetProperty("maxScore").GetDouble() / 2;

        // Mẫu 07: tổ chức Đảng, bảy ô văn bản + bốn mục I.1–I.4, điểm từng tiêu chí; mức tự xếp loại mặc định của form.
        var sections07 = new JsonObject();
        foreach (var section in catalog.GetProperty("form07Strengths").EnumerateArray())
            sections07[section.GetProperty("code").GetString()!] = $"Kết quả {section.GetProperty("code").GetString()}";
        var m07 = await Data(await PostRawAsync(w.CellSec, "/api/evaluations/collective-records", new JsonObject
        {
            ["version"] = null,
            ["periodId"] = periodId,
            ["form"] = "M07",
            ["partyCellId"] = w.Cell,
            ["subjectName"] = "Chi bộ kiểm thử",
            ["strengths"] = "", ["limitations"] = "Còn chậm", ["causes"] = "Thiếu người", ["previousRemediation"] = "",
            ["explanation"] = "", ["responsibilities"] = "Bí thư", ["remediationPlan"] = "Bổ sung",
            ["generalCriteriaScore"] = 0, ["taskCriteriaScore"] = 0,
            ["criteriaScores"] = criteriaScores.DeepClone(),
            ["selfProposedGrade"] = "HoanThanhTot",
            ["sections"] = sections07,
            ["items"] = new JsonArray()
        }));
        var m07Id = m07.GetProperty("id").GetGuid();
        Assert.True(m07.GetProperty("totalScore").GetDouble() > 0);
        Assert.True(m07.GetProperty("canAttach").GetBoolean());
        // Sửa: gửi lại version đọc được; hồ sơ trùng tổ chức bị từ chối bằng thông báo nêu việc cần làm.
        var m07Edit = await Data(await PutRawAsync(w.CellSec, $"/api/evaluations/collective-records/{m07Id}", new JsonObject
        {
            ["version"] = m07.GetProperty("version").GetUInt32(),
            ["periodId"] = periodId,
            ["form"] = "M07",
            ["partyCellId"] = w.Cell,
            ["subjectName"] = "Chi bộ kiểm thử (sửa)",
            ["strengths"] = "", ["limitations"] = "Còn chậm", ["causes"] = "Thiếu người", ["previousRemediation"] = "",
            ["explanation"] = "", ["responsibilities"] = "Bí thư", ["remediationPlan"] = "Bổ sung",
            ["generalCriteriaScore"] = 0, ["taskCriteriaScore"] = 0,
            ["criteriaScores"] = criteriaScores.DeepClone(),
            ["selfProposedGrade"] = "HoanThanhTot",
            ["sections"] = sections07.DeepClone(),
            ["items"] = new JsonArray()
        }));
        Assert.Equal("Chi bộ kiểm thử (sửa)", m07Edit.GetProperty("subjectName").GetString());
        var duplicate = await PostRawAsync(w.CellSec, "/api/evaluations/collective-records", new JsonObject
        {
            ["periodId"] = periodId, ["form"] = "M07", ["partyCellId"] = w.Cell, ["subjectName"] = "Trùng",
            ["strengths"] = "", ["limitations"] = "", ["causes"] = "", ["previousRemediation"] = "", ["explanation"] = "", ["responsibilities"] = "", ["remediationPlan"] = "",
            ["generalCriteriaScore"] = 0, ["taskCriteriaScore"] = 0, ["criteriaScores"] = criteriaScores.DeepClone(), ["selfProposedGrade"] = "HoanThanhTot",
            ["sections"] = new JsonObject(), ["items"] = new JsonArray()
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("hãy mở hồ sơ đó để sửa", (await MessageAsync(duplicate)).ToLowerInvariant());

        // Mẫu 06 của Phòng: số nhiệm vụ dạng chuỗi số, ô văn bản, mức tự xếp loại chọn trên form.
        var sections06 = new JsonObject();
        foreach (var field in catalog.GetProperty("form06Fields").EnumerateArray())
        {
            var code = field.GetProperty("code").GetString()!;
            sections06[code] = code switch
            {
                Hd03FormCatalog.Form06AssignedCode => "10",
                Hd03FormCatalog.Form06OnTimeCode => "7",
                Hd03FormCatalog.Form06LateCode => "2",
                Hd03FormCatalog.Form06ObjectiveCode => "1",
                _ => $"Nội dung {code}"
            };
        }
        var m06 = await Data(await PostRawAsync(w.DeptCollective, "/api/evaluations/collective-records", new JsonObject
        {
            ["version"] = null,
            ["periodId"] = periodId, ["form"] = "M06", ["departmentId"] = w.Dept, ["subjectName"] = "Phòng kiểm thử",
            ["strengths"] = "Hoàn thành", ["limitations"] = "", ["causes"] = "", ["previousRemediation"] = "", ["explanation"] = "", ["responsibilities"] = "", ["remediationPlan"] = "Tiếp tục",
            ["generalCriteriaScore"] = 0, ["taskCriteriaScore"] = 0, ["criteriaScores"] = criteriaScores.DeepClone(), ["selfProposedGrade"] = "HoanThanhXuatSac",
            ["sections"] = sections06, ["items"] = new JsonArray()
        }));
        Assert.Equal("HoanThanhXuatSac", m06.GetProperty("selfProposedGrade").GetString());

        // Mẫu 08: nhóm nội dung chọn từ danh mục, điểm nhóm gửi 0 (form không có ô điểm cho Mẫu 08), sections rỗng.
        var category = catalog.GetProperty("form08Categories")[0].GetProperty("code").GetString();
        var m08 = await Data(await PostRawAsync(w.DeptCollective, "/api/evaluations/collective-records", new JsonObject
        {
            ["version"] = null,
            ["periodId"] = periodId, ["form"] = "M08", ["departmentId"] = w.Dept, ["subjectName"] = "Đơn vị kiểm thử",
            ["strengths"] = "", ["limitations"] = "", ["causes"] = "", ["previousRemediation"] = "", ["explanation"] = "", ["responsibilities"] = "", ["remediationPlan"] = "",
            ["generalCriteriaScore"] = 0, ["taskCriteriaScore"] = 0, ["selfProposedGrade"] = "HoanThanhTot",
            ["sections"] = new JsonObject(),
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["itemOrder"] = 1, ["category"] = category, ["taskName"] = "Nhiệm vụ 1", ["planOrDirection"] = "Kế hoạch",
                    ["result"] = "Đạt", ["limitations"] = "", ["notes"] = ""
                }
            }
        }));
        var m08Id = m08.GetProperty("id").GetGuid();
        Assert.Equal(1, m08.GetProperty("items").GetArrayLength());

        // Xuất như trang Tập thể & Hội nghị: Word cho cả ba mẫu, Excel cho Mẫu 08, PDF nhận 200 hoặc 503 tiếng Việt.
        await AssertFileAsync(w.CellSec, $"/api/reports/docx/mau-07/{m07Id}", DocxType);
        await AssertFileAsync(w.DeptCollective, $"/api/reports/docx/mau-06/{m06.GetProperty("id").GetGuid()}", DocxType);
        await AssertFileAsync(w.DeptCollective, $"/api/reports/docx/mau-08/{m08Id}", DocxType);
        await AssertFileAsync(w.DeptCollective, $"/api/reports/form-08/{m08Id}", XlsxType);
        await AssertPdfOrUnavailableAsync(w.CellSec, $"/api/reports/docx/mau-07/{m07Id}?format=pdf");
        var list = await Data(w.CellSec, $"/api/evaluations/collective-records?periodId={periodId}");
        Assert.Contains(list.EnumerateArray(), r => r.GetProperty("id").GetGuid() == m07Id);
    }

    #endregion

    #region U5 — biên bản hội nghị Mẫu 12, 13

    [SkippableFact]
    public async Task U5_Meetings_M12_M13_AsSentByForm_CreateUpdateExport()
    {
        var w = await WorldAsync();
        var s = await Scenario.StartAsync(w, _factory, gates: GateSet.AllOff);
        var periodId = s.PeriodId;

        // Mẫu 12 của tập thể lãnh đạo Phòng (thư ký): thân yêu cầu đúng như MeetingEditor.
        var m12 = await Data(await PostRawAsync(w.Secretary, "/api/evaluations/meetings", MeetingBody(periodId, "M12", "B3A_COLLECTIVE", department: w.Dept, present: 8, invited: 9)));
        Assert.Equal("B3A_COLLECTIVE", m12.GetProperty("stage").GetString());
        Assert.Equal("Thành viên A", m12.GetProperty("details").GetProperty("attendees")[0].GetProperty("name").GetString());
        var m12Edit = MeetingBody(periodId, "M12", "B3A_COLLECTIVE", department: w.Dept, present: 8, invited: 9);
        m12Edit["version"] = m12.GetProperty("version").GetUInt32();
        m12Edit["location"] = "Phòng họp 2";
        var edited = await Data(await PutRawAsync(w.Secretary, $"/api/evaluations/meetings/{m12.GetProperty("id").GetGuid()}", m12Edit));
        Assert.Equal("Phòng họp 2", edited.GetProperty("location").GetString());

        // Mẫu 13 của Chi bộ do Văn phòng Đảng ủy lập: danh sách hồ sơ lấy theo branchId như form, chỉ gửi hồ sơ đã nhập phiếu.
        var records = await Data(w.Office, $"/api/evaluations/branch-records?periodId={periodId}&branchId={w.Cell}");
        Assert.NotEmpty(records.EnumerateArray());
        var body13 = MeetingBody(periodId, "M13", "B4_DECISION", cell: w.Cell, present: 5, invited: 6);
        body13["voteSummaries"] = new JsonArray(records.EnumerateArray().Select(r => (JsonNode?)new JsonObject
        {
            ["recordId"] = r.GetProperty("id").GetGuid(),
            ["votesExcellent"] = 0, ["votesGood"] = 3, ["votesSatisfactory"] = 1, ["votesUnsatisfactory"] = 0, ["votesNotRated"] = 0, ["invalidVotes"] = 1,
            ["notes"] = ""
        }).ToArray());
        var m13 = await Data(await PostRawAsync(w.Office, "/api/evaluations/meetings", body13));
        Assert.Equal(records.GetArrayLength(), m13.GetProperty("voteSummaries").GetArrayLength());

        // Không nhập phiếu nào: thông báo nêu rõ việc cần làm.
        var empty13 = MeetingBody(periodId, "M13", "B4_DECISION", cell: w.Cell, present: 5, invited: 6);
        var emptyResponse = await PostRawAsync(w.Office, "/api/evaluations/meetings", empty13);
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        Assert.Contains("tổng hợp phiếu", await MessageAsync(emptyResponse));

        // Sửa Mẫu 13: form gửi voteSummaries rỗng, kết quả kiểm phiếu giữ nguyên.
        var edit13 = MeetingBody(periodId, "M13", "B4_DECISION", cell: w.Cell, present: 5, invited: 6);
        edit13["version"] = m13.GetProperty("version").GetUInt32();
        edit13["voteSummaries"] = new JsonArray();
        var after13 = await Data(await PutRawAsync(w.Office, $"/api/evaluations/meetings/{m13.GetProperty("id").GetGuid()}", edit13));
        Assert.Equal(records.GetArrayLength(), after13.GetProperty("voteSummaries").GetArrayLength());

        // Xuất như trang: Mẫu 12, Mẫu 11 phiếu trắng, Mẫu 13.
        var m12Id = m12.GetProperty("id").GetGuid();
        await AssertFileAsync(w.Secretary, $"/api/reports/docx/mau-12/{m12Id}", DocxType);
        await AssertFileAsync(w.Secretary, $"/api/reports/docx/mau-11/meeting/{m12Id}", DocxType);
        await AssertFileAsync(w.Office, $"/api/reports/docx/mau-13/{m13.GetProperty("id").GetGuid()}", DocxType);
        await AssertPdfOrUnavailableAsync(w.Office, $"/api/reports/docx/mau-13/{m13.GetProperty("id").GetGuid()}?format=pdf");
    }

    private static JsonObject MeetingBody(Guid periodId, string form, string stage, Guid? department = null, Guid? cell = null, int present = 5, int invited = 6) =>
        new()
        {
            ["version"] = null,
            ["periodId"] = periodId,
            ["partyCellId"] = cell,
            ["departmentId"] = department,
            ["stage"] = stage,
            ["formCode"] = form,
            ["meetingType"] = stage == "B3A_COLLECTIVE" ? "Hội nghị tập thể lãnh đạo, quản lý" : "Hội nghị đánh giá, xếp loại chất lượng cán bộ quý",
            ["location"] = "Phòng họp 1",
            ["startedAt"] = DateTime.UtcNow.AddHours(-2).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            ["endedAt"] = DateTime.UtcNow.AddHours(-1).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
            ["invitedCount"] = invited,
            ["presentCount"] = present,
            ["absentCount"] = Math.Max(invited - present, 0),
            ["absentReasons"] = "",
            ["chairName"] = "Chủ trì",
            ["secretaryName"] = "Thư ký",
            ["minutesContent"] = "Diễn biến",
            ["outcomeContent"] = "Kết quả",
            ["voteCountingContent"] = "",
            ["details"] = new JsonObject
            {
                ["workingRules"] = "Quy chế",
                ["reportingUnit"] = null,
                ["chairTitle"] = "Bí thư",
                ["secretaryTitle"] = null,
                ["attendees"] = new JsonArray { new JsonObject { ["name"] = "Thành viên A", ["title"] = "Phó phòng" } },
                ["countingCommittee"] = new JsonArray { new JsonObject { ["name"] = "Tổ trưởng", ["title"] = "" } },
                ["ballotsIssued"] = null,
                ["ballotsCollected"] = null,
                ["ballotsValid"] = null,
                ["ballotsInvalid"] = null
            },
            ["voteSummaries"] = new JsonArray()
        };

    #endregion

    #region U6 — biểu mẫu của hồ sơ và báo cáo theo đường dẫn của giao diện

    [SkippableFact]
    public async Task U6_RecordFormsAndReports_UrlsAsBuiltByUi_Export()
    {
        var w = await WorldAsync();
        var s = await Scenario.StartAsync(w, _factory, gates: GateSet.AllOff);
        await s.DriveToPublishedAsync();

        // Khối "Biểu mẫu" của hồ sơ: danh sách mẫu, danh sách phiếu đề nghị (Mẫu 04/05), mỗi mẫu xuất Word.
        var forms = (await Data(s.Owner, $"/api/reports/docx/record/{s.RecordId}")).EnumerateArray().ToList();
        Assert.NotEmpty(forms);
        foreach (var form in forms)
        {
            var code = Uri.EscapeDataString(form.GetProperty("code").GetString()!);
            await AssertFileAsync(s.Owner, $"/api/reports/docx/record/{s.RecordId}/{code}", DocxType);
        }
        await AssertPdfOrUnavailableAsync(s.Owner, $"/api/reports/docx/record/{s.RecordId}/{Uri.EscapeDataString(forms[0].GetProperty("code").GetString()!)}?format=pdf");
        var requests = await Data(s.Owner, $"/api/reports/docx/record/{s.RecordId}/requests");
        Assert.Equal(JsonValueKind.Array, requests.ValueKind);

        // Trang Báo cáo: periodId, có hoặc không branchId.
        var periodId = s.PeriodId;
        foreach (var query in new[] { $"periodId={periodId}", $"periodId={periodId}&branchId={w.Cell}" })
        {
            await AssertFileAsync(w.Manager, $"/api/reports/docx/mau-11?{query}", DocxType);
            await AssertFileAsync(w.Manager, $"/api/reports/form-14?{query}", XlsxType);
            await AssertFileAsync(w.Manager, $"/api/reports/form-15a?{query}", XlsxType);
            await AssertFileAsync(w.Manager, $"/api/reports/form-15b?{query}", XlsxType);
            await AssertFileAsync(w.Manager, $"/api/reports/docx/mau-16?{query}", DocxType);
            await AssertFileAsync(w.Manager, $"/api/reports/internal/rating-summary?{query}", XlsxType);
            await AssertFileAsync(w.Manager, $"/api/reports/internal/excellent-quota?{query}", XlsxType);
        }
        await AssertFileAsync(w.Manager, "/api/reports/cadres", XlsxType);
        var readiness = await Data(w.Manager, $"/api/reports/decision/readiness?periodId={periodId}");
        Assert.True(readiness.GetProperty("ready").GetBoolean(), readiness.GetRawText());
        await AssertFileAsync(w.Manager, $"/api/reports/docx/decision?periodId={periodId}", DocxType);

        // Soạn Mẫu 16: đọc bản nháp rồi lưu với thân yêu cầu của form (version null khi chưa lưu).
        var draft = await Data(w.Manager, $"/api/reports/mau-16/draft?periodId={periodId}");
        var save = await Data(await PutRawAsync(w.Manager, $"/api/reports/mau-16/draft?periodId={periodId}", new JsonObject
        {
            ["content"] = new JsonObject { ["documentNumber"] = "15-BC/ĐU", ["recipient"] = null, ["proposal1"] = "Đề xuất 1" }
        }));
        Assert.Equal("15-BC/ĐU", save.GetProperty("content").GetProperty("documentNumber").GetString());
        Assert.Equal(JsonValueKind.Number, save.GetProperty("version").ValueKind);
        Assert.True(draft.TryGetProperty("baseRows", out _));
        // Người không có quyền xuất báo cáo nhận 403 để giao diện hiện thông báo.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync($"/api/reports/form-14?periodId={periodId}")).StatusCode);
    }

    #endregion

    #region U7, U8 — danh sách người được đánh giá và điều chỉnh việc theo thân yêu cầu của giao diện

    [SkippableFact]
    public async Task U7_Participants_AsSentByUi_AddByUnit_ChangeProfile_Snapshot_Remove()
    {
        var w = await WorldAsync();
        var periodId = await CreatePeriodAsUiAsync(w, GateSet.AllOff);
        var a = await w.NewOwnerAsync(_factory);
        var b = await w.NewOwnerAsync(_factory);

        // Tìm cán bộ: chỉ truyền tham số có giá trị.
        var candidates = await Data(w.Manager, $"/api/evaluations/periods/{periodId}/candidates?departmentId={w.Dept}");
        Assert.Contains(candidates.EnumerateArray(), c => c.GetProperty("memberId").GetGuid() == a.User.Id && !c.GetProperty("alreadyAdded").GetBoolean());

        // Chọn tay (memberIds), rồi "Thêm cả đơn vị" (memberIds rỗng + departmentId): người đã có được liệt kê trong skipped.
        var manual = await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { a.User.Id } }));
        Assert.Equal(1, manual.GetProperty("added").GetInt32());
        var byUnit = await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants", new { memberIds = Array.Empty<Guid>(), departmentId = w.Dept }));
        Assert.True(byUnit.GetProperty("added").GetInt32() >= 1);
        Assert.NotEmpty(byUnit.GetProperty("skipped").EnumerateArray());

        var rows = (await Data(w.Manager, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray().ToList();
        var rowA = rows.First(r => r.GetProperty("memberId").GetGuid() == a.User.Id);
        var rowB = rows.First(r => r.GetProperty("memberId").GetGuid() == b.User.Id);
        foreach (var field in new[] { "recordId", "version", "weightFrameCode", "weightFrameLocked", "approvalAuthority", "workflowProfileCode", "workflowProfileName", "status", "statusDisplayName", "currentStep", "currentStepName" })
            Assert.True(rowA.TryGetProperty(field, out _), field);

        // Đổi diện quyết định một người (hộp thoại: version, workflowProfileCode, reason).
        var changed = await Data(await PutRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants/{rowA.GetProperty("recordId").GetGuid()}/profile",
            new { version = rowA.GetProperty("version").GetUInt32(), workflowProfileCode = "bi-thu-nhan-vien", reason = "Bí thư Chi bộ là nhân viên" }));
        Assert.Equal("bi-thu-nhan-vien", changed.GetProperty("workflowProfileCode").GetString());

        // Đổi hàng loạt: items gồm recordId và version đã tải.
        var bulk = await Data(await PutRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants/profile", new
        {
            items = new[]
            {
                new { recordId = changed.GetProperty("recordId").GetGuid(), version = changed.GetProperty("version").GetUInt32() },
                new { recordId = rowB.GetProperty("recordId").GetGuid(), version = rowB.GetProperty("version").GetUInt32() }
            },
            workflowProfileCode = "co-so",
            reason = "Theo quyết định mới"
        }));
        Assert.True(bulk.GetProperty("updated").GetInt32() >= 1);
        Assert.Equal(JsonValueKind.Array, bulk.GetProperty("skipped").ValueKind);

        // Sửa thông tin: form luôn gửi đủ Phòng, Chi bộ, khung, cấp quyết định (khung trống nếu cán bộ chưa có khung).
        rows = (await Data(w.Manager, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray().ToList();
        rowB = rows.First(r => r.GetProperty("memberId").GetGuid() == b.User.Id);
        var snapshot = await Data(await PutRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants/{rowB.GetProperty("recordId").GetGuid()}/snapshot", new
        {
            version = rowB.GetProperty("version").GetUInt32(),
            departmentId = (Guid?)w.Dept,
            partyCellId = (Guid?)w.Cell,
            weightFrameCode = rowB.GetProperty("weightFrameCode").GetString() ?? "",
            approvalAuthority = "CapTren",
            reason = "Chuyển sang diện cấp trên"
        }));
        Assert.Equal("CapTren", snapshot.GetProperty("approvalAuthority").GetString());
        var unchanged = await PutRawAsync(w.Manager, $"/api/evaluations/periods/{periodId}/participants/{rowB.GetProperty("recordId").GetGuid()}/snapshot", new
        {
            version = snapshot.GetProperty("version").GetUInt32(), departmentId = (Guid?)w.Dept, partyCellId = (Guid?)w.Cell,
            weightFrameCode = snapshot.GetProperty("weightFrameCode").GetString() ?? "", approvalAuthority = "CapTren", reason = "Không đổi gì"
        });
        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);
        Assert.Contains("thay đổi", await MessageAsync(unchanged));

        // Bỏ khỏi kỳ: version nằm trong query.
        var removed = await w.Manager.DeleteAsync($"/api/evaluations/periods/{periodId}/participants/{rowB.GetProperty("recordId").GetGuid()}?version={snapshot.GetProperty("version").GetUInt32()}");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
    }

    [SkippableFact]
    public async Task U8_TaskChangeRequests_AsSentByUi_AdjustUrgentExclude_ThenRequestFormsExport()
    {
        var w = await WorldAsync();
        var s = await Scenario.StartAsync(w, _factory, gates: GateSet.AllOff);
        await s.RegisterAsync();
        await s.ApproveAsync();
        var baseUrl = $"/api/evaluations/records/{s.RecordId}/task-changes";

        var state = await Data(s.Owner, baseUrl);
        Assert.True(state.GetProperty("applicable").GetBoolean());
        Assert.True(state.GetProperty("canRequestChange").GetBoolean());
        var record = await Data(s.Owner, $"/api/evaluations/records/{s.RecordId}");
        var tasks = record.GetProperty("tasks").EnumerateArray().ToList();
        var ids = tasks.Select(t => t.GetProperty("id").GetGuid()).ToList();

        // "Đề nghị điều chỉnh": nội dung điền sẵn từ việc được chọn, thời hạn cắt còn yyyy-MM-dd, trọng số của mọi việc.
        var target = tasks[0];
        var adjust = await Data(await PostRawAsync(s.Owner, baseUrl, new
        {
            version = record.GetProperty("version").GetUInt32(),
            type = "Adjust",
            taskId = ids[0],
            proposal = new
            {
                taskName = "Sản phẩm 1 (điều chỉnh)", targetOutput = target.GetProperty("targetOutput").GetString(),
                deadline = target.GetProperty("deadline").GetString()![..10], axisCode = "T1", role = "ChuTri",
                exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc"
            },
            weights = tasks.ToDictionary(t => t.GetProperty("id").GetGuid(), t => t.GetProperty("weight").GetDouble()),
            reason = "Đổi theo chỉ đạo"
        }));
        var request = adjust.GetProperty("requests")[0];
        await s.AssertQueueAsync(w.Lead, "TASK_CHANGES");
        // Duyệt: thân yêu cầu chỉ có version khi không nhập ý kiến.
        var approved = await Data(await PostRawAsync(w.Lead, $"{baseUrl}/{request.GetProperty("id").GetGuid()}/approve", new { version = request.GetProperty("version").GetUInt32() }));
        Assert.Equal("Approved", approved.GetProperty("requests")[0].GetProperty("status").GetString());

        // "Ghi nhận việc đột xuất": taskId null, trọng số từ ô nhập, các ô tùy chọn để trống; giảm các việc cũ để giữ tổng 70.
        record = await Data(s.Owner, $"/api/evaluations/records/{s.RecordId}");
        var urgent = await Data(await PostRawAsync(w.Lead, baseUrl, new
        {
            version = record.GetProperty("version").GetUInt32(),
            type = "Urgent",
            taskId = (Guid?)null,
            proposal = new { taskName = "Sự cố đột xuất", targetOutput = "Khắc phục", axisCode = "T1", role = "ChuTri", deadline = (string?)null, weight = 10.0 },
            weights = new Dictionary<Guid, double> { [ids[0]] = 20, [ids[1]] = 20, [ids[2]] = 20 },
            reason = "Phát sinh theo chỉ đạo"
        }));
        var urgentRequest = urgent.GetProperty("requests").EnumerateArray().First(r => r.GetProperty("type").GetString() == "Urgent");
        await Data(await PostRawAsync(w.Lead, $"{baseUrl}/{urgentRequest.GetProperty("id").GetGuid()}/approve", new { version = urgentRequest.GetProperty("version").GetUInt32() }));

        // "Loại trừ" việc 2: không gửi proposal; không duyệt thiếu lý do bị từ chối, có lý do thì ghi nhận.
        record = await Data(s.Owner, $"/api/evaluations/records/{s.RecordId}");
        var urgentTaskId = record.GetProperty("tasks").EnumerateArray().First(t => t.GetProperty("kind").GetString() == "DotXuat").GetProperty("id").GetGuid();
        var exclude = await Data(await PostRawAsync(s.Owner, baseUrl, new
        {
            version = record.GetProperty("version").GetUInt32(),
            type = "Exclude",
            taskId = ids[1],
            weights = new Dictionary<Guid, double> { [ids[0]] = 25, [ids[2]] = 25, [urgentTaskId] = 20 },
            reason = "Dự án tạm dừng do nguyên nhân khách quan"
        }));
        var excludeRequest = exclude.GetProperty("requests").EnumerateArray().First(r => r.GetProperty("type").GetString() == "Exclude");
        var noComment = await PostRawAsync(w.Lead, $"{baseUrl}/{excludeRequest.GetProperty("id").GetGuid()}/reject", new { version = excludeRequest.GetProperty("version").GetUInt32(), comment = "" });
        Assert.Equal(HttpStatusCode.BadRequest, noComment.StatusCode);
        var rejected = await Data(await PostRawAsync(w.Lead, $"{baseUrl}/{excludeRequest.GetProperty("id").GetGuid()}/reject",
            new { version = excludeRequest.GetProperty("version").GetUInt32(), comment = "Chưa có xác nhận của cấp thẩm quyền" }));
        Assert.Contains(rejected.GetProperty("requests").EnumerateArray(), r => r.GetProperty("status").GetString() == "Rejected");

        // Khối "Biểu mẫu": mỗi đề nghị một phiếu Mẫu 04/05; đường dẫn xuất ghép mã mẫu và requestId như giao diện.
        var requestForms = (await Data(s.Owner, $"/api/reports/docx/record/{s.RecordId}/requests")).EnumerateArray().ToList();
        Assert.Equal(new[] { "04", "05", "04" }, requestForms.Select(f => f.GetProperty("code").GetString()));
        foreach (var form in requestForms)
        {
            var url = $"/api/reports/docx/record/{s.RecordId}/{Uri.EscapeDataString(form.GetProperty("code").GetString()!)}/{form.GetProperty("requestId").GetGuid()}";
            await AssertFileAsync(s.Owner, url, DocxType);
        }
    }

    #endregion

    #region U9 — nhắc việc dẫn đúng nơi làm việc

    [SkippableFact]
    public async Task U9_Notifications_CrosscheckItemLinksToTheForm_WorkQueueItemHasFormId()
    {
        var w = await WorldAsync();
        var s = await Scenario.StartAsync(w, _factory, gates: GateSet.AllOff);
        await s.RegisterAsync();
        await s.ApproveAsync();
        await s.SelfScoreDraftThenSubmitAsync();
        await s.CellConfirmAsync();
        await s.CollectiveAsync();

        var created = await Data(await PostRawAsync(w.Manager, $"/api/evaluations/records/{s.RecordId}/crosschecks", new
        {
            expertDepartmentId = w.ExpertDept,
            fields = new[] { "SAFETY_QUALITY" },
            addresseeName = "Phòng chuyên môn",
            requestReason = "Xác nhận số liệu sự cố trong quý",
            items = new object[] { new { itemName = "Số sự cố nghiêm trọng", selfData = "0" } }
        }));
        var formId = created.GetProperty("id").GetGuid();

        // Người của cơ quan chuyên môn không xem được hồ sơ nên đường dẫn từ chuông nhắc việc phải tới phiếu.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Responder.GetAsync($"/api/evaluations/records/{s.RecordId}")).StatusCode);
        var queue = await Data(w.Responder, "/api/evaluations/work-queue");
        var group = queue.GetProperty("groups").EnumerateArray().First(g => g.GetProperty("step").GetString() == "CROSSCHECKS");
        Assert.Contains(group.GetProperty("items").EnumerateArray(), i => i.GetProperty("formId").GetGuid() == formId);
        var summary = await Data(w.Responder, "/api/notifications/summary");
        Assert.Contains(summary.GetProperty("items").EnumerateArray(), i => i.GetProperty("link").GetString() == $"/crosschecks/{formId}");
        Assert.DoesNotContain(summary.GetProperty("items").EnumerateArray(), i => i.GetProperty("link").GetString() == $"/evaluations/{s.RecordId}");
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

    private enum GateSet { Default, AllOff, Recorded, Required }

    /// <summary>Tạo kỳ như hộp thoại "Tạo kỳ" (ngày dạng yyyy-MM-dd), rồi lưu cấu hình cổng như bảng diện quyết định.</summary>
    private static async Task<Guid> CreatePeriodAsUiAsync(World w, GateSet gates)
    {
        var year = TestPeriodSlots.NextYear();
        var created = await Data(await PostRawAsync(w.Manager, "/api/evaluations/periods", new
        {
            year,
            quarter = TestPeriodSlots.Quarter,
            name = $"Kỳ UI {Guid.NewGuid().ToString("N")[..8]}",
            startDate = $"{year}-10-01",
            endDate = $"{year}-12-31",
            criteriaSetId = await TestCriteriaSets.LatestPublishedAsync(w.Manager)
        }));
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        if (gates == GateSet.Default)
            return id;

        var settings = JsonNode.Parse(created.GetProperty("settings").GetRawText())!.AsObject();
        var mode = gates switch { GateSet.AllOff => "Off", GateSet.Required => "Required", _ => "Recorded" };
        foreach (var profile in settings["profiles"]!.AsArray())
        {
            profile!["gates"]!["CALIBRATION"] = new JsonObject { ["mode"] = mode };
            profile["gates"]!["CONTROL"] = new JsonObject { ["mode"] = mode };
        }
        await Data(await PutPeriodAsync(w.Manager, id, created.GetProperty("version").GetUInt32(), settings));
        return id;
    }

    private static Task<HttpResponseMessage> PutPeriodAsync(HttpClient client, Guid periodId, uint version, JsonNode settings, bool applyDeadlineRules = false)
    {
        var body = new JsonObject { ["version"] = version, ["settings"] = settings.DeepClone() };
        if (applyDeadlineRules)
            body["applyDeadlineRules"] = true;
        return PutRawAsync(client, $"/api/evaluations/periods/{periodId}", body);
    }

    private static Task<HttpResponseMessage> PostRawAsync(HttpClient client, string url, object body) =>
        client.PostAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

    private static Task<HttpResponseMessage> PutRawAsync(HttpClient client, string url, object body) =>
        client.PutAsync(url, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

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

    private static async Task AssertFileAsync(HttpClient client, string url, string contentType)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {url}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
        Assert.True((await response.Content.ReadAsByteArrayAsync()).Length > 1000, url);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.False(string.IsNullOrWhiteSpace(disposition?.FileName ?? disposition?.FileNameStar), $"Thiếu tên tệp: {url}");
    }

    /// <summary>Máy chủ không có LibreOffice thì 503 kèm thông báo tiếng Việt để giao diện hiện cho người dùng.</summary>
    private static async Task AssertPdfOrUnavailableAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            return;
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var message = await MessageAsync(response);
        Assert.Contains("PDF", message);
        Assert.DoesNotContain("Không khởi động được", message);
    }

    private sealed class World
    {
        public Guid Dept, Cell, ExpertDept;
        public HttpClient Responder = null!;
        public HttpClient Manager = null!, CellSec = null!, Secretary = null!, Lead = null!, Director = null!, Office = null!, DeptCollective = null!;

        public async Task<(TestUser User, HttpClient Client)> NewOwnerAsync(ApiFactory factory)
        {
            var user = await factory.CreateUserAsync(Dept, Cell, ApprovalAuthority.CoSo, $"UI Chủ hồ sơ {Guid.NewGuid().ToString("N")[..6]}");
            await factory.AssignAsync(user.Id, await factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
            return (user, await factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true));
        }

        public static async Task<World> CreateAsync(ApiFactory factory)
        {
            var w = new World();
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            await factory.WithDbAsync(async db =>
            {
                var dept = new AdministrativeDepartment { Code = $"UI-P-{suffix}", Name = $"Phòng UI {suffix}" };
                var cell = new PartyCell { Code = $"UI-C-{suffix}", Name = $"Chi bộ UI {suffix}" };
                db.AddRange(dept, cell);
                await db.SaveChangesAsync();
                w.Dept = dept.Id;
                w.Cell = cell.Id;
                var expert = new AdministrativeDepartment { Code = $"UI-E-{suffix}", Name = $"Phòng chuyên môn UI {suffix}" };
                db.Add(expert);
                await db.SaveChangesAsync();
                w.ExpertDept = expert.Id;
            });

            var manager = await factory.CreateUserAsync();
            var cellSec = await factory.CreateUserAsync();
            var secretary = await factory.CreateUserAsync();
            var lead = await factory.CreateUserAsync(w.Dept);
            var director = await factory.CreateUserAsync();
            var office = await factory.CreateUserAsync();
            var deptCollective = await factory.CreateUserWithPermissionsAsync("collective.manage", "evaluation.read");
            var responder = await factory.CreateUserAsync(w.ExpertDept, null);
            await factory.AssignAsync(responder.Id, (await factory.CreateRoleAsync("evaluation.crosscheck.respond")).Id, RoleScopeType.Department, w.ExpertDept);

            await factory.AssignAsync(manager.Id, await factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await factory.AssignAsync(cellSec.Id, await factory.GetRoleIdAsync("CHI_UY_CHI_BO"), RoleScopeType.PartyCell, w.Cell);
            await factory.AssignAsync(secretary.Id, await factory.GetRoleIdAsync("THU_KY_TAP_THE"), RoleScopeType.Department, w.Dept);
            await factory.AssignAsync(lead.Id, await factory.GetRoleIdAsync("LANH_DAO_PHONG"), RoleScopeType.Department, w.Dept);
            await factory.AssignAsync(director.Id, await factory.GetRoleIdAsync("CAP_TRUC_TIEP_SU_DUNG"), RoleScopeType.Global, null);
            await factory.AssignAsync(office.Id, await factory.GetRoleIdAsync("VAN_PHONG_DANG_UY"), RoleScopeType.Global, null);

            Task<HttpClient> Login(TestUser user) => factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            w.Manager = await Login(manager);
            w.CellSec = await Login(cellSec);
            w.Secretary = await Login(secretary);
            w.Lead = await Login(lead);
            w.Director = await Login(director);
            w.Office = await Login(office);
            w.DeptCollective = await Login(deptCollective);
            w.Responder = await Login(responder);
            return w;
        }
    }

    /// <summary>Một hồ sơ trong kỳ riêng, mỗi bước gửi đúng thân yêu cầu của biểu mẫu thao tác trên giao diện.</summary>
    private sealed class Scenario
    {
        private readonly World _w;
        private readonly ApiFactory _factory;

        public Guid PeriodId, RecordId;
        public HttpClient Owner = null!;
        public string Grade = string.Empty;
        private Guid _b4Meeting;

        private Scenario(World w, ApiFactory factory)
        {
            _w = w;
            _factory = factory;
        }

        public HttpClient Office => _w.Office;

        public static async Task<Scenario> StartAsync(World w, ApiFactory factory, GateSet gates)
        {
            var s = new Scenario(w, factory);
            s.PeriodId = await CreatePeriodAsUiAsync(w, gates);
            var (user, client) = await w.NewOwnerAsync(factory);
            s.Owner = client;
            // Thêm người (memberIds), mở kỳ như trang cấu hình kỳ.
            await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{s.PeriodId}/participants", new { memberIds = new[] { user.Id } }));
            var period = await Data(w.Manager, $"/api/evaluations/periods/{s.PeriodId}");
            var readiness = await Data(w.Manager, $"/api/evaluations/periods/{s.PeriodId}/readiness");
            Assert.True(readiness.GetProperty("ready").GetBoolean(), readiness.GetRawText());
            await Data(await PostRawAsync(w.Manager, $"/api/evaluations/periods/{s.PeriodId}/open", new { version = period.GetProperty("version").GetUInt32(), force = false }));
            s.RecordId = (await Data(w.Manager, $"/api/evaluations/periods/{s.PeriodId}/participants")).EnumerateArray()
                .First(p => p.GetProperty("memberId").GetGuid() == user.Id).GetProperty("recordId").GetGuid();
            return s;
        }

        /// <summary>Như <c>performAction</c> của frontend: thân yêu cầu cộng <c>version</c> của hồ sơ đã tải.</summary>
        public async Task<HttpResponseMessage> ActAsync(HttpClient client, string path, object payload)
        {
            var node = JsonSerializer.SerializeToNode(payload)!.AsObject();
            node["version"] = (await Data(_w.Manager, $"/api/evaluations/records/{RecordId}")).GetProperty("version").GetUInt32();
            return await client.PostAsync($"/api/evaluations/records/{RecordId}/{path}",
                new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json"));
        }

        private async Task<JsonElement> StepAsync(HttpClient client, string path, object payload, string expectedStatus)
        {
            var data = await Data(await ActAsync(client, path, payload));
            Assert.Equal(expectedStatus, data.GetProperty("status").GetString());
            return data;
        }

        public async Task<JsonElement> QueueItemAsync(HttpClient client, string step)
        {
            var queue = await Data(client, "/api/evaluations/work-queue");
            var group = queue.GetProperty("groups").EnumerateArray()
                .FirstOrDefault(g => g.GetProperty("step").GetString() == step
                    && g.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("recordId").GetGuid() == RecordId));
            Assert.True(group.ValueKind == JsonValueKind.Object, $"Không có việc {step} của hồ sơ trong danh sách việc cần xử lý: {queue.GetRawText()}");
            var item = group.GetProperty("items").EnumerateArray().First(i => i.GetProperty("recordId").GetGuid() == RecordId);
            foreach (var field in new[] { "version", "periodName", "fullName", "status", "statusDisplayName", "mode", "isOwnRecord", "overdue", "workflowProfileName" })
                Assert.True(item.TryGetProperty(field, out _), field);
            Assert.False(string.IsNullOrWhiteSpace(group.GetProperty("stepName").GetString()));
            return item;
        }

        public async Task AssertQueueAsync(HttpClient client, string step) => await QueueItemAsync(client, step);

        /// <summary>TasksForm: ô trống gửi null/chuỗi rỗng, ô chưa chọn mã SP và hạn thành null.</summary>
        public async Task RegisterAsync()
        {
            await AssertQueueAsync(Owner, "B1_REGISTER");
            var tasks = new[] { ("Sản phẩm 1", 30.0, "TrongTam", "ChuTri"), ("Sản phẩm 2", 20.0, "TrongTam", "ChuTri"), ("Sản phẩm 3", 20.0, "ThuongXuyen", "PhoiHop") }.Select(t => new
            {
                taskName = t.Item1, targetOutput = $"Kết quả {t.Item1}", weight = t.Item2, deadline = (string?)null, attachmentId = (Guid?)null,
                axisCode = "T1", productCode = (string?)null, kind = t.Item3, role = t.Item4,
                exceedCriteria = "Vượt tiến độ", evidenceSource = "Biên bản nghiệm thu", confirmer = "Giám đốc"
            }).ToArray();
            await StepAsync(Owner, "tasks/submit", new { tasks }, "AwaitingTaskApproval");
        }

        public async Task ApproveAsync()
        {
            await AssertQueueAsync(_w.Lead, "B1_APPROVE");
            var actions = await Data(_w.Lead, $"/api/evaluations/records/{RecordId}/actions");
            Assert.Contains(actions.GetProperty("actions").EnumerateArray(), a => a.GetProperty("action").GetString() == "ApproveTasks");
            await StepAsync(_w.Lead, "tasks/approve", new { comment = "" }, "AwaitingSelfScore");
        }

        /// <summary>SelfScoreForm: lưu nháp khi chưa nhập tỷ lệ (taskScores rỗng), rồi nộp với đủ bốn tỷ lệ.</summary>
        public async Task SelfScoreDraftThenSubmitAsync()
        {
            await AssertQueueAsync(Owner, "B2_SELF_SCORE");
            var record = await Data(Owner, $"/api/evaluations/records/{RecordId}");
            var axisReports = new Dictionary<string, object>
            {
                ["T1"] = new
                {
                    tasks = record.GetProperty("tasks").EnumerateArray().Select(t => new { content = t.GetProperty("taskName").GetString(), level = (int?)null }).ToArray(),
                    deadline = (string?)null, status = (string?)null, product = (string?)null, progress = (string?)null, note = (string?)null
                }
            };
            object Common(object[] taskScores) => new
            {
                generalScores = CriteriaTestData.General(),
                generalGroupNotes = new Dictionary<string, string>(),
                selfProposedGrade = (string?)null,
                selfGradeExplanation = "",
                selfAssessment = new Dictionary<string, string> { ["I"] = "Tự đánh giá của cá nhân" },
                axisReports,
                taskScores
            };

            var draft = await Data(await Owner.PostAsync($"/api/evaluations/records/{RecordId}/self-score/draft",
                new StringContent(WithVersion(Common(Array.Empty<object>()), record.GetProperty("version").GetUInt32()), Encoding.UTF8, "application/json")));
            Assert.Equal("AwaitingSelfScore", draft.GetProperty("status").GetString());
            var ratioA = 1.0;
            var scores = record.GetProperty("tasks").EnumerateArray().Select(t => (object)new
            {
                taskId = t.GetProperty("id").GetGuid(), criteriaA_Ratio = ratioA, criteriaB_Ratio = 0.9, criteriaC_Ratio = 1.0, criteriaD_Ratio = 1.0,
                isExceedStandard = false, attachmentId = (Guid?)null
            }).ToArray();
            // Nộp dùng phiên bản mới của lần lưu nháp (trang gửi payload.version).
            var submitted = await Data(await Owner.PostAsync($"/api/evaluations/records/{RecordId}/self-score/submit",
                new StringContent(WithVersion(Common(scores), draft.GetProperty("version").GetUInt32()), Encoding.UTF8, "application/json")));
            Assert.Equal("AwaitingCellConfirm", submitted.GetProperty("status").GetString());
            Grade = submitted.GetProperty("selfProposedGrade").GetString()!;
            Assert.NotEqual("ChuaXepLoai", Grade);
        }

        private static string WithVersion(object payload, uint version)
        {
            var node = JsonSerializer.SerializeToNode(payload)!.AsObject();
            node["version"] = version;
            return node.ToJsonString();
        }

        public async Task CellConfirmAsync()
        {
            await AssertQueueAsync(_w.CellSec, "B2_CELL_CONFIRM");
            await StepAsync(_w.CellSec, "cell/confirm", new { comment = "" }, "AwaitingCollective");
        }

        /// <summary>ProposalForm B3a: biên bản chọn từ danh sách, phiếu ghi kèm khi chọn "Ghi kết quả kiểm phiếu".</summary>
        public async Task CollectiveAsync()
        {
            await AssertQueueAsync(_w.Secretary, "B3A_COLLECTIVE");
            var meeting = await Data(await PostRawAsync(_w.Secretary, "/api/evaluations/meetings", MeetingBody(PeriodId, "M12", "B3A_COLLECTIVE", department: _w.Dept, present: 8, invited: 9)));
            var meetings = await Data(_w.Secretary, $"/api/evaluations/meetings?periodId={PeriodId}");
            Assert.Contains(meetings.EnumerateArray(), m => m.GetProperty("id").GetGuid() == meeting.GetProperty("id").GetGuid());
            await StepAsync(_w.Secretary, "collective", new
            {
                meetingId = meeting.GetProperty("id").GetGuid(),
                votes = new
                {
                    votesExcellent = Grade == "HoanThanhXuatSac" ? 6 : 0, votesGood = Grade == "HoanThanhTot" ? 6 : 0,
                    votesSatisfactory = Grade == "HoanThanh" ? 6 : 0, votesUnsatisfactory = Grade == "KhongHoanThanh" ? 6 : 0,
                    votesNotRated = 0, invalidVotes = 1, notes = ""
                },
                proposedGrade = Grade,
                comment = ""
            }, "AwaitingAppraisal");
        }

        /// <summary>AppraisalForm: hai điểm nhóm lấy từ hồ sơ, mức từng nhiệm vụ, giải trình null khi không cần.</summary>
        public async Task AppraiseAsync()
        {
            await AssertQueueAsync(_w.Manager, "B3B_APPRAISAL");
            var record = await Data(_w.Manager, $"/api/evaluations/records/{RecordId}");
            await StepAsync(_w.Manager, "appraisal", new
            {
                generalScore = record.GetProperty("generalCriteriaScore").GetDouble(),
                tasksScore = record.GetProperty("tasksScore").GetDouble(),
                proposedGrade = Grade,
                comment = "",
                explanation = (string?)null,
                taskLevels = record.GetProperty("tasks").EnumerateArray()
                    .Select(t => new { taskId = t.GetProperty("id").GetGuid(), level = 1, comment = (string?)null }).ToArray()
            }, "AwaitingDirectorReview");
        }

        public async Task DirectorReviewAsync()
        {
            await AssertQueueAsync(_w.Director, "B3C_DIRECTOR");
            await StepAsync(_w.Director, "director-review", new { proposedGrade = Grade, comment = "" }, "AwaitingDecision");
        }

        /// <summary>ProposalForm B4: điểm để trống (lấy điểm thẩm định), số văn bản có thể trống, biên bản của Chi bộ.</summary>
        public async Task<HttpResponseMessage> DecideAsync(bool expectOk)
        {
            await AssertQueueAsync(_w.Office, "B4_DECISION");
            if (_b4Meeting == Guid.Empty)
            {
                var meeting = await Data(await PostRawAsync(_w.Office, "/api/evaluations/meetings", MeetingBody(PeriodId, "M12", "B4_DECISION", cell: _w.Cell, present: 5, invited: 6)));
                _b4Meeting = meeting.GetProperty("id").GetGuid();
            }
            var response = await ActAsync(_w.Office, "decision", new
            {
                meetingId = _b4Meeting,
                votes = (object?)null,
                finalGrade = Grade,
                finalScore = (double?)null,
                documentNumber = "",
                documentDate = (string?)null,
                authorityName = "Đảng ủy cơ sở",
                cadreWorkProposal = (string?)null,
                explanation = (string?)null
            });
            if (expectOk)
            {
                var data = await Data(response);
                Assert.Equal("AwaitingPublish", data.GetProperty("status").GetString());
                Assert.Equal(Grade, data.GetProperty("finalGrade").GetString());
                Assert.True(data.GetProperty("finalScore").GetDouble() > 0);
                // Bản sao dữ liệu quyết định để lần sau sửa lại.
                return new HttpResponseMessage(HttpStatusCode.OK);
            }
            return response;
        }

        public async Task PublishAsync()
        {
            await AssertQueueAsync(_w.Office, "B5_PUBLISH");
            await StepAsync(_w.Office, "publish", new { }, "Published");
        }

        public async Task DriveToDecisionAsync()
        {
            await RegisterAsync();
            await ApproveAsync();
            await SelfScoreDraftThenSubmitAsync();
            await CellConfirmAsync();
            await CollectiveAsync();
            await AppraiseAsync();
            await DirectorReviewAsync();
        }

        public async Task DriveToPublishedAsync()
        {
            await DriveToDecisionAsync();
            await DecideAsync(expectOk: true);
            await PublishAsync();
        }
    }

    #endregion
}
