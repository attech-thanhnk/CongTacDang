using System;
using System.Collections.Generic;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.DTOs;

#region Hồ sơ đánh giá cá nhân

/// <summary>Lịch sử chuyển trạng thái hồ sơ đánh giá.</summary>
public class EvaluationRecordHistoryDto
{
    public Guid Id { get; set; }
    public Guid RecordId { get; set; }
    public string? FromStatus { get; set; }
    public string? FromStatusName { get; set; }
    public string ToStatus { get; set; } = string.Empty;
    public string ToStatusName { get; set; } = string.Empty;
    /// <summary>Mã bước (B1_REGISTER…); null với thao tác không thuộc bước.</summary>
    public string? Step { get; set; }
    public string? StepName { get; set; }
    /// <summary>Complete / Return / Reopen / Create / EditSnapshot / Recalculate.</summary>
    public string Action { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public double? ScoreBefore { get; set; }
    public double? ScoreAfter { get; set; }
    public string? GradeBefore { get; set; }
    public string? GradeAfter { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Thông tin công việc chuyên môn đăng ký và đánh giá (Mẫu 01 & Mẫu 02)</summary>
public class EvaluationTaskDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin của công việc.</summary>
    public uint Version { get; set; }
    public Guid RecordId { get; set; }
    public int TaskOrder { get; set; }
    public string TaskName { get; set; } = string.Empty;
    public string TargetOutput { get; set; } = string.Empty;
    public double Weight { get; set; }
    public DateTime Deadline { get; set; }
    /// <summary>Mã trục kết quả (Mẫu 01).</summary>
    public string? AxisCode { get; set; }
    public double CriteriaA_Ratio { get; set; }
    public double CriteriaB_Ratio { get; set; }
    public double CriteriaC_Ratio { get; set; }
    public double CriteriaD_Ratio { get; set; }
    public double SelfScore { get; set; }
    public double? SupervisorScore { get; set; }
    public bool IsExceedStandard { get; set; }
    /// <summary>Id tệp minh chứng (phiên bản hiện hành của nhóm tệp — T-53).</summary>
    public Guid? AttachmentId { get; set; }
    /// <summary>Tên tệp minh chứng theo phiên bản hiện hành (T-53).</summary>
    public string? AttachmentFileName { get; set; }
    /// <summary>Tên gốc tệp minh chứng theo phiên bản hiện hành (T-53).</summary>
    public string? AttachmentOriginalName { get; set; }
}

/// <summary>Một bước trên thanh tiến trình của hồ sơ (tính ở backend — frontend không tự suy luật).</summary>
public class RecordStepProgressDto
{
    public string Step { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Bước có áp dụng cho hồ sơ (chế độ khác "Không áp dụng" trong hồ sơ luồng).</summary>
    public bool Enabled { get; set; }
    /// <summary>Chế độ của bước trong hồ sơ luồng: Internal | External | Off.</summary>
    public string Mode { get; set; } = nameof(StepMode.Internal);
    /// <summary>done | current | pending | skipped.</summary>
    public string State { get; set; } = string.Empty;
    public DateOnly? Deadline { get; set; }
    /// <summary>Đã quá thời hạn mà bước chưa xong.</summary>
    public bool Overdue { get; set; }
}

/// <summary>Thông tin Hồ sơ Đánh giá Cán bộ</summary>
public class EvaluationRecordDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin — bắt buộc gửi lại ở mọi hành động ghi.</summary>
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string PeriodStatus { get; set; } = string.Empty;
    /// <summary>Mẫu tự chấm theo bộ tiêu chí của kỳ (09A/09B); rỗng nếu kỳ chưa chọn bộ.</summary>
    public string SelfScoreForm { get; set; } = string.Empty;
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PartyCardNumber { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    /// <summary>Chi bộ ảnh chụp trên hồ sơ.</summary>
    public string? PartyCellName { get; set; }
    public Guid? PartyCellId { get; set; }
    /// <summary>Phòng ảnh chụp trên hồ sơ.</summary>
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>Mã khung tỷ trọng A-B-C-D (ảnh chụp).</summary>
    public string WeightFrameCode { get; set; } = string.Empty;
    public string ApprovalAuthority { get; set; } = string.Empty;
    /// <summary>Hồ sơ luồng (nhóm đối tượng) của hồ sơ.</summary>
    public string WorkflowProfileCode { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;

    /// <summary>Trạng thái = bước đang chờ (AwaitingRegistration … Published).</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    /// <summary>Mã bước đang chờ (null khi đã công bố).</summary>
    public string? CurrentStep { get; set; }
    /// <summary>Lý do trả lại gần nhất (còn hiệu lực đến khi chủ hồ sơ nộp lại).</summary>
    public string? ReturnReason { get; set; }
    /// <summary>Tiến trình 9 bước.</summary>
    public List<RecordStepProgressDto> Progress { get; set; } = new();

    // B1
    public string? TasksApprovedByName { get; set; }
    public DateTime? TasksApprovedAt { get; set; }
    public string? TasksApprovalComment { get; set; }

    // B2 — tự chấm
    /// <summary>Điểm từng tiêu chí con (khóa = mã tiêu chí con của bộ tiêu chí của kỳ).</summary>
    public Dictionary<string, GeneralItemScore> GeneralScores { get; set; } = new();
    public double GeneralCriteriaScore { get; set; }
    public double TasksScore { get; set; }
    /// <summary>Điểm theo trục (Mẫu 09B, khóa = mã trục); null nếu chưa chấm theo trục.</summary>
    public Dictionary<string, double>? AxisScores { get; set; }
    public double TotalSelfScore { get; set; }
    public string SelfProposedGrade { get; set; } = string.Empty;
    public DateTime? SelfScoredAt { get; set; }

    // B2 — Chi bộ xác nhận
    /// <summary>Ý kiến xác nhận của Chi bộ trên phiếu tự chấm.</summary>
    public string PartyCellComment { get; set; } = string.Empty;
    public string? CellConfirmedByName { get; set; }
    public DateTime? CellConfirmedAt { get; set; }

    // B3a
    public string CollectiveProposedGrade { get; set; } = string.Empty;
    public string? CollectiveComment { get; set; }
    public Guid? CollectiveMeetingId { get; set; }
    public string? CollectiveRecordedByName { get; set; }
    public DateTime? CollectiveRecordedAt { get; set; }

    // B3b
    public double? AppraisalScore { get; set; }
    public string AppraisalComment { get; set; } = string.Empty;
    /// <summary>Nội dung giải trình/căn cứ khi chênh lệch tự chấm – thẩm định (B-09).</summary>
    public string? AppraisalExplanation { get; set; }
    public string AppraisalProposedGrade { get; set; } = string.Empty;
    public string? AppraisedByName { get; set; }
    public DateTime? AppraisedAt { get; set; }

    // B3c
    public string? DirectorComment { get; set; }
    public string DirectorProposedGrade { get; set; } = string.Empty;
    public string? DirectorReviewedByName { get; set; }
    public DateTime? DirectorReviewedAt { get; set; }

    // B4
    public double FinalScore { get; set; }
    public string FinalGrade { get; set; } = string.Empty;
    public string? DecisionDocumentNumber { get; set; }
    public DateTime? DecisionDocumentDate { get; set; }
    public string? DecisionAuthorityName { get; set; }
    public Guid? DecisionMeetingId { get; set; }
    public string? DecisionRecordedByName { get; set; }
    public DateTime? DecisionRecordedAt { get; set; }

    // B5
    public string? PublishedByName { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Kết quả ghi nhận của các bước do cấp trên thực hiện.</summary>
    public List<ExternalResultDto> ExternalResults { get; set; } = new();

    public List<EvaluationTaskDto> Tasks { get; set; } = new();
}

/// <summary>Báo cáo kiểm soát tỷ lệ trần Hoàn thành xuất sắc theo từng Chi bộ (Mẫu 15)</summary>
public class BranchQuotaCheckDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public int TotalCadres { get; set; }
    public int GoodOrBetterCount { get; set; }
    public int MaxExcellentAllowed { get; set; }
    public int ProposedExcellentCount { get; set; }
    public double ActualExcellentPercentage { get; set; }
    public bool IsExceedingQuota { get; set; }
}

#endregion

#region Hành động theo bước (request)

/// <summary>Phần chung của mọi hành động ghi: phiên bản hồ sơ đã đọc (bắt buộc — thiếu → 400).</summary>
public class WorkflowRequestDto
{
    /// <summary>Phiên bản xmin của hồ sơ khi đọc.</summary>
    public uint? Version { get; set; }
}

/// <summary>Trả lại hồ sơ (bắt buộc lý do).</summary>
public class ReturnRecordRequestDto : WorkflowRequestDto
{
    public string? Reason { get; set; }
}

/// <summary>Hoàn thành bước kèm ý kiến tùy chọn (duyệt danh mục, Chi bộ xác nhận).</summary>
public class CommentRequestDto : WorkflowRequestDto
{
    public string? Comment { get; set; }
}

/// <summary>Dữ liệu một nhiệm vụ khi đăng ký (Mẫu 01).</summary>
public class TaskInputDto
{
    public string? TaskName { get; set; }
    public string? TargetOutput { get; set; }
    public double Weight { get; set; }
    public DateTime? Deadline { get; set; }
    public Guid? AttachmentId { get; set; }
    /// <summary>Mã trục kết quả (thuộc bộ tiêu chí của kỳ); không bắt buộc.</summary>
    public string? AxisCode { get; set; }
}

/// <summary>B1_REGISTER: nộp danh mục sản phẩm (Mẫu 01).</summary>
public class SubmitTasksRequestDto : WorkflowRequestDto
{
    public List<TaskInputDto> Tasks { get; set; } = new();
}

/// <summary>Kết quả tự chấm 4 tiêu chí A-B-C-D của một nhiệm vụ (Mẫu 02).</summary>
public class TaskScoreInputDto
{
    public Guid TaskId { get; set; }
    public double CriteriaA_Ratio { get; set; } = 1.0;
    public double CriteriaB_Ratio { get; set; } = 1.0;
    public double CriteriaC_Ratio { get; set; } = 1.0;
    public double CriteriaD_Ratio { get; set; } = 1.0;
    public bool IsExceedStandard { get; set; }
    public Guid? AttachmentId { get; set; }
}

/// <summary>B2_SELF_SCORE: nộp phiếu tự chấm theo bộ tiêu chí của kỳ (09A: theo nhiệm vụ; 09B: theo trục).</summary>
public class SubmitSelfScoreRequestDto : WorkflowRequestDto
{
    /// <summary>Điểm từng tiêu chí con của nhóm tiêu chí chung (khóa = mã tiêu chí con), có "K/AD" kèm lý do.</summary>
    public Dictionary<string, GeneralItemScore> GeneralScores { get; set; } = new();
    /// <summary>Mẫu 09A: tự chấm A-B-C-D từng nhiệm vụ.</summary>
    public List<TaskScoreInputDto> TaskScores { get; set; } = new();
    /// <summary>Mẫu 09B: điểm theo trục (khóa = mã trục).</summary>
    public Dictionary<string, double>? AxisScores { get; set; }
    /// <summary>Mức tự đề xuất; trống → gợi ý theo tổng điểm.</summary>
    public string? SelfProposedGrade { get; set; }
}

/// <summary>Kết quả kiểm phiếu tổng hợp của một hồ sơ (không có thông tin người bỏ phiếu).</summary>
public class VoteTallyDto
{
    public int VotesExcellent { get; set; }
    public int VotesGood { get; set; }
    public int VotesSatisfactory { get; set; }
    public int VotesUnsatisfactory { get; set; }
    public int InvalidVotes { get; set; }
    public string? Notes { get; set; }
}

/// <summary>B3A_COLLECTIVE: ghi nhận đề xuất của tập thể lãnh đạo.</summary>
public class CollectiveProposalRequestDto : WorkflowRequestDto
{
    public string? ProposedGrade { get; set; }
    public string? Comment { get; set; }
    /// <summary>Biên bản (Mẫu 12/13) ghi kết quả kiểm phiếu — bắt buộc khi có <see cref="Votes"/>.</summary>
    public Guid? MeetingId { get; set; }
    public VoteTallyDto? Votes { get; set; }
}

/// <summary>B3B_APPRAISAL: thẩm định.</summary>
public class AppraisalRequestDto : WorkflowRequestDto
{
    public double? AppraisalScore { get; set; }
    public string? Comment { get; set; }
    public string? ProposedGrade { get; set; }
    /// <summary>
    /// Nội dung giải trình/căn cứ — bắt buộc khi |tự chấm − thẩm định| ≥ ngưỡng của bộ tiêu chí (hoặc chênh lệch làm đổi mức).
    /// </summary>
    public string? Explanation { get; set; }
}

/// <summary>B3C_DIRECTOR: nhận xét, đề xuất của cấp trực tiếp sử dụng.</summary>
public class DirectorReviewRequestDto : WorkflowRequestDto
{
    public string? Comment { get; set; }
    public string? ProposedGrade { get; set; }
}

/// <summary>B4_DECISION: ghi nhận quyết định mức xếp loại.</summary>
public class DecisionRequestDto : WorkflowRequestDto
{
    public string? FinalGrade { get; set; }
    /// <summary>Điểm chính thức; trống → điểm thẩm định (hoặc điểm tự chấm).</summary>
    public double? FinalScore { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? AuthorityName { get; set; }
    public Guid? MeetingId { get; set; }
    public VoteTallyDto? Votes { get; set; }
}

/// <summary>Mở lại hồ sơ đã công bố.</summary>
public class ReopenRequestDto : WorkflowRequestDto
{
    public string? Reason { get; set; }
    /// <summary>Bước quay về (mã bước, không sớm hơn B2_SELF_SCORE).</summary>
    public string? TargetStep { get; set; }
}

#endregion

#region Hành động được phép, việc cần xử lý

/// <summary>Một hành động người hiện tại được làm trên hồ sơ (thiết kế mục 5).</summary>
public class RecordActionDto
{
    /// <summary>Mã hành động: SubmitTasks, ApproveTasks, ReturnTasks, SubmitSelfScore, ConfirmByCell, ReturnByCell,
    /// RecordCollectiveProposal, Appraise, ReturnByAppraiser, DirectorReview, RecordDecision, Publish, Reopen.</summary>
    public string Action { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool RequiresReason { get; set; }
    public bool ReasonOptional { get; set; }
    /// <summary>Đã quá thời hạn của bước (chỉ cảnh báo khi kỳ không bật chặn theo thời hạn).</summary>
    public bool Overdue { get; set; }
    /// <summary>Với Reopen: các bước được chọn để quay về.</summary>
    public List<string>? TargetSteps { get; set; }
}

/// <summary>Kết quả <c>GET records/{id}/actions</c>.</summary>
public class RecordActionsDto
{
    public Guid RecordId { get; set; }
    public uint Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<RecordActionDto> Actions { get; set; } = new();
}

/// <summary>Một hồ sơ trong danh sách việc cần xử lý.</summary>
public class WorkQueueItemDto
{
    public Guid RecordId { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    public string ApprovalAuthority { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    /// <summary>Chế độ của bước đang chờ: Internal | External.</summary>
    public string Mode { get; set; } = nameof(StepMode.Internal);
    public bool IsOwnRecord { get; set; }
    public string? ReturnReason { get; set; }
    public DateOnly? Deadline { get; set; }
    public bool Overdue { get; set; }
}

/// <summary>Nhóm việc theo bước.</summary>
public class WorkQueueGroupDto
{
    public string Step { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<WorkQueueItemDto> Items { get; set; } = new();
}

/// <summary>Kết quả <c>GET work-queue</c>.</summary>
public class WorkQueueDto
{
    public int Total { get; set; }
    public List<WorkQueueGroupDto> Groups { get; set; } = new();
}

#endregion

#region Kỳ đánh giá

/// <summary>Thông tin Kỳ đánh giá.</summary>
public class EvaluationPeriodDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin — gửi lại khi sửa/chuyển trạng thái kỳ.</summary>
    public uint Version { get; set; }
    public int Year { get; set; }
    public int Quarter { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>Draft / Open / Locked / Closed.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    public string? StatusReason { get; set; }
    /// <summary>Số người được đánh giá.</summary>
    public int TotalRecords { get; set; }
    /// <summary>Là kỳ hiện hành (kỳ Open/Locked mới nhất).</summary>
    public bool IsActive { get; set; }
    public PeriodSettings Settings { get; set; } = new();
    /// <summary>Bộ tiêu chí đã chọn.</summary>
    public Guid? CriteriaSetId { get; set; }
    /// <summary>Ảnh chụp bộ tiêu chí của kỳ (null nếu chưa chọn) — form tự chấm/thẩm định dựng từ đây.</summary>
    public CriteriaSnapshot? Criteria { get; set; }
}

/// <summary>Mẫu cấu hình dựng sẵn.</summary>
public class PeriodPresetDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    /// <summary>Mẫu tự chấm gợi ý (09A/09B) — chọn sẵn bộ tiêu chí đã xuất bản mới nhất có mẫu này.</summary>
    public string SuggestedForm { get; set; } = string.Empty;
    public PeriodSettings Settings { get; set; } = new();
}

/// <summary>Tạo kỳ từ mẫu cấu hình.</summary>
public class CreatePeriodDto
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public string? Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>Mã mẫu: <c>full</c> (mặc định) hoặc <c>q3-2026-transition</c>.</summary>
    public string? Preset { get; set; }
    /// <summary>Bộ tiêu chí (đã xuất bản); trống → bộ đã xuất bản mới nhất có mẫu tự chấm gợi ý của kiểu kỳ (nếu có).</summary>
    public Guid? CriteriaSetId { get; set; }
}

/// <summary>Sửa kỳ: Draft sửa mọi thứ; Open/Locked chỉ sửa thời hạn.</summary>
public class UpdatePeriodDto
{
    public uint? Version { get; set; }
    public string? Name { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public PeriodSettings? Settings { get; set; }
    /// <summary>Đổi bộ tiêu chí (chỉ khi kỳ dự thảo; chỉ bộ đã xuất bản); trống = giữ nguyên.</summary>
    public Guid? CriteriaSetId { get; set; }
}

/// <summary>Chuyển trạng thái kỳ.</summary>
public class PeriodTransitionDto
{
    public uint? Version { get; set; }
    /// <summary>Bắt buộc khi Khóa dữ liệu → Đang mở, và khi mở kỳ bắt buộc (<see cref="Force"/>) còn cảnh báo kẹt luồng.</summary>
    public string? Reason { get; set; }
    /// <summary>Mở kỳ dù kiểm tra kẹt luồng còn lỗi (bắt buộc lý do, ghi vào kỳ).</summary>
    public bool Force { get; set; }
}

/// <summary>Thêm người được đánh giá: chọn tay và/hoặc theo Phòng/Chi bộ.</summary>
public class AddParticipantsDto
{
    public List<Guid> MemberIds { get; set; } = new();
    public Guid? DepartmentId { get; set; }
    public Guid? PartyCellId { get; set; }
    /// <summary>Hồ sơ luồng gán cho người được thêm; bỏ trống → mặc định theo cấp quyết định của từng người.</summary>
    public string? WorkflowProfileCode { get; set; }
}

/// <summary>Kết quả thêm người được đánh giá.</summary>
public class AddParticipantsResultDto
{
    public int Added { get; set; }
    public List<string> Skipped { get; set; } = new();
}

/// <summary>Một người trong danh sách được đánh giá.</summary>
public class PeriodParticipantDto
{
    public Guid RecordId { get; set; }
    public uint Version { get; set; }
    public Guid MemberId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    /// <summary>Mã khung tỷ trọng A-B-C-D (ảnh chụp).</summary>
    public string WeightFrameCode { get; set; } = string.Empty;
    /// <summary>Tên khung theo bộ tiêu chí của kỳ; null nếu khung không có trong bộ.</summary>
    public string? WeightFrameName { get; set; }
    public string ApprovalAuthority { get; set; } = string.Empty;
    public string WorkflowProfileCode { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
}

/// <summary>Cán bộ có thể thêm vào kỳ.</summary>
public class ParticipantCandidateDto
{
    public Guid MemberId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    public bool AlreadyAdded { get; set; }
}

/// <summary>Sửa ảnh chụp trên hồ sơ (bắt buộc lý do).</summary>
public class UpdateSnapshotDto
{
    public uint? Version { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PartyCellId { get; set; }
    /// <summary>Mã khung tỷ trọng (phải có trong bộ tiêu chí của kỳ); trống = giữ nguyên.</summary>
    public string? WeightFrameCode { get; set; }
    public string? ApprovalAuthority { get; set; }
    public string? Reason { get; set; }
}

#endregion

#region Hồ sơ tập thể, biên bản

/// <summary>Dòng nội dung chi tiết của hồ sơ tập thể Mẫu 06 hoặc Mẫu 08.</summary>
public class CollectiveEvaluationItemDto
{
    public Guid Id { get; set; }
    public int ItemOrder { get; set; }
    public string Category { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string PlanOrDirection { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Hồ sơ đánh giá tập thể Mẫu 06, 07, 08.</summary>
public class CollectiveEvaluationRecordDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string Form { get; set; } = string.Empty;
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? HeadId { get; set; }
    public string? HeadName { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Causes { get; set; } = string.Empty;
    public string PreviousRemediation { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public string RemediationPlan { get; set; } = string.Empty;
    public double GeneralCriteriaScore { get; set; }
    public double TaskCriteriaScore { get; set; }
    public double TotalScore { get; set; }
    public string SelfProposedGrade { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<CollectiveEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>Dữ liệu tạo hồ sơ tập thể.</summary>
public class SaveCollectiveEvaluationRequestDto
{
    public uint? Version { get; set; }
    public Guid PeriodId { get; set; }
    public string Form { get; set; } = "M07";
    public Guid? PartyCellId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? HeadId { get; set; }
    public string SubjectName { get; set; } = string.Empty;
    public string Strengths { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Causes { get; set; } = string.Empty;
    public string PreviousRemediation { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public string RemediationPlan { get; set; } = string.Empty;
    public double GeneralCriteriaScore { get; set; }
    public double TaskCriteriaScore { get; set; }
    public string SelfProposedGrade { get; set; } = "HoanThanhTot";
    public List<CollectiveEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>Thông tin hội nghị và biên bản Mẫu 12, 13.</summary>
public class EvaluationMeetingDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>B3A_COLLECTIVE / B4_DECISION (null: không gắn bước cụ thể).</summary>
    public string? Stage { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public string MeetingType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int InvitedCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string AbsentReasons { get; set; } = string.Empty;
    public Guid? ChairId { get; set; }
    public string ChairName { get; set; } = string.Empty;
    public Guid? SecretaryId { get; set; }
    public string SecretaryName { get; set; } = string.Empty;
    public string MinutesContent { get; set; } = string.Empty;
    public string OutcomeContent { get; set; } = string.Empty;
    public string VoteCountingContent { get; set; } = string.Empty;
    public List<EvaluationMeetingVoteSummaryDto> VoteSummaries { get; set; } = new();
}

/// <summary>Tổng hợp phiếu theo từng hồ sơ, không lưu danh tính người bỏ phiếu.</summary>
public class EvaluationMeetingVoteSummaryDto
{
    public Guid? Id { get; set; }
    public Guid RecordId { get; set; }
    public string? FullName { get; set; }
    public int VotesExcellent { get; set; }
    public int VotesGood { get; set; }
    public int VotesSatisfactory { get; set; }
    public int VotesUnsatisfactory { get; set; }
    public int InvalidVotes { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>Dữ liệu tạo biên bản hội nghị và kiểm phiếu.</summary>
public class SaveEvaluationMeetingRequestDto
{
    public uint? Version { get; set; }
    public Guid PeriodId { get; set; }
    /// <summary>Hội nghị của Chi bộ (tùy chọn).</summary>
    public Guid? PartyCellId { get; set; }
    /// <summary>Hội nghị tập thể lãnh đạo cấp Phòng (tùy chọn). Không có cả hai = cấp Công ty.</summary>
    public Guid? DepartmentId { get; set; }
    /// <summary>B3A_COLLECTIVE hoặc B4_DECISION (tùy chọn).</summary>
    public string? Stage { get; set; }
    public string FormCode { get; set; } = "M12";
    public string MeetingType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int InvitedCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string AbsentReasons { get; set; } = string.Empty;
    public Guid? ChairId { get; set; }
    public string ChairName { get; set; } = string.Empty;
    public Guid? SecretaryId { get; set; }
    public string SecretaryName { get; set; } = string.Empty;
    public string MinutesContent { get; set; } = string.Empty;
    public string OutcomeContent { get; set; } = string.Empty;
    public string VoteCountingContent { get; set; } = string.Empty;
    public List<EvaluationMeetingVoteSummaryDto> VoteSummaries { get; set; } = new();
}

#endregion
