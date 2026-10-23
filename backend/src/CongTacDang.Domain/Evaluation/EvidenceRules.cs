using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Giai đoạn của minh chứng cá nhân (tệp chủ hồ sơ gắn vào hồ sơ / nhiệm vụ của mình).</summary>
public enum EvidenceStage
{
    /// <summary>Trước khi nộp phiếu tự chấm, hoặc hồ sơ được trả về bước của chủ hồ sơ: thêm, thay, gỡ tự do.</summary>
    Open = 1,

    /// <summary>Đã nộp phiếu tự chấm, chưa công bố: chỉ được bổ sung tệp mới, không thay, không gỡ tệp đã nộp.</summary>
    AddOnly = 2,

    /// <summary>Đã công bố hoặc kỳ không mở (dự thảo, khóa dữ liệu, đã đóng): khóa minh chứng; bổ sung qua kiến nghị.</summary>
    Locked = 3
}

/// <summary>
/// Luật minh chứng cá nhân theo giai đoạn của hồ sơ (thuần, không phụ thuộc quyền). Quyền trên hồ sơ (<c>evaluation.self</c>)
/// kiểm tra riêng; luật này chỉ quyết định thao tác nào còn được làm ở trạng thái hiện tại.
/// Không áp dụng cho văn bản của cấp trên gắn vào kết quả bước do cấp trên thực hiện.
/// </summary>
public static class EvidenceRules
{
    /// <summary>Giai đoạn minh chứng; hồ sơ đang được mở riêng thì kỳ khóa dữ liệu không chặn.</summary>
    public static EvidenceStage StageOf(RecordStatus recordStatus, PeriodStatus periodStatus, bool recordUnlocked = false)
    {
        var periodOpen = periodStatus == PeriodStatus.Open || (periodStatus == PeriodStatus.Locked && recordUnlocked);
        if (!periodOpen || recordStatus == RecordStatus.Published)
            return EvidenceStage.Locked;
        return recordStatus is RecordStatus.AwaitingRegistration or RecordStatus.AwaitingTaskApproval or RecordStatus.AwaitingSelfScore
            ? EvidenceStage.Open
            : EvidenceStage.AddOnly;
    }

    /// <summary>Được bổ sung tệp mới.</summary>
    public static bool CanAdd(EvidenceStage stage) => stage != EvidenceStage.Locked;

    /// <summary>Được thay tệp bằng phiên bản mới.</summary>
    public static bool CanReplace(EvidenceStage stage) => stage == EvidenceStage.Open;

    /// <summary>Được gỡ tệp.</summary>
    public static bool CanRemove(EvidenceStage stage) => stage == EvidenceStage.Open;

    /// <summary>Lý do không bổ sung được tệp (null nếu được).</summary>
    public static string? AddBlockReason(EvidenceStage stage, RecordStatus recordStatus, PeriodStatus periodStatus) =>
        CanAdd(stage) ? null : LockedReason(recordStatus, periodStatus);

    /// <summary>Lý do không thay / gỡ được tệp (null nếu được).</summary>
    public static string? ChangeBlockReason(EvidenceStage stage, RecordStatus recordStatus, PeriodStatus periodStatus) => stage switch
    {
        EvidenceStage.Open => null,
        EvidenceStage.AddOnly =>
            "Phiếu tự chấm đã nộp: tệp minh chứng đã nộp không thay hoặc gỡ được, chỉ được bổ sung tệp mới. "
            + "Nếu hồ sơ được trả lại để tự chấm lại thì thay, gỡ tệp được như trước khi nộp.",
        _ => LockedReason(recordStatus, periodStatus)
    };

    private static string LockedReason(RecordStatus recordStatus, PeriodStatus periodStatus)
    {
        if (recordStatus == RecordStatus.Published)
            return "Hồ sơ đã công bố: minh chứng của hồ sơ đã khóa. Nếu cần bổ sung minh chứng, hãy gửi kiến nghị kèm tệp.";
        return periodStatus switch
        {
            PeriodStatus.Draft => "Kỳ đánh giá chưa mở: chưa đính kèm được minh chứng.",
            PeriodStatus.Locked => "Kỳ đánh giá đã khóa dữ liệu: minh chứng của hồ sơ đã khóa, không thêm, thay hoặc gỡ được.",
            _ => "Kỳ đánh giá đã đóng: minh chứng của hồ sơ đã khóa, không thêm, thay hoặc gỡ được."
        };
    }
}
