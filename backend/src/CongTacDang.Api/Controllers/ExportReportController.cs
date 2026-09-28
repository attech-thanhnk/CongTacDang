using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Xuất báo cáo tổng hợp và biểu mẫu chuẩn Đảng (.docx / .xlsx)</summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ExportReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ExportReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    #region Báo cáo Excel Quản trị

    /// <summary>Xuất danh sách cán bộ ra file Excel</summary>
    [HttpGet("cadres")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportCadres()
    {
        var result = await _reportService.ExportCadresReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 14 — Tổng hợp danh sách hồ sơ cán bộ theo Chi bộ và đơn vị</summary>
    [HttpGet("form-14")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau14()
    {
        var result = await _reportService.ExportForm14ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15 — Thống kê cơ cấu tổ chức và sĩ số các Chi bộ</summary>
    [HttpGet("form-15")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau15()
    {
        var result = await _reportService.ExportForm15ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15A — kiểm soát tỷ lệ trần 20% cấp Đảng ủy Công ty</summary>
    [HttpGet("form-15a")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau15A()
    {
        var result = await _reportService.ExportForm15AReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15B — kiểm soát tỷ lệ trần 20% theo từng Chi bộ</summary>
    [HttpGet("form-15b")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau15B()
    {
        var result = await _reportService.ExportForm15BReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 16 — tổng hợp kết quả xếp loại theo nhóm chức vụ</summary>
    [HttpGet("form-16")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau16()
    {
        var result = await _reportService.ExportForm16ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion

    #region Biểu mẫu Word (.docx) Chuẩn Hướng dẫn 03-HD/TVĐU

    /// <summary>Xuất Word Mẫu 01 — Phiếu giao/đăng ký sản phẩm chuyên môn hàng quý</summary>
    [HttpGet("docx/mau-01/{recordId}")]
    public async Task<IActionResult> ExportMau01Docx(Guid recordId)
    {
        var result = await _reportService.ExportMau01DocxAsync(recordId);
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm hàng quý</summary>
    [HttpGet("docx/mau-02/{recordId}")]
    public async Task<IActionResult> ExportMau02Docx(Guid recordId)
    {
        var result = await _reportService.ExportMau02DocxAsync(recordId);
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 10 — Phiếu thẩm định, nhận xét và ghi nhận giải trình</summary>
    [HttpGet("docx/mau-10/{recordId}")]
    public async Task<IActionResult> ExportMau10Docx(Guid recordId)
    {
        var result = await _reportService.ExportMau10DocxAsync(recordId);
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 11 — Phiếu đánh giá, xếp loại cán bộ quý (Bỏ phiếu kín Chi bộ)</summary>
    [HttpGet("docx/mau-11")]
    public async Task<IActionResult> ExportMau11Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId)
    {
        var result = await _reportService.ExportMau11DocxAsync(periodId, branchId);
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 13 — Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý</summary>
    [HttpGet("docx/mau-13")]
    public async Task<IActionResult> ExportMau13Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId)
    {
        var result = await _reportService.ExportMau13DocxAsync(periodId, branchId);
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion
}
