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
/// Biên bản hiệu chuẩn kết quả đánh giá (Mẫu 20). Service kiểm tra quyền trên từng hồ sơ trong biên bản
/// bằng <see cref="IAuthorizationGuard"/>.
/// </summary>
[ApiController]
[Route("api/calibrations")]
public class CalibrationController : ControllerBase
{
    private readonly ICalibrationService _calibrations;

    public CalibrationController(ICalibrationService calibrations)
    {
        _calibrations = calibrations;
    }

    /// <summary>Biên bản trong phạm vi của người dùng.</summary>
    [HttpGet]
    [RequireAnyPermission(PermissionCodes.EvaluationCalibrationManage, PermissionCodes.EvaluationCalibrationConclude, PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> List([FromQuery] Guid? periodId, CancellationToken ct)
    {
        var data = await _calibrations.ListAsync(periodId, ct);
        return Ok(ApiResponse<List<CalibrationSessionListItemDto>>.Ok(data, "Lấy danh sách biên bản hiệu chuẩn thành công."));
    }

    /// <summary>Hồ sơ đã thẩm định, chưa thuộc biên bản nào, người dùng lập được.</summary>
    [HttpGet("candidates")]
    [RequirePermission(PermissionCodes.EvaluationCalibrationManage)]
    public async Task<IActionResult> Candidates([FromQuery] Guid periodId, [FromQuery] Guid? departmentId, [FromQuery] string? workflowProfileCode,
        CancellationToken ct)
    {
        var data = await _calibrations.ListCandidatesAsync(periodId, departmentId, workflowProfileCode, ct);
        return Ok(ApiResponse<List<CalibrationCandidateDto>>.Ok(data, "Lấy danh sách hồ sơ có thể hiệu chuẩn thành công."));
    }

    [HttpGet("{id:guid}")]
    [RequireAnyPermission(PermissionCodes.EvaluationCalibrationManage, PermissionCodes.EvaluationCalibrationConclude, PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var data = await _calibrations.GetAsync(id, ct);
        return Ok(ApiResponse<CalibrationSessionDto>.Ok(data, "Lấy biên bản hiệu chuẩn thành công."));
    }

    [HttpGet("{id:guid}/history")]
    [RequireAnyPermission(PermissionCodes.EvaluationCalibrationManage, PermissionCodes.EvaluationCalibrationConclude, PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> History(Guid id, CancellationToken ct)
    {
        var data = await _calibrations.GetHistoryAsync(id, ct);
        return Ok(ApiResponse<List<CalibrationHistoryDto>>.Ok(data, "Lấy lịch sử biên bản hiệu chuẩn thành công."));
    }

    /// <summary>Lập biên bản nháp với các hồ sơ đã chọn.</summary>
    [HttpPost]
    [RequirePermission(PermissionCodes.EvaluationCalibrationManage)]
    public async Task<IActionResult> Create([FromBody] CreateCalibrationSessionDto dto, CancellationToken ct)
    {
        var data = await _calibrations.CreateAsync(dto, ct);
        return Ok(ApiResponse<CalibrationSessionDto>.Ok(data, "Đã lập biên bản hiệu chuẩn."));
    }

    /// <summary>Lưu bản nháp (version = phiên bản biên bản).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.EvaluationCalibrationManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCalibrationSessionDto dto, CancellationToken ct)
    {
        var data = await _calibrations.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<CalibrationSessionDto>.Ok(data, "Đã lưu biên bản hiệu chuẩn."));
    }

    /// <summary>Chốt biên bản và áp kết quả sau hiệu chuẩn lên hồ sơ.</summary>
    [HttpPost("{id:guid}/conclude")]
    [RequirePermission(PermissionCodes.EvaluationCalibrationConclude)]
    public async Task<IActionResult> Conclude(Guid id, [FromBody] ConcludeCalibrationDto dto, CancellationToken ct)
    {
        var data = await _calibrations.ConcludeAsync(id, dto, ct);
        return Ok(ApiResponse<CalibrationSessionDto>.Ok(data, "Đã chốt biên bản hiệu chuẩn."));
    }

    /// <summary>Mở lại biên bản đã chốt (bắt buộc lý do); hủy kết quả đã áp lên hồ sơ.</summary>
    [HttpPost("{id:guid}/reopen")]
    [RequirePermission(PermissionCodes.EvaluationCalibrationConclude)]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] CalibrationReasonDto dto, CancellationToken ct)
    {
        var data = await _calibrations.ReopenAsync(id, dto, ct);
        return Ok(ApiResponse<CalibrationSessionDto>.Ok(data, "Đã mở lại biên bản hiệu chuẩn."));
    }

    /// <summary>Hủy bản nháp (bắt buộc lý do).</summary>
    [HttpPost("{id:guid}/void")]
    [RequirePermission(PermissionCodes.EvaluationCalibrationManage)]
    public async Task<IActionResult> Void(Guid id, [FromBody] CalibrationReasonDto dto, CancellationToken ct)
    {
        var data = await _calibrations.VoidAsync(id, dto, ct);
        return Ok(ApiResponse<CalibrationSessionDto>.Ok(data, "Đã hủy biên bản hiệu chuẩn."));
    }
}
