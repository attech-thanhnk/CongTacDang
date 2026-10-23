using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Security;

/// <summary>Cách một tệp đính kèm gắn vào hồ sơ đánh giá.</summary>
public enum AttachmentLinkKind
{
    /// <summary>Tệp gắn trực tiếp vào hồ sơ/nhiệm vụ khi tải lên (TaskAttachment.RecordId) — tệp của chủ hồ sơ.</summary>
    Record,

    /// <summary>Tệp minh chứng do chủ hồ sơ chọn cho nhiệm vụ (EvaluationTask.AttachmentId).</summary>
    Task,

    /// <summary>
    /// Văn bản của cấp trên gắn vào kết quả bước do cấp trên thực hiện (EvaluationExternalResult.AttachmentId) —
    /// người ghi nhận (<c>evaluation.external.record</c>) quản lý, người xem được hồ sơ xem được tệp.
    /// </summary>
    ExternalResult
}

/// <summary>Liên kết giữa tệp đính kèm và một hồ sơ đánh giá.</summary>
/// <param name="Record">Hồ sơ đánh giá (nạp kèm Member).</param>
/// <param name="Kind">Cách tệp gắn vào hồ sơ (quyết định quyền sửa/xóa tệp).</param>
public sealed record AttachmentRecordLink(EvaluationRecord Record, AttachmentLinkKind Kind);

/// <summary>Hồ sơ đánh giá tập thể sở hữu tệp (phụ lục, báo cáo kèm theo) — dữ liệu kiểm tra quyền trên tệp.</summary>
/// <param name="Id">Id hồ sơ tập thể.</param>
/// <param name="DepartmentId">Đơn vị chính quyền của hồ sơ.</param>
/// <param name="PartyCellId">Tổ chức Đảng của hồ sơ.</param>
/// <param name="PeriodClosed">Kỳ của hồ sơ đã đóng (không tải lên/thay/gỡ tệp).</param>
public sealed record CollectiveAttachmentOwner(System.Guid Id, System.Guid? DepartmentId, System.Guid? PartyCellId, bool PeriodClosed)
{
    /// <summary>Đối tượng kiểm tra quyền theo tổ chức của hồ sơ tập thể.</summary>
    public AccessTarget Target => new(DepartmentId: DepartmentId, PartyCellId: PartyCellId);

    /// <summary>Đối tượng kiểm tra quyền khi tải lên/thay/gỡ tệp: mọi tổ chức của hồ sơ đều phải trong phạm vi.</summary>
    public AccessTarget WriteTarget => Target.ForWrite();
}
