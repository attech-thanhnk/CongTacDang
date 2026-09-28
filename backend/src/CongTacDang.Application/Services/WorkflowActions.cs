using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Định nghĩa một hành động của luồng đánh giá.</summary>
/// <param name="Code">Mã hành động (trả về trong API actions).</param>
/// <param name="Step">Bước thực hiện.</param>
/// <param name="Kind">Hoàn thành / trả lại / mở lại.</param>
/// <param name="Label">Nhãn nút.</param>
public sealed record WorkflowActionDefinition(string Code, WorkflowStep Step, WorkflowAction Kind, string Label);

/// <summary>Danh mục hành động và luật cố định (quyền theo bước, luật trạng thái kỳ).</summary>
public static class WorkflowActions
{
    public const string SubmitTasks = "SubmitTasks";
    public const string ApproveTasks = "ApproveTasks";
    public const string ReturnTasks = "ReturnTasks";
    public const string SubmitSelfScore = "SubmitSelfScore";
    public const string ConfirmByCell = "ConfirmByCell";
    public const string ReturnByCell = "ReturnByCell";
    public const string RecordCollectiveProposal = "RecordCollectiveProposal";
    public const string Appraise = "Appraise";
    public const string ReturnByAppraiser = "ReturnByAppraiser";
    public const string DirectorReview = "DirectorReview";
    public const string RecordDecision = "RecordDecision";
    public const string Publish = "Publish";
    public const string Reopen = "Reopen";

    /// <summary>Toàn bộ hành động theo thứ tự bước.</summary>
    public static readonly IReadOnlyList<WorkflowActionDefinition> All = new[]
    {
        new WorkflowActionDefinition(SubmitTasks, WorkflowStep.B1_REGISTER, WorkflowAction.Complete, "Nộp danh mục sản phẩm"),
        new WorkflowActionDefinition(ApproveTasks, WorkflowStep.B1_APPROVE, WorkflowAction.Complete, "Duyệt danh mục"),
        new WorkflowActionDefinition(ReturnTasks, WorkflowStep.B1_APPROVE, WorkflowAction.Return, "Trả lại danh mục"),
        new WorkflowActionDefinition(SubmitSelfScore, WorkflowStep.B2_SELF_SCORE, WorkflowAction.Complete, "Nộp phiếu tự chấm"),
        new WorkflowActionDefinition(ConfirmByCell, WorkflowStep.B2_CELL_CONFIRM, WorkflowAction.Complete, "Chi bộ xác nhận"),
        new WorkflowActionDefinition(ReturnByCell, WorkflowStep.B2_CELL_CONFIRM, WorkflowAction.Return, "Trả lại phiếu tự chấm"),
        new WorkflowActionDefinition(RecordCollectiveProposal, WorkflowStep.B3A_COLLECTIVE, WorkflowAction.Complete, "Ghi nhận đề xuất của tập thể"),
        new WorkflowActionDefinition(Appraise, WorkflowStep.B3B_APPRAISAL, WorkflowAction.Complete, "Thẩm định"),
        new WorkflowActionDefinition(ReturnByAppraiser, WorkflowStep.B3B_APPRAISAL, WorkflowAction.Return, "Trả lại để chủ hồ sơ sửa"),
        new WorkflowActionDefinition(DirectorReview, WorkflowStep.B3C_DIRECTOR, WorkflowAction.Complete, "Nhận xét, đề xuất"),
        new WorkflowActionDefinition(RecordDecision, WorkflowStep.B4_DECISION, WorkflowAction.Complete, "Ghi nhận quyết định"),
        new WorkflowActionDefinition(Publish, WorkflowStep.B5_PUBLISH, WorkflowAction.Complete, "Công bố, khóa kết quả"),
        new WorkflowActionDefinition(Reopen, WorkflowStep.B5_PUBLISH, WorkflowAction.Reopen, "Mở lại hồ sơ")
    };

    /// <summary>Tra định nghĩa theo mã.</summary>
    public static WorkflowActionDefinition Get(string code) =>
        All.FirstOrDefault(a => a.Code == code) ?? throw new ArgumentOutOfRangeException(nameof(code), code, null);

    /// <summary>Hành động hoàn thành của một bước.</summary>
    public static WorkflowActionDefinition CompleteOf(WorkflowStep step) =>
        All.First(a => a.Step == step && a.Kind == WorkflowAction.Complete);

    /// <summary>Hành động trả lại của một bước (null nếu bước không trả lại được).</summary>
    public static WorkflowActionDefinition? ReturnOf(WorkflowStep step) =>
        All.FirstOrDefault(a => a.Step == step && a.Kind == WorkflowAction.Return);

    /// <summary>Mã quyền cần có để thực hiện hành động trên hồ sơ có cấp quyết định <paramref name="authority"/>.</summary>
    public static string PermissionFor(WorkflowActionDefinition action, ApprovalAuthority authority)
    {
        if (action.Kind == WorkflowAction.Reopen)
            return PermissionCodes.EvaluationReopen;

        return action.Step switch
        {
            WorkflowStep.B1_REGISTER or WorkflowStep.B2_SELF_SCORE => PermissionCodes.EvaluationSelf,
            WorkflowStep.B1_APPROVE => PermissionCodes.EvaluationTasksApprove,
            WorkflowStep.B2_CELL_CONFIRM => PermissionCodes.EvaluationCellConfirm,
            WorkflowStep.B3A_COLLECTIVE => PermissionCodes.EvaluationCollectiveRecord,
            WorkflowStep.B3B_APPRAISAL => PermissionCodes.EvaluationAppraise,
            WorkflowStep.B3C_DIRECTOR => PermissionCodes.EvaluationDirectorReview,
            WorkflowStep.B4_DECISION => authority == ApprovalAuthority.CapTren
                ? PermissionCodes.EvaluationDecideExternal
                : PermissionCodes.EvaluationDecide,
            WorkflowStep.B5_PUBLISH => PermissionCodes.EvaluationPublish,
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
    }

    /// <summary>
    /// Luật trạng thái kỳ (thiết kế mục 3.1). Trả thông báo lỗi (409) hoặc null nếu được phép:
    /// Dự thảo — chưa ai thao tác; Đang mở — mọi bước; Khóa dữ liệu — chỉ từ thẩm định trở đi và mở lại;
    /// Đã đóng — chỉ mở lại.
    /// </summary>
    public static string? PeriodBlockReason(PeriodStatus status, WorkflowActionDefinition action)
    {
        switch (status)
        {
            case PeriodStatus.Open:
                return null;
            case PeriodStatus.Draft:
                return "Kỳ đánh giá đang ở trạng thái dự thảo, chưa mở. Hãy chờ người quản lý kỳ mở kỳ.";
            case PeriodStatus.Locked:
                if (action.Kind == WorkflowAction.Reopen
                    || WorkflowSteps.IndexOf(action.Step) >= WorkflowSteps.IndexOf(WorkflowSteps.FirstStepWhenLocked))
                    return null;
                return "Kỳ đánh giá đã khóa dữ liệu: chỉ các bước từ thẩm định trở đi được thao tác, chủ hồ sơ không sửa được. "
                    + "Liên hệ người quản lý kỳ nếu cần mở lại kỳ.";
            case PeriodStatus.Closed:
                return action.Kind == WorkflowAction.Reopen
                    ? null
                    : "Kỳ đánh giá đã đóng: chỉ có thể mở lại hồ sơ để đính chính.";
            default:
                return "Trạng thái kỳ không hợp lệ.";
        }
    }
}
