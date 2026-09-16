using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

public class EvaluationPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Year { get; set; }
    public EvaluationQuarter Quarter { get; set; }
    public string Name { get; set; } = string.Empty; // Ví dụ: Đánh giá định kỳ Quý I/2026

    // Lịch trình thời hạn
    public DateTime RegistrationDeadline { get; set; } // Hạn Mẫu 01 (ngày 5 đầu quý)
    public DateTime SelfEvaluationDeadline { get; set; } // Hạn Mẫu 02 (ngày 12 tháng cuối quý)
    public DateTime VotingDeadline { get; set; } // Hạn bỏ phiếu Mẫu 11 (ngày 15 tháng cuối quý)
    public DateTime ApprovalDeadline { get; set; } // Hạn phê duyệt (ngày 20 tháng cuối quý)

    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; } = false; // Khóa chốt dữ liệu
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public EvaluationSetting? Setting { get; set; }
    public ICollection<EvaluationRecord> Records { get; set; } = new List<EvaluationRecord>();
}

public class EvaluationSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public EvaluationPeriod? Period { get; set; }

    // 1. Thang điểm
    public decimal MaxTotalScore { get; set; } = 100m;
    public decimal GeneralCriteriaMaxScore { get; set; } = 30m; // Nhóm 30 điểm chung
    public decimal TaskCriteriaMaxScore { get; set; } = 70m;    // Nhóm 70 điểm nhiệm vụ

    // Chi tiết nhóm tiêu chí chung (18 - 4 - 8)
    public decimal Tc1Score { get; set; } = 18m; // Phẩm chất chính trị, đạo đức, kỷ luật
    public decimal Tc2Score { get; set; } = 4m;  // Đổi mới, dám nghĩ dám làm
    public decimal Tc3Score { get; set; } = 8m;  // Tự phê bình và phê bình, khắc phục hạn chế

    // 2. Tỷ lệ trần Xuất sắc (mặc định 20%)
    public decimal MaxExcellentPercentage { get; set; } = 20m;
    // Tỷ lệ trần đặc biệt cho đơn vị nổi trội (mặc định 25%)
    public decimal MaxExcellentSpecialPercentage { get; set; } = 25m;

    // 3. Số lượng nhiệm vụ đăng ký
    public int MinTasks { get; set; } = 3;
    public int MaxTasks { get; set; } = 7;

    // 4. Ngưỡng điểm xếp loại
    public decimal ExcellentMinScore { get; set; } = 90m; // >= 90
    public decimal GoodMinScore { get; set; } = 70m;      // >= 70
    public decimal PassMinScore { get; set; } = 50m;      // >= 50
}
