using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Kỳ đang dùng một bộ tiêu chí (chỉ đọc).</summary>
/// <param name="PeriodId">Id kỳ.</param>
/// <param name="PeriodName">Tên kỳ.</param>
/// <param name="CriteriaSetId">Id bộ tiêu chí.</param>
public sealed record CriteriaSetUsage(Guid PeriodId, string PeriodName, Guid CriteriaSetId);

/// <summary>
/// Truy cập dữ liệu bộ tiêu chí (task 16). Thao tác ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// </summary>
public interface ICriteriaSetRepository
{
    /// <summary>Mọi bộ tiêu chí (không theo dõi), mới cập nhật trước.</summary>
    Task<List<CriteriaSet>> ListAsync(CancellationToken ct = default);

    /// <summary>Bộ theo Id (được theo dõi).</summary>
    Task<CriteriaSet?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Mã đã được bộ khác dùng (không phân biệt hoa thường).</summary>
    Task<bool> CodeExistsAsync(string code, Guid? exceptId, CancellationToken ct = default);

    /// <summary>Bộ đã xuất bản mới nhất (theo thời điểm xuất bản) có mẫu tự chấm cho trước (null = mọi mẫu).</summary>
    Task<CriteriaSet?> LatestPublishedAsync(string? selfScoreForm, CancellationToken ct = default);

    /// <summary>Các kỳ (chưa xóa) đang chọn bộ tiêu chí.</summary>
    Task<List<CriteriaSetUsage>> ListUsagesAsync(CancellationToken ct = default);

    /// <summary>Ảnh chụp bộ tiêu chí của các kỳ (jsonb) — mới nhất trước — để lấy danh mục khung "bộ đang dùng gần nhất".</summary>
    Task<string?> LatestPeriodSnapshotAsync(CancellationToken ct = default);

    /// <summary>Đưa bộ mới vào DbContext.</summary>
    void Add(CriteriaSet set);

    /// <summary>Xóa hẳn bộ (chỉ bản nháp).</summary>
    void Remove(CriteriaSet set);
}
