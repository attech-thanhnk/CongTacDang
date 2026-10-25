using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>Biểu mẫu cá nhân của hồ sơ: mẫu áp dụng theo bộ tiêu chí của kỳ, xuất Word/PDF.</summary>
public interface IRecordFormService
{
    /// <summary>Mẫu 09A + <c>requiredForms</c> của bộ tiêu chí của kỳ.</summary>
    Task<List<RecordFormDto>> GetFormsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Mẫu 04/05: mỗi đề nghị thay đổi việc của hồ sơ một phiếu.</summary>
    Task<List<RecordFormDto>> GetRequestFormsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Xuất Mẫu 04 (điều chỉnh, chuyển tiếp, loại trừ) hoặc Mẫu 05 (đột xuất) của một đề nghị; sai mẫu so với loại đề nghị → 409.</summary>
    Task<ReportFileResult> ExportRequestFormAsync(Guid recordId, string formCode, Guid requestId, ReportFormat format, CancellationToken ct = default);

    /// <summary>Mẫu không áp dụng cho kỳ → 409, mã không có → 400.</summary>
    Task<ReportFileResult> ExportAsync(Guid recordId, string formCode, ReportFormat format, CancellationToken ct = default);
}
