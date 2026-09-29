using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 18 (T-82, T-83): nhập Mẫu 09C + 9D (+ phần tự luận theo trục 09B) khi tự chấm → nộp → xuất 09B/09C/9D ra .docx hợp lệ
/// (không còn tag, có nội dung đã nhập, tên đơn vị từ cài đặt); người ngoài phạm vi 403; trả lại → sửa được; khóa/công bố → không sửa.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class IndividualFormsIntegrationTests
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private readonly ApiFactory _factory;

    public IndividualFormsIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task F1_SelfScoreWith09C9D_Submit_Export09B09C9D()
    {
        var w = await World.CreateAsync(_factory);
        var (_, recordId) = await OpenRecordAsync(w);

        // Danh sách mẫu áp dụng theo bộ 09B (Quý III/2026): không có 01/02/09A.
        var forms = (await Data(w.Owner, $"/api/reports/docx/record/{recordId}")).EnumerateArray().Select(f => f.GetProperty("code").GetString()).ToList();
        Assert.Equal(new[] { "09B", "09C", "9D", "10" }, forms);

        // Kiểm tra độ dài 09C, trục 9D không có trong bộ → 400 (hồ sơ không đổi).
        var tooLong = await SubmitAsync(w, recordId, SelfScoreBody(selfAssessment: new string('a', 6001)));
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("6000 ký tự", await MessageAsync(tooLong));
        var badAxis = await SubmitAsync(w, recordId, SelfScoreBody(extraAxis: "T9"));
        Assert.Equal(HttpStatusCode.BadRequest, badAxis.StatusCode);
        Assert.Contains("\"T9\"", await MessageAsync(badAxis));

        var saved = await Data(await SubmitAsync(w, recordId, SelfScoreBody()));
        Assert.Equal("AwaitingCellConfirm", saved.GetProperty("status").GetString());
        Assert.Equal("Tự đánh giá mục I — lần 1", saved.GetProperty("selfAssessment").GetProperty("I").GetString());
        var rows = saved.GetProperty("taskResults").EnumerateArray().ToList();
        Assert.Equal(new[] { "T1", "T1", "T4" }, rows.Select(r => r.GetProperty("axisCode").GetString()));
        Assert.Equal("Mục tiêu trục 1 (IT)", saved.GetProperty("axisNotes").GetProperty("T1").GetProperty("target").GetString());

        // Lịch sử ghi nhận thay đổi biểu mẫu.
        var history = (await Data(w.Owner, $"/api/evaluations/records/{recordId}/history")).EnumerateArray().ToList();
        Assert.Contains(history, h => h.GetProperty("comment").GetString()!.Contains("Mẫu 09C") && h.GetProperty("comment").GetString()!.Contains("Mẫu 9D"));

        var settings = await Data(w.Manager, "/api/settings/organization");
        var partyName = settings.GetProperty("partyCommitteeName").GetString();

        var b = await DocxAsync(w.Owner, $"/api/reports/docx/record/{recordId}/09B");
        Assert.Contains("PHIẾU TỰ CHẤM ĐIỂM", b.Text);
        Assert.Contains("TRỤC (1) – NHIỆM VỤ CHÍNH TRỊ", b.Text);
        Assert.Contains("Mục tiêu trục 1 (IT)", b.Text);
        Assert.Contains("Kết quả trục 1 (IT)", b.Text);
        Assert.Contains(w.OwnerName, b.Text);
        Assert.Contains(w.PositionName, b.Text);
        Assert.Contains($"CHI BỘ 1 {w.Suffix}", b.Text);

        var c = await DocxAsync(w.Owner, $"/api/reports/docx/record/{recordId}/09c");
        Assert.Contains("BẢN TỰ ĐÁNH GIÁ, XẾP LOẠI CỦA CÁ NHÂN", c.Text);
        Assert.Contains("Tự đánh giá mục I — lần 1", c.Text);
        Assert.Contains("Hoàn thành tốt nhiệm vụ", c.Text);

        var d = await DocxAsync(w.CellSec, $"/api/reports/docx/record/{recordId}/9D");
        Assert.Contains("PHỤ LỤC", d.Text);
        Assert.Contains("Việc T1 thứ nhất (IT)", d.Text);
        Assert.Contains("Việc T4 (IT)", d.Text);
        Assert.Contains("Trục 6", d.Text);
        Assert.Contains(w.OwnerName, d.Text);

        foreach (var doc in new[] { b, c, d })
        {
            if (!string.IsNullOrWhiteSpace(partyName))
                Assert.Contains(partyName, doc.Text);
            Assert.DoesNotContain("«", doc.Text);
            Assert.Equal(0, doc.TaggedControls);
            Assert.Empty(doc.ValidationErrors);
        }
        Assert.StartsWith("Mau_09B_", b.FileName);

        // Mẫu không áp dụng cho kỳ → 409; mã không có → 400.
        Assert.Equal(HttpStatusCode.Conflict, (await w.Owner.GetAsync($"/api/reports/docx/record/{recordId}/01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.Owner.GetAsync($"/api/reports/docx/record/{recordId}/07")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await w.Owner.GetAsync($"/api/reports/docx/record/{recordId}/09B?format=xlsx")).StatusCode);

        // Người ngoài phạm vi (xem hồ sơ Phòng 2) và người không có quyền → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Outsider.GetAsync($"/api/reports/docx/record/{recordId}/09C")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Outsider.GetAsync($"/api/reports/docx/record/{recordId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await w.Plain.GetAsync($"/api/reports/docx/record/{recordId}/9D")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await w.Owner.GetAsync($"/api/reports/docx/record/{Guid.NewGuid()}/09B")).StatusCode);
    }

    [SkippableFact]
    public async Task F2_ReturnedRecord_IsEditable_LockedOrPublished_IsReadOnly()
    {
        var w = await World.CreateAsync(_factory);
        var (periodId, recordId) = await OpenRecordAsync(w);
        await Data(await SubmitAsync(w, recordId, SelfScoreBody()));

        // Chi bộ trả lại → chủ hồ sơ sửa 09C/9D rồi nộp lại; không gửi axisNotes thì giữ nội dung đã lưu.
        await Data(await PostAsync(w.CellSec, recordId, "cell/return", new { reason = "Bổ sung mục I" }));
        var body = SelfScoreBody(selfAssessment: "Tự đánh giá mục I — lần 2", withAxisNotes: false);
        var resubmitted = await Data(await SubmitAsync(w, recordId, body));
        Assert.Equal("AwaitingCellConfirm", resubmitted.GetProperty("status").GetString());
        Assert.Equal("Tự đánh giá mục I — lần 2", resubmitted.GetProperty("selfAssessment").GetProperty("I").GetString());
        Assert.Equal("Mục tiêu trục 1 (IT)", resubmitted.GetProperty("axisNotes").GetProperty("T1").GetProperty("target").GetString());
        var exported = await DocxAsync(w.Owner, $"/api/reports/docx/record/{recordId}/09C");
        Assert.Contains("Tự đánh giá mục I — lần 2", exported.Text);
        Assert.DoesNotContain("lần 1", exported.Text);

        // Đã công bố → không sửa được (409), nội dung giữ nguyên; vẫn xuất được.
        await SetStatusAsync(recordId, RecordStatus.Published);
        var published = await SubmitAsync(w, recordId, SelfScoreBody(selfAssessment: "Sửa sau công bố"));
        Assert.Equal(HttpStatusCode.Conflict, published.StatusCode);
        var afterPublish = await Data(w.Owner, $"/api/evaluations/records/{recordId}");
        Assert.Equal("Tự đánh giá mục I — lần 2", afterPublish.GetProperty("selfAssessment").GetProperty("I").GetString());
        Assert.Contains("Tự đánh giá mục I — lần 2", (await DocxAsync(w.Owner, $"/api/reports/docx/record/{recordId}/09C")).Text);

        // Kỳ khóa dữ liệu → chủ hồ sơ không sửa được (409).
        await SetStatusAsync(recordId, RecordStatus.AwaitingSelfScore);
        await TransitionAsync(w.Manager, periodId, "lock");
        var locked = await SubmitAsync(w, recordId, SelfScoreBody(selfAssessment: "Sửa khi khóa"));
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains("khóa dữ liệu", await MessageAsync(locked));
    }

    #region Hỗ trợ

    private static object SelfScoreBody(string selfAssessment = "Tự đánh giá mục I — lần 1", string? extraAxis = null, bool withAxisNotes = true)
    {
        var rows = new List<object>
        {
            new { axisCode = "T1", content = "Việc T1 thứ nhất (IT)", deadline = "30/9/2026", status = "Đã hoàn thành", product = "Báo cáo", progress = "Đúng hạn" },
            new { axisCode = "T4", content = "Việc T4 (IT)" },
            new { axisCode = "t1", content = "Việc T1 thứ hai (IT)" }
        };
        if (extraAxis != null)
            rows.Add(new { axisCode = extraAxis, content = "Trục lạ" });

        var general = CriteriaTestData.General("2.1");
        var axis = CriteriaTestData.Axis(14, 9, 9, 14, 9, 9);
        if (!withAxisNotes)
            return new { generalScores = general, axisScores = axis, selfProposedGrade = "HoanThanhTot", selfAssessment = new Dictionary<string, string> { ["I"] = selfAssessment }, taskResults = rows };
        return new
        {
            generalScores = general,
            axisScores = axis,
            selfProposedGrade = "HoanThanhTot",
            selfAssessment = new Dictionary<string, string> { ["I"] = selfAssessment },
            taskResults = rows,
            axisNotes = new Dictionary<string, object> { ["T1"] = new { target = "Mục tiêu trục 1 (IT)", result = "Kết quả trục 1 (IT)" } }
        };
    }

    private async Task<(Guid PeriodId, Guid RecordId)> OpenRecordAsync(World w)
    {
        var body = new
        {
            year = 2100 - Random.Shared.Next(1, 90), quarter = Random.Shared.Next(1, 5), preset = "q3-2026-transition",
            name = $"Kỳ J18 {Guid.NewGuid().ToString("N")[..8]}",
            startDate = DateTime.UtcNow.AddDays(-5), endDate = DateTime.UtcNow.AddDays(60)
        };
        var periodId = (await Data(await w.Manager.PostAsJsonAsync("/api/evaluations/periods", body))).GetProperty("id").GetGuid();
        await Data(await w.Manager.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants", new { memberIds = new[] { w.OwnerId } }));
        await TransitionAsync(w.Manager, periodId, "open");
        var recordId = (await Data(w.Manager, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .First(p => p.GetProperty("memberId").GetGuid() == w.OwnerId).GetProperty("recordId").GetGuid();
        return (periodId, recordId);
    }

    private static async Task TransitionAsync(HttpClient client, Guid periodId, string action)
    {
        var period = await Data(client, $"/api/evaluations/periods/{periodId}");
        await Data(await client.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/{action}",
            new { version = period.GetProperty("version").GetUInt32(), reason = "Kiểm thử biểu mẫu cá nhân", force = true }));
    }

    private static Task<HttpResponseMessage> SubmitAsync(World w, Guid recordId, object body) =>
        PostAsync(w.Owner, recordId, "self-score/submit", body, w.Manager);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, Guid recordId, string action, object body, HttpClient? reader = null)
    {
        var version = (await Data(reader ?? client, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["version"] = version;
        return await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    private async Task SetStatusAsync(Guid recordId, RecordStatus status) =>
        await _factory.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.FirstAsync(r => r.Id == recordId);
            record.Status = status;
            await db.SaveChangesAsync();
        });

    private sealed record DocxResult(string Text, int TaggedControls, List<string> ValidationErrors, string FileName);

    private static async Task<DocxResult> DocxAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {url}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(DocxType, response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var text = string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText));
        var tagged = body.Descendants<SdtElement>().Count(s => s.SdtProperties?.GetFirstChild<Tag>() != null);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2019).Validate(doc).Select(e => e.Description).ToList();
        return new DocxResult(text, tagged, errors, response.Content.Headers.ContentDisposition?.FileName?.Trim('"') ?? string.Empty);
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

    /// <summary>Dữ liệu dùng chung: 2 Phòng, 2 Chi bộ, chủ hồ sơ (có chức vụ Đảng), Chi ủy Chi bộ 1, người quản lý kỳ, người ngoài phạm vi.</summary>
    private sealed class World
    {
        public string Suffix = string.Empty;
        public Guid OwnerId;
        public string OwnerName = string.Empty;
        public string PositionName = string.Empty;
        public HttpClient Owner = null!, CellSec = null!, Manager = null!, Outsider = null!, Plain = null!;

        public static async Task<World> CreateAsync(ApiFactory factory)
        {
            Skip.If(factory.SkipReason != null, factory.SkipReason);
            var w = new World { Suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant() };
            Guid dept1 = Guid.Empty, dept2 = Guid.Empty, cell1 = Guid.Empty;
            await factory.WithDbAsync(async db =>
            {
                var d1 = new AdministrativeDepartment { Code = $"J-P1-{w.Suffix}", Name = $"Phòng 1 {w.Suffix}" };
                var d2 = new AdministrativeDepartment { Code = $"J-P2-{w.Suffix}", Name = $"Phòng 2 {w.Suffix}" };
                var c1 = new PartyCell { Code = $"J-C1-{w.Suffix}", Name = $"Chi bộ 1 {w.Suffix}" };
                var c2 = new PartyCell { Code = $"J-C2-{w.Suffix}", Name = $"Chi bộ 2 {w.Suffix}" };
                db.AddRange(d1, d2, c1, c2);
                await db.SaveChangesAsync();
                (dept1, dept2, cell1) = (d1.Id, d2.Id, c1.Id);
            });

            w.OwnerName = $"J Chủ hồ sơ {w.Suffix}";
            var owner = await factory.CreateUserAsync(dept1, cell1, ApprovalAuthority.CoSo, w.OwnerName);
            w.OwnerId = owner.Id;
            w.PositionName = $"Bí thư Chi bộ J {w.Suffix}";
            await factory.WithDbAsync(async db =>
            {
                var position = new CongTacDang.Domain.Entities.Position { Name = w.PositionName, Side = PositionSide.Party, SortOrder = 1 };
                db.Add(position);
                db.Add(new MemberPosition { UserId = owner.Id, PositionId = position.Id, PartyCellId = cell1, IsPrimary = true, ValidFrom = DateTime.UtcNow.AddYears(-1) });
                await db.SaveChangesAsync();
            });

            var cellSec = await factory.CreateUserAsync(null, null);
            var manager = await factory.CreateUserAsync(null, null);
            var outsider = await factory.CreateUserAsync(dept2, null);
            var plain = await factory.CreateUserAsync(dept1, cell1);

            await factory.AssignAsync(owner.Id, await factory.GetRoleIdAsync("NGUOI_DUOC_DANH_GIA"), RoleScopeType.Global, null);
            await factory.AssignAsync(cellSec.Id, await factory.GetRoleIdAsync("CHI_UY_CHI_BO"), RoleScopeType.PartyCell, cell1);
            await factory.AssignAsync(manager.Id, await factory.GetRoleIdAsync("CO_QUAN_THAM_DINH"), RoleScopeType.Global, null);
            await factory.AssignAsync(manager.Id, (await factory.CreateRoleAsync("system.settings.manage")).Id, RoleScopeType.Global, null);
            await factory.AssignAsync(outsider.Id, (await factory.CreateRoleAsync("evaluation.read")).Id, RoleScopeType.Department, dept2);

            Task<HttpClient> Login(TestUser user) => factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
            w.Owner = await Login(owner);
            w.CellSec = await Login(cellSec);
            w.Manager = await Login(manager);
            w.Outsider = await Login(outsider);
            w.Plain = await Login(plain);
            return w;
        }
    }

    #endregion
}
