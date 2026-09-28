using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể Kỳ đánh giá định kỳ hằng quý theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class EvaluationPeriod : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

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

    /// <summary>Trạng thái kỳ: Draft → Open → Locked → Closed.</summary>
    public PeriodStatus Status { get; set; } = PeriodStatus.Draft;

    /// <summary>
    /// Cấu hình kỳ dạng JSON (cột jsonb, có schema version) — đọc/ghi qua <see cref="PeriodSettings"/>.
    /// Chuỗi rỗng được hiểu là mẫu "Đầy đủ theo HD03".
    /// </summary>
    public string Settings { get; set; } = PeriodSettings.FullPreset().ToJson();

    /// <summary>Lý do của lần chuyển trạng thái kỳ gần nhất (bắt buộc khi Khóa dữ liệu → Đang mở).</summary>
    public string? StatusReason { get; set; }

    /// <summary>Thời điểm chuyển trạng thái kỳ gần nhất.</summary>
    public DateTime? StatusChangedAt { get; set; }

    /// <summary>Người chuyển trạng thái kỳ gần nhất.</summary>
    public Guid? StatusChangedBy { get; set; }

    /// <summary>Thời điểm tạo kỳ đánh giá</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách hồ sơ đánh giá của các cán bộ trong kỳ (= danh sách người được đánh giá).</summary>
    public ICollection<EvaluationRecord> Records { get; set; } = new List<EvaluationRecord>();

    /// <summary>Danh sách hồ sơ đánh giá tập thể Mẫu 06, 07, 08 trong kỳ</summary>
    public ICollection<CollectiveEvaluationRecord> CollectiveRecords { get; set; } = new List<CollectiveEvaluationRecord>();

    /// <summary>Danh sách hội nghị và biên bản Mẫu 12, 13 trong kỳ</summary>
    public ICollection<EvaluationMeeting> Meetings { get; set; } = new List<EvaluationMeeting>();

    /// <summary>Đọc cấu hình kỳ.</summary>
    public PeriodSettings GetSettings() => PeriodSettings.Parse(Settings);
}
