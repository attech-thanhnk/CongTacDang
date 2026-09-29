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
