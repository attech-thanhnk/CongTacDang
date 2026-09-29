using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Một phiên bản file mẫu Word do quản trị tải lên (bảng <c>word_template_versions</c>, task 17 — T-81).
/// Nội dung tệp nằm trong kho tệp (<c>IFileStorageService</c>), bảng chỉ giữ metadata. Mỗi biểu mẫu có tối đa một
/// phiên bản đang kích hoạt; không có phiên bản kích hoạt → dùng file mẫu gốc đi kèm ứng dụng.
/// </summary>
public class WordTemplateVersion : IAuditableEntity
{
    /// <summary>Độ dài tối đa của mã biểu mẫu.</summary>
    public const int MaxCodeLength = 20;

    /// <summary>Độ dài tối đa của tên tệp gốc.</summary>
    public const int MaxFileNameLength = 255;

    /// <summary>Độ dài tối đa của ghi chú.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>Mã định danh.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã biểu mẫu trong danh mục (ví dụ <c>MAU_02</c>).</summary>
    public string TemplateCode { get; set; } = string.Empty;

    /// <summary>Số phiên bản (1, 2, …) trong biểu mẫu.</summary>
    public int VersionNumber { get; set; }

    /// <summary>Tên tệp người dùng tải lên.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Khóa tệp trong kho tệp.</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Dung lượng (byte).</summary>
    public long FileSize { get; set; }

    /// <summary>SHA-256 (hex) của nội dung tệp.</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>Đang được dùng để xuất biểu mẫu.</summary>
    public bool IsActive { get; set; }

    /// <summary>Ghi chú của người tải lên (lý do thay mẫu…).</summary>
    public string? Note { get; set; }

    /// <summary>Tag có trong tệp (JSON mảng chuỗi) tại thời điểm tải lên.</summary>
    public string TagsJson { get; set; } = "[]";

    /// <summary>Cảnh báo khi kiểm tra (JSON mảng chuỗi): tag bắt buộc bị thiếu…</summary>
    public string WarningsJson { get; set; } = "[]";

    /// <summary>Người tải lên.</summary>
    public Guid? UploadedById { get; set; }

    /// <summary>Họ tên người tải lên (ảnh chụp lúc tải).</summary>
    public string? UploadedByName { get; set; }

    /// <summary>Thời điểm tải lên (UTC).</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Lần kích hoạt gần nhất (UTC).</summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>Người kích hoạt gần nhất.</summary>
    public Guid? ActivatedById { get; set; }

    /// <summary>Họ tên người kích hoạt gần nhất.</summary>
    public string? ActivatedByName { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
