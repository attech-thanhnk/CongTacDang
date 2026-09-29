using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Biểu mẫu cá nhân của hồ sơ đánh giá (task 18 — T-83): Mẫu 09A/09B/09C/9D (và 01/02/10) theo bộ tiêu chí của kỳ, xuất Word
/// hoặc PDF (<c>?format=pdf</c>). Quyền: xem được hồ sơ (<c>evaluation.read</c> trong phạm vi; chủ hồ sơ luôn xem được) — kiểm tra
/// trên từng hồ sơ trong service qua <c>IAuthorizationGuard</c>.
/// </summary>
[ApiController]
[Route("api/reports/docx/record")]
[Authorize]
public class RecordFormsController : ControllerBase
{
    private readonly IRecordFormService _forms;

    public RecordFormsController(IRecordFormService forms) => _forms = forms;

    /// <summary>Biểu mẫu áp dụng cho hồ sơ (mã, tên).</summary>
    [HttpGet("{recordId:guid}")]
    public async Task<IActionResult> GetForms(Guid recordId, CancellationToken ct)
    {
        var forms = await _forms.GetFormsAsync(recordId, ct);
        return Ok(ApiResponse<List<RecordFormDto>>.Ok(forms, "Lấy danh sách biểu mẫu của hồ sơ thành công."));
    }

    /// <summary>Xuất một biểu mẫu của hồ sơ (<paramref name="formCode"/>: 01, 02, 09A, 09B, 09C, 9D, 10).</summary>
    [HttpGet("{recordId:guid}/{formCode}")]
    public async Task<IActionResult> Export(Guid recordId, string formCode, [FromQuery] string? format, CancellationToken ct)
    {
        var result = await _forms.ExportAsync(recordId, formCode, ParseFormat(format), ct);
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary><c>docx</c> (hoặc bỏ trống) = Word; <c>pdf</c> = PDF chuyển phía máy chủ.</summary>
    private static ReportFormat ParseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return ReportFormat.Original;
        return format.Trim().ToLowerInvariant() switch
        {
            "docx" or "original" => ReportFormat.Original,
            "pdf" => ReportFormat.Pdf,
            _ => throw new ArgumentException("Tham số format chỉ nhận docx hoặc pdf.")
        };
    }
}
