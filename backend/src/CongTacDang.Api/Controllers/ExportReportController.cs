using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Xuất báo cáo tổng hợp và biểu mẫu chuẩn Đảng (.docx / .xlsx, hoặc PDF với <c>?format=pdf</c>)</summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ExportReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IReportAccessService _reportAccess;

    public ExportReportController(IReportService reportService, IReportAccessService reportAccess)
    {
        _reportService = reportService;
        _reportAccess = reportAccess;
    }

    /// <summary>Lấy Id người dùng hiện tại từ JWT.</summary>
    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Không xác thực được danh tính người dùng hiện tại.");
        return userId;
    }

    /// <summary>
    /// Đọc tham số định dạng: <c>docx</c>/<c>xlsx</c> (hoặc bỏ trống) = định dạng gốc, <c>pdf</c> = PDF chuyển phía máy chủ.
    /// </summary>
    private static ReportFormat ParseFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
            return ReportFormat.Original;

        return format.Trim().ToLowerInvariant() switch
        {
            "docx" or "xlsx" or "original" => ReportFormat.Original,
            "pdf" => ReportFormat.Pdf,
            _ => throw new ArgumentException("Tham số format chỉ nhận docx, xlsx hoặc pdf.")
        };
    }

    // Excel 14/15/15A/15B/16 (T-30): periodId (mặc định kỳ đang hoạt động), branchId (Chi bộ).
    // Không truyền branchId: cấp cao xuất toàn Đảng bộ, Bí thư Chi bộ (branch_vote) xuất Chi bộ của mình; người khác 403.

    #region Báo cáo Excel Quản trị

    /// <summary>Xuất danh sách cán bộ ra file Excel</summary>
    [HttpGet("cadres")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportCadres()
    {
        var result = await _reportService.ExportCadresReportAsync();
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 14 — Tổng hợp danh sách hồ sơ cán bộ theo Chi bộ và đơn vị (toàn Đảng bộ hoặc theo Chi bộ)</summary>
    [HttpGet("form-14")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau14([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm14ReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15 — Thống kê cơ cấu tổ chức và sĩ số các Chi bộ (toàn Đảng bộ hoặc theo Chi bộ)</summary>
    [HttpGet("form-15")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau15([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm15ReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15A — kiểm soát tỷ lệ trần 20% cấp Đảng ủy Công ty</summary>
    [HttpGet("form-15a")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau15A([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm15AReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 15B — kiểm soát tỷ lệ trần 20% theo từng Chi bộ (toàn Đảng bộ hoặc theo Chi bộ)</summary>
    [HttpGet("form-15b")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau15B([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm15BReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 16 — tổng hợp kết quả xếp loại theo nhóm chức vụ (toàn Đảng bộ hoặc theo Chi bộ)</summary>
    [HttpGet("form-16")]
    [Authorize(Policy = AppPermissions.ReportsExport)]
    public async Task<IActionResult> ExportMau16([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm16ReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion

    #region Biểu mẫu Word (.docx) Chuẩn Hướng dẫn 03-HD/TVĐU

    /// <summary>Xuất Word Mẫu 01 — Phiếu giao/đăng ký sản phẩm chuyên môn hàng quý</summary>
    [HttpGet("docx/mau-01/{recordId}")]
    public async Task<IActionResult> ExportMau01Docx(Guid recordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportRecordAsync(GetCurrentUserId(), recordId);
        var result = await _reportService.ExportMau01DocxAsync(recordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm hàng quý</summary>
    [HttpGet("docx/mau-02/{recordId}")]
    public async Task<IActionResult> ExportMau02Docx(Guid recordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportRecordAsync(GetCurrentUserId(), recordId);
        var result = await _reportService.ExportMau02DocxAsync(recordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 10 — Phiếu thẩm định, nhận xét và ghi nhận giải trình</summary>
    [HttpGet("docx/mau-10/{recordId}")]
    public async Task<IActionResult> ExportMau10Docx(Guid recordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportRecordAsync(GetCurrentUserId(), recordId);
        var result = await _reportService.ExportMau10DocxAsync(recordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Xuất Word Mẫu 11 — Phiếu đánh giá, xếp loại cán bộ quý (Bỏ phiếu kín Chi bộ).
    /// Không truyền <paramref name="branchId"/>: cấp cao xuất toàn Đảng bộ, Chi bộ xuất Chi bộ của mình.
    /// </summary>
    [HttpGet("docx/mau-11")]
    public async Task<IActionResult> ExportMau11Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportMau11DocxAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Xuất Word Mẫu 13 — Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý.
    /// Không truyền <paramref name="branchId"/>: cấp cao xuất toàn Đảng bộ, Chi bộ xuất Chi bộ của mình.
    /// </summary>
    [HttpGet("docx/mau-13")]
    public async Task<IActionResult> ExportMau13Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportMau13DocxAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion
}
