using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Theo dõi hồ sơ tập thể: chỉ tính trạng thái, hạn là ngày 8 tháng cuối quý.</summary>
public class CollectiveTrackingTests
{
    private static readonly EvaluationPeriod Period = new() { Year = 2030, Quarter = EvaluationQuarter.Quy2 };
    private static readonly PartyCellMemberCount CellA = new(Guid.NewGuid(), "Chi bộ A", 3);
    private static readonly PartyCellMemberCount CellB = new(Guid.NewGuid(), "Chi bộ B", 5);

    private static CollectiveEvaluationRecord Record(Guid cellId, CollectiveEvaluationForm form, CollectiveRecordStatus status) =>
        new() { PartyCellId = cellId, Form = form, Status = status, CreatedAt = new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc) };

    [Fact]
    public void Deadline_IsDay8OfLastMonth_RolledToWorkingDay()
    {
        // 08/06/2030 là thứ Bảy → dời sang thứ Hai 10/06.
        Assert.Equal(new DateOnly(2030, 6, 10), CollectiveTrackingBuilder.Deadline(2030, 2));
        Assert.Equal(new DateOnly(2030, 3, 8), CollectiveTrackingBuilder.Deadline(2030, 1));
    }

    [Fact]
    public void Build_MarksMissingForms_CompleteAndOverdue()
    {
        var records = new[]
        {
            Record(CellA.PartyCellId, CollectiveEvaluationForm.M06, CollectiveRecordStatus.Submitted),
            Record(CellA.PartyCellId, CollectiveEvaluationForm.M07, CollectiveRecordStatus.Submitted),
            Record(CellA.PartyCellId, CollectiveEvaluationForm.M08, CollectiveRecordStatus.Submitted),
            Record(CellB.PartyCellId, CollectiveEvaluationForm.M07, CollectiveRecordStatus.Draft)
        };

        var result = CollectiveTrackingBuilder.Build(Period, new[] { CellA, CellB }, records, ScopeFilter.Global, new DateOnly(2030, 6, 20));

        Assert.Equal("2030-06-10", result.Deadline);
        // Tổ chức chưa đủ xếp trước.
        Assert.Equal(CellB.PartyCellId, result.Rows[0].PartyCellId);
        Assert.False(result.Rows[0].Complete);
        Assert.True(result.Rows[0].Overdue);
        Assert.Equal(new[] { "M06", "M07", "M08" }, result.Rows[0].Forms.Select(f => f.Form));
        Assert.Null(result.Rows[0].Forms[0].RecordId);
        Assert.Equal("Draft", result.Rows[0].Forms[1].Status);
        Assert.True(result.Rows[1].Complete);
        Assert.False(result.Rows[1].Overdue);
        Assert.NotNull(result.Rows[1].LastUpdatedAt);
    }

    [Fact]
    public void Build_BeforeDeadline_NotOverdue()
    {
        var result = CollectiveTrackingBuilder.Build(Period, new[] { CellB }, Array.Empty<CollectiveEvaluationRecord>(), ScopeFilter.Global, new DateOnly(2030, 6, 10));

        Assert.False(Assert.Single(result.Rows).Overdue);
        Assert.Null(result.Rows[0].LastUpdatedAt);
    }

    [Fact]
    public void Build_FiltersByScope()
    {
        var scope = new ScopeFilter(false, Array.Empty<Guid>(), new[] { CellA.PartyCellId });

        var result = CollectiveTrackingBuilder.Build(Period, new[] { CellA, CellB }, Array.Empty<CollectiveEvaluationRecord>(), scope, new DateOnly(2030, 6, 1));

        Assert.Equal(CellA.PartyCellId, Assert.Single(result.Rows).PartyCellId);
    }
}
