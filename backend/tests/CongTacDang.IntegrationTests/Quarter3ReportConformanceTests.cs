using System.Globalization;
using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using DocumentFormat.OpenXml.Packaging;
using Xunit;
using Position = CongTacDang.Domain.Entities.Position;
using WordTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using WordTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;

namespace CongTacDang.IntegrationTests;

/// <summary>Đối chiếu Mẫu 14, 15A, 15B, 16 với kết quả thật Quý III/2026 (<see cref="Quarter3Structure"/>, tên giả): từng con số phải khớp.</summary>
[Collection(ApiCollection.Name)]
public sealed class Quarter3ReportConformanceTests
{
    private readonly ApiFactory _factory;
    private static Scenario? _scenario;
    private static readonly SemaphoreSlim ScenarioLock = new(1, 1);

    public Quarter3ReportConformanceTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    [Fact]
    public void Structure_MatchesSubmittedCounts()
    {
        // Cơ cấu dựng đúng 6 + 48 người; mã suy ra từ chức vụ và diện suy ra từ chức vụ đúng như bảng kết quả.
        Assert.Equal(6, Quarter3Structure.Cadres.Count(c => c.Authority == ApprovalAuthority.CapTren));
        Assert.Equal(48, Quarter3Structure.Cadres.Count(c => c.Authority == ApprovalAuthority.CoSo));
        var specs = Quarter3Structure.Positions.ToDictionary(p => p.Key);
        foreach (var cadre in Quarter3Structure.Cadres)
        {
            var held = cadre.Positions.Select(k => new HeldPosition(specs[k].Name, specs[k].StatCode, specs[k].Authority)).ToList();
            Assert.Equal(cadre.ExpectedCode, PositionRules.PersonStatCode(held));
            Assert.Equal(cadre.Authority, PositionRules.DeriveApprovalAuthority(held));
        }
        foreach (var (code, counts) in Quarter3Structure.Form15A.Concat(Quarter3Structure.Form15B))
            Assert.Equal(counts[0], Quarter3Structure.Cadres.Count(c => c.ExpectedCode == code));
    }

    // ===================== Mẫu 15A, 15B =====================

    [SkippableFact]
    public async Task Form15A_And_15B_MatchQuarter3Submission()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        using (var book = await WorkbookAsync(s.Committee, $"/api/reports/form-15a?periodId={s.PeriodId}"))
            AssertStatCodeSheet(book.Worksheet(1), Hd03FormCatalog.Form15AFirstCode, Hd03FormCatalog.Form15ALastCode, Quarter3Structure.Form15A, 6);

        using (var book = await WorkbookAsync(s.Committee, $"/api/reports/form-15b?periodId={s.PeriodId}"))
        {
            AssertStatCodeSheet(book.Worksheet(1), Hd03FormCatalog.Form15BFirstCode, Hd03FormCatalog.Form15BLastCode, Quarter3Structure.Form15B, 48);
            Assert.Equal("Mọi cán bộ trong phạm vi đã có mã chức danh thống kê.", book.Worksheet("Kiểm tra dữ liệu").Cell(1, 1).GetString());
        }
    }

    /// <summary>
    /// Mỗi mã trong khoảng một dòng: tổng số và 5 mức đúng bảng đã nộp (mã không có trong bảng → 0); tỷ lệ % = xuất sắc /
    /// (xuất sắc + tốt), bảng đã nộp ghi 0 khi không ai tốt trở lên — phần mềm để trống ô đó.
    /// </summary>
    private static void AssertStatCodeSheet(IXLWorksheet sheet, int first, int last, IReadOnlyDictionary<string, int[]> expected, int total)
    {
        for (var order = first; order <= last; order++)
        {
            var code = "M" + order.ToString(CultureInfo.InvariantCulture);
            var row = sheet.RowsUsed().Single(r => r.Cell(3).GetString() == code);
            var want = expected.GetValueOrDefault(code) ?? new int[6];
            Assert.True(want.SequenceEqual(Counts(row)), $"{code}: mong đợi [{string.Join(",", want)}], phần mềm [{string.Join(",", Counts(row))}]");
            Assert.Equal(ExpectedPercent(want), row.Cell(10).GetString());
        }
        var sum = sheet.RowsUsed().Single(r => r.Cell(2).GetString() == "Tổng cộng");
        Assert.Equal(new[] { total, 0, total, 0, 0, 0 }, Counts(sum));
        Assert.Equal("0,0%", sum.Cell(10).GetString());
    }

    // ===================== Mẫu 14 =====================

    [SkippableFact]
    public async Task Form14_SuperiorSheet_ListsSixCadresLikeSubmission()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        using var book = await WorkbookAsync(s.Committee, $"/api/reports/form-14?periodId={s.PeriodId}");
        var superior = book.Worksheet(2);
        Assert.Contains("BAN THƯỜNG VỤ ĐẢNG ỦY TỔNG CÔNG TY", superior.Cell(6, 1).GetString());
        var rows = superior.RowsUsed().Where(r => r.RowNumber() > 10 && s.Names.ContainsKey(r.Cell(2).GetString())).ToList();
        Assert.Equal(6, rows.Count);
        foreach (var row in rows)
        {
            var cadre = s.Names[row.Cell(2).GetString()];
            Assert.Equal(ApprovalAuthority.CapTren, cadre.Authority);
            Assert.Equal(cadre.ExpectedCode, row.Cell(4).GetString());
            Assert.Equal(cadre.GeneralScore, row.Cell(5).GetValue<int>());
            Assert.Equal(cadre.TasksScore, row.Cell(6).GetValue<int>());
            Assert.Equal("Hoàn thành tốt nhiệm vụ", row.Cell(7).GetString());
            Assert.Equal("Hoàn thành tốt nhiệm vụ", row.Cell(8).GetString());
            Assert.Equal(string.Empty, row.Cell(9).GetString());
            // Cấp trực tiếp sử dụng: chỉ Phó Giám đốc (M14) có đề xuất trong bảng đã nộp; cột quyết định của cấp trên còn trống.
            Assert.Equal(cadre.ExpectedCode == "M14" ? "Hoàn thành tốt nhiệm vụ" : string.Empty, row.Cell(10).GetString());
            Assert.Equal(string.Empty, row.Cell(11).GetString());
        }
        Assert.Equal(new[] { "M14", "M14", "M14", "M15", "M6", "M9" }, rows.Select(r => r.Cell(4).GetString()).OrderBy(c => c, StringComparer.Ordinal));

        // Trang tính diện Đảng ủy cơ sở có đủ 48 người, không lẫn người diện cấp trên.
        var baseNames = book.Worksheet(1).RowsUsed().Where(r => r.RowNumber() > 10).Select(r => r.Cell(2).GetString())
            .Where(s.Names.ContainsKey).ToList();
        Assert.Equal(48, baseNames.Count);
        Assert.All(baseNames, n => Assert.Equal(ApprovalAuthority.CoSo, s.Names[n].Authority));
    }

    // ===================== Mẫu 16 =====================

    [SkippableFact]
    public async Task Form16_SectionsMatchQuarter3Submission()
    {
        SkipIfNoDatabase();
        var s = await GetScenarioAsync();

        var response = await s.Committee.GetAsync($"/api/reports/mau-16/draft?periodId={s.PeriodId}");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, text);
        using var json = JsonDocument.Parse(text);
        var data = json.RootElement.GetProperty("data");

        // Mục I (Đảng ủy cơ sở quyết định) = Mẫu 15B; mục II (đề nghị BTV cấp trên) = Mẫu 15A.
        AssertSection(data.GetProperty("baseRows"), Quarter3Structure.Form15B, 48);
        AssertSection(data.GetProperty("superiorRows"), Quarter3Structure.Form15A, 6);

        // Word: mỗi dòng có số liệu in đủ 5 mức, tỷ lệ "0,0%" và mã ở cột ghi chú.
        var rows = await DocxTableRowsAsync(s.Committee, $"/api/reports/docx/mau-16?periodId={s.PeriodId}");
        foreach (var (code, want) in Quarter3Structure.Form15B.Concat(Quarter3Structure.Form15A))
        {
            var row = rows.Single(r => r.Length >= 10 && r[9] == code);
            Assert.Equal(Hd03FormCatalog.StatCodeSubjects[code], row[1]);
            Assert.Equal(want.Select(n => n.ToString(CultureInfo.InvariantCulture)), row[2..8]);
            Assert.Equal("0,0%", row[8]);
        }
        Assert.Equal(2, rows.Count(r => r.Length >= 10 && r[1] == "Tổng cộng"));
    }

    /// <summary>
    /// Dòng có người khớp bảng đã nộp; phần mềm chỉ in mã có người (bảng đã nộp in đủ mọi mã, mã không có người ghi 0);
    /// dòng cuối "Tổng cộng".
    /// </summary>
    private static void AssertSection(JsonElement rows, IReadOnlyDictionary<string, int[]> expected, int total)
    {
        var list = rows.EnumerateArray().ToList();
        var coded = list.Take(list.Count - 1).ToList();
        Assert.Equal(expected.Keys.OrderBy(k => PositionRules.StatCodeOrder(k)), coded.Select(r => r.GetProperty("statCode").GetString()));
        foreach (var row in coded)
        {
            var want = expected[row.GetProperty("statCode").GetString()!];
            Assert.Equal(want, SummaryCounts(row));
            Assert.Equal(0.0, row.GetProperty("excellentPercent").GetDouble());
        }
        var sum = list[^1];
        Assert.Equal("Tổng cộng", sum.GetProperty("subject").GetString());
        Assert.Equal(new[] { total, 0, total, 0, 0, 0 }, SummaryCounts(sum));
    }

    private static int[] SummaryCounts(JsonElement row) =>
        new[] { "total", "excellent", "good", "satisfactory", "unsatisfactory", "notRated" }.Select(p => row.GetProperty(p).GetInt32()).ToArray();

    #region Dữ liệu

    private sealed class Scenario
    {
        public Guid PeriodId;
        public Dictionary<string, Quarter3Structure.Cadre> Names = new();
        public HttpClient Committee = null!;
    }

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

    /// <summary>
    /// Kỳ Quý III riêng với 54 hồ sơ dựng thẳng vào CSDL theo <see cref="Quarter3Structure"/>: chức vụ trong danh mục (kiêm
    /// nhiệm), diện suy ra từ chức vụ; diện cơ sở đã ghi quyết định của Đảng ủy, diện cấp trên chờ cấp trên quyết định.
    /// </summary>
    private async Task<Scenario> CreateScenarioAsync()
    {
        var s = new Scenario();
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        await _factory.WithDbAsync(async db =>
        {
            var root = new PartyCell { Code = $"Q3-R-{sfx}", Name = $"Đảng ủy Q3 {sfx}" };
            root.Path = OrgTree.BuildPath(null, root.Id);
            var cell = new PartyCell { Code = $"Q3-C-{sfx}", Name = $"Chi bộ Q3 {sfx}", ParentId = root.Id };
            cell.Path = OrgTree.BuildPath(root.Path, cell.Id);
            var dept = new AdministrativeDepartment { Code = $"Q3-D-{sfx}", Name = $"Phòng Q3 {sfx}" };
            dept.Path = OrgTree.BuildPath(null, dept.Id);
            var period = new EvaluationPeriod
            {
                Year = 3000 + Random.Shared.Next(0, 900), Quarter = EvaluationQuarter.Quy3, Name = $"Kỳ Q3 {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                CriteriaSnapshot = CriteriaTestData.Snapshot()
            };
            db.AddRange(root, cell, dept, period);

            var positions = new Dictionary<string, Position>();
            var sort = 0;
            foreach (var spec in Quarter3Structure.Positions)
            {
                var position = new Position
                {
                    Name = $"{spec.Name} Q3 {sfx}", StatCode = spec.StatCode, DefaultApprovalAuthority = spec.Authority,
                    Side = spec.Side, IsLeadership = spec.StatCode != null, SortOrder = ++sort
                };
                positions[spec.Key] = position;
                db.Add(position);
            }

            var from = DateTime.UtcNow.AddYears(-1);
            var now = DateTime.UtcNow;
            var index = 0;
            foreach (var cadre in Quarter3Structure.Cadres)
            {
                index++;
                var held = cadre.Positions.Select(k => positions[k]).ToList();
                var authority = PositionRules.DeriveApprovalAuthority(
                    held.Select(p => new HeldPosition(p.Name, p.StatCode, p.DefaultApprovalAuthority)));
                var name = $"Cán bộ Q3 {index:D2} {sfx}";
                var member = new PartyMemberProfile
                {
                    Username = $"q3_{sfx.ToLowerInvariant()}_{index:D2}", FullName = name, PasswordHash = "khong-dang-nhap",
                    IsActive = true, DepartmentId = dept.Id, PartyCellId = authority == ApprovalAuthority.CapTren ? root.Id : cell.Id,
                    ApprovalAuthority = authority
                };
                db.Add(member);
                for (var i = 0; i < held.Count; i++)
                    db.Add(new MemberPosition { UserId = member.Id, PositionId = held[i].Id, IsPrimary = i == 0, ValidFrom = from });

                var decided = cadre.Final != EvaluationGrade.ChuaXepLoai;
                db.Add(new EvaluationRecord
                {
                    PeriodId = period.Id, MemberId = member.Id, DepartmentId = dept.Id, PartyCellId = member.PartyCellId,
                    ApprovalAuthority = authority,
                    Status = decided ? RecordStatus.Published : RecordStatus.AwaitingDecision,
                    WeightFrameCode = "K2", GeneralCriteriaScore = cadre.GeneralScore, TasksScore = cadre.TasksScore,
                    TotalSelfScore = cadre.GeneralScore + cadre.TasksScore, SelfScoredAt = now,
                    SelfProposedGrade = EvaluationGrade.HoanThanhTot, CollectiveProposedGrade = cadre.Collective,
                    AppraisalProposedGrade = cadre.Appraisal, DirectorProposedGrade = cadre.Director, FinalGrade = cadre.Final,
                    DecisionRecordedAt = decided ? now : null, PublishedAt = decided ? now : null, UpdatedAt = now
                });
                s.Names[name] = cadre;
            }

            await db.SaveChangesAsync();
            s.PeriodId = period.Id;
        });

        var committee = await _factory.CreateUserWithPermissionsAsync(new[] { PermissionCodes.ReportExport });
        s.Committee = await _factory.LoginAsAsync(committee.Username, committee.Password, distinctClientIp: true);
        return s;
    }

    #endregion

    #region Hỗ trợ

    private static string ExpectedPercent(int[] counts)
    {
        var goodOrBetter = counts[1] + counts[2];
        return goodOrBetter == 0
            ? string.Empty
            : Math.Round((double)counts[1] / goodOrBetter * 100, 1).ToString("0.0", CultureInfo.GetCultureInfo("vi-VN")) + "%";
    }

    private static async Task<XLWorkbook> WorkbookAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Tổng số và 5 mức (cột 4–9).</summary>
    private static int[] Counts(IXLRow row) => Enumerable.Range(4, 6).Select(c => row.Cell(c).GetValue<int>()).ToArray();

    /// <summary>Các dòng bảng của tệp Word: mỗi dòng là mảng chữ của từng ô (đã cắt khoảng trắng).</summary>
    private static async Task<List<string[]>> DocxTableRowsAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var doc = WordprocessingDocument.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), false);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<WordTableRow>()
            .Select(r => r.Elements<WordTableCell>().Select(c => c.InnerText.Trim()).ToArray())
            .ToList();
    }

    #endregion
}
