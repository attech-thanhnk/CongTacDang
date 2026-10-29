using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Ghép tổ chức Đảng với hồ sơ tập thể để theo dõi; chỉ tính trạng thái, không chặn bước nào.</summary>
public static class CollectiveTrackingBuilder
{
    /// <summary>Ngày 8 của tháng cuối quý.</summary>
    public const int DeadlineDay = 8;

    private static readonly CollectiveEvaluationForm[] TrackedForms =
        { CollectiveEvaluationForm.M06, CollectiveEvaluationForm.M07, CollectiveEvaluationForm.M08 };

    public static DateOnly Deadline(int year, int quarter, IReadOnlyCollection<DateOnly>? holidays = null) =>
        DeadlineCalculator.Compute(new DeadlineRule { Kind = DeadlineRuleKind.DayOfLastMonth, Value = DeadlineDay }, year, quarter, holidays);

    public static CollectiveTrackingDto Build(
        EvaluationPeriod period,
        IEnumerable<PartyCellMemberCount> cells,
        IEnumerable<CollectiveEvaluationRecord> records,
        ScopeFilter scope,
        DateOnly today,
        IReadOnlyCollection<DateOnly>? holidays = null)
    {
        var deadline = Deadline(period.Year, (int)period.Quarter, holidays);
        var byCell = records
            .Where(r => r.PartyCellId.HasValue)
            .GroupBy(r => r.PartyCellId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<CollectiveTrackingRowDto>();
        foreach (var cell in cells.Where(c => scope.Matches(null, null, c.PartyCellId)))
        {
            var cellRecords = byCell.GetValueOrDefault(cell.PartyCellId) ?? new List<CollectiveEvaluationRecord>();
            var forms = TrackedForms.Select(form =>
            {
                var record = cellRecords.FirstOrDefault(r => r.Form == form);
                return new CollectiveTrackingFormDto
                {
                    Form = form.ToString(),
                    RecordId = record?.Id,
                    Status = record?.Status.ToString(),
                    UpdatedAt = record == null ? null : record.UpdatedAt ?? record.CreatedAt
                };
            }).ToList();
            var complete = forms.All(f => f.Status == nameof(CollectiveRecordStatus.Submitted));
            rows.Add(new CollectiveTrackingRowDto
            {
                PartyCellId = cell.PartyCellId,
                PartyCellName = cell.Name,
                MemberCount = cell.MemberCount,
                Forms = forms,
                Complete = complete,
                Overdue = !complete && today > deadline,
                LastUpdatedAt = forms.Where(f => f.UpdatedAt.HasValue).Select(f => f.UpdatedAt).Max()
            });
        }

        return new CollectiveTrackingDto
        {
            Deadline = deadline.ToString("yyyy-MM-dd"),
            Rows = rows.OrderBy(r => r.Complete).ThenBy(r => r.PartyCellName).ToList()
        };
    }
}
