using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Xuất báo cáo tổng hợp ra file Excel (Biểu mẫu 14, 15, danh sách cán bộ)</summary>
[ApiController]
[Route("api/reports")]
[Authorize] // Xuất báo cáo yêu cầu đăng nhập
public class ExportReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ExportReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>Xuất danh sách cán bộ ra file Excel</summary>
    [HttpGet("cadres")]
    [HttpGet("can-bo")]
    public async Task<IActionResult> ExportCadres()
    {
        var result = await _reportService.ExportCadresReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 14 — Tổng hợp danh sách xếp loại cán bộ</summary>
    [HttpGet("form-14")]
    [HttpGet("mau-14")]
    public async Task<IActionResult> ExportMau14([FromQuery] int periodId = 3)
    {
        var result = await _reportService.ExportForm14ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15 — Tổng hợp tỷ lệ xếp loại Hoàn thành xuất sắc (kiểm tra trần 20%)</summary>
    [HttpGet("form-15")]
    [HttpGet("mau-15")]
    public async Task<IActionResult> ExportMau15([FromQuery] int periodId = 3)
    {
        var result = await _reportService.ExportForm15ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }
}
