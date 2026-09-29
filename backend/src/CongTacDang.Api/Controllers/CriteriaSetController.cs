using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Bộ tiêu chí và thang điểm theo phiên bản (task 16). Xem: <c>criteria.manage</c> hoặc <c>period.manage</c> (chọn bộ cho kỳ);
/// ghi: <c>criteria.manage</c>. Bộ đã xuất bản không sửa được — nhân bản thành bản nháp mới.
/// </summary>
[ApiController]
[Route("api/criteria-sets")]
[Authorize]
public class CriteriaSetController : ControllerBase
{
    private readonly ICriteriaSetService _sets;

    public CriteriaSetController(ICriteriaSetService sets)
    {
        _sets = sets;
    }

    /// <summary>Danh sách bộ tiêu chí.</summary>
    [HttpGet]
    [RequireAnyPermission(PermissionCodes.CriteriaManage, PermissionCodes.PeriodManage)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(ApiResponse<List<CriteriaSetListItemDto>>.Ok(await _sets.ListAsync(ct), "Lấy danh sách bộ tiêu chí thành công."));

    /// <summary>
    /// Khung tỷ trọng A-B-C-D chọn được cho hồ sơ cán bộ (từ bộ tiêu chí đang dùng gần nhất) — mọi người đã đăng nhập xem được
    /// (danh mục, không phải dữ liệu đánh giá).
    /// </summary>
    [HttpGet("weight-frames")]
    public async Task<IActionResult> WeightFrames(CancellationToken ct) =>
        Ok(ApiResponse<WeightFrameOptionsDto>.Ok(await _sets.GetWeightFrameOptionsAsync(ct), "Lấy danh sách khung tỷ trọng thành công."));

    /// <summary>Nội dung mặc định theo bản trích xuất HD03 cho mẫu tự chấm (09A/09B) — dùng khi tạo bộ mới.</summary>
    [HttpGet("defaults/{form}")]
    [RequirePermission(PermissionCodes.CriteriaManage)]
    public IActionResult Defaults(string form) =>
        Ok(ApiResponse<CriteriaSetContent>.Ok(_sets.DefaultContent(form), "Lấy nội dung mặc định thành công."));

    /// <summary>Chi tiết bộ tiêu chí (kèm nội dung và lỗi kiểm tra).</summary>
    [HttpGet("{id:guid}")]
    [RequireAnyPermission(PermissionCodes.CriteriaManage, PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<CriteriaSetDto>.Ok(await _sets.GetAsync(id, ct), "Lấy bộ tiêu chí thành công."));

    /// <summary>Tạo bản nháp.</summary>
    [HttpPost]
    [RequirePermission(PermissionCodes.CriteriaManage)]
    public async Task<IActionResult> Create([FromBody] CreateCriteriaSetDto dto, CancellationToken ct) =>
        Ok(ApiResponse<CriteriaSetDto>.Ok(await _sets.CreateAsync(dto, ct), "Đã tạo bộ tiêu chí (bản nháp)."));

    /// <summary>Nhân bản thành bản nháp mới.</summary>
    [HttpPost("{id:guid}/clone")]
    [RequirePermission(PermissionCodes.CriteriaManage)]
    public async Task<IActionResult> Clone(Guid id, [FromBody] CloneCriteriaSetDto dto, CancellationToken ct) =>
        Ok(ApiResponse<CriteriaSetDto>.Ok(await _sets.CloneAsync(id, dto, ct), "Đã nhân bản bộ tiêu chí thành bản nháp mới."));

    /// <summary>Sửa bản nháp.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CriteriaManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCriteriaSetDto dto, CancellationToken ct) =>
        Ok(ApiResponse<CriteriaSetDto>.Ok(await _sets.UpdateAsync(id, dto, ct), "Đã lưu bản nháp bộ tiêu chí."));

    /// <summary>Xuất bản (kiểm tra đầy đủ; sau đó không sửa được).</summary>
    [HttpPost("{id:guid}/publish")]
    [RequirePermission(PermissionCodes.CriteriaManage)]
    public async Task<IActionResult> Publish(Guid id, [FromBody] CriteriaSetActionDto dto, CancellationToken ct) =>
        Ok(ApiResponse<CriteriaSetDto>.Ok(await _sets.PublishAsync(id, dto, ct), "Đã xuất bản bộ tiêu chí."));

    /// <summary>Lưu trữ bộ đã xuất bản (kỳ đã dùng giữ nguyên ảnh chụp; không chọn được cho kỳ mới).</summary>
    [HttpPost("{id:guid}/archive")]
    [RequirePermission(PermissionCodes.CriteriaManage)]
    public async Task<IActionResult> Archive(Guid id, [FromBody] CriteriaSetActionDto dto, CancellationToken ct) =>
        Ok(ApiResponse<CriteriaSetDto>.Ok(await _sets.ArchiveAsync(id, dto, ct), "Đã lưu trữ bộ tiêu chí."));

    /// <summary>Xóa bản nháp.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CriteriaManage)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] uint? version, CancellationToken ct)
    {
        await _sets.DeleteAsync(id, new CriteriaSetActionDto { Version = version }, ct);
        return Ok(ApiResponse.Ok("Đã xóa bản nháp bộ tiêu chí."));
    }
}
