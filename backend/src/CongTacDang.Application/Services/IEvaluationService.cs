using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện dịch vụ xử lý quy trình đánh giá, xếp loại cán bộ theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public interface IEvaluationService
{
    /// <summary>Lấy danh sách tất cả các kỳ đánh giá</summary>
    Task<List<EvaluationPeriodDto>> GetPeriodsAsync();

    /// <summary>Lấy kỳ đánh giá đang kích hoạt</summary>
    Task<EvaluationPeriodDto?> GetActivePeriodAsync();

    /// <summary>Khởi tạo kỳ đánh giá mới</summary>
    Task<EvaluationPeriodDto> CreatePeriodAsync(CreatePeriodDto dto);

    /// <summary>Thiết lập kỳ đánh giá làm kỳ hiện hành</summary>
    Task<EvaluationPeriodDto> SetActivePeriodAsync(Guid periodId, uint? version);

    /// <summary>Cập nhật trạng thái tiến trình của kỳ đánh giá</summary>
    Task<EvaluationPeriodDto> UpdatePeriodStatusAsync(Guid periodId, PeriodStatus status, uint? version);

    /// <summary>Lấy hồ sơ đánh giá của cán bộ trong một kỳ đánh giá</summary>
    Task<EvaluationRecordDto?> GetUserEvaluationRecordAsync(Guid periodId, Guid memberId);

    /// <summary>Lấy chi tiết hồ sơ đánh giá theo Id trong phạm vi người dùng yêu cầu.</summary>
    Task<EvaluationRecordDto> GetRecordByIdAsync(Guid recordId, Guid requesterId);

    /// <summary>Lấy lịch sử chuyển trạng thái của hồ sơ đánh giá trong phạm vi người dùng yêu cầu.</summary>
    Task<List<EvaluationRecordHistoryDto>> GetRecordHistoryAsync(Guid recordId, Guid requesterId);

    /// <summary>Lấy hồ sơ đánh giá của một kỳ trong phạm vi người dùng yêu cầu.</summary>
    Task<List<EvaluationRecordDto>> GetRecordsByPeriodAsync(Guid periodId, Guid requesterId);

    /// <summary>Lấy danh sách hồ sơ đánh giá của một Chi bộ trong kỳ (hoặc Chi bộ của người dùng)</summary>
    Task<List<EvaluationRecordDto>> GetRecordsByBranchAsync(Guid periodId, Guid? branchId = null, Guid? currentUserId = null);

    /// <summary>Bước 1: Cán bộ đăng ký 3-7 nhiệm vụ trọng tâm quý (Mẫu 01 - Tổng trọng số = 70.0)</summary>
    Task<EvaluationRecordDto> RegisterTasksAsync(Guid memberId, RegisterTasksRequestDto dto);

    /// <summary>Bước 2: Cán bộ tự chấm điểm Tiêu chí chung (Mẫu 09) và Sản phẩm chuyên môn (Mẫu 02)</summary>
    Task<EvaluationRecordDto> SubmitSelfScoreAsync(Guid memberId, SubmitSelfScoreRequestDto dto);

    /// <summary>Bước 3: Chi bộ nhận xét và bỏ phiếu đánh giá (Mẫu 10, 11, 13)</summary>
    Task<EvaluationRecordDto> SubmitBranchReviewAsync(Guid reviewerId, SubmitBranchReviewRequestDto dto);

    /// <summary>Bước 3b: Chi bộ lưu toàn bộ Biên bản kiểm phiếu của Chi bộ trong cuộc họp (Mẫu 13)</summary>
    Task<List<EvaluationRecordDto>> SubmitBranchMeetingAsync(Guid reviewerId, SubmitBranchMeetingRequestDto dto);

    /// <summary>Bước 4: Tổ thẩm định thẩm tra và chấm điểm (Mẫu 03)</summary>
    Task<EvaluationRecordDto> SubmitAppraisalAsync(Guid appraiserId, SubmitAppraisalRequestDto dto);

    /// <summary>Bước 4b: Kiểm tra tỷ lệ trần 20% trong phạm vi thẩm quyền của người dùng (Mẫu 15).</summary>
    Task<List<BranchQuotaCheckDto>> CheckBranchQuotasAsync(Guid periodId, Guid requesterId);

    /// <summary>Bước 5: Ban Thường vụ phê duyệt và quyết định xếp loại chính thức (Mẫu 14, 16)</summary>
    Task<EvaluationRecordDto> ApproveFinalGradeAsync(Guid approverId, ApproveFinalGradeRequestDto dto);
}
