using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Phiếu kiểm soát (Mẫu 19). Ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface IControlCheckRepository
{
    /// <summary>Kèm mục và hồ sơ (cán bộ, đơn vị); được theo dõi.</summary>
    Task<ControlCheck?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kèm kỳ và hồ sơ; không theo dõi; mới nhất trước; không gồm phiếu đã hủy.</summary>
    Task<List<ControlCheck>> ListAsync(Guid? periodId, CancellationToken ct = default);

    /// <summary>Dòng hồ sơ đang hiệu lực của các hồ sơ, kèm phiếu; được theo dõi.</summary>
    Task<List<ControlCheckRecord>> ListCurrentRowsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);

    /// <summary>Lịch sử thao tác của phiếu, cũ trước.</summary>
    Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid checkId, CancellationToken ct = default);

    /// <summary>Lịch sử hồ sơ sau mốc thời gian; không theo dõi.</summary>
    Task<List<EvaluationRecordHistory>> ListRecordHistorySinceAsync(IReadOnlyCollection<Guid> recordIds, DateTime since, CancellationToken ct = default);

    void Add(ControlCheck check);

    void AddRecord(ControlCheckRecord row);

    void RemoveRecord(ControlCheckRecord row);

    void AddHistory(EvaluationFormHistory history);
}
