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
    public void Add(RecordOpinion opinion) => _db.Set<RecordOpinion>().Add(opinion);
}
