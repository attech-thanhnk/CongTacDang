using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Lệnh chuyển trạng thái đưa vào máy trạng thái.</summary>
/// <param name="Action">Hoàn thành / trả lại / mở lại.</param>
/// <param name="Step">Bước thực hiện (với mở lại: <see cref="WorkflowStep.B5_PUBLISH"/> hoặc bất kỳ — không dùng).</param>
/// <param name="ReopenTarget">Bước mà hồ sơ quay về khi mở lại.</param>
public sealed record WorkflowCommand(WorkflowAction Action, WorkflowStep Step, WorkflowStep? ReopenTarget = null)
{
    /// <summary>Hoàn thành bước.</summary>
    public static WorkflowCommand Complete(WorkflowStep step) => new(WorkflowAction.Complete, step);

    /// <summary>Trả lại tại bước.</summary>
    public static WorkflowCommand Return(WorkflowStep step) => new(WorkflowAction.Return, step);

    /// <summary>Mở lại hồ sơ đã công bố, quay về bước <paramref name="target"/>.</summary>
    public static WorkflowCommand Reopen(WorkflowStep target) => new(WorkflowAction.Reopen, WorkflowStep.B5_PUBLISH, target);
}

/// <summary>Kết quả chuyển trạng thái: trạng thái mới hoặc thông báo lỗi (tiếng Việt).</summary>
public sealed record TransitionResult(bool Succeeded, RecordStatus Status, string? Error)
{
    /// <summary>Thành công.</summary>
    public static TransitionResult Ok(RecordStatus status) => new(true, status, null);

    /// <summary>Không hợp lệ.</summary>
    public static TransitionResult Fail(RecordStatus current, string error) => new(false, current, error);
}

/// <summary>
/// Máy trạng thái thuần của hồ sơ đánh giá cá nhân (docs/thiet-ke/luong-danh-gia.md mục 2):
/// input (trạng thái, các bước bật trong kỳ, hành động) → trạng thái mới hoặc lỗi. Không đọc CSDL, không kiểm tra quyền.
/// </summary>
public static class RecordStateMachine
{
    /// <summary>Trạng thái đầu của hồ sơ mới theo cấu hình bước của kỳ.</summary>
    public static RecordStatus Initial(IReadOnlySet<WorkflowStep> enabledSteps)
    {
        ArgumentNullException.ThrowIfNull(enabledSteps);
        var first = WorkflowSteps.Ordered.FirstOrDefault(enabledSteps.Contains);
        // Bước tự chấm luôn bật nên luôn có bước đầu; phòng cấu hình lỗi thì bắt đầu từ tự chấm.
        return enabledSteps.Contains(first) ? WorkflowSteps.StatusOf(first) : RecordStatus.AwaitingSelfScore;
    }

    /// <summary>Trạng thái sau khi hoàn thành <paramref name="step"/>: bước bật kế tiếp, hoặc Đã công bố sau bước cuối.</summary>
    public static RecordStatus NextAfter(WorkflowStep step, IReadOnlySet<WorkflowStep> enabledSteps)
    {
        ArgumentNullException.ThrowIfNull(enabledSteps);
        var index = WorkflowSteps.IndexOf(step);
        foreach (var next in WorkflowSteps.Ordered.Skip(index + 1))
        {
            if (enabledSteps.Contains(next))
                return WorkflowSteps.StatusOf(next);
        }
        return RecordStatus.Published;
    }

    /// <summary>Các bước được chọn khi mở lại hồ sơ đã công bố (bước bật, không sớm hơn tự chấm).</summary>
    public static IReadOnlyList<WorkflowStep> ReopenTargets(IReadOnlySet<WorkflowStep> enabledSteps)
    {
        ArgumentNullException.ThrowIfNull(enabledSteps);
        var earliest = WorkflowSteps.IndexOf(WorkflowSteps.EarliestReopenStep);
        return WorkflowSteps.Ordered.Where(s => enabledSteps.Contains(s) && WorkflowSteps.IndexOf(s) >= earliest).ToList();
    }

    /// <summary>Áp lệnh lên trạng thái hiện tại.</summary>
    public static TransitionResult Apply(RecordStatus current, IReadOnlySet<WorkflowStep> enabledSteps, WorkflowCommand command)
    {
        ArgumentNullException.ThrowIfNull(enabledSteps);
        ArgumentNullException.ThrowIfNull(command);
        var where = WorkflowSteps.StatusDisplayName(current);

        switch (command.Action)
        {
            case WorkflowAction.Complete:
            {
                var step = command.Step;
                var name = WorkflowSteps.DisplayName(step);
                if (!enabledSteps.Contains(step))
                    return TransitionResult.Fail(current, $"Bước \"{name}\" không áp dụng trong kỳ này.");
                if (current != WorkflowSteps.StatusOf(step))
                    return TransitionResult.Fail(current, $"Hồ sơ đang ở bước \"{where}\", không thể thực hiện \"{name}\".");
                return TransitionResult.Ok(NextAfter(step, enabledSteps));
            }
            case WorkflowAction.Return:
            {
                var step = command.Step;
                var name = WorkflowSteps.DisplayName(step);
                if (!WorkflowSteps.ReturnTargets.TryGetValue(step, out var target))
                    return TransitionResult.Fail(current, $"Không thể trả lại hồ sơ ở bước \"{name}\".");
                if (!enabledSteps.Contains(step))
                    return TransitionResult.Fail(current, $"Bước \"{name}\" không áp dụng trong kỳ này.");
                if (current != WorkflowSteps.StatusOf(step))
                    return TransitionResult.Fail(current, $"Hồ sơ đang ở bước \"{where}\", không thể trả lại tại bước \"{name}\".");
                if (!enabledSteps.Contains(target))
                    return TransitionResult.Fail(current, $"Bước \"{WorkflowSteps.DisplayName(target)}\" không áp dụng trong kỳ này nên không thể trả lại.");
                return TransitionResult.Ok(WorkflowSteps.StatusOf(target));
            }
            case WorkflowAction.Reopen:
            {
                if (current != RecordStatus.Published)
                    return TransitionResult.Fail(current, $"Hồ sơ đang ở bước \"{where}\", chỉ mở lại được hồ sơ đã công bố.");
                if (command.ReopenTarget is not { } target)
                    return TransitionResult.Fail(current, "Hãy chọn bước mà hồ sơ quay về khi mở lại.");
                if (!ReopenTargets(enabledSteps).Contains(target))
                {
                    return TransitionResult.Fail(current,
                        $"Không thể mở lại về bước \"{WorkflowSteps.DisplayName(target)}\": chỉ chọn bước đang áp dụng trong kỳ, "
                        + $"không sớm hơn \"{WorkflowSteps.DisplayName(WorkflowSteps.EarliestReopenStep)}\".");
                }
                return TransitionResult.Ok(WorkflowSteps.StatusOf(target));
            }
            default:
                return TransitionResult.Fail(current, "Hành động không được hỗ trợ.");
        }
    }
}
