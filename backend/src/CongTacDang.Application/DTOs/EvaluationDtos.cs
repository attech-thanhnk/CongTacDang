using System;
using System.Collections.Generic;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.DTOs;

#region Hồ sơ đánh giá cá nhân

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
    public string? AxisCode { get; set; }
    public string? ProductCode { get; set; }
    /// <summary><c>TrongTam</c> / <c>ThuongXuyen</c> / <c>DotXuat</c>; null với dữ liệu cũ.</summary>
    public string? Kind { get; set; }
    /// <summary><c>ChuTri</c> / <c>PhoiHop</c>; null với dữ liệu cũ.</summary>
    public string? Role { get; set; }
    public string? ExceedCriteria { get; set; }
    public string? EvidenceSource { get; set; }
    public string? Confirmer { get; set; }
    /// <summary><c>Excluded</c> / <c>CarriedOver</c>; null = còn hiệu lực.</summary>
    public string? ChangeStatus { get; set; }
    /// <summary>Tỷ lệ 0..1; null = chủ hồ sơ chưa nhập (không điền sẵn 100%).</summary>
    public double? CriteriaA_Ratio { get; set; }
    public double? CriteriaB_Ratio { get; set; }
    public double? CriteriaC_Ratio { get; set; }
    public double? CriteriaD_Ratio { get; set; }
    /// <summary>Chủ hồ sơ đã nhập tỷ lệ A-B-C-D (nháp hoặc nộp).</summary>
    public bool SelfScoreEntered { get; set; }
    public double SelfScore { get; set; }
    public double? SupervisorScore { get; set; }
    public bool IsExceedStandard { get; set; }
    /// <summary>Mức 1/2/3; null = chưa thẩm định.</summary>
    public int? AppraisalLevel { get; set; }
    public string? AppraisalComment { get; set; }
    /// <summary>Tệp minh chứng: phiên bản hiện hành của nhóm tệp.</summary>
    public Guid? AttachmentId { get; set; }
    public string? AttachmentFileName { get; set; }
    public string? AttachmentOriginalName { get; set; }
}

/// <summary>Một bước trên thanh tiến trình; tính ở backend, frontend không tự suy luật.</summary>
public class RecordStepProgressDto
{
    public string Step { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Chế độ của bước khác Off.</summary>
    public bool Enabled { get; set; }
    /// <summary>Internal | External | Off.</summary>
    public string Mode { get; set; } = nameof(StepMode.Internal);
    /// <summary>done | current | pending | skipped.</summary>
    public string State { get; set; } = string.Empty;
    public DateOnly? Deadline { get; set; }
    public bool Overdue { get; set; }
    /// <summary>Bước cấp trên: "Cấp trên thực hiện".</summary>
    public string Actor { get; set; } = string.Empty;
    /// <summary>Chỉ có với bước đã xong và có dữ liệu thời điểm.</summary>
    public DateTime? DoneAt { get; set; }
    /// <summary>Chỉ có với bước đã xong.</summary>
    public string? DoneByName { get; set; }
}

public class EvaluationRecordDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin — bắt buộc gửi lại ở mọi hành động ghi.</summary>
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string PeriodStatus { get; set; } = string.Empty;
    /// <summary>09A/09B; rỗng nếu kỳ chưa chọn bộ tiêu chí.</summary>
    public string SelfScoreForm { get; set; } = string.Empty;
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PartyCardNumber { get; set; }
    public string PositionTitle { get; set; } = string.Empty;
    /// <summary>Ảnh chụp trên hồ sơ.</summary>
    public string? PartyCellName { get; set; }
    public Guid? PartyCellId { get; set; }
    /// <summary>Ảnh chụp trên hồ sơ.</summary>
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>Ảnh chụp trên hồ sơ.</summary>
    public string WeightFrameCode { get; set; } = string.Empty;
    /// <summary>Khung gốc trong bộ tiêu chí của kỳ; null nếu không có.</summary>
    public RecordWeightFrameDto? BaseWeightFrame { get; set; }
    /// <summary>Khung dùng để chấm: khung gốc đã áp tỷ trọng điều chỉnh (nếu có).</summary>
    public RecordWeightFrameDto? WeightFrame { get; set; }
    /// <summary>Cấp giao nhiệm vụ đã điều chỉnh tỷ trọng khi duyệt danh mục.</summary>
    public bool WeightFrameAdjusted { get; set; }
    /// <summary>Điểm phần trăm; null = không cho điều chỉnh.</summary>
    public double? FrameAdjustLimit { get; set; }
    public string ApprovalAuthority { get; set; } = string.Empty;
    public string WorkflowProfileCode { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;

    /// <summary>Trạng thái = bước đang chờ (AwaitingRegistration … Published).</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;
    /// <summary>null khi đã công bố.</summary>
    public string? CurrentStep { get; set; }
    /// <summary>Còn hiệu lực đến khi chủ hồ sơ nộp lại.</summary>
    public string? ReturnReason { get; set; }
    /// <summary>UTC; null = không mở riêng.</summary>
    public DateTime? UnlockedUntil { get; set; }
    /// <summary>Mở riêng còn hạn trong khi kỳ khóa dữ liệu.</summary>
    public bool UnlockActive { get; set; }
    public string? UnlockedByName { get; set; }
    public string? UnlockReason { get; set; }
    public List<RecordStepProgressDto> Progress { get; set; } = new();

    // B1
    public string? TasksApprovedByName { get; set; }
    public DateTime? TasksApprovedAt { get; set; }
    public string? TasksApprovalComment { get; set; }

    // B2 — tự chấm
    /// <summary>Khóa = mã tiêu chí con của bộ tiêu chí của kỳ.</summary>
    public Dictionary<string, GeneralItemScore> GeneralScores { get; set; } = new();
    public double GeneralCriteriaScore { get; set; }
    public double TasksScore { get; set; }
    /// <summary>Mẫu 09B, khóa = mã trục; null nếu chưa chấm theo trục.</summary>
    public Dictionary<string, double>? AxisScores { get; set; }
    public double TotalSelfScore { get; set; }
    public string SelfProposedGrade { get; set; } = string.Empty;
    /// <summary>Lý do tự đề xuất mức khác mức theo điểm.</summary>
    public string? SelfGradeExplanation { get; set; }
    public DateTime? SelfScoredAt { get; set; }
    /// <summary>Mẫu 09C, khóa = mã mục của bộ tiêu chí.</summary>
    public Dictionary<string, string> SelfAssessment { get; set; } = new();
    /// <summary>Mẫu 09C/9D, khóa = mã trục.</summary>
    public Dictionary<string, AxisReport> AxisReports { get; set; } = new();
    /// <summary>Mẫu 09B, khóa = mã trục.</summary>
    public Dictionary<string, AxisNote> AxisNotes { get; set; } = new();
    /// <summary>Khóa = mã nhóm; in ở dòng nhóm của phiếu tự chấm.</summary>
    public Dictionary<string, string> GeneralGroupNotes { get; set; } = new();

    // B2 — Chi bộ xác nhận
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
    /// <summary>= điểm nhóm tiêu chí chung + điểm nhóm kết quả thực hiện nhiệm vụ.</summary>
    public double? AppraisalScore { get; set; }
    /// <summary>null khi chỉ có điểm tổng.</summary>
    public double? AppraisalGeneralScore { get; set; }
    /// <summary>null khi chỉ có điểm tổng.</summary>
    public double? AppraisalTasksScore { get; set; }
    public string AppraisalComment { get; set; } = string.Empty;
    /// <summary>Giải trình khi chênh lệch tự chấm – thẩm định.</summary>
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
    /// <summary>Căn cứ khi mức quyết định khác mức theo điểm.</summary>
    public string? DecisionExplanation { get; set; }
    public string? DecisionDocumentNumber { get; set; }
    public DateTime? DecisionDocumentDate { get; set; }
    public string? DecisionAuthorityName { get; set; }
    /// <summary>Cột 13 Mẫu 14.</summary>
    public string? CadreWorkProposal { get; set; }
    public Guid? DecisionMeetingId { get; set; }
    public string? DecisionRecordedByName { get; set; }
    public DateTime? DecisionRecordedAt { get; set; }

    // B5
    public string? PublishedByName { get; set; }
    public DateTime? PublishedAt { get; set; }

    /// <summary>Kết quả các bước do cấp trên thực hiện.</summary>
    public List<ExternalResultDto> ExternalResults { get; set; } = new();

    public List<EvaluationTaskDto> Tasks { get; set; } = new();

    /// <summary>Cảnh báo về mức hiệu lực: điều kiện xếp loại theo mức thẩm định từng nhiệm vụ, mức khác mức theo điểm; không chặn.</summary>
    public List<string> GradeWarnings { get; set; } = new();

    /// <summary>Các bước có mức đề xuất khác mức theo điểm của bộ tiêu chí.</summary>
    public List<GradeDeviationDto> GradeDeviations { get; set; } = new();

    /// <summary>Tỷ lệ nhiệm vụ vượt chuẩn tối thiểu để đạt Xuất sắc (0..1); null = không xét.</summary>
    public double? ExcellentMinExceedRatio { get; set; }
}

/// <summary>Trần tỷ lệ Xuất sắc theo nhóm chức danh trong từng cấp quyết định; chỉ cảnh báo, không chặn.</summary>
public class ExcellentQuotaCheckDto
{
    public Guid PeriodId { get; set; }

    /// <summary>0,20..0,25 theo bộ tiêu chí của kỳ.</summary>
    public double Ratio { get; set; }

    /// <summary>Phạm vi tính (gồm tổ chức con); null = mọi hồ sơ được xem.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Tính trên toàn bộ hồ sơ của kỳ; false = chỉ hồ sơ trong phạm vi được xem/được chọn.</summary>
    public bool IsFullScope { get; set; }

    public int ExceedingGroupCount { get; set; }

    /// <summary>Cấp trên trước, theo thứ tự mã chức danh.</summary>
    public List<ExcellentQuotaGroupDto> Groups { get; set; } = new();
}

/// <summary>Một nhóm chức danh trong một cấp quyết định.</summary>
public class ExcellentQuotaGroupDto
{
    /// <summary><c>CoSo</c> / <c>CapTren</c>.</summary>
    public string ApprovalAuthority { get; set; } = string.Empty;

    public string AuthorityName { get; set; } = string.Empty;

    /// <summary>null = cán bộ chưa có mã.</summary>
    public string? StatCode { get; set; }

    public string GroupName { get; set; } = string.Empty;

    /// <summary>Kể cả người đứng đầu.</summary>
    public int TotalCadres { get; set; }

    /// <summary>Không tính vào trần.</summary>
    public int HeadCount { get; set; }

    /// <summary>Không kể người đứng đầu.</summary>
    public int GoodCount { get; set; }

    /// <summary>Không kể người đứng đầu.</summary>
    public int ExcellentCount { get; set; }

    public int MaxExcellentAllowed { get; set; }

    /// <summary>% Xuất sắc trên số Hoàn thành tốt; null khi chưa có ai Hoàn thành tốt.</summary>
    public double? ExcellentPercentOfGood { get; set; }

    public bool IsExceedingQuota { get; set; }

    public int ExcessCount { get; set; }
}

#endregion

#region Hành động theo bước (request)

/// <summary>Phần chung của mọi hành động ghi; thiếu Version → 400.</summary>
public class WorkflowRequestDto
{
    /// <summary>Phiên bản xmin của hồ sơ khi đọc.</summary>
    public uint? Version { get; set; }
}

/// <summary>Trả lại hồ sơ (bắt buộc lý do).</summary>
public class ReturnRecordRequestDto : WorkflowRequestDto
{
    public string? Reason { get; set; }
    /// <summary>UTC; bắt buộc khi kỳ khóa dữ liệu và hồ sơ về bước trước thẩm định,
    /// trừ khi hồ sơ đang được mở riêng còn hạn (giữ hạn cũ).</summary>
    public DateTime? UnlockUntil { get; set; }
}

/// <summary>Gia hạn mở riêng hồ sơ khi kỳ khóa dữ liệu (bắt buộc lý do và hạn mới).</summary>
public class UnlockRecordRequestDto : WorkflowRequestDto
{
    public string? Reason { get; set; }
    /// <summary>UTC.</summary>
    public DateTime? UnlockUntil { get; set; }
}

/// <summary>Hoàn thành bước kèm ý kiến tùy chọn (duyệt danh mục, Chi bộ xác nhận).</summary>
public class CommentRequestDto : WorkflowRequestDto
{
    public string? Comment { get; set; }
}

/// <summary>B1_APPROVE: ý kiến + điều chỉnh tỷ lệ A-B-C-D tùy chọn.</summary>
public class ApproveTasksRequestDto : CommentRequestDto
{
    /// <summary>null = giữ tỷ lệ đang áp dụng.</summary>
    public FrameAdjustmentDto? FrameAdjustment { get; set; }
}

/// <summary>Tỷ lệ A-B-C-D theo phần trăm (tổng 100).</summary>
public class FrameAdjustmentDto
{
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }
    public double D { get; set; }
}

/// <summary>Tỷ trọng A-B-C-D của khung (0..1).</summary>
public class RecordWeightFrameDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double A { get; set; }
    public double B { get; set; }
    public double C { get; set; }
    public double D { get; set; }

    public static RecordWeightFrameDto? From(CongTacDang.Domain.Evaluation.WeightFrame? frame) => frame == null
        ? null
        : new RecordWeightFrameDto { Code = frame.Code, Name = frame.Name, A = frame.A, B = frame.B, C = frame.C, D = frame.D };
}

public class TaskInputDto
{
    public string? TaskName { get; set; }
    /// <summary>Chuẩn đạt; bắt buộc.</summary>
    public string? TargetOutput { get; set; }
    public double Weight { get; set; }
    public DateTime? Deadline { get; set; }
    public Guid? AttachmentId { get; set; }
    /// <summary>Thuộc bộ tiêu chí của kỳ; bắt buộc khi bộ có trục.</summary>
    public string? AxisCode { get; set; }
    /// <summary>Không bắt buộc.</summary>
    public string? ProductCode { get; set; }
    /// <summary><c>TrongTam</c> / <c>ThuongXuyen</c>; bắt buộc.</summary>
    public string? Kind { get; set; }
    /// <summary><c>ChuTri</c> / <c>PhoiHop</c>; bắt buộc.</summary>
    public string? Role { get; set; }
    /// <summary>Bắt buộc.</summary>
    public string? ExceedCriteria { get; set; }
    /// <summary>Bắt buộc.</summary>
    public string? EvidenceSource { get; set; }
    /// <summary>Bắt buộc.</summary>
    public string? Confirmer { get; set; }
}

/// <summary>B1_REGISTER.</summary>
public class SubmitTasksRequestDto : WorkflowRequestDto
{
    public List<TaskInputDto> Tasks { get; set; } = new();
}

/// <summary>Tỷ lệ 0..1, không có mặc định: nộp phải đủ bốn tỷ lệ; lưu nháp bỏ qua nhiệm vụ còn thiếu.</summary>
public class TaskScoreInputDto
{
    public Guid TaskId { get; set; }
    public double? CriteriaA_Ratio { get; set; }
    public double? CriteriaB_Ratio { get; set; }
    public double? CriteriaC_Ratio { get; set; }
    public double? CriteriaD_Ratio { get; set; }
    public bool IsExceedStandard { get; set; }
    public Guid? AttachmentId { get; set; }
}

/// <summary>B2_SELF_SCORE: 09A chấm theo nhiệm vụ, 09B chấm theo trục.</summary>
public class SubmitSelfScoreRequestDto : WorkflowRequestDto
{
    /// <summary>Khóa = mã tiêu chí con; "K/AD" phải kèm lý do.</summary>
    public Dictionary<string, GeneralItemScore> GeneralScores { get; set; } = new();
    /// <summary>Mẫu 09A.</summary>
    public List<TaskScoreInputDto> TaskScores { get; set; } = new();
    /// <summary>Mẫu 09B, khóa = mã trục.</summary>
    public Dictionary<string, double>? AxisScores { get; set; }
    /// <summary>Trống → gợi ý theo tổng điểm.</summary>
    public string? SelfProposedGrade { get; set; }
    /// <summary>Bắt buộc khi mức tự đề xuất khác mức theo điểm và bộ tiêu chí yêu cầu giải trình.</summary>
    public string? SelfGradeExplanation { get; set; }
    /// <summary>Mẫu 09C; null = giữ nội dung đã lưu.</summary>
    public Dictionary<string, string?>? SelfAssessment { get; set; }
    /// <summary>Mẫu 09C/9D, khóa = mã trục; null = giữ nội dung đã lưu.</summary>
    public Dictionary<string, AxisReport?>? AxisReports { get; set; }
    /// <summary>Mẫu 09B, khóa = mã trục; null = giữ nội dung đã lưu.</summary>
    public Dictionary<string, AxisNote?>? AxisNotes { get; set; }
    /// <summary>Khóa = mã nhóm; null = giữ nội dung đã lưu.</summary>
    public Dictionary<string, string?>? GeneralGroupNotes { get; set; }
}

/// <summary>Kiểm phiếu tổng hợp của một hồ sơ, không có thông tin người bỏ phiếu.</summary>
public class VoteTallyDto
{
    public int VotesExcellent { get; set; }
    public int VotesGood { get; set; }
    public int VotesSatisfactory { get; set; }
    public int VotesUnsatisfactory { get; set; }
    /// <summary>Phiếu "Chưa đánh giá, xếp loại".</summary>
    public int VotesNotRated { get; set; }
    public int InvalidVotes { get; set; }
    public string? Notes { get; set; }
}

/// <summary>B3A_COLLECTIVE.</summary>
public class CollectiveProposalRequestDto : WorkflowRequestDto
{
    public string? ProposedGrade { get; set; }
    public string? Comment { get; set; }
    /// <summary>Biên bản Mẫu 12/13; bắt buộc khi có <see cref="Votes"/>.</summary>
    public Guid? MeetingId { get; set; }
    public VoteTallyDto? Votes { get; set; }
}

/// <summary>B3B_APPRAISAL.</summary>
public class AppraisalRequestDto : WorkflowRequestDto
{
    /// <summary>Gửi cùng <see cref="TasksScore"/>; tổng do máy chủ tính.</summary>
    public double? GeneralScore { get; set; }
    /// <summary>Gửi cùng <see cref="GeneralScore"/>.</summary>
    public double? TasksScore { get; set; }
    /// <summary>Chỉ dùng khi không gửi điểm theo nhóm; có điểm theo nhóm thì bị bỏ qua.</summary>
    public double? AppraisalScore { get; set; }
    /// <summary>Bắt buộc đủ mọi nhiệm vụ khi bộ tiêu chí chấm theo nhiệm vụ (09A).</summary>
    public List<AppraisalTaskLevelDto>? TaskLevels { get; set; }
    public string? Comment { get; set; }
    public string? ProposedGrade { get; set; }
    /// <summary>Bắt buộc khi |tự chấm − thẩm định| ≥ ngưỡng của bộ tiêu chí hoặc chênh lệch làm đổi mức.</summary>
    public string? Explanation { get; set; }
}

public class AppraisalTaskLevelDto
{
    public Guid TaskId { get; set; }
    /// <summary>1 / 2 / 3.</summary>
    public int? Level { get; set; }
    public string? Comment { get; set; }
}

/// <summary>B3C_DIRECTOR.</summary>
public class DirectorReviewRequestDto : WorkflowRequestDto
{
    public string? Comment { get; set; }
    public string? ProposedGrade { get; set; }
}

/// <summary>B4_DECISION.</summary>
public class DecisionRequestDto : WorkflowRequestDto
{
    public string? FinalGrade { get; set; }
    /// <summary>Trống → điểm thẩm định (hoặc điểm tự chấm).</summary>
    public double? FinalScore { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? AuthorityName { get; set; }
    /// <summary>Tùy chọn; cột 13 Mẫu 14.</summary>
    public string? CadreWorkProposal { get; set; }
    public Guid? MeetingId { get; set; }
    public VoteTallyDto? Votes { get; set; }
    /// <summary>Căn cứ khi mức quyết định khác mức theo điểm.</summary>
    public string? Explanation { get; set; }
}

/// <summary>Mở lại hồ sơ đã công bố.</summary>
public class ReopenRequestDto : WorkflowRequestDto
{
    public string? Reason { get; set; }
    /// <summary>Mã bước, không sớm hơn B2_SELF_SCORE.</summary>
    public string? TargetStep { get; set; }
    /// <summary>UTC; bắt buộc khi kỳ khóa dữ liệu/đã đóng và bước quay về thuộc <see cref="RecordActionDto.UnlockDeadlineSteps"/>.</summary>
    public DateTime? UnlockUntil { get; set; }
}

#endregion

#region Hành động được phép, việc cần xử lý

/// <summary>Một hành động người hiện tại được làm trên hồ sơ.</summary>
public class RecordActionDto
{
    /// <summary>Mã hành động: SubmitTasks, ApproveTasks, ReturnTasks, SubmitSelfScore, ConfirmByCell, ReturnByCell,
    /// RecordCollectiveProposal, Appraise, ReturnByAppraiser, DirectorReview, RecordDecision, Publish, Reopen.</summary>
    public string Action { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool RequiresReason { get; set; }
    public bool ReasonOptional { get; set; }
    /// <summary>Chỉ cảnh báo khi kỳ không bật chặn theo thời hạn.</summary>
    public bool Overdue { get; set; }
    /// <summary>Reopen: các bước được chọn để quay về.</summary>
    public List<string>? TargetSteps { get; set; }
    /// <summary>Phải nhập hạn mở riêng hồ sơ; với Reopen xem <see cref="UnlockDeadlineSteps"/>.</summary>
    public bool RequiresUnlockDeadline { get; set; }
    /// <summary>Reopen: các bước quay về cần nhập hạn mở riêng.</summary>
    public List<string>? UnlockDeadlineSteps { get; set; }
    /// <summary>Hạn mở riêng hiện tại; để trống hạn mới thì giữ hạn này (trừ gia hạn).</summary>
    public DateTime? CurrentUnlockedUntil { get; set; }
    /// <summary>Số ngày gợi ý / tối đa của hạn mở riêng.</summary>
    public int? DefaultUnlockDays { get; set; }
    public int? MaxUnlockDays { get; set; }
}

/// <summary>Kết quả <c>GET records/{id}/actions</c>.</summary>
public class RecordActionsDto
{
    public Guid RecordId { get; set; }
    public uint Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<RecordActionDto> Actions { get; set; } = new();
}

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
    public int TotalRecords { get; set; }
    /// <summary>Kỳ Open/Locked mới nhất.</summary>
    public bool IsActive { get; set; }
    public PeriodSettings Settings { get; set; } = new();
    public Guid? CriteriaSetId { get; set; }
    /// <summary>Ảnh chụp bộ tiêu chí của kỳ; null nếu chưa chọn.</summary>
    public CriteriaSnapshot? Criteria { get; set; }
}

/// <summary>Tạo kỳ dự thảo; hồ sơ luồng lấy theo cấu hình mặc định.</summary>
public class CreatePeriodDto
{
    public int Year { get; set; }
    public int Quarter { get; set; }
    public string? Name { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    /// <summary>Bộ đã xuất bản, bắt buộc. Mẫu tự chấm của bộ quyết định có bước đăng ký, duyệt sản phẩm
    /// (09A có, 09B không).</summary>
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
    /// <summary>Điền thời hạn các bước theo quy tắc (cần gửi kèm <see cref="Settings"/>).</summary>
    public bool ApplyDeadlineRules { get; set; }
    /// <summary>Chỉ khi kỳ dự thảo và bộ đã xuất bản; trống = giữ nguyên. Đổi bộ thì bước đăng ký, duyệt sản phẩm
    /// của mọi hồ sơ luồng đặt lại theo bộ mới.</summary>
    public Guid? CriteriaSetId { get; set; }
}

public class PeriodTransitionDto
{
    public uint? Version { get; set; }
    /// <summary>Bắt buộc khi Locked → Open, và khi mở kỳ bằng <see cref="Force"/>.</summary>
    public string? Reason { get; set; }
    /// <summary>Mở kỳ dù kiểm tra kẹt luồng còn lỗi.</summary>
    public bool Force { get; set; }
}

/// <summary>Thêm người được đánh giá: chọn tay và/hoặc theo Phòng/Chi bộ.</summary>
public class AddParticipantsDto
{
    public List<Guid> MemberIds { get; set; } = new();
    public Guid? DepartmentId { get; set; }
    public Guid? PartyCellId { get; set; }
    /// <summary>Trống → mặc định theo cấp quyết định của từng người.</summary>
    public string? WorkflowProfileCode { get; set; }
}

public class AddParticipantsResultDto
{
    public int Added { get; set; }
    public List<string> Skipped { get; set; } = new();
}

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
    /// <summary>Ảnh chụp trên hồ sơ.</summary>
    public string WeightFrameCode { get; set; } = string.Empty;
    /// <summary>null nếu khung không có trong bộ tiêu chí của kỳ.</summary>
    public string? WeightFrameName { get; set; }
    /// <summary>Hồ sơ đã qua bước tự chấm: muốn đổi phải trả hồ sơ về bước tự chấm.</summary>
    public bool WeightFrameLocked { get; set; }
    public string ApprovalAuthority { get; set; } = string.Empty;
    public string WorkflowProfileCode { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusDisplayName { get; set; } = string.Empty;

    /// <summary>null khi đã công bố.</summary>
    public string? CurrentStep { get; set; }

    public string? CurrentStepName { get; set; }

    /// <summary>Theo hồ sơ luồng; null khi bước không đặt hạn hoặc đã công bố.</summary>
    public DateOnly? CurrentStepDeadline { get; set; }

    /// <summary>≥ 1, theo giờ Việt Nam; null khi chưa quá hạn hoặc không có hạn.</summary>
    public int? DaysOverdue { get; set; }

    /// <summary>0 = hạn là hôm nay; null khi đã quá hạn hoặc không có hạn.</summary>
    public int? DaysLeft { get; set; }
}

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
    /// <summary>Phải có trong bộ tiêu chí của kỳ; trống = giữ nguyên.</summary>
    public string? WeightFrameCode { get; set; }
    public string? ApprovalAuthority { get; set; }
    public string? Reason { get; set; }
}

#endregion

#region Hồ sơ tập thể, biên bản

/// <summary>Dòng chi tiết của Mẫu 06/08.</summary>
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

    /// <summary>Được tải lên, thay, gỡ tệp đính kèm (có quyền trên tổ chức của hồ sơ, kỳ chưa đóng).</summary>
    public bool CanAttach { get; set; }
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
    /// <summary>Khóa = mã tiêu chí con; rỗng = nhập thẳng hai điểm nhóm.</summary>
    public Dictionary<string, double> CriteriaScores { get; set; } = new();
    public double TotalScore { get; set; }
    public string SelfProposedGrade { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    /// <summary>Khóa = mã mục của biểu mẫu (Mẫu 07: I.1–I.4).</summary>
    public Dictionary<string, string> Sections { get; set; } = new();
    public List<CollectiveEvaluationItemDto> Items { get; set; } = new();
}

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
    /// <summary>Khi bộ tiêu chí có khung tập thể: máy chủ tự cộng ra hai điểm nhóm, bỏ qua giá trị gửi lên.</summary>
    public Dictionary<string, double>? CriteriaScores { get; set; }
    /// <summary>Khóa = mã mục (Mẫu 07: I.1–I.4); mã khác bị từ chối.</summary>
    public Dictionary<string, string> Sections { get; set; } = new();
    /// <summary>Mẫu 08: <see cref="CollectiveEvaluationItemDto.Category"/> là mã nhóm nội dung 1–13.</summary>
    public List<CollectiveEvaluationItemDto> Items { get; set; } = new();
}

/// <summary>Hội nghị và biên bản Mẫu 12, 13.</summary>
public class EvaluationMeetingDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>B3A_COLLECTIVE / B4_DECISION; null = không gắn bước.</summary>
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
    /// <summary>Các mục biên bản Mẫu 12 chưa có cột riêng.</summary>
    public MeetingDetailsDto Details { get; set; } = new();
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
    /// <summary>Phiếu "Chưa đánh giá, xếp loại".</summary>
    public int VotesNotRated { get; set; }
    public int InvalidVotes { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class SaveEvaluationMeetingRequestDto
{
    public uint? Version { get; set; }
    public Guid PeriodId { get; set; }
    /// <summary>Hội nghị Chi bộ (tùy chọn).</summary>
    public Guid? PartyCellId { get; set; }
    /// <summary>Hội nghị cấp Phòng (tùy chọn); không có cả hai = cấp Công ty.</summary>
    public Guid? DepartmentId { get; set; }
    /// <summary>B3A_COLLECTIVE / B4_DECISION (tùy chọn).</summary>
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
    /// <summary>Các mục biên bản Mẫu 12 chưa có cột riêng; null = để trống.</summary>
    public MeetingDetailsDto? Details { get; set; }
    public List<EvaluationMeetingVoteSummaryDto> VoteSummaries { get; set; } = new();
}

#endregion

/// <summary>Mức đề xuất của một bước khác mức theo điểm.</summary>
public class GradeDeviationDto
{
    public string Step { get; set; } = string.Empty;
    public string ProposedGrade { get; set; } = string.Empty;
    /// <summary>Mức theo ngưỡng điểm (và tỷ lệ vượt chuẩn) của bộ tiêu chí.</summary>
    public string ScoreGrade { get; set; } = string.Empty;
    public double Score { get; set; }
    /// <summary><c>Higher</c> / <c>Lower</c>.</summary>
    public string Direction { get; set; } = string.Empty;
    public bool HasExplanation { get; set; }
    public string Message { get; set; } = string.Empty;
}
