using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Xuất báo cáo và biểu mẫu (.docx/.xlsx, hoặc PDF với <c>?format=pdf</c>).</summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ExportReportController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IReportAccessService _reportAccess;
    private readonly IPeriodService _periods;

    public ExportReportController(IReportService reportService, IReportAccessService reportAccess, IPeriodService periods)
    {
        _reportService = reportService;
        _reportAccess = reportAccess;
        _periods = periods;
    }

    /// <summary>Không chọn kỳ → kỳ đang hoạt động; không có → 404.</summary>
    private async Task<Guid> ResolvePeriodIdAsync(Guid? periodId)
    {
        if (periodId.HasValue && periodId.Value != Guid.Empty)
            return periodId.Value;
        var active = await _periods.GetActivePeriodAsync();
        return active?.Id ?? throw new NotFoundException(
            "Hiện không có kỳ đánh giá đang mở hoặc đang khóa dữ liệu. Hãy chọn kỳ đánh giá của báo cáo (tham số periodId).");
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Không xác thực được danh tính người dùng hiện tại.");
        return userId;
    }

    /// <summary><c>docx</c>/<c>xlsx</c>/trống = định dạng gốc, <c>pdf</c> = PDF chuyển phía máy chủ.</summary>
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

    // Không truyền branchId: phạm vi Toàn công ty → toàn Đảng bộ; đúng 1 tổ chức Đảng → tổ chức đó;
    // nhiều tổ chức → 400; không có phạm vi tổ chức Đảng → 403.

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

    /// <summary>Bảng tổng hợp xếp loại cán bộ của kỳ (Excel), lọc theo phạm vi report.export; branchId thu hẹp thêm.</summary>
    [HttpGet("internal/rating-summary")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportRatingSummary([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        Guid? branch = null;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
            branch = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportRatingSummaryAsync(
            await ResolvePeriodIdAsync(periodId), branch, _reportAccess.GetCadreExportScope(), ParseFormat(format));
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

    /// <summary>Mẫu 15A (M1–M16, cấp trên quyết định).</summary>
    [HttpGet("form-15a")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau15A([FromQuery] Guid? periodId = null, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportForm15AReportAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Mẫu 15B (M17–M26, cơ sở quyết định).</summary>
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
    public async Task<IActionResult> GetMau16Draft([FromQuery] Guid? periodId, [FromQuery] Guid? branchId = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var draft = await _reportService.GetForm16DraftAsync(await ResolvePeriodIdAsync(periodId), scope);
        return Ok(ApiResponse<Form16DraftDto>.Ok(draft, "Lấy bản nháp Mẫu 16 thành công."));
    }

    /// <summary>Lưu bản nháp phần nhập tay Mẫu 16 (kiểm tra phiên bản).</summary>
    [HttpPut("mau-16/draft")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> SaveMau16Draft([FromQuery] Guid? periodId, [FromQuery] Guid? branchId, [FromBody] SaveForm16DraftDto dto)
    {
        var userId = GetCurrentUserId();
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(userId, branchId);
        var draft = await _reportService.SaveForm16DraftAsync(await ResolvePeriodIdAsync(periodId), scope, userId, dto);
        return Ok(ApiResponse<Form16DraftDto>.Ok(draft, "Đã lưu bản nháp Mẫu 16."));
    }

    /// <summary>Xuất Word Mẫu 16.</summary>
    [HttpGet("docx/mau-16")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau16Docx([FromQuery] Guid? periodId, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportMau16DocxAsync(await ResolvePeriodIdAsync(periodId), scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Quyết định xếp loại kèm Phụ lục; còn hồ sơ chưa ghi quyết định → 400.</summary>
    [HttpGet("docx/decision")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportDecisionDocx([FromQuery] Guid? periodId, [FromQuery] Guid? branchId = null, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.ExportDecisionDocxAsync(await ResolvePeriodIdAsync(periodId), scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Kiểm tra lập được Quyết định xếp loại chưa (kèm lý do).</summary>
    [HttpGet("decision/readiness")]
    [RequirePermission(PermissionCodes.ReportExport)]
    public async Task<IActionResult> GetDecisionReadiness([FromQuery] Guid? periodId, [FromQuery] Guid? branchId = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(GetCurrentUserId(), branchId);
        var result = await _reportService.GetDecisionReadinessAsync(await ResolvePeriodIdAsync(periodId), scope);
        return Ok(ApiResponse<DecisionReadinessDto>.Ok(result, result.Ready ? "Đủ điều kiện lập Quyết định." : result.Message ?? string.Empty));
    }

    #endregion

    #region Mẫu 07, 08 (hồ sơ tập thể), Mẫu 12 (biên bản hội nghị)

    /// <summary>Xuất Word Mẫu 07 (hồ sơ tập thể).</summary>
    [HttpGet("docx/mau-07/{collectiveRecordId}")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> ExportMau07Docx(Guid collectiveRecordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportCollectiveAsync(collectiveRecordId);
        var result = await _reportService.ExportMau07DocxAsync(collectiveRecordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 08 (hồ sơ tập thể).</summary>
    [HttpGet("docx/mau-08/{collectiveRecordId}")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> ExportMau08Docx(Guid collectiveRecordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportCollectiveAsync(collectiveRecordId);
        var result = await _reportService.ExportMau08DocxAsync(collectiveRecordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Excel Mẫu 08 (cùng dữ liệu bản Word).</summary>
    [HttpGet("form-08/{collectiveRecordId}")]
    [RequireAnyPermission(PermissionCodes.EvaluationRead, PermissionCodes.CollectiveManage)]
    public async Task<IActionResult> ExportForm08Excel(Guid collectiveRecordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportCollectiveAsync(collectiveRecordId);
        var result = await _reportService.ExportForm08ExcelAsync(collectiveRecordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 12 — biên bản hội nghị (B3a hoặc B4).</summary>
    [HttpGet("docx/mau-12/{meetingId}")]
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.MeetingManage)]
    public async Task<IActionResult> ExportMau12Docx(Guid meetingId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportMeetingAsync(meetingId);
        var result = await _reportService.ExportMau12DocxAsync(meetingId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 13 — biên bản kiểm phiếu; chưa có kết quả kiểm phiếu → 400.</summary>
    [HttpGet("docx/mau-13/{meetingId}")]
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.MeetingManage)]
    public async Task<IActionResult> ExportMau13Docx(Guid meetingId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportMeetingAsync(meetingId);
        var result = await _reportService.ExportMau13DocxAsync(meetingId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    #endregion
    #region Biểu mẫu Word (.docx) Chuẩn Hướng dẫn 03-HD/TVĐU

    /// <summary>Xuất Word Mẫu 01.</summary>
    [HttpGet("docx/mau-01/{recordId}")]
    public async Task<IActionResult> ExportMau01Docx(Guid recordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportRecordAsync(GetCurrentUserId(), recordId);
        var result = await _reportService.ExportMau01DocxAsync(recordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 02.</summary>
    [HttpGet("docx/mau-02/{recordId}")]
    public async Task<IActionResult> ExportMau02Docx(Guid recordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportRecordAsync(GetCurrentUserId(), recordId);
        var result = await _reportService.ExportMau02DocxAsync(recordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 10.</summary>
    [HttpGet("docx/mau-10/{recordId}")]
    public async Task<IActionResult> ExportMau10Docx(Guid recordId, [FromQuery] string? format = null)
    {
        await _reportAccess.EnsureCanExportRecordAsync(GetCurrentUserId(), recordId);
        var result = await _reportService.ExportMau10DocxAsync(recordId, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }

    /// <summary>Xuất Word Mẫu 11; không truyền branchId: cấp cao xuất toàn Đảng bộ, Chi bộ xuất Chi bộ mình.</summary>
    [HttpGet("docx/mau-11")]
    [RequireAnyPermission(PermissionCodes.MeetingRead, PermissionCodes.ReportExport)]
    public async Task<IActionResult> ExportMau11Docx([FromQuery] Guid periodId, [FromQuery] Guid? branchId, [FromQuery] string? format = null)
    {
        var scope = await _reportAccess.ResolveBranchExportScopeAsync(
            GetCurrentUserId(), branchId, PermissionCodes.MeetingRead, PermissionCodes.ReportExport);
        var result = await _reportService.ExportMau11DocxAsync(periodId, scope, ParseFormat(format));
        return File(result.FileBytes, result.ContentType, result.FileName);
    }


    #endregion
}
