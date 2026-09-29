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

/// <summary>
/// Danh mục chức vụ (task 14). Xem: mọi người đã đăng nhập. Thêm/sửa/xóa: <c>catalog.manage</c>.
/// </summary>
[ApiController]
[Route("api/positions")]
[Authorize]
public class PositionController : ControllerBase
{
    private readonly IPositionService _positions;

    /// <summary>Khởi tạo controller.</summary>
    public PositionController(IPositionService positions) => _positions = positions;

    /// <summary>Danh mục chức vụ kèm số người đang giữ.</summary>
    [HttpGet]
    public async Task<IActionResult> GetPositions(CancellationToken ct)
    {
        var positions = await _positions.GetPositionsAsync(ct);
        return Ok(ApiResponse<List<PositionDto>>.Ok(positions, "Lấy danh mục chức vụ thành công."));
    }

    /// <summary>Thêm chức vụ.</summary>
    [HttpPost]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Create([FromBody] SavePositionDto request, CancellationToken ct)
    {
        var position = await _positions.CreatePositionAsync(request, ct);
        return Ok(ApiResponse<PositionDto>.Ok(position, "Đã thêm chức vụ."));
    }

    /// <summary>Sửa chức vụ (kể cả ngừng dùng).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SavePositionDto request, CancellationToken ct)
    {
        var position = await _positions.UpdatePositionAsync(id, request, ct);
        return Ok(ApiResponse<PositionDto>.Ok(position, "Đã cập nhật chức vụ."));
    }

    /// <summary>Xóa chức vụ chưa từng được gán (409 nếu đã gán — hãy ngừng dùng).</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _positions.DeletePositionAsync(id, ct);
        return Ok(ApiResponse.Ok("Đã xóa chức vụ."));
    }
}

/// <summary>
/// Chức vụ (kể cả kiêm nhiệm) và thẩm quyền phê duyệt của một cán bộ (task 14).
/// Xem: <c>system.users.read</c>; sửa: <c>system.users.manage</c> — service kiểm tra phạm vi theo đơn vị của cán bộ.
/// </summary>
[ApiController]
[Route("api/users/{userId:guid}")]
[Authorize]
public class MemberPositionController : ControllerBase
{
    private readonly IPositionService _positions;

    /// <summary>Khởi tạo controller.</summary>
    public MemberPositionController(IPositionService positions) => _positions = positions;

    /// <summary>Chức vụ của cán bộ (đang và đã giữ).</summary>
    [HttpGet("positions")]
    [RequirePermission(PermissionCodes.SystemUsersRead)]
    public async Task<IActionResult> List(Guid userId, CancellationToken ct)
    {
        var items = await _positions.GetMemberPositionsAsync(userId, ct);
        return Ok(ApiResponse<List<MemberPositionDto>>.Ok(items, "Lấy danh sách chức vụ thành công."));
    }

    /// <summary>Thêm chức vụ (chính hoặc kiêm nhiệm).</summary>
    [HttpPost("positions")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> Add(Guid userId, [FromBody] SaveMemberPositionDto request, CancellationToken ct)
    {
        var item = await _positions.AddMemberPositionAsync(userId, request, ct);
        return Ok(ApiResponse<MemberPositionDto>.Ok(item, "Đã thêm chức vụ cho cán bộ."));
    }

    /// <summary>Sửa chức vụ (đơn vị, chính/kiêm nhiệm, thời hạn, ghi chú).</summary>
    [HttpPut("positions/{id:guid}")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> Update(Guid userId, Guid id, [FromBody] SaveMemberPositionDto request, CancellationToken ct)
    {
        var item = await _positions.UpdateMemberPositionAsync(userId, id, request, ct);
        return Ok(ApiResponse<MemberPositionDto>.Ok(item, "Đã cập nhật chức vụ."));
    }

    /// <summary>Kết thúc chức vụ ngay bây giờ.</summary>
    [HttpPost("positions/{id:guid}/end")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> End(Guid userId, Guid id, CancellationToken ct)
    {
        var item = await _positions.EndMemberPositionAsync(userId, id, ct);
        return Ok(ApiResponse<MemberPositionDto>.Ok(item, "Đã kết thúc chức vụ."));
    }

    /// <summary>Xóa bản ghi chức vụ nhập nhầm.</summary>
    [HttpDelete("positions/{id:guid}")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> Delete(Guid userId, Guid id, CancellationToken ct)
    {
        await _positions.DeleteMemberPositionAsync(userId, id, ct);
        return Ok(ApiResponse.Ok("Đã xóa chức vụ."));
    }

    /// <summary>Thẩm quyền phê duyệt: đang áp dụng, suy ra từ chức vụ, đặt tay; mã chức danh thống kê.</summary>
    [HttpGet("approval-authority")]
    [RequirePermission(PermissionCodes.SystemUsersRead)]
    public async Task<IActionResult> GetApprovalAuthority(Guid userId, CancellationToken ct)
    {
        var info = await _positions.GetApprovalAuthorityAsync(userId, ct);
        return Ok(ApiResponse<ApprovalAuthorityInfoDto>.Ok(info, "Lấy thẩm quyền phê duyệt thành công."));
    }

    /// <summary>Đặt tay (kèm lý do) hoặc bỏ đặt tay thẩm quyền phê duyệt.</summary>
    [HttpPut("approval-authority")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> SetApprovalAuthority(Guid userId, [FromBody] SetApprovalAuthorityOverrideDto request, CancellationToken ct)
    {
        var info = await _positions.SetApprovalAuthorityOverrideAsync(userId, request, ct);
        return Ok(ApiResponse<ApprovalAuthorityInfoDto>.Ok(info,
            info.Override == null ? "Đã bỏ đặt tay — thẩm quyền suy ra từ chức vụ." : "Đã đặt tay thẩm quyền phê duyệt."));
    }
}
