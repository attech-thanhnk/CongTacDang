using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Danh mục tổ chức: Chi bộ và Phòng/đơn vị chuyên môn.
/// Xem: mọi người đã đăng nhập. Thêm/sửa/xóa: quyền <c>catalog.manage</c>.
/// Lỗi nghiệp vụ (400/404/409) do service ném, <c>GlobalExceptionMiddleware</c> trả về <c>ApiResponse</c>.
/// </summary>
[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _orgService;

    public OrganizationController(IOrganizationService orgService)
    {
        _orgService = orgService;
    }

    // ===================== Chi bộ =====================

    /// <summary>Danh sách Chi bộ thuộc Đảng bộ ATTECH</summary>
    [HttpGet("branches")]
    public async Task<IActionResult> GetPartyCells()
    {
        var branches = await _orgService.GetBranchesAsync();
        return Ok(ApiResponse<List<BranchDto>>.Ok(branches, "Lấy danh sách Chi bộ thành công."));
    }

    /// <summary>Chi tiết một Chi bộ theo ID</summary>
    [HttpGet("branches/{id:guid}")]
    public async Task<IActionResult> GetBranchById(Guid id)
    {
        var branch = await _orgService.GetBranchByIdAsync(id);
        if (branch == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy Chi bộ."));

        return Ok(ApiResponse<BranchDto>.Ok(branch, "Lấy thông tin Chi bộ thành công."));
    }

    /// <summary>Thêm mới Chi bộ</summary>
    [HttpPost("branches")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> CreateBranch([FromBody] SaveCatalogItemDto request)
    {
        var branch = await _orgService.CreateBranchAsync(request);
        return Ok(ApiResponse<BranchDto>.Ok(branch, "Thêm mới Chi bộ thành công."));
    }

    /// <summary>Cập nhật thông tin Chi bộ (kể cả ngừng hoạt động / hoạt động lại)</summary>
    [HttpPut("branches/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] SaveCatalogItemDto request)
    {
        var branch = await _orgService.UpdateBranchAsync(id, request);
        return Ok(ApiResponse<BranchDto>.Ok(branch, "Cập nhật Chi bộ thành công."));
    }

    /// <summary>Xóa Chi bộ (409 khi còn cán bộ hoặc hồ sơ đánh giá)</summary>
    [HttpDelete("branches/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> DeleteBranch(Guid id)
    {
        await _orgService.DeleteBranchAsync(id);
        return Ok(ApiResponse.Ok("Đã xóa Chi bộ thành công."));
    }

    // ===================== Phòng / đơn vị =====================

    /// <summary>Danh sách Phòng/đơn vị chuyên môn</summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var depts = await _orgService.GetDepartmentsAsync();
        return Ok(ApiResponse<List<DepartmentDto>>.Ok(depts, "Lấy danh sách đơn vị chuyên môn thành công."));
    }

    /// <summary>Chi tiết Phòng/đơn vị theo ID</summary>
    [HttpGet("departments/{id:guid}")]
    public async Task<IActionResult> GetDepartmentById(Guid id)
    {
        var department = await _orgService.GetDepartmentByIdAsync(id);
        if (department == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy Phòng/đơn vị."));

        return Ok(ApiResponse<DepartmentDto>.Ok(department, "Lấy thông tin Phòng/đơn vị thành công."));
    }

    /// <summary>Thêm mới Phòng/đơn vị</summary>
    [HttpPost("departments")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> CreateDepartment([FromBody] SaveCatalogItemDto request)
    {
        var department = await _orgService.CreateDepartmentAsync(request);
        return Ok(ApiResponse<DepartmentDto>.Ok(department, "Thêm mới Phòng/đơn vị thành công."));
    }

    /// <summary>Cập nhật Phòng/đơn vị (kể cả ngừng hoạt động / hoạt động lại)</summary>
    [HttpPut("departments/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> UpdateDepartment(Guid id, [FromBody] SaveCatalogItemDto request)
    {
        var department = await _orgService.UpdateDepartmentAsync(id, request);
        return Ok(ApiResponse<DepartmentDto>.Ok(department, "Cập nhật Phòng/đơn vị thành công."));
    }

    /// <summary>Xóa Phòng/đơn vị (409 khi còn cán bộ hoặc hồ sơ đánh giá)</summary>
    [HttpDelete("departments/{id:guid}")]
    [RequirePermission(PermissionCodes.CatalogManage)]
    public async Task<IActionResult> DeleteDepartment(Guid id)
    {
        await _orgService.DeleteDepartmentAsync(id);
        return Ok(ApiResponse.Ok("Đã xóa Phòng/đơn vị thành công."));
    }
}
