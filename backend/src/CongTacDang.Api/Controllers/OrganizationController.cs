using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Quản lý tổ chức Chi bộ và Phòng ban chuyên môn</summary>
[ApiController]
[Route("api/organizations")]
[Authorize] // Tất cả endpoint yêu cầu đăng nhập
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _orgService;

    public OrganizationController(IOrganizationService orgService)
    {
        _orgService = orgService;
    }

    /// <summary>Danh sách Chi bộ thuộc Đảng bộ ATTECH</summary>
    [HttpGet("branches")]
    [HttpGet("party-cells")]
    public async Task<IActionResult> GetPartyCells()
    {
        var branches = await _orgService.GetBranchesAsync();
        return Ok(ApiResponse<List<BranchDto>>.Ok(branches, "Lấy danh sách Chi bộ thành công."));
    }

    /// <summary>Chi tiết một Chi bộ theo ID</summary>
    [HttpGet("branches/{id}")]
    public async Task<IActionResult> GetBranchById(Guid id)
    {
        var branch = await _orgService.GetBranchByIdAsync(id);
        if (branch == null)
            return NotFound(ApiResponse.Fail("Không tìm thấy Chi bộ."));

        return Ok(ApiResponse<BranchDto>.Ok(branch, "Lấy thông tin Chi bộ thành công."));
    }

    /// <summary>Thêm mới Chi bộ — chỉ Ban Thường vụ trở lên</summary>
    [HttpPost("branches")]
    [Authorize(Policy = "RequireBanThuongVu")]
    public async Task<IActionResult> CreateBranch([FromBody] CreateBranchDto request)
    {
        try
        {
            var branch = await _orgService.CreateBranchAsync(request);
            return Ok(ApiResponse<BranchDto>.Ok(branch, "Thêm mới Chi bộ thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Cập nhật thông tin Chi bộ — chỉ Ban Thường vụ trở lên</summary>
    [HttpPut("branches/{id}")]
    [Authorize(Policy = "RequireBanThuongVu")]
    public async Task<IActionResult> UpdateBranch(Guid id, [FromBody] UpdateBranchDto request)
    {
        try
        {
            var branch = await _orgService.UpdateBranchAsync(id, request);
            return Ok(ApiResponse<BranchDto>.Ok(branch, "Cập nhật Chi bộ thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Xóa Chi bộ — chỉ Ban Thường vụ trở lên</summary>
    [HttpDelete("branches/{id}")]
    [Authorize(Policy = "RequireBanThuongVu")]
    public async Task<IActionResult> DeleteBranch(Guid id)
    {
        try
        {
            await _orgService.DeleteBranchAsync(id);
            return Ok(ApiResponse.Ok("Đã xóa Chi bộ thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>Danh sách Phòng ban chuyên môn</summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var depts = await _orgService.GetDepartmentsAsync();
        return Ok(ApiResponse<List<DepartmentDto>>.Ok(depts, "Lấy danh sách đơn vị chuyên môn thành công."));
    }
}
