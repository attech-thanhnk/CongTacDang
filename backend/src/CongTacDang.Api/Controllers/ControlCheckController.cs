using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán (Mẫu 19). Controller chỉ yêu cầu có quyền ở phạm vi nào đó;
/// service kiểm tra quyền trên từng hồ sơ trong phiếu.
/// </summary>
[ApiController]
[Route("api/control-checks")]
public class ControlCheckController : ControllerBase
{
    private readonly IControlCheckService _checks;

    public ControlCheckController(IControlCheckService checks)
    {
        _checks = checks;
    }

    /// <summary>Danh sách phiếu trong phạm vi người xem.</summary>
    [HttpGet]
    [RequireAnyPermission(PermissionCodes.EvaluationControlPerform, PermissionCodes.EvaluationControlConfirm, PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> List([FromQuery] Guid? periodId, CancellationToken ct)
    {
        var data = await _checks.ListAsync(periodId, ct);
        return Ok(ApiResponse<List<ControlCheckSummaryDto>>.Ok(data, "Lấy danh sách phiếu kiểm soát thành công."));
    }

    [HttpGet("{id:guid}")]
    [RequireAnyPermission(PermissionCodes.EvaluationControlPerform, PermissionCodes.EvaluationControlConfirm, PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var data = await _checks.GetAsync(id, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Lấy phiếu kiểm soát thành công."));
    }

    /// <summary>Lập phiếu nháp cho một nhóm hồ sơ.</summary>
    [HttpPost]
    [RequirePermission(PermissionCodes.EvaluationControlPerform)]
    public async Task<IActionResult> Create([FromBody] CreateControlCheckDto dto, CancellationToken ct)
    {
        var data = await _checks.CreateAsync(dto, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Đã lập phiếu kiểm soát."));
    }

    /// <summary>Lưu bản nháp (version = phiên bản phiếu).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.EvaluationControlPerform)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateControlCheckDto dto, CancellationToken ct)
    {
        var data = await _checks.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Đã lưu phiếu kiểm soát."));
    }

    [HttpPost("{id:guid}/submit")]
    [RequirePermission(PermissionCodes.EvaluationControlPerform)]
    public async Task<IActionResult> Submit(Guid id, [FromBody] ControlCheckActionDto dto, CancellationToken ct)
    {
        var data = await _checks.SubmitAsync(id, dto, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Đã gửi phiếu kiểm soát chờ xác nhận."));
    }

    [HttpPost("{id:guid}/confirm")]
    [RequirePermission(PermissionCodes.EvaluationControlConfirm)]
    public async Task<IActionResult> Confirm(Guid id, [FromBody] ControlCheckActionDto dto, CancellationToken ct)
    {
        var data = await _checks.ConfirmAsync(id, dto, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Đã xác nhận phiếu kiểm soát."));
    }

    [HttpPost("{id:guid}/return")]
    [RequirePermission(PermissionCodes.EvaluationControlConfirm)]
    public async Task<IActionResult> Return(Guid id, [FromBody] ControlCheckActionDto dto, CancellationToken ct)
    {
        var data = await _checks.ReturnAsync(id, dto, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Đã trả lại phiếu kiểm soát."));
    }

    [HttpPost("{id:guid}/reopen")]
    [RequirePermission(PermissionCodes.EvaluationControlConfirm)]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ControlCheckActionDto dto, CancellationToken ct)
    {
        var data = await _checks.ReopenAsync(id, dto, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Đã mở lại phiếu kiểm soát."));
    }

    [HttpPost("{id:guid}/cancel")]
    [RequireAnyPermission(PermissionCodes.EvaluationControlPerform, PermissionCodes.EvaluationControlConfirm)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] ControlCheckActionDto dto, CancellationToken ct)
    {
        var data = await _checks.CancelAsync(id, dto, ct);
        return Ok(ApiResponse<ControlCheckDto>.Ok(data, "Đã hủy phiếu kiểm soát."));
    }
}
