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
/// <param name="Kind">Hoàn thành / trả lại / mở lại / ghi nhận kết quả của cấp trên.</param>
/// <param name="Label">Nhãn nút.</param>
public sealed record WorkflowActionDefinition(string Code, WorkflowStep Step, WorkflowAction Kind, string Label)
{
    /// <summary>Hành động làm bước tiến lên (hoàn thành trong hệ thống hoặc ghi nhận kết quả của cấp trên).</summary>
    public bool Advances => Kind is WorkflowAction.Complete or WorkflowAction.RecordExternal;
}

/// <summary>
/// Danh mục hành động và luật cố định (quyền theo bước của hồ sơ luồng, luật trạng thái kỳ, chế độ bước).
/// </summary>
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

    /// <summary>Ghi nhận kết quả của bước do cấp trên thực hiện (một mã cho mọi bước; bước nằm ở trường <c>step</c>).</summary>
    public const string RecordExternal = "RecordExternal";

    /// <summary>Hành động trong hệ thống theo thứ tự bước.</summary>
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

    /// <summary>Tra định nghĩa hành động trong hệ thống theo mã.</summary>
    public static WorkflowActionDefinition Get(string code) =>
        All.FirstOrDefault(a => a.Code == code) ?? throw new ArgumentOutOfRangeException(nameof(code), code, null);

    /// <summary>Hành động hoàn thành (trong hệ thống) của một bước.</summary>
    public static WorkflowActionDefinition CompleteOf(WorkflowStep step) =>
        All.First(a => a.Step == step && a.Kind == WorkflowAction.Complete);

    /// <summary>Hành động trả lại của một bước (null nếu bước không trả lại được).</summary>
    public static WorkflowActionDefinition? ReturnOf(WorkflowStep step) =>
        All.FirstOrDefault(a => a.Step == step && a.Kind == WorkflowAction.Return);

    /// <summary>Hành động ghi nhận kết quả của cấp trên cho một bước.</summary>
    public static WorkflowActionDefinition ExternalOf(WorkflowStep step) =>
        new(RecordExternal, step, WorkflowAction.RecordExternal, "Ghi nhận kết quả của cấp trên");

    /// <summary>Hành động làm bước tiến lên theo chế độ của bước trong hồ sơ luồng (null khi bước không áp dụng).</summary>
    public static WorkflowActionDefinition? AdvanceOf(WorkflowStep step, WorkflowProfile profile) => profile.Mode(step) switch
    {
        StepMode.Internal => CompleteOf(step),
        StepMode.External => ExternalOf(step),
        _ => null
    };

    /// <summary>
    /// Mã quyền cần có để thực hiện hành động trên hồ sơ dùng hồ sơ luồng <paramref name="profile"/>: mở lại →
    /// <c>evaluation.reopen</c>; ghi nhận kết quả của cấp trên → <c>evaluation.external.record</c>; bước của chủ hồ sơ →
    /// <c>evaluation.self</c>; bước nội bộ → quyền thực hiện cấu hình trong hồ sơ luồng.
    /// </summary>
    public static string PermissionFor(WorkflowActionDefinition action, WorkflowProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (action.Kind == WorkflowAction.Reopen)
            return PermissionCodes.EvaluationReopen;
        if (action.Kind == WorkflowAction.RecordExternal)
            return PermissionCodes.EvaluationExternalRecord;
        if (WorkflowSteps.OwnerSteps.Contains(action.Step))
            return PermissionCodes.EvaluationSelf;
        var setting = profile.Step(action.Step);
        return string.IsNullOrWhiteSpace(setting.Permission) ? WorkflowPermissions.DefaultFor(action.Step) : setting.Permission;
    }

    /// <summary>
    /// Hành động có khớp chế độ của bước trong hồ sơ luồng không. Trả thông báo lỗi (409) hoặc null nếu khớp:
    /// bước nội bộ chỉ nhận hành động trong hệ thống; bước cấp trên thực hiện chỉ nhận ghi nhận kết quả; bước không áp dụng
    /// không nhận hành động nào.
    /// </summary>
    public static string? ModeBlockReason(WorkflowActionDefinition action, WorkflowProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (action.Kind == WorkflowAction.Reopen)
            return null;

        var name = WorkflowSteps.DisplayName(action.Step);
        return (profile.Mode(action.Step), action.Kind) switch
        {
            (StepMode.Off, _) =>
                $"Bước \"{name}\" không áp dụng cho nhóm đối tượng của hồ sơ này (hồ sơ luồng \"{profile.Name}\").",
            (StepMode.External, not WorkflowAction.RecordExternal) =>
                $"Bước \"{name}\" do cấp trên thực hiện đối với hồ sơ luồng \"{profile.Name}\": hãy dùng chức năng \"Ghi nhận kết quả của cấp trên\".",
            (StepMode.Internal, WorkflowAction.RecordExternal) =>
                $"Bước \"{name}\" được thực hiện trong hệ thống đối với hồ sơ luồng \"{profile.Name}\", không ghi nhận kết quả của cấp trên được.",
            _ => null
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

    /// <summary>Mã quyền được chọn làm quyền thực hiện bước nội bộ (không phải bước của chủ hồ sơ).</summary>
    public static bool IsAssignableStepPermission(string code) =>
        PermissionCodes.Find(code) is { Module: "evaluation" } definition
        && definition.Code is not (PermissionCodes.EvaluationSelf or PermissionCodes.EvaluationRead or PermissionCodes.EvaluationReopen);

    /// <summary>Danh sách mã quyền được chọn làm quyền thực hiện bước (theo thứ tự danh mục).</summary>
    public static IReadOnlyList<PermissionDefinition> AssignableStepPermissions() =>
        PermissionCodes.Definitions.Where(d => IsAssignableStepPermission(d.Code)).ToList();
}
