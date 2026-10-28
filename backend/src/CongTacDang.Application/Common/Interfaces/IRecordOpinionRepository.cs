using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Ý kiến ghi nhận trên hồ sơ. Ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface IRecordOpinionRepository
{
    /// <summary>Mới nhất trước; không theo dõi.</summary>
    Task<List<RecordOpinion>> ListByRecordAsync(Guid recordId, CancellationToken ct = default);

    void Add(RecordOpinion opinion);
}
