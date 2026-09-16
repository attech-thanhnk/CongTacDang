using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/organizations")]
[Route("api/[controller]")]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _orgService;

    public OrganizationController(IOrganizationService orgService)
    {
        _orgService = orgService;
    }

    /// <summary>Lay danh sach cac Chi bo thuoc Dang bo ATTECH</summary>
    [HttpGet("branches")]
    [HttpGet("party-cells")]
    public async Task<IActionResult> GetPartyCells()
    {
        var branches = await _orgService.GetBranchesAsync();
        return Ok(ApiResponse<List<BranchDto>>.Ok(branches, "Lấy danh sách Chi bộ thành công."));
    }

    /// <summary>Them moi Chi bo Dang</summary>
    [HttpPost("branches")]
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

    /// <summary>Cap nhat thong tin Chi bo Dang</summary>
    [HttpPut("branches/{id}")]
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

    /// <summary>Xoa Chi bo Dang</summary>
    [HttpDelete("branches/{id}")]
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

    /// <summary>Lay danh sach cac Phong ban / Xuong san xuat chinh quyen</summary>
    [HttpGet("departments")]
    public async Task<IActionResult> GetDepartments()
    {
        var depts = await _orgService.GetDepartmentsAsync();
        return Ok(ApiResponse<List<DepartmentDto>>.Ok(depts, "Lấy danh sách đơn vị chuyên môn thành công."));
    }
}
