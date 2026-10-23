using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Trần tỷ lệ Hoàn thành xuất sắc trên PostgreSQL thật: <c>GET /api/evaluations/excellent-quotas</c> tính theo nhóm chức danh
/// trong cấp quyết định, không tính người đứng đầu, đơn vị theo ảnh chụp trên hồ sơ, lọc theo phạm vi người xem; báo cáo nội bộ
/// cảnh báo vượt trần.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ExcellentQuotaIntegrationTests
{
    private readonly ApiFactory _factory;

    public ExcellentQuotaIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task ExcellentQuotas_GroupByPositionAndAuthority_ExcludeHeads_UseRecordSnapshotCell()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        Guid periodId = Guid.Empty, cellA = Guid.Empty, cellB = Guid.Empty, dept = Guid.Empty;
        Guid headPos = Guid.Empty, deputyPos = Guid.Empty, directorPos = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            var d = new AdministrativeDepartment { Code = $"IT-XS-P-{sfx}", Name = $"Phòng trần {sfx}" };
            var a = new PartyCell { Code = $"IT-XS-A-{sfx}", Name = $"Chi bộ trần A {sfx}" };
            var b = new PartyCell { Code = $"IT-XS-B-{sfx}", Name = $"Chi bộ trần B {sfx}" };
            var period = new EvaluationPeriod
            {
                Year = 2033, Quarter = EvaluationQuarter.Quy2, Name = $"Kỳ trần xuất sắc {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-20), EndDate = DateTime.UtcNow.AddDays(60),
                Status = PeriodStatus.Open, CriteriaSnapshot = CriteriaTestData.Snapshot("09A")
            };
            var head = new Position { Name = $"Trưởng phòng trần {sfx}", Side = PositionSide.Administrative, StatCode = "M26", DefaultApprovalAuthority = ApprovalAuthority.CoSo, IsHead = true };
            var deputy = new Position { Name = $"Phó Trưởng phòng trần {sfx}", Side = PositionSide.Administrative, StatCode = "M26", DefaultApprovalAuthority = ApprovalAuthority.CoSo };
            var director = new Position { Name = $"Phó Giám đốc trần {sfx}", Side = PositionSide.Administrative, StatCode = "M14", DefaultApprovalAuthority = ApprovalAuthority.CapTren };
            db.AddRange(d, a, b, period, head, deputy, director);
            await db.SaveChangesAsync();
            (periodId, cellA, cellB, dept) = (period.Id, a.Id, b.Id, d.Id);
            (headPos, deputyPos, directorPos) = (head.Id, deputy.Id, director.Id);
        });

        // (chức vụ, Chi bộ hiện tại của cán bộ, Chi bộ chụp trên hồ sơ, cấp quyết định, mức).
        var specs = new List<(Guid Position, Guid CurrentCell, Guid SnapshotCell, ApprovalAuthority Authority, Action<EvaluationRecord> Grade)>();
        void Add(int count, Guid position, Guid current, Guid snapshot, ApprovalAuthority authority, Action<EvaluationRecord> grade)
        {
            for (var i = 0; i < count; i++)
                specs.Add((position, current, snapshot, authority, grade));
        }
        static Action<EvaluationRecord> Appraised(EvaluationGrade g) => r => r.AppraisalProposedGrade = g;

        Add(4, deputyPos, cellA, cellA, ApprovalAuthority.CoSo, Appraised(EvaluationGrade.HoanThanhTot));
        Add(4, deputyPos, cellB, cellB, ApprovalAuthority.CoSo, Appraised(EvaluationGrade.HoanThanhTot));
        Add(2, deputyPos, cellA, cellA, ApprovalAuthority.CoSo, Appraised(EvaluationGrade.HoanThanhXuatSac));
        // Đã chuyển sang Chi bộ B sau khi mở kỳ: vẫn thuộc Chi bộ A theo ảnh chụp, chỉ đếm một lần.
        Add(1, deputyPos, cellB, cellA, ApprovalAuthority.CoSo, Appraised(EvaluationGrade.HoanThanhXuatSac));
        // Người đứng đầu Xuất sắc: không tính.
        Add(2, headPos, cellA, cellA, ApprovalAuthority.CoSo, Appraised(EvaluationGrade.HoanThanhXuatSac));
        // Cấp trên quyết định: đã quyết định "Hoàn thành tốt" dù thẩm định đề xuất Xuất sắc → dùng mức quyết định.
        Add(1, directorPos, cellA, cellA, ApprovalAuthority.CapTren, r =>
        {
            r.AppraisalProposedGrade = EvaluationGrade.HoanThanhXuatSac;
            r.FinalGrade = EvaluationGrade.HoanThanhTot;
            r.DecisionRecordedAt = DateTime.UtcNow;
        });

        var members = new List<(TestUser User, (Guid Position, Guid CurrentCell, Guid SnapshotCell, ApprovalAuthority Authority, Action<EvaluationRecord> Grade) Spec)>();
        foreach (var spec in specs)
            members.Add((await _factory.CreateUserAsync(dept, spec.CurrentCell, spec.Authority), spec));
        await _factory.WithDbAsync(async db =>
        {
            foreach (var (user, spec) in members)
            {
                db.MemberPositions.Add(new MemberPosition
                {
                    UserId = user.Id, PositionId = spec.Position, DepartmentId = dept, IsPrimary = true, ValidFrom = DateTime.UtcNow.AddDays(-30)
                });
                var record = new EvaluationRecord
                {
                    PeriodId = periodId, MemberId = user.Id, DepartmentId = dept, PartyCellId = spec.SnapshotCell,
                    ApprovalAuthority = spec.Authority, Status = RecordStatus.AwaitingRegistration, UpdatedAt = DateTime.UtcNow, WeightFrameCode = "K2"
                };
                spec.Grade(record);
                db.EvaluationRecords.Add(record);
            }
            await db.SaveChangesAsync();
        });

        var global = await _factory.LoginAsAsync(await _factory.CreateUserWithPermissionsAsync("evaluation.read", "report.export"));
        var cellReader = await _factory.CreateUserAsync();
        await _factory.AssignAsync(cellReader.Id, (await _factory.CreateRoleAsync("evaluation.read")).Id, RoleScopeType.PartyCell, cellA);
        var cellClient = await _factory.LoginAsAsync(cellReader);
        var noRead = await _factory.LoginAsAsync(await _factory.CreateUserWithPermissionsAsync("report.export"));

        // Toàn bộ (phạm vi toàn Đảng bộ).
        var all = await Data(global, $"/api/evaluations/excellent-quotas?periodId={periodId}");
        Assert.True(all.GetProperty("isFullScope").GetBoolean());
        Assert.Equal(0.20, all.GetProperty("ratio").GetDouble(), 3);
        var groups = all.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(new (string?, string?)[] { ("CapTren", "M14"), ("CoSo", "M26") },
            groups.Select(g => (g.GetProperty("approvalAuthority").GetString(), g.GetProperty("statCode").GetString())));
        var m26 = groups[1];
        // 13 hồ sơ, 2 người đứng đầu; 8 tốt → trần 2 (1,6 → 2); 3 xuất sắc → vượt 1.
        Assert.Equal((13, 2, 8, 3, 2, true, 1), Counts(m26));
        var m14 = groups[0];
        Assert.Equal((1, 0, 1, 0, 0, false, 0), Counts(m14));
        Assert.Equal(1, all.GetProperty("exceedingGroupCount").GetInt32());

        // Chọn Chi bộ B: không có hồ sơ chụp Chi bộ A của người đã chuyển sang B.
        var byB = await Data(global, $"/api/evaluations/excellent-quotas?periodId={periodId}&partyCellId={cellB}");
        Assert.False(byB.GetProperty("isFullScope").GetBoolean());
        var groupB = Assert.Single(byB.GetProperty("groups").EnumerateArray().ToList());
        Assert.Equal((4, 0, 4, 0, 1, false, 0), Counts(groupB));

        // Người xem chỉ có phạm vi Chi bộ A: số liệu trong phạm vi được xem (4 tốt → trần 1; 3 xuất sắc → vượt 2).
        var scoped = await Data(cellClient, $"/api/evaluations/excellent-quotas?periodId={periodId}");
        Assert.False(scoped.GetProperty("isFullScope").GetBoolean());
        var scopedM26 = scoped.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("statCode").GetString() == "M26");
        Assert.Equal((9, 2, 4, 3, 1, true, 2), Counts(scopedM26));

        Assert.Equal(HttpStatusCode.Forbidden, (await noRead.GetAsync($"/api/evaluations/excellent-quotas?periodId={periodId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await global.GetAsync($"/api/evaluations/excellent-quotas?periodId={periodId}&partyCellId={Guid.NewGuid()}")).StatusCode);

        // Báo cáo nội bộ: một dòng mỗi nhóm, nhóm M26 cơ sở ghi "Vượt trần 1 người".
        var response = await global.GetAsync($"/api/reports/internal/excellent-quota?periodId={periodId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var rows = workbook.Worksheet(1).RowsUsed().Where(r => r.RowNumber() >= 8).ToList();
        var m26Row = rows.Single(r => r.Cell(3).GetString() == "M26");
        Assert.Equal("Vượt trần 1 người", m26Row.Cell(11).GetString());
        Assert.Equal(2, m26Row.Cell(9).GetValue<int>());
        Assert.Equal("Trong giới hạn", rows.Single(r => r.Cell(3).GetString() == "M14").Cell(11).GetString());
    }

    private static (int Total, int Heads, int Good, int Excellent, int Max, bool Exceeding, int Excess) Counts(JsonElement g) => (
        g.GetProperty("totalCadres").GetInt32(), g.GetProperty("headCount").GetInt32(), g.GetProperty("goodCount").GetInt32(),
        g.GetProperty("excellentCount").GetInt32(), g.GetProperty("maxExcellentAllowed").GetInt32(),
        g.GetProperty("isExceedingQuota").GetBoolean(), g.GetProperty("excessCount").GetInt32());

    private static async Task<JsonElement> Data(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").Clone();
    }
}
