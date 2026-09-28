using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 09: ma trận endpoint × vai trò mặc định × phạm vi trên PostgreSQL thật — kiểm tra 200/403 và
/// <b>dữ liệu trả về đúng phạm vi</b>. T-45 (biên bản), T-48 (thẩm định từng hồ sơ), T-61 (danh sách cán bộ) có test riêng.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class AuthorizationMatrixTests
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly Dictionary<string, Scenario> Scenarios = new();

    private readonly ApiFactory _factory;

    public AuthorizationMatrixTests(ApiFactory factory) => _factory = factory;

    private async Task<Scenario> GetScenarioAsync()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        await Gate.WaitAsync();
        try
        {
            if (!Scenarios.TryGetValue(_factory.DatabaseName, out var scenario))
            {
                scenario = await Scenario.CreateAsync(_factory);
                Scenarios[_factory.DatabaseName] = scenario;
            }
            return scenario;
        }
        finally
        {
            Gate.Release();
        }
    }

    #region Hồ sơ đánh giá — đọc

    [SkippableFact]
    public async Task Periods_ReadableByEveryLoggedInUser_ManageNeedsPeriodManage()
    {
        var s = await GetScenarioAsync();

        Assert.Equal(HttpStatusCode.OK, (await s.Plain.GetAsync("/api/evaluations/periods")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerA1.GetAsync("/api/evaluations/periods/active")).StatusCode);

        var body = new { year = 2031, quarter = 1, name = "Kỳ test " + Guid.NewGuid().ToString("N")[..6], startDate = DateTime.UtcNow, endDate = DateTime.UtcNow.AddMonths(3) };
        Assert.Equal(HttpStatusCode.OK, (await s.Appraiser.PostAsJsonAsync("/api/evaluations/periods", body)).StatusCode);
        foreach (var client in new[] { s.Office, s.Admin, s.OwnerA1, s.Plain })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/evaluations/periods", body)).StatusCode);
        // Chuyển trạng thái kỳ qua endpoint theo hành động.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await s.CellSecA.PostAsJsonAsync($"/api/evaluations/periods/{s.PeriodId}/lock", new { version = 0 })).StatusCode);
    }

    [SkippableFact]
    public async Task RecordById_And_History_FollowScope_OwnerAlways_AdminNever()
    {
        var s = await GetScenarioAsync();
        var cases = new (HttpClient Client, Guid Record, HttpStatusCode Expected, string Why)[]
        {
            (s.OwnerA1, s.RecordA1, HttpStatusCode.OK, "chủ hồ sơ"),
            (s.OwnerA1, s.RecordB1, HttpStatusCode.Forbidden, "hồ sơ người khác, không có evaluation.read"),
            (s.CellSecA, s.RecordA1, HttpStatusCode.OK, "Chi ủy phạm vi Chi bộ A"),
            (s.CellSecA, s.RecordB1, HttpStatusCode.Forbidden, "ngoài Chi bộ A"),
            (s.DeptLeadA, s.RecordA2, HttpStatusCode.OK, "Lãnh đạo Phòng A"),
            (s.DeptLeadA, s.RecordB1, HttpStatusCode.Forbidden, "ngoài Phòng A"),
            (s.Appraiser, s.RecordB1, HttpStatusCode.OK, "thẩm định Toàn công ty"),
            (s.Committee, s.RecordA1, HttpStatusCode.OK, "cấp ủy viên Toàn công ty"),
            (s.Admin, s.RecordA1, HttpStatusCode.Forbidden, "quản trị kỹ thuật không xem nội dung đánh giá"),
            (s.Plain, s.RecordA1, HttpStatusCode.Forbidden, "không có bản gán")
        };

        foreach (var (client, record, expected, why) in cases)
        {
            Assert.True(expected == (await client.GetAsync($"/api/evaluations/records/{record}")).StatusCode, why);
            Assert.True(expected == (await client.GetAsync($"/api/evaluations/records/{record}/history")).StatusCode, why + " (lịch sử)");
        }
    }

    [SkippableFact]
    public async Task RecordLists_ReturnOnlyRecordsInScope()
    {
        var s = await GetScenarioAsync();

        Assert.Equal(new[] { s.RecordA1, s.RecordA2, s.RecordS }.OrderBy(x => x),
            (await Ids(s.CellSecA, $"/api/evaluations/records?periodId={s.PeriodId}")).OrderBy(x => x));
        Assert.Equal(new[] { s.RecordA1, s.RecordA2 }.OrderBy(x => x),
            (await Ids(s.DeptLeadA, $"/api/evaluations/records?periodId={s.PeriodId}")).OrderBy(x => x));
        Assert.Equal(s.AllRecords.OrderBy(x => x), (await Ids(s.Appraiser, $"/api/evaluations/records?periodId={s.PeriodId}")).OrderBy(x => x));

        // Chọn Chi bộ ngoài phạm vi → không lộ hồ sơ nào.
        Assert.Empty(await Ids(s.CellSecA, $"/api/evaluations/branch-records?periodId={s.PeriodId}&branchId={s.CellB}"));
        Assert.Equal(new[] { s.RecordB1, s.RecordT }.OrderBy(x => x),
            (await Ids(s.Appraiser, $"/api/evaluations/branch-records?periodId={s.PeriodId}&branchId={s.CellB}")).OrderBy(x => x));

        // Không có evaluation.read → 403 ở danh sách, nhưng vẫn xem được hồ sơ của mình.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.GetAsync($"/api/evaluations/records?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Admin.GetAsync($"/api/evaluations/branch-records?periodId={s.PeriodId}")).StatusCode);
        var mine = await Data(s.OwnerA1, $"/api/evaluations/my-record?periodId={s.PeriodId}");
        Assert.Equal(s.RecordA1, mine.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.OK, (await s.Appraiser.GetAsync($"/api/evaluations/branch-quotas?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.GetAsync($"/api/evaluations/branch-quotas?periodId={s.PeriodId}")).StatusCode);
    }

    #endregion

    #region Hồ sơ đánh giá — ghi theo từng bước

    // Bước ghi đi qua endpoint theo hành động (records/{id}/...) và máy trạng thái; kiểm tra quyền (guard) chạy trước
    // kiểm tra trạng thái nên hồ sơ ngoài phạm vi luôn 403. Hồ sơ dùng chung giữa các test → mỗi test tự đặt trạng thái cần có.

    [SkippableFact]
    public async Task SelfSteps_OnlyOwnerWithEvaluationSelf()
    {
        var s = await GetScenarioAsync();
        await SetStatusAsync(s.RecordB1, RecordStatus.AwaitingRegistration);
        var register = new
        {
            version = await VersionAsync(s, s.RecordB1),
            tasks = new[]
            {
                new { taskName = "Nhiệm vụ 1", targetOutput = "Kết quả 1", weight = 30.0 },
                new { taskName = "Nhiệm vụ 2", targetOutput = "Kết quả 2", weight = 20.0 },
                new { taskName = "Nhiệm vụ 3", targetOutput = "Kết quả 3", weight = 20.0 }
            }
        };

        var url = $"/api/evaluations/records/{s.RecordB1}/tasks/submit";
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Plain.PostAsJsonAsync(url, register)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Admin.PostAsJsonAsync(url, register)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerB1.PostAsJsonAsync(url, register)).StatusCode);

        await SetStatusAsync(s.RecordA1, RecordStatus.AwaitingSelfScore, withTasks: true);
        var selfScoreUrl = $"/api/evaluations/records/{s.RecordA1}/self-score/submit";
        var selfScore = new { version = await VersionAsync(s, s.RecordA1), generalScores = new[] { 4.0, 4.0, 4.0, 4.0, 4.0, 4.0 }, selfProposedGrade = "HoanThanhTot" };
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerB1.PostAsJsonAsync(selfScoreUrl, selfScore)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerA1.PostAsJsonAsync(selfScoreUrl, selfScore)).StatusCode);
    }

    [SkippableFact]
    public async Task CellConfirm_PerRecordScope_AndConflictOfInterest()
    {
        var s = await GetScenarioAsync();
        foreach (var record in new[] { s.RecordA1, s.RecordA2, s.RecordB1, s.RecordS })
            await SetStatusAsync(record, RecordStatus.AwaitingCellConfirm);
        async Task<object> Confirm(Guid record) => new { version = await VersionAsync(s, record), comment = "Xác nhận" };
        string Url(Guid record) => $"/api/evaluations/records/{record}/cell/confirm";

        Assert.Equal(HttpStatusCode.OK, (await s.CellSecA.PostAsJsonAsync(Url(s.RecordA1), await Confirm(s.RecordA1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellSecA.PostAsJsonAsync(Url(s.RecordB1), await Confirm(s.RecordB1))).StatusCode);
        var own = await s.CellSecA.PostAsJsonAsync(Url(s.RecordS), await Confirm(s.RecordS));
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);
        Assert.Contains("xung đột lợi ích", await Message(own), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.PostAsJsonAsync(Url(s.RecordA1), await Confirm(s.RecordA1))).StatusCode);

        // Trả lại: bắt buộc lý do (400), có lý do → về bước tự chấm.
        var returnUrl = $"/api/evaluations/records/{s.RecordA2}/cell/return";
        Assert.Equal(HttpStatusCode.BadRequest,
            (await s.CellSecA.PostAsJsonAsync(returnUrl, new { version = await VersionAsync(s, s.RecordA2), reason = " " })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await s.CellSecA.PostAsJsonAsync(returnUrl, new { version = await VersionAsync(s, s.RecordA2), reason = "Thiếu minh chứng" })).StatusCode);
    }

    [SkippableFact]
    public async Task T48_Appraisal_ChecksEachRecord()
    {
        var s = await GetScenarioAsync();
        foreach (var record in new[] { s.RecordA1, s.RecordB1, s.RecordT })
            await SetStatusAsync(record, RecordStatus.AwaitingAppraisal);
        async Task<object> Appraise(Guid record) => new { version = await VersionAsync(s, record), appraisalScore = 90.0, comment = "Thẩm định", proposedGrade = "HoanThanhTot" };
        string Url(Guid record) => $"/api/evaluations/records/{record}/appraisal";

        Assert.Equal(HttpStatusCode.OK, (await s.Appraiser.PostAsJsonAsync(Url(s.RecordB1), await Appraise(s.RecordB1))).StatusCode);
        // Có evaluation.appraise nhưng là hồ sơ của chính mình → 403 (T-48).
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Appraiser.PostAsJsonAsync(Url(s.RecordT), await Appraise(s.RecordT))).StatusCode);
        // Thẩm định theo phạm vi Phòng A không thẩm định được hồ sơ Phòng B.
        Assert.Equal(HttpStatusCode.OK, (await s.DeptAppraiserA.PostAsJsonAsync(Url(s.RecordA1), await Appraise(s.RecordA1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.DeptAppraiserA.PostAsJsonAsync(Url(s.RecordB1), await Appraise(s.RecordB1))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellSecA.PostAsJsonAsync(Url(s.RecordA1), await Appraise(s.RecordA1))).StatusCode);
    }

    [SkippableFact]
    public async Task ApproveFinal_FollowsApprovalAuthority()
    {
        var s = await GetScenarioAsync();
        foreach (var record in new[] { s.RecordA1, s.RecordA2, s.RecordB1 })
            await SetStatusAsync(record, RecordStatus.AwaitingDecision);
        async Task<object> Decide(Guid record) => new { version = await VersionAsync(s, record), finalScore = 90.0, finalGrade = "HoanThanhTot" };
        string Url(Guid record) => $"/api/evaluations/records/{record}/decision";

        Assert.Equal(HttpStatusCode.OK, (await s.Office.PostAsJsonAsync(Url(s.RecordA1), await Decide(s.RecordA1))).StatusCode);   // CoSo
        Assert.Equal(HttpStatusCode.OK, (await s.Office.PostAsJsonAsync(Url(s.RecordA2), await Decide(s.RecordA2))).StatusCode);   // CapTren
        Assert.Equal(HttpStatusCode.OK, (await s.LocalDecider.PostAsJsonAsync(Url(s.RecordB1), await Decide(s.RecordB1))).StatusCode);
        await SetStatusAsync(s.RecordA2, RecordStatus.AwaitingDecision);
        var external = await s.LocalDecider.PostAsJsonAsync(Url(s.RecordA2), await Decide(s.RecordA2));
        Assert.Equal(HttpStatusCode.Forbidden, external.StatusCode);
        Assert.Contains("cấp trên", await Message(external), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Appraiser.PostAsJsonAsync(Url(s.RecordA1), await Decide(s.RecordA1))).StatusCode);
    }

    /// <summary>Đặt trạng thái hồ sơ trực tiếp trong CSDL (dựng dữ liệu test); <paramref name="withTasks"/> tạo danh mục 3 sản phẩm.</summary>
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
                    db.EvaluationTasks.Add(new EvaluationTask { RecordId = recordId, TaskOrder = order, TaskName = $"Nhiệm vụ {order++}", Weight = weight, SelfScore = weight });
            }
            await db.SaveChangesAsync();
        });
    }

    /// <summary>Phiên bản hiện tại của hồ sơ (đọc bằng tài khoản thẩm định phạm vi Toàn công ty).</summary>
    private static async Task<uint> VersionAsync(Scenario s, Guid recordId) =>
        (await Data(s.Appraiser, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();

    #endregion

    #region Tập thể, biên bản

    [SkippableFact]
    public async Task CollectiveRecords_FilteredByCellOrDepartmentScope()
    {
        var s = await GetScenarioAsync();

        Assert.Equal(new[] { s.CollectiveA }, await Ids(s.CellSecA, $"/api/evaluations/collective-records?periodId={s.PeriodId}"));
        Assert.Equal(new[] { s.CollectiveB }, await Ids(s.DeptLeadB, $"/api/evaluations/collective-records?periodId={s.PeriodId}"));
        Assert.Equal(new[] { s.CollectiveA, s.CollectiveB }.OrderBy(x => x),
            (await Ids(s.Committee, $"/api/evaluations/collective-records?periodId={s.PeriodId}")).OrderBy(x => x));
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.GetAsync($"/api/evaluations/collective-records?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellSecA.GetAsync($"/api/evaluations/collective-records/{s.CollectiveB}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.CellSecA.GetAsync($"/api/evaluations/collective-records/{s.CollectiveA}")).StatusCode);

        object Collective(Guid cell) => new { periodId = s.PeriodId, form = "M07", partyCellId = cell, subjectName = "Chi bộ", generalCriteriaScore = 25.0, taskCriteriaScore = 60.0 };
        Assert.Equal(HttpStatusCode.OK, (await s.CellSecA.PostAsJsonAsync("/api/evaluations/collective-records", Collective(s.CellA))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellSecA.PostAsJsonAsync("/api/evaluations/collective-records", Collective(s.CellB))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Appraiser.PostAsJsonAsync("/api/evaluations/collective-records", Collective(s.CellA))).StatusCode);
    }

    [SkippableFact]
    public async Task T45_Meetings_OnlyWithinMeetingScope()
    {
        var s = await GetScenarioAsync();

        // Chi ủy (meeting.read phạm vi Chi bộ A) chỉ thấy biên bản Chi bộ A (có thể có thêm biên bản kiểm phiếu do test khác tạo ở Chi bộ A).
        var cellSecMeetings = await Meetings(s.CellSecA, $"/api/evaluations/meetings?periodId={s.PeriodId}");
        Assert.Contains(s.MeetingA, cellSecMeetings.Keys);
        Assert.All(cellSecMeetings.Values, cell => Assert.Equal(s.CellA, cell));
        // Lọc Chi bộ khác qua tham số → không lộ biên bản.
        Assert.Empty(await Ids(s.CellSecA, $"/api/evaluations/meetings?periodId={s.PeriodId}&partyCellId={s.CellB}"));
        var committeeMeetings = await Meetings(s.Committee, $"/api/evaluations/meetings?periodId={s.PeriodId}");
        Assert.Contains(s.MeetingA, committeeMeetings.Keys);
        Assert.Contains(s.MeetingB, committeeMeetings.Keys);
        // Có meeting.manage/meeting.read nhưng chỉ phạm vi Phòng (không khớp Chi bộ nào) → không thấy biên bản nào (T-45).
        Assert.Empty(await Ids(s.DeptSecretaryA, $"/api/evaluations/meetings?periodId={s.PeriodId}"));
        Assert.Equal(HttpStatusCode.Forbidden, (await s.DeptSecretaryA.GetAsync($"/api/evaluations/meetings/{s.MeetingA}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.GetAsync($"/api/evaluations/meetings?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellSecA.GetAsync($"/api/evaluations/meetings/{s.MeetingB}")).StatusCode);

        object Meeting(Guid cell) => new { periodId = s.PeriodId, partyCellId = cell, formCode = "M12", meetingType = "Hội nghị", invitedCount = 5, presentCount = 5 };
        Assert.Equal(HttpStatusCode.OK, (await s.Office.PostAsJsonAsync("/api/evaluations/meetings", Meeting(s.CellB))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellSecA.PostAsJsonAsync("/api/evaluations/meetings", Meeting(s.CellA))).StatusCode);
    }

    #endregion

    #region Tệp đính kèm

    [SkippableFact]
    public async Task Attachments_FollowRecordPermissions_GeneralDocsPublic()
    {
        var s = await GetScenarioAsync();

        Assert.Equal(HttpStatusCode.OK, (await Upload(s.OwnerA1, "MAU02", "EvaluationRecord", s.RecordA1)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(s.OwnerB1, "MAU02", "EvaluationRecord", s.RecordA1)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(s.CellSecA, "MAU02", "EvaluationRecord", s.RecordA1)).StatusCode);

        var byOwner = $"/api/attachments?ownerType=EvaluationRecord&ownerId={s.RecordA1}";
        Assert.Single(await Ids(s.OwnerA1, byOwner));
        Assert.Single(await Ids(s.CellSecA, byOwner));
        Assert.Empty(await Ids(s.OwnerB1, byOwner));
        Assert.Empty(await Ids(s.Admin, byOwner));

        // Văn bản chung: quản trị tải lên → mọi người xem; cán bộ tải "GENERAL" → chỉ mình thấy.
        var general = await Upload(s.Admin, "GENERAL", null, null);
        Assert.Equal(HttpStatusCode.OK, general.StatusCode);
        var generalId = (await ReadData(general)).GetProperty("id").GetGuid();
        Assert.Contains(generalId, await Ids(s.Plain, "/api/attachments/list"));
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.DeleteAsync($"/api/attachments/{generalId}")).StatusCode);

        var privateGeneral = await Upload(s.OwnerA1, "GENERAL", null, null);
        Assert.Equal(HttpStatusCode.OK, privateGeneral.StatusCode);
        var privateId = (await ReadData(privateGeneral)).GetProperty("id").GetGuid();
        Assert.DoesNotContain(privateId, await Ids(s.Plain, "/api/attachments/list"));
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(s.Plain, "MAU02", null, null)).StatusCode);
    }

    #endregion

    #region Báo cáo

    [SkippableFact]
    public async Task T61_CadreExport_FilteredByReportScope()
    {
        var s = await GetScenarioAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.GetAsync("/api/reports/cadres")).StatusCode);

        var scoped = await CadreNames(s.CellReporterA);
        Assert.Contains(s.NameA1, scoped);
        Assert.DoesNotContain(s.NameB1, scoped);

        var all = await CadreNames(s.Committee);
        Assert.Contains(s.NameA1, all);
        Assert.Contains(s.NameB1, all);
    }

    [SkippableFact]
    public async Task BranchReports_ResolveScope()
    {
        var s = await GetScenarioAsync();

        Assert.Equal(HttpStatusCode.OK, (await s.CellReporterA.GetAsync($"/api/reports/form-14?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellReporterA.GetAsync($"/api/reports/form-14?periodId={s.PeriodId}&branchId={s.CellB}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Committee.GetAsync($"/api/reports/form-15?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.GetAsync($"/api/reports/form-14?periodId={s.PeriodId}")).StatusCode);
        // Mẫu 13 theo Chi bộ: meeting.read (Chi ủy) trong phạm vi Chi bộ.
        Assert.Equal(HttpStatusCode.OK, (await s.CellSecA.GetAsync($"/api/reports/docx/mau-13?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellSecA.GetAsync($"/api/reports/docx/mau-13?periodId={s.PeriodId}&branchId={s.CellB}")).StatusCode);
        // Biểu mẫu hồ sơ cá nhân: quyền xem hồ sơ.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.OwnerA1.GetAsync($"/api/reports/docx/mau-01/{s.RecordB1}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.OwnerB1.GetAsync($"/api/reports/docx/mau-01/{s.RecordB1}")).StatusCode);
    }

    #endregion

    #region Hỗ trợ

    private static async Task<JsonElement> ReadData(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await ReadData(await client.GetAsync(url));

    private static async Task<List<Guid>> Ids(HttpClient client, string url)
    {
        var data = await Data(client, url);
        return data.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
    }

    private static async Task<Dictionary<Guid, Guid?>> Meetings(HttpClient client, string url)
    {
        var data = await Data(client, url);
        return data.EnumerateArray().ToDictionary(
            e => e.GetProperty("id").GetGuid(),
            e => e.GetProperty("partyCellId").ValueKind == JsonValueKind.Null ? (Guid?)null : e.GetProperty("partyCellId").GetGuid());
    }

    private static async Task<string> Message(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, string formCode, string? ownerType, Guid? ownerId)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "minh-chung.pdf");
        content.Add(new StringContent(formCode), "formCode");
        if (ownerType != null)
            content.Add(new StringContent(ownerType), "ownerType");
        if (ownerId != null)
            content.Add(new StringContent(ownerId.Value.ToString()), "ownerId");
        return await client.PostAsync("/api/attachments/upload", content);
    }

    private static async Task<List<string>> CadreNames(HttpClient client)
    {
        var response = await client.GetAsync("/api/reports/cadres");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        return sheet.RowsUsed().Where(r => r.RowNumber() >= 6).Select(r => r.Cell(2).GetString()).ToList();
    }

    /// <summary>Dữ liệu dùng chung: 2 Phòng, 2 Chi bộ, 1 kỳ, các cán bộ được gán vai trò mặc định với phạm vi khác nhau.</summary>
    private sealed class Scenario
    {
        public Guid DeptA, DeptB, CellA, CellB, PeriodId;
        public Guid RecordA1, RecordA2, RecordB1, RecordS, RecordT;
        public Guid MeetingA, MeetingB, CollectiveA, CollectiveB;
        public string NameA1 = string.Empty, NameB1 = string.Empty;
        public HttpClient OwnerA1 = null!, OwnerB1 = null!, CellSecA = null!, DeptLeadA = null!, DeptLeadB = null!;
        public HttpClient Appraiser = null!, DeptAppraiserA = null!, Office = null!, Committee = null!, Admin = null!;
        public HttpClient Plain = null!, LocalDecider = null!, CellReporterA = null!, DeptSecretaryA = null!;

        public IEnumerable<Guid> AllRecords => new[] { RecordA1, RecordA2, RecordB1, RecordS, RecordT };

        public static async Task<Scenario> CreateAsync(ApiFactory factory)
        {
            var s = new Scenario();
            var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            await factory.WithDbAsync(async db =>
            {
                var deptA = new AdministrativeDepartment { Code = $"IT-PA-{suffix}", Name = $"Phòng A {suffix}" };
                var deptB = new AdministrativeDepartment { Code = $"IT-PB-{suffix}", Name = $"Phòng B {suffix}" };
                var cellA = new PartyCell { Code = $"IT-CA-{suffix}", Name = $"Chi bộ A {suffix}" };
                var cellB = new PartyCell { Code = $"IT-CB-{suffix}", Name = $"Chi bộ B {suffix}" };
                var period = new EvaluationPeriod
                {
                    Year = 2030, Quarter = EvaluationQuarter.Quy1, Name = $"Kỳ ma trận {suffix}",
                    StartDate = DateTime.UtcNow.AddDays(-10), EndDate = DateTime.UtcNow.AddDays(80),
                    Status = PeriodStatus.Open
                };
                db.AddRange(deptA, deptB, cellA, cellB, period);
                await db.SaveChangesAsync();
                (s.DeptA, s.DeptB, s.CellA, s.CellB, s.PeriodId) = (deptA.Id, deptB.Id, cellA.Id, cellB.Id, period.Id);
            });

            async Task<Guid> Role(string code) => await factory.GetRoleIdAsync(code);
            var evaluatee = await Role("NGUOI_DUOC_DANH_GIA");

            s.NameA1 = $"IT A1 {suffix}";
            s.NameB1 = $"IT B1 {suffix}";
            var ownerA1 = await factory.CreateUserAsync(s.DeptA, s.CellA, ApprovalAuthority.CoSo, s.NameA1);
            var ownerA2 = await factory.CreateUserAsync(s.DeptA, s.CellA, ApprovalAuthority.CapTren);
            var ownerB1 = await factory.CreateUserAsync(s.DeptB, s.CellB, ApprovalAuthority.CoSo, s.NameB1);
            var cellSecA = await factory.CreateUserAsync(s.DeptB, s.CellA);
            var deptLeadA = await factory.CreateUserAsync(s.DeptA, s.CellB);
            var deptLeadB = await factory.CreateUserAsync(s.DeptB, s.CellB);
            var appraiser = await factory.CreateUserAsync(s.DeptB, s.CellB);
            var deptAppraiserA = await factory.CreateUserAsync(s.DeptB, s.CellB);
            var office = await factory.CreateUserAsync();
            var committee = await factory.CreateUserAsync();
            var admin = await factory.CreateUserAsync();
            var plain = await factory.CreateUserAsync(s.DeptA, s.CellA);
            var localDecider = await factory.CreateUserAsync();
            var cellReporterA = await factory.CreateUserAsync();
            var deptSecretaryA = await factory.CreateUserAsync();

            foreach (var user in new[] { ownerA1, ownerA2, ownerB1, cellSecA, appraiser })
                await factory.AssignAsync(user.Id, evaluatee, RoleScopeType.Global, null);
            await factory.AssignAsync(cellSecA.Id, await Role("CHI_UY_CHI_BO"), RoleScopeType.PartyCell, s.CellA);
            await factory.AssignAsync(deptLeadA.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, s.DeptA);
            await factory.AssignAsync(deptLeadB.Id, await Role("LANH_DAO_PHONG"), RoleScopeType.Department, s.DeptB);
            await factory.AssignAsync(appraiser.Id, await Role("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await factory.AssignAsync(office.Id, await Role("VAN_PHONG_DANG_UY"), RoleScopeType.Global, null);
            await factory.AssignAsync(committee.Id, await Role("CAP_UY_VIEN"), RoleScopeType.Global, null);
            await factory.AssignAsync(admin.Id, await factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);
            await factory.AssignAsync(deptSecretaryA.Id, await Role("THU_KY_TAP_THE"), RoleScopeType.Department, s.DeptA);
            await factory.AssignAsync(localDecider.Id, (await factory.CreateRoleAsync("evaluation.decide")).Id, RoleScopeType.Global, null);
            await factory.AssignAsync(cellReporterA.Id, (await factory.CreateRoleAsync("report.export")).Id, RoleScopeType.PartyCell, s.CellA);
            await factory.AssignAsync(deptAppraiserA.Id, (await factory.CreateRoleAsync("evaluation.appraise")).Id, RoleScopeType.Department, s.DeptA);
            // Lãnh đạo Phòng B cần evaluation.read theo Phòng (vai trò mặc định đã có) — dùng cho hồ sơ tập thể theo Phòng.

            await factory.WithDbAsync(async db =>
            {
                EvaluationRecord Record(TestUser owner, Guid dept, Guid cell, ApprovalAuthority authority) => new()
                {
                    PeriodId = s.PeriodId, MemberId = owner.Id, DepartmentId = dept, PartyCellId = cell,
                    ApprovalAuthority = authority, Status = RecordStatus.AwaitingRegistration, UpdatedAt = DateTime.UtcNow
                };
                var a1 = Record(ownerA1, s.DeptA, s.CellA, ApprovalAuthority.CoSo);
                var a2 = Record(ownerA2, s.DeptA, s.CellA, ApprovalAuthority.CapTren);
                var b1 = Record(ownerB1, s.DeptB, s.CellB, ApprovalAuthority.CoSo);
                var rs = Record(cellSecA, s.DeptB, s.CellA, ApprovalAuthority.CoSo);
                var rt = Record(appraiser, s.DeptB, s.CellB, ApprovalAuthority.CoSo);
                var meetingA = new EvaluationMeeting { PeriodId = s.PeriodId, PartyCellId = s.CellA, FormCode = "M12", MeetingType = "Hội nghị A", UpdatedAt = DateTime.UtcNow };
                var meetingB = new EvaluationMeeting { PeriodId = s.PeriodId, PartyCellId = s.CellB, FormCode = "M12", MeetingType = "Hội nghị B", UpdatedAt = DateTime.UtcNow };
                var collectiveA = new CollectiveEvaluationRecord { PeriodId = s.PeriodId, Form = CollectiveEvaluationForm.M07, PartyCellId = s.CellA, SubjectName = "Chi bộ A", UpdatedAt = DateTime.UtcNow };
                var collectiveB = new CollectiveEvaluationRecord { PeriodId = s.PeriodId, Form = CollectiveEvaluationForm.M06, DepartmentId = s.DeptB, SubjectName = "Phòng B", UpdatedAt = DateTime.UtcNow };
                db.AddRange(a1, a2, b1, rs, rt, meetingA, meetingB, collectiveA, collectiveB);
                await db.SaveChangesAsync();
                (s.RecordA1, s.RecordA2, s.RecordB1, s.RecordS, s.RecordT) = (a1.Id, a2.Id, b1.Id, rs.Id, rt.Id);
                (s.MeetingA, s.MeetingB, s.CollectiveA, s.CollectiveB) = (meetingA.Id, meetingB.Id, collectiveA.Id, collectiveB.Id);
            });

            Task<HttpClient> Login(TestUser user) => factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            s.OwnerA1 = await Login(ownerA1);
            s.OwnerB1 = await Login(ownerB1);
            s.CellSecA = await Login(cellSecA);
            s.DeptLeadA = await Login(deptLeadA);
            s.DeptLeadB = await Login(deptLeadB);
            s.Appraiser = await Login(appraiser);
            s.DeptAppraiserA = await Login(deptAppraiserA);
            s.Office = await Login(office);
            s.Committee = await Login(committee);
            s.Admin = await Login(admin);
            s.Plain = await Login(plain);
            s.LocalDecider = await Login(localDecider);
            s.CellReporterA = await Login(cellReporterA);
            s.DeptSecretaryA = await Login(deptSecretaryA);
            return s;
        }
    }

    #endregion
}
