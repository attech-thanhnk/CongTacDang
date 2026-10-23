using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Giai đoạn minh chứng cá nhân (tệp chủ hồ sơ gắn vào hồ sơ / nhiệm vụ của mình).</summary>
public enum EvidenceStage
{
    /// <summary>Trước khi nộp tự chấm hoặc khi bị trả về: thêm, thay, gỡ tự do.</summary>
    Open = 1,

    /// <summary>Đã nộp, chưa công bố: chỉ bổ sung, không thay, không gỡ.</summary>
    AddOnly = 2,

    /// <summary>Đã công bố hoặc kỳ không mở: khóa; bổ sung qua kiến nghị.</summary>
    Locked = 3
}

/// <summary>Thao tác minh chứng còn được làm theo giai đoạn hồ sơ; quyền kiểm tra riêng. Không áp dụng cho văn bản của cấp trên.</summary>
public static class EvidenceRules
{
    /// <summary>Hồ sơ đang được mở riêng thì kỳ khóa dữ liệu không chặn.</summary>
    public static EvidenceStage StageOf(RecordStatus recordStatus, PeriodStatus periodStatus, bool recordUnlocked = false)
    {
        var periodOpen = periodStatus == PeriodStatus.Open || (periodStatus == PeriodStatus.Locked && recordUnlocked);
        if (!periodOpen || recordStatus == RecordStatus.Published)
            return EvidenceStage.Locked;
        return recordStatus is RecordStatus.AwaitingRegistration or RecordStatus.AwaitingTaskApproval or RecordStatus.AwaitingSelfScore
            ? EvidenceStage.Open
            : EvidenceStage.AddOnly;
    }

    public static bool CanAdd(EvidenceStage stage) => stage != EvidenceStage.Locked;

    public static bool CanReplace(EvidenceStage stage) => stage == EvidenceStage.Open;

    public static bool CanRemove(EvidenceStage stage) => stage == EvidenceStage.Open;

    /// <summary>Null nếu được.</summary>
    public static string? AddBlockReason(EvidenceStage stage, RecordStatus recordStatus, PeriodStatus periodStatus) =>
        CanAdd(stage) ? null : LockedReason(recordStatus, periodStatus);

    /// <summary>Null nếu được.</summary>
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
