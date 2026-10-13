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

/// <summary>
/// "Người thực hiện": ai làm được việc gì của quy trình đánh giá tại một đơn vị (docs/thiet-ke/phan-quyen.md mục 5).
/// Thêm/gỡ người dùng API gán vai trò (<c>/api/admin/assignments</c>).
/// </summary>
[ApiController]
[Route("api/admin/performers")]
public class AdminPerformerController : ControllerBase
{
    private readonly IPerformerService _performers;

    /// <summary>Khởi tạo controller.</summary>
    public AdminPerformerController(IPerformerService performers) => _performers = performers;

    /// <summary>
    /// Các việc và người làm được tại đơn vị: <c>?departmentId=</c> (đơn vị chính quyền) hoặc <c>?partyCellId=</c>
    /// (tổ chức Đảng); bỏ trống cả hai = Toàn công ty.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Get([FromQuery] Guid? departmentId, [FromQuery] Guid? partyCellId)
    {
        var result = await _performers.GetAsync(departmentId, partyCellId, HttpContext.RequestAborted);
        return Ok(ApiResponse<UnitPerformersDto>.Ok(result, "Lấy danh sách người thực hiện thành công."));
    }

    /// <summary>
    /// Bản gán cần xem lại: gán không thời hạn tại đơn vị mà người được giao không còn giữ chức vụ hay sinh hoạt ở đó,
    /// hoặc giao tạm sắp hết hạn. <c>?userId=</c> để xem riêng một người.
    /// </summary>
    [HttpGet("review")]
    [RequirePermission(PermissionCodes.SystemAssignmentsManage)]
    public async Task<IActionResult> Review([FromQuery] Guid? userId)
    {
        var result = await _performers.GetReviewAsync(userId, HttpContext.RequestAborted);
        return Ok(ApiResponse<List<PerformerReviewItemDto>>.Ok(result, "Lấy danh sách bản gán cần xem lại thành công."));
    }
}
