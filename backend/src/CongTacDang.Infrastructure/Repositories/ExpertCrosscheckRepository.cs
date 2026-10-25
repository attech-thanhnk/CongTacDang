using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Ghi chỉ đưa vào DbContext; lưu qua IUnitOfWork.</summary>
public sealed class ExpertCrosscheckRepository : IExpertCrosscheckRepository
{
    private readonly CongTacDangDbContext _db;

    public ExpertCrosscheckRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    private DbSet<ExpertCrosscheck> Crosschecks => _db.Set<ExpertCrosscheck>();

    private static readonly CrosscheckStatus[] OpenStatuses =
    {
        CrosscheckStatus.Requested, CrosscheckStatus.InProgress, CrosscheckStatus.Submitted
    };

    /// <inheritdoc />
    public Task<ExpertCrosscheck?> FindAsync(Guid id, CancellationToken ct = default) =>
        Crosschecks
            .Include(c => c.Items)
            .Include(c => c.ExpertDepartment)
            .Include(c => c.Record).ThenInclude(r => r.Period)
            .Include(c => c.Record).ThenInclude(r => r.Member)
            .Include(c => c.Record).ThenInclude(r => r.Department)
            .Include(c => c.Record).ThenInclude(r => r.PartyCell)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public Task<List<ExpertCrosscheck>> ListByRecordAsync(Guid recordId, CancellationToken ct = default) =>
        WithSummaryIncludes(Crosschecks.AsNoTracking())
            .Where(c => c.RecordId == recordId)
            .OrderByDescending(c => c.RequestedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<ExpertCrosscheck>> ListAsync(Guid? periodId, CrosscheckStatus? status, CancellationToken ct = default)
    {
        var query = WithSummaryIncludes(Crosschecks.AsNoTracking());
        if (periodId.HasValue && periodId.Value != Guid.Empty)
            query = query.Where(c => c.PeriodId == periodId.Value);
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);
        return query.OrderByDescending(c => c.RequestedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<List<ExpertCrosscheck>> ListOpenAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default)
    {
        var ids = periodIds.ToList();
        return WithSummaryIncludes(Crosschecks.AsNoTracking())
            .Where(c => ids.Contains(c.PeriodId) && OpenStatuses.Contains(c.Status))
            .OrderBy(c => c.RequestedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<int> CountOpenAsync(Guid recordId, CancellationToken ct = default) =>
        Crosschecks.CountAsync(c => c.RecordId == recordId && OpenStatuses.Contains(c.Status), ct);

    /// <inheritdoc />
    public Task<string?> GetDepartmentNameAsync(Guid departmentId, CancellationToken ct = default) =>
        _db.Set<AdministrativeDepartment>().Where(d => d.Id == departmentId).Select(d => d.Name).FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid crosscheckId, CancellationToken ct = default) =>
        _db.EvaluationFormHistories.AsNoTracking()
            .Where(h => h.FormType == EvaluationFormType.M03 && h.FormId == crosscheckId)
            .OrderBy(h => h.At)
            .ToListAsync(ct);

    /// <inheritdoc />
    public void Add(ExpertCrosscheck crosscheck) => Crosschecks.Add(crosscheck);

    /// <inheritdoc />
    public void AddItem(ExpertCrosscheckItem item) => _db.Set<ExpertCrosscheckItem>().Add(item);

    /// <inheritdoc />
    public void RemoveItem(ExpertCrosscheckItem item) => _db.Set<ExpertCrosscheckItem>().Remove(item);

    /// <inheritdoc />
    public void AddHistory(EvaluationFormHistory history) => _db.EvaluationFormHistories.Add(history);

    private static IQueryable<ExpertCrosscheck> WithSummaryIncludes(IQueryable<ExpertCrosscheck> query) =>
        query
            .Include(c => c.Items)
            .Include(c => c.ExpertDepartment)
            .Include(c => c.Record).ThenInclude(r => r.Period)
            .Include(c => c.Record).ThenInclude(r => r.Member)
            .Include(c => c.Record).ThenInclude(r => r.Department)
            .Include(c => c.Record).ThenInclude(r => r.PartyCell)
            .AsSplitQuery();
}
