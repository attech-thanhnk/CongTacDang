using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>Danh mục ngày nghỉ dùng chung (lễ, Tết, nghỉ bù) để tính thời hạn theo ngày làm việc.</summary>
[ApiController]
[Route("api/holidays")]
[Authorize]
public class HolidaysController : ControllerBase
{
    private readonly IHolidayService _service;

    public HolidaysController(IHolidayService service)
    {
        _service = service;
    }

    /// <summary>Ngày nghỉ của một năm (xem: mọi người đã đăng nhập).</summary>
    [HttpGet]
    public async Task<IActionResult> GetByYear([FromQuery] int year)
    {
        var items = await _service.GetByYearAsync(year);
        return Ok(ApiResponse<List<HolidayDto>>.Ok(items, "Lấy danh mục ngày nghỉ thành công."));
    }

    /// <summary>Thêm một ngày hoặc một khoảng ngày nghỉ.</summary>
    [HttpPost]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Create([FromBody] SaveHolidayDto request)
    {
        var items = await _service.CreateAsync(request);
        return Ok(ApiResponse<List<HolidayDto>>.Ok(items, $"Đã thêm {items.Count} ngày nghỉ."));
    }

    /// <summary>Sửa ngày nghỉ.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveHolidayDto request)
    {
        var item = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<HolidayDto>.Ok(item, "Đã cập nhật ngày nghỉ."));
    }

    /// <summary>Xóa ngày nghỉ.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("Đã xóa ngày nghỉ."));
    }
}
