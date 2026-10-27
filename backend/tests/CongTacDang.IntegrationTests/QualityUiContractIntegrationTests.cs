using System.Net;
using System.Text;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Biên bản hiệu chuẩn (Mẫu 20) và phiếu kiểm soát (Mẫu 19) với đúng nội dung JSON mà giao diện gửi
/// (chuỗi JSON.stringify: trường để trống là null, ngày dạng yyyy-MM-dd, danh sách gửi đủ).
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class QualityUiContractIntegrationTests
{
    private readonly ApiFactory _factory;

    public QualityUiContractIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Calibration_UiBodies_CreateSaveTwiceConcludeReopenVoid()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync(AwaitingStatus.DirectorReview);

        // Danh sách ứng viên gọi chỉ với kỳ.
        var candidates = await Data(w.PreparerC, $"/api/calibrations/candidates?periodId={w.PeriodId}");
        Assert.Equal(3, candidates.GetArrayLength());

        // Lập: giao diện chỉ gửi kỳ, tên nhóm, hồ sơ.
        var created = await Data(await Raw(w.PreparerC, HttpMethod.Post, "/api/calibrations",
            $$"""{"periodId":"{{w.PeriodId}}","groupName":"Nhóm giao diện","recordIds":["{{w.RecordA}}","{{w.RecordB}}"]}"""));
        var id = created.GetProperty("id").GetGuid();
        var v1 = created.GetProperty("version").GetUInt32();
        Assert.Equal(5, created.GetProperty("summary").GetArrayLength());

        // Lưu lần một đúng như trang chi tiết: ngày yyyy-MM-dd, ô trống là null, đủ bốn dòng tổng hợp.
        string Save(uint version, string cases, string extra = "null") => $$"""
            {"version":{{version}},"groupName":"Nhóm giao diện","heldAt":"2026-10-26","location":null,"chairName":"Chủ trì","secretaryName":"Thư ký",
             "scopeNote":null,"extraConclusion":{{extra}},"recordIds":["{{w.RecordA}}","{{w.RecordB}}"],
             "attendees":[{"fullName":"Thành viên","memberId":null,"capacity":null,"dissentNote":null}],
             "summary":[{"gradeCode":"HoanThanhXuatSac","notableDifference":null,"preliminaryConclusion":null},
                        {"gradeCode":"HoanThanhTot","notableDifference":"Chênh lệch","preliminaryConclusion":null},
                        {"gradeCode":"HoanThanh","notableDifference":null,"preliminaryConclusion":null},
                        {"gradeCode":"KhongHoanThanh","notableDifference":null,"preliminaryConclusion":null}],
             "cases":{{cases}}}
            """;

        // Mới lưu chưa có trường hợp: không chốt được vì còn thiếu nội dung nhưng lưu được.
        var saved1 = await Data(await Raw(w.PreparerC, HttpMethod.Put, $"/api/calibrations/{id}", Save(v1, "[]")));
        Assert.StartsWith("2026-10-26", saved1.GetProperty("heldAt").GetString());
        Assert.Equal("Chủ trì", saved1.GetProperty("chairName").GetString());
        Assert.Equal(JsonValueKind.Null, saved1.GetProperty("location").ValueKind);
        var v2 = saved1.GetProperty("version").GetUInt32();
        Assert.NotEqual(v1, v2);

        // Lưu lần hai bằng phiên bản trả về của lần một: một trường hợp giữ nguyên, một trường hợp điều chỉnh điểm.
        var cases = $$"""
            [{"recordId":"{{w.RecordA}}","clarification":"Đã làm rõ","gradeAfter":null,"scoreAfter":null,"basis":null,"responsibleName":null,"responsibleMemberId":null},
             {"recordId":"{{w.RecordB}}","clarification":"Đã làm rõ","gradeAfter":"HoanThanhTot","scoreAfter":80.5,"basis":"Căn cứ","responsibleName":"Chủ trì","responsibleMemberId":null}]
            """;
        var saved2 = await Data(await Raw(w.PreparerC, HttpMethod.Put, $"/api/calibrations/{id}", Save(v2, cases)));
        var caseB = saved2.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("recordId").GetGuid() == w.RecordB);
        Assert.True(caseB.GetProperty("changed").GetBoolean());
        Assert.Equal(80.5, caseB.GetProperty("scoreAfter").GetDouble(), 6);
        Assert.Equal("HoanThanhTot", caseB.GetProperty("gradeAfter").GetString());
        var caseA = saved2.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("recordId").GetGuid() == w.RecordA);
        Assert.False(caseA.GetProperty("changed").GetBoolean());
        Assert.Equal(string.Empty, caseA.GetProperty("gradeAfter").GetString());

        // Chỉ ô chênh lệch ở dòng Hoàn thành tốt được giữ; dòng khác để trống không tạo dòng chữ.
        var summaryRows = saved2.GetProperty("summary").EnumerateArray().ToList();
        Assert.Equal("TOTAL", summaryRows.Last().GetProperty("gradeCode").GetString());
        Assert.Equal("Chênh lệch", summaryRows.Single(r => r.GetProperty("gradeCode").GetString() == "HoanThanhTot").GetProperty("notableDifference").GetString());

        // Chưa chốt: bản Word chỉ có số liệu gốc và dòng bản nháp.
        var draftText = await DocxTextAsync(w.PreparerC, $"/api/reports/docx/mau-20/{id}");
        Assert.Contains("BẢN NHÁP — CHƯA XÁC NHẬN", draftText);
        Assert.DoesNotContain("sau hiệu chuẩn", draftText);

        // Người lập thấy lý do không tự chốt, không có cờ chốt; người chốt chốt bằng phiên bản vừa nhận.
        Assert.False(saved2.GetProperty("canConclude").GetBoolean());
        var asConcluder = await Data(w.ConcluderC, $"/api/calibrations/{id}");
        Assert.True(asConcluder.GetProperty("canConclude").GetBoolean());
        Assert.Equal(JsonValueKind.Null, asConcluder.GetProperty("concludeBlockedReason").ValueKind);
        var v3 = saved2.GetProperty("version").GetUInt32();
        var concluded = await Data(await Raw(w.ConcluderC, HttpMethod.Post, $"/api/calibrations/{id}/conclude", $$"""{"version":{{v3}}}"""));
        Assert.Equal("Concluded", concluded.GetProperty("status").GetString());
        Assert.True(concluded.GetProperty("canReopen").GetBoolean());

        // Đã chốt: bỏ dòng bản nháp, thêm số liệu sau hiệu chuẩn; PDF xuất được hoặc báo rõ thiếu LibreOffice.
        var concludedText = await DocxTextAsync(w.ConcluderC, $"/api/reports/docx/mau-20/{id}");
        Assert.DoesNotContain("BẢN NHÁP", concludedText);
        Assert.Contains("(sau hiệu chuẩn: ", concludedText);
        await AssertPdfOrUnavailableAsync(w.ConcluderC, $"/api/reports/docx/mau-20/{id}?format=pdf");

        var reopened = await Data(await Raw(w.ConcluderC, HttpMethod.Post, $"/api/calibrations/{id}/reopen",
            $$"""{"version":{{concluded.GetProperty("version").GetUInt32()}},"reason":"Bổ sung"}"""));
        Assert.Equal("Draft", reopened.GetProperty("status").GetString());
        var reopenedForPreparer = await Data(w.PreparerC, $"/api/calibrations/{id}");
        Assert.True(reopenedForPreparer.GetProperty("canEdit").GetBoolean());
        Assert.True(reopenedForPreparer.GetProperty("canVoid").GetBoolean());

        var voided = await Data(await Raw(w.PreparerC, HttpMethod.Post, $"/api/calibrations/{id}/void",
            $$"""{"version":{{reopened.GetProperty("version").GetUInt32()}},"reason":"Lập nhầm"}"""));
        Assert.Equal("Void", voided.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task Calibration_Create_RecordsOfDifferentUnits_AreRefusedWithGuidance_AndPickerHasUnitInfo()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync(AwaitingStatus.DirectorReview);
        await _factory.WithDbAsync(async db =>
        {
            var d = new AdministrativeDepartment { Code = $"UI-P2-{Guid.NewGuid():N}"[..14], Name = "Phòng khác giao diện" };
            db.Add(d);
            await db.SaveChangesAsync();
            db.Set<EvaluationRecord>().Single(r => r.Id == w.RecordC).DepartmentId = d.Id;
            await db.SaveChangesAsync();
        });

        var candidates = await Data(w.PreparerC, $"/api/calibrations/candidates?periodId={w.PeriodId}");
        var units = candidates.EnumerateArray().Select(c => c.GetProperty("departmentName").GetString()).Distinct().ToList();
        Assert.Equal(2, units.Count);

        var response = await Raw(w.PreparerC, HttpMethod.Post, "/api/calibrations",
            $$"""{"periodId":"{{w.PeriodId}}","groupName":"Hai đơn vị","recordIds":["{{w.RecordA}}","{{w.RecordC}}"]}""");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("cùng một đơn vị", body.RootElement.GetProperty("message").GetString());
    }

    [SkippableFact]
    public async Task ControlCheck_UiBodies_CandidatesSaveWithRecordsAndClearedText_SubmitConfirm()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await SetupAsync(AwaitingStatus.Decision);

        // Người chỉ có quyền lập phiếu (không có quyền xem hồ sơ) vẫn lấy được ứng viên.
        var candidates = await Data(w.ControlPerformerOnlyC, $"/api/control-checks/candidates?periodId={w.PeriodId}");
        Assert.Equal(3, candidates.GetArrayLength());
        Assert.Equal(HttpStatusCode.Forbidden, (await w.NobodyC.GetAsync($"/api/control-checks/candidates?periodId={w.PeriodId}")).StatusCode);
        // Chủ hồ sơ có quyền lập không thấy hồ sơ của chính mình.
        var ownerView = await Data(w.OwnerPerformerC, $"/api/control-checks/candidates?periodId={w.PeriodId}");
        Assert.DoesNotContain(ownerView.EnumerateArray(), c => c.GetProperty("recordId").GetGuid() == w.RecordA);

        var created = await Data(await Raw(w.ControlPerformerOnlyC, HttpMethod.Post, "/api/control-checks",
            $$"""{"periodId":"{{w.PeriodId}}","groupName":"Nhóm giao diện","recordIds":["{{w.RecordA}}","{{w.RecordB}}"]}"""));
        var id = created.GetProperty("id").GetGuid();

        // Hồ sơ đã nằm trong phiếu còn hiệu lực không còn là ứng viên.
        var after = await Data(w.ControlPerformerOnlyC, $"/api/control-checks/candidates?periodId={w.PeriodId}");
        Assert.Equal(new[] { w.RecordC }, after.EnumerateArray().Select(c => c.GetProperty("recordId").GetGuid()).ToArray());

        // Lưu lần đầu đúng như trang chi tiết khi chưa chọn gì: kết quả null, kết luận rỗng, nội dung rỗng.
        string Items(Func<string, string> result, Func<string, string> evidence) =>
            "[" + string.Join(",", Codes().Select(c => $$"""{"itemCode":"{{c}}","result":{{result(c)}},"evidence":{{evidence(c)}}}""")) + "]";
        string Body(uint version, string records, string items, string conclusion, string needs) =>
            $$"""{"version":{{version}},"groupName":"Nhóm giao diện","recordIds":{{records}},"items":{{items}},"conclusion":"{{conclusion}}","needsWorkContent":"{{needs}}"}""";
        var bothRecords = $"[\"{w.RecordA}\",\"{w.RecordB}\"]";
        var empty = await Data(await Raw(w.ControlPerformerOnlyC, HttpMethod.Put, $"/api/control-checks/{id}",
            Body(created.GetProperty("version").GetUInt32(), bothRecords, Items(_ => "null", _ => "null"), "", "")));
        Assert.Equal(2, empty.GetProperty("records").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("conclusion").ValueKind);

        // Thay hồ sơ B bằng hồ sơ C (gỡ rồi thêm từ danh sách ứng viên), có mục chưa đạt kèm minh chứng và nội dung cần hoàn thiện.
        var replaced = $"[\"{w.RecordA}\",\"{w.RecordC}\"]";
        var items = Items(c => c == "K03" ? "\"Fail\"" : "\"Pass\"", c => c == "K03" ? "\"Thiếu minh chứng\"" : "null");
        var saved = await Data(await Raw(w.ControlPerformerOnlyC, HttpMethod.Put, $"/api/control-checks/{id}",
            Body(empty.GetProperty("version").GetUInt32(), replaced, items, "NeedsWork", "Bổ sung minh chứng")));
        Assert.Equal(new[] { w.RecordA, w.RecordC }.OrderBy(g => g),
            saved.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("recordId").GetGuid()).OrderBy(g => g));
        Assert.Equal("NeedsWork", saved.GetProperty("conclusion").GetString());
        Assert.Equal("Bổ sung minh chứng", saved.GetProperty("needsWorkContent").GetString());
        Assert.True(saved.GetProperty("canSubmit").GetBoolean());

        // Chọn lại Đủ điều kiện, xóa trắng nội dung cần hoàn thiện: nội dung phải được xóa, không giữ bản cũ.
        var passItems = Items(_ => "\"Pass\"", _ => "null");
        var cleared = await Data(await Raw(w.ControlPerformerOnlyC, HttpMethod.Put, $"/api/control-checks/{id}",
            Body(saved.GetProperty("version").GetUInt32(), replaced, passItems, "Eligible", "")));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("needsWorkContent").ValueKind);
        Assert.Equal("Eligible", cleared.GetProperty("conclusion").GetString());

        // Tên nhóm để trống trên giao diện phải báo lỗi, không im lặng giữ tên cũ.
        var noName = await Raw(w.ControlPerformerOnlyC, HttpMethod.Put, $"/api/control-checks/{id}",
            Body(cleared.GetProperty("version").GetUInt32(), replaced, passItems, "Eligible", "").Replace("Nhóm giao diện", ""));
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);

        // Gửi xác nhận, xác nhận bằng phiên bản trả về.
        var submitted = await Data(await Raw(w.ControlPerformerOnlyC, HttpMethod.Post, $"/api/control-checks/{id}/submit",
            $$"""{"version":{{cleared.GetProperty("version").GetUInt32()}}}"""));
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        Assert.Contains("BẢN NHÁP — CHƯA XÁC NHẬN", await DocxTextAsync(w.ControlConfirmerC, $"/api/reports/docx/mau-19/{id}"));
        var asConfirmer = await Data(w.ControlConfirmerC, $"/api/control-checks/{id}");
        Assert.True(asConfirmer.GetProperty("canConfirm").GetBoolean());
        var confirmed = await Data(await Raw(w.ControlConfirmerC, HttpMethod.Post, $"/api/control-checks/{id}/confirm",
            $$"""{"version":{{asConfirmer.GetProperty("version").GetUInt32()}}}"""));
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
        Assert.DoesNotContain("BẢN NHÁP", await DocxTextAsync(w.ControlConfirmerC, $"/api/reports/docx/mau-19/{id}"));
        await AssertPdfOrUnavailableAsync(w.ControlConfirmerC, $"/api/reports/docx/mau-19/{id}?format=pdf");

        // Trả lại, mở lại và hủy đều nhận {version, reason}.
        var reopened = await Data(await Raw(w.ControlConfirmerC, HttpMethod.Post, $"/api/control-checks/{id}/reopen",
            $$"""{"version":{{confirmed.GetProperty("version").GetUInt32()}},"reason":"Xem lại"}"""));
        Assert.Equal("Draft", reopened.GetProperty("status").GetString());
        var cancelled = await Data(await Raw(w.ControlPerformerOnlyC, HttpMethod.Post, $"/api/control-checks/{id}/cancel",
            $$"""{"version":{{reopened.GetProperty("version").GetUInt32()}},"reason":"Lập nhầm"}"""));
        Assert.Equal("Cancelled", cancelled.GetProperty("status").GetString());

        // Hủy phiếu thì hồ sơ trở lại danh sách ứng viên.
        var released = await Data(w.ControlPerformerOnlyC, $"/api/control-checks/candidates?periodId={w.PeriodId}");
        Assert.Equal(3, released.GetArrayLength());
    }

    #region Hỗ trợ

    /// <summary>Tải bản Word và trả toàn bộ chữ trong thân văn bản.</summary>
    private static async Task<string> DocxTextAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {url}: {await response.Content.ReadAsStringAsync()}");
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        return System.Text.RegularExpressions.Regex.Replace(await reader.ReadToEndAsync(), "<[^>]+>", string.Empty);
    }

    /// <summary>Máy chủ không có LibreOffice thì 503 kèm thông báo tiếng Việt cho giao diện hiện.</summary>
    private static async Task AssertPdfOrUnavailableAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
            return;
        }
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("PDF", await response.Content.ReadAsStringAsync());
    }

    private enum AwaitingStatus { DirectorReview, Decision }

    private static string[] Codes() => Enumerable.Range(1, 10).Select(i => $"K{i:00}").ToArray();

    private sealed class World
    {
        public Guid PeriodId, DepartmentId, RecordA, RecordB, RecordC;
        public HttpClient PreparerC = null!, ConcluderC = null!, ControlPerformerOnlyC = null!, ControlConfirmerC = null!, OwnerPerformerC = null!, NobodyC = null!;
    }

    private async Task<World> SetupAsync(AwaitingStatus status)
    {
        var w = new World();
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var owners = new List<TestUser>();
        for (var i = 0; i < 3; i++)
            owners.Add(await _factory.CreateUserAsync(fullName: $"UI Cán bộ {i + 1} {suffix}"));
        await _factory.WithDbAsync(async db =>
        {
            var dept = new AdministrativeDepartment { Code = $"UI-P-{suffix}", Name = $"Phòng UI {suffix}" };
            var settings = PeriodSettings.Default();
            foreach (var profile in settings.Profiles)
                profile.Gates[WorkflowGates.Calibration] = new GateSetting { Mode = status == AwaitingStatus.Decision ? GateMode.Off : GateMode.Required };
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy2, Name = $"Kỳ UI {suffix}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                Settings = settings.ToJson(), CriteriaSnapshot = CriteriaTestData.Snapshot(collectiveFramework: false)
            };
            db.AddRange(dept, period);
            await db.SaveChangesAsync();
            (w.DepartmentId, w.PeriodId) = (dept.Id, period.Id);

            var grades = new[] { (EvaluationGrade.HoanThanhTot, 85.0), (EvaluationGrade.HoanThanh, 72.0), (EvaluationGrade.HoanThanhTot, 88.0) };
            var records = owners.Select((o, i) => new EvaluationRecord
            {
                PeriodId = period.Id, MemberId = o.Id, DepartmentId = dept.Id, ApprovalAuthority = ApprovalAuthority.CoSo, WeightFrameCode = "K2",
                Status = status == AwaitingStatus.Decision ? RecordStatus.AwaitingDecision : RecordStatus.AwaitingDirectorReview,
                AppraisalProposedGrade = grades[i].Item1, AppraisalScore = grades[i].Item2,
                AppraisedAt = DateTime.UtcNow.AddHours(-1), SelfScoredAt = DateTime.UtcNow.AddDays(-1), TotalSelfScore = grades[i].Item2
            }).ToList();
            db.AddRange(records);
            await db.SaveChangesAsync();
            (w.RecordA, w.RecordB, w.RecordC) = (records[0].Id, records[1].Id, records[2].Id);
        });

        var preparer = await _factory.CreateUserAsync(fullName: $"UI Người lập {suffix}");
        var concluder = await _factory.CreateUserAsync(fullName: $"UI Người chốt {suffix}");
        var performerOnly = await _factory.CreateUserAsync(fullName: $"UI Người lập phiếu {suffix}");
        var confirmer = await _factory.CreateUserAsync(fullName: $"UI Người xác nhận {suffix}");
        var nobody = await _factory.CreateUserAsync(fullName: $"UI Không quyền {suffix}");
        var calibrationManage = await _factory.CreateRoleAsync(PermissionCodes.EvaluationCalibrationManage, PermissionCodes.EvaluationRead);
        var calibrationConclude = await _factory.CreateRoleAsync(PermissionCodes.EvaluationCalibrationConclude, PermissionCodes.EvaluationRead);
        var controlPerform = await _factory.CreateRoleAsync(PermissionCodes.EvaluationControlPerform);
        var controlConfirm = await _factory.CreateRoleAsync(PermissionCodes.EvaluationControlConfirm);
        await _factory.AssignAsync(preparer.Id, calibrationManage.Id, RoleScopeType.Global, null);
        await _factory.AssignAsync(concluder.Id, calibrationConclude.Id, RoleScopeType.Global, null);
        await _factory.AssignAsync(performerOnly.Id, controlPerform.Id, RoleScopeType.Global, null);
        await _factory.AssignAsync(confirmer.Id, controlConfirm.Id, RoleScopeType.Global, null);
        await _factory.AssignAsync(owners[0].Id, controlPerform.Id, RoleScopeType.Global, null);

        w.PreparerC = await _factory.LoginAsAsync(preparer.Username, preparer.Password, distinctClientIp: true);
        w.ConcluderC = await _factory.LoginAsAsync(concluder.Username, concluder.Password, distinctClientIp: true);
        w.ControlPerformerOnlyC = await _factory.LoginAsAsync(performerOnly.Username, performerOnly.Password, distinctClientIp: true);
        w.ControlConfirmerC = await _factory.LoginAsAsync(confirmer.Username, confirmer.Password, distinctClientIp: true);
        w.OwnerPerformerC = await _factory.LoginAsAsync(owners[0].Username, owners[0].Password, distinctClientIp: true);
        w.NobodyC = await _factory.LoginAsAsync(nobody.Username, nobody.Password, distinctClientIp: true);
        return w;
    }

    private static Task<HttpResponseMessage> Raw(HttpClient client, HttpMethod method, string url, string json) =>
        client.SendAsync(new HttpRequestMessage(method, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") });

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
