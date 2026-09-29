using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Hồ sơ đánh giá cá nhân và luồng 9 bước theo cấu hình kỳ (task 12). Controller chỉ kiểm tra "có quyền ở phạm vi nào đó";
/// service kiểm tra quyền trên từng hồ sơ, trạng thái kỳ, máy trạng thái và phiên bản (xmin).
/// </summary>
[ApiController]
[Route("api/evaluations")]
[Authorize]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationService _evaluations;
    private readonly IEvaluationWorkflowService _workflow;

    public EvaluationController(IEvaluationService evaluations, IEvaluationWorkflowService workflow)
    {
        _evaluations = evaluations;
        _workflow = workflow;
    }

    #region Đọc

    /// <summary>Hồ sơ của chính người dùng trong kỳ (null nếu không có trong danh sách được đánh giá).</summary>
    [HttpGet("my-record")]
    public async Task<IActionResult> GetMyRecord([FromQuery] Guid periodId, CancellationToken ct)
    {
        var record = await _evaluations.GetMyRecordAsync(periodId, ct);
        return Ok(ApiResponse<EvaluationRecordDto?>.Ok(record, "Lấy hồ sơ cá nhân thành công."));
    }

    /// <summary>Chi tiết hồ sơ (service kiểm tra evaluation.read; chủ hồ sơ luôn xem được).</summary>
    [HttpGet("records/{id:guid}")]
    public async Task<IActionResult> GetRecordById(Guid id, CancellationToken ct)
    {
        var record = await _evaluations.GetRecordByIdAsync(id, ct);
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(record, "Lấy chi tiết hồ sơ đánh giá thành công."));
    }

    /// <summary>Lịch sử của hồ sơ.</summary>
    [HttpGet("records/{id:guid}/history")]
    public async Task<IActionResult> GetRecordHistory(Guid id, CancellationToken ct)
    {
        var history = await _evaluations.GetRecordHistoryAsync(id, ct);
        return Ok(ApiResponse<List<EvaluationRecordHistoryDto>>.Ok(history, "Lấy lịch sử hồ sơ đánh giá thành công."));
    }

    /// <summary>Hành động người hiện tại được làm trên hồ sơ (frontend hiển thị nút theo danh sách này).</summary>
    [HttpGet("records/{id:guid}/actions")]
    public async Task<IActionResult> GetActions(Guid id, CancellationToken ct)
    {
        var actions = await _workflow.GetActionsAsync(id, ct);
        return Ok(ApiResponse<RecordActionsDto>.Ok(actions, "Lấy danh sách hành động thành công."));
    }

    /// <summary>Hồ sơ đang chờ người hiện tại xử lý, nhóm theo bước.</summary>
    [HttpGet("work-queue")]
    public async Task<IActionResult> GetWorkQueue([FromQuery] Guid? periodId, CancellationToken ct)
    {
        var queue = await _workflow.GetWorkQueueAsync(periodId, ct);
        return Ok(ApiResponse<WorkQueueDto>.Ok(queue, "Lấy danh sách việc cần xử lý thành công."));
    }

    /// <summary>Hồ sơ trong kỳ theo phạm vi evaluation.read.</summary>
    [HttpGet("records")]
    [RequirePermission(PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> GetRecordsByPeriod([FromQuery] Guid periodId, CancellationToken ct)
    {
        var records = await _evaluations.GetRecordsByPeriodAsync(periodId, ct);
        return Ok(ApiResponse<List<EvaluationRecordDto>>.Ok(records, "Lấy danh sách hồ sơ đánh giá thành công."));
    }

    /// <summary>Hồ sơ của một Chi bộ trong kỳ, lọc theo phạm vi evaluation.read.</summary>
    [HttpGet("branch-records")]
    [RequirePermission(PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> GetRecordsByBranch([FromQuery] Guid periodId, [FromQuery] Guid? branchId, CancellationToken ct)
    {
        var records = await _evaluations.GetRecordsByBranchAsync(periodId, branchId, ct);
        return Ok(ApiResponse<List<EvaluationRecordDto>>.Ok(records, "Lấy danh sách hồ sơ Chi bộ thành công."));
    }

    /// <summary>Kiểm tra trần tỷ lệ Hoàn thành xuất sắc theo Chi bộ (hiển thị trên trang Đánh giá; không phải biểu mẫu HD03).</summary>
    [HttpGet("branch-quotas")]
    [RequirePermission(PermissionCodes.EvaluationRead)]
    public async Task<IActionResult> CheckBranchQuotas([FromQuery] Guid periodId, CancellationToken ct)
    {
        var quotas = await _evaluations.CheckBranchQuotasAsync(periodId, ct);
        return Ok(ApiResponse<List<BranchQuotaCheckDto>>.Ok(quotas, "Kiểm tra trần tỷ lệ theo Chi bộ thành công."));
    }

    #endregion

    #region Hành động theo bước

    // Quyền thực hiện các bước không phải của chủ hồ sơ (duyệt, xác nhận, ghi nhận, thẩm định, nhận xét, quyết định, công bố)
    // cấu hình theo hồ sơ luồng của từng hồ sơ (task 15) nên controller chỉ yêu cầu đăng nhập; service kiểm tra chế độ bước,
    // xung đột lợi ích và IAuthorizationGuard.Ensure với đúng mã quyền của bước trên hồ sơ (403 nêu tên quyền).

    /// <summary>B1_REGISTER — chủ hồ sơ nộp danh mục sản phẩm (Mẫu 01).</summary>
    [HttpPost("records/{id:guid}/tasks/submit")]
    [RequirePermission(PermissionCodes.EvaluationSelf)]
    public Task<IActionResult> SubmitTasks(Guid id, [FromBody] SubmitTasksRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.SubmitTasksAsync(id, dto, ct), "Đã nộp danh mục sản phẩm.");

    /// <summary>B1_APPROVE — duyệt danh mục.</summary>
    [HttpPost("records/{id:guid}/tasks/approve")]
    public Task<IActionResult> ApproveTasks(Guid id, [FromBody] CommentRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.ApproveTasksAsync(id, dto, ct), "Đã duyệt danh mục sản phẩm.");

    /// <summary>B1_APPROVE — trả lại danh mục (bắt buộc lý do).</summary>
    [HttpPost("records/{id:guid}/tasks/return")]
    public Task<IActionResult> ReturnTasks(Guid id, [FromBody] ReturnRecordRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.ReturnTasksAsync(id, dto, ct), "Đã trả lại danh mục sản phẩm.");

    /// <summary>B2_SELF_SCORE — chủ hồ sơ nộp phiếu tự chấm.</summary>
    [HttpPost("records/{id:guid}/self-score/submit")]
    [RequirePermission(PermissionCodes.EvaluationSelf)]
    public Task<IActionResult> SubmitSelfScore(Guid id, [FromBody] SubmitSelfScoreRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.SubmitSelfScoreAsync(id, dto, ct), "Đã nộp phiếu tự chấm.");

    /// <summary>B2_CELL_CONFIRM — Chi bộ xác nhận phiếu tự chấm.</summary>
    [HttpPost("records/{id:guid}/cell/confirm")]
    public Task<IActionResult> ConfirmByCell(Guid id, [FromBody] CommentRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.ConfirmByCellAsync(id, dto, ct), "Chi bộ đã xác nhận phiếu tự chấm.");

    /// <summary>B2_CELL_CONFIRM — Chi bộ trả lại phiếu tự chấm (bắt buộc lý do).</summary>
    [HttpPost("records/{id:guid}/cell/return")]
    public Task<IActionResult> ReturnByCell(Guid id, [FromBody] ReturnRecordRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.ReturnByCellAsync(id, dto, ct), "Chi bộ đã trả lại phiếu tự chấm.");

    /// <summary>B3A_COLLECTIVE — ghi nhận đề xuất của tập thể lãnh đạo (kết quả kiểm phiếu tổng hợp).</summary>
    [HttpPost("records/{id:guid}/collective")]
    public Task<IActionResult> RecordCollectiveProposal(Guid id, [FromBody] CollectiveProposalRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.RecordCollectiveProposalAsync(id, dto, ct), "Đã ghi nhận đề xuất của tập thể lãnh đạo.");

    /// <summary>B3B_APPRAISAL — thẩm định.</summary>
    [HttpPost("records/{id:guid}/appraisal")]
    public Task<IActionResult> Appraise(Guid id, [FromBody] AppraisalRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.AppraiseAsync(id, dto, ct), "Đã ghi nhận kết quả thẩm định.");

    /// <summary>B3B_APPRAISAL — trả lại để chủ hồ sơ sửa (bắt buộc lý do).</summary>
    [HttpPost("records/{id:guid}/appraisal/return")]
    public Task<IActionResult> ReturnByAppraiser(Guid id, [FromBody] ReturnRecordRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.ReturnByAppraiserAsync(id, dto, ct), "Đã trả lại hồ sơ.");

    /// <summary>B3C_DIRECTOR — nhận xét, đề xuất của cấp trực tiếp sử dụng.</summary>
    [HttpPost("records/{id:guid}/director-review")]
    public Task<IActionResult> DirectorReview(Guid id, [FromBody] DirectorReviewRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.DirectorReviewAsync(id, dto, ct), "Đã ghi nhận nhận xét của cấp trực tiếp sử dụng.");

    /// <summary>B4_DECISION — ghi nhận quyết định (bước nội bộ; bước do cấp trên quyết định dùng <c>external/B4_DECISION</c>).</summary>
    [HttpPost("records/{id:guid}/decision")]
    public Task<IActionResult> RecordDecision(Guid id, [FromBody] DecisionRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.RecordDecisionAsync(id, dto, ct), "Đã ghi nhận quyết định mức xếp loại.");

    /// <summary>B5_PUBLISH — công bố, khóa hồ sơ.</summary>
    [HttpPost("records/{id:guid}/publish")]
    public Task<IActionResult> Publish(Guid id, [FromBody] WorkflowRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.PublishAsync(id, dto, ct), "Đã công bố kết quả.");

    /// <summary>Mở lại hồ sơ đã công bố (bắt buộc lý do, chọn bước quay về).</summary>
    [HttpPost("records/{id:guid}/reopen")]
    [RequirePermission(PermissionCodes.EvaluationReopen)]
    public Task<IActionResult> Reopen(Guid id, [FromBody] ReopenRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.ReopenAsync(id, dto, ct), "Đã mở lại hồ sơ.");

    /// <summary>
    /// Ghi nhận kết quả của bước do cấp trên thực hiện (<paramref name="step"/> = mã bước, ví dụ <c>B4_DECISION</c>):
    /// cơ quan, số/ngày văn bản, nhận xét, mức, điểm, tệp đính kèm.
    /// </summary>
    [HttpPost("records/{id:guid}/external/{step}")]
    [RequirePermission(PermissionCodes.EvaluationExternalRecord)]
    public Task<IActionResult> RecordExternalResult(Guid id, string step, [FromBody] ExternalResultRequestDto dto, CancellationToken ct) =>
        Run(() => _workflow.RecordExternalResultAsync(id, step, dto, ct), "Đã ghi nhận kết quả của cấp trên.");

    private async Task<IActionResult> Run(Func<Task<EvaluationRecordDto>> action, string message)
    {
        var record = await action();
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(record, message));
    }

    #endregion
}
