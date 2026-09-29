using System.Net;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Tích hợp đợt 8: cảnh báo "Kế hoạch 30-60-90 ngày cần lập" chỉ xét kỳ đang mở/khóa dữ liệu và kỳ đã đóng trong số ngày cảnh báo
/// của bộ tiêu chí (tham số <c>improvementPlanAlertDays</c>, mặc định 90) — không quét mọi kỳ cũ.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class Wave8IntegrationTests
{
    private readonly ApiFactory _factory;

    public Wave8IntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task W1_ImprovementPlanAlert_OpenLockedAndRecentlyClosedPeriodsOnly()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var baseYear = 2100 + Random.Shared.Next(0, 700);

        Guid deptId = Guid.Empty, cellId = Guid.Empty;
        await _factory.WithDbAsync(async db =>
        {
            var dept = new AdministrativeDepartment { Code = $"W8-D-{sfx}", Name = $"Phòng W8 {sfx}" };
            dept.Path = OrgTree.BuildPath(null, dept.Id);
            var cell = new PartyCell { Code = $"W8-C-{sfx}", Name = $"Chi bộ W8 {sfx}" };
            cell.Path = OrgTree.BuildPath(null, cell.Id);
            db.AddRange(dept, cell);
            await db.SaveChangesAsync();
            (deptId, cellId) = (dept.Id, cell.Id);
        });

        // Kỳ: đang mở, khóa dữ liệu, đóng 10 ngày trước, đóng 200 ngày trước (mặc định 90 ngày), đóng 200 ngày trước nhưng bộ
        // tiêu chí của kỳ đặt cửa sổ 365 ngày.
        var cases = new (string Key, PeriodStatus Status, int ClosedDaysAgo, int? AlertDays, bool Expected)[]
        {
            ("open", PeriodStatus.Open, 0, null, true),
            ("locked", PeriodStatus.Locked, 0, null, true),
            ("recent", PeriodStatus.Closed, 10, null, true),
            ("old", PeriodStatus.Closed, 200, null, false),
            ("old365", PeriodStatus.Closed, 200, 365, true)
        };
        var names = new Dictionary<string, string>();
        for (var i = 0; i < cases.Length; i++)
        {
            var c = cases[i];
            names[c.Key] = $"W8 {c.Key} {sfx}";
            var owner = await _factory.CreateUserAsync(deptId, cellId, fullName: names[c.Key]);
            await _factory.WithDbAsync(async db =>
            {
                var snapshot = CriteriaSnapshot.Parse(CriteriaTestData.Snapshot("09B"))!;
                if (c.AlertDays is { } days)
                    snapshot.Content.Parameters.ImprovementPlanAlertDays = days;
                var period = new EvaluationPeriod
                {
                    Year = baseYear + i, Quarter = EvaluationQuarter.Quy2, Name = $"Kỳ W8 {c.Key} {sfx}",
                    StartDate = DateTime.UtcNow.AddDays(-300), EndDate = DateTime.UtcNow.AddDays(-210), Status = c.Status,
                    StatusChangedAt = DateTime.UtcNow.AddDays(-c.ClosedDaysAgo), CriteriaSnapshot = snapshot.ToJson()
                };
                db.Add(period);
                db.Add(new EvaluationRecord
                {
                    PeriodId = period.Id, MemberId = owner.Id, DepartmentId = deptId, PartyCellId = cellId, ApprovalAuthority = ApprovalAuthority.CoSo,
                    Status = RecordStatus.Published, WeightFrameCode = "K2", FinalGrade = EvaluationGrade.HoanThanh, FinalScore = 60,
                    PublishedAt = DateTime.UtcNow.AddDays(-c.ClosedDaysAgo - 1), UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            });
        }

        var manager = await _factory.CreateUserWithPermissionsAsync(PermissionCodes.EvaluationImprovementManage, PermissionCodes.EvaluationRead);
        using var client = await _factory.LoginAsAsync(manager.Username, manager.Password, distinctClientIp: true);
        var response = await client.GetAsync("/api/evaluations/work-queue");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var listed = json.RootElement.GetProperty("data").GetProperty("groups").EnumerateArray()
            .Where(g => g.GetProperty("step").GetString() == "IMPROVEMENT_PLANS")
            .SelectMany(g => g.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("fullName").GetString()))
            .ToHashSet();

        foreach (var c in cases)
            Assert.True(listed.Contains(names[c.Key]) == c.Expected, $"{c.Key}: mong đợi {(c.Expected ? "có" : "không có")} trong nhóm kế hoạch cần lập");
    }
}
