using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Sau công bố (task 20): công khai kết quả (T-86), kiến nghị (T-87), kế hoạch 30-60-90 ngày — Mẫu 17 (T-88), nhắc việc (T-89).
/// Controller chỉ kiểm tra "có quyền ở phạm vi nào đó"; service kiểm tra quyền trên từng hồ sơ bằng <see cref="IAuthorizationGuard"/>.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class PostPublishController : ControllerBase
{
    private readonly IPublishedResultService _results;
    private readonly IAppealService _appeals;
    private readonly IImprovementPlanService _plans;
    private readonly IImprovementPlanDocument _planDocument;
    private readonly INotificationService _notifications;

    public PostPublishController(
        IPublishedResultService results,
        IAppealService appeals,
        IImprovementPlanService plans,
        IImprovementPlanDocument planDocument,
        INotificationService notifications)
    {
        _results = results;
        _appeals = appeals;
        _plans = plans;
        _planDocument = planDocument;
        _notifications = notifications;
    }

    #region Công khai kết quả

    /// <summary>Kết quả đã công bố trong phạm vi <c>evaluation.results.view</c> (chỉ mức; điểm khi bộ tiêu chí cho phép).</summary>
    [HttpGet("results")]
    [RequirePermission(PermissionCodes.EvaluationResultsView)]
    public async Task<IActionResult> GetResults([FromQuery] Guid? periodId, [FromQuery] Guid? departmentId, [FromQuery] string? grade, CancellationToken ct)
    {
        var results = await _results.GetResultsAsync(periodId, departmentId, grade, ct);
        return Ok(ApiResponse<PublishedResultsDto>.Ok(results, "Lấy danh sách kết quả đánh giá thành công."));
    }

    #endregion

    #region Kiến nghị

    /// <summary>Khối kiến nghị của hồ sơ (chủ hồ sơ, người xử lý kiến nghị hoặc người xem hồ sơ trong phạm vi).</summary>
    [HttpGet("evaluations/records/{recordId:guid}/appeals")]
    public async Task<IActionResult> GetRecordAppeals(Guid recordId, CancellationToken ct)
    {
        var appeals = await _appeals.GetRecordAppealsAsync(recordId, ct);
        return Ok(ApiResponse<RecordAppealsDto>.Ok(appeals, "Lấy danh sách kiến nghị thành công."));
    }

    /// <summary>Chủ hồ sơ gửi kiến nghị về kết quả đã công bố.</summary>
    [HttpPost("evaluations/records/{recordId:guid}/appeals")]
    [RequirePermission(PermissionCodes.EvaluationAppealSubmit)]
    public async Task<IActionResult> SubmitAppeal(Guid recordId, [FromBody] SubmitAppealRequestDto dto, CancellationToken ct)
    {
        var appeal = await _appeals.SubmitAsync(recordId, dto, ct);
        return Ok(ApiResponse<AppealDto>.Ok(appeal, "Đã gửi kiến nghị. Kết quả của bạn được ghi chú \"đang xem xét\" tới khi có trả lời."));
    }

    /// <summary>Nhận xem xét kiến nghị.</summary>
    [HttpPost("appeals/{id:guid}/start-review")]
    [RequirePermission(PermissionCodes.EvaluationAppealResolve)]
    public async Task<IActionResult> StartReview(Guid id, [FromBody] VersionedRequestDto dto, CancellationToken ct)
    {
        var appeal = await _appeals.StartReviewAsync(id, dto, ct);
        return Ok(ApiResponse<AppealDto>.Ok(appeal, "Đã nhận xem xét kiến nghị."));
    }

    /// <summary>Trả lời kiến nghị (chấp nhận / không chấp nhận, bắt buộc căn cứ).</summary>
    [HttpPost("appeals/{id:guid}/resolve")]
    [RequirePermission(PermissionCodes.EvaluationAppealResolve)]
    public async Task<IActionResult> ResolveAppeal(Guid id, [FromBody] ResolveAppealRequestDto dto, CancellationToken ct)
    {
        var appeal = await _appeals.ResolveAsync(id, dto, ct);
        return Ok(ApiResponse<AppealDto>.Ok(appeal, "Đã trả lời kiến nghị."));
    }

    /// <summary>Mở lại hồ sơ theo kiến nghị đã chấp nhận (thao tác "Mở lại" hiện có, lý do dẫn chiếu kiến nghị).</summary>
    [HttpPost("appeals/{id:guid}/reopen")]
    [RequirePermission(PermissionCodes.EvaluationReopen)]
    public async Task<IActionResult> ReopenFromAppeal(Guid id, [FromBody] ReopenFromAppealRequestDto dto, CancellationToken ct)
    {
        var appeal = await _appeals.ReopenRecordAsync(id, dto, ct);
        return Ok(ApiResponse<AppealDto>.Ok(appeal, "Đã mở lại hồ sơ theo kiến nghị."));
    }

    #endregion

    #region Kế hoạch 30-60-90 ngày (Mẫu 17)

    /// <summary>Khối kế hoạch 30-60-90 ngày của hồ sơ.</summary>
    [HttpGet("evaluations/records/{recordId:guid}/improvement-plan")]
    public async Task<IActionResult> GetRecordPlan(Guid recordId, CancellationToken ct)
    {
        var plan = await _plans.GetRecordPlanAsync(recordId, ct);
        return Ok(ApiResponse<RecordImprovementPlanDto>.Ok(plan, "Lấy kế hoạch 30-60-90 ngày thành công."));
    }

    /// <summary>Lập kế hoạch cho hồ sơ đã công bố.</summary>
    [HttpPost("evaluations/records/{recordId:guid}/improvement-plan")]
    [RequirePermission(PermissionCodes.EvaluationImprovementManage)]
    public async Task<IActionResult> CreatePlan(Guid recordId, [FromBody] SaveImprovementPlanRequestDto dto, CancellationToken ct)
    {
        var plan = await _plans.CreateAsync(recordId, dto, ct);
        return Ok(ApiResponse<ImprovementPlanDto>.Ok(plan, "Đã lập kế hoạch 30-60-90 ngày (đang lập)."));
    }

    /// <summary>Sửa kế hoạch đang lập.</summary>
    [HttpPut("improvement-plans/{id:guid}")]
    [RequirePermission(PermissionCodes.EvaluationImprovementManage)]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] SaveImprovementPlanRequestDto dto, CancellationToken ct)
    {
        var plan = await _plans.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<ImprovementPlanDto>.Ok(plan, "Đã lưu kế hoạch."));
    }

    /// <summary>Thủ trưởng đơn vị duyệt kế hoạch.</summary>
    [HttpPost("improvement-plans/{id:guid}/approve")]
    [RequirePermission(PermissionCodes.EvaluationImprovementManage)]
    public async Task<IActionResult> ApprovePlan(Guid id, [FromBody] VersionedRequestDto dto, CancellationToken ct)
    {
        var plan = await _plans.ApproveAsync(id, dto, ct);
        return Ok(ApiResponse<ImprovementPlanDto>.Ok(plan, "Đã duyệt kế hoạch; chờ cá nhân xác nhận."));
    }

    /// <summary>Chủ hồ sơ xác nhận cam kết khắc phục.</summary>
    [HttpPost("improvement-plans/{id:guid}/acknowledge")]
    [RequirePermission(PermissionCodes.EvaluationSelf)]
    public async Task<IActionResult> AcknowledgePlan(Guid id, [FromBody] AcknowledgePlanRequestDto dto, CancellationToken ct)
    {
        var plan = await _plans.AcknowledgeAsync(id, dto, ct);
        return Ok(ApiResponse<ImprovementPlanDto>.Ok(plan, "Đã xác nhận kế hoạch."));
    }

    /// <summary>Ghi kết quả một mốc (mốc 90 ngày đóng kế hoạch).</summary>
    [HttpPost("improvement-plans/{id:guid}/milestone-result")]
    [RequirePermission(PermissionCodes.EvaluationImprovementManage)]
    public async Task<IActionResult> RecordMilestoneResult(Guid id, [FromBody] MilestoneResultRequestDto dto, CancellationToken ct)
    {
        var plan = await _plans.RecordResultAsync(id, dto, ct);
        return Ok(ApiResponse<ImprovementPlanDto>.Ok(plan, "Đã ghi kết quả mốc."));
    }

    /// <summary>Xuất Mẫu 17 (Word; <c>format=pdf</c> → PDF).</summary>
    [HttpGet("improvement-plans/{id:guid}/mau-17")]
    public async Task<IActionResult> ExportMau17(Guid id, [FromQuery] string? format, CancellationToken ct)
    {
        var reportFormat = format?.Trim().ToLowerInvariant() switch
        {
            null or "" or "docx" => ReportFormat.Original,
            "pdf" => ReportFormat.Pdf,
            _ => throw new ValidationException("Định dạng xuất không hợp lệ (docx | pdf).")
        };
        var (plan, record) = await _plans.GetForExportAsync(id, ct);
        var file = await _planDocument.RenderMau17Async(plan, record, reportFormat, ct);
        return File(file.FileBytes, file.ContentType, file.FileName);
    }

    #endregion

    #region Nhắc việc

    /// <summary>Số việc trên chuông ở header (tính khi tải).</summary>
    [HttpGet("notifications/summary")]
    public async Task<IActionResult> GetNotificationSummary(CancellationToken ct)
    {
        var summary = await _notifications.GetSummaryAsync(ct);
        return Ok(ApiResponse<NotificationSummaryDto>.Ok(summary, "Lấy nhắc việc thành công."));
    }

    #endregion
}
