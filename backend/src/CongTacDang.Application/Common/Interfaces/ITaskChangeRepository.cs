using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Truy cập dữ liệu đề nghị điều chỉnh việc trong quý (Mẫu 04, 05). Ghi chỉ đưa vào DbContext; lưu qua
/// <see cref="IUnitOfWork.SaveChangesAsync"/> (một lần lưu = một transaction).
/// </summary>
public interface ITaskChangeRepository
{
    /// <summary>Đề nghị của hồ sơ, mới nhất trước (không theo dõi).</summary>
    Task<List<TaskChangeRequest>> ListByRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Đề nghị theo Id (được theo dõi).</summary>
    Task<TaskChangeRequest?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đề nghị đang chờ duyệt trong các kỳ cho trước, kèm hồ sơ (cán bộ, Phòng, Chi bộ, kỳ) — không theo dõi.</summary>
    Task<List<TaskChangeRequest>> ListPendingAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default);

    /// <summary>Đưa đề nghị mới vào DbContext.</summary>
    void Add(TaskChangeRequest request);

    /// <summary>Đưa việc mới (việc đột xuất được duyệt) vào DbContext.</summary>
    void AddTask(EvaluationTask task);
}
