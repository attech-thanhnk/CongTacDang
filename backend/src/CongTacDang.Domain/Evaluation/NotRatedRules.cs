using System;
using System.Collections.Generic;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Hồ sơ "Chưa xếp loại" ngay từ đầu (đào tạo, nghỉ dài ngày, mới bổ nhiệm, đang bị kiểm tra): không đi các bước,
/// coi như đã có quyết định "Chưa xếp loại" và chờ công bố. Luật thuần, không kiểm tra quyền.
/// </summary>
public static class NotRatedRules
{
    public const int MaxReasonLength = 2000;

    /// <summary>Null nếu đánh dấu được.</summary>
    public static string? MarkBlockReason(EvaluationRecord record, PeriodStatus periodStatus)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (periodStatus is not (PeriodStatus.Draft or PeriodStatus.Open))
            return $"Kỳ đang ở trạng thái \"{WorkflowSteps.PeriodStatusDisplayName(periodStatus)}\": chỉ đánh dấu Chưa xếp loại khi kỳ dự thảo hoặc đang mở.";
        if (record.IsMarkedNotRated())
            return "Hồ sơ đã được đánh dấu Chưa xếp loại.";
        if (record.DecisionRecordedAt.HasValue || record.Status is RecordStatus.AwaitingPublish or RecordStatus.Published)
            return "Hồ sơ đã có quyết định mức xếp loại: chỉ đánh dấu Chưa xếp loại trước khi ghi nhận quyết định.";
        return null;
    }

    /// <summary>Null nếu bỏ đánh dấu được.</summary>
    public static string? UnmarkBlockReason(EvaluationRecord record, PeriodStatus periodStatus)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!record.IsMarkedNotRated())
            return "Hồ sơ chưa được đánh dấu Chưa xếp loại.";
        if (record.Status == RecordStatus.Published)
            return "Hồ sơ đã công bố: hãy mở lại hồ sơ nếu cần đính chính.";
        if (periodStatus is not (PeriodStatus.Draft or PeriodStatus.Open))
            return $"Kỳ đang ở trạng thái \"{WorkflowSteps.PeriodStatusDisplayName(periodStatus)}\": chỉ bỏ đánh dấu Chưa xếp loại khi kỳ dự thảo hoặc đang mở.";
        return null;
    }

    /// <summary>Ghi lý do, đặt quyết định "Chưa xếp loại" và chuyển hồ sơ sang chờ công bố.</summary>
    public static void Mark(EvaluationRecord record, string reason, Guid? actorId, string? actorName, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.NotRatedFromStatus = record.Status;
        record.NotRatedReason = reason;
        record.NotRatedAt = now;
        record.NotRatedById = actorId;
        record.NotRatedByName = actorName;
        record.FinalGrade = EvaluationGrade.ChuaXepLoai;
        record.FinalScore = 0;
        record.DecisionExplanation = null;
        record.DecisionRecordedAt = now;
        record.DecisionRecordedById = actorId;
        record.DecisionRecordedByName = actorName;
        record.ReturnReason = null;
        record.Status = RecordStatus.AwaitingPublish;
    }

    /// <summary>
    /// Bỏ các trường đã đặt khi đánh dấu; hồ sơ về trạng thái trước khi đánh dấu (khớp lại bước áp dụng), thiếu thì về bước đầu.
    /// </summary>
    public static RecordStatus Unmark(EvaluationRecord record, IReadOnlySet<WorkflowStep> activeSteps)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(activeSteps);
        var initial = RecordStateMachine.Initial(activeSteps);
        var target = record.NotRatedFromStatus is { } from && WorkflowSteps.IsBefore(from, RecordStatus.AwaitingPublish)
            ? RecordStateMachine.Realign(from, activeSteps)
            : initial;
        if (!WorkflowSteps.IsBefore(target, RecordStatus.AwaitingPublish))
            target = initial;

        Clear(record);
        record.Status = target;
        return target;
    }

    /// <summary>Xóa dấu và quyết định "Chưa xếp loại" đi kèm, không đổi trạng thái.</summary>
    public static void Clear(EvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        record.NotRatedReason = null;
        record.NotRatedAt = null;
        record.NotRatedById = null;
        record.NotRatedByName = null;
        record.NotRatedFromStatus = null;
        record.FinalGrade = EvaluationGrade.ChuaXepLoai;
        record.FinalScore = 0;
        record.DecisionRecordedAt = null;
        record.DecisionRecordedById = null;
        record.DecisionRecordedByName = null;
    }
}
