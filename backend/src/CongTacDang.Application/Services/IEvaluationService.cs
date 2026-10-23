using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>Tra cứu hồ sơ đánh giá; thao tác ghi ở <see cref="IEvaluationWorkflowService"/>.</summary>
public interface IEvaluationService
{
    /// <summary>Null nếu người dùng không được đánh giá trong kỳ; <c>Guid.Empty</c> → kỳ đang hoạt động (không có → 404).</summary>
    Task<EvaluationRecordDto?> GetMyRecordAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Chủ hồ sơ luôn xem được.</summary>
    Task<EvaluationRecordDto> GetRecordByIdAsync(Guid recordId, CancellationToken ct = default);

    Task<List<EvaluationRecordHistoryDto>> GetRecordHistoryAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Theo phạm vi evaluation.read, kèm hồ sơ của chính mình; <c>Guid.Empty</c> → 400.</summary>
    Task<List<EvaluationRecordDto>> GetRecordsByPeriodAsync(Guid periodId, CancellationToken ct = default);

    Task<List<EvaluationRecordDto>> GetRecordsByBranchAsync(Guid periodId, Guid? branchId, CancellationToken ct = default);

    /// <summary>Kiểm soát trần Hoàn thành xuất sắc theo nhóm chức danh trong từng cấp quyết định.</summary>
    Task<ExcellentQuotaCheckDto> CheckExcellentQuotasAsync(Guid periodId, Guid? partyCellId, CancellationToken ct = default);
}
