using System;
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
}
