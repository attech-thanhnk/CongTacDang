using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 16 trên PostgreSQL thật: (C1) tạo bộ nháp → xuất bản → kỳ chọn bộ → mở kỳ (chụp) → nhân bản + xuất bản + lưu trữ bộ
/// gốc không ảnh hưởng kỳ đã mở; tự chấm 17 tiêu chí + K/AD → thẩm định chênh ≥ ngưỡng bắt buộc giải trình → Đã công bố,
/// xuất Mẫu 10. (C2) bộ 09A: khung tỷ trọng phải có trong bộ (kẹt luồng), nhiệm vụ gắn mã trục, tính A-B-C-D theo khung,
/// xuất Mẫu 01/02. (C3) quyền <c>criteria.manage</c>, danh mục khung cho hồ sơ cán bộ.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CriteriaSetIntegrationTests
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, World> Worlds = new();

    private readonly ApiFactory _factory;

    public CriteriaSetIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task C1_DraftPublishSnapshot_SelfScoreWithNotApplicable_ExplanationRequired_ToPublished()
    {
        var w = await WorldAsync();

        // Hai bộ mặc định theo bản trích xuất HD03 đã xuất bản.
        var sets = await Data(w.Manager, "/api/criteria-sets");
        Assert.Contains(sets.EnumerateArray(), s => s.GetProperty("code").GetString() == CriteriaSetDefaults.Code09B && s.GetProperty("status").GetString() == "Published");
        Assert.Contains(sets.EnumerateArray(), s => s.GetProperty("code").GetString() == CriteriaSetDefaults.Code09A && s.GetProperty("generalItemCount").GetInt32() == 17);

        // 1. Tạo bản nháp 09B (nội dung mặc định), sửa sai → lưu được nhưng không xuất bản được; sửa đúng → xuất bản.
        var code = $"IT-09B-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
        var draft = await Data(await w.Manager.PostAsJsonAsync("/api/criteria-sets", new { code, name = "Bộ thử 09B", selfScoreForm = "09B" }));
        var setId = draft.GetProperty("id").GetGuid();
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Empty(draft.GetProperty("validationErrors").EnumerateArray());
        Assert.Equal(HttpStatusCode.Conflict, (await w.Manager.PostAsJsonAsync("/api/criteria-sets", new { code, name = "Trùng", selfScoreForm = "09B" })).StatusCode);

        var content = JsonNode.Parse(draft.GetProperty("content").GetRawText())!.AsObject();
        content["axes"]![0]!["maxScore"] = 16;
        var broken = await Data(await w.Manager.PutAsJsonAsync($"/api/criteria-sets/{setId}", new { version = draft.GetProperty("version").GetUInt32(), content }));
        Assert.Contains(broken.GetProperty("validationErrors").EnumerateArray(), e => e.GetString()!.Contains("(71)"));
        var refused = await w.Manager.PostAsJsonAsync($"/api/criteria-sets/{setId}/publish", new { version = broken.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("tổng điểm tối đa các trục (71)", await MessageAsync(refused));

        content["axes"]![0]!["maxScore"] = 15;
        var fixedSet = await Data(await w.Manager.PutAsJsonAsync($"/api/criteria-sets/{setId}", new { version = broken.GetProperty("version").GetUInt32(), content, notes = "Bộ thử của test tích hợp" }));
        var published = await Data(await w.Manager.PostAsJsonAsync($"/api/criteria-sets/{setId}/publish", new { version = fixedSet.GetProperty("version").GetUInt32() }));
        Assert.Equal("Published", published.GetProperty("status").GetString());
        var publishedVersion = published.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.Conflict, (await w.Manager.PutAsJsonAsync($"/api/criteria-sets/{setId}", new { version = publishedVersion, name = "Sửa" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await w.Manager.DeleteAsync($"/api/criteria-sets/{setId}?version={publishedVersion}")).StatusCode);

        // 2. Kỳ (Quý III — chuyển tiếp) chọn bộ vừa xuất bản; mở kỳ → chụp nguyên bộ.
        var period = await CreatePeriodAsync(w, "q3-2026-transition", setId);
        var periodId = period.GetProperty("id").GetGuid();
        Assert.Equal(setId, period.GetProperty("criteriaSetId").GetGuid());
        Assert.Equal(code, period.GetProperty("criteria").GetProperty("code").GetString());
        var recordId = await AddParticipantAsync(w, periodId, w.Owner1.Id);
        await OpenAsync(w, periodId);

        // 3. Nhân bản → sửa bản nháp (ngưỡng giải trình 3) → xuất bản; lưu trữ bộ gốc. Kỳ đã mở giữ nguyên ảnh chụp.
        var clone = await Data(await w.Manager.PostAsJsonAsync($"/api/criteria-sets/{setId}/clone", new { }));
        var cloneId = clone.GetProperty("id").GetGuid();
        Assert.Equal("Draft", clone.GetProperty("status").GetString());
        Assert.Equal(setId, clone.GetProperty("sourceSetId").GetGuid());
        Assert.StartsWith(code, clone.GetProperty("code").GetString());
        var cloneContent = JsonNode.Parse(clone.GetProperty("content").GetRawText())!.AsObject();
        cloneContent["parameters"]!["explanationThreshold"] = 3;
        cloneContent["generalGroups"]![0]!["items"]![0]!["text"] = "Nội dung đã sửa ở bản mới";
        var cloneSaved = await Data(await w.Manager.PutAsJsonAsync($"/api/criteria-sets/{cloneId}", new { version = clone.GetProperty("version").GetUInt32(), content = cloneContent }));
        var clonePublished = await Data(await w.Manager.PostAsJsonAsync($"/api/criteria-sets/{cloneId}/publish", new { version = cloneSaved.GetProperty("version").GetUInt32() }));
        var archived = await Data(await w.Manager.PostAsJsonAsync($"/api/criteria-sets/{setId}/archive", new { version = publishedVersion }));
        Assert.Equal("Archived", archived.GetProperty("status").GetString());
        Assert.Contains(archived.GetProperty("usedByPeriods").EnumerateArray(), n => n.GetString() == period.GetProperty("name").GetString());
        // Dọn: bộ nhân bản không được là "bộ 09B mới nhất" cho test khác.
        await Data(await w.Manager.PostAsJsonAsync($"/api/criteria-sets/{cloneId}/archive", new { version = clonePublished.GetProperty("version").GetUInt32() }));

        var opened = await Data(w.Manager, $"/api/evaluations/periods/{periodId}");
        var snapshot = opened.GetProperty("criteria");
        Assert.Equal(code, snapshot.GetProperty("code").GetString());
        Assert.Equal(5, snapshot.GetProperty("content").GetProperty("parameters").GetProperty("explanationThreshold").GetDouble());
        Assert.NotEqual("Nội dung đã sửa ở bản mới", snapshot.GetProperty("content").GetProperty("generalGroups")[0].GetProperty("items")[0].GetProperty("text").GetString());
        // Bộ đã lưu trữ không chọn được cho kỳ mới.
        var archivedChoice = await PostPeriodAsync(w, "q3-2026-transition", setId);
        Assert.Equal(HttpStatusCode.BadRequest, archivedChoice.StatusCode);
        Assert.Contains("đã xuất bản", await MessageAsync(archivedChoice));

        // 4. Tự chấm theo 17 tiêu chí + K/AD: thiếu lý do → 400; có lý do → điểm quy đổi 26/28 × 30 = 27,9.
        var general = CriteriaTestData.General("1.2");
        general["1.1"] = new GeneralItemScore { NotApplicable = true };
        var body = new { generalScores = general, axisScores = CriteriaTestData.Axis(13, 9, 9, 13, 9, 9) };
        var noReason = await PostAsync(w.Owner1C, recordId, "self-score/submit", body, await VersionAsync(w, recordId));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Contains("K/AD", await MessageAsync(noReason));
        general["1.1"].Reason = "Không thuộc phạm vi chức trách";
        var scored = await StepAsync(w, w.Owner1C, recordId, "self-score/submit", body, "AwaitingCellConfirm");
        Assert.Equal(27.9, scored.GetProperty("generalCriteriaScore").GetDouble(), 6);
        Assert.Equal(62.0, scored.GetProperty("tasksScore").GetDouble(), 6);
        Assert.Equal(89.9, scored.GetProperty("totalSelfScore").GetDouble(), 6);
        Assert.Equal("HoanThanhTot", scored.GetProperty("selfProposedGrade").GetString());
        Assert.True(scored.GetProperty("generalScores").GetProperty("1.1").GetProperty("notApplicable").GetBoolean());
        Assert.Equal(13.0, scored.GetProperty("axisScores").GetProperty("T1").GetDouble());
        Assert.Equal("09B", scored.GetProperty("selfScoreForm").GetString());

        await StepAsync(w, w.CellSecC, recordId, "cell/confirm", new { comment = "Xác nhận" }, "AwaitingCollective");
        await StepAsync(w, w.SecretaryC, recordId, "collective", new { proposedGrade = "HoanThanhTot" }, "AwaitingAppraisal");

        // 5. Thẩm định chênh 5,9 điểm (≥ ngưỡng 5 của bộ trong ảnh chụp) → bắt buộc giải trình.
        var noExplanation = await PostAsync(w.AppraiserC, recordId, "appraisal", new { appraisalScore = 84.0, proposedGrade = "HoanThanhTot" }, await VersionAsync(w, recordId));
        Assert.Equal(HttpStatusCode.BadRequest, noExplanation.StatusCode);
        Assert.Contains("giải trình", await MessageAsync(noExplanation));
        var appraised = await StepAsync(w, w.AppraiserC, recordId, "appraisal",
            new { appraisalScore = 84.0, proposedGrade = "HoanThanhTot", comment = "Đạt", explanation = "Trừ điểm trục 1 do chậm tiến độ dự án" }, "AwaitingDirectorReview");
        Assert.Equal("Trừ điểm trục 1 do chậm tiến độ dự án", appraised.GetProperty("appraisalExplanation").GetString());

        await StepAsync(w, w.DirectorC, recordId, "director-review", new { proposedGrade = "HoanThanhTot", comment = "Đồng ý" }, "AwaitingDecision");
        await StepAsync(w, w.OfficeC, recordId, "decision", new { finalGrade = "HoanThanhTot" }, "AwaitingPublish");
        await StepAsync(w, w.OfficeC, recordId, "publish", new { }, "Published");

        // 6. Mẫu 10 xuất được từ hồ sơ đã công bố.
        await AssertDocxAsync(w.Manager, $"/api/reports/docx/mau-10/{recordId}", "84");
    }

    [SkippableFact]
    public async Task C2_Form09A_WeightFrameReadiness_TaskAxisCodes_AbcdByFrame_ExportsMau01Mau02()
    {
        var w = await WorldAsync();
        var period = await CreatePeriodAsync(w, "full", null);
        var periodId = period.GetProperty("id").GetGuid();
        Assert.Equal("09A", period.GetProperty("criteria").GetProperty("selfScoreForm").GetString());

        // Cán bộ có khung "K9" (không có trong bộ) → kẹt luồng báo lỗi; sửa ảnh chụp sang khung không có → 400; sang K1 → hết lỗi.
        await _factory.WithDbAsync(async db =>
        {
            var member = await db.PartyMemberProfiles.SingleAsync(m => m.Id == w.Owner2.Id);
            member.WeightFrameCode = "K9";
            await db.SaveChangesAsync();
        });
        var r1 = await AddParticipantAsync(w, periodId, w.Owner1.Id);
        var r2 = await AddParticipantAsync(w, periodId, w.Owner2.Id);
        var participants = await Data(w.Manager, $"/api/evaluations/periods/{periodId}/participants");
        Assert.Equal("K2", Participant(participants, r1).GetProperty("weightFrameCode").GetString()); // chưa có khung → khung mặc định của bộ
        Assert.Equal("K9", Participant(participants, r2).GetProperty("weightFrameCode").GetString());

        var readiness = await Data(w.Manager, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.False(readiness.GetProperty("ready").GetBoolean());
        var frameIssue = Assert.Single(readiness.GetProperty("issues").EnumerateArray(), i => i.GetProperty("message").GetString()!.Contains("Khung tỷ trọng"));
        Assert.Equal(r2, frameIssue.GetProperty("recordId").GetGuid());
        Assert.Contains("\"K9\"", frameIssue.GetProperty("message").GetString());
        var openRefused = await w.Manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = (await Data(w.Manager, $"/api/evaluations/periods/{periodId}")).GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.Conflict, openRefused.StatusCode);

        var p2 = Participant(participants, r2);
        var badFrame = await w.Manager.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{r2}/snapshot",
            new { version = p2.GetProperty("version").GetUInt32(), weightFrameCode = "K8", reason = "Sửa khung" });
        Assert.Equal(HttpStatusCode.BadRequest, badFrame.StatusCode);
        var fixedFrame = await Data(await w.Manager.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{r2}/snapshot",
            new { version = p2.GetProperty("version").GetUInt32(), weightFrameCode = "k1", reason = "Sửa khung theo bộ tiêu chí" }));
        Assert.Equal("K1", fixedFrame.GetProperty("weightFrameCode").GetString());
        Assert.Equal("Khung 1 - Quản lý, tham mưu, công tác Đảng, Đoàn thể", fixedFrame.GetProperty("weightFrameName").GetString());
        readiness = await Data(w.Manager, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.DoesNotContain(readiness.GetProperty("issues").EnumerateArray(), i => i.GetProperty("message").GetString()!.Contains("Khung tỷ trọng"));
        await OpenAsync(w, periodId);

        // Đăng ký: mã trục phải thuộc bộ của kỳ.
        var badAxis = await PostAsync(w.Owner1C, r1, "tasks/submit", Tasks("T7"), await VersionAsync(w, r1));
        Assert.Equal(HttpStatusCode.BadRequest, badAxis.StatusCode);
        Assert.Contains("\"T7\"", await MessageAsync(badAxis));
        var registered = await StepAsync(w, w.Owner1C, r1, "tasks/submit", Tasks("t1"), "AwaitingTaskApproval");
        Assert.All(registered.GetProperty("tasks").EnumerateArray(), t => Assert.Equal("T1", t.GetProperty("axisCode").GetString()));
        await StepAsync(w, w.DeptLeadC, r1, "tasks/approve", new { comment = "Đồng ý" }, "AwaitingSelfScore");

        // Tự chấm A-B-C-D theo khung K2 của hồ sơ, làm tròn 1 chữ số (bộ mặc định): TC-1 dòng 1, 2, 4.
        var record = await Data(w.Owner1C, $"/api/evaluations/records/{r1}");
        var tasks = record.GetProperty("tasks").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()).ToList();
        var taskScores = new object[]
        {
            new { taskId = tasks[0], criteriaA_Ratio = 1.0, criteriaB_Ratio = 0.95, criteriaC_Ratio = 1.0, criteriaD_Ratio = 1.0, isExceedStandard = true },
            new { taskId = tasks[1], criteriaA_Ratio = 1.0, criteriaB_Ratio = 0.90, criteriaC_Ratio = 1.0, criteriaD_Ratio = 0.95, isExceedStandard = false },
            new { taskId = tasks[2], criteriaA_Ratio = 1.0, criteriaB_Ratio = 0.95, criteriaC_Ratio = 1.0, criteriaD_Ratio = 0.90, isExceedStandard = false }
        };
        var scored = await StepAsync(w, w.Owner1C, r1, "self-score/submit", new { generalScores = CriteriaTestData.General(), taskScores }, "AwaitingCellConfirm");
        var selfScores = scored.GetProperty("tasks").EnumerateArray().OrderBy(t => t.GetProperty("taskOrder").GetInt32())
            .Select(t => t.GetProperty("selfScore").GetDouble()).ToList();
        Assert.Equal(new[] { 29.3, 23.5, 14.3 }, selfScores); // 30×97,5% = 29,25→29,3; 25×94% = 23,5; 15×95,5% = 14,325→14,3
        Assert.Equal(67.1, scored.GetProperty("tasksScore").GetDouble(), 6);
        Assert.Equal(97.1, scored.GetProperty("totalSelfScore").GetDouble(), 6);
        Assert.Equal("HoanThanhXuatSac", scored.GetProperty("selfProposedGrade").GetString()); // 1/3 vượt chuẩn ≥ 30%

        // Mẫu 01 ghi mã trục, Mẫu 02 ghi điểm theo số chữ số thập phân của bộ.
        await AssertDocxAsync(w.Manager, $"/api/reports/docx/mau-01/{r1}", "T1");
        await AssertDocxAsync(w.Manager, $"/api/reports/docx/mau-02/{r1}", "29,3");
    }

    [SkippableFact]
    public async Task C3_Permissions_AndWeightFrameOptions()
    {
        var w = await WorldAsync();

        // Người quản lý kỳ (không có criteria.manage): xem được danh sách để chọn bộ, không tạo/sửa được.
        var periodOnlyUser = await _factory.CreateUserWithPermissionsAsync("period.manage");
        var periodOnly = await _factory.LoginAsAsync(periodOnlyUser.Username, periodOnlyUser.Password, distinctClientIp: true);
        Assert.Equal(HttpStatusCode.OK, (await periodOnly.GetAsync("/api/criteria-sets")).StatusCode);
        var denied = await periodOnly.PostAsJsonAsync("/api/criteria-sets", new { code = "IT-X", name = "X", selfScoreForm = "09A" });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("Quản lý bộ tiêu chí", await MessageAsync(denied));

        // Người dùng thường: không xem danh sách bộ; xem được danh mục khung tỷ trọng (cho hồ sơ cán bộ).
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Owner1C.GetAsync("/api/criteria-sets")).StatusCode);
        var frames = await Data(w.Owner1C, "/api/criteria-sets/weight-frames");
        Assert.Equal(new[] { "K1", "K2", "K3", "K4" }, frames.GetProperty("frames").EnumerateArray().Select(f => f.GetProperty("code").GetString()));

        // Bản nháp xóa được; bản nháp không chọn được cho kỳ.
        var draft = await Data(await w.Manager.PostAsJsonAsync("/api/criteria-sets", new { code = $"IT-DEL-{Guid.NewGuid().ToString("N")[..6]}", name = "Nháp xóa", selfScoreForm = "09A" }));
        var draftId = draft.GetProperty("id").GetGuid();
        var draftChoice = await PostPeriodAsync(w, "full", draftId);
        Assert.Equal(HttpStatusCode.BadRequest, draftChoice.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await w.Manager.DeleteAsync($"/api/criteria-sets/{draftId}?version={draft.GetProperty("version").GetUInt32()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.Manager.GetAsync($"/api/criteria-sets/{draftId}")).StatusCode);

        // Vai trò mặc định "Cơ quan thẩm định" có quyền criteria.manage (seed).
        await _factory.WithDbAsync(async db =>
        {
            var role = await db.Roles.Include(r => r.Permissions).SingleAsync(r => r.Code == "CO_QUAN_THAM_DINH");
            Assert.Contains(role.Permissions, p => p.Code == "criteria.manage");
            Assert.Equal(2, await db.Set<CriteriaSet>().CountAsync(s => s.Code == CriteriaSetDefaults.Code09A || s.Code == CriteriaSetDefaults.Code09B));
        });
    }

    #region Thế giới test và hỗ trợ

    private sealed class World
    {
        public required TestUser Owner1 { get; init; }
        public required TestUser Owner2 { get; init; }
        public required HttpClient Manager { get; init; }
        public required HttpClient Owner1C { get; init; }
        public required HttpClient AppraiserC { get; init; }
        public required HttpClient CellSecC { get; init; }
        public required HttpClient SecretaryC { get; init; }
        public required HttpClient DirectorC { get; init; }
        public required HttpClient OfficeC { get; init; }
        public required HttpClient DeptLeadC { get; init; }

        public static async Task<World> CreateAsync(ApiFactory f)
        {
            async Task<HttpClient> WithRole(string roleCode)
            {
                var user = await f.CreateUserAsync();
                await f.AssignAsync(user.Id, await f.GetRoleIdAsync(roleCode), RoleScopeType.Global, null);
                return await f.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            }

            var evaluatee = await f.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA");
            var owner1 = await f.CreateUserAsync(fullName: "IT Tiêu chí 1");
            var owner2 = await f.CreateUserAsync(fullName: "IT Tiêu chí 2");
            await f.AssignAsync(owner1.Id, evaluatee, RoleScopeType.Global, null);
            await f.AssignAsync(owner2.Id, evaluatee, RoleScopeType.Global, null);

            return new World
            {
                Owner1 = owner1,
                Owner2 = owner2,
                // Cơ quan thẩm định: quản lý kỳ + bộ tiêu chí + thẩm định + xuất báo cáo (vai trò mặc định).
                Manager = await WithRole("CO_QUAN_THAM_DINH"),
                Owner1C = await f.LoginAsAsync(owner1.Username, owner1.Password, distinctClientIp: true),
                AppraiserC = await WithRole("CO_QUAN_THAM_DINH"),
                CellSecC = await WithRole("CHI_UY_CHI_BO"),
                SecretaryC = await WithRole("THU_KY_TAP_THE"),
                DirectorC = await WithRole("CAP_TRUC_TIEP_SU_DUNG"),
                OfficeC = await WithRole("VAN_PHONG_DANG_UY"),
                DeptLeadC = await WithRole("LANH_DAO_PHONG")
            };
        }
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

    private static object Tasks(string axisCode) => new
    {
        tasks = new[]
        {
            new { taskName = "Sản phẩm trọng tâm", targetOutput = "Kết quả 1", weight = 30.0, axisCode },
            new { taskName = "Sản phẩm 2", targetOutput = "Kết quả 2", weight = 25.0, axisCode },
            new { taskName = "Sản phẩm 3", targetOutput = "Kết quả 3", weight = 15.0, axisCode }
        }
    };

    private static Task<HttpResponseMessage> PostPeriodAsync(World w, string preset, Guid? criteriaSetId) =>
        w.Manager.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = 2100 - Random.Shared.Next(1, 90), quarter = Random.Shared.Next(1, 5), preset, criteriaSetId,
            name = $"Kỳ tiêu chí {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        });

    private static async Task<JsonElement> CreatePeriodAsync(World w, string preset, Guid? criteriaSetId) =>
        await Data(await PostPeriodAsync(w, preset, criteriaSetId));

    private static async Task<Guid> AddParticipantAsync(World w, Guid periodId, Guid memberId)
    {
        await Data(await w.Manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { memberId } }));
        return Participant(await Data(w.Manager, $"/api/evaluations/periods/{periodId}/participants"), null, memberId).GetProperty("recordId").GetGuid();
    }

    private static JsonElement Participant(JsonElement list, Guid? recordId, Guid? memberId = null) =>
        list.EnumerateArray().First(p => recordId.HasValue
            ? p.GetProperty("recordId").GetGuid() == recordId.Value
            : p.GetProperty("memberId").GetGuid() == memberId!.Value);

    /// <summary>Mở kỳ (mọi vai trò thực hiện gán Toàn công ty nên không kẹt luồng — không cần mở bắt buộc).</summary>
    private static async Task OpenAsync(World w, Guid periodId)
    {
        var period = await Data(w.Manager, $"/api/evaluations/periods/{periodId}");
        var opened = await Data(await w.Manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = period.GetProperty("version").GetUInt32() }));
        Assert.Equal("Open", opened.GetProperty("status").GetString());
    }

    private static async Task<uint> VersionAsync(World w, Guid recordId) =>
        (await Data(w.Manager, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body, uint version)
    {
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["version"] = version;
        return client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    private static async Task<JsonElement> StepAsync(World w, HttpClient client, Guid recordId, string action, object body, string expectedStatus)
    {
        var data = await Data(await PostAsync(client, recordId, action, body, await VersionAsync(w, recordId)));
        Assert.Equal(expectedStatus, data.GetProperty("status").GetString());
        return data;
    }

    /// <summary>Tải biểu mẫu Word và kiểm tra nội dung văn bản có chuỗi cho trước.</summary>
    private static async Task AssertDocxAsync(HttpClient client, string url, string expectedText)
    {
        var response = await client.GetAsync(url);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {url}: {System.Text.Encoding.UTF8.GetString(bytes)}");
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var text = System.Text.RegularExpressions.Regex.Replace(await reader.ReadToEndAsync(), "<[^>]+>", string.Empty);
        Assert.Contains(expectedText, text);
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

    #endregion
}
