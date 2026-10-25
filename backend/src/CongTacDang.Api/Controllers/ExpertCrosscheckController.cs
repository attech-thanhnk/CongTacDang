using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Phiếu đối soát chuyên ngành (Mẫu 03). Controller chỉ yêu cầu có quyền ở phạm vi nào đó;
/// service kiểm tra quyền trên hồ sơ và trên đơn vị đối soát của từng phiếu.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class ExpertCrosscheckController : ControllerBase
{
    private readonly IExpertCrosscheckService _crosschecks;

    public ExpertCrosscheckController(IExpertCrosscheckService crosschecks)
    {
        _crosschecks = crosschecks;
    }

    /// <summary>Khối "Đối soát chuyên ngành" của hồ sơ.</summary>
    [HttpGet("evaluations/records/{recordId:guid}/crosschecks")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCrosscheckRequest,
        PermissionCodes.EvaluationCrosscheckRespond, PermissionCodes.EvaluationCrosscheckConfirm)]
    public async Task<IActionResult> GetByRecord(Guid recordId, CancellationToken ct)
    {
        var data = await _crosschecks.GetByRecordAsync(recordId, ct);
        return Ok(ApiResponse<RecordCrosschecksDto>.Ok(data, "Lấy danh sách phiếu đối soát của hồ sơ thành công."));
    }

    /// <summary>Yêu cầu đối soát cho hồ sơ.</summary>
    [HttpPost("evaluations/records/{recordId:guid}/crosschecks")]
    [RequirePermission(PermissionCodes.EvaluationCrosscheckRequest)]
    public async Task<IActionResult> Create(Guid recordId, [FromBody] SaveCrosscheckRequestDto dto, CancellationToken ct)
    {
        var data = await _crosschecks.CreateAsync(recordId, dto, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Đã gửi yêu cầu đối soát chuyên ngành."));
    }

    /// <summary>Phiếu trong phạm vi xem; lọc theo kỳ và trạng thái.</summary>
    [HttpGet("crosschecks")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCrosscheckRequest,
        PermissionCodes.EvaluationCrosscheckRespond, PermissionCodes.EvaluationCrosscheckConfirm)]
    public async Task<IActionResult> List([FromQuery] Guid? periodId, [FromQuery] string? status, CancellationToken ct)
    {
        var data = await _crosschecks.ListAsync(periodId, status, ct);
        return Ok(ApiResponse<List<ExpertCrosscheckSummaryDto>>.Ok(data, "Lấy danh sách phiếu đối soát thành công."));
    }

    [HttpGet("crosschecks/{id:guid}")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.EvaluationCrosscheckRequest,
        PermissionCodes.EvaluationCrosscheckRespond, PermissionCodes.EvaluationCrosscheckConfirm)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var data = await _crosschecks.GetAsync(id, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Lấy phiếu đối soát thành công."));
    }

    /// <summary>Sửa nội dung yêu cầu khi cơ quan đối soát chưa lập.</summary>
    [HttpPut("crosschecks/{id:guid}")]
    [RequirePermission(PermissionCodes.EvaluationCrosscheckRequest)]
    public async Task<IActionResult> UpdateRequest(Guid id, [FromBody] SaveCrosscheckRequestDto dto, CancellationToken ct)
    {
        var data = await _crosschecks.UpdateRequestAsync(id, dto, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Đã cập nhật yêu cầu đối soát."));
    }

    /// <summary>Lưu kết quả đối soát (chưa gửi xác nhận).</summary>
    [HttpPut("crosschecks/{id:guid}/response")]
    [RequireAnyPermission(PermissionCodes.EvaluationCrosscheckRespond, PermissionCodes.EvaluationCrosscheckRequest)]
    public async Task<IActionResult> SaveResponse(Guid id, [FromBody] SaveCrosscheckResponseDto dto, CancellationToken ct)
    {
        var data = await _crosschecks.SaveResponseAsync(id, dto, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Đã lưu kết quả đối soát."));
    }

    [HttpPost("crosschecks/{id:guid}/submit")]
    [RequireAnyPermission(PermissionCodes.EvaluationCrosscheckRespond, PermissionCodes.EvaluationCrosscheckRequest)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] CrosscheckActionDto dto, CancellationToken ct)
    {
        var data = await _crosschecks.SubmitAsync(id, dto, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Đã gửi phiếu đối soát chờ xác nhận."));
    }

    [HttpPost("crosschecks/{id:guid}/confirm")]
    [RequirePermission(PermissionCodes.EvaluationCrosscheckConfirm)]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] CrosscheckActionDto dto, CancellationToken ct)
    {
        var data = await _crosschecks.ConfirmAsync(id, dto, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Đã xác nhận phiếu đối soát."));
    }

    /// <summary>Trả lại phiếu để lập lại (bắt buộc lý do).</summary>
    [HttpPost("crosschecks/{id:guid}/return")]
    [RequirePermission(PermissionCodes.EvaluationCrosscheckConfirm)]
    public async Task<IActionResult> Return(Guid id, [FromBody] CrosscheckActionDto dto, CancellationToken ct)
    {
        var data = await _crosschecks.ReturnAsync(id, dto, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Đã trả lại phiếu đối soát."));
    }

    /// <summary>Hủy yêu cầu (bắt buộc lý do).</summary>
    [HttpPost("crosschecks/{id:guid}/cancel")]
    [RequirePermission(PermissionCodes.EvaluationCrosscheckRequest)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CrosscheckActionDto dto, CancellationToken ct)
    {
        var data = await _crosschecks.CancelAsync(id, dto, ct);
        return Ok(ApiResponse<ExpertCrosscheckDto>.Ok(data, "Đã hủy yêu cầu đối soát."));
    }
}
