using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
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

    // Bảng tính Mẫu 14/15A/15B, Word Mẫu 16: periodId (mặc định kỳ đang hoạt động), branchId (tổ chức Đảng).
    // Không truyền branchId: phạm vi report.export Toàn công ty → toàn Đảng bộ; phạm vi đúng 1 tổ chức Đảng → tổ chức đó;
    // nhiều tổ chức → 400 yêu cầu chọn; không có phạm vi tổ chức Đảng/Toàn công ty → 403 (task 09).
    // Task 19 (T-85): bỏ "form-15" (thống kê/kiểm soát trần — không phải biểu mẫu HD03, chuyển thành báo cáo nội bộ) và
    // "form-16" Excel (Mẫu 16 HD03 là báo cáo Word: docx/mau-16).

    #region Báo cáo nội bộ (không mang số mẫu HD03)

    /// <summary>Danh sách cán bộ (nội bộ) — Excel theo phạm vi report.export.</summary>
    [HttpGet("cadres")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportCadres()
    {
        var result = await _reportService.ExportCadresReportAsync(_reportAccess.GetCadreExportScope());
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Báo cáo nội bộ — kiểm soát tỷ lệ Hoàn thành xuất sắc theo tổ chức Đảng (Excel).</summary>
    [HttpGet("internal/excellent-quota")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportExcellentQuota([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportExcellentQuotaReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion

    #region Hồ sơ nộp theo HD03 — bảng tính

    /// <summary>Mẫu 14 — Danh sách đánh giá và đề xuất xếp loại (Excel, mỗi cấp quyết định một trang tính).</summary>
    [HttpGet("form-14")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau14([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm14ReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Mẫu 15A — Tổng hợp kết quả đánh giá, xếp loại (đối tượng đề nghị BTVĐUTCT quyết định, M1–M16).</summary>
    [HttpGet("form-15a")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau15A([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm15AReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Mẫu 15B — Tổng hợp kết quả đánh giá, xếp loại (đối tượng thuộc diện Đảng ủy/Chi ủy cơ sở quyết định, M17–M26).</summary>
    [HttpGet("form-15b")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau15B([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm15BReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion

    #region Mẫu 16 — báo cáo Word của cấp ủy (số liệu tự động + phần nhập tay lưu nháp theo kỳ và tổ chức Đảng)

    /// <summary>Bản nháp phần nhập tay Mẫu 16 kèm số liệu tổng hợp tự động của kỳ và tổ chức Đảng.</summary>
    [HttpGet("mau-16/draft")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> GetMau16Draft([FromQuery] Guid periodId, [FromQuery] Guid? branchId = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var draft = await _reportService.GetForm16DraftAsync(periodId, scope);
        return Ok(ApiResponse<Form16DraftDto>.Ok(draft, "Lấy bản nháp Mẫu 16 thành công."));
    }

    /// <summary>Lưu bản nháp phần nhập tay Mẫu 16 (kiểm tra phiên bản).</summary>
    [HttpPut("mau-16/draft")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> SaveMau16Draft([FromQuery] Guid periodId, [FromQuery] Guid? branchId, [FromBody] SaveForm16DraftDto dto)
    {
        var userId = GetCurrentUserId();
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(userId, branchId);
        var draft = await _reportService.SaveForm16DraftAsync(periodId, scope, userId, dto);
        return Ok(ApiResponse<Form16DraftDto>.Ok(draft, "Đã lưu bản nháp Mẫu 16."));
    }

    /// <summary>Xuất Word Mẫu 16 — Báo cáo về kết quả đánh giá, xếp loại chất lượng cán bộ quý.</summary>
    [HttpGet("docx/mau-16")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau16Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportMau16DocxAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion

    #region Mẫu 07, 08 (hồ sơ tập thể), Mẫu 12 (biên bản hội nghị)

    /// <summary>Xuất Word Mẫu 07 — Báo cáo tự đánh giá, xếp loại chất lượng của tập thể (hồ sơ tập thể M07).</summary>
    [HttpGet("docx/mau-07/{collectiveRecordId}")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> ExportMau07Docx(Guid collectiveRecordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportCollectiveAsync(collectiveRecordId);
        var result = await _reportService.ExportMau07DocxAsync(collectiveRecordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 08 — Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị (hồ sơ tập thể M08).</summary>
    [HttpGet("docx/mau-08/{collectiveRecordId}")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> ExportMau08Docx(Guid collectiveRecordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportCollectiveAsync(collectiveRecordId);
        var result = await _reportService.ExportMau08DocxAsync(collectiveRecordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 12 — Biên bản hội nghị (hội nghị tập thể lãnh đạo B3a hoặc hội nghị cấp ủy B4).</summary>
    [HttpGet("docx/mau-12/{meetingId}")]
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.MeetingManage)]
    public async Task<IActionResult> ExportMau12Docx(Guid meetingId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportMeetingAsync(meetingId);
        var result = await _reportService.ExportMau12DocxAsync(meetingId, ParseFormat(format));
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
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau11Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(
            GetCurrentUserId(), branchId, PermissionCodes.MeetingRead, PermissionCodes.ReportExport);
        var result = await _reportService.ExportMau11DocxAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Xuất Word Mẫu 13 — Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý.
    /// Không truyền <paramref name="branchId"/>: cấp cao xuất toàn Đảng bộ, Chi bộ xuất Chi bộ của mình.
    /// </summary>
    [HttpGet("docx/mau-13")]
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau13Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(
            GetCurrentUserId(), branchId, PermissionCodes.MeetingRead, PermissionCodes.ReportExport);
        var result = await _reportService.ExportMau13DocxAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion
}
