using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Security;

/// <summary>Cách tệp gắn vào hồ sơ — quyết định ai được sửa/xóa tệp.</summary>
public enum AttachmentLinkKind
{
    /// <summary>Gắn khi tải lên (TaskAttachment.RecordId) — tệp của chủ hồ sơ.</summary>
    Record,

    /// <summary>Minh chứng chủ hồ sơ chọn cho nhiệm vụ (EvaluationTask.AttachmentId).</summary>
    Task,

    /// <summary>Văn bản cấp trên (EvaluationExternalResult.AttachmentId) — người ghi nhận quản lý, người xem hồ sơ xem được.</summary>
    ExternalResult
}

/// <param name="Record">Nạp kèm Member.</param>
public sealed record AttachmentRecordLink(EvaluationRecord Record, AttachmentLinkKind Kind);

/// <summary>Hồ sơ tập thể sở hữu tệp — dữ liệu kiểm tra quyền trên tệp.</summary>
/// <param name="PeriodClosed">Kỳ đã đóng: không tải lên/thay/gỡ tệp.</param>
public sealed record CollectiveAttachmentOwner(System.Guid Id, System.Guid? DepartmentId, System.Guid? PartyCellId, bool PeriodClosed)
{
    public AccessTarget Target => new(DepartmentId: DepartmentId, PartyCellId: PartyCellId);

    /// <summary>Tải lên/thay/gỡ tệp: mọi tổ chức của hồ sơ đều phải trong phạm vi.</summary>
    public AccessTarget WriteTarget => Target.ForWrite();
}
