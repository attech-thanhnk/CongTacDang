using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Đề nghị điều chỉnh việc trong quý (Mẫu 04, 05). Ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface ITaskChangeRepository
{
    /// <summary>Mới nhất trước; không theo dõi.</summary>
    Task<List<TaskChangeRequest>> ListByRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Được theo dõi.</summary>
    Task<TaskChangeRequest?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đang chờ duyệt, kèm hồ sơ (cán bộ, Phòng, Chi bộ, kỳ); không theo dõi.</summary>
    Task<List<TaskChangeRequest>> ListPendingAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default);

    void Add(TaskChangeRequest request);

    /// <summary>Việc đột xuất được duyệt.</summary>
    void AddTask(EvaluationTask task);
}
