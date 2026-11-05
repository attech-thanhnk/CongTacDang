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
public sealed class RecordOpinionRepository : IRecordOpinionRepository
{
    private readonly CongTacDangDbContext _db;

    public RecordOpinionRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<List<RecordOpinion>> ListByRecordAsync(Guid recordId, CancellationToken ct = default) =>
        _db.Set<RecordOpinion>().AsNoTracking()
            .Where(o => o.RecordId == recordId)
            .OrderByDescending(o => o.RecordedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<HashSet<(Guid RecordId, RecordOpinionKind Kind)>> ListKindsByAuthorAsync(Guid userId, IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        var rows = await _db.Set<RecordOpinion>().AsNoTracking()
            .Where(o => o.RecordedById == userId && recordIds.Contains(o.RecordId))
            .Select(o => new { o.RecordId, o.Kind })
            .Distinct()
            .ToListAsync(ct);
        return rows.Select(r => (r.RecordId, r.Kind)).ToHashSet();
    }

    /// <inheritdoc />
    public void Add(RecordOpinion opinion) => _db.Set<RecordOpinion>().Add(opinion);
}
