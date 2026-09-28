using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Thực thể lưu trữ tệp đính kèm văn bản và hồ sơ minh chứng đánh giá theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class TaskAttachment : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã định danh duy nhất của tệp đính kèm</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tên tệp tin hiển thị</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tên tệp tin gốc khi tải lên</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>Định dạng MIME của tệp (VD: application/pdf)</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Dung lượng tệp tính bằng byte</summary>
    public long FileSize { get; set; }

    /// <summary>Khóa định danh đường dẫn tệp trong Storage (VD: general/202609/uuid_name.pdf)</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Bí danh đường dẫn tệp tương thích ngược</summary>
    public string FilePath 
    { 
        get => ObjectKey; 
        set => ObjectKey = value; 
    }

    /// <summary>Mã băm SHA-256 kiểm tra tính toàn vẹn của tệp</summary>
    public string Checksum { get; set; } = string.Empty;

    /// <summary>Thời điểm tải tệp lên máy chủ</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Họ tên hoặc tài khoản người tải tệp lên (chỉ dùng để hiển thị)</summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>Mã người dùng đã tải tệp lên, dùng để kiểm tra quyền sở hữu. Dữ liệu cũ để null.</summary>
    public Guid? UploadedById { get; set; }

    /// <summary>Mã biểu mẫu hoặc ký hiệu hồ sơ (FormCode: M01, M02, M10... hoặc GENERAL)</summary>
    public string FormCode { get; set; } = "GENERAL";

    /// <summary>Mã định danh đối tượng nghiệp vụ liên quan</summary>
    public Guid? RelatedId { get; set; }

    /// <summary>Bí danh liên kết nhiệm vụ</summary>
    public Guid? TaskId { get => RelatedId; set => RelatedId = value; }

    /// <summary>Mã bản ghi liên kết</summary>
    public Guid? RecordId { get; set; }

    /// <summary>Trích yếu hoặc tóm tắt nội dung văn bản</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Trạng thái tệp (đang sử dụng / đã ẩn)</summary>
    public bool IsActive { get; set; } = true;

    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
