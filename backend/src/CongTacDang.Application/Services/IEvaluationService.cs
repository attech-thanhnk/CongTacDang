using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Tra cứu hồ sơ đánh giá (đọc). Các hành động ghi theo bước nằm ở <see cref="IEvaluationWorkflowService"/>,
/// quản lý kỳ ở <see cref="IPeriodService"/> (task 12).
/// </summary>
public interface IEvaluationService
{
    /// <summary>Hồ sơ của chính người dùng trong kỳ (null nếu người dùng không có trong danh sách được đánh giá).</summary>
    Task<EvaluationRecordDto?> GetMyRecordAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Chi tiết hồ sơ (evaluation.read trên hồ sơ; chủ hồ sơ luôn xem được).</summary>
    Task<EvaluationRecordDto> GetRecordByIdAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Lịch sử của hồ sơ (cùng quyền xem hồ sơ).</summary>
    Task<List<EvaluationRecordHistoryDto>> GetRecordHistoryAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Hồ sơ trong kỳ theo phạm vi evaluation.read (kèm hồ sơ của chính mình).</summary>
    Task<List<EvaluationRecordDto>> GetRecordsByPeriodAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Hồ sơ của một Chi bộ trong kỳ, lọc theo phạm vi evaluation.read.</summary>
    Task<List<EvaluationRecordDto>> GetRecordsByBranchAsync(Guid periodId, Guid? branchId, CancellationToken ct = default);

    /// <summary>Kiểm tra trần Hoàn thành xuất sắc theo Chi bộ (Mẫu 15) trong phạm vi người dùng.</summary>
    Task<List<BranchQuotaCheckDto>> CheckBranchQuotasAsync(Guid periodId, CancellationToken ct = default);
}
