using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Phiếu đối soát chuyên ngành (Mẫu 03). Ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface IExpertCrosscheckRepository
{
    /// <summary>Được theo dõi; kèm dòng, hồ sơ (kỳ, cán bộ, Phòng, Chi bộ) và đơn vị đối soát.</summary>
    Task<ExpertCrosscheck?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Mới nhất trước; không theo dõi; kèm dòng và đơn vị đối soát.</summary>
    Task<List<ExpertCrosscheck>> ListByRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Không theo dõi; kèm hồ sơ, dòng và đơn vị đối soát.</summary>
    Task<List<ExpertCrosscheck>> ListAsync(Guid? periodId, CrosscheckStatus? status, CancellationToken ct = default);

    /// <summary>Phiếu chưa xác nhận và chưa hủy của các kỳ; không theo dõi.</summary>
    Task<List<ExpertCrosscheck>> ListOpenAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default);

    Task<int> CountOpenAsync(Guid recordId, CancellationToken ct = default);

    Task<string?> GetDepartmentNameAsync(Guid departmentId, CancellationToken ct = default);

    Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid crosscheckId, CancellationToken ct = default);

    void Add(ExpertCrosscheck crosscheck);

    void AddItem(ExpertCrosscheckItem item);

    void RemoveItem(ExpertCrosscheckItem item);

    void AddHistory(EvaluationFormHistory history);
}
