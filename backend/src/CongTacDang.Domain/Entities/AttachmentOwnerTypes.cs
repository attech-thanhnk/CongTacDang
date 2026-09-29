using System;
using System.Linq;

namespace CongTacDang.Domain.Entities;

/// <summary>Các loại đối tượng có thể sở hữu tệp đính kèm (<see cref="TaskAttachment.OwnerType"/>).</summary>
public static class AttachmentOwnerTypes
{
    /// <summary>
    /// Tệp chưa gắn đối tượng: tải lên trước rồi gắn vào nhiệm vụ / kết quả của cấp trên qua <c>AttachmentId</c>;
    /// chỉ người tải lên thấy tới khi được gắn. Không nhận làm <c>ownerType</c> từ client (không có trong <see cref="All"/>).
    /// </summary>
    public const string Unlinked = "Unlinked";

    /// <summary>Hồ sơ đánh giá cá nhân (<see cref="Entities.EvaluationRecord"/>).</summary>
    public const string EvaluationRecord = "EvaluationRecord";

    /// <summary>Nhiệm vụ/sản phẩm chuyên môn trong hồ sơ đánh giá (<see cref="Entities.EvaluationTask"/>).</summary>
    public const string EvaluationTask = "EvaluationTask";

    /// <summary>Danh sách loại đối tượng được gắn tệp (client truyền khi tải lên / tra cứu tệp của đối tượng).</summary>
    public static readonly string[] All = { EvaluationRecord, EvaluationTask };

    /// <summary>Chuẩn hóa tên loại đối tượng (không phân biệt hoa thường); trả về null nếu không hỗ trợ.</summary>
    public static string? Normalize(string? ownerType)
    {
        if (string.IsNullOrWhiteSpace(ownerType))
            return null;
        return All.FirstOrDefault(t => string.Equals(t, ownerType.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
