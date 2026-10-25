using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

public sealed record WorkflowActionDefinition(string Code, WorkflowStep Step, WorkflowAction Kind, string Label)
{
    /// <summary>Hoàn thành trong hệ thống hoặc ghi nhận kết quả của cấp trên.</summary>
    public bool Advances => Kind is WorkflowAction.Complete or WorkflowAction.RecordExternal;
}

public static class WorkflowActions
{
    public const string SubmitTasks = "SubmitTasks";
    public const string ApproveTasks = "ApproveTasks";
    public const string ReturnTasks = "ReturnTasks";
    public const string RevokeTaskApproval = "RevokeTaskApproval";
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

    /// <summary>Một mã cho mọi bước; bước nằm ở trường <c>step</c>.</summary>
    public const string RecordExternal = "RecordExternal";

    /// <summary>Theo thứ tự bước.</summary>
    public static readonly IReadOnlyList<WorkflowActionDefinition> All = new[]
    {
        new WorkflowActionDefinition(SubmitTasks, WorkflowStep.B1_REGISTER, WorkflowAction.Complete, "Nộp danh mục sản phẩm"),
        new WorkflowActionDefinition(ApproveTasks, WorkflowStep.B1_APPROVE, WorkflowAction.Complete, "Duyệt danh mục"),
        new WorkflowActionDefinition(ReturnTasks, WorkflowStep.B1_APPROVE, WorkflowAction.Return, "Trả lại danh mục"),
        new WorkflowActionDefinition(RevokeTaskApproval, WorkflowStep.B1_APPROVE, WorkflowAction.RevokeTaskApproval, "Thu hồi duyệt danh mục"),
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

    public static WorkflowActionDefinition Get(string code) =>
        All.FirstOrDefault(a => a.Code == code) ?? throw new ArgumentOutOfRangeException(nameof(code), code, null);

    public static WorkflowActionDefinition CompleteOf(WorkflowStep step) =>
        All.First(a => a.Step == step && a.Kind == WorkflowAction.Complete);

    /// <summary>Null nếu bước không trả lại được.</summary>
    public static WorkflowActionDefinition? ReturnOf(WorkflowStep step) =>
        All.FirstOrDefault(a => a.Step == step && a.Kind == WorkflowAction.Return);

    public static WorkflowActionDefinition ExternalOf(WorkflowStep step) =>
        new(RecordExternal, step, WorkflowAction.RecordExternal, "Ghi nhận kết quả của cấp trên");

    /// <summary>Theo chế độ của bước trong hồ sơ luồng; null khi bước không áp dụng.</summary>
    public static WorkflowActionDefinition? AdvanceOf(WorkflowStep step, WorkflowProfile profile) => profile.Mode(step) switch
    {
        StepMode.Internal => CompleteOf(step),
        StepMode.External => ExternalOf(step),
        _ => null
    };

    /// <summary>
    /// Mở lại → <c>evaluation.reopen</c>; ghi nhận cấp trên → <c>evaluation.external.record</c>; bước của chủ hồ sơ →
    /// <c>evaluation.self</c>; bước nội bộ → quyền cấu hình trong hồ sơ luồng.
    /// </summary>
    public static string PermissionFor(WorkflowActionDefinition action, WorkflowProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (action.Kind == WorkflowAction.Reopen)
            return PermissionCodes.EvaluationReopen;
        if (action.Kind == WorkflowAction.RecordExternal)
            return PermissionCodes.EvaluationExternalRecord;
        // Thu hồi duyệt danh mục dùng chung quyền bước duyệt theo hồ sơ luồng (nhánh cuối).
        if (WorkflowSteps.OwnerSteps.Contains(action.Step))
            return PermissionCodes.EvaluationSelf;
        var setting = profile.Step(action.Step);
        return string.IsNullOrWhiteSpace(setting.Permission) ? WorkflowPermissions.DefaultFor(action.Step) : setting.Permission;
    }

    /// <summary>Bước nội bộ chỉ nhận hành động trong hệ thống, bước cấp trên chỉ nhận ghi nhận kết quả; null nếu khớp.</summary>
    public static string? ModeBlockReason(WorkflowActionDefinition action, WorkflowProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (action.Kind == WorkflowAction.Reopen)
            return null;
        if (action.Kind == WorkflowAction.RevokeTaskApproval)
            return null;

        var name = WorkflowSteps.DisplayName(action.Step);
        return (profile.Mode(action.Step), action.Kind) switch
        {
            (StepMode.Off, _) =>
                $"Bước \"{name}\" không áp dụng cho nhóm đối tượng của hồ sơ này (cách xét duyệt \"{profile.Name}\").",
            (StepMode.External, not WorkflowAction.RecordExternal) =>
                $"Bước \"{name}\" do cấp trên thực hiện đối với cách xét duyệt \"{profile.Name}\": hãy dùng chức năng \"Ghi nhận kết quả của cấp trên\".",
            (StepMode.Internal, WorkflowAction.RecordExternal) =>
                $"Bước \"{name}\" được thực hiện trong hệ thống đối với cách xét duyệt \"{profile.Name}\", không ghi nhận kết quả của cấp trên được.",
            _ => null
        };
    }

    /// <summary>
    /// Dự thảo: chưa ai thao tác; Đang mở: mọi bước; Khóa dữ liệu: từ thẩm định trở đi và mở lại; Đã đóng: chỉ mở lại.
    /// </summary>
    public static string? PeriodBlockReason(PeriodStatus status, WorkflowActionDefinition action) =>
        PeriodBlockReason(status, action, null, DateTime.UtcNow);

    /// <summary>Hồ sơ được mở riêng còn hạn khi kỳ khóa dữ liệu → không bị chặn (trừ thu hồi duyệt danh mục).</summary>
    public static string? PeriodBlockReason(PeriodStatus status, WorkflowActionDefinition action, DateTime? unlockedUntil, DateTime utcNow)
    {
        if (status == PeriodStatus.Locked && action.Kind != WorkflowAction.RevokeTaskApproval
            && action.Kind != WorkflowAction.Reopen && WorkflowSteps.IsLockedStep(action.Step) && unlockedUntil is { } until)
        {
            if (until > utcNow)
                return null;
            return $"Thời hạn mở riêng hồ sơ đã hết ({FormatLocal(until)}). Liên hệ người quản lý kỳ để gia hạn.";
        }
        return BaseBlockReason(status, action);
    }

    /// <summary>Số ngày mở riêng hồ sơ gợi ý trên giao diện.</summary>
    public const int DefaultUnlockDays = 7;

    /// <summary>Tính từ lúc mở / gia hạn.</summary>
    public const int MaxUnlockDays = 60;

    /// <summary>Không thuộc bước nào của luồng.</summary>
    public const string ExtendUnlock = "ExtendUnlock";

    /// <summary>Theo giờ Việt Nam (dd/MM/yyyy HH:mm).</summary>
    public static string FormatLocal(DateTime utc) =>
        (utc + EvaluationMapping.BusinessUtcOffset).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private static string? BaseBlockReason(PeriodStatus status, WorkflowActionDefinition action)
    {
        switch (status)
        {
            case PeriodStatus.Open:
                return null;
            case PeriodStatus.Draft:
                return "Kỳ đánh giá đang ở trạng thái dự thảo, chưa mở. Hãy chờ người quản lý kỳ mở kỳ.";
            case PeriodStatus.Locked:
                if (action.Kind == WorkflowAction.RevokeTaskApproval)
                    return "Kỳ đánh giá đã khóa dữ liệu: không thể thu hồi duyệt danh mục.";
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

    /// <summary>Không gồm quyền của bước chủ hồ sơ.</summary>
    public static bool IsAssignableStepPermission(string code) =>
        PermissionCodes.Find(code) is { Module: "evaluation" } definition
        && definition.Code is not (PermissionCodes.EvaluationSelf or PermissionCodes.EvaluationRead or PermissionCodes.EvaluationReopen)
        && !PermissionCodes.FormPermissions.Contains(definition.Code);

    /// <summary>Theo thứ tự danh mục.</summary>
    public static IReadOnlyList<PermissionDefinition> AssignableStepPermissions() =>
        PermissionCodes.Definitions.Where(d => IsAssignableStepPermission(d.Code)).ToList();
}
