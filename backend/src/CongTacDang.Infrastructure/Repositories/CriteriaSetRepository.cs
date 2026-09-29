using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Truy cập dữ liệu bộ tiêu chí (task 16). Ghi chỉ đưa vào DbContext, lưu qua IUnitOfWork.</summary>
public sealed class CriteriaSetRepository : ICriteriaSetRepository
{
    private readonly CongTacDangDbContext _db;

    public CriteriaSetRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<List<CriteriaSet>> ListAsync(CancellationToken ct = default) =>
        _db.Set<CriteriaSet>().AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<CriteriaSet?> FindAsync(Guid id, CancellationToken ct = default) =>
        _db.Set<CriteriaSet>().FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct = default)
    {
        var upper = code.ToUpperInvariant();
        return _db.Set<CriteriaSet>().AnyAsync(s => s.Code.ToUpper() == upper && (exceptId == null || s.Id != exceptId), ct);
    }

    /// <inheritdoc />
    public Task<CriteriaSet?> LatestPublishedAsync(string? selfScoreForm, CancellationToken ct = default) =>
        _db.Set<CriteriaSet>().AsNoTracking()
            .Where(s => s.Status == CriteriaSetStatus.Published && (selfScoreForm == null || s.SelfScoreForm == selfScoreForm))
            .OrderByDescending(s => s.PublishedAt)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<List<CriteriaSetUsage>> ListUsagesAsync(CancellationToken ct = default) =>
        _db.EvaluationPeriods.AsNoTracking()
            .Where(p => p.CriteriaSetId != null)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter)
            .Select(p => new CriteriaSetUsage(p.Id, p.Name, p.CriteriaSetId!.Value))
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<string?> LatestPeriodSnapshotAsync(CancellationToken ct = default) =>
        _db.EvaluationPeriods.AsNoTracking()
            .Where(p => p.CriteriaSnapshot != null)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).ThenByDescending(p => p.CreatedAt)
            .Select(p => p.CriteriaSnapshot)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public void Add(CriteriaSet set) => _db.Set<CriteriaSet>().Add(set);

    /// <inheritdoc />
    public void Remove(CriteriaSet set) => _db.Set<CriteriaSet>().Remove(set);
}
