using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Tệp đính kèm, minh chứng; có phiên bản theo nhóm.</summary>
public class TaskAttachment : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FileName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    /// <summary>Byte.</summary>
    public long FileSize { get; set; }

    /// <summary>Khóa trong kho lưu trữ (ví dụ mau02/202609/uuid_name.pdf).</summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>SHA-256.</summary>
    public string Checksum { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Chỉ để hiển thị; kiểm quyền sở hữu dùng <see cref="UploadedById"/>.</summary>
    public string UploadedBy { get; set; } = string.Empty;

    /// <summary>Dữ liệu cũ để null.</summary>
    public Guid? UploadedById { get; set; }

    /// <summary>Phân loại tệp (MAU01, MAU02, CAPTREN...).</summary>
    public string FormCode { get; set; } = string.Empty;

    /// <summary>Nhiệm vụ sở hữu khi tệp gắn nhiệm vụ.</summary>
    public Guid? RelatedId { get; set; }

    /// <summary>Hồ sơ đánh giá chứa đối tượng sở hữu.</summary>
    public Guid? RecordId { get; set; }

    /// <summary>Trích yếu văn bản.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>False = đã ẩn.</summary>
    public bool IsActive { get; set; } = true;

    #region Đối tượng sở hữu và phiên bản (T-36)

    /// <summary>Xem <see cref="AttachmentOwnerTypes"/>; null với dữ liệu cũ (suy ra từ RecordId / RelatedId).</summary>
    public string? OwnerType { get; set; }

    public Guid? OwnerId { get; set; }

    /// <summary>Id phiên bản đầu tiên, chung cho mọi phiên bản; null với dữ liệu cũ (dùng <see cref="GroupId"/>).</summary>
    public Guid? FileGroupId { get; set; }

    /// <summary>Bắt đầu từ 1.</summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>Mỗi nhóm có đúng một phiên bản chưa bị thay (hiện hành).</summary>
    public bool IsSuperseded { get; set; }

    public bool IsCurrent => !IsSuperseded;

    public DateTime? SupersededAt { get; set; }

    public Guid? SupersededById { get; set; }

    /// <summary>Xử lý cả dữ liệu cũ chưa có <see cref="FileGroupId"/>.</summary>
    public Guid GroupId => FileGroupId ?? Id;

    /// <summary>Suy ra từ cột cũ khi chưa có <see cref="OwnerType"/>.</summary>
    public string EffectiveOwnerType => OwnerType
        ?? (RecordId.HasValue ? AttachmentOwnerTypes.EvaluationRecord
            : RelatedId.HasValue ? AttachmentOwnerTypes.EvaluationTask
            : AttachmentOwnerTypes.Unlinked);

    /// <summary>Suy ra từ cột cũ khi chưa có <see cref="OwnerType"/>.</summary>
    public Guid? EffectiveOwnerId => OwnerType != null ? OwnerId : RecordId ?? RelatedId;

    #endregion

    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
