using System;
using System.Linq;

namespace CongTacDang.Domain.Entities;

/// <summary>Các loại đối tượng có thể sở hữu tệp đính kèm (<see cref="TaskAttachment.OwnerType"/>).</summary>
public static class AttachmentOwnerTypes
{
    /// <summary>Văn bản chung, không gắn đối tượng nghiệp vụ.</summary>
    public const string General = "General";

    /// <summary>Hồ sơ đánh giá cá nhân (<see cref="Entities.EvaluationRecord"/>).</summary>
    public const string EvaluationRecord = "EvaluationRecord";

    /// <summary>Nhiệm vụ/sản phẩm chuyên môn trong hồ sơ đánh giá (<see cref="Entities.EvaluationTask"/>).</summary>
    public const string EvaluationTask = "EvaluationTask";

    /// <summary>Danh sách loại đối tượng được hỗ trợ.</summary>
    public static readonly string[] All = { General, EvaluationRecord, EvaluationTask };

    /// <summary>Chuẩn hóa tên loại đối tượng (không phân biệt hoa thường); trả về null nếu không hỗ trợ.</summary>
    public static string? Normalize(string? ownerType)
    {
        if (string.IsNullOrWhiteSpace(ownerType))
            return null;
        return All.FirstOrDefault(t => string.Equals(t, ownerType.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
