using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Ghi chỉ đưa vào DbContext; lưu qua IUnitOfWork.</summary>
public sealed class ControlCheckRepository : IControlCheckRepository
{
    private readonly CongTacDangDbContext _db;

    public ControlCheckRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    private DbSet<ControlCheck> Checks => _db.Set<ControlCheck>();

    /// <inheritdoc />
    public Task<ControlCheck?> FindAsync(Guid id, CancellationToken ct = default) =>
        Checks.Include(c => c.Period)
            .Include(c => c.Items)
            .Include(c => c.Records).ThenInclude(r => r.Record).ThenInclude(r => r.Member)
            .Include(c => c.Records).ThenInclude(r => r.Record).ThenInclude(r => r.Department)
            .Include(c => c.Records).ThenInclude(r => r.Record).ThenInclude(r => r.PartyCell)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public Task<List<ControlCheck>> ListAsync(Guid? periodId, CancellationToken ct = default) =>
        Checks.AsNoTracking()
            .Include(c => c.Period)
            .Include(c => c.Records).ThenInclude(r => r.Record)
            .AsSplitQuery()
            .Where(c => c.Status != ControlCheckStatus.Cancelled && (periodId == null || c.PeriodId == periodId))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<ControlCheckRecord>> ListCurrentRowsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        var ids = recordIds.ToList();
        return _db.Set<ControlCheckRecord>()
            .Include(r => r.Check).ThenInclude(c => c.Records)
            .AsSplitQuery()
            .Where(r => r.IsCurrent && ids.Contains(r.RecordId))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid checkId, CancellationToken ct = default) =>
        _db.EvaluationFormHistories.AsNoTracking()
            .Where(h => h.FormType == EvaluationFormType.M19 && h.FormId == checkId)
            .OrderBy(h => h.At)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<EvaluationRecordHistory>> ListRecordHistorySinceAsync(IReadOnlyCollection<Guid> recordIds, DateTime since, CancellationToken ct = default)
    {
        var ids = recordIds.ToList();
        return _db.EvaluationRecordHistories.AsNoTracking()
            .Where(h => ids.Contains(h.RecordId) && h.CreatedAt > since)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public void Add(ControlCheck check) => Checks.Add(check);

    /// <inheritdoc />
    public void AddRecord(ControlCheckRecord row) => _db.Set<ControlCheckRecord>().Add(row);

    /// <inheritdoc />
    public void RemoveRecord(ControlCheckRecord row) => _db.Set<ControlCheckRecord>().Remove(row);

    /// <inheritdoc />
    public void AddHistory(EvaluationFormHistory history) => _db.EvaluationFormHistories.Add(history);
}
