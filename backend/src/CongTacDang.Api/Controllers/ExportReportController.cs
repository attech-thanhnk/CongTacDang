using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Xuất báo cáo tổng hợp ra file Excel (Biểu mẫu 14, 15, danh sách cán bộ)</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = AppPermissions.ReportsExport)]
public class ExportReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ExportReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Xuất danh sách cán bộ ra file Excel</summary>
    [HttpGet("cadres")]
    public async Task<IActionResult> ExportCadres()
    {
        var result = await _reportService.ExportCadresReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 14 — Tổng hợp danh sách hồ sơ cán bộ theo Chi bộ và đơn vị</summary>
    [HttpGet("form-14")]
    public async Task<IActionResult> ExportMau14()
    {
        var result = await _reportService.ExportForm14ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15 — Thống kê cơ cấu tổ chức và sĩ số các Chi bộ</summary>
    [HttpGet("form-15")]
    public async Task<IActionResult> ExportMau15()
    {
        var result = await _reportService.ExportForm15ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }
}
