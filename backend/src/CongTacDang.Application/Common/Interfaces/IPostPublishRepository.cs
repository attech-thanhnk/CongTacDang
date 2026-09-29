using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Truy cập dữ liệu sau công bố (task 20): kết quả công khai, kiến nghị, kế hoạch 30-60-90 ngày. Ghi chỉ đưa vào DbContext,
/// lưu qua <see cref="IUnitOfWork"/>.
/// </summary>
public interface IPostPublishRepository
{
    /// <summary>
    /// Hồ sơ đã công bố (kèm kỳ, cán bộ, đơn vị, tổ chức Đảng ảnh chụp) nằm trong <paramref name="scope"/>, lọc theo kỳ nếu có
    /// (không theo dõi).
    /// </summary>
    Task<List<EvaluationRecord>> ListPublishedRecordsAsync(ScopeFilter scope, Guid? periodId, CancellationToken ct = default);

    /// <summary>Hồ sơ theo Id kèm kỳ, cán bộ, đơn vị, tổ chức Đảng (không theo dõi).</summary>
    Task<EvaluationRecord?> FindRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Id các hồ sơ (trong danh sách cho trước) đang có kiến nghị chưa trả lời.</summary>
    Task<HashSet<Guid>> GetRecordIdsWithOpenAppealsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);

    /// <summary>Kiến nghị của hồ sơ, mới nhất trước (không theo dõi).</summary>
    Task<List<EvaluationAppeal>> ListAppealsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Kiến nghị theo Id kèm hồ sơ (được theo dõi).</summary>
    Task<EvaluationAppeal?> FindAppealAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kiến nghị chưa trả lời kèm hồ sơ, kỳ, cán bộ, đơn vị (không theo dõi); lọc theo kỳ nếu có.</summary>
    Task<List<EvaluationAppeal>> ListOpenAppealsAsync(Guid? periodId, CancellationToken ct = default);

    /// <summary>Đưa kiến nghị mới vào DbContext.</summary>
    void AddAppeal(EvaluationAppeal appeal);

    /// <summary>Kế hoạch của hồ sơ (được theo dõi); null nếu chưa có.</summary>
    Task<ImprovementPlan?> FindPlanByRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Kế hoạch theo Id kèm hồ sơ, kỳ, cán bộ, đơn vị (được theo dõi).</summary>
    Task<ImprovementPlan?> FindPlanAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đưa kế hoạch mới vào DbContext.</summary>
    void AddPlan(ImprovementPlan plan);

    /// <summary>
    /// Kỳ có thể còn hồ sơ cần lập kế hoạch 30-60-90 ngày: đang mở, khóa dữ liệu hoặc đã đóng (không theo dõi, không kèm hồ sơ) —
    /// service lọc theo cửa sổ cảnh báo của bộ tiêu chí từng kỳ.
    /// </summary>
    Task<List<EvaluationPeriod>> ListPlanAlertPeriodsAsync(Guid? periodId, CancellationToken ct = default);

    /// <summary>
    /// Hồ sơ đã công bố của các kỳ <paramref name="periodIds"/> có mức chính thức (khác "Chưa xếp loại") mà chưa có kế hoạch hoặc
    /// kế hoạch đang lập — ứng viên nhóm "kế hoạch cần lập" (service lọc tiếp theo mức bắt buộc của bộ tiêu chí và quyền). Kèm kỳ,
    /// cán bộ, đơn vị; không theo dõi.
    /// </summary>
    Task<List<(EvaluationRecord Record, ImprovementPlan? Plan)>> ListRecordsNeedingPlanAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default);

    /// <summary>Kế hoạch đã duyệt đang chờ chủ hồ sơ <paramref name="memberId"/> xác nhận (kèm hồ sơ; không theo dõi).</summary>
    Task<List<ImprovementPlan>> ListPlansAwaitingAcknowledgementAsync(Guid memberId, Guid? periodId, CancellationToken ct = default);

    /// <summary>Tên đầy đủ của cán bộ (để ghi người thực hiện).</summary>
    Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default);
}
