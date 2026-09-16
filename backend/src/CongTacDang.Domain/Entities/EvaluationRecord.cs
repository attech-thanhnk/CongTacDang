using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

public class EvaluationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public EvaluationPeriod? Period { get; set; }

    public Guid MemberId { get; set; }
    public PartyMemberProfile? Member { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Draft;

    // --- MẪU 02: KẾT QUẢ TỰ CHẤM CỦA CÁ NHÂN ---
    public decimal SelfScoreTc1 { get; set; } = 18m;
    public decimal SelfScoreTc2 { get; set; } = 4m;
    public decimal SelfScoreTc3 { get; set; } = 8m;
    public decimal SelfGeneralScore => SelfScoreTc1 + SelfScoreTc2 + SelfScoreTc3; // Tối đa 30đ

    public decimal SelfTaskScore { get; set; } = 0m; // Tổng điểm các nhiệm vụ (Tối đa 70đ)
    public decimal SelfTotalScore => SelfGeneralScore + SelfTaskScore; // Tối đa 100đ
    public EvaluationGrade SelfGrade { get; set; } = EvaluationGrade.ChuaXepLoai;
    public string SelfExplanation { get; set; } = string.Empty; // Giải trình ưu/khuyết điểm

    // --- Ý KIẾN CỦA CẤP QUẢN LÝ TRỰC TIẾP ---
    public string SupervisorFeedback { get; set; } = string.Empty;
    public EvaluationGrade SupervisorSuggestedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;

    // --- KẾT QUẢ THẨM ĐỊNH & PHÊ DUYỆT CHÍNH THỨC CỦA CẤP ỦY ---
    public decimal? FinalApprovedScore { get; set; }
    public EvaluationGrade FinalApprovedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;
    public string ApprovalNotes { get; set; } = string.Empty;
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }

    public ICollection<EvaluationTask> Tasks { get; set; } = new List<EvaluationTask>();
}

public class EvaluationTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RecordId { get; set; }
    public EvaluationRecord? Record { get; set; }

    public int OrderNumber { get; set; } // Thứ tự 1, 2, 3...
    public string TaskName { get; set; } = string.Empty; // Tên sản phẩm/nhiệm vụ
    public TaskResultAxis Axis { get; set; } = TaskResultAxis.T1; // 1 trong 6 trục
    public string TargetOutput { get; set; } = string.Empty; // Sản phẩm đầu ra
    public string CompletionQuarter { get; set; } = string.Empty; // Mốc hoàn thành trong quý
    public string StandardBenchmark { get; set; } = string.Empty; // Tiêu chuẩn đạt
    public string ExceededBenchmark { get; set; } = string.Empty; // Dấu hiệu vượt chuẩn

    // Trọng số nhiệm vụ (Tổng các nhiệm vụ của 1 cá nhân = 70 điểm)
    public decimal Weight { get; set; } = 15m;

    // Tỷ trọng thành phần A-B-C-D của nhiệm vụ này (Tổng = 100%)
    public decimal WeightA { get; set; } = 25m; // Khối lượng
    public decimal WeightB { get; set; } = 35m; // Chất lượng/An toàn
    public decimal WeightC { get; set; } = 20m; // Tiến độ
    public decimal WeightD { get; set; } = 20m; // Tổ chức/Hiệu quả

    // Mức % thực hiện tự chấm (0 - 100%)
    public decimal ScoreA_Percent { get; set; } = 100m;
    public decimal ScoreB_Percent { get; set; } = 100m;
    public decimal ScoreC_Percent { get; set; } = 100m;
    public decimal ScoreD_Percent { get; set; } = 100m;

    // Công thức tính điểm nhiệm vụ:
    // ResultPercent = A * wA + B * wB + C * wC + D * wD (%)
    // TaskScore = ResultPercent * Weight / 100
    public decimal FinalTaskScore { get; set; } = 0m;

    // Ghi nhận vượt chuẩn (để xét điều kiện Xuất sắc >= 30% việc vượt chuẩn)
    public bool IsExceededStandard { get; set; } = false;

    // Minh chứng (tên tệp hoặc URL văn bản trên VPCT)
    public string EvidenceDescription { get; set; } = string.Empty;
    public string? EvidenceFileBlobName { get; set; }
    public string? EvidenceFileName { get; set; }
}
