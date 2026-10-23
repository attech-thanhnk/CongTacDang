using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Hồ sơ đánh giá, xếp loại cá nhân trong một kỳ. Chi bộ, đơn vị, khung tỷ trọng, cấp quyết định
/// là ảnh chụp lúc thêm người vào kỳ.</summary>
public class EvaluationRecord : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PeriodId { get; set; }

    public EvaluationPeriod Period { get; set; } = null!;

    public Guid MemberId { get; set; }

    public PartyMemberProfile Member { get; set; } = null!;

    /// <summary>Ảnh chụp lúc thêm vào kỳ.</summary>
    public Guid? PartyCellId { get; set; }

    public PartyCell? PartyCell { get; set; }

    /// <summary>Ảnh chụp lúc thêm vào kỳ.</summary>
    public Guid? DepartmentId { get; set; }

    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Ảnh chụp khung mặc định của cán bộ; phải có trong bộ tiêu chí của kỳ.</summary>
    public string WeightFrameCode { get; set; } = string.Empty;

    /// <summary>Tỷ trọng A (0..1) đã chỉnh riêng cho hồ sơ; cả bốn FrameA..D null = dùng khung gốc
    /// (xem <c>RecordWeightFrames.Effective</c>).</summary>
    public double? FrameA { get; set; }

    public double? FrameB { get; set; }

    public double? FrameC { get; set; }

    public double? FrameD { get; set; }

    /// <summary>Ảnh chụp từ hồ sơ cán bộ lúc thêm vào kỳ.</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; } = ApprovalAuthority.CoSo;

    /// <summary>Hồ sơ luồng trong cấu hình kỳ, quyết định bước, hành động và quyền. Mặc định theo cấp quyết định;
    /// đổi được (có lý do).</summary>
    public string WorkflowProfileCode { get; set; } = string.Empty;

    /// <summary>Trạng thái = bước đang chờ.</summary>
    public RecordStatus Status { get; set; } = RecordStatus.AwaitingRegistration;

    /// <summary>Lý do trả lại gần nhất; xóa khi chủ hồ sơ nộp lại.</summary>
    public string? ReturnReason { get; set; }

    #region Mở riêng hồ sơ khi kỳ khóa dữ liệu

    /// <summary>Hạn (UTC) mở riêng hồ sơ khi kỳ đang khóa dữ liệu; xóa khi hồ sơ tới bước được thao tác lúc kỳ khóa
    /// (hoặc công bố).</summary>
    public DateTime? UnlockedUntil { get; set; }

    public DateTime? UnlockedAt { get; set; }

    public Guid? UnlockedById { get; set; }

    public string? UnlockedByName { get; set; }

    public string? UnlockReason { get; set; }

    public bool IsUnlockedAt(DateTime utcNow) => UnlockedUntil is { } until && until > utcNow;

    #endregion

    #region B1: Duyệt danh mục sản phẩm (Mẫu 01)

    public Guid? TasksApprovedById { get; set; }

    public string? TasksApprovedByName { get; set; }

    public DateTime? TasksApprovedAt { get; set; }

    public string? TasksApprovalComment { get; set; }

    #endregion

    #region B2: Điểm nhóm tiêu chí chung (theo bộ tiêu chí của kỳ)

    /// <summary>jsonb theo mã tiêu chí con: <c>{ "1.1": { "score": 2, "notApplicable": false, "reason": null } }</c>
    /// (<see cref="Evaluation.GeneralItemScore"/>).</summary>
    public string GeneralScores { get; set; } = "{}";

    /// <summary>Đã xử lý "K/AD".</summary>
    public double GeneralCriteriaScore { get; set; }

    #endregion

    #region B2: Điểm kết quả thực hiện nhiệm vụ (Mẫu 02 hoặc theo trục Mẫu 09B)

    /// <summary>Tổng điểm nhiệm vụ (09A) hoặc tổng điểm các trục (09B).</summary>
    public double TasksScore { get; set; }

    /// <summary>jsonb <c>{ "T1": 14 }</c> (Mẫu 09B); null khi không chấm theo trục.</summary>
    public string? AxisScores { get; set; }

    /// <summary>09A/09B theo bộ tiêu chí của kỳ lúc nộp.</summary>
    public string? SelfScoreForm { get; set; }

    /// <summary>Chung + nhiệm vụ, làm tròn theo bộ tiêu chí.</summary>
    public double TotalSelfScore { get; set; }

    public EvaluationGrade SelfProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    public DateTime? SelfScoredAt { get; set; }

    #endregion

    #region B2: Nội dung biểu mẫu cá nhân nhập cùng phiếu tự chấm (task 18)

    /// <summary>Mẫu 09C, jsonb <c>{ "I": "…" }</c> theo mã mục (<see cref="Evaluation.SelfAssessmentSection"/>).</summary>
    public string SelfAssessment { get; set; } = "{}";

    /// <summary>jsonb <c>{ "T1": <see cref="Evaluation.AxisReport"/> }</c>: nhiệm vụ kèm mức in ở Mẫu 09C,
    /// phần tóm tắt là một dòng Mẫu 9D.</summary>
    public string AxisReports { get; set; } = "{}";

    /// <summary>Mẫu 09B, jsonb <c>{ "T1": { "target", "result", "note" } }</c> (<see cref="Evaluation.AxisNote"/>).</summary>
    public string? AxisNotes { get; set; }

    /// <summary>Nhận xét theo nhóm tiêu chí chung, jsonb <c>{ "1": "…" }</c>; in ở ô "Tóm tắt kết quả/Tài liệu minh chứng".</summary>
    public string? GeneralGroupNotes { get; set; }

    #endregion

    #region B2: Chi bộ xác nhận phiếu tự chấm

    public string PartyCellComment { get; set; } = string.Empty;

    public Guid? CellConfirmedById { get; set; }

    public string? CellConfirmedByName { get; set; }

    /// <summary>"Xác lập thời điểm" trên Mẫu 09x.</summary>
    public DateTime? CellConfirmedAt { get; set; }

    #endregion

    #region B3a: Đề xuất của tập thể lãnh đạo (kết quả phiếu kín — chỉ tổng hợp, gắn biên bản)

    public EvaluationGrade CollectiveProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    public string? CollectiveComment { get; set; }

    /// <summary>Biên bản hội nghị/kiểm phiếu (Mẫu 12/13) chứa kết quả kiểm phiếu.</summary>
    public Guid? CollectiveMeetingId { get; set; }

    /// <summary>Thư ký ghi nhận.</summary>
    public Guid? CollectiveRecordedById { get; set; }

    public string? CollectiveRecordedByName { get; set; }

    public DateTime? CollectiveRecordedAt { get; set; }

    #endregion

    #region B3b: Thẩm định (Mẫu 10, 03)

    /// <summary>Điểm thẩm định chung + nhiệm vụ, làm tròn theo quy tắc "tổng điểm". Kết quả cấp trên ghi nhận chỉ có tổng.</summary>
    public double? AppraisalScore { get; set; }

    /// <summary>Null khi chỉ có điểm tổng.</summary>
    public double? AppraisalGeneralScore { get; set; }

    /// <summary>Null khi chỉ có điểm tổng.</summary>
    public double? AppraisalTasksScore { get; set; }

    public string AppraisalComment { get; set; } = string.Empty;

    /// <summary>Bắt buộc khi chênh lệch với tự chấm từ ngưỡng của bộ tiêu chí trở lên hoặc làm đổi mức.</summary>
    public string? AppraisalExplanation { get; set; }

    public EvaluationGrade AppraisalProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    public Guid? AppraisedById { get; set; }

    public string? AppraisedByName { get; set; }

    public DateTime? AppraisedAt { get; set; }

    #endregion

    #region B3c: Nhận xét, đề xuất của cấp trực tiếp sử dụng (Mẫu 10)

    public string? DirectorComment { get; set; }

    public EvaluationGrade DirectorProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    public Guid? DirectorReviewedById { get; set; }

    public string? DirectorReviewedByName { get; set; }

    public DateTime? DirectorReviewedAt { get; set; }

    #endregion

    #region B4: Quyết định mức xếp loại (Mẫu 12–15)

    /// <summary>Chỉ có khi văn bản quyết định ghi điểm.</summary>
    public double FinalScore { get; set; }

    public EvaluationGrade FinalGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    public string? DecisionDocumentNumber { get; set; }

    public DateTime? DecisionDocumentDate { get; set; }

    /// <summary>Ví dụ Đảng ủy cơ sở, BTV Đảng ủy Tổng công ty.</summary>
    public string? DecisionAuthorityName { get; set; }

    /// <summary>Đề xuất về công tác cán bộ (cột 13 Mẫu 14).</summary>
    public string? CadreWorkProposal { get; set; }

    public Guid? DecisionMeetingId { get; set; }

    public Guid? DecisionRecordedById { get; set; }

    public string? DecisionRecordedByName { get; set; }

    public DateTime? DecisionRecordedAt { get; set; }

    #endregion

    #region B5: Công bố

    public Guid? PublishedById { get; set; }

    public string? PublishedByName { get; set; }

    public DateTime? PublishedAt { get; set; }

    #endregion

    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Sản phẩm chuyên môn (Mẫu 01, 02).</summary>
    public ICollection<EvaluationTask> Tasks { get; set; } = new List<EvaluationTask>();

    /// <summary>Kết quả các bước do cấp trên thực hiện, mỗi bước một bản ghi.</summary>
    public ICollection<EvaluationExternalResult> ExternalResults { get; set; } = new List<EvaluationExternalResult>();

    /// <summary>Quyết định → thẩm định → tự chấm (chỉ khi đã nộp phiếu).</summary>
    public double EffectiveScore() =>
        FinalGrade != EvaluationGrade.ChuaXepLoai && FinalScore > 0 ? FinalScore : AppraisalScore ?? (SelfScoredAt.HasValue ? TotalSelfScore : 0);

    /// <summary>Quyết định → cấp trực tiếp sử dụng → thẩm định → tập thể → tự đề xuất.</summary>
    public EvaluationGrade EffectiveGrade()
    {
        // Mức tự đề xuất chỉ tính khi đã nộp phiếu tự chấm (bản nháp không tính).
        var self = SelfScoredAt.HasValue ? SelfProposedGrade : EvaluationGrade.ChuaXepLoai;
        foreach (var grade in new[] { FinalGrade, DirectorProposedGrade, AppraisalProposedGrade, CollectiveProposedGrade, self })
        {
            if (grade != EvaluationGrade.ChuaXepLoai)
                return grade;
        }
        return EvaluationGrade.ChuaXepLoai;
    }
}
