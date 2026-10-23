using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Trọn một kỳ Quý IV/2026 theo bộ 09B với đủ 4 cách xét duyệt, đi hết các bước, xuất mọi biểu mẫu.
/// Chạy trên CSDL tạm riêng để kỳ đúng năm 2026, quý IV.</summary>
[Collection(Quarter4Collection.Name)]
public sealed class Quarter4ScenarioTests
{
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // Thông tin đơn vị (tên giả) — biểu mẫu phải in đúng các giá trị này, không phải giá trị mặc định khi cài đặt.
    private const string PartyCommittee = "ĐẢNG BỘ CÔNG TY THỬ NGHIỆM QUÝ BỐN";
    private const string SuperiorParty = "ĐẢNG BỘ TỔNG CÔNG TY THỬ NGHIỆM";
    private const string Company = "Công ty Thử nghiệm Quý Bốn";
    private const string ParentCompany = "Tổng công ty Thử nghiệm";

    /// <summary>Chữ "quý III" ghi cứng ở chỗ phải là quý của kỳ (không bắt số mục La Mã "III." của biểu mẫu).</summary>
    private static readonly Regex HardcodedQuarter3 = new(@"(qu[ýy]\s*III\b|\bIII\s*/\s*20\d\d|\bIII\s*[-–]\s*20\d\d|\bIII\s+NĂM\b|\bIII\s+năm\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Quý IV của kỳ.</summary>
    private static readonly Regex Quarter4 = new(@"(qu[ýy]\s*IV\b|\bIV\s*/\s*2026|\bIV\s+NĂM\s+2026)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ApiFactory _factory;

    public Quarter4ScenarioTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task Quarter4_2026_LikeQuarter3_AllFourProfiles_EndToEnd_ThenEveryFormPrintsQuarterIV()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var w = await World.CreateAsync(_factory);

        // ============ 1. Tạo kỳ Quý IV/2026 với bộ 09B ============
        var setId = await TestCriteriaSets.LatestPublishedAsync(w.Tccb, "09B");
        var created = await Data(await w.Tccb.PostAsJsonAsync("/api/evaluations/periods", new
        {
            year = 2026, quarter = 4, name = "Đánh giá cán bộ Quý IV/2026", criteriaSetId = setId,
            startDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), endDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        }));
        var periodId = created.GetProperty("id").GetGuid();
        var settings = JsonNode.Parse(created.GetProperty("settings").GetRawText())!.AsObject();
        var profileCodes = settings["profiles"]!.AsArray().Select(p => p!["code"]!.GetValue<string>()).ToList();
        Assert.Equal(new[] { "co-so", "cap-tren", "bi-thu-nhan-vien", "pho-giam-doc" }, profileCodes);
        // Bộ 09B: không đăng ký, không duyệt việc ở mọi cách xét duyệt.
        foreach (var profile in settings["profiles"]!.AsArray())
        {
            Assert.Equal("Off", profile!["steps"]!["B1_REGISTER"]!["mode"]!.GetValue<string>());
            Assert.Equal("Off", profile["steps"]!["B1_APPROVE"]!["mode"]!.GetValue<string>());
        }
        var deputy = Profile(settings, "pho-giam-doc")["steps"]!;
        Assert.Equal("Off", deputy["B3B_APPRAISAL"]!["mode"]!.GetValue<string>());
        Assert.Equal("Internal", deputy["B3C_DIRECTOR"]!["mode"]!.GetValue<string>());
        Assert.Equal("External", deputy["B4_DECISION"]!["mode"]!.GetValue<string>());

        // Thời hạn theo kế hoạch Quý IV của Phòng TCCB-LĐ (ngày của tháng cuối quý).
        SetDeadlines(settings, "co-so", ("B2_SELF_SCORE", 9), ("B3A_COLLECTIVE", 10), ("B3B_APPRAISAL", 11), ("B3C_DIRECTOR", 14), ("B4_DECISION", 14));
        SetDeadlines(settings, "bi-thu-nhan-vien", ("B2_SELF_SCORE", 9), ("B3A_COLLECTIVE", 10), ("B3B_APPRAISAL", 11), ("B3C_DIRECTOR", 14), ("B4_DECISION", 14));
        SetDeadlines(settings, "cap-tren", ("B2_SELF_SCORE", 9), ("B3A_COLLECTIVE", 14), ("B3B_APPRAISAL", 15), ("B3C_DIRECTOR", 17), ("B4_DECISION", 18));
        SetDeadlines(settings, "pho-giam-doc", ("B2_SELF_SCORE", 9), ("B3A_COLLECTIVE", 14), ("B3C_DIRECTOR", 14), ("B4_DECISION", 18));
        await Data(await w.Tccb.PutAsJsonAsync($"/api/evaluations/periods/{periodId}",
            new { version = created.GetProperty("version").GetUInt32(), settings }));

        // ============ 2. Thêm người; Bí thư Chi bộ là nhân viên thêm kèm cách xét duyệt; đổi Phó Giám đốc sang pho-giam-doc ============
        var main = new[] { w.Tp, w.Ptp, w.Ct, w.Gd, w.Ksv, w.Pgd };
        Assert.Equal(6, (await Data(await w.Tccb.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = main.Select(p => p.Id) }))).GetProperty("added").GetInt32());
        Assert.Equal(1, (await Data(await w.Tccb.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/participants",
            new { memberIds = new[] { w.Bt.Id }, workflowProfileCode = "bi-thu-nhan-vien" }))).GetProperty("added").GetInt32());

        var participants = await Participants(w, periodId);
        Assert.Equal("co-so", participants[w.Tp.Id].GetProperty("workflowProfileCode").GetString());
        Assert.Equal("co-so", participants[w.Ptp.Id].GetProperty("workflowProfileCode").GetString());
        Assert.Equal("bi-thu-nhan-vien", participants[w.Bt.Id].GetProperty("workflowProfileCode").GetString());
        foreach (var leader in new[] { w.Ct, w.Gd, w.Ksv, w.Pgd })
        {
            Assert.Equal("CapTren", participants[leader.Id].GetProperty("approvalAuthority").GetString());
            Assert.Equal("cap-tren", participants[leader.Id].GetProperty("workflowProfileCode").GetString());
        }
        var pgdRow = participants[w.Pgd.Id];
        await Data(await w.Tccb.PutAsJsonAsync($"/api/evaluations/periods/{periodId}/participants/{pgdRow.GetProperty("recordId").GetGuid()}/profile",
            new { version = pgdRow.GetProperty("version").GetUInt32(), workflowProfileCode = "pho-giam-doc", reason = "Phó Giám đốc: Chủ tịch nhận xét, BTV quyết định" }));
        participants = await Participants(w, periodId);
        Assert.Equal("pho-giam-doc", participants[w.Pgd.Id].GetProperty("workflowProfileCode").GetString());
        var id = participants.ToDictionary(p => p.Key, p => p.Value.GetProperty("recordId").GetGuid());

        // ============ 3. Kiểm tra kẹt luồng → sẵn sàng; mở kỳ không cần mở bắt buộc ============
        var readiness = await Data(w.Tccb, $"/api/evaluations/periods/{periodId}/readiness");
        Assert.True(readiness.GetProperty("ready").GetBoolean(), readiness.GetRawText());
        Assert.Equal(7, readiness.GetProperty("checkedRecords").GetInt32());
        var draft = await Data(w.Tccb, $"/api/evaluations/periods/{periodId}");
        var opened = await Data(await w.Tccb.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/open",
            new { version = draft.GetProperty("version").GetUInt32() }));
        Assert.Equal("Open", opened.GetProperty("status").GetString());
        Assert.Equal(4, opened.GetProperty("quarter").GetInt32());

        // Hồ sơ của tôi và việc cần xử lý: bước đầu là tự chấm (không đăng ký việc).
        foreach (var p in w.All)
        {
            var mine = await Data(p.Client, "/api/evaluations/my-record");
            Assert.Equal(id[p.Id], mine.GetProperty("id").GetGuid());
            Assert.Equal("AwaitingSelfScore", mine.GetProperty("status").GetString());
        }
        var forms = (await Data(w.Tp.Client, $"/api/reports/docx/record/{id[w.Tp.Id]}")).EnumerateArray().Select(f => f.GetProperty("code").GetString()).ToList();
        Assert.Equal(new[] { "09B", "09C", "9D", "10" }, forms);
        Assert.Equal("2026-12-09", participants[w.Tp.Id].GetProperty("currentStepDeadline").GetString());

        // ============ 4. Bước 2: tự chấm 09B (+09C, 9D) → Chi bộ xác nhận ============
        foreach (var p in w.All)
            await Step(p.Client, id[p.Id], "self-score/submit", SelfScoreBody(p), "AwaitingCellConfirm");

        // Bí thư Chi bộ không tự xác nhận phiếu của mình (xung đột lợi ích); Chi bộ khác không xác nhận được.
        var selfConfirm = await Post(w.Bt.Client, id[w.Bt.Id], "cell/confirm", new { comment = "Tự xác nhận" }, w.Tccb);
        Assert.Equal(HttpStatusCode.Forbidden, selfConfirm.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await Post(w.CellVp, id[w.Tp.Id], "cell/confirm", new { comment = "Sai Chi bộ" }, w.Tccb)).StatusCode);
        await Step(w.Bt.Client, id[w.Tp.Id], "cell/confirm", new { comment = "Chi bộ Kỹ thuật xác nhận" }, "AwaitingCollective");
        await Step(w.CellKtDeputy, id[w.Bt.Id], "cell/confirm", new { comment = "Phó Bí thư xác nhận" }, "AwaitingCollective");
        foreach (var p in new[] { w.Ptp, w.Ct, w.Gd, w.Ksv, w.Pgd })
            await Step(w.CellVp, id[p.Id], "cell/confirm", new { comment = "Chi bộ Văn phòng xác nhận" }, "AwaitingCollective");

        // ============ 5. Bước 3a: tập thể lãnh đạo bỏ phiếu kín (biên bản kiểm phiếu Mẫu 13) ============
        var meetingKt = await CreateBallotMeetingAsync(w.SecKt, periodId, w.DeptKt, null, "B3A_COLLECTIVE", "Hội nghị tập thể lãnh đạo Phòng Kỹ thuật", 5, (id[w.Tp.Id], 4, 1), (id[w.Bt.Id], 0, 5));
        var meetingTc = await CreateBallotMeetingAsync(w.SecTc, periodId, w.DeptTc, null, "B3A_COLLECTIVE", "Hội nghị tập thể lãnh đạo Phòng TCCB-LĐ", 4, (id[w.Ptp.Id], 0, 4));
        var meetingBgd = await CreateBallotMeetingAsync(w.SecBgd, periodId, w.DeptBgd, null, "B3A_COLLECTIVE", "Hội nghị tập thể lãnh đạo Công ty", 7, (id[w.Ct.Id], 1, 6), (id[w.Gd.Id], 1, 6), (id[w.Ksv.Id], 1, 6), (id[w.Pgd.Id], 0, 7));
        // Thư ký Phòng khác không ghi nhận được đề xuất của tập thể Phòng Kỹ thuật.
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(w.SecTc, id[w.Tp.Id], "collective",
            new { proposedGrade = "HoanThanhTot" }, w.Tccb)).StatusCode);
        // Biên bản của Phòng khác → 400, thông báo rõ.
        var wrongMeeting = await Post(w.SecKt, id[w.Tp.Id], "collective",
            new { proposedGrade = "HoanThanhTot", meetingId = meetingTc, votes = Votes(4, 1) }, w.Tccb);
        Assert.Equal(HttpStatusCode.BadRequest, wrongMeeting.StatusCode);
        Assert.Contains("đơn vị/tổ chức Đảng của biên bản", await Message(wrongMeeting));

        await Step(w.SecKt, id[w.Tp.Id], "collective",
            new { proposedGrade = "HoanThanhXuatSac", meetingId = meetingKt, votes = Votes(4, 1) }, "AwaitingAppraisal");
        await Step(w.SecKt, id[w.Bt.Id], "collective",
            new { proposedGrade = "HoanThanhTot", meetingId = meetingKt, votes = Votes(0, 5) }, "AwaitingAppraisal");
        await Step(w.SecTc, id[w.Ptp.Id], "collective",
            new { proposedGrade = "HoanThanhTot", meetingId = meetingTc, votes = Votes(0, 4) }, "AwaitingAppraisal");
        // Diện cấp trên: B3b do Ban TCĐU thực hiện → chờ ghi nhận thẩm định; Phó Giám đốc không có thẩm định → chờ Chủ tịch.
        foreach (var p in new[] { w.Ct, w.Gd, w.Ksv })
            await Step(w.SecBgd, id[p.Id], "collective",
                new { proposedGrade = "HoanThanhTot", meetingId = meetingBgd, votes = Votes(1, 6) }, "AwaitingAppraisal");
        await Step(w.SecBgd, id[w.Pgd.Id], "collective",
            new { proposedGrade = "HoanThanhTot", meetingId = meetingBgd, votes = Votes(0, 7) }, "AwaitingDirectorReview");

        // ============ 6. Bước 3b: Phòng TCCB-LĐ thẩm định (co-so, bi-thu-nhan-vien) ============
        // Cơ quan thẩm định không thẩm định hồ sơ diện cấp trên (cấp trên thực hiện) → 409.
        var upper = await Post(w.Tccb, id[w.Gd.Id], "appraisal",
            new { generalScore = 30.0, tasksScore = 60.0, comment = "x", proposedGrade = "HoanThanhTot", explanation = "x" }, w.Tccb);
        Assert.Equal(HttpStatusCode.Conflict, upper.StatusCode);
        foreach (var (p, grade) in new[] { (w.Tp, "HoanThanhXuatSac"), (w.Bt, "HoanThanhTot"), (w.Ptp, "HoanThanhTot") })
            await Step(w.Tccb, id[p.Id], "appraisal", new
            {
                generalScore = 30.0, tasksScore = 60.0, comment = "Đủ minh chứng", proposedGrade = grade,
                explanation = "Căn cứ thẩm định theo minh chứng nộp kèm phiếu"
            }, "AwaitingDirectorReview");

        // ============ 7. Bước 3c: cấp trực tiếp ============
        // Trưởng phòng, Phó trưởng phòng → Giám đốc; Bí thư Chi bộ là nhân viên → Trưởng phòng; Phó Giám đốc → Chủ tịch.
        await AssertActions(w.Gd.Client, id[w.Bt.Id]);
        await AssertActions(w.Gd.Client, id[w.Pgd.Id]);
        await AssertActions(w.Ct.Client, id[w.Tp.Id]);
        await AssertActions(w.Tp.Client, id[w.Ptp.Id]);
        await AssertActions(w.Gd.Client, id[w.Tp.Id], "DirectorReview");
        await AssertActions(w.Tp.Client, id[w.Bt.Id], "DirectorReview");
        await AssertActions(w.Ct.Client, id[w.Pgd.Id], "DirectorReview");
        var gdQueue = await Data(w.Gd.Client, $"/api/evaluations/work-queue?periodId={periodId}");
        var gdB3c = gdQueue.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("step").GetString() == "B3C_DIRECTOR");
        Assert.Equal(2, gdB3c.GetProperty("items").GetArrayLength());

        await Step(w.Gd.Client, id[w.Tp.Id], "director-review", new { comment = "Giám đốc nhận xét: hoàn thành xuất sắc", proposedGrade = "HoanThanhXuatSac" }, "AwaitingDecision");
        await Step(w.Gd.Client, id[w.Ptp.Id], "director-review", new { comment = "Giám đốc nhận xét: hoàn thành tốt", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");
        await Step(w.Tp.Client, id[w.Bt.Id], "director-review", new { comment = "Trưởng phòng đề xuất hoàn thành tốt", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");
        await Step(w.Ct.Client, id[w.Pgd.Id], "director-review", new { comment = "Chủ tịch nhận xét, đề xuất BTV hoàn thành tốt", proposedGrade = "HoanThanhTot" }, "AwaitingDecision");

        // Diện cấp trên: Văn phòng ghi nhận thẩm định (Ban TCĐU), nhận xét (HĐTV).
        foreach (var p in new[] { w.Ct, w.Gd, w.Ksv })
        {
            await Step(w.Office, id[p.Id], "external/B3B_APPRAISAL", new
            {
                authorityName = "Ban Tổ chức Đảng ủy Tổng công ty", documentNumber = "15-TTr/BTCĐU", documentDate = "2026-12-15",
                comment = "Thẩm định đủ điều kiện", grade = "HoanThanhTot", score = 90.0
            }, "AwaitingDirectorReview");
            await Step(w.Office, id[p.Id], "external/B3C_DIRECTOR",
                new { authorityName = "Hội đồng thành viên Tổng công ty", comment = "Đề nghị hoàn thành tốt", grade = "HoanThanhTot" }, "AwaitingDecision");
        }

        // ============ 8. Bước 4: Đảng ủy quyết định (bỏ phiếu) / ghi nhận quyết định của BTV Đảng ủy Tổng công ty ============
        var decisionMeeting = await CreateBallotMeetingAsync(w.Office, periodId, null, null, "B4_DECISION", "Hội nghị Đảng ủy Công ty", 9, (id[w.Tp.Id], 8, 1), (id[w.Ptp.Id], 0, 9), (id[w.Bt.Id], 0, 9));
        // Hồ sơ diện cấp trên: Văn phòng không quyết định nội bộ được (409); hồ sơ cơ sở: không ghi nhận cấp trên được (409).
        Assert.Equal(HttpStatusCode.Conflict, (await Post(w.Office, id[w.Gd.Id], "decision", new { finalGrade = "HoanThanhTot" }, w.Tccb)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(w.Office, id[w.Tp.Id], "external/B4_DECISION",
            new { authorityName = "BTV", grade = "HoanThanhTot" }, w.Tccb)).StatusCode);
        foreach (var (p, grade, votes) in new[] { (w.Tp, "HoanThanhXuatSac", Votes(8, 1)), (w.Ptp, "HoanThanhTot", Votes(0, 9)), (w.Bt, "HoanThanhTot", Votes(0, 9)) })
            await Step(w.Office, id[p.Id], "decision", new
            {
                finalGrade = grade, documentNumber = "45-QĐ/ĐU", documentDate = "2026-12-14", meetingId = decisionMeeting, votes
            }, "AwaitingPublish");
        foreach (var (p, grade) in new[] { (w.Ct, "HoanThanhTot"), (w.Gd, "HoanThanhXuatSac"), (w.Ksv, "HoanThanhTot"), (w.Pgd, "HoanThanhTot") })
            await Step(w.Office, id[p.Id], "external/B4_DECISION", new
            {
                authorityName = "Ban Thường vụ Đảng ủy Tổng công ty", documentNumber = "120-QĐ/ĐUTCT", documentDate = "2026-12-18", grade
            }, "AwaitingPublish");

        // ============ 9. Bước 5: công bố, đóng kỳ ============
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(w.Tccb, id[w.Tp.Id], "publish", new { }, w.Tccb)).StatusCode);
        foreach (var p in w.All)
            await Step(w.Office, id[p.Id], "publish", new { }, "Published");

        var final = (await Participants(w, periodId)).ToDictionary(p => p.Key, p => p.Value);
        Assert.All(final.Values, p => Assert.Equal("Published", p.GetProperty("status").GetString()));
        var record = await Data(w.Pgd.Client, $"/api/evaluations/records/{id[w.Pgd.Id]}");
        Assert.Equal("HoanThanhTot", record.GetProperty("finalGrade").GetString());
        Assert.Equal("Ban Thường vụ Đảng ủy Tổng công ty", record.GetProperty("decisionAuthorityName").GetString());
        var history = await Data(w.Pgd.Client, $"/api/evaluations/records/{id[w.Pgd.Id]}/history");
        var steps = history.EnumerateArray().Where(h => h.GetProperty("step").ValueKind == JsonValueKind.String
                && h.GetProperty("action").GetString() is "Complete" or "RecordExternal")
            .Select(h => h.GetProperty("step").GetString()).ToList();
        Assert.Equal(new[] { "B2_SELF_SCORE", "B2_CELL_CONFIRM", "B3A_COLLECTIVE", "B3C_DIRECTOR", "B4_DECISION", "B5_PUBLISH" },
            steps.OrderBy(StepOrder).ToList());

        // Công khai kết quả: người được đánh giá xem được mức của kỳ.
        var results = (await Data(w.Ptp.Client, $"/api/results?periodId={periodId}")).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(7, results.Count);

        // ============ 10. Xuất biểu mẫu (trước khi đóng kỳ và sau khi đóng kỳ đều phải được) ============
        var period = await Data(w.Tccb, $"/api/evaluations/periods/{periodId}");
        var closed = await Data(await w.Tccb.PostAsJsonAsync($"/api/evaluations/periods/{periodId}/close",
            new { version = period.GetProperty("version").GetUInt32() }));
        Assert.Equal("Closed", closed.GetProperty("status").GetString());

        var printed = new List<(string Name, string Text)>();

        // Biểu mẫu cá nhân của từng hồ sơ: 09B, 09C, 9D, 10.
        foreach (var p in w.All)
        {
            foreach (var code in new[] { "09B", "09C", "9D", "10" })
            {
                var doc = await Docx(w.Office, $"/api/reports/docx/record/{id[p.Id]}/{code}");
                Assert.Contains(p.Name, doc.Text);
                Assert.DoesNotContain("Quy III", doc.FileName, StringComparison.OrdinalIgnoreCase);
                printed.Add(($"{code} {p.Name}", doc.Text));
            }
        }
        var tp09b = printed.Single(x => x.Name == $"09B {w.Tp.Name}").Text;
        Assert.Contains("PHIẾU TỰ CHẤM ĐIỂM", tp09b);
        Assert.Contains($"Mục tiêu trục 1 của {w.Tp.Name}", tp09b);
        Assert.Contains("Quý IV Năm 2026", tp09b);
        Assert.Contains("Chi bộ: Kỹ thuật", tp09b); // không lặp "Chi bộ: Chi bộ …" (như phiếu đã nộp quý III)
        Assert.Contains("CHI BỘ KỸ THUẬT", tp09b);
        var tp09c = printed.Single(x => x.Name == $"09C {w.Tp.Name}").Text;
        Assert.Contains($"Tự đánh giá Quý IV của {w.Tp.Name}", tp09c);
        Assert.Contains("Hoàn thành tốt nhiệm vụ", tp09c);
        var tp9d = printed.Single(x => x.Name == $"9D {w.Tp.Name}").Text;
        Assert.Contains($"Sản phẩm trục 1 của {w.Tp.Name}", tp9d);
        Assert.Contains("TRONG QUÝ IV NĂM 2026", tp9d);
        Assert.Contains("Chi bộ, Đảng bộ đang sinh hoạt: Kỹ thuật", tp9d);
        var tp10 = printed.Single(x => x.Name == $"10 {w.Tp.Name}").Text;
        Assert.Contains("Giám đốc nhận xét: hoàn thành xuất sắc", tp10);
        var pgd10 = printed.Single(x => x.Name == $"10 {w.Pgd.Name}").Text;
        Assert.Contains("Chủ tịch nhận xét, đề xuất BTV hoàn thành tốt", pgd10);

        // Mẫu 11 (phiếu bỏ phiếu), 12 (biên bản hội nghị), 13 (biên bản kiểm phiếu), 16 (báo cáo).
        var m11 = await Docx(w.Office, $"/api/reports/docx/mau-11?periodId={periodId}");
        printed.Add(("11", m11.Text));
        Assert.Contains("PHIẾU ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ QUÝ IV NĂM 2026", m11.Text);
        foreach (var p in w.All)
            Assert.Contains(p.Name, m11.Text);
        var minutes = await CreateMinutesAsync(w.Office, periodId);
        var m12 = await Docx(w.Office, $"/api/reports/docx/mau-12/{minutes}");
        printed.Add(("12", m12.Text));
        foreach (var (name, meeting, client) in new[] { ("13 Phòng KT", meetingKt, w.SecKt), ("13 Công ty", meetingBgd, w.SecBgd), ("13 Đảng ủy", decisionMeeting, w.Office) })
        {
            var doc = await Docx(client, $"/api/reports/docx/mau-13/{meeting}");
            printed.Add((name, doc.Text));
        }
        var m13Company = printed.Single(x => x.Name == "13 Công ty").Text;
        foreach (var p in new[] { w.Ct, w.Gd, w.Ksv, w.Pgd })
            Assert.Contains(p.Name, m13Company);
        var m13Party = printed.Single(x => x.Name == "13 Đảng ủy").Text;
        foreach (var p in new[] { w.Tp, w.Ptp, w.Bt })
            Assert.Contains(p.Name, m13Party);
        var m16 = await Docx(w.Office, $"/api/reports/docx/mau-16?periodId={periodId}");
        printed.Add(("16", m16.Text));

        // Mẫu 14, 15A, 15B (Excel).
        var m14 = await Workbook(w.Office, $"/api/reports/form-14?periodId={periodId}");
        var m15a = await Workbook(w.Office, $"/api/reports/form-15a?periodId={periodId}");
        var m15b = await Workbook(w.Office, $"/api/reports/form-15b?periodId={periodId}");
        printed.Add(("14", m14.Text));
        printed.Add(("15A", m15a.Text));
        printed.Add(("15B", m15b.Text));
        foreach (var name in new[] { w.Tp.Name, w.Ptp.Name, w.Bt.Name })
            Assert.Contains(name, m14.Sheets[0]);
        foreach (var name in new[] { w.Ct.Name, w.Gd.Name, w.Ksv.Name, w.Pgd.Name })
            Assert.Contains(name, m14.Sheets[1]);
        Assert.DoesNotContain(w.Tp.Name, m14.Sheets[1]);
        Assert.DoesNotContain(w.Gd.Name, m14.Sheets[0]);
        using (var book15a = new XLWorkbook(new MemoryStream(m15a.Bytes)))
        {
            Assert.Equal(new[] { 3, 1, 2 }, Counts(book15a.Worksheet(1), "M14").Take(3).ToArray()); // Chủ tịch, Giám đốc, Phó Giám đốc
            Assert.Equal(new[] { 1, 0, 1 }, Counts(book15a.Worksheet(1), "M15").Take(3).ToArray()); // Kiểm soát viên
        }
        using (var book15b = new XLWorkbook(new MemoryStream(m15b.Bytes)))
        {
            Assert.Equal(new[] { 2, 1, 1 }, Counts(book15b.Worksheet(1), "M26").Take(3).ToArray()); // Trưởng phòng (XS), Phó trưởng phòng (Tốt)
            Assert.Equal(new[] { 1, 0, 1 }, Counts(book15b.Worksheet(1), "M22").Take(3).ToArray()); // Bí thư Chi bộ
        }

        // Mọi biểu mẫu: quý IV, năm 2026 của kỳ; không còn "quý III" ghi cứng; tên tổ chức theo cài đặt.
        var problems = new List<string>();
        foreach (var (name, text) in printed)
        {
            var bad = HardcodedQuarter3.Match(text);
            if (bad.Success)
                problems.Add($"Mẫu {name}: còn \"{Around(text, bad.Index)}\"");
            if (!Quarter4.IsMatch(text))
                problems.Add($"Mẫu {name}: không in quý IV/2026");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));

        foreach (var (name, text) in printed.Where(x => !x.Name.StartsWith("15", StringComparison.Ordinal) && x.Name != "14"))
        {
            Assert.True(text.Contains(PartyCommittee, StringComparison.OrdinalIgnoreCase)
                || text.Contains(Company, StringComparison.OrdinalIgnoreCase)
                || text.Contains(ParentCompany, StringComparison.OrdinalIgnoreCase), $"Mẫu {name}: thiếu tên đơn vị từ cài đặt");
            // Tiêu đề biểu mẫu không còn tên đơn vị ghi cứng (nội dung gợi ý của bộ tiêu chí và câu "Căn cứ Hướng dẫn…" của
            // Mẫu 12/13 trích văn bản cấp trên nên không xét).
            Assert.DoesNotContain("QUẢN LÝ BAY VIỆT NAM", text);
            Assert.DoesNotContain("KỸ THUẬT QUẢN LÝ BAY", text);
        }
    }

    #region Dữ liệu

    private static object SelfScoreBody(Person p) => new
    {
        generalScores = CriteriaTestData.General(),
        axisScores = CriteriaTestData.Axis(14, 9, 9, 14, 9, 9),
        selfProposedGrade = "HoanThanhTot",
        selfAssessment = new Dictionary<string, string> { ["I"] = $"Tự đánh giá Quý IV của {p.Name}" },
        axisReports = new Dictionary<string, object>
        {
            ["T1"] = new
            {
                tasks = new object[] { new { content = $"Nhiệm vụ trục 1 của {p.Name}", level = 1 } },
                deadline = "31/12/2026", status = "- Đã hoàn thành", product = $"Sản phẩm trục 1 của {p.Name}", progress = "Đúng hạn"
            },
            ["T4"] = new { tasks = new[] { new { content = $"Công tác Đảng của {p.Name}" } }, product = "Sinh hoạt chi bộ đầy đủ" }
        },
        axisNotes = new Dictionary<string, object> { ["T1"] = new { target = $"Mục tiêu trục 1 của {p.Name}", result = "Đạt" } },
        generalGroupNotes = new Dictionary<string, string> { ["1"] = "Chấp hành tốt" }
    };

    private static object Votes(int excellent, int good) => new
    {
        votesExcellent = excellent, votesGood = good, votesSatisfactory = 0, votesUnsatisfactory = 0, votesNotRated = 0, invalidVotes = 0
    };

    private static async Task<Guid> CreateBallotMeetingAsync(HttpClient client, Guid periodId, Guid? departmentId, Guid? partyCellId,
        string stage, string meetingType, int present, params (Guid RecordId, int Excellent, int Good)[] ballots)
    {
        var meeting = await Data(await client.PostAsJsonAsync("/api/evaluations/meetings", new
        {
            voteSummaries = ballots.Select(b => new
            {
                recordId = b.RecordId, votesExcellent = b.Excellent, votesGood = b.Good, votesSatisfactory = 0, votesUnsatisfactory = 0,
                votesNotRated = 0, invalidVotes = 0, notes = ""
            }),
            periodId, departmentId, partyCellId, stage, formCode = "M13", meetingType, location = "Phòng họp tầng 3",
            startedAt = new DateTime(2026, 12, 10, 1, 0, 0, DateTimeKind.Utc), endedAt = new DateTime(2026, 12, 10, 3, 0, 0, DateTimeKind.Utc),
            invitedCount = present, presentCount = present, absentCount = 0,
            chairName = "Chủ trì hội nghị", secretaryName = "Thư ký hội nghị",
            details = new
            {
                chairTitle = "Chủ trì", secretaryTitle = "Thư ký",
                countingCommittee = new[] { new { name = "Tổ trưởng kiểm phiếu", title = "Ủy viên" } },
                ballotsIssued = present, ballotsCollected = present, ballotsValid = present, ballotsInvalid = 0
            }
        }));
        return meeting.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateMinutesAsync(HttpClient client, Guid periodId)
    {
        var meeting = await Data(await client.PostAsJsonAsync("/api/evaluations/meetings", new
        {
            periodId, stage = "B4_DECISION", formCode = "M12", meetingType = "Hội nghị Đảng ủy Công ty", location = "Hội trường",
            startedAt = new DateTime(2026, 12, 14, 1, 0, 0, DateTimeKind.Utc), endedAt = new DateTime(2026, 12, 14, 4, 0, 0, DateTimeKind.Utc),
            invitedCount = 9, presentCount = 9, absentCount = 0, chairName = "Chủ trì hội nghị", secretaryName = "Thư ký hội nghị",
            outcomeContent = "Hội nghị thống nhất kết quả",
            details = new { chairTitle = "Bí thư Đảng ủy", secretaryTitle = "Đảng ủy viên" }
        }));
        return meeting.GetProperty("id").GetGuid();
    }

    private static JsonObject Profile(JsonObject settings, string code) =>
        settings["profiles"]!.AsArray().Select(p => p!.AsObject()).Single(p => p["code"]!.GetValue<string>() == code);

    private static void SetDeadlines(JsonObject settings, string profile, params (string Step, int Day)[] deadlines)
    {
        var steps = Profile(settings, profile)["steps"]!;
        foreach (var (step, day) in deadlines)
            steps[step]!["deadline"] = $"2026-12-{day:00}";
    }

    #endregion

    #region Hỗ trợ

    private static int StepOrder(string? step) => Array.IndexOf(
        new[] { "B1_REGISTER", "B1_APPROVE", "B2_SELF_SCORE", "B2_CELL_CONFIRM", "B3A_COLLECTIVE", "B3B_APPRAISAL", "B3C_DIRECTOR", "B4_DECISION", "B5_PUBLISH" },
        step);

    private static string Around(string text, int index)
    {
        var start = Math.Max(0, index - 40);
        return text.Substring(start, Math.Min(text.Length - start, 90)).Replace('\n', ' ');
    }

    /// <summary>Tổng số và các mức (cột 4–9) của dòng có mã chức danh.</summary>
    private static int[] Counts(IXLWorksheet sheet, string code)
    {
        var row = sheet.RowsUsed().Single(r => r.Cell(3).GetString() == code);
        return Enumerable.Range(4, 6).Select(c => row.Cell(c).GetValue<int>()).ToArray();
    }

    private static async Task<Dictionary<Guid, JsonElement>> Participants(World w, Guid periodId) =>
        (await Data(w.Tccb, $"/api/evaluations/periods/{periodId}/participants")).EnumerateArray()
            .ToDictionary(p => p.GetProperty("memberId").GetGuid(), p => p);

    private static async Task<HttpResponseMessage> Post(HttpClient client, Guid recordId, string action, object body, HttpClient reader)
    {
        var version = (await Data(reader, $"/api/evaluations/records/{recordId}")).GetProperty("version").GetUInt32();
        var json = JsonSerializer.SerializeToNode(body)!.AsObject();
        json["version"] = version;
        return await client.PostAsJsonAsync($"/api/evaluations/records/{recordId}/{action}", json);
    }

    private static async Task<JsonElement> Step(HttpClient client, Guid recordId, string action, object body, string expectedStatus)
    {
        var result = await Data(await Post(client, recordId, action, body, client));
        Assert.Equal(expectedStatus, result.GetProperty("status").GetString());
        return result;
    }

    private static async Task AssertActions(HttpClient client, Guid recordId, params string[] expected)
    {
        var response = await client.GetAsync($"/api/evaluations/records/{recordId}/actions");
        if (expected.Length == 0 && response.StatusCode == HttpStatusCode.Forbidden)
            return; // không xem được hồ sơ thì cũng không có thao tác
        var data = await Data(response);
        var actions = data.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("action").GetString()!).OrderBy(x => x).ToList();
        Assert.Equal(expected.OrderBy(x => x).ToList(), actions);
    }

    private sealed record DocxResult(string Text, string FileName);

    private static async Task<DocxResult> Docx(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {url}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(DocxType, response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Empty(new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019).Validate(doc));
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
        var text = string.Join("\n", body.Descendants<Paragraph>().Select(p => string.Concat(p.Descendants().Select(e => e switch
        {
            Text t => t.Text,
            TabChar => "\t",
            Break => "\n",
            _ => string.Empty
        }))));
        Assert.DoesNotContain("«", text);
        return new DocxResult(text, response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? string.Empty);
    }

    private sealed record WorkbookResult(string Text, List<string> Sheets, byte[] Bytes);

    private static async Task<WorkbookResult> Workbook(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode} {url}: {await response.Content.ReadAsStringAsync()}");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var book = new XLWorkbook(new MemoryStream(bytes));
        var sheets = book.Worksheets.Select(s => string.Join("\n", s.CellsUsed().Select(c => c.GetString()))).ToList();
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName ?? string.Empty;
        Assert.DoesNotContain("Quy III", fileName, StringComparison.OrdinalIgnoreCase);
        return new WorkbookResult(string.Join("\n", sheets), sheets, bytes);
    }

    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<JsonElement> Data(HttpClient client, string url) => await Data(await client.GetAsync(url));

    private static async Task<string> Message(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("message").GetString() ?? string.Empty;
    }

    #endregion

    /// <summary>Một cán bộ được đánh giá (tên giả) kèm phiên đăng nhập.</summary>
    private sealed record Person(Guid Id, string Name, HttpClient Client);

    /// <summary>
    /// Tổ chức (Công ty → Ban Giám đốc, Phòng Kỹ thuật, Phòng TCCB-LĐ; Đảng ủy → Chi bộ Kỹ thuật, Chi bộ Văn phòng), 7 cán bộ được
    /// đánh giá và người thực hiện các bước. Đơn vị, chức vụ, thông tin đơn vị và vai trò khai báo qua API của trang quản trị.
    /// </summary>
    private sealed class World
    {
        public Person Tp = null!, Ptp = null!, Bt = null!, Ct = null!, Gd = null!, Ksv = null!, Pgd = null!;
        public HttpClient Admin = null!, Tccb = null!, CellKtDeputy = null!, CellVp = null!, SecKt = null!, SecTc = null!, SecBgd = null!, Office = null!;
        public Guid DeptKt, DeptTc, DeptBgd, CellKt, CellVpId;

        public IEnumerable<Person> All => new[] { Tp, Ptp, Bt, Ct, Gd, Ksv, Pgd };

        public static async Task<World> CreateAsync(ApiFactory factory)
        {
            var w = new World();
            var adminUser = await factory.CreateUserAsync(fullName: "Quản trị hệ thống");
            await factory.AssignAsync(adminUser.Id, await factory.GetAdministratorRoleIdAsync(), RoleScopeType.Global, null);
            w.Admin = await factory.LoginAsAsync(adminUser.Username, adminUser.Password, distinctClientIp: true);

            // Thông tin đơn vị (tên giả).
            await Data(await w.Admin.PutAsJsonAsync("/api/settings/organization", new
            {
                partyCommitteeName = PartyCommittee, superiorPartyName = SuperiorParty, companyName = Company,
                parentCompanyName = ParentCompany, shortName = "CTTN4", location = "Hà Nội", systemName = "Đánh giá cán bộ"
            }));

            // Danh mục đơn vị và tổ chức Đảng.
            var types = (await Data(w.Admin, "/api/organizations/unit-types")).EnumerateArray()
                .ToDictionary(t => $"{t.GetProperty("side").GetString()}:{t.GetProperty("name").GetString()}", t => t.GetProperty("id").GetGuid());
            var company = await GoLiveHttp.CreateCatalogItemAsync(w.Admin, "departments", "CT", Company, 0, null, types["Administrative:Công ty"]);
            w.DeptBgd = await GoLiveHttp.CreateCatalogItemAsync(w.Admin, "departments", "BGD", "Ban Giám đốc", 1, company, types["Administrative:Đơn vị"]);
            w.DeptKt = await GoLiveHttp.CreateCatalogItemAsync(w.Admin, "departments", "PKT", "Phòng Kỹ thuật", 2, company, types["Administrative:Phòng"]);
            w.DeptTc = await GoLiveHttp.CreateCatalogItemAsync(w.Admin, "departments", "PTC", "Phòng Tổ chức cán bộ - Lao động", 3, company, types["Administrative:Phòng"]);
            var committee = await GoLiveHttp.CreateCatalogItemAsync(w.Admin, "branches", "DU", "Đảng ủy Công ty", 0, null, types["Party:Đảng ủy"]);
            w.CellKt = await GoLiveHttp.CreateCatalogItemAsync(w.Admin, "branches", "CBKT", "Chi bộ Kỹ thuật", 1, committee, types["Party:Chi bộ"]);
            w.CellVpId = await GoLiveHttp.CreateCatalogItemAsync(w.Admin, "branches", "CBVP", "Chi bộ Văn phòng", 2, committee, types["Party:Chi bộ"]);

            var positions = (await Data(w.Admin, "/api/positions")).EnumerateArray()
                .ToDictionary(p => p.GetProperty("name").GetString()!, p => p.GetProperty("id").GetGuid());

            async Task<(TestUser User, HttpClient Client)> Account(string name, Guid? dept, Guid? cell, ApprovalAuthority authority = ApprovalAuthority.CoSo)
            {
                var user = await factory.CreateUserAsync(dept, cell, authority, name);
                return (user, await factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true));
            }

            async Task<Person> Cadre(string name, Guid dept, Guid cell, ApprovalAuthority authority, string position, Guid? positionDept, Guid? positionCell)
            {
                var (user, client) = await Account(name, dept, cell, authority);
                await Data(await w.Admin.PostAsJsonAsync($"/api/users/{user.Id}/positions", new
                {
                    positionId = positions[position], departmentId = positionDept, partyCellId = positionCell, isPrimary = true,
                    validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                }));
                return new Person(user.Id, name, client);
            }

            w.Tp = await Cadre("Đỗ Minh Trưởng", w.DeptKt, w.CellKt, ApprovalAuthority.CoSo, "Trưởng phòng", w.DeptKt, null);
            w.Ptp = await Cadre("Vũ Thị Phó", w.DeptTc, w.CellVpId, ApprovalAuthority.CoSo, "Phó Trưởng phòng", w.DeptTc, null);
            w.Bt = await Cadre("Lương Văn Bí", w.DeptKt, w.CellKt, ApprovalAuthority.CoSo, "Bí thư Chi bộ", null, w.CellKt);
            w.Ct = await Cadre("Hoàng Văn Chủ", w.DeptBgd, w.CellVpId, ApprovalAuthority.CapTren, "Chủ tịch Công ty", w.DeptBgd, null);
            w.Gd = await Cadre("Trịnh Văn Giám", w.DeptBgd, w.CellVpId, ApprovalAuthority.CapTren, "Giám đốc", w.DeptBgd, null);
            w.Ksv = await Cadre("Mai Thị Soát", w.DeptBgd, w.CellVpId, ApprovalAuthority.CapTren, "Kiểm soát viên", w.DeptBgd, null);
            w.Pgd = await Cadre("Tạ Văn Phó", w.DeptBgd, w.CellVpId, ApprovalAuthority.CapTren, "Phó Giám đốc", w.DeptBgd, null);

            var tccb = await Account("Ngô Thị Thẩm", w.DeptTc, null);
            var cellKtDeputy = await Account("Kiều Văn Phó Bí", w.DeptKt, w.CellKt);
            var cellVp = await Account("Phan Thị Chi Ủy", w.DeptTc, w.CellVpId);
            var secKt = await Account("Thư ký Phòng Kỹ thuật", w.DeptKt, null);
            var secTc = await Account("Thư ký Phòng TCCB-LĐ", w.DeptTc, null);
            var secBgd = await Account("Thư ký Công ty", w.DeptBgd, null);
            var office = await Account("Chuyên viên Văn phòng Đảng ủy", w.DeptTc, null);

            // Giao người thực hiện qua API gán vai trò (theo vai trò mặc định, phạm vi đơn vị bao trùm đơn vị con).
            var roles = (await Data(w.Admin, "/api/admin/roles")).EnumerateArray()
                .ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("id").GetGuid());
            async Task Assign(Guid user, string role, string scopeType, Guid? scopeId) =>
                await GoLiveHttp.AssignAsync(w.Admin, user, roles[role], scopeType, scopeId, note: "Kế hoạch đánh giá Quý IV/2026");

            foreach (var p in w.All)
                await Assign(p.Id, "Người được đánh giá", "Global", null);
            await Assign(tccb.User.Id, "Phòng TCCB-LĐ (mở kỳ, thẩm định)", "Global", null);
            await Assign(w.Bt.Id, "Chi ủy / Bí thư Chi bộ", "PartyCell", w.CellKt);
            await Assign(cellKtDeputy.User.Id, "Chi ủy / Bí thư Chi bộ", "PartyCell", w.CellKt);
            await Assign(cellVp.User.Id, "Chi ủy / Bí thư Chi bộ", "PartyCell", w.CellVpId);
            await Assign(secKt.User.Id, "Thư ký tập thể lãnh đạo", "Department", w.DeptKt);
            await Assign(secTc.User.Id, "Thư ký tập thể lãnh đạo", "Department", w.DeptTc);
            await Assign(secBgd.User.Id, "Thư ký tập thể lãnh đạo", "Department", w.DeptBgd);
            // Cấp trực tiếp: Giám đốc cho các Phòng; Chủ tịch cho Ban Giám đốc (Phó Giám đốc); Trưởng phòng (Lãnh đạo Phòng).
            await Assign(w.Gd.Id, "Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)", "Department", w.DeptKt);
            await Assign(w.Gd.Id, "Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)", "Department", w.DeptTc);
            await Assign(w.Ct.Id, "Cấp trực tiếp sử dụng (Giám đốc/Chủ tịch)", "Department", w.DeptBgd);
            await Assign(w.Tp.Id, "Lãnh đạo Phòng", "Department", w.DeptKt);
            await Assign(office.User.Id, "Văn phòng Đảng ủy (quyết định, công bố)", "Global", null);

            w.Tccb = tccb.Client;
            w.CellKtDeputy = cellKtDeputy.Client;
            w.CellVp = cellVp.Client;
            w.SecKt = secKt.Client;
            w.SecTc = secTc.Client;
            w.SecBgd = secBgd.Client;
            w.Office = office.Client;
            return w;
        }
    }
}

/// <summary>Collection riêng của kịch bản Quý IV/2026: CSDL tạm riêng để kỳ đúng năm 2026, quý IV.</summary>
[CollectionDefinition(Name)]
public sealed class Quarter4Collection : ICollectionFixture<ApiFactory>
{
    public const string Name = "quarter4";
}
