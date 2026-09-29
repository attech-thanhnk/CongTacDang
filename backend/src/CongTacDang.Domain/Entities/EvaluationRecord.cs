using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể Hồ sơ Đánh giá, xếp loại cá nhân của Cán bộ theo Hướng dẫn 03-HD/TVĐU.
/// Hồ sơ được tạo khi <c>period.manage</c> thêm người vào danh sách được đánh giá của kỳ (task 12);
/// Phòng, Chi bộ, khung chức danh, cấp quyết định là <b>ảnh chụp</b> tại thời điểm thêm.
/// </summary>
public class EvaluationRecord : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    /// <summary>Mã định danh hồ sơ đánh giá</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã kỳ đánh giá</summary>
    public Guid PeriodId { get; set; }

    /// <summary>Đối tượng kỳ đánh giá</summary>
    public EvaluationPeriod Period { get; set; } = null!;

    /// <summary>Mã cán bộ được đánh giá</summary>
    public Guid MemberId { get; set; }

    /// <summary>Đối tượng hồ sơ cán bộ</summary>
    public PartyMemberProfile Member { get; set; } = null!;

    /// <summary>Mã Chi bộ sinh hoạt tại thời điểm đánh giá (ảnh chụp)</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Đối tượng Chi bộ</summary>
    public PartyCell? PartyCell { get; set; }

    /// <summary>Mã Đơn vị chuyên môn tại thời điểm đánh giá (ảnh chụp)</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Đối tượng Đơn vị chuyên môn</summary>
    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Khung chức danh công tác (1 đến 4) để áp tỷ trọng điểm tiêu chí (ảnh chụp)</summary>
    public JobGroup JobGroup { get; set; } = JobGroup.Khung2_AnToanKyThuat;

    /// <summary>Cấp có thẩm quyền quyết định xếp loại — ảnh chụp từ hồ sơ cán bộ tại thời điểm thêm vào kỳ</summary>
    public ApprovalAuthority ApprovalAuthority { get; set; } = ApprovalAuthority.CoSo;

    /// <summary>
    /// Mã hồ sơ luồng (nhóm đối tượng) trong cấu hình kỳ — ảnh chụp khi thêm vào kỳ (mặc định theo cấp quyết định);
    /// <c>period.manage</c> đổi được (có lý do). Máy trạng thái, hành động, quyền dùng cấu hình bước của hồ sơ luồng này.
    /// </summary>
    public string WorkflowProfileCode { get; set; } = string.Empty;

    /// <summary>Trạng thái = bước đang chờ.</summary>
    public RecordStatus Status { get; set; } = RecordStatus.AwaitingRegistration;

    /// <summary>Lý do của lần trả lại gần nhất (xóa khi chủ hồ sơ nộp lại).</summary>
    public string? ReturnReason { get; set; }

    #region B1: Duyệt danh mục sản phẩm (Mẫu 01)

    /// <summary>Người duyệt danh mục.</summary>
    public Guid? TasksApprovedById { get; set; }

    /// <summary>Họ tên người duyệt danh mục.</summary>
    public string? TasksApprovedByName { get; set; }

    /// <summary>Thời điểm duyệt danh mục.</summary>
    public DateTime? TasksApprovedAt { get; set; }

    /// <summary>Ý kiến khi duyệt danh mục.</summary>
    public string? TasksApprovalComment { get; set; }

    #endregion

    #region B2: Điểm Tiêu chí Chung (Mẫu 09 - Tối đa 30.0 điểm)

    /// <summary>T1: Tư tưởng chính trị (tối đa 5.0đ)</summary>
    public double GeneralScoreT1 { get; set; }

    /// <summary>T2: Đạo đức, lối sống (tối đa 5.0đ)</summary>
    public double GeneralScoreT2 { get; set; }

    /// <summary>T3: Tác phong, lề lối làm việc (tối đa 5.0đ)</summary>
    public double GeneralScoreT3 { get; set; }

    /// <summary>T4: Ý thức tổ chức kỷ luật (tối đa 5.0đ)</summary>
    public double GeneralScoreT4 { get; set; }

    /// <summary>T5: Tinh thần đổi mới sáng tạo, dám nghĩ dám làm (tối đa 5.0đ)</summary>
    public double GeneralScoreT5 { get; set; }

    /// <summary>T6: Trách nhiệm nêu gương (tối đa 5.0đ)</summary>
    public double GeneralScoreT6 { get; set; }

    /// <summary>Tổng điểm Nhóm tiêu chí chung (T1 + ... + T6, tối đa 30.0đ)</summary>
    public double GeneralCriteriaScore { get; set; }

    #endregion

    #region B2: Điểm kết quả thực hiện nhiệm vụ (Mẫu 02 hoặc 6 trục Mẫu 09B — tối đa 70.0 điểm)

    /// <summary>Tổng điểm nhóm kết quả: từ 3-7 công việc (09A) hoặc tổng 6 trục (09B), tối đa 70.0đ</summary>
    public double TasksScore { get; set; }

    /// <summary>Mẫu 09B — Trục 1 (điểm tự chấm).</summary>
    public double? AxisScoreT1 { get; set; }

    /// <summary>Mẫu 09B — Trục 2.</summary>
    public double? AxisScoreT2 { get; set; }

    /// <summary>Mẫu 09B — Trục 3.</summary>
    public double? AxisScoreT3 { get; set; }

    /// <summary>Mẫu 09B — Trục 4.</summary>
    public double? AxisScoreT4 { get; set; }

    /// <summary>Mẫu 09B — Trục 5.</summary>
    public double? AxisScoreT5 { get; set; }

    /// <summary>Mẫu 09B — Trục 6.</summary>
    public double? AxisScoreT6 { get; set; }

    /// <summary>Mẫu tự chấm đã dùng (09A/09B) — theo cấu hình kỳ lúc nộp.</summary>
    public string? SelfScoreForm { get; set; }

    /// <summary>Tổng điểm tự chấm toàn diện (Chung 30đ + Chuyên môn 70đ, tối đa 100.0đ)</summary>
    public double TotalSelfScore { get; set; }

    /// <summary>Mức xếp loại cá nhân cán bộ tự đề xuất</summary>
    public EvaluationGrade SelfProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Thời điểm nộp phiếu tự chấm gần nhất.</summary>
    public DateTime? SelfScoredAt { get; set; }

    #endregion

    #region B2: Chi bộ xác nhận phiếu tự chấm

    /// <summary>Ý kiến xác nhận của Chi bộ trên phiếu tự chấm.</summary>
    public string PartyCellComment { get; set; } = string.Empty;

    /// <summary>Người xác nhận thay mặt Chi bộ.</summary>
    public Guid? CellConfirmedById { get; set; }

    /// <summary>Họ tên người xác nhận thay mặt Chi bộ.</summary>
    public string? CellConfirmedByName { get; set; }

    /// <summary>Thời điểm Chi bộ xác nhận ("Xác lập thời điểm" trên Mẫu 09x).</summary>
    public DateTime? CellConfirmedAt { get; set; }

    #endregion

    #region B3a: Đề xuất của tập thể lãnh đạo (kết quả phiếu kín — chỉ tổng hợp, gắn biên bản)

    /// <summary>Mức xếp loại tập thể lãnh đạo đề xuất.</summary>
    public EvaluationGrade CollectiveProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Nhận xét của tập thể lãnh đạo.</summary>
    public string? CollectiveComment { get; set; }

    /// <summary>Biên bản hội nghị/kiểm phiếu (Mẫu 12/13) chứa kết quả kiểm phiếu của hồ sơ.</summary>
    public Guid? CollectiveMeetingId { get; set; }

    /// <summary>Người ghi nhận (thư ký).</summary>
    public Guid? CollectiveRecordedById { get; set; }

    /// <summary>Họ tên người ghi nhận.</summary>
    public string? CollectiveRecordedByName { get; set; }

    /// <summary>Thời điểm ghi nhận.</summary>
    public DateTime? CollectiveRecordedAt { get; set; }

    #endregion

    #region B3b: Thẩm định (Mẫu 10, 03)

    /// <summary>Điểm do cơ quan thẩm định chấm lại (nếu có)</summary>
    public double? AppraisalScore { get; set; }

    /// <summary>Ý kiến thẩm định</summary>
    public string AppraisalComment { get; set; } = string.Empty;

    /// <summary>Mức xếp loại cơ quan thẩm định đề xuất</summary>
    public EvaluationGrade AppraisalProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Người thẩm định.</summary>
    public Guid? AppraisedById { get; set; }

    /// <summary>Họ tên người thẩm định.</summary>
    public string? AppraisedByName { get; set; }

    /// <summary>Thời điểm thẩm định.</summary>
    public DateTime? AppraisedAt { get; set; }

    #endregion

    #region B3c: Nhận xét, đề xuất của cấp trực tiếp sử dụng (Mẫu 10)

    /// <summary>Nhận xét của cấp trực tiếp sử dụng.</summary>
    public string? DirectorComment { get; set; }

    /// <summary>Mức xếp loại cấp trực tiếp sử dụng đề xuất.</summary>
    public EvaluationGrade DirectorProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Người nhận xét.</summary>
    public Guid? DirectorReviewedById { get; set; }

    /// <summary>Họ tên người nhận xét.</summary>
    public string? DirectorReviewedByName { get; set; }

    /// <summary>Thời điểm nhận xét.</summary>
    public DateTime? DirectorReviewedAt { get; set; }

    #endregion

    #region B4: Quyết định mức xếp loại (Mẫu 12–15)

    /// <summary>Điểm số chính thức (nếu văn bản quyết định ghi điểm)</summary>
    public double FinalScore { get; set; }

    /// <summary>Mức xếp loại chất lượng chính thức</summary>
    public EvaluationGrade FinalGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Số văn bản quyết định / thông báo kết quả.</summary>
    public string? DecisionDocumentNumber { get; set; }

    /// <summary>Ngày văn bản quyết định.</summary>
    public DateTime? DecisionDocumentDate { get; set; }

    /// <summary>Cơ quan quyết định (Đảng ủy cơ sở / BTV Đảng ủy Tổng công ty…).</summary>
    public string? DecisionAuthorityName { get; set; }

    /// <summary>Biên bản hội nghị/kiểm phiếu quyết định (nếu có).</summary>
    public Guid? DecisionMeetingId { get; set; }

    /// <summary>Người ghi nhận quyết định.</summary>
    public Guid? DecisionRecordedById { get; set; }

    /// <summary>Họ tên người ghi nhận quyết định.</summary>
    public string? DecisionRecordedByName { get; set; }

    /// <summary>Thời điểm ghi nhận quyết định.</summary>
    public DateTime? DecisionRecordedAt { get; set; }

    #endregion

    #region B5: Công bố

    /// <summary>Người công bố.</summary>
    public Guid? PublishedById { get; set; }

    /// <summary>Họ tên người công bố.</summary>
    public string? PublishedByName { get; set; }

    /// <summary>Thời điểm công bố.</summary>
    public DateTime? PublishedAt { get; set; }

    #endregion

    /// <summary>Thời điểm cập nhật hồ sơ gần nhất</summary>
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách các công việc / sản phẩm chuyên môn (Mẫu 01 & Mẫu 02)</summary>
    public ICollection<EvaluationTask> Tasks { get; set; } = new List<EvaluationTask>();

    /// <summary>Kết quả ghi nhận của các bước do cấp trên / cơ quan ngoài hệ thống thực hiện (mỗi bước một bản ghi).</summary>
    public ICollection<EvaluationExternalResult> ExternalResults { get; set; } = new List<EvaluationExternalResult>();

    /// <summary>Điểm hiệu lực: quyết định → thẩm định → tự chấm.</summary>
    public double EffectiveScore() =>
        FinalGrade != EvaluationGrade.ChuaXepLoai && FinalScore > 0 ? FinalScore : AppraisalScore ?? TotalSelfScore;

    /// <summary>Mức hiệu lực: quyết định → cấp trực tiếp sử dụng → thẩm định → tập thể → tự đề xuất.</summary>
    public EvaluationGrade EffectiveGrade()
    {
        foreach (var grade in new[] { FinalGrade, DirectorProposedGrade, AppraisalProposedGrade, CollectiveProposedGrade, SelfProposedGrade })
        {
            if (grade != EvaluationGrade.ChuaXepLoai)
                return grade;
        }
        return EvaluationGrade.ChuaXepLoai;
    }
}
