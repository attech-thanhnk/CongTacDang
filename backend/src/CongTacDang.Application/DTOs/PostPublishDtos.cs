using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

#region Công khai kết quả (T-86)

/// <summary>
/// Một dòng kết quả đã công bố. Chỉ gồm thông tin công khai (HD03 II.2): họ tên, chức danh, đơn vị, mức chính thức; điểm chính thức
/// chỉ khi bộ tiêu chí của kỳ bật <c>publishScores</c>. Không có chi tiết hồ sơ, minh chứng, ý kiến.
/// </summary>
public class PublishedResultItemDto
{
    /// <summary>Id hồ sơ — chỉ có khi người xem có quyền xem hồ sơ (chủ hồ sơ hoặc <c>evaluation.read</c> trong phạm vi).</summary>
    public Guid? RecordId { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PositionTitle { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    /// <summary>Mã mức xếp loại chính thức.</summary>
    public string FinalGrade { get; set; } = string.Empty;
    public string FinalGradeName { get; set; } = string.Empty;
    /// <summary>Điểm chính thức — null khi bộ tiêu chí của kỳ không cho công khai điểm.</summary>
    public double? FinalScore { get; set; }
    public DateTime? PublishedAt { get; set; }
    /// <summary>Có kiến nghị đang xử lý → kết quả được ghi chú "đang xem xét" (HD03 PL II III.2).</summary>
    public bool UnderReview { get; set; }
    public bool IsOwn { get; set; }
}

/// <summary>Một lựa chọn lọc (kỳ, đơn vị).</summary>
public class ResultFilterOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Số người theo mức (sau khi lọc).</summary>
public class GradeCountDto
{
    public string Grade { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Kết quả <c>GET /api/results</c>.</summary>
public class PublishedResultsDto
{
    public List<PublishedResultItemDto> Items { get; set; } = new();
    public List<GradeCountDto> GradeCounts { get; set; } = new();
    /// <summary>Các kỳ có kết quả công bố trong phạm vi.</summary>
    public List<ResultFilterOptionDto> Periods { get; set; } = new();
    /// <summary>Các đơn vị có kết quả trong phạm vi (của kỳ đang lọc).</summary>
    public List<ResultFilterOptionDto> Departments { get; set; } = new();
    /// <summary>Có dòng nào hiển thị điểm (bộ tiêu chí cho công khai điểm).</summary>
    public bool ShowsScores { get; set; }
}

#endregion

#region Kiến nghị (T-87)

/// <summary>Một kiến nghị.</summary>
public class AppealDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid RecordId { get; set; }
    public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public string Content { get; set; } = string.Empty;
    /// <summary>Mã bước bị kiến nghị.</summary>
    public List<string> ConcernedSteps { get; set; } = new();
    public List<string> ConcernedStepNames { get; set; } = new();
    /// <summary>Submitted / UnderReview / Accepted / Rejected.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string? ReviewStartedByName { get; set; }
    public DateTime? ReviewStartedAt { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    /// <summary>Trả lời kèm căn cứ.</summary>
    public string? Response { get; set; }
    public DateTime? ReopenedAt { get; set; }
    /// <summary>Người hiện tại được nhận xem xét / trả lời kiến nghị này.</summary>
    public bool CanResolve { get; set; }
    /// <summary>Lý do không xử lý được (xung đột lợi ích…) — để hiển thị cho người có quyền xử lý.</summary>
    public string? ResolveBlockedReason { get; set; }
    /// <summary>Người hiện tại được mở lại hồ sơ theo kiến nghị đã chấp nhận (có quyền mở lại, hồ sơ đang công bố, chưa mở lại).</summary>
    public bool CanReopen { get; set; }
    /// <summary>Người hiện tại được gắn thêm tệp (chủ hồ sơ, kiến nghị đang xử lý).</summary>
    public bool CanAttach { get; set; }
}

/// <summary>Khối "Kiến nghị" trên trang hồ sơ.</summary>
public class RecordAppealsDto
{
    public Guid RecordId { get; set; }
    /// <summary>Kết quả đang được xem xét theo kiến nghị (có kiến nghị chưa trả lời).</summary>
    public bool UnderReview { get; set; }
    /// <summary>Người hiện tại được gửi kiến nghị mới.</summary>
    public bool CanSubmit { get; set; }
    /// <summary>Lý do chưa gửi được (khi là chủ hồ sơ).</summary>
    public string? SubmitBlockedReason { get; set; }
    /// <summary>Các bước chọn được làm "liên quan tới" khi gửi (bước đã có người thực hiện trên hồ sơ).</summary>
    public List<AppealStepOptionDto> StepOptions { get; set; } = new();
    public List<AppealDto> Appeals { get; set; } = new();
}

/// <summary>Bước chọn được khi gửi kiến nghị.</summary>
public class AppealStepOptionDto
{
    public string Step { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    /// <summary>Người/cơ quan đã thực hiện bước.</summary>
    public string? ActorName { get; set; }
}

/// <summary>Gửi kiến nghị.</summary>
public class SubmitAppealRequestDto
{
    /// <summary>Nội dung kiến nghị, đề nghị hiệu chỉnh.</summary>
    public string? Content { get; set; }
    /// <summary>Mã bước kiến nghị liên quan tới (tùy chọn).</summary>
    public List<string>? ConcernedSteps { get; set; }
}

/// <summary>Thao tác có phiên bản (xmin) của kiến nghị/kế hoạch.</summary>
public class VersionedRequestDto
{
    public uint? Version { get; set; }
}

/// <summary>Trả lời kiến nghị.</summary>
public class ResolveAppealRequestDto : VersionedRequestDto
{
    /// <summary>true = chấp nhận, false = không chấp nhận.</summary>
    public bool? Accepted { get; set; }
    /// <summary>Nội dung trả lời — bắt buộc nêu căn cứ.</summary>
    public string? Response { get; set; }
}

/// <summary>Mở lại hồ sơ theo kiến nghị đã chấp nhận (dùng thao tác "Mở lại" hiện có).</summary>
public class ReopenFromAppealRequestDto
{
    /// <summary>Phiên bản hồ sơ đánh giá (xmin) đã đọc.</summary>
    public uint? RecordVersion { get; set; }
    /// <summary>Bước hồ sơ quay về.</summary>
    public string? TargetStep { get; set; }
    /// <summary>Lý do mở lại (được ghép dẫn chiếu kiến nghị vào lịch sử hồ sơ).</summary>
    public string? Reason { get; set; }
}

#endregion

#region Kế hoạch 30-60-90 ngày — Mẫu 17 (T-88)

/// <summary>Một mốc của kế hoạch.</summary>
public class ImprovementMilestoneDto
{
    /// <summary>M30 / M60 / M90.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Days { get; set; }
    /// <summary>Hạn của mốc (ngày bắt đầu + số ngày) — null khi chưa có ngày bắt đầu.</summary>
    public DateOnly? DueDate { get; set; }
    public string? Limitation { get; set; }
    public string? Target { get; set; }
    public string? Measures { get; set; }
    public string? Coordination { get; set; }
    /// <summary>Achieved / NotAchieved / null.</summary>
    public string? Result { get; set; }
    public string ResultName { get; set; } = string.Empty;
    public string? ResultNote { get; set; }
    public string? ResultRecordedByName { get; set; }
    public DateTime? ResultRecordedAt { get; set; }
}

/// <summary>Kế hoạch 30-60-90 ngày.</summary>
public class ImprovementPlanDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid RecordId { get; set; }
    /// <summary>Draft / Approved / Acknowledged / Closed.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public string? SupporterName { get; set; }
    public string? SupporterTitle { get; set; }
    public List<ImprovementMilestoneDto> Milestones { get; set; } = new();
    public string? PreparedByName { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgementComment { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Khối "Kế hoạch 30-60-90 ngày" trên trang hồ sơ.</summary>
public class RecordImprovementPlanDto
{
    public Guid RecordId { get; set; }
    /// <summary>Hồ sơ đã công bố.</summary>
    public bool IsPublished { get; set; }
    /// <summary>Mức chính thức thuộc nhóm bắt buộc lập kế hoạch (theo bộ tiêu chí của kỳ).</summary>
    public bool Required { get; set; }
    public string FinalGrade { get; set; } = string.Empty;
    public string FinalGradeName { get; set; } = string.Empty;
    /// <summary>Tên các mức bắt buộc (theo bộ tiêu chí của kỳ).</summary>
    public List<string> RequiredGradeNames { get; set; } = new();
    public ImprovementPlanDto? Plan { get; set; }
    /// <summary>Người hiện tại lập/duyệt/ghi kết quả được (<c>evaluation.improvement.manage</c> trong phạm vi, không phải hồ sơ của mình).</summary>
    public bool CanManage { get; set; }
    /// <summary>Người hiện tại là chủ hồ sơ và kế hoạch đang chờ xác nhận.</summary>
    public bool CanAcknowledge { get; set; }
}

/// <summary>Nội dung mốc khi lập/sửa kế hoạch.</summary>
public class ImprovementMilestoneInputDto
{
    public string? Code { get; set; }
    public string? Limitation { get; set; }
    public string? Target { get; set; }
    public string? Measures { get; set; }
    public string? Coordination { get; set; }
}

/// <summary>Lập / sửa kế hoạch (chỉ khi đang lập).</summary>
public class SaveImprovementPlanRequestDto : VersionedRequestDto
{
    public string? SupporterName { get; set; }
    public string? SupporterTitle { get; set; }
    public DateOnly? StartDate { get; set; }
    public List<ImprovementMilestoneInputDto>? Milestones { get; set; }
}

/// <summary>Cá nhân xác nhận cam kết khắc phục.</summary>
public class AcknowledgePlanRequestDto : VersionedRequestDto
{
    public string? Comment { get; set; }
}

/// <summary>Ghi kết quả một mốc.</summary>
public class MilestoneResultRequestDto : VersionedRequestDto
{
    /// <summary>M30 / M60 / M90.</summary>
    public string? Milestone { get; set; }
    /// <summary>Achieved / NotAchieved.</summary>
    public string? Result { get; set; }
    public string? Note { get; set; }
}

#endregion

#region Nhắc việc (T-89)

/// <summary>Một mục nhắc việc.</summary>
public class NotificationItemDto
{
    /// <summary>overdue / dueSoon / appeal / improvementPlan / acknowledge / pending.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    /// <summary>Đường dẫn trong ứng dụng.</summary>
    public string Link { get; set; } = string.Empty;
    public DateOnly? Deadline { get; set; }
}

/// <summary>Kết quả <c>GET /api/notifications/summary</c> — tính khi tải, không đẩy thời gian thực.</summary>
public class NotificationSummaryDto
{
    /// <summary>Tổng số việc (số hiện trên chuông).</summary>
    public int Total { get; set; }
    /// <summary>Hồ sơ đang chờ người dùng ở các bước luồng đánh giá.</summary>
    public int PendingSteps { get; set; }
    /// <summary>Bước sắp tới hạn (còn ≤ <see cref="DueSoonDays"/> ngày).</summary>
    public int DueSoon { get; set; }
    /// <summary>Bước đã quá hạn.</summary>
    public int Overdue { get; set; }
    /// <summary>Kiến nghị chờ người dùng xử lý.</summary>
    public int Appeals { get; set; }
    /// <summary>Kế hoạch 30-60-90 ngày cần lập/duyệt.</summary>
    public int ImprovementPlans { get; set; }
    /// <summary>Kế hoạch chờ chính người dùng xác nhận.</summary>
    public int PlansToAcknowledge { get; set; }
    /// <summary>Ngưỡng "sắp tới hạn" (ngày).</summary>
    public int DueSoonDays { get; set; }
    public List<NotificationItemDto> Items { get; set; } = new();
}

#endregion
