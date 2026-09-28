using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Hồ sơ đánh giá tập thể cho Mẫu 06, 07 và 08. Nội dung chi tiết được lưu theo nhóm trường của từng mẫu.
/// </summary>
public class CollectiveEvaluationRecord : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public EvaluationPeriod Period { get; set; } = null!;
    public CollectiveEvaluationForm Form { get; set; }
    public Guid? PartyCellId { get; set; }
    public PartyCell? PartyCell { get; set; }
    public Guid? DepartmentId { get; set; }
    public AdministrativeDepartment? Department { get; set; }
    public Guid? HeadId { get; set; }
    public PartyMemberProfile? Head { get; set; }

    /// <summary>Tên tập thể hoặc lĩnh vực được đánh giá.</summary>
    public string SubjectName { get; set; } = string.Empty;

    /// <summary>Nội dung ưu điểm, kết quả đạt được.</summary>
    public string Strengths { get; set; } = string.Empty;

    /// <summary>Nội dung hạn chế, khuyết điểm và nguyên nhân.</summary>
    public string Limitations { get; set; } = string.Empty;
    public string Causes { get; set; } = string.Empty;

    /// <summary>Kết quả khắc phục, giải trình, trách nhiệm và phương hướng khắc phục.</summary>
    public string PreviousRemediation { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public string RemediationPlan { get; set; } = string.Empty;

    /// <summary>Điểm nhóm tiêu chí chung, nhóm kết quả và tổng điểm.</summary>
    public double GeneralCriteriaScore { get; set; }
    public double TaskCriteriaScore { get; set; }
    public double TotalScore { get; set; }
    public EvaluationGrade SelfProposedGrade { get; set; } = EvaluationGrade.ChuaXepLoai;
    public CollectiveRecordStatus Status { get; set; } = CollectiveRecordStatus.Draft;

    public ICollection<CollectiveEvaluationItem> Items { get; set; } = new List<CollectiveEvaluationItem>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Chỉ tiêu/dòng nội dung chi tiết của Mẫu 06 hoặc Mẫu 08.</summary>
public class CollectiveEvaluationItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CollectiveRecordId { get; set; }
    public CollectiveEvaluationRecord CollectiveRecord { get; set; } = null!;
    public int ItemOrder { get; set; }
    public string Category { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string PlanOrDirection { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Limitations { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}
