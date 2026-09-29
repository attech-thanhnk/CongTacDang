using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using Position = CongTacDang.Domain.Entities.Position;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Task 19 (T-84, T-85): xuất Mẫu 07, 08 (hồ sơ tập thể), 12 (biên bản), 16 (báo cáo Word + bản nháp nhập tay), số liệu Mẫu 14,
/// 15A, 15B trên kỳ có hồ sơ đã công bố ở các mức khác nhau; phạm vi tổ chức Đảng; báo cáo cũ đã bỏ trả 404.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CollectiveFormsReportsIntegrationTests
{
    private readonly ApiFactory _factory;
    private static Scenario? _scenario;
    private static readonly SemaphoreSlim ScenarioLock = new(1, 1);

    public CollectiveFormsReportsIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    // ===================== R1: Mẫu 15A/15B/14 — số đếm, tỷ lệ, bố cục HD03 =====================

    [SkippableFact]
    public async Task R1_Form15AB_And14_CountByStatCode_AndAuthority_WithHd03Layout()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        using (var book = await WorkbookAsync(s.Committee, $"/api/reports/form-15b?periodId={s.PeriodId}"))
        {
            var sheet = book.Worksheet(1);
            Assert.Equal("Mẫu 15B", sheet.Cell(1, 11).GetString());
            Assert.Contains("TỔNG HỢP KẾT QUẢ ĐÁNH GIÁ, XẾP LOẠI CÁN BỘ QUÝ III NĂM", sheet.Cell(5, 1).GetString());
            Assert.Equal("(Đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định, phê duyệt mức xếp loại)", sheet.Cell(6, 1).GetString());
            Assert.Equal("Đối tượng đánh giá, xếp loại", sheet.Cell(8, 2).GetString());
            Assert.Equal("Tỷ lệ % xếp loại xuất sắc trong số xếp loại tốt trở lên", sheet.Cell(8, 10).GetString());

            var m22 = Row(sheet, "M22");
            Assert.Equal("Bí thư các chi bộ trực thuộc đảng ủy cơ sở.", m22.Cell(2).GetString());
            Assert.Equal(new[] { 1, 1, 0, 0, 0, 0 }, Counts(m22));
            var m26 = Row(sheet, "M26");
            Assert.Equal(new[] { 3, 0, 1, 1, 1, 0 }, Counts(m26));
            var total = sheet.RowsUsed().Single(r => r.Cell(2).GetString() == "Tổng cộng");
            Assert.Equal(new[] { 4, 1, 1, 1, 1, 0 }, Counts(total));
            Assert.Equal("50,0%", total.Cell(10).GetString());
            Assert.Equal(10, sheet.RowsUsed().Count(r => System.Text.RegularExpressions.Regex.IsMatch(r.Cell(3).GetString(), "^M[0-9]+$")));
            // Cán bộ chưa có mã: trang kiểm tra dữ liệu, không vào bảng.
            Assert.Contains(s.NoCodeName, book.Worksheet("Kiểm tra dữ liệu").Column(1).CellsUsed().Select(c => c.GetString()));
        }

        using (var book = await WorkbookAsync(s.Committee, $"/api/reports/form-15a?periodId={s.PeriodId}"))
        {
            var m8 = Row(book.Worksheet(1), "M8");
            Assert.Equal(new[] { 1, 0, 1, 0, 0, 0 }, Counts(m8));
            Assert.Equal("Mức xếp loại đề nghị BTVĐUTCT quyết định", book.Worksheet(1).Cell(8, 5).GetString());
        }

        // Mẫu 14: mỗi cấp quyết định một trang tính, đủ 13 cột đúng tên biểu mẫu.
        using (var book = await WorkbookAsync(s.Committee, $"/api/reports/form-14?periodId={s.PeriodId}"))
        {
            var coSo = book.Worksheet(1);
            Assert.Contains("ĐỐI VỚI CÁN BỘ THUỘC DIỆN ĐẢNG ỦY CƠ SỞ QUYẾT ĐỊNH, PHÊ DUYỆT MỨC XẾP LOẠI", coSo.Cell(6, 1).GetString());
            Assert.Equal("Mã chức danh", coSo.Cell(8, 4).GetString());
            Assert.Equal("Cá nhân tự chấm điểm, đề xuất mức xếp loại", coSo.Cell(8, 5).GetString());
            Assert.Equal("Mức tự xếp loại đề xuất", coSo.Cell(9, 7).GetString());
            Assert.Equal(13, coSo.Cell(10, 13).GetValue<int>());
            var names = coSo.RowsUsed().Where(r => r.RowNumber() > 10).Select(r => r.Cell(2).GetString()).ToList();
            Assert.Contains(s.NameA1, names);
            Assert.DoesNotContain(s.NameC1, names);
            var a1 = coSo.RowsUsed().Single(r => r.Cell(2).GetString() == s.NameA1);
            Assert.Equal("M22", a1.Cell(4).GetString());
            Assert.Equal("Hoàn thành xuất sắc nhiệm vụ", a1.Cell(11).GetString());
            Assert.Contains("Căn cứ xuất sắc", a1.Cell(12).GetString());
            // Cột 13 (đợt 8): đề xuất nội dung liên quan về công tác cán bộ nhập ở bước quyết định.
            Assert.Equal("Đề xuất đưa vào quy hoạch cấp trên", a1.Cell(13).GetString());
            var capTren = book.Worksheet(2);
            Assert.Contains("BAN THƯỜNG VỤ ĐẢNG ỦY TỔNG CÔNG TY", capTren.Cell(6, 1).GetString());
            Assert.Single(capTren.RowsUsed(), r => r.Cell(2).GetString() == s.NameC1);
        }

        // Phạm vi Chi bộ A: chỉ hồ sơ của Chi bộ A (M26: 2 người).
        using (var book = await WorkbookAsync(s.CellLeaderA, $"/api/reports/form-15b?periodId={s.PeriodId}"))
            Assert.Equal(new[] { 2, 0, 1, 1, 0, 0 }, Counts(Row(book.Worksheet(1), "M26")));
    }

    // ===================== R2: Mẫu 16 — bản nháp + số liệu tự động + phạm vi =====================

    [SkippableFact]
    public async Task R2_Form16_DraftSavedPerScope_AutoCounts_ExportsWord()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        var draft = await DataAsync(await s.Committee.GetAsync($"/api/reports/mau-16/draft?periodId={s.PeriodId}"));
        var baseRows = draft.GetProperty("baseRows").EnumerateArray().ToList();
        var baseTotal = baseRows.Last();
        Assert.Equal("Tổng cộng", baseTotal.GetProperty("subject").GetString());
        Assert.Equal(5, baseTotal.GetProperty("total").GetInt32());
        Assert.Equal(1, baseTotal.GetProperty("excellent").GetInt32());
        Assert.Equal(2, baseTotal.GetProperty("good").GetInt32());
        Assert.Equal(33.3, baseTotal.GetProperty("excellentPercent").GetDouble());
        Assert.Contains(baseRows, r => r.GetProperty("statCode").ValueKind == JsonValueKind.Null && r.GetProperty("subject").GetString() != "Tổng cộng");
        var superior = draft.GetProperty("superiorRows").EnumerateArray().ToList();
        Assert.Equal("M8", superior[0].GetProperty("statCode").GetString());
        Assert.Equal(1, superior.Last().GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("version").ValueKind);

        // Lưu nháp (lần đầu, chưa có phiên bản) → lưu lại với phiên bản cũ → 409.
        var saved = await DataAsync(await s.Committee.PutAsJsonAsync($"/api/reports/mau-16/draft?periodId={s.PeriodId}", new
        {
            content = new { documentNumber = "Số 7-BC/ĐU", recipient = "Ban Thường vụ Đảng ủy cấp trên", proposal3 = "Đề xuất khác của Đảng ủy", signerName = "Bí thư Test" }
        }));
        var version = saved.GetProperty("version").GetUInt32();
        var second = await s.Committee.PutAsJsonAsync($"/api/reports/mau-16/draft?periodId={s.PeriodId}",
            new { version, content = new { documentNumber = "Số 8-BC/ĐU", recipient = "Ban Thường vụ Đảng ủy cấp trên", signerName = "Bí thư Test" } });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var stale = await s.Committee.PutAsJsonAsync($"/api/reports/mau-16/draft?periodId={s.PeriodId}", new { version, content = new { documentNumber = "Cũ" } });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var org = await DataAsync(await s.Committee.GetAsync("/api/settings/organization"));
        var text = await DocxTextAsync(s.Committee, $"/api/reports/docx/mau-16?periodId={s.PeriodId}");
        Assert.Contains("Số 8-BC/ĐU", text);
        Assert.Contains("Kính gửi: Ban Thường vụ Đảng ủy cấp trên", text);
        Assert.Contains("Bí thư các chi bộ trực thuộc đảng ủy cơ sở.", text);
        Assert.Contains("33,3%", text);
        Assert.Contains("Bí thư Test", text);
        Assert.Contains(org.GetProperty("partyCommitteeName").GetString()!.ToUpperInvariant(), text);
        // Mục III.3 đã bỏ ở lần lưu sau → giữ đoạn mẫu của biểu mẫu.
        Assert.Contains("3. Ý kiến đề xuất khác (nếu có).", text);

        // Bí thư Chi bộ A: không chọn phạm vi → Chi bộ A (bản nháp riêng), tiêu đề là tổ chức cha + Chi bộ A; Chi bộ B → 403.
        var cellDraft = await DataAsync(await s.CellLeaderA.GetAsync($"/api/reports/mau-16/draft?periodId={s.PeriodId}"));
        Assert.Equal(s.CellA, cellDraft.GetProperty("partyCellId").GetGuid());
        Assert.Equal(4, cellDraft.GetProperty("baseRows").EnumerateArray().Last().GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, cellDraft.GetProperty("version").ValueKind);
        var cellText = await DocxTextAsync(s.CellLeaderA, $"/api/reports/docx/mau-16?periodId={s.PeriodId}");
        Assert.Contains(s.CellAName.ToUpperInvariant(), cellText);
        Assert.Contains(s.RootName.ToUpperInvariant(), cellText);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderA.GetAsync($"/api/reports/docx/mau-16?periodId={s.PeriodId}&branchId={s.CellB}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderA.PutAsJsonAsync($"/api/reports/mau-16/draft?periodId={s.PeriodId}&branchId={s.CellB}", new { content = new { } })).StatusCode);
    }

    // ===================== R3: Mẫu 07, 08 — hồ sơ tập thể =====================

    [SkippableFact]
    public async Task R3_CollectiveRecords_Form07And08_SaveSections_ExportWord_ScopeByCell()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        var catalog = await DataAsync(await s.CellLeaderA.GetAsync("/api/evaluations/collective-forms/catalog"));
        Assert.Equal(4, catalog.GetProperty("form07Strengths").GetArrayLength());
        Assert.Equal(13, catalog.GetProperty("form08Categories").GetArrayLength());

        // Mục không có trong biểu mẫu → 400.
        var bad = await s.CellLeaderA.PostAsJsonAsync("/api/evaluations/collective-records", Collective07(s, new Dictionary<string, string> { ["IX.9"] = "x" }));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var created = await DataAsync(await s.CellLeaderA.PostAsJsonAsync("/api/evaluations/collective-records",
            Collective07(s, new Dictionary<string, string> { ["I.1"] = "Chấp hành tốt nguyên tắc tập trung dân chủ", ["I.3"] = "Xây dựng Đảng vững mạnh" })));
        var id07 = created.GetProperty("id").GetGuid();
        Assert.Equal("Xây dựng Đảng vững mạnh", created.GetProperty("sections").GetProperty("I.3").GetString());

        var text = await DocxTextAsync(s.CellLeaderA, $"/api/reports/docx/mau-07/{id07}");
        Assert.Contains("Chấp hành tốt nguyên tắc tập trung dân chủ", text);
        Assert.Contains("CỦA TẬP THỂ CHI ỦY " + s.CellAName.ToUpperInvariant(), text);
        Assert.Contains("1. Nhóm tiêu chí chung: 27/30", text);
        Assert.Contains(s.CellAName.ToUpperInvariant(), text);

        // Sửa (kiểm tra phiên bản) → bản xuất theo nội dung mới.
        var body = Collective07(s, new Dictionary<string, string> { ["I.4"] = "Nội dung mục 4 đã sửa" });
        body["version"] = created.GetProperty("version").GetUInt32();
        var updated = await s.CellLeaderA.PutAsJsonAsync($"/api/evaluations/collective-records/{id07}", body);
        Assert.True(updated.StatusCode == HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        Assert.Contains("Nội dung mục 4 đã sửa", await DocxTextAsync(s.CellLeaderA, $"/api/reports/docx/mau-07/{id07}"));
        var stale = await s.CellLeaderA.PutAsJsonAsync($"/api/evaluations/collective-records/{id07}", body);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        // Chi bộ B không xem/xuất được hồ sơ của Chi bộ A; hồ sơ M07 không xuất theo Mẫu 08.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderB.GetAsync($"/api/reports/docx/mau-07/{id07}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.CellLeaderA.GetAsync($"/api/reports/docx/mau-08/{id07}")).StatusCode);

        // Mẫu 08: nhóm nội dung phải thuộc 1–13.
        var bad08 = await s.CellLeaderA.PostAsJsonAsync("/api/evaluations/collective-records", Collective08(s, "99"));
        Assert.Equal(HttpStatusCode.BadRequest, bad08.StatusCode);
        var id08 = (await DataAsync(await s.CellLeaderA.PostAsJsonAsync("/api/evaluations/collective-records", Collective08(s, "1")))).GetProperty("id").GetGuid();
        var text08 = await DocxTextAsync(s.CellLeaderA, $"/api/reports/docx/mau-08/{id08}");
        Assert.Contains("- Bảo dưỡng hệ thống radar", text08);
        Assert.Contains("Hoàn thành 100% kế hoạch", text08);
        Assert.Contains("Đảm bảo an toàn bay", text08);
        Assert.Contains("Nhiệm vụ phát sinh, đột xuất theo Chỉ đạo, điều hành của cấp có thẩm quyền.", text08);

        // Mẫu 08 bản Excel (đợt 8, HD03 V.1): đúng cột, 13 nhóm nội dung, dữ liệu như bản Word; tên tệp theo quy cách HD03.
        using (var book08 = await WorkbookAsync(s.CellLeaderA, $"/api/reports/form-08/{id08}"))
        {
            var sheet = book08.Worksheet(1);
            Assert.Equal("Mẫu số 08", sheet.Cell(1, 6).GetString());
            Assert.StartsWith("BÁO CÁO TỔNG HỢP KẾT QUẢ THỰC HIỆN CÁC NHIỆM VỤ CỦA CƠ QUAN, ĐƠN VỊ QUÝ III/NĂM", sheet.Cell(5, 1).GetString());
            Assert.Equal("Nội dung công việc", sheet.Cell(8, 2).GetString());
            Assert.Equal("Tồn tại, hạn chế hoặc thành tích đã được ghi nhận, biểu dương", sheet.Cell(8, 5).GetString());
            Assert.Equal(6, sheet.Cell(9, 6).GetValue<int>());
            Assert.StartsWith("Thực hiện nhiệm vụ sản xuất, kinh doanh.", sheet.Cell(10, 2).GetString());
            Assert.Contains("- Bảo dưỡng hệ thống radar", sheet.Cell(10, 2).GetString());
            Assert.Equal("Hoàn thành 100% kế hoạch", sheet.Cell(10, 4).GetString());
            Assert.Contains("- Nhiệm vụ 1: …", sheet.Cell(11, 2).GetString());
            Assert.Equal(13, sheet.Cell(22, 1).GetValue<int>());
            Assert.StartsWith("Nhiệm vụ phát sinh, đột xuất", sheet.Cell(22, 2).GetString());
        }
        var file08 = await s.CellLeaderA.GetAsync($"/api/reports/form-08/{id08}");
        Assert.StartsWith("Mau 08_", file08.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.EndsWith(".xlsx", file08.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderB.GetAsync($"/api/reports/form-08/{id08}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.CellLeaderA.GetAsync($"/api/reports/form-08/{id07}")).StatusCode);

        // PDF cùng endpoint: không có LibreOffice → 503 kèm thông báo; có → PDF.
        var pdf = await s.CellLeaderA.GetAsync($"/api/reports/docx/mau-08/{id08}?format=pdf");
        Assert.True(pdf.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable, pdf.StatusCode.ToString());
    }

    // ===================== R4: Mẫu 12 — biên bản hội nghị =====================

    [SkippableFact]
    public async Task R4_Meeting_Form12_DetailsSavedAndExported_ScopeByCell()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        var request = new Dictionary<string, object?>
        {
            ["periodId"] = s.PeriodId, ["partyCellId"] = s.CellA, ["stage"] = "B4_DECISION", ["formCode"] = "M12",
            ["meetingType"] = "Hội nghị đánh giá", ["location"] = "Hội trường tầng 2",
            ["startedAt"] = new DateTime(2026, 9, 14, 1, 0, 0, DateTimeKind.Utc), ["endedAt"] = new DateTime(2026, 9, 14, 3, 30, 0, DateTimeKind.Utc),
            ["invitedCount"] = 7, ["presentCount"] = 6, ["absentCount"] = 1,
            ["chairName"] = "Nguyễn Chủ Trì", ["secretaryName"] = "Trần Thư Ký",
            ["outcomeContent"] = "Hội nghị thống nhất 100% kết quả",
            ["details"] = new
            {
                workingRules = "Chi bộ A nhiệm kỳ 2025–2030", chairTitle = "Bí thư Chi bộ", secretaryTitle = "Chi ủy viên",
                attendees = new[] { new { name = "Lê Phục Vụ", title = "Đảng viên, ghi chép" } }
            }
        };
        var created = await DataAsync(await s.CellLeaderA.PostAsJsonAsync("/api/evaluations/meetings", request));
        var meetingId = created.GetProperty("id").GetGuid();
        Assert.Equal("Bí thư Chi bộ", created.GetProperty("details").GetProperty("chairTitle").GetString());

        var text = await DocxTextAsync(s.CellLeaderA, $"/api/reports/docx/mau-12/{meetingId}");
        Assert.Contains($"Hội nghị {s.CellAName} về việc đánh giá, xếp loại chất lượng cán bộ quý III/", text);
        Assert.Contains("Bắt đầu vào hồi 08h00, ngày 14/09/2026", text);
        Assert.Contains("Hội trường tầng 2", text);
        Assert.Contains("(1) Đồng chí Lê Phục Vụ - Đảng viên, ghi chép.", text);
        Assert.Contains("4. Chủ trì Hội nghị: Đồng chí Nguyễn Chủ Trì - Bí thư Chi bộ.", text);
        Assert.Contains("bỏ phiếu quyết định, phê duyệt mức xếp loại", text);
        Assert.Contains("Hội nghị kết thúc vào hồi 10h30", text);
        Assert.Contains("Hội nghị thống nhất 100% kết quả", text);
        Assert.Matches(@"chất lượng cán bộ quý III/\d{4}\. Cụ thể như sau:", text);

        // Sửa mục của Mẫu 12 (phiên bản) → xuất theo nội dung mới.
        request["version"] = created.GetProperty("version").GetUInt32();
        request["details"] = new { workingRules = "Chi bộ A nhiệm kỳ 2025–2030", chairTitle = "Bí thư Chi bộ A", attendees = Array.Empty<object>() };
        var updated = await s.CellLeaderA.PutAsJsonAsync($"/api/evaluations/meetings/{meetingId}", request);
        Assert.True(updated.StatusCode == HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        var text2 = await DocxTextAsync(s.CellLeaderA, $"/api/reports/docx/mau-12/{meetingId}");
        Assert.Contains("Bí thư Chi bộ A.", text2);
        Assert.DoesNotContain("Lê Phục Vụ", text2);

        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderB.GetAsync($"/api/reports/docx/mau-12/{meetingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderB.PutAsJsonAsync($"/api/evaluations/meetings/{meetingId}", request)).StatusCode);

        // Biên bản kiểm phiếu (M13) không xuất theo Mẫu 12.
        var m13 = new Dictionary<string, object?>(request) { ["formCode"] = "M13", ["version"] = null };
        m13["details"] = new
        {
            workingRules = "Chi bộ A nhiệm kỳ 2025–2030", chairTitle = "Bí thư Chi bộ",
            countingCommittee = new[] { new { name = "Phan Tổ Trưởng", title = "Chi ủy viên" }, new { name = "Đỗ Thành Viên", title = "Đảng viên" } },
            ballotsIssued = 6, ballotsCollected = 6, ballotsValid = 5, ballotsInvalid = 1
        };
        m13["voteSummaries"] = new object[]
        {
            new { recordId = s.RecordA1, votesExcellent = 5, votesGood = 0, votesSatisfactory = 0, votesUnsatisfactory = 0, votesNotRated = 0, invalidVotes = 1, notes = "" },
            new { recordId = s.RecordC1, votesExcellent = 0, votesGood = 3, votesSatisfactory = 1, votesUnsatisfactory = 0, votesNotRated = 1, invalidVotes = 1, notes = "Trình BTV" }
        };
        var m13Created = await DataAsync(await s.CellLeaderA.PostAsJsonAsync("/api/evaluations/meetings", m13));
        var m13Id = m13Created.GetProperty("id").GetGuid();
        Assert.Equal(2, m13Created.GetProperty("details").GetProperty("countingCommittee").GetArrayLength());
        Assert.Contains(m13Created.GetProperty("voteSummaries").EnumerateArray(), v => v.GetProperty("votesNotRated").GetInt32() == 1);
        Assert.Equal(HttpStatusCode.BadRequest, (await s.CellLeaderA.GetAsync($"/api/reports/docx/mau-12/{m13Id}")).StatusCode);

        // Mẫu 13 (đợt 8): đúng biểu mẫu gốc — mục I (BTVĐUTCT) / mục II (Đảng ủy/Chi ủy cơ sở), cột "Chưa đánh giá, xếp loại",
        // Tổ kiểm phiếu, số phiếu phát ra/thu về/hợp lệ/không hợp lệ.
        var text13 = await DocxTextAsync(s.CellLeaderA, $"/api/reports/docx/mau-13/{m13Id}");
        Assert.Contains("BIÊN BẢN KIỂM PHIẾU", text13);
        Assert.Matches(@"chất lượng cán bộ quý III/\d{4}\. Cụ thể như sau:", text13);
        Assert.Contains($"Hội nghị {s.CellAName} về việc đánh giá, xếp loại chất lượng cán bộ quý III/", text13);
        Assert.Contains("(1) Đồng chí Phan Tổ Trưởng - Chi ủy viên: Tổ trưởng.", text13);
        Assert.Contains("(2) Đồng chí Đỗ Thành Viên - Đảng viên: Thành viên.", text13);
        Assert.Contains("- Tổng số phiếu phát ra:\t6 Phiếu.", text13);
        Assert.Contains("+ Số phiếu không hợp lệ:\t1 Phiếu.", text13);
        Assert.Contains("Chưa đánh giá, xếp loại", text13);
        var superiorAt = text13.IndexOf("CỦA BTVĐUTCT", StringComparison.Ordinal);
        var baseAt = text13.IndexOf("CỦA ĐẢNG ỦY/CHI ỦY CƠ SỞ", StringComparison.Ordinal);
        Assert.True(superiorAt > 0 && baseAt > superiorAt);
        var c1At = text13.IndexOf(s.NameC1, StringComparison.Ordinal);
        var a1At = text13.IndexOf(s.NameA1, StringComparison.Ordinal);
        Assert.True(c1At > superiorAt && c1At < baseAt, "Cán bộ thẩm quyền cấp trên phải ở mục I");
        Assert.True(a1At > baseAt, "Cán bộ thẩm quyền cơ sở phải ở mục II");
        Assert.Contains("Trình BTV", text13);
        Assert.Contains("Phan Tổ Trưởng", text13[text13.LastIndexOf("T/M TỔ KIỂM PHIẾU", StringComparison.Ordinal)..]);
        var file13 = await s.CellLeaderA.GetAsync($"/api/reports/docx/mau-13/{m13Id}");
        Assert.StartsWith("Mau 13_", file13.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        // Biên bản chưa có kết quả kiểm phiếu → 400; Chi bộ khác → 403; endpoint cũ theo kỳ đã bỏ.
        Assert.Equal(HttpStatusCode.BadRequest, (await s.CellLeaderA.GetAsync($"/api/reports/docx/mau-13/{meetingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderB.GetAsync($"/api/reports/docx/mau-13/{m13Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await s.Committee.GetAsync($"/api/reports/docx/mau-13?periodId={s.PeriodId}")).StatusCode);

        // Số phiếu hợp lệ + không hợp lệ ≠ thu về → 400.
        var badBallots = new Dictionary<string, object?>(m13)
        {
            ["details"] = new { ballotsIssued = 6, ballotsCollected = 6, ballotsValid = 4, ballotsInvalid = 1 },
            ["voteSummaries"] = new[] { new { recordId = s.RecordA1, votesExcellent = 1, votesGood = 0, votesSatisfactory = 0, votesUnsatisfactory = 0, votesNotRated = 0, invalidVotes = 0, notes = "" } }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await s.CellLeaderA.PostAsJsonAsync("/api/evaluations/meetings", badBallots)).StatusCode);
    }

    // ===================== R5: báo cáo nội bộ; tên tệp theo quy cách HD03 =====================

    [SkippableFact]
    public async Task R5_InternalReports_And_Hd03FileNames()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        Assert.Equal(HttpStatusCode.OK, (await s.Committee.GetAsync($"/api/reports/internal/excellent-quota?periodId={s.PeriodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await s.Committee.GetAsync("/api/reports/cadres")).StatusCode);
        var file = await s.Committee.GetAsync($"/api/reports/form-15a?periodId={s.PeriodId}");
        Assert.StartsWith("Mau 15A_", file.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
    }

    #region Dữ liệu

    private static Dictionary<string, object?> Collective07(Scenario s, Dictionary<string, string> sections) => new()
    {
        ["periodId"] = s.PeriodId, ["form"] = "M07", ["partyCellId"] = s.CellA, ["subjectName"] = "Chi ủy " + s.CellAName,
        ["limitations"] = "Còn chậm báo cáo", ["causes"] = "Chủ quan", ["remediationPlan"] = "Khắc phục quý IV",
        ["generalCriteriaScore"] = 27, ["taskCriteriaScore"] = 63, ["selfProposedGrade"] = "HoanThanhTot", ["sections"] = sections
    };

    private static Dictionary<string, object?> Collective08(Scenario s, string category) => new()
    {
        ["periodId"] = s.PeriodId, ["form"] = "M08", ["partyCellId"] = s.CellA, ["subjectName"] = "Cơ quan " + s.CellAName,
        ["items"] = new[]
        {
            new { itemOrder = 1, category, taskName = "Bảo dưỡng hệ thống radar", planOrDirection = "Kế hoạch năm", result = "Hoàn thành 100% kế hoạch", limitations = "", notes = "" }
        }
    };

    private sealed class Scenario
    {
        public Guid PeriodId, CellA, CellB, RecordA1, RecordC1;
        public string CellAName = string.Empty, RootName = string.Empty;
        public string NameA1 = string.Empty, NameC1 = string.Empty, NoCodeName = string.Empty;
        public HttpClient Committee = null!, CellLeaderA = null!, CellLeaderB = null!;
    }

    /// <summary>
    /// Kỳ Quý III có 6 hồ sơ đã công bố: Chi bộ A — a1 (M22, xuất sắc), a2 (M26, tốt), a3 (M26, hoàn thành), n1 (chưa có mã, tốt),
    /// c1 (M8, cấp trên, thẩm định đề xuất tốt); Chi bộ B — b1 (M26, không hoàn thành).
    /// </summary>
    private async Task<Scenario> GetScenarioAsync()
    {
        await ScenarioLock.WaitAsync();
        try
        {
            return _scenario ??= await CreateScenarioAsync();
        }
        finally
        {
            ScenarioLock.Release();
        }
    }

    private async Task<Scenario> CreateScenarioAsync()
    {
        var s = new Scenario();
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        s.RootName = $"Đảng ủy K19 {sfx}";
        s.CellAName = $"Chi bộ K19A {sfx}";
        s.NameA1 = $"K19 A1 {sfx}";
        s.NameC1 = $"K19 C1 {sfx}";
        s.NoCodeName = $"K19 N1 {sfx}";

        Guid rootId = Guid.Empty, deptId = Guid.Empty;
        var positions = new Dictionary<string, Guid>();
        await _factory.WithDbAsync(async db =>
        {
            var root = new PartyCell { Code = $"K19-R-{sfx}", Name = s.RootName };
            root.Path = OrgTree.BuildPath(null, root.Id);
            var cellA = new PartyCell { Code = $"K19-A-{sfx}", Name = s.CellAName, ParentId = root.Id };
            cellA.Path = OrgTree.BuildPath(root.Path, cellA.Id);
            var cellB = new PartyCell { Code = $"K19-B-{sfx}", Name = $"Chi bộ K19B {sfx}", ParentId = root.Id };
            cellB.Path = OrgTree.BuildPath(root.Path, cellB.Id);
            var dept = new AdministrativeDepartment { Code = $"K19-D-{sfx}", Name = $"Phòng K19 {sfx}" };
            dept.Path = OrgTree.BuildPath(null, dept.Id);
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy3, Name = $"Kỳ K19 {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                CriteriaSnapshot = CriteriaTestData.Snapshot("09B")
            };
            foreach (var (code, authority) in new[] { ("M8", ApprovalAuthority.CapTren), ("M22", ApprovalAuthority.CoSo), ("M26", ApprovalAuthority.CoSo) })
            {
                var position = new Position
                {
                    Name = $"K19 {code} {sfx}", StatCode = code, DefaultApprovalAuthority = authority, IsLeadership = true,
                    Side = code == "M26" ? PositionSide.Administrative : PositionSide.Party
                };
                db.Add(position);
                positions[code] = position.Id;
            }
            db.AddRange(root, cellA, cellB, dept, period);
            await db.SaveChangesAsync();
            (rootId, deptId, s.CellA, s.CellB, s.PeriodId) = (root.Id, dept.Id, cellA.Id, cellB.Id, period.Id);
        });

        var a1 = await _factory.CreateUserAsync(deptId, s.CellA, fullName: s.NameA1);
        var a2 = await _factory.CreateUserAsync(deptId, s.CellA, fullName: $"K19 A2 {sfx}");
        var a3 = await _factory.CreateUserAsync(deptId, s.CellA, fullName: $"K19 A3 {sfx}");
        var n1 = await _factory.CreateUserAsync(deptId, s.CellA, fullName: s.NoCodeName);
        var c1 = await _factory.CreateUserAsync(deptId, s.CellA, ApprovalAuthority.CapTren, s.NameC1);
        var b1 = await _factory.CreateUserAsync(deptId, s.CellB, fullName: $"K19 B1 {sfx}");

        await _factory.WithDbAsync(async db =>
        {
            var from = DateTime.UtcNow.AddYears(-1);
            foreach (var (user, code) in new[] { (a1, "M22"), (a2, "M26"), (a3, "M26"), (c1, "M8"), (b1, "M26") })
                db.Add(new MemberPosition { UserId = user.Id, PositionId = positions[code], IsPrimary = true, ValidFrom = from });

            EvaluationRecord Record(TestUser owner, Guid cell, ApprovalAuthority authority, EvaluationGrade final) => new()
            {
                PeriodId = s.PeriodId, MemberId = owner.Id, DepartmentId = deptId, PartyCellId = cell, ApprovalAuthority = authority,
                Status = final == EvaluationGrade.ChuaXepLoai ? RecordStatus.AwaitingDecision : RecordStatus.Published,
                WeightFrameCode = "K2", GeneralCriteriaScore = 28, TasksScore = 60, TotalSelfScore = 88, SelfScoredAt = DateTime.UtcNow,
                SelfProposedGrade = EvaluationGrade.HoanThanhTot, FinalGrade = final, UpdatedAt = DateTime.UtcNow,
                PublishedAt = final == EvaluationGrade.ChuaXepLoai ? null : DateTime.UtcNow
            };
            var ra1 = Record(a1, s.CellA, ApprovalAuthority.CoSo, EvaluationGrade.HoanThanhXuatSac);
            ra1.AppraisalExplanation = "Căn cứ xuất sắc: vượt 3 sản phẩm";
            ra1.CadreWorkProposal = "Đề xuất đưa vào quy hoạch cấp trên";
            var rc1 = Record(c1, s.CellA, ApprovalAuthority.CapTren, EvaluationGrade.ChuaXepLoai);
            rc1.AppraisalProposedGrade = EvaluationGrade.HoanThanhTot;
            db.AddRange(
                ra1,
                Record(a2, s.CellA, ApprovalAuthority.CoSo, EvaluationGrade.HoanThanhTot),
                Record(a3, s.CellA, ApprovalAuthority.CoSo, EvaluationGrade.HoanThanh),
                Record(n1, s.CellA, ApprovalAuthority.CoSo, EvaluationGrade.HoanThanhTot),
                rc1,
                Record(b1, s.CellB, ApprovalAuthority.CoSo, EvaluationGrade.KhongHoanThanh));
            await db.SaveChangesAsync();
            s.RecordA1 = ra1.Id;
            s.RecordC1 = rc1.Id;
        });

        var perms = new[]
        {
            PermissionCodes.ReportExport, PermissionCodes.CollectiveManage, PermissionCodes.MeetingManage,
            PermissionCodes.MeetingRead, PermissionCodes.EvaluationRead
        };
        var committee = await _factory.CreateUserWithPermissionsAsync(perms);
        var cellRole = await _factory.CreateRoleAsync(perms);
        var leaderA = await _factory.CreateUserAsync();
        var leaderB = await _factory.CreateUserAsync();
        await _factory.AssignAsync(leaderA.Id, cellRole.Id, RoleScopeType.PartyCell, s.CellA);
        await _factory.AssignAsync(leaderB.Id, cellRole.Id, RoleScopeType.PartyCell, s.CellB);

        s.Committee = await _factory.LoginAsAsync(committee.Username, committee.Password, distinctClientIp: true);
        s.CellLeaderA = await _factory.LoginAsAsync(leaderA.Username, leaderA.Password, distinctClientIp: true);
        s.CellLeaderB = await _factory.LoginAsAsync(leaderB.Username, leaderB.Password, distinctClientIp: true);
        return s;
    }

    #endregion

    #region Hỗ trợ

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }

    private static async Task<XLWorkbook> WorkbookAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    private static IXLRow Row(IXLWorksheet sheet, string code) =>
        sheet.RowsUsed().Single(r => r.Cell(3).GetString() == code);

    /// <summary>Tổng số và 5 mức (cột 4–9).</summary>
    private static int[] Counts(IXLRow row) => Enumerable.Range(4, 6).Select(c => row.Cell(c).GetValue<int>()).ToArray();

    /// <summary>Tải .docx, kiểm tra hợp lệ OpenXML, trả toàn văn (mỗi đoạn một dòng, ngắt dòng trong đoạn → "\n").</summary>
    private static async Task<string> DocxTextAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Empty(new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019).Validate(doc));
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
        return string.Join("\n", body.Descendants<Paragraph>().Select(p => string.Concat(p.Descendants().Select(e => e switch
        {
            Text t => t.Text,
            TabChar => "\t",
            Break => "\n",
            _ => string.Empty
        }))));
    }

    #endregion
}
