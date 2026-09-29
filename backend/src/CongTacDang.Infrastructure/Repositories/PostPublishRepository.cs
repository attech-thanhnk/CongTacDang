using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Truy cập dữ liệu sau công bố (task 20). Ghi chỉ đưa vào DbContext, lưu qua IUnitOfWork.</summary>
public sealed class PostPublishRepository : IPostPublishRepository
{
    private readonly CongTacDangDbContext _db;

    public PostPublishRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    private DbSet<EvaluationAppeal> Appeals => _db.Set<EvaluationAppeal>();

    private DbSet<ImprovementPlan> Plans => _db.Set<ImprovementPlan>();

    private IQueryable<EvaluationRecord> RecordsWithHeader() => _db.EvaluationRecords
        .Include(r => r.Period)
        .Include(r => r.Member)
        .Include(r => r.Department)
        .Include(r => r.PartyCell);

    /// <inheritdoc />
    public Task<List<EvaluationRecord>> ListPublishedRecordsAsync(ScopeFilter scope, Guid? periodId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var query = RecordsWithHeader().AsNoTracking().Where(r => r.Status == RecordStatus.Published);
        if (periodId.HasValue)
            query = query.Where(r => r.PeriodId == periodId.Value);

        if (!scope.IsGlobal)
        {
            var departments = scope.DepartmentIds.ToList();
            var cells = scope.PartyCellIds.ToList();
            var owner = scope.OwnerId;
            query = query.Where(r =>
                (r.DepartmentId.HasValue && departments.Contains(r.DepartmentId.Value))
                || (r.PartyCellId.HasValue && cells.Contains(r.PartyCellId.Value))
                || (owner.HasValue && r.MemberId == owner.Value));
        }

        return query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<EvaluationRecord?> FindRecordAsync(Guid recordId, CancellationToken ct = default) =>
        RecordsWithHeader().AsNoTracking().FirstOrDefaultAsync(r => r.Id == recordId, ct);

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetRecordIdsWithOpenAppealsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        if (recordIds.Count == 0)
            return new HashSet<Guid>();
        var ids = recordIds.ToList();
        return (await Appeals.AsNoTracking()
            .Where(a => ids.Contains(a.RecordId) && (a.Status == AppealStatus.Submitted || a.Status == AppealStatus.UnderReview))
            .Select(a => a.RecordId)
            .Distinct()
            .ToListAsync(ct)).ToHashSet();
    }

    /// <inheritdoc />
    public Task<List<EvaluationAppeal>> ListAppealsAsync(Guid recordId, CancellationToken ct = default) =>
        Appeals.AsNoTracking()
            .Where(a => a.RecordId == recordId)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<EvaluationAppeal?> FindAppealAsync(Guid id, CancellationToken ct = default) =>
        Appeals
            .Include(a => a.Record).ThenInclude(r => r.Period)
            .Include(a => a.Record).ThenInclude(r => r.Member)
            .Include(a => a.Record).ThenInclude(r => r.Department)
            .Include(a => a.Record).ThenInclude(r => r.PartyCell)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <inheritdoc />
    public Task<List<EvaluationAppeal>> ListOpenAppealsAsync(Guid? periodId, CancellationToken ct = default)
    {
        var query = Appeals.AsNoTracking()
            .Include(a => a.Record).ThenInclude(r => r.Period)
            .Include(a => a.Record).ThenInclude(r => r.Member)
            .Include(a => a.Record).ThenInclude(r => r.Department)
            .Include(a => a.Record).ThenInclude(r => r.PartyCell)
            .Where(a => a.Status == AppealStatus.Submitted || a.Status == AppealStatus.UnderReview);
        if (periodId.HasValue)
            query = query.Where(a => a.Record.PeriodId == periodId.Value);
        return query.OrderBy(a => a.SubmittedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public void AddAppeal(EvaluationAppeal appeal) => Appeals.Add(appeal);

    /// <inheritdoc />
    public Task<ImprovementPlan?> FindPlanByRecordAsync(Guid recordId, CancellationToken ct = default) =>
        Plans.FirstOrDefaultAsync(p => p.RecordId == recordId, ct);

    /// <inheritdoc />
    public Task<ImprovementPlan?> FindPlanAsync(Guid id, CancellationToken ct = default) =>
        Plans
            .Include(p => p.Record).ThenInclude(r => r.Period)
            .Include(p => p.Record).ThenInclude(r => r.Member)
            .Include(p => p.Record).ThenInclude(r => r.Department)
            .Include(p => p.Record).ThenInclude(r => r.PartyCell)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public void AddPlan(ImprovementPlan plan) => Plans.Add(plan);

    /// <inheritdoc />
    public Task<List<EvaluationPeriod>> ListPlanAlertPeriodsAsync(Guid? periodId, CancellationToken ct = default)
    {
        var periods = _db.EvaluationPeriods.AsNoTracking()
            .Where(p => p.Status == PeriodStatus.Open || p.Status == PeriodStatus.Locked || p.Status == PeriodStatus.Closed);
        if (periodId.HasValue)
            periods = periods.Where(p => p.Id == periodId.Value);
        return periods.ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<(EvaluationRecord Record, ImprovementPlan? Plan)>> ListRecordsNeedingPlanAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default)
    {
        if (periodIds.Count == 0)
            return new List<(EvaluationRecord Record, ImprovementPlan? Plan)>();
        var ids = periodIds.ToList();
        var records = RecordsWithHeader().AsNoTracking()
            .Where(r => ids.Contains(r.PeriodId) && r.Status == RecordStatus.Published && r.FinalGrade != EvaluationGrade.ChuaXepLoai);

        var rows = await records
            .Select(r => new { Record = r, Plan = Plans.FirstOrDefault(p => p.RecordId == r.Id) })
            .Where(x => x.Plan == null || x.Plan.Status == ImprovementPlanStatus.Draft)
            .ToListAsync(ct);
        return rows.Select(x => (x.Record, x.Plan)).ToList();
    }

    /// <inheritdoc />
    public Task<List<ImprovementPlan>> ListPlansAwaitingAcknowledgementAsync(Guid memberId, Guid? periodId, CancellationToken ct = default)
    {
        var query = Plans.AsNoTracking()
            .Include(p => p.Record).ThenInclude(r => r.Period)
            .Include(p => p.Record).ThenInclude(r => r.Member)
            .Include(p => p.Record).ThenInclude(r => r.Department)
            .Include(p => p.Record).ThenInclude(r => r.PartyCell)
            .Where(p => p.Status == ImprovementPlanStatus.Approved && p.Record.MemberId == memberId);
        if (periodId.HasValue)
            query = query.Where(p => p.Record.PeriodId == periodId.Value);
        return query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default) =>
        _db.PartyMemberProfiles.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => (string?)m.FullName)
            .FirstOrDefaultAsync(ct);
}
