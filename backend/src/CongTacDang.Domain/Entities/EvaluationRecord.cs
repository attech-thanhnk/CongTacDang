using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể Hồ sơ Đánh giá, xếp loại cá nhân của Cán bộ theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class EvaluationRecord
{
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

    /// <summary>Mã Chi bộ sinh hoạt tại thời điểm đánh giá</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Đối tượng Chi bộ</summary>
    public PartyCell? PartyCell { get; set; }

    /// <summary>Mã Đơn vị chuyên môn tại thời điểm đánh giá</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Đối tượng Đơn vị chuyên môn</summary>
    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Khung chức danh công tác (1 đến 4) để áp tỷ trọng điểm tiêu chí</summary>
    public JobGroup JobGroup { get; set; } = JobGroup.Khung2_AnToanKyThuat;

    #region Bước 2: Điểm Tiêu chí Chung (Mẫu 09 - Tối đa 30.0 điểm)

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

    #region Bước 2: Điểm Sản phẩm Chuyên môn (Mẫu 02 - Tối đa 70.0 điểm)

    /// <summary>Tổng điểm tự chấm từ danh sách 3-7 công việc chuyên môn (tối đa 70.0đ)</summary>
    public double TasksScore { get; set; }

    /// <summary>Tổng điểm tự chấm toàn diện (Chung 30đ + Chuyên môn 70đ, tối đa 100.0đ)</summary>
    public double TotalSelfScore { get; set; }

    /// <summary>Mức xếp loại cá nhân cán bộ tự đề xuất</summary>
    public EvaluationGrade SelfProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    #endregion

    #region Bước 3: Đánh giá tại Hội nghị Chi bộ (Mẫu 10, 11, 13)

    /// <summary>Ý kiến nhận xét của Cấp ủy / Chi bộ nơi sinh hoạt Đảng (Mẫu 10)</summary>
    public string PartyCellComment { get; set; } = string.Empty;

    /// <summary>Mức xếp loại do Chi bộ đề xuất</summary>
    public EvaluationGrade PartyCellProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Số phiếu bầu Hoàn thành xuất sắc nhiệm vụ (Mẫu 13)</summary>
    public int VotesExcellent { get; set; }

    /// <summary>Số phiếu bầu Hoàn thành tốt nhiệm vụ (Mẫu 13)</summary>
    public int VotesGood { get; set; }

    /// <summary>Số phiếu bầu Hoàn thành nhiệm vụ (Mẫu 13)</summary>
    public int VotesSatisfactory { get; set; }

    /// <summary>Số phiếu bầu Không hoàn thành nhiệm vụ (Mẫu 13)</summary>
    public int VotesUnsatisfactory { get; set; }

    /// <summary>Tổng số đảng viên chính thức tham gia bỏ phiếu</summary>
    public int TotalVoters { get; set; }

    #endregion

    #region Bước 4: Thẩm định chuyên môn & Kiểm soát trần 20% (Mẫu 03 & Mẫu 15)

    /// <summary>Điểm do Tổ Thẩm định chấm lại (nếu có chênh lệch >= 5.0đ)</summary>
    public double? AppraisalScore { get; set; }

    /// <summary>Ý kiến nhận xét đối soát của Tổ Thẩm định Đảng ủy</summary>
    public string AppraisalComment { get; set; } = string.Empty;

    /// <summary>Mức xếp loại do Tổ Thẩm định đề xuất</summary>
    public EvaluationGrade AppraisalProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    #endregion

    #region Bước 5: Ban Thường vụ Phê duyệt Chính thức (Mẫu 14)

    /// <summary>Điểm số chính thức sau khi Ban Thường vụ họp quyết nghị</summary>
    public double FinalScore { get; set; }

    /// <summary>Mức xếp loại chất lượng chính thức do Ban Thường vụ chuẩn y</summary>
    public EvaluationGrade FinalGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    /// <summary>Trạng thái hồ sơ trong quy trình 5 bước</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Draft;

    #endregion

    /// <summary>Thời điểm cập nhật hồ sơ gần nhất</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Danh sách các công việc / sản phẩm chuyên môn (Mẫu 01 & Mẫu 02)</summary>
    public ICollection<EvaluationTask> Tasks { get; set; } = new List<EvaluationTask>();
}
