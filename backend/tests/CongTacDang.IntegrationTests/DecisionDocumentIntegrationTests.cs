using System.Net;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using Position = CongTacDang.Domain.Entities.Position;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Quyết định xếp loại cán bộ quý kèm Phụ lục (<c>GET /api/reports/docx/decision</c>): chỉ hồ sơ thuộc diện tổ chức Đảng cơ sở
/// quyết định và đã ghi quyết định; số/ngày văn bản và người chủ trì hội nghị lấy từ dữ liệu; còn hồ sơ chưa quyết định hoặc không
/// có hồ sơ → 400; thiếu quyền <c>report.export</c> → 403; phạm vi tổ chức Đảng như Mẫu 16.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class DecisionDocumentIntegrationTests
{
    private readonly ApiFactory _factory;
    private static Scenario? _scenario;
    private static readonly SemaphoreSlim ScenarioLock = new(1, 1);

    public DecisionDocumentIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    [SkippableFact]
    public async Task D1_CellScope_OnlyBaseLevelDecidedRecords_WithDocumentAndChair()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        var response = await s.Committee.GetAsync($"/api/reports/docx/decision?periodId={s.PeriodId}&branchId={s.CellA}");
        var (text, rows) = await ReadAsync(response);

        Assert.StartsWith("Quyet dinh xep loai_", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.Contains(s.CellAName.ToUpperInvariant() + " QUYẾT ĐỊNH", text);
        Assert.Contains("Số 21-QĐ/CB", text);
        Assert.Contains("ngày 22 tháng 09 năm", text);
        Assert.Contains("Hội nghị " + s.CellAName + " ngày 21/09/", text);
        Assert.Contains("Chủ Trì K20 " + s.Suffix, text);
        Assert.Contains("(kèm theo Quyết định số 21-QĐ/CB ngày 22/09/", text);

        // Phụ lục: hai hồ sơ diện cơ sở đã quyết định; hồ sơ diện cấp trên không vào.
        var names = rows.Select(r => r[1]).ToList();
        Assert.Equal(new[] { s.NameD1, s.NameD2 }.OrderBy(n => n, StringComparer.Ordinal), names.OrderBy(n => n, StringComparer.Ordinal));
        Assert.DoesNotContain(s.NameC1, text);
        var d1 = rows.Single(r => r[1] == s.NameD1);
        Assert.Contains("K20 M22 " + s.Suffix, d1[2]);
        Assert.Equal("Hoàn thành tốt nhiệm vụ", d1[3]);
        Assert.Equal("Hoàn thành xuất sắc nhiệm vụ", d1[4]);
        Assert.Equal("Hoàn thành xuất sắc nhiệm vụ", d1[5]);
        Assert.Equal(string.Empty, d1[6]);
        Assert.Equal("Hoàn thành xuất sắc nhiệm vụ", d1[7]);
    }

    [SkippableFact]
    public async Task D2_PendingDecision_NoBaseRecords_Return400()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        // Toàn Đảng bộ: hồ sơ của Chi bộ B chưa ghi quyết định → 400 nêu tên.
        var whole = await s.Committee.GetAsync($"/api/reports/docx/decision?periodId={s.PeriodId}");
        Assert.Equal(HttpStatusCode.BadRequest, whole.StatusCode);
        using (var json = JsonDocument.Parse(await whole.Content.ReadAsStringAsync()))
        {
            var message = json.RootElement.GetProperty("message").GetString()!;
            Assert.Contains("chưa ghi quyết định", message);
            Assert.Contains(s.NamePending, message);
        }

        // Chi bộ C chỉ có hồ sơ diện cấp trên → không có Quyết định để lập.
        var onlySuperior = await s.Committee.GetAsync($"/api/reports/docx/decision?periodId={s.PeriodId}&branchId={s.CellC}");
        Assert.Equal(HttpStatusCode.BadRequest, onlySuperior.StatusCode);
        using (var json = JsonDocument.Parse(await onlySuperior.Content.ReadAsStringAsync()))
            Assert.Contains("không có hồ sơ nào thuộc diện", json.RootElement.GetProperty("message").GetString());
    }

    [SkippableFact]
    public async Task D3_Permissions_ReportExportRequired_ScopeByCell()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await s.NoExport.GetAsync($"/api/reports/docx/decision?periodId={s.PeriodId}&branchId={s.CellA}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderA.GetAsync($"/api/reports/docx/decision?periodId={s.PeriodId}&branchId={s.CellB}")).StatusCode);
        // Không chọn phạm vi: phạm vi report.export đúng một tổ chức Đảng → tổ chức đó.
        var (_, rows) = await ReadAsync(await s.CellLeaderA.GetAsync($"/api/reports/docx/decision?periodId={s.PeriodId}"));
        Assert.Equal(2, rows.Count);
    }

    #region Dữ liệu

    private sealed class Scenario
    {
        public Guid PeriodId, CellA, CellB, CellC;
        public string Suffix = string.Empty, CellAName = string.Empty;
        public string NameD1 = string.Empty, NameD2 = string.Empty, NameC1 = string.Empty, NamePending = string.Empty;
        public HttpClient Committee = null!, CellLeaderA = null!, NoExport = null!;
    }

    /// <summary>
    /// Kỳ Quý III: Chi bộ A — d1 (M22, quyết định xuất sắc), d2 (quyết định tốt) cùng văn bản "21-QĐ/CB", biên bản B4 có chủ trì;
    /// c1 (diện cấp trên, đã quyết định). Chi bộ B — p1 (diện cơ sở, chờ quyết định). Chi bộ C — c2 (diện cấp trên).
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
        s.Suffix = sfx;
        s.CellAName = $"Chi bộ K20A {sfx}";
        s.NameD1 = $"K20 D1 {sfx}";
        s.NameD2 = $"K20 D2 {sfx}";
        s.NameC1 = $"K20 C1 {sfx}";
        s.NamePending = $"K20 P1 {sfx}";

        Guid deptId = Guid.Empty, positionId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var root = new PartyCell { Code = $"K20-R-{sfx}", Name = $"Đảng bộ K20 {sfx}" };
            root.Path = OrgTree.BuildPath(null, root.Id);
            PartyCell Child(string code, string name)
            {
                var cell = new PartyCell { Code = $"K20-{code}-{sfx}", Name = name, ParentId = root.Id };
                cell.Path = OrgTree.BuildPath(root.Path, cell.Id);
                return cell;
            }
            var cellA = Child("A", s.CellAName);
            var cellB = Child("B", $"Chi bộ K20B {sfx}");
            var cellC = Child("C", $"Chi bộ K20C {sfx}");
            var dept = new AdministrativeDepartment { Code = $"K20-D-{sfx}", Name = $"Phòng K20 {sfx}" };
            dept.Path = OrgTree.BuildPath(null, dept.Id);
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy3, Name = $"Kỳ K20 {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                CriteriaSnapshot = CriteriaTestData.Snapshot("09B")
            };
            var position = new Position
            {
                Name = $"K20 M22 {sfx}", StatCode = "M22", DefaultApprovalAuthority = ApprovalAuthority.CoSo, IsLeadership = true,
                Side = PositionSide.Party
            };
            db.AddRange(root, cellA, cellB, cellC, dept, period, position);
            await db.SaveChangesAsync();
            (deptId, positionId, s.CellA, s.CellB, s.CellC, s.PeriodId) = (dept.Id, position.Id, cellA.Id, cellB.Id, cellC.Id, period.Id);
        });

        var d1 = await _factory.CreateUserAsync(deptId, s.CellA, fullName: s.NameD1);
        var d2 = await _factory.CreateUserAsync(deptId, s.CellA, fullName: s.NameD2);
        var c1 = await _factory.CreateUserAsync(deptId, s.CellA, ApprovalAuthority.CapTren, s.NameC1);
        var p1 = await _factory.CreateUserAsync(deptId, s.CellB, fullName: s.NamePending);
        var c2 = await _factory.CreateUserAsync(deptId, s.CellC, ApprovalAuthority.CapTren, $"K20 C2 {sfx}");

        await _factory.WithDbAsync(async db =>
        {
            db.Add(new MemberPosition { UserId = d1.Id, PositionId = positionId, IsPrimary = true, ValidFrom = DateTime.UtcNow.AddYears(-1) });
            var meeting = new EvaluationMeeting
            {
                PeriodId = s.PeriodId, PartyCellId = s.CellA, Stage = WorkflowStep.B4_DECISION, MeetingType = "Hội nghị Chi bộ",
                StartedAt = new DateTime(2026, 9, 21, 1, 0, 0, DateTimeKind.Utc), ChairName = $"Chủ Trì K20 {sfx}"
            };
            db.Add(meeting);

            EvaluationRecord Record(TestUser owner, Guid cell, ApprovalAuthority authority, EvaluationGrade? final) => new()
            {
                PeriodId = s.PeriodId, MemberId = owner.Id, DepartmentId = deptId, PartyCellId = cell, ApprovalAuthority = authority,
                Status = final.HasValue ? RecordStatus.AwaitingPublish : RecordStatus.AwaitingDecision,
                WeightFrameCode = "K2", GeneralCriteriaScore = 28, TasksScore = 60, TotalSelfScore = 88, SelfScoredAt = DateTime.UtcNow,
                SelfProposedGrade = EvaluationGrade.HoanThanhTot, CollectiveProposedGrade = EvaluationGrade.HoanThanhTot,
                FinalGrade = final ?? EvaluationGrade.ChuaXepLoai, UpdatedAt = DateTime.UtcNow,
                DecisionRecordedAt = final.HasValue ? DateTime.UtcNow : null,
                DecisionDocumentNumber = final.HasValue ? "21-QĐ/CB" : null,
                DecisionDocumentDate = final.HasValue ? new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc) : null,
                DecisionMeetingId = final.HasValue && cell == s.CellA ? meeting.Id : null
            };
            var rd1 = Record(d1, s.CellA, ApprovalAuthority.CoSo, EvaluationGrade.HoanThanhXuatSac);
            rd1.CollectiveProposedGrade = EvaluationGrade.HoanThanhXuatSac;
            rd1.AppraisalProposedGrade = EvaluationGrade.HoanThanhXuatSac;
            db.AddRange(
                rd1,
                Record(d2, s.CellA, ApprovalAuthority.CoSo, EvaluationGrade.HoanThanhTot),
                Record(c1, s.CellA, ApprovalAuthority.CapTren, EvaluationGrade.HoanThanhTot),
                Record(p1, s.CellB, ApprovalAuthority.CoSo, null),
                Record(c2, s.CellC, ApprovalAuthority.CapTren, EvaluationGrade.HoanThanhTot));
            await db.SaveChangesAsync();
        });

        var committee = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.ReportExport);
        var noExport = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationRead, PermissionCodes.MeetingRead);
        var cellRole = await _factory.CreateRoleAsync(PermissionCodes.ReportExport);
        var leaderA = await _factory.CreateUserAsync();
        await _factory.AssignAsync(leaderA.Id, cellRole.Id, RoleScopeType.PartyCell, s.CellA);

        s.Committee = await _factory.LoginAsAsync(committee.Username, committee.Password, distinctClientIp: true);
        s.NoExport = await _factory.LoginAsAsync(noExport.Username, noExport.Password, distinctClientIp: true);
        s.CellLeaderA = await _factory.LoginAsAsync(leaderA.Username, leaderA.Password, distinctClientIp: true);
        return s;
    }

    #endregion

    #region Hỗ trợ

    /// <summary>Tải .docx, kiểm tra hợp lệ; trả toàn văn và các dòng dữ liệu của bảng Phụ lục (bỏ 2 dòng tiêu đề).</summary>
    private static async Task<(string Text, List<List<string>> Rows)> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document", response.Content.Headers.ContentType?.MediaType);
        using var doc = WordprocessingDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        Assert.Empty(new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2019).Validate(doc));
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.DoesNotContain(body.Descendants<SdtElement>(), sdt => sdt.SdtProperties?.GetFirstChild<Tag>() != null);
        var text = string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText));
        var rows = body.Elements<Table>().Last().Elements<TableRow>().Skip(2)
            .Select(r => r.Elements<TableCell>().Select(c => c.InnerText).ToList())
            .ToList();
        return (text, rows);
    }

    #endregion
}
