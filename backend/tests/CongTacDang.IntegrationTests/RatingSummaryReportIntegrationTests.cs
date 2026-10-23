using System.Net;
using ClosedXML.Excel;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;
using Position = CongTacDang.Domain.Entities.Position;

namespace CongTacDang.IntegrationTests;

/// <summary>Bảng tổng hợp xếp loại của kỳ: hai khối theo cấp quyết định, lọc theo phạm vi <c>report.export</c>.</summary>
[Collection(ApiCollection.Name)]
public sealed class RatingSummaryReportIntegrationTests
{
    private const string CapTrenTitle = "THUỘC DIỆN BAN THƯỜNG VỤ ĐẢNG ỦY TCT ĐÁNH GIÁ, XẾP LOẠI";
    private const string CoSoTitle = "THUỘC DIỆN BAN CHẤP HÀNH ĐẢNG BỘ CÔNG TY ĐÁNH GIÁ, XẾP LOẠI";

    private readonly ApiFactory _factory;

    public RatingSummaryReportIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    [SkippableFact]
    public async Task RatingSummary_TwoBlocks_CodesGradesAttachments_FilteredByScope()
    {
        SkipIfNoDatabase();
        var s = await CreateScenarioAsync();
        var url = $"/api/reports/internal/rating-summary?periodId={s.PeriodId}";

        // Toàn công ty: khối cấp trên 1 người, khối cơ sở 4 người (thứ tự mã M22, M26, M26, chưa có mã).
        using (var book = await WorkbookAsync(s.Committee, url))
        {
            var sheet = book.Worksheet(1);
            Assert.Equal("BẢNG TỔNG HỢP XẾP LOẠI CÁN BỘ", sheet.Cell(3, 1).GetString());

            var capTren = Block(sheet, CapTrenTitle);
            Assert.Equal("Ban Thường vụ Đảng ủy TCT xếp loại", sheet.Cell(capTren.HeaderRow, 12).GetString());
            Assert.Equal("Mã hiệu", sheet.Cell(capTren.HeaderRow, 2).GetString());
            Assert.Equal("(13)", sheet.Cell(capTren.HeaderRow + 1, 13).GetString());
            var c1 = Assert.Single(capTren.Rows);
            Assert.Equal(s.NameC1, c1.Cell(3).GetString());
            Assert.Equal("M8", c1.Cell(2).GetString());
            Assert.Equal(28, c1.Cell(5).GetValue<double>());
            Assert.Equal(60, c1.Cell(6).GetValue<double>());
            Assert.Equal(88, c1.Cell(7).GetValue<double>());
            Assert.Equal("Hoàn thành tốt nhiệm vụ", c1.Cell(8).GetString());
            Assert.Equal("Hoàn thành tốt nhiệm vụ", c1.Cell(9).GetString());
            Assert.Equal("Hoàn thành xuất sắc nhiệm vụ", c1.Cell(10).GetString());
            Assert.Equal("Hoàn thành tốt nhiệm vụ", c1.Cell(11).GetString());
            Assert.Equal("Hoàn thành tốt nhiệm vụ", c1.Cell(12).GetString());

            var coSo = Block(sheet, CoSoTitle);
            Assert.Equal("Ban Chấp hành Đảng bộ Công ty xếp loại", sheet.Cell(coSo.HeaderRow, 12).GetString());
            Assert.Equal(new[] { "M22", "M26", "M26", "" }, coSo.Rows.Select(r => r.Cell(2).GetString()).ToArray());
            Assert.Equal(new[] { 1, 2, 3, 4 }, coSo.Rows.Select(r => r.Cell(1).GetValue<int>()).ToArray());
            Assert.Equal(s.NoCodeName, coSo.Rows[^1].Cell(3).GetString());

            // Chưa tự chấm, chưa có bước nào → các cột điểm, mức để trống.
            var a2 = coSo.Rows.Single(r => r.Cell(3).GetString() == s.NameA2);
            for (var c = 5; c <= 13; c++)
                Assert.True(a2.Cell(c).IsEmpty(), $"Cột {c} phải để trống");

            // Bước thẩm định do cấp trên thực hiện: mức ghi nhận; chưa quyết định → cột (12) trống.
            var b1 = coSo.Rows.Single(r => r.Cell(3).GetString() == s.NameB1);
            Assert.Equal("Hoàn thành nhiệm vụ", b1.Cell(10).GetString());
            Assert.True(b1.Cell(12).IsEmpty());
            // 2 phiên bản của một tệp (tính 1) + văn bản của cấp trên = 2 tệp.
            Assert.Equal("2 tệp đính kèm", b1.Cell(13).GetString());
            Assert.Contains($"Phòng RS2 {s.Suffix}", b1.Cell(4).GetString());
        }

        // Phạm vi đơn vị chính quyền D2: chỉ hồ sơ b1; khối cấp trên không có cán bộ.
        using (var book = await WorkbookAsync(s.DeptReporter, url))
        {
            var sheet = book.Worksheet(1);
            Assert.Empty(Block(sheet, CapTrenTitle).Rows);
            var only = Assert.Single(Block(sheet, CoSoTitle).Rows);
            Assert.Equal(s.NameB1, only.Cell(3).GetString());
        }

        // Phạm vi Chi bộ A: không thấy b1 (Chi bộ B); chọn Chi bộ B → 403.
        using (var book = await WorkbookAsync(s.CellLeaderA, url))
        {
            var names = Block(book.Worksheet(1), CoSoTitle).Rows.Select(r => r.Cell(3).GetString()).ToList();
            Assert.Equal(3, names.Count);
            Assert.DoesNotContain(s.NameB1, names);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await s.CellLeaderA.GetAsync($"{url}&branchId={s.CellB}")).StatusCode);

        // Toàn công ty chọn Chi bộ B → chỉ b1.
        using (var book = await WorkbookAsync(s.Committee, $"{url}&branchId={s.CellB}"))
            Assert.Equal(s.NameB1, Assert.Single(Block(book.Worksheet(1), CoSoTitle).Rows).Cell(3).GetString());

        // Không có quyền report.export → 403.
        Assert.Equal(HttpStatusCode.Forbidden, (await s.Owner.GetAsync(url)).StatusCode);
    }

    #region Dữ liệu

    private sealed class Scenario
    {
        public Guid PeriodId, CellA, CellB;
        public string Suffix = string.Empty, NameC1 = string.Empty, NameA2 = string.Empty, NameB1 = string.Empty, NoCodeName = string.Empty;
        public HttpClient Committee = null!, DeptReporter = null!, CellLeaderA = null!, Owner = null!;
    }

    /// <summary>
    /// Kỳ có 5 hồ sơ: Chi bộ A / Phòng 1 — c1 (M8, cấp trên, đủ các bước, đã quyết định), a1 (M22), a2 (M26, chưa tự chấm),
    /// n1 (chưa có chức vụ); Chi bộ B / Phòng 2 — b1 (M26, thẩm định do cấp trên ghi nhận, có tệp đính kèm).
    /// </summary>
    private async Task<Scenario> CreateScenarioAsync()
    {
        var s = new Scenario();
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        s.Suffix = sfx;
        s.NameC1 = $"RS C1 {sfx}";
        s.NameA2 = $"RS A2 {sfx}";
        s.NameB1 = $"RS B1 {sfx}";
        s.NoCodeName = $"RS N1 {sfx}";

        Guid dept1 = Guid.Empty, dept2 = Guid.Empty;
        var positions = new Dictionary<string, Guid>();
        await _factory.WithDbAsync(async db =>
        {
            var root = new PartyCell { Code = $"RS-R-{sfx}", Name = $"Đảng ủy RS {sfx}" };
            root.Path = OrgTree.BuildPath(null, root.Id);
            var cellA = new PartyCell { Code = $"RS-A-{sfx}", Name = $"Chi bộ RSA {sfx}", ParentId = root.Id };
            cellA.Path = OrgTree.BuildPath(root.Path, cellA.Id);
            var cellB = new PartyCell { Code = $"RS-B-{sfx}", Name = $"Chi bộ RSB {sfx}", ParentId = root.Id };
            cellB.Path = OrgTree.BuildPath(root.Path, cellB.Id);
            var d1 = new AdministrativeDepartment { Code = $"RS-D1-{sfx}", Name = $"Phòng RS1 {sfx}" };
            d1.Path = OrgTree.BuildPath(null, d1.Id);
            var d2 = new AdministrativeDepartment { Code = $"RS-D2-{sfx}", Name = $"Phòng RS2 {sfx}" };
            d2.Path = OrgTree.BuildPath(null, d2.Id);
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy3, Name = $"Kỳ RS {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                CriteriaSnapshot = CriteriaTestData.Snapshot("09B")
            };
            foreach (var (code, authority) in new[] { ("M8", ApprovalAuthority.CapTren), ("M22", ApprovalAuthority.CoSo), ("M26", ApprovalAuthority.CoSo) })
            {
                var position = new Position
                {
                    Name = $"RS {code} {sfx}", StatCode = code, DefaultApprovalAuthority = authority, IsLeadership = true,
                    Side = code == "M26" ? PositionSide.Administrative : PositionSide.Party
                };
                db.Add(position);
                positions[code] = position.Id;
            }
            db.AddRange(root, cellA, cellB, d1, d2, period);
            await db.SaveChangesAsync();
            (dept1, dept2, s.CellA, s.CellB, s.PeriodId) = (d1.Id, d2.Id, cellA.Id, cellB.Id, period.Id);
        });

        var c1 = await _factory.CreateUserAsync(dept1, s.CellA, ApprovalAuthority.CapTren, s.NameC1);
        var a1 = await _factory.CreateUserAsync(dept1, s.CellA, fullName: $"RS A1 {sfx}");
        var a2 = await _factory.CreateUserAsync(dept1, s.CellA, fullName: s.NameA2);
        var n1 = await _factory.CreateUserAsync(dept1, s.CellA, fullName: s.NoCodeName);
        var b1 = await _factory.CreateUserAsync(dept2, s.CellB, fullName: s.NameB1);

        await _factory.WithDbAsync(async db =>
        {
            var from = DateTime.UtcNow.AddYears(-1);
            foreach (var (user, code) in new[] { (c1, "M8"), (a1, "M22"), (a2, "M26"), (b1, "M26") })
                db.Add(new MemberPosition { UserId = user.Id, PositionId = positions[code], IsPrimary = true, ValidFrom = from });

            EvaluationRecord Record(TestUser owner, Guid dept, Guid cell, ApprovalAuthority authority, bool selfScored) => new()
            {
                PeriodId = s.PeriodId, MemberId = owner.Id, DepartmentId = dept, PartyCellId = cell, ApprovalAuthority = authority,
                Status = RecordStatus.AwaitingDecision, WeightFrameCode = "K2",
                GeneralCriteriaScore = selfScored ? 28 : 0, TasksScore = selfScored ? 60 : 0, TotalSelfScore = selfScored ? 88 : 0,
                SelfScoredAt = selfScored ? DateTime.UtcNow : null,
                SelfProposedGrade = selfScored ? EvaluationGrade.HoanThanhTot : EvaluationGrade.ChuaXepLoai, UpdatedAt = DateTime.UtcNow
            };

            var rc1 = Record(c1, dept1, s.CellA, ApprovalAuthority.CapTren, true);
            rc1.CollectiveProposedGrade = EvaluationGrade.HoanThanhTot;
            rc1.AppraisalProposedGrade = EvaluationGrade.HoanThanhXuatSac;
            rc1.DirectorProposedGrade = EvaluationGrade.HoanThanhTot;
            rc1.FinalGrade = EvaluationGrade.HoanThanhTot;
            rc1.DecisionRecordedAt = DateTime.UtcNow;
            rc1.Status = RecordStatus.Published;

            var rb1 = Record(b1, dept2, s.CellB, ApprovalAuthority.CoSo, true);
            rb1.AppraisalProposedGrade = EvaluationGrade.HoanThanh;
            rb1.AppraisedByName = "Cơ quan cấp trên";

            db.AddRange(rc1, Record(a1, dept1, s.CellA, ApprovalAuthority.CoSo, true), Record(a2, dept1, s.CellA, ApprovalAuthority.CoSo, false),
                Record(n1, dept1, s.CellA, ApprovalAuthority.CoSo, true), rb1);

            // Tệp của hồ sơ b1: một tệp hai phiên bản (cũ đã bị thay) và văn bản của cấp trên gắn vào kết quả thẩm định.
            var groupId = Guid.NewGuid();
            TaskAttachment File(string name, Guid? group, bool superseded, string? ownerType, Guid? ownerId) => new()
            {
                FileName = name, OriginalFileName = name, ContentType = "application/pdf", ObjectKey = $"it/{Guid.NewGuid():N}",
                FormCode = "IT", FileGroupId = group, IsSuperseded = superseded, OwnerType = ownerType, OwnerId = ownerId,
                RecordId = ownerType == AttachmentOwnerTypes.EvaluationRecord ? ownerId : null
            };
            var external = File("van-ban-cap-tren.pdf", null, false, null, null);
            db.AddRange(
                File("minh-chung-v1.pdf", groupId, true, AttachmentOwnerTypes.EvaluationRecord, rb1.Id),
                File("minh-chung-v2.pdf", groupId, false, AttachmentOwnerTypes.EvaluationRecord, rb1.Id),
                external);
            db.Add(new EvaluationExternalResult
            {
                RecordId = rb1.Id, Step = WorkflowStep.B3B_APPRAISAL, AuthorityName = "Cơ quan cấp trên",
                Grade = EvaluationGrade.HoanThanh, AttachmentId = external.Id
            });
            await db.SaveChangesAsync();
        });

        var committee = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.ReportExport);
        var role = await _factory.CreateRoleAsync(PermissionCodes.ReportExport);
        var deptReporter = await _factory.CreateUserAsync();
        var leaderA = await _factory.CreateUserAsync();
        await _factory.AssignAsync(deptReporter.Id, role.Id, RoleScopeType.Department, dept2);
        await _factory.AssignAsync(leaderA.Id, role.Id, RoleScopeType.PartyCell, s.CellA);

        s.Committee = await _factory.LoginAsAsync(committee.Username, committee.Password, distinctClientIp: true);
        s.DeptReporter = await _factory.LoginAsAsync(deptReporter.Username, deptReporter.Password, distinctClientIp: true);
        s.CellLeaderA = await _factory.LoginAsAsync(leaderA.Username, leaderA.Password, distinctClientIp: true);
        s.Owner = await _factory.LoginAsAsync(a1.Username, a1.Password, distinctClientIp: true);
        return s;
    }

    #endregion

    #region Hỗ trợ

    private static async Task<XLWorkbook> WorkbookAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    /// <summary>Dòng tên cột và các dòng cán bộ của một khối (từ sau dòng số cột đến dòng có cột STT không phải số).</summary>
    private static (int HeaderRow, List<IXLRow> Rows) Block(IXLWorksheet sheet, string title)
    {
        var titleRow = sheet.RowsUsed().Single(r => r.Cell(1).GetString() == title).RowNumber();
        var rows = new List<IXLRow>();
        for (var n = titleRow + 3; sheet.Cell(n, 1).TryGetValue<int>(out _) && !sheet.Cell(n, 1).IsEmpty(); n++)
            rows.Add(sheet.Row(n));
        return (titleRow + 1, rows);
    }

    #endregion
}
