using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Security;

/// <summary>Liên kết giữa tệp đính kèm và một hồ sơ đánh giá.</summary>
/// <param name="Record">Hồ sơ đánh giá (nạp kèm Member).</param>
/// <param name="ViaTask">
/// true nếu liên kết qua nhiệm vụ (EvaluationTask.AttachmentId, do người dùng tự chọn khi đăng ký/tự chấm);
/// false nếu qua TaskAttachment.RecordId.
/// </param>
public sealed record AttachmentRecordLink(EvaluationRecord Record, bool ViaTask);
