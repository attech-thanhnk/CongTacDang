using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Mã bước cố định trong code; chế độ, người làm, thời hạn cấu hình theo hồ sơ luồng (<see cref="WorkflowProfile"/>).</summary>
public enum WorkflowStep
{
    /// <summary>Đăng ký sản phẩm (Mẫu 01).</summary>
    B1_REGISTER = 1,

    /// <summary>Duyệt / trả lại danh mục sản phẩm.</summary>
    B1_APPROVE = 2,

    /// <summary>Tự chấm, đề xuất mức (Mẫu 02, 09A/09B, 09C, 9D); luôn bật.</summary>
    B2_SELF_SCORE = 3,

    B2_CELL_CONFIRM = 4,

    /// <summary>Ghi nhận đề xuất tập thể lãnh đạo (phiếu kín, Mẫu 11–13).</summary>
    B3A_COLLECTIVE = 5,

    /// <summary>Mẫu 10, 03, 19, 20.</summary>
    B3B_APPRAISAL = 6,

    /// <summary>Mẫu 10.</summary>
    B3C_DIRECTOR = 7,

    /// <summary>Ghi nhận quyết định mức (Mẫu 12–15); luôn bật.</summary>
    B4_DECISION = 8,

    /// <summary>Công bố, khóa hồ sơ (Mẫu 16); luôn bật.</summary>
    B5_PUBLISH = 9
}

/// <summary>Hành động ghi vào lịch sử hồ sơ.</summary>
public enum WorkflowAction
{
    /// <summary>Chuyển sang bước áp dụng kế tiếp.</summary>
    Complete = 1,

    /// <summary>Về bước của chủ hồ sơ; bắt buộc lý do.</summary>
    Return = 2,

    /// <summary>Mở lại hồ sơ đã công bố; bắt buộc lý do.</summary>
    Reopen = 3,

    /// <summary>Thêm người vào kỳ (tạo hồ sơ).</summary>
    Create = 4,

    /// <summary>Sửa ảnh chụp Phòng/Chi bộ/khung/cấp quyết định; bắt buộc lý do.</summary>
    EditSnapshot = 5,

    /// <summary>Kỳ dự thảo đổi cấu hình → tính lại trạng thái đầu của hồ sơ.</summary>
    Recalculate = 6,

    /// <summary>Đổi hồ sơ luồng; bắt buộc lý do.</summary>
    ChangeProfile = 7,

    /// <summary>Ghi nhận kết quả bước do cấp trên thực hiện.</summary>
    RecordExternal = 8,

    /// <summary>Duyệt đề nghị điều chỉnh, chuyển tiếp, loại trừ hoặc việc đột xuất (Mẫu 04, 05).</summary>
    TaskChange = 9,

    /// <summary>Kỳ dự thảo: cập nhật ảnh chụp theo hồ sơ cán bộ hiện tại.</summary>
    SyncSnapshot = 10,

    /// <summary>Thu hồi duyệt nhầm danh mục, trước khi chủ hồ sơ tự chấm.</summary>
    RevokeTaskApproval = 11,

    /// <summary>Mở riêng (hoặc gia hạn) hồ sơ khi kỳ khóa dữ liệu.</summary>
    GrantUnlock = 12,

    /// <summary>Hồ sơ đã tới bước được thao tác khi kỳ khóa, hết mở riêng.</summary>
    ClearUnlock = 13
}

public static class WorkflowSteps
{
    public static readonly IReadOnlyList<WorkflowStep> Ordered = new[]
    {
        WorkflowStep.B1_REGISTER,
        WorkflowStep.B1_APPROVE,
        WorkflowStep.B2_SELF_SCORE,
        WorkflowStep.B2_CELL_CONFIRM,
        WorkflowStep.B3A_COLLECTIVE,
        WorkflowStep.B3B_APPRAISAL,
        WorkflowStep.B3C_DIRECTOR,
        WorkflowStep.B4_DECISION,
        WorkflowStep.B5_PUBLISH
    };

    public static readonly IReadOnlySet<WorkflowStep> Mandatory = new HashSet<WorkflowStep>
    {
        WorkflowStep.B2_SELF_SCORE,
        WorkflowStep.B4_DECISION,
        WorkflowStep.B5_PUBLISH
    };

    /// <summary>Bước của chủ hồ sơ và bước công bố.</summary>
    public static readonly IReadOnlySet<WorkflowStep> NeverExternal = new HashSet<WorkflowStep>
    {
        WorkflowStep.B1_REGISTER,
        WorkflowStep.B2_SELF_SCORE,
        WorkflowStep.B5_PUBLISH
    };

    /// <summary>Ghi nhận kết quả cấp trên ở các bước này bắt buộc chọn mức.</summary>
    public static readonly IReadOnlySet<WorkflowStep> GradedSteps = new HashSet<WorkflowStep>
    {
        WorkflowStep.B3A_COLLECTIVE,
        WorkflowStep.B3B_APPRAISAL,
        WorkflowStep.B3C_DIRECTOR,
        WorkflowStep.B4_DECISION
    };

    public static readonly IReadOnlySet<WorkflowStep> OwnerSteps = new HashSet<WorkflowStep>
    {
        WorkflowStep.B1_REGISTER,
        WorkflowStep.B2_SELF_SCORE
    };

    /// <summary>Bước được trả lại → bước của chủ hồ sơ mà hồ sơ quay về.</summary>
    public static readonly IReadOnlyDictionary<WorkflowStep, WorkflowStep> ReturnTargets = new Dictionary<WorkflowStep, WorkflowStep>
    {
        [WorkflowStep.B1_APPROVE] = WorkflowStep.B1_REGISTER,
        [WorkflowStep.B2_CELL_CONFIRM] = WorkflowStep.B2_SELF_SCORE,
        [WorkflowStep.B3B_APPRAISAL] = WorkflowStep.B2_SELF_SCORE
    };

    public const WorkflowStep EarliestReopenStep = WorkflowStep.B2_SELF_SCORE;

    /// <summary>Kỳ "Khóa dữ liệu" chỉ cho thao tác từ bước này trở đi.</summary>
    public const WorkflowStep FirstStepWhenLocked = WorkflowStep.B3B_APPRAISAL;

    /// <summary>Bước bị chặn khi kỳ khóa dữ liệu, trừ khi hồ sơ được mở riêng.</summary>
    public static bool IsLockedStep(WorkflowStep step) => IndexOf(step) < IndexOf(FirstStepWhenLocked);

    /// <summary>Khóa trong cấu hình JSON và giá trị trả về API.</summary>
    public static string Code(WorkflowStep step) => step.ToString();

    /// <summary>Null nếu không hợp lệ.</summary>
    public static WorkflowStep? Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        return Enum.TryParse<WorkflowStep>(code.Trim(), ignoreCase: true, out var step) && Enum.IsDefined(step) ? step : null;
    }

    public static RecordStatus StatusOf(WorkflowStep step) => step switch
    {
        WorkflowStep.B1_REGISTER => RecordStatus.AwaitingRegistration,
        WorkflowStep.B1_APPROVE => RecordStatus.AwaitingTaskApproval,
        WorkflowStep.B2_SELF_SCORE => RecordStatus.AwaitingSelfScore,
        WorkflowStep.B2_CELL_CONFIRM => RecordStatus.AwaitingCellConfirm,
        WorkflowStep.B3A_COLLECTIVE => RecordStatus.AwaitingCollective,
        WorkflowStep.B3B_APPRAISAL => RecordStatus.AwaitingAppraisal,
        WorkflowStep.B3C_DIRECTOR => RecordStatus.AwaitingDirectorReview,
        WorkflowStep.B4_DECISION => RecordStatus.AwaitingDecision,
        WorkflowStep.B5_PUBLISH => RecordStatus.AwaitingPublish,
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
    };

    /// <summary>Null với <see cref="RecordStatus.Published"/>.</summary>
    public static WorkflowStep? StepOf(RecordStatus status) => status switch
    {
        RecordStatus.AwaitingRegistration => WorkflowStep.B1_REGISTER,
        RecordStatus.AwaitingTaskApproval => WorkflowStep.B1_APPROVE,
        RecordStatus.AwaitingSelfScore => WorkflowStep.B2_SELF_SCORE,
        RecordStatus.AwaitingCellConfirm => WorkflowStep.B2_CELL_CONFIRM,
        RecordStatus.AwaitingCollective => WorkflowStep.B3A_COLLECTIVE,
        RecordStatus.AwaitingAppraisal => WorkflowStep.B3B_APPRAISAL,
        RecordStatus.AwaitingDirectorReview => WorkflowStep.B3C_DIRECTOR,
        RecordStatus.AwaitingDecision => WorkflowStep.B4_DECISION,
        RecordStatus.AwaitingPublish => WorkflowStep.B5_PUBLISH,
        _ => null
    };

    public static int IndexOf(WorkflowStep step) => Ordered.ToList().IndexOf(step);

    public static string DisplayName(WorkflowStep step) => step switch
    {
        WorkflowStep.B1_REGISTER => "Đăng ký sản phẩm, nhiệm vụ",
        WorkflowStep.B1_APPROVE => "Duyệt danh mục sản phẩm",
        WorkflowStep.B2_SELF_SCORE => "Tự chấm điểm, đề xuất mức",
        WorkflowStep.B2_CELL_CONFIRM => "Chi bộ xác nhận phiếu tự chấm",
        WorkflowStep.B3A_COLLECTIVE => "Đề xuất của tập thể lãnh đạo",
        WorkflowStep.B3B_APPRAISAL => "Thẩm định",
        WorkflowStep.B3C_DIRECTOR => "Nhận xét của cấp trực tiếp sử dụng",
        WorkflowStep.B4_DECISION => "Quyết định mức xếp loại",
        WorkflowStep.B5_PUBLISH => "Công bố, khóa kết quả",
        _ => step.ToString()
    };

    public static string StatusDisplayName(RecordStatus status) => status switch
    {
        RecordStatus.AwaitingRegistration => "Chờ đăng ký sản phẩm",
        RecordStatus.AwaitingTaskApproval => "Chờ duyệt danh mục",
        RecordStatus.AwaitingSelfScore => "Chờ tự chấm",
        RecordStatus.AwaitingCellConfirm => "Chờ Chi bộ xác nhận",
        RecordStatus.AwaitingCollective => "Chờ ghi nhận đề xuất tập thể",
        RecordStatus.AwaitingAppraisal => "Chờ thẩm định",
        RecordStatus.AwaitingDirectorReview => "Chờ cấp trực tiếp sử dụng",
        RecordStatus.AwaitingDecision => "Chờ quyết định",
        RecordStatus.AwaitingPublish => "Chờ công bố",
        RecordStatus.Published => "Đã công bố",
        _ => status.ToString()
    };

    public static string PeriodStatusDisplayName(PeriodStatus status) => status switch
    {
        PeriodStatus.Draft => "Dự thảo",
        PeriodStatus.Open => "Đang mở",
        PeriodStatus.Locked => "Khóa dữ liệu",
        PeriodStatus.Closed => "Đã đóng",
        _ => status.ToString()
    };

    public static string ActionDisplayName(WorkflowAction action) => action switch
    {
        WorkflowAction.Complete => "Hoàn thành",
        WorkflowAction.Return => "Trả lại",
        WorkflowAction.Reopen => "Mở lại",
        WorkflowAction.Create => "Thêm vào danh sách",
        WorkflowAction.EditSnapshot => "Sửa thông tin ảnh chụp",
        WorkflowAction.Recalculate => "Tính lại theo cấu hình kỳ",
        WorkflowAction.ChangeProfile => "Đổi cách xét duyệt",
        WorkflowAction.RecordExternal => "Ghi nhận kết quả của cấp trên",
        WorkflowAction.TaskChange => "Điều chỉnh việc trong quý",
        WorkflowAction.SyncSnapshot => "Cập nhật theo hồ sơ cán bộ",
        WorkflowAction.RevokeTaskApproval => "Thu hồi duyệt danh mục",
        WorkflowAction.GrantUnlock => "Mở riêng hồ sơ",
        WorkflowAction.ClearUnlock => "Kết thúc mở riêng hồ sơ",
        _ => action.ToString()
    };
}
