using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

public class EvaluationPeriod : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public int Year { get; set; }

    public EvaluationQuarter Quarter { get; set; }

    /// <summary>Ví dụ "Đánh giá, xếp loại cán bộ Quý IV/2026".</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public PeriodStatus Status { get; set; } = PeriodStatus.Draft;

    /// <summary>jsonb, đọc/ghi qua <see cref="PeriodSettings"/>; chuỗi rỗng = cấu hình mặc định.</summary>
    public string Settings { get; set; } = PeriodSettings.Default().ToJson();

    /// <summary>Chỉ bộ đã xuất bản; chỉ chọn/đổi khi kỳ còn dự thảo.</summary>
    public Guid? CriteriaSetId { get; set; }

    /// <summary>jsonb <see cref="Evaluation.CriteriaSnapshot"/>: chụp khi chọn bộ và khi mở kỳ; mọi tính điểm, biểu mẫu của kỳ dùng bản này.</summary>
    public string? CriteriaSnapshot { get; set; }

    /// <summary>Bắt buộc khi Khóa dữ liệu → Đang mở.</summary>
    public string? StatusReason { get; set; }

    public DateTime? StatusChangedAt { get; set; }

    public Guid? StatusChangedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Cũng là danh sách người được đánh giá.</summary>
    public ICollection<EvaluationRecord> Records { get; set; } = new List<EvaluationRecord>();

    /// <summary>Mẫu 06, 07, 08.</summary>
    public ICollection<CollectiveEvaluationRecord> CollectiveRecords { get; set; } = new List<CollectiveEvaluationRecord>();

    /// <summary>Hội nghị, biên bản Mẫu 12, 13.</summary>
    public ICollection<EvaluationMeeting> Meetings { get; set; } = new List<EvaluationMeeting>();

    public PeriodSettings GetSettings() => PeriodSettings.Parse(Settings);

    /// <summary>Null nếu kỳ chưa chọn bộ; JSON sai → <see cref="FormatException"/>.</summary>
    public CriteriaSnapshot? GetCriteria() => Evaluation.CriteriaSnapshot.Parse(CriteriaSnapshot);
}
