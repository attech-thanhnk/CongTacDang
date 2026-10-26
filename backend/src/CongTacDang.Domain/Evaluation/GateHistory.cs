using System;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Quy tắc dùng chung cho cổng hiệu chuẩn và kiểm soát khi đọc lịch sử hồ sơ.</summary>
public static class GateHistory
{
    /// <summary>Ghi nhận quyết định hoặc thao tác sau đó của chính hồ sơ: không còn cần cổng nên không làm biên bản/phiếu hết hiệu lực.</summary>
    public static bool IsDecisionOrLater(EvaluationRecordHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        return history.Action is WorkflowAction.Complete or WorkflowAction.RecordExternal
            && history.FromStatus is { } from && from >= RecordStatus.AwaitingDecision;
    }
}
