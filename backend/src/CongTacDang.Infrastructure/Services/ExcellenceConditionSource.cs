using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Reports;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Services;

public sealed class ExcellenceConditionSource : IExcellenceConditionSource
{
    private readonly CongTacDangDbContext _db;

    public ExcellenceConditionSource(CongTacDangDbContext db) => _db = db;

    public async Task<ExcellenceConditionContext> LoadAsync(EvaluationRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var now = DateTime.UtcNow;
        var period = record.Period ?? await _db.EvaluationPeriods.AsNoTracking().FirstAsync(p => p.Id == record.PeriodId, ct);
        var criteria = EvaluationMapping.SafeCriteria(period);

        var input = new ExcellenceConditionInput
        {
            Tasks = ExcellenceConditions.TasksOf(record.Tasks),
            MinExceedRatio = GradeConditions.ExcellentMinExceedRatio(criteria?.Content),
            IsLeader = await IsLeaderAsync(record.MemberId, ExcellentQuotaCalculator.PositionDate(period, now), ct),
            UnitReport = await UnitReportAsync(record, ct),
            PreviousPlan = await PreviousPlanAsync(record, period, now, ct),
            SelfScored = record.SelfScoredAt.HasValue,
            SelfProposedGrade = record.SelfProposedGrade,
            SelfDeclaresNoCap = record.SelfDeclaresNoCap,
            Appraised = record.AppraisedAt.HasValue,
            GradeCap = record.AppraisalGradeCap,
            CapReason = record.AppraisalCapReason,
            Overrides = ExcellenceConditionCodes.ParseOverrides(record.AppraisalConditionOverrides)
        };
        return new ExcellenceConditionContext(ExcellenceConditions.Evaluate(input), await HasSafetyDefectAsync(record.Id, ct));
    }

    private Task<bool> IsLeaderAsync(Guid memberId, DateTime at, CancellationToken ct) =>
        _db.MemberPositions.AsNoTracking()
            .AnyAsync(mp => mp.UserId == memberId && !mp.IsDeleted && mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at)
                && mp.Position != null && (mp.Position.IsHead || mp.Position.IsLeadership), ct);

    /// <summary>Mẫu 06 cùng kỳ ghi cán bộ là người đứng đầu; bản cập nhật gần nhất.</summary>
    private async Task<UnitTaskReport?> UnitReportAsync(EvaluationRecord record, CancellationToken ct)
    {
        var sections = await _db.CollectiveEvaluationRecords.AsNoTracking()
            .Where(c => c.PeriodId == record.PeriodId && !c.IsDeleted && c.Form == CollectiveEvaluationForm.M06 && c.HeadId == record.MemberId)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .Select(c => c.Sections)
            .FirstOrDefaultAsync(ct);
        if (sections == null)
            return null;
        var values = CollectiveEvaluationService.DeserializeSections(sections);
        int? Count(string code) =>
            values.TryGetValue(code, out var text) && int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        return new UnitTaskReport(Count(Hd03FormCatalog.Form06AssignedCode), Count(Hd03FormCatalog.Form06OnTimeCode));
    }

    /// <summary>Kỳ liền trước theo (năm, quý) của cùng cán bộ.</summary>
    private async Task<PreviousPlanState?> PreviousPlanAsync(EvaluationRecord record, EvaluationPeriod period, DateTime now, CancellationToken ct)
    {
        var (year, quarter) = period.Quarter == EvaluationQuarter.Quy1 ? (period.Year - 1, EvaluationQuarter.Quy4) : (period.Year, period.Quarter - 1);
        var plan = await _db.Set<ImprovementPlan>().AsNoTracking()
            .Where(p => p.Record.MemberId == record.MemberId && !p.Record.IsDeleted
                && p.Record.Period.Year == year && p.Record.Period.Quarter == quarter && !p.Record.Period.IsDeleted)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (plan == null)
            return null;
        if (plan.Status == ImprovementPlanStatus.Draft)
            return new PreviousPlanState(false, Array.Empty<PlanMilestoneState>());

        var content = SafeContent(plan.Content);
        var start = plan.StartDate ?? (plan.ApprovedAt is { } approved ? DateOnly.FromDateTime(approved) : (DateOnly?)null);
        var today = DateOnly.FromDateTime(now);
        var milestones = ImprovementPlanContent.Milestones
            .Select(m => new PlanMilestoneState(m.Name, content.Stage(m.Code).Result, start is { } s && s.AddDays(m.Days) <= today))
            .ToList();
        return new PreviousPlanState(true, milestones);
    }

    private static ImprovementPlanContent SafeContent(string json)
    {
        try
        {
            return ImprovementPlanContent.Parse(json);
        }
        catch (FormatException)
        {
            return new ImprovementPlanContent().Normalize();
        }
    }

    private async Task<bool> HasSafetyDefectAsync(Guid recordId, CancellationToken ct)
    {
        var fields = await _db.Set<ExpertCrosscheck>().AsNoTracking()
            .Where(c => c.RecordId == recordId && !c.IsDeleted && c.Status != CrosscheckStatus.Cancelled
                && c.Items.Any(i => !i.NotApplicable && i.Result == CrosscheckResult.Defect))
            .Select(c => c.Fields)
            .ToListAsync(ct);
        // Cột Fields là chuỗi JSON nên lọc lĩnh vực sau khi đọc.
        return fields.Any(f => CrosscheckRules.FieldsFromJson(f).Contains(CrosscheckFields.SafetyQuality));
    }
}
