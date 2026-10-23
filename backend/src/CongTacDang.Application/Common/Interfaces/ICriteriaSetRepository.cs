using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Kỳ đang dùng một bộ tiêu chí.</summary>
public sealed record CriteriaSetUsage(Guid PeriodId, string PeriodName, Guid CriteriaSetId);

/// <summary>Ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface ICriteriaSetRepository
{
    /// <summary>Không theo dõi, mới cập nhật trước.</summary>
    Task<List<CriteriaSet>> ListAsync(CancellationToken ct = default);

    /// <summary>Được theo dõi.</summary>
    Task<CriteriaSet?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Không phân biệt hoa thường.</summary>
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct = default);

    /// <summary>Theo thời điểm xuất bản; <paramref name="selfScoreForm"/> null = mọi mẫu.</summary>
    Task<CriteriaSet?> LatestPublishedAsync(string? selfScoreForm, CancellationToken ct = default);

    /// <summary>Các kỳ chưa xóa đang chọn bộ tiêu chí.</summary>
    Task<List<CriteriaSetUsage>> ListUsagesAsync(CancellationToken ct = default);

    /// <summary>Ảnh chụp (jsonb) của kỳ mới nhất — để lấy danh mục khung "bộ đang dùng gần nhất".</summary>
    Task<string?> LatestPeriodSnapshotAsync(CancellationToken ct = default);

    void Add(CriteriaSet set);

    /// <summary>Xóa hẳn — chỉ bản nháp.</summary>
    void Remove(CriteriaSet set);
}
