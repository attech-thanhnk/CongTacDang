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

/// <summary>Một điều kiện quyền: có <paramref name="Permission"/> trên mọi đích trong <paramref name="Targets"/>.</summary>
public sealed record AttachmentPermissionRule(string Permission, System.Collections.Generic.IReadOnlyList<AccessTarget> Targets);

/// <summary>Phiếu đối soát / phiếu kiểm soát / biên bản hiệu chuẩn sở hữu tệp — dữ liệu kiểm tra quyền.</summary>
/// <param name="RecordMemberIds">Chủ của các hồ sơ trong phiếu; những người này bị chặn như khi xem phiếu.</param>
/// <param name="Editable">Phiếu còn sửa được và kỳ đang mở; false thì khóa tải lên/thay/gỡ.</param>
/// <param name="ViewRules">Thỏa một điều kiện là xem/tải được.</param>
/// <param name="EditRules">Thỏa một điều kiện là tải lên/thay/gỡ được (khi <paramref name="Editable"/>).</param>
public sealed record FormAttachmentOwner(
    System.Guid Id,
    string OwnerType,
    System.Collections.Generic.IReadOnlyCollection<System.Guid> RecordMemberIds,
    bool Editable,
    System.Collections.Generic.IReadOnlyList<AttachmentPermissionRule> ViewRules,
    System.Collections.Generic.IReadOnlyList<AttachmentPermissionRule> EditRules);
