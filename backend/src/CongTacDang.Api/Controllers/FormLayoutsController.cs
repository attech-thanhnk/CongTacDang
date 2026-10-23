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

/// <summary>Danh mục kiểu trình bày biểu mẫu tự đánh giá (Mẫu 09C, 9D).</summary>
[ApiController]
[Route("api/form-layouts")]
[Authorize]
public class FormLayoutsController : ControllerBase
{
    private readonly IFormLayoutService _service;

    public FormLayoutsController(IFormLayoutService service) => _service = service;

    /// <summary>Danh mục kiểu (xem: mọi người đã đăng nhập).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var items = await _service.GetAllAsync(ct);
        return Ok(ApiResponse<List<FormLayoutDto>>.Ok(items, "Lấy danh mục kiểu trình bày thành công."));
    }

    [HttpPost]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Create([FromBody] SaveFormLayoutDto request, CancellationToken ct)
    {
        var item = await _service.CreateAsync(request, ct);
        return Ok(ApiResponse<FormLayoutDto>.Ok(item, "Đã thêm kiểu trình bày."));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveFormLayoutDto request, CancellationToken ct)
    {
        var item = await _service.UpdateAsync(id, request, ct);
        return Ok(ApiResponse<FormLayoutDto>.Ok(item, "Đã cập nhật kiểu trình bày."));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Đã xóa kiểu trình bày."));
    }
}

/// <summary>Kiểu trình bày biểu mẫu của cán bộ; service kiểm tra phạm vi theo đơn vị của cán bộ.</summary>
[ApiController]
[Route("api/users/{userId:guid}/form-layout")]
[Authorize]
public class MemberFormLayoutController : ControllerBase
{
    private readonly IFormLayoutService _service;

    public MemberFormLayoutController(IFormLayoutService service) => _service = service;

    /// <summary>Kiểu đang áp dụng, suy ra từ chức vụ, đặt riêng.</summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.SystemUsersRead)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken ct)
    {
        var info = await _service.GetMemberLayoutAsync(userId, ct);
        return Ok(ApiResponse<MemberFormLayoutDto>.Ok(info, "Lấy kiểu trình bày biểu mẫu thành công."));
    }

    /// <summary>Đặt riêng (LayoutId) hoặc bỏ đặt riêng (LayoutId = null).</summary>
    [HttpPut]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> Set(Guid userId, [FromBody] SetMemberFormLayoutDto request, CancellationToken ct)
    {
        var info = await _service.SetMemberLayoutAsync(userId, request, ct);
        return Ok(ApiResponse<MemberFormLayoutDto>.Ok(info,
            info.OverrideId == null ? "Đã bỏ đặt riêng — kiểu trình bày theo chức vụ." : "Đã đặt riêng kiểu trình bày."));
    }
}
