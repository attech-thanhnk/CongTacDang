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
    private readonly IAuthorizationGuard _guard;

    public HolidaysController(IHolidayService service, IAuthorizationGuard guard)
    {
        _service = service;
        _guard = guard;
    }

    // Ngày nghỉ áp dụng toàn công ty nên người quản lý kỳ phải có phạm vi toàn công ty.
    private void EnsureCanEdit()
    {
        if (!_guard.HasAny(PermissionCodes.CatalogManage))
            _guard.Ensure(PermissionCodes.PeriodManage, AccessTarget.None);
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
    [RequireAnyPermission(PermissionCodes.CatalogManage, PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Create([FromBody] SaveHolidayDto request)
    {
        EnsureCanEdit();
        var items = await _service.CreateAsync(request);
        return Ok(ApiResponse<List<HolidayDto>>.Ok(items, $"Đã thêm {items.Count} ngày nghỉ."));
    }

    /// <summary>Sửa ngày nghỉ.</summary>
    [HttpPut("{id:guid}")]
    [RequireAnyPermission(PermissionCodes.CatalogManage, PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveHolidayDto request)
    {
        EnsureCanEdit();
        var item = await _service.UpdateAsync(id, request);
        return Ok(ApiResponse<HolidayDto>.Ok(item, "Đã cập nhật ngày nghỉ."));
    }

    /// <summary>Xóa ngày nghỉ.</summary>
    [HttpDelete("{id:guid}")]
    [RequireAnyPermission(PermissionCodes.CatalogManage, PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Delete(Guid id)
    {
        EnsureCanEdit();
        await _service.DeleteAsync(id);
        return Ok(ApiResponse.Ok("Đã xóa ngày nghỉ."));
    }
}
