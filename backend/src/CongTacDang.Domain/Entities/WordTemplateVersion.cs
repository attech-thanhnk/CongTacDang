using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Phiên bản file mẫu Word (nội dung trong kho tệp). Mỗi biểu mẫu tối đa một phiên bản kích hoạt;
/// không có thì dùng file mẫu gốc đi kèm ứng dụng.</summary>
public class WordTemplateVersion : IAuditableEntity
{
    public const int MaxCodeLength = 20;

    public const int MaxFileNameLength = 255;

    public const int MaxNoteLength = 500;

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ví dụ <c>MAU_02</c>.</summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>1, 2, … trong cùng biểu mẫu.</summary>
    public int VersionNumber { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Byte.</summary>
    public long FileSize { get; set; }

    /// <summary>SHA-256 (hex).</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>Đang dùng để xuất biểu mẫu.</summary>
    public bool IsActive { get; set; }

    public string? Note { get; set; }

    /// <summary>JSON mảng chuỗi, lấy lúc tải lên.</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>JSON mảng chuỗi, ví dụ tag bắt buộc bị thiếu.</summary>
    public string WarningsJson { get; set; } = "[]";

    public Guid? UploadedById { get; set; }

    /// <summary>Ảnh chụp lúc tải.</summary>
    public string? UploadedByName { get; set; }

    /// <summary>UTC.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC.</summary>
    public DateTime? ActivatedAt { get; set; }

    public Guid? ActivatedById { get; set; }

    public string? ActivatedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
