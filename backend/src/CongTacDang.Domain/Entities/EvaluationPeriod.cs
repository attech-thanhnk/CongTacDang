using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể Kỳ đánh giá định kỳ hằng quý theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class EvaluationPeriod : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh kỳ đánh giá</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Năm đánh giá (VD: 2026)</summary>
    public int Year { get; set; }

    /// <summary>Quý đánh giá trong năm (Quy1, Quy2, Quy3, Quy4)</summary>
    public EvaluationQuarter Quarter { get; set; }

    /// <summary>Tên hiển thị kỳ đánh giá (VD: "Đánh giá, xếp loại cán bộ Quý III/2026")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Ngày bắt đầu kỳ đánh giá</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Ngày kết thúc kỳ đánh giá</summary>
    public DateTime EndDate { get; set; }

    /// <summary>Trạng thái tiến trình quy trình 5 bước</summary>
    public PeriodStatus Status { get; set; } = PeriodStatus.Draft;

    /// <summary>Kỳ đánh giá có đang hoạt động hay không</summary>
    public bool IsActive { get; set; } = false;

    /// <summary>Thời điểm tạo kỳ đánh giá</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách hồ sơ đánh giá của các cán bộ trong kỳ</summary>
    public ICollection<EvaluationRecord> Records { get; set; } = new List<EvaluationRecord>();

    /// <summary>Danh sách hồ sơ đánh giá tập thể Mẫu 06, 07, 08 trong kỳ</summary>
    public ICollection<CollectiveEvaluationRecord> CollectiveRecords { get; set; } = new List<CollectiveEvaluationRecord>();

    /// <summary>Danh sách hội nghị và biên bản Mẫu 12, 13 trong kỳ</summary>
    public ICollection<EvaluationMeeting> Meetings { get; set; } = new List<EvaluationMeeting>();
}
