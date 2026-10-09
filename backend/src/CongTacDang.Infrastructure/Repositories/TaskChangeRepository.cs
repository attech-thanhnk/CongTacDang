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

/// <summary>Truy cập dữ liệu đề nghị điều chỉnh việc trong quý (Mẫu 04, 05). Ghi chỉ đưa vào DbContext, lưu qua IUnitOfWork.</summary>
public sealed class TaskChangeRepository : ITaskChangeRepository
{
    private readonly CongTacDangDbContext _db;

    public TaskChangeRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    private DbSet<TaskChangeRequest> Requests => _db.Set<TaskChangeRequest>();

    /// <inheritdoc />
    public Task<List<TaskChangeRequest>> ListByRecordAsync(Guid recordId, CancellationToken ct = default) =>
        Requests.AsNoTracking()
            .Where(r => r.RecordId == recordId)
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<TaskChangeRequest?> FindAsync(Guid id, CancellationToken ct = default) =>
        Requests.FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public Task<List<TaskChangeRequest>> ListPendingAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default)
    {
        var ids = periodIds.ToList();
        return Requests.AsNoTracking()
            .Include(r => r.Record).ThenInclude(x => x.Period)
            .Include(r => r.Record).ThenInclude(x => x.Member)
            .Include(r => r.Record).ThenInclude(x => x.Department)
            .Include(r => r.Record).ThenInclude(x => x.PartyCell)
            .Where(r => r.Status == TaskChangeStatus.Pending && ids.Contains(r.Record.PeriodId))
            .OrderBy(r => r.RequestedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public void Add(TaskChangeRequest request) => Requests.Add(request);

    /// <inheritdoc />
    public void AddTask(EvaluationTask task) => _db.EvaluationTasks.Add(task);
}
