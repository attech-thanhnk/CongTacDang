using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Quản lý hồ sơ tập thể M06-M08 và biên bản hội nghị M12-M13.</summary>
[ApiController]
[Route("api/evaluations")]
[Authorize]
public class CollectiveEvaluationController : ControllerBase
{
    private readonly ICollectiveEvaluationService _service;

    public CollectiveEvaluationController(ICollectiveEvaluationService service)
    {
        _service = service;
    }

    /// <summary>Lấy hồ sơ đánh giá tập thể trong một kỳ.</summary>
    [HttpGet("collective-records")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> GetCollectiveRecords([FromQuery] Guid periodId, [FromQuery] string? form)
    {
        var records = await _service.GetCollectiveRecordsAsync(periodId, GetCurrentUserId(), form);
        return Ok(ApiResponse<List<CollectiveEvaluationRecordDto>>.Ok(records, "Lấy hồ sơ tập thể thành công."));
    }

    /// <summary>Lấy chi tiết một hồ sơ tập thể.</summary>
    [HttpGet("collective-records/{id}")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> GetCollectiveRecord(Guid id)
    {
        var record = await _service.GetCollectiveRecordAsync(id, GetCurrentUserId());
        return Ok(ApiResponse<CollectiveEvaluationRecordDto>.Ok(record, "Lấy chi tiết hồ sơ tập thể thành công."));
    }

    /// <summary>Tạo hồ sơ Mẫu 06, 07 hoặc 08.</summary>
    [HttpPost("collective-records")]
    [RequirePermission(PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> CreateCollectiveRecord([FromBody] SaveCollectiveEvaluationRequestDto dto)
    {
        var record = await _service.CreateCollectiveRecordAsync(GetCurrentUserId(), dto);
        return Ok(ApiResponse<CollectiveEvaluationRecordDto>.Ok(record, "Tạo hồ sơ tập thể thành công."));
    }

    /// <summary>Lấy biên bản hội nghị trong một kỳ.</summary>
    [HttpGet("meetings")]
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.MeetingManage)]
    public async Task<IActionResult> GetMeetings([FromQuery] Guid periodId, [FromQuery] Guid? partyCellId)
    {
        var meetings = await _service.GetMeetingsAsync(periodId, GetCurrentUserId(), partyCellId);
        return Ok(ApiResponse<List<EvaluationMeetingDto>>.Ok(meetings, "Lấy biên bản hội nghị thành công."));
    }

    /// <summary>Lấy chi tiết biên bản hội nghị.</summary>
    [HttpGet("meetings/{id}")]
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.MeetingManage)]
    public async Task<IActionResult> GetMeeting(Guid id)
    {
        var meeting = await _service.GetMeetingAsync(id, GetCurrentUserId());
        return Ok(ApiResponse<EvaluationMeetingDto>.Ok(meeting, "Lấy chi tiết biên bản hội nghị thành công."));
    }

    /// <summary>Tạo biên bản Mẫu 12 hoặc Mẫu 13.</summary>
    [HttpPost("meetings")]
    [RequirePermission(PermissionCodes.MeetingManage)]
    public async Task<IActionResult> CreateMeeting([FromBody] SaveEvaluationMeetingRequestDto dto)
    {
        var meeting = await _service.CreateMeetingAsync(GetCurrentUserId(), dto);
        return Ok(ApiResponse<EvaluationMeetingDto>.Ok(meeting, "Tạo biên bản hội nghị thành công."));
    }

    /// <summary>Lấy Id người dùng hiện tại từ claim JWT.</summary>
    private Guid GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var id))
            throw new UnauthorizedAccessException("Không xác định được người dùng hiện tại.");
        return id;
    }
}
