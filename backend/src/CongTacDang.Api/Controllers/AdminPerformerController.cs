using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>Ai làm được việc gì tại một đơn vị (chỉ đọc; thêm/gỡ qua <c>/api/admin/assignments</c>).</summary>
[ApiController]
[Route("api/admin/performers")]
public class AdminPerformerController : ControllerBase
{
    private readonly IPerformerService _performers;

    public AdminPerformerController(IPerformerService performers) => _performers = performers;

    /// <summary>Theo departmentId hoặc partyCellId; bỏ trống cả hai = Toàn công ty.</summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Get([FromQuery] Guid? departmentId, [FromQuery] Guid? partyCellId)
    {
        var result = await _performers.GetAsync(departmentId, partyCellId, HttpContext.RequestAborted);
        return Ok(ApiResponse<UnitPerformersDto>.Ok(result, "Lấy danh sách người thực hiện thành công."));
    }

    /// <summary>Bản gán cần xem lại: người không còn ở đơn vị được gán, hoặc giao tạm sắp hết hạn.</summary>
    [HttpGet("review")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Review([FromQuery] Guid? userId)
    {
        var result = await _performers.GetReviewAsync(userId, HttpContext.RequestAborted);
        return Ok(ApiResponse<List<PerformerReviewItemDto>>.Ok(result, "Lấy danh sách bản gán cần xem lại thành công."));
    }
}
