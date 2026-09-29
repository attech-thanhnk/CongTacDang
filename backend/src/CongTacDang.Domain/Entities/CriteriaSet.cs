using System;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Bộ tiêu chí và thang điểm theo phiên bản (task 16): tiêu chí chung + tiêu chí con, trục kết quả, khung tỷ trọng A-B-C-D,
/// thang quy đổi, mức xếp loại, tham số — nội dung jsonb <see cref="Content"/> (<see cref="CriteriaSetContent"/>).
/// Bản nháp sửa/xóa được; bộ đã xuất bản bất biến (chỉ nhân bản thành bản nháp mới); kỳ chọn bộ đã xuất bản và chụp nguyên bộ.
/// </summary>
public class CriteriaSet : IAuditableEntity, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    /// <summary>Mã định danh.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã bộ (duy nhất).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên bộ.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Trạng thái.</summary>
    public CriteriaSetStatus Status { get; set; } = CriteriaSetStatus.Draft;

    /// <summary>Mẫu tự chấm: 09A (chấm nhiệm vụ theo Mẫu 01/02 + A-B-C-D) hoặc 09B (chấm trực tiếp theo trục).</summary>
    public string SelfScoreForm { get; set; } = CriteriaSetContent.Form09A;

    /// <summary>Ghi chú (căn cứ văn bản, phạm vi áp dụng).</summary>
    public string? Notes { get; set; }

    /// <summary>Nội dung bộ (jsonb, có schemaVersion).</summary>
    public string Content { get; set; } = new CriteriaSetContent().ToJson();

    /// <summary>Bộ gốc khi được nhân bản.</summary>
    public Guid? SourceSetId { get; set; }

    /// <summary>Thời điểm xuất bản.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Người xuất bản.</summary>
    public Guid? PublishedBy { get; set; }

    /// <summary>Thời điểm lưu trữ.</summary>
    public DateTime? ArchivedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>Đọc nội dung bộ.</summary>
    public CriteriaSetContent GetContent() => CriteriaSetContent.Parse(Content);

    /// <summary>Ảnh chụp nguyên bộ để lưu vào kỳ.</summary>
    public CriteriaSnapshot TakeSnapshot(DateTime takenAt) => new()
    {
        SetId = Id,
        Code = Code,
        Name = Name,
        SelfScoreForm = SelfScoreForm,
        TakenAt = takenAt,
        Content = GetContent()
    };
}
