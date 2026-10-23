using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Kết quả công khai, kiến nghị, kế hoạch 30-60-90 ngày. Ghi chỉ đưa vào DbContext, lưu qua <see cref="IUnitOfWork"/>.</summary>
public interface IPostPublishRepository
{
    /// <summary>Kèm kỳ, cán bộ, đơn vị, tổ chức Đảng ảnh chụp; không theo dõi.</summary>
    Task<List<EvaluationRecord>> ListPublishedRecordsAsync(ScopeFilter scope, Guid? periodId, CancellationToken ct = default);

    /// <summary>Kỳ có hồ sơ đã công bố, đã có mức chính thức trong phạm vi; không nạp hồ sơ.</summary>
    Task<List<PublishedPeriodOption>> ListPublishedPeriodsAsync(ScopeFilter scope, CancellationToken ct = default);

    /// <summary>Kèm kỳ, cán bộ, đơn vị, tổ chức Đảng; không theo dõi.</summary>
    Task<EvaluationRecord?> FindRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Hồ sơ đang có kiến nghị chưa trả lời.</summary>
    Task<HashSet<Guid>> GetRecordIdsWithOpenAppealsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);

    /// <summary>Mới nhất trước; không theo dõi.</summary>
    Task<List<EvaluationAppeal>> ListAppealsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Kèm hồ sơ; được theo dõi.</summary>
    Task<EvaluationAppeal?> FindAppealAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kiến nghị chưa trả lời kèm hồ sơ, kỳ, cán bộ, đơn vị; không theo dõi.</summary>
    Task<List<EvaluationAppeal>> ListOpenAppealsAsync(Guid? periodId, CancellationToken ct = default);

    void AddAppeal(EvaluationAppeal appeal);

    /// <summary>Được theo dõi; null nếu chưa có.</summary>
    Task<ImprovementPlan?> FindPlanByRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Kèm hồ sơ, kỳ, cán bộ, đơn vị; được theo dõi.</summary>
    Task<ImprovementPlan?> FindPlanAsync(Guid id, CancellationToken ct = default);

    void AddPlan(ImprovementPlan plan);

    /// <summary>Kỳ đang mở, khóa dữ liệu hoặc đã đóng; service lọc tiếp theo cửa sổ cảnh báo của bộ tiêu chí.</summary>
    Task<List<EvaluationPeriod>> ListPlanAlertPeriodsAsync(Guid? periodId, CancellationToken ct = default);

    /// <summary>
    /// Hồ sơ đã công bố có mức chính thức mà chưa có kế hoạch hoặc kế hoạch đang lập.
    /// Chỉ là ứng viên — service lọc tiếp theo mức bắt buộc và quyền.
    /// </summary>
    Task<List<(EvaluationRecord Record, ImprovementPlan? Plan)>> ListRecordsNeedingPlanAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default);

    /// <summary>Kế hoạch đã duyệt đang chờ chủ hồ sơ xác nhận; không theo dõi.</summary>
    Task<List<ImprovementPlan>> ListPlansAwaitingAcknowledgementAsync(Guid memberId, Guid? periodId, CancellationToken ct = default);

    Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default);
}

/// <param name="CreatedAt">Dùng sắp xếp các kỳ cùng năm, quý.</param>
public sealed record PublishedPeriodOption(Guid Id, string Name, int Year, Domain.Enums.EvaluationQuarter Quarter, DateTime CreatedAt);
