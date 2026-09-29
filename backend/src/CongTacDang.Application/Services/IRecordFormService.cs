using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>
/// Biểu mẫu cá nhân của một hồ sơ đánh giá (task 18 — T-83): danh sách mẫu áp dụng theo bộ tiêu chí của kỳ và xuất Word/PDF.
/// Kiểm tra quyền xem hồ sơ (<c>evaluation.read</c>, chủ hồ sơ luôn xem được) bằng <c>IAuthorizationGuard</c> trên hồ sơ.
/// </summary>
public interface IRecordFormService
{
    /// <summary>Biểu mẫu áp dụng cho hồ sơ (mẫu tự chấm của bộ + <c>requiredForms</c>; kèm 09A/09B nếu hồ sơ có dữ liệu tự chấm theo mẫu đó).</summary>
    Task<List<RecordFormDto>> GetFormsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Xuất một biểu mẫu của hồ sơ; mẫu không áp dụng cho kỳ → 409, mã không có → 400.</summary>
    Task<ReportFileResult> ExportAsync(Guid recordId, string formCode, ReportFormat format, CancellationToken ct = default);
}
