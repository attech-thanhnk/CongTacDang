using System;
using System.Linq;

namespace CongTacDang.Domain.Entities;

/// <summary>Các loại đối tượng có thể sở hữu tệp đính kèm (<see cref="TaskAttachment.OwnerType"/>).</summary>
public static class AttachmentOwnerTypes
{
    /// <summary>Tải lên trước rồi gắn qua <c>AttachmentId</c>; chỉ người tải lên thấy tới khi gắn. Client không truyền được.</summary>
    public const string Unlinked = "Unlinked";

    public const string EvaluationRecord = "EvaluationRecord";

    public const string EvaluationTask = "EvaluationTask";

    /// <summary>Chủ hồ sơ chỉ bổ sung tệp khi kiến nghị còn xử lý (không thay, không gỡ).</summary>
    public const string EvaluationAppeal = "EvaluationAppeal";

    /// <summary>Hồ sơ tập thể (Mẫu 06/07/08); tải lên/thay/gỡ cần <c>collective.manage</c> và kỳ chưa đóng.</summary>
    public const string CollectiveEvaluationRecord = "CollectiveEvaluationRecord";

    /// <summary>Phiếu đối soát chuyên ngành (Mẫu 03); chưa cho client truyền tới khi service phiếu có quy tắc phạm vi.</summary>
    public const string ExpertCrosscheck = "ExpertCrosscheck";

    /// <summary>Phiếu kiểm soát (Mẫu 19); chưa cho client truyền tới khi service phiếu có quy tắc phạm vi.</summary>
    public const string ControlCheck = "ControlCheck";

    /// <summary>Biên bản hiệu chuẩn (Mẫu 20); chưa cho client truyền tới khi service biên bản có quy tắc phạm vi.</summary>
    public const string CalibrationSession = "CalibrationSession";

    /// <summary>Các loại client được truyền.</summary>
    public static readonly string[] All = { EvaluationRecord, EvaluationTask, EvaluationAppeal, CollectiveEvaluationRecord };

    /// <summary>Không phân biệt hoa thường; null nếu không hỗ trợ.</summary>
    public static string? Normalize(string? ownerType)
    {
        if (string.IsNullOrWhiteSpace(ownerType))
            return null;
        return All.FirstOrDefault(t => string.Equals(t, ownerType.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
