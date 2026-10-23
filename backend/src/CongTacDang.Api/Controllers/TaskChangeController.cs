using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Điều chỉnh việc trong quý (Mẫu 04, 05). Quyền duyệt cấu hình theo hồ sơ luồng nên controller chỉ yêu cầu đăng nhập;
/// service kiểm tra quyền trên hồ sơ.
/// </summary>
[ApiController]
[Route("api/evaluations/records/{recordId:guid}/task-changes")]
[Authorize]
public class TaskChangeController : ControllerBase
{
    private readonly ITaskChangeService _changes;

    public TaskChangeController(ITaskChangeService changes)
    {
        _changes = changes;
    }

    /// <summary>Danh sách đề nghị của hồ sơ và cờ hành động của người xem.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid recordId, CancellationToken ct)
    {
        var data = await _changes.GetAsync(recordId, ct);
        return Ok(ApiResponse<RecordTaskChangesDto>.Ok(data, "Lấy danh sách đề nghị điều chỉnh việc thành công."));
    }

    /// <summary>Gửi đề nghị (version = phiên bản hồ sơ).</summary>
    [HttpPost]
    public async Task<IActionResult> Create(Guid recordId, [FromBody] CreateTaskChangeRequestDto dto, CancellationToken ct)
    {
        var data = await _changes.CreateAsync(recordId, dto, ct);
        return Ok(ApiResponse<RecordTaskChangesDto>.Ok(data, "Đã gửi đề nghị điều chỉnh việc."));
    }

    /// <summary>Duyệt đề nghị (version = phiên bản đề nghị) — áp vào danh mục việc của hồ sơ.</summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid recordId, Guid id, [FromBody] DecideTaskChangeRequestDto dto, CancellationToken ct)
    {
        var data = await _changes.ApproveAsync(recordId, id, dto, ct);
        return Ok(ApiResponse<RecordTaskChangesDto>.Ok(data, "Đã duyệt đề nghị, danh mục việc đã được cập nhật."));
    }

    /// <summary>Không duyệt đề nghị (bắt buộc lý do).</summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid recordId, Guid id, [FromBody] DecideTaskChangeRequestDto dto, CancellationToken ct)
    {
        var data = await _changes.RejectAsync(recordId, id, dto, ct);
        return Ok(ApiResponse<RecordTaskChangesDto>.Ok(data, "Đã không duyệt đề nghị."));
    }
}
