using System.Reflection;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Reports;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Trần tỷ lệ Xuất sắc: nhóm chức danh, loại người đứng đầu, mức dùng để tính, làm tròn, đơn vị theo ảnh chụp.</summary>
public class ExcellentQuotaTests
{
    private const EvaluationGrade XS = EvaluationGrade.HoanThanhXuatSac;
    private const EvaluationGrade Tot = EvaluationGrade.HoanThanhTot;
    private const EvaluationGrade HT = EvaluationGrade.HoanThanh;

    private static ExcellentQuotaEntry E(ApprovalAuthority a, string? code, EvaluationGrade g, bool head = false) => new(a, code, head, g);

    [Fact]
    public void Compute_GroupsByAuthorityAndStatCode_NotByCell()
    {
        // 8 "Hoàn thành tốt" M26 cơ sở (thuộc nhiều Chi bộ khác nhau — không quan trọng) → trần 2; 3 Xuất sắc → vượt 1.
        var entries = Enumerable.Repeat(E(ApprovalAuthority.CoSo, "M26", Tot), 8)
            .Concat(Enumerable.Repeat(E(ApprovalAuthority.CoSo, "M26", XS), 3))
            .Append(E(ApprovalAuthority.CoSo, "M22", XS))
            .Append(E(ApprovalAuthority.CapTren, "M14", Tot))
            .Append(E(ApprovalAuthority.CapTren, "M26", Tot)) // cùng mã nhưng khác cấp quyết định → nhóm riêng
            .Append(E(ApprovalAuthority.CoSo, null, HT))
            .ToList();

        var groups = ExcellentQuotaCalculator.Compute(entries, 0.20);

        Assert.Equal(
            new (ApprovalAuthority, string?)[]
            {
                (ApprovalAuthority.CapTren, "M14"), (ApprovalAuthority.CapTren, "M26"),
                (ApprovalAuthority.CoSo, "M22"), (ApprovalAuthority.CoSo, "M26"), (ApprovalAuthority.CoSo, null)
            },
            groups.Select(g => (g.Authority, g.StatCode)));
        var m26 = groups.Single(g => g.Authority == ApprovalAuthority.CoSo && g.StatCode == "M26");
        Assert.Equal((11, 8, 3, 2, true, 1), (m26.Total, m26.Good, m26.Excellent, m26.MaxExcellent, m26.IsExceeding, m26.Excess));
        Assert.Equal(37.5, m26.ExcellentPercentOfGood);

        // Nhóm không có ai "Hoàn thành tốt": trần 0 → có Xuất sắc là vượt trần.
        var m22 = groups.Single(g => g.StatCode == "M22");
        Assert.Equal((0, 1, 0, true), (m22.Good, m22.Excellent, m22.MaxExcellent, m22.IsExceeding));
        Assert.Null(m22.ExcellentPercentOfGood);
        Assert.False(groups.Single(g => g.StatCode == null).IsExceeding);
    }

    [Fact]
    public void Compute_ExcludesHeads_FromNumeratorAndDenominator()
    {
        var entries = Enumerable.Repeat(E(ApprovalAuthority.CoSo, "M26", Tot), 5)
            .Append(E(ApprovalAuthority.CoSo, "M26", XS))
            .Append(E(ApprovalAuthority.CoSo, "M26", XS, head: true))
            .Append(E(ApprovalAuthority.CoSo, "M26", Tot, head: true))
            .Append(E(ApprovalAuthority.CoSo, "M26", Tot, head: true))
            .ToList();

        var g = Assert.Single(ExcellentQuotaCalculator.Compute(entries, 0.20));
        // Không kể người đứng đầu: 5 tốt → trần 1, 1 xuất sắc → trong giới hạn. Nếu đếm cả người đứng đầu: 7 tốt → 1, 2 XS → vượt.
        Assert.Equal((9, 3, 5, 1, 1, false), (g.Total, g.Heads, g.Good, g.Excellent, g.MaxExcellent, g.IsExceeding));
    }

    [Theory]
    [InlineData(7, 0.20, 1)]
    [InlineData(8, 0.20, 2)]
    [InlineData(12, 0.20, 2)]
    [InlineData(13, 0.20, 3)]
    [InlineData(10, 0.25, 3)]
    [InlineData(2, 0.25, 1)]
    [InlineData(1, 0.25, 0)]
    public void Compute_RoundsHalfUp(int good, double ratio, int expected)
    {
        var g = Assert.Single(ExcellentQuotaCalculator.Compute(Enumerable.Repeat(E(ApprovalAuthority.CoSo, "M26", Tot), good), ratio));
        Assert.Equal(expected, g.MaxExcellent);
    }

    [Fact]
    public void GradeFor_UsesDecision_ThenLatestProposalInStepOrder()
    {
        var r = new EvaluationRecord();
        Assert.Equal(EvaluationGrade.ChuaXepLoai, ExcellentQuotaCalculator.GradeFor(r));

        // Tự đề xuất chỉ tính khi đã nộp phiếu.
        r.SelfProposedGrade = XS;
        Assert.Equal(EvaluationGrade.ChuaXepLoai, ExcellentQuotaCalculator.GradeFor(r));
        r.SelfScoredAt = DateTime.UtcNow;
        Assert.Equal(XS, ExcellentQuotaCalculator.GradeFor(r));

        r.CollectiveProposedGrade = Tot;
        Assert.Equal(Tot, ExcellentQuotaCalculator.GradeFor(r));
        r.AppraisalProposedGrade = XS;
        Assert.Equal(XS, ExcellentQuotaCalculator.GradeFor(r));
        r.DirectorProposedGrade = HT;
        Assert.Equal(HT, ExcellentQuotaCalculator.GradeFor(r));

        r.FinalGrade = Tot;
        r.DecisionRecordedAt = DateTime.UtcNow;
        Assert.Equal(Tot, ExcellentQuotaCalculator.GradeFor(r));

        // Đã ghi quyết định "Chưa xếp loại" → không tính theo đề xuất.
        r.FinalGrade = EvaluationGrade.ChuaXepLoai;
        Assert.Equal(EvaluationGrade.ChuaXepLoai, ExcellentQuotaCalculator.GradeFor(r));
    }

    [Fact]
    public void EntryFor_UsesPriorStatCode_AndHeadFlagOfAnyHeldPosition()
    {
        var r = new EvaluationRecord { ApprovalAuthority = ApprovalAuthority.CapTren, AppraisalProposedGrade = XS };
        var held = new[]
        {
            new HeldPosition("Giám đốc", "M14", ApprovalAuthority.CapTren, IsHead: true),
            new HeldPosition("Phó Bí thư Đảng ủy", "M9", ApprovalAuthority.CapTren)
        };
        Assert.Equal(new ExcellentQuotaEntry(ApprovalAuthority.CapTren, "M9", true, XS), ExcellentQuotaCalculator.EntryFor(r, held));
        Assert.Equal(new ExcellentQuotaEntry(ApprovalAuthority.CapTren, null, false, XS), ExcellentQuotaCalculator.EntryFor(r, null));
    }

    [Fact]
    public async Task Service_UsesRecordSnapshotCell_AndCountsEachRecordOnce()
    {
        var cellA = Guid.NewGuid();
        var cellB = Guid.NewGuid();
        var period = new EvaluationPeriod { Id = Guid.NewGuid(), EndDate = DateTime.UtcNow.AddDays(30) };
        // Cán bộ đã chuyển sang Chi bộ B nhưng hồ sơ chụp Chi bộ A.
        var records = Enumerable.Range(0, 5).Select(i => new EvaluationRecord
        {
            PeriodId = period.Id,
            MemberId = Guid.NewGuid(),
            Member = new PartyMemberProfile { PartyCellId = cellB },
            PartyCellId = cellA,
            ApprovalAuthority = ApprovalAuthority.CoSo,
            AppraisalProposedGrade = i == 0 ? XS : Tot
        }).ToList();
        var held = records.ToDictionary(r => r.MemberId, _ => new List<HeldPosition> { new("Phó Trưởng phòng", "M26", ApprovalAuthority.CoSo) });

        var repo = Stub<IEvaluationWorkflowRepository>.Create(new()
        {
            ["FindPeriodAsync"] = _ => Task.FromResult<EvaluationPeriod?>(period),
            ["ListRecordsAsync"] = _ => Task.FromResult(records.ToList()),
            ["PartyCellExistsAsync"] = _ => Task.FromResult(true)
        });
        var positions = Stub<IPositionRepository>.Create(new()
        {
            ["GetHeldPositionsAsync"] = _ => Task.FromResult(held),
            ["GetOrgPathsAsync"] = _ => Task.FromResult(new OrgPaths(
                new Dictionary<Guid, string>(),
                new Dictionary<Guid, string> { [cellA] = OrgTree.BuildPath(null, cellA), [cellB] = OrgTree.BuildPath(null, cellB) }))
        });
        var service = new EvaluationService(repo, positions, new NoUser(), new GlobalReadGuard());

        var all = await service.CheckExcellentQuotasAsync(period.Id, null);
        var group = Assert.Single(all.Groups);
        Assert.Equal((5, 4, 1, 1, false), (group.TotalCadres, group.GoodCount, group.ExcellentCount, group.MaxExcellentAllowed, group.IsExceedingQuota));
        Assert.True(all.IsFullScope);
        Assert.Equal(0.20, all.Ratio);

        Assert.Equal(5, (await service.CheckExcellentQuotasAsync(period.Id, cellA)).Groups.Sum(g => g.TotalCadres));
        var byCurrentCell = await service.CheckExcellentQuotasAsync(period.Id, cellB);
        Assert.Empty(byCurrentCell.Groups);
        Assert.False(byCurrentCell.IsFullScope);
    }

    [Fact]
    public async Task Service_RequiresPeriod()
    {
        var service = new EvaluationService(
            Stub<IEvaluationWorkflowRepository>.Create(new()), Stub<IPositionRepository>.Create(new()), new NoUser(), new GlobalReadGuard());
        await Assert.ThrowsAsync<ValidationException>(() => service.CheckExcellentQuotasAsync(Guid.Empty, null));
    }

    private sealed class GlobalReadGuard : IAuthorizationGuard
    {
        public bool Can(string permission, AccessTarget target) => true;
        public void Ensure(string permission, AccessTarget target) { }
        public bool HasAny(string permission) => true;
        public ScopeFilter GetScope(string permission) => ScopeFilter.Global;
    }

    private sealed class NoUser : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public string UserName => "nguoi-xem";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    /// <summary>Bản giả của interface: chỉ các phương thức được khai báo trả kết quả, phương thức khác báo lỗi.</summary>
    public class Stub<T> : DispatchProxy where T : class
    {
        private Dictionary<string, Func<object?[]?, object?>> _handlers = new();

        public static T Create(Dictionary<string, Func<object?[]?, object?>> handlers)
        {
            var proxy = Create<T, Stub<T>>();
            ((Stub<T>)(object)proxy)._handlers = handlers;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod != null && _handlers.TryGetValue(targetMethod.Name, out var handler)
                ? handler(args)
                : throw new NotSupportedException($"Bản giả không hỗ trợ {typeof(T).Name}.{targetMethod?.Name}.");
    }
}
