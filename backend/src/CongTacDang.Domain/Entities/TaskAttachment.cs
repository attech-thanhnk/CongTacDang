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

    #region Đối tượng sở hữu và phiên bản (T-36)

    /// <summary>
    /// Loại đối tượng sở hữu tệp (xem <see cref="AttachmentOwnerTypes"/>). Null với dữ liệu cũ —
    /// khi đó đối tượng được suy ra từ <see cref="RecordId"/> / <see cref="RelatedId"/>.
    /// </summary>
    public string? OwnerType { get; set; }

    /// <summary>Mã đối tượng sở hữu tệp (null với tệp chung hoặc dữ liệu cũ).</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>
    /// Mã nhóm phiên bản: mọi phiên bản của cùng một tệp có chung giá trị này (bằng Id của phiên bản đầu tiên).
    /// Null với dữ liệu cũ — khi đó tệp tự là nhóm của chính nó (<see cref="GroupId"/> = <see cref="Id"/>).
    /// </summary>
    public Guid? FileGroupId { get; set; }

    /// <summary>Số thứ tự phiên bản trong nhóm, bắt đầu từ 1.</summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// Phiên bản đã bị thay bằng phiên bản mới hơn. Mỗi nhóm có đúng một phiên bản chưa bị thay (hiện hành).
    /// Mặc định false để dữ liệu cũ tự là phiên bản hiện hành.
    /// </summary>
    public bool IsSuperseded { get; set; }

    /// <summary>Phiên bản hiện hành của nhóm.</summary>
    public bool IsCurrent => !IsSuperseded;

    /// <summary>Thời điểm phiên bản này bị thay bằng phiên bản mới.</summary>
    public DateTime? SupersededAt { get; set; }

    /// <summary>Người đã thay phiên bản này bằng phiên bản mới.</summary>
    public Guid? SupersededById { get; set; }

    /// <summary>Mã nhóm phiên bản thực tế (xử lý cả dữ liệu cũ chưa có <see cref="FileGroupId"/>).</summary>
    public Guid GroupId => FileGroupId ?? Id;

    /// <summary>Loại đối tượng sở hữu thực tế, suy ra từ cột cũ khi chưa có <see cref="OwnerType"/>.</summary>
    public string EffectiveOwnerType => OwnerType
        ?? (RecordId.HasValue ? AttachmentOwnerTypes.EvaluationRecord
            : RelatedId.HasValue ? AttachmentOwnerTypes.EvaluationTask
            : AttachmentOwnerTypes.General);

    /// <summary>Mã đối tượng sở hữu thực tế, suy ra từ cột cũ khi chưa có <see cref="OwnerType"/>.</summary>
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
