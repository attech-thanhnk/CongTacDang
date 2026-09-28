using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Quản lý quy trình Đánh giá, xếp loại cán bộ 5 bước theo Hướng dẫn 03-HD/TVĐU
/// </summary>
[ApiController]
[Route("api/evaluations")]
[Authorize]
public class EvaluationController : ControllerBase
{
    private readonly IEvaluationService _evaluationService;

    public EvaluationController(IEvaluationService evaluationService)
    {
        _evaluationService = evaluationService;
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(sub) || !Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác thực được danh tính người dùng hiện tại.");
        }
        return userId;
    }

    #region Kỳ đánh giá (Evaluation Periods)

    /// <summary>Lấy danh sách tất cả các kỳ đánh giá</summary>
    [HttpGet("periods")]
    [Authorize(Policy = AppPermissions.EvaluationsRead)]
    public async Task<IActionResult> GetPeriods()
    {
        var periods = await _evaluationService.GetPeriodsAsync();
        return Ok(ApiResponse<List<EvaluationPeriodDto>>.Ok(periods, "Lấy danh sách kỳ đánh giá thành công."));
    }

    /// <summary>Lấy thông tin kỳ đánh giá đang hoạt động</summary>
    [HttpGet("periods/active")]
    [Authorize(Policy = AppPermissions.EvaluationsRead)]
    public async Task<IActionResult> GetActivePeriod()
    {
        var period = await _evaluationService.GetActivePeriodAsync();
        return Ok(ApiResponse<EvaluationPeriodDto?>.Ok(period, "Lấy kỳ đánh giá hiện hành thành công."));
    }

    /// <summary>Tạo mới kỳ đánh giá</summary>
    [HttpPost("periods")]
    [Authorize(Policy = AppPermissions.PolicyManagePeriods)]
    public async Task<IActionResult> CreatePeriod([FromBody] CreatePeriodDto dto)
    {
        var period = await _evaluationService.CreatePeriodAsync(dto);
        return Ok(ApiResponse<EvaluationPeriodDto>.Ok(period, "Khởi tạo kỳ đánh giá mới thành công."));
    }

    /// <summary>Kích hoạt kỳ đánh giá làm kỳ hiện hành</summary>
    [HttpPut("periods/{id}/activate")]
    [Authorize(Policy = AppPermissions.PolicyManagePeriods)]
    public async Task<IActionResult> SetActivePeriod(Guid id, [FromQuery] uint version)
    {
        var period = await _evaluationService.SetActivePeriodAsync(id, version);
        return Ok(ApiResponse<EvaluationPeriodDto>.Ok(period, "Kích hoạt kỳ đánh giá thành công."));
    }

    /// <summary>Cập nhật trạng thái tiến trình của kỳ đánh giá</summary>
    [HttpPut("periods/{id}/status")]
    [Authorize(Policy = AppPermissions.PolicyManagePeriods)]
    public async Task<IActionResult> UpdatePeriodStatus(Guid id, [FromQuery] PeriodStatus status, [FromQuery] uint version)
    {
        var period = await _evaluationService.UpdatePeriodStatusAsync(id, status, version);
        return Ok(ApiResponse<EvaluationPeriodDto>.Ok(period, "Cập nhật trạng thái kỳ đánh giá thành công."));
    }

    #endregion

    #region Hồ sơ đánh giá (Evaluation Records)

    /// <summary>Lấy hồ sơ đánh giá của cá nhân cán bộ đăng nhập trong kỳ</summary>
    [HttpGet("my-record")]
    [Authorize(Policy = AppPermissions.EvaluationsRead)]
    public async Task<IActionResult> GetMyRecord([FromQuery] Guid periodId)
    {
        var userId = GetCurrentUserId();
        var record = await _evaluationService.GetUserEvaluationRecordAsync(periodId, userId);
        return Ok(ApiResponse<EvaluationRecordDto?>.Ok(record, "Lấy hồ sơ cá nhân thành công."));
    }

    /// <summary>Lấy chi tiết hồ sơ đánh giá theo Id</summary>
    [HttpGet("records/{id}")]
    [Authorize(Policy = AppPermissions.EvaluationsRead)]
    public async Task<IActionResult> GetRecordById(Guid id)
    {
        var record = await _evaluationService.GetRecordByIdAsync(id, GetCurrentUserId());
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(record, "Lấy chi tiết hồ sơ đánh giá thành công."));
    }

    /// <summary>Lấy lịch sử chuyển trạng thái và người thực hiện của hồ sơ.</summary>
    [HttpGet("records/{id}/history")]
    [Authorize(Policy = AppPermissions.EvaluationsRead)]
    public async Task<IActionResult> GetRecordHistory(Guid id)
    {
        var history = await _evaluationService.GetRecordHistoryAsync(id, GetCurrentUserId());
        return Ok(ApiResponse<List<EvaluationRecordHistoryDto>>.Ok(history, "Lấy lịch sử hồ sơ đánh giá thành công."));
    }

    /// <summary>Lấy toàn bộ danh sách hồ sơ đánh giá của một kỳ (Chỉ dành cho Tổ Thẩm định & BTV)</summary>
    [HttpGet("records")]
    [Authorize(Policy = AppPermissions.PolicyEvaluationsAppraiseOrApprove)]
    public async Task<IActionResult> GetRecordsByPeriod([FromQuery] Guid periodId)
    {
        var records = await _evaluationService.GetRecordsByPeriodAsync(periodId, GetCurrentUserId());
        return Ok(ApiResponse<List<EvaluationRecordDto>>.Ok(records, "Lấy danh sách hồ sơ đánh giá thành công."));
    }

    /// <summary>Lấy danh sách hồ sơ đánh giá thuộc một Chi bộ (Chỉ dành cho Chi ủy, Tổ thẩm định, hoặc BTV)</summary>
    [HttpGet("branch-records")]
    [Authorize(Policy = AppPermissions.PolicyEvaluationsBranchView)]
    public async Task<IActionResult> GetRecordsByBranch([FromQuery] Guid periodId, [FromQuery] Guid? branchId)
    {
        var currentUserId = GetCurrentUserId();
        var records = await _evaluationService.GetRecordsByBranchAsync(periodId, branchId, currentUserId);
        return Ok(ApiResponse<List<EvaluationRecordDto>>.Ok(records, "Lấy danh sách hồ sơ Chi bộ thành công."));
    }

    #endregion

    #region Quy trình 5 bước

    /// <summary>Bước 1: Cán bộ đăng ký 3-7 nhiệm vụ chuyên môn đầu quý (Mẫu 01 - Tổng trọng số đúng 70.0đ)</summary>
    [HttpPost("tasks/register")]
    [Authorize(Policy = AppPermissions.EvaluationsRegister)]
    public async Task<IActionResult> RegisterTasks([FromBody] RegisterTasksRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _evaluationService.RegisterTasksAsync(userId, dto);
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(result, "Đăng ký danh mục nhiệm vụ trọng tâm thành công."));
    }

    /// <summary>Bước 2: Cán bộ tự chấm điểm Tiêu chí chung (Mẫu 09) và Sản phẩm chuyên môn (Mẫu 02)</summary>
    [HttpPost("self-score")]
    [Authorize(Policy = AppPermissions.EvaluationsSelfScore)]
    public async Task<IActionResult> SubmitSelfScore([FromBody] SubmitSelfScoreRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _evaluationService.SubmitSelfScoreAsync(userId, dto);
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(result, "Hoàn tất tự chấm điểm cá nhân thành công."));
    }

    /// <summary>Bước 3: Chi bộ nhận xét và ghi nhận kết quả bỏ phiếu kín (Mẫu 10, 11, 13)</summary>
    [HttpPost("branch-review")]
    [Authorize(Policy = AppPermissions.EvaluationsBranchVote)]
    public async Task<IActionResult> SubmitBranchReview([FromBody] SubmitBranchReviewRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _evaluationService.SubmitBranchReviewAsync(userId, dto);
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(result, "Ghi nhận đánh giá của Chi bộ thành công."));
    }

    /// <summary>Bước 3b: Chi bộ lưu toàn bộ Biên bản kiểm phiếu của Chi bộ trong cuộc họp (Mẫu 13)</summary>
    [HttpPost("branch-meeting-review")]
    [Authorize(Policy = AppPermissions.EvaluationsBranchVote)]
    public async Task<IActionResult> SubmitBranchMeeting([FromBody] SubmitBranchMeetingRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _evaluationService.SubmitBranchMeetingAsync(userId, dto);
        return Ok(ApiResponse<List<EvaluationRecordDto>>.Ok(result, "Ghi nhận kết quả kiểm phiếu Chi bộ thành công."));
    }

    /// <summary>Bước 4: Tổ Thẩm định đối soát điểm và đề xuất xếp loại (Mẫu 03)</summary>
    [HttpPost("appraisal")]
    [Authorize(Policy = AppPermissions.EvaluationsAppraise)]
    public async Task<IActionResult> SubmitAppraisal([FromBody] SubmitAppraisalRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _evaluationService.SubmitAppraisalAsync(userId, dto);
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(result, "Ghi nhận kết quả thẩm định thành công."));
    }

    /// <summary>Bước 4b: Kiểm tra tỷ lệ trần 20% Hoàn thành xuất sắc nhiệm vụ theo Chi bộ (Mẫu 15 - Chỉ dành cho Thẩm định & BTV)</summary>
    [HttpGet("branch-quotas")]
    [Authorize(Policy = AppPermissions.PolicyEvaluationsAppraiseOrApprove)]
    public async Task<IActionResult> CheckBranchQuotas([FromQuery] Guid periodId)
    {
        var quotas = await _evaluationService.CheckBranchQuotasAsync(periodId, GetCurrentUserId());
        return Ok(ApiResponse<List<BranchQuotaCheckDto>>.Ok(quotas, "Kiểm tra tỷ lệ trần 20% theo Chi bộ thành công."));
    }

    /// <summary>Bước 5: Ban Thường vụ chuẩn y mức xếp loại chính thức (Mẫu 14 & 16)</summary>
    [HttpPost("approve-final")]
    [Authorize(Policy = AppPermissions.EvaluationsApprove)]
    public async Task<IActionResult> ApproveFinalGrade([FromBody] ApproveFinalGradeRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var result = await _evaluationService.ApproveFinalGradeAsync(userId, dto);
        return Ok(ApiResponse<EvaluationRecordDto>.Ok(result, "Chuẩn y xếp loại chất lượng cán bộ thành công."));
    }

    #endregion
}
