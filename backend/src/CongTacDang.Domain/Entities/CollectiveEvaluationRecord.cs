using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Hồ sơ đánh giá tập thể (Mẫu 06, 07, 08).</summary>
public class CollectiveEvaluationRecord : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
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

    public string SubjectName { get; set; } = string.Empty;

    public string Strengths { get; set; } = string.Empty;

    /// <summary>Hạn chế, khuyết điểm (nguyên nhân ở <see cref="Causes"/>).</summary>
    public string Limitations { get; set; } = string.Empty;
    public string Causes { get; set; } = string.Empty;

    /// <summary>Nhóm trường khắc phục, giải trình, trách nhiệm, phương hướng.</summary>
    public string PreviousRemediation { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Responsibilities { get; set; } = string.Empty;
    public string RemediationPlan { get; set; } = string.Empty;

    /// <summary>Mục con chưa có cột riêng (jsonb theo mã mục), ví dụ Mẫu 07 <c>{ "I.1": "…" }</c>; thêm mục không cần đổi schema.</summary>
    public string Sections { get; set; } = "{}";

    /// <summary>jsonb <c>{ "I.1": 4.5 }</c> theo mã tiêu chí trong <c>CollectiveGroups</c>; rỗng khi bộ không có khung tập thể.</summary>
    public string CriteriaScores { get; set; } = "{}";

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

/// <summary>Dòng nội dung chi tiết của Mẫu 06 hoặc 08.</summary>
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
