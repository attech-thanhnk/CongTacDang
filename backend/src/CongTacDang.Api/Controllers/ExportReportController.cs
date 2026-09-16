using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Route("api/[controller]")]
public class ExportReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ExportReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Export list of cadres to Excel (.xlsx)
    /// </summary>
    [HttpGet("cadres")]
    [HttpGet("can-bo")]
    public async Task<IActionResult> ExportCadres()
    {
        var result = await _reportService.ExportCadresReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Ket xuat file Excel Mau 14: Tong hop danh sach xep loai can bo
    /// </summary>
    [HttpGet("form-14")]
    [HttpGet("mau-14")]
    public async Task<IActionResult> ExportMau14([FromQuery] int periodId = 3)
    {
        var result = await _reportService.ExportForm14ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Ket xuat file Excel Mau 15: Tong hop ty le % Xuat sac (Ap tran 20%)
    /// </summary>
    [HttpGet("form-15")]
    [HttpGet("mau-15")]
    public async Task<IActionResult> ExportMau15([FromQuery] int periodId = 3)
    {
        var result = await _reportService.ExportForm15ReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }
}
