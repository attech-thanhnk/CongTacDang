using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Mã bước của luồng đánh giá (docs/thiet-ke/luong-danh-gia.md mục 1). Mã cố định trong code; bước nào bật/tắt,
/// ai làm, thời hạn là cấu hình của kỳ (<see cref="PeriodSettings"/>).
/// </summary>
public enum WorkflowStep
{
    /// <summary>Cá nhân đăng ký sản phẩm/nhiệm vụ (Mẫu 01).</summary>
    B1_REGISTER = 1,

    /// <summary>Duyệt / trả lại danh mục sản phẩm (Mẫu 01).</summary>
    B1_APPROVE = 2,

    /// <summary>Tự chấm điểm, đề xuất mức (Mẫu 02, 09A/09B, 09C, 9D) — luôn bật.</summary>
    B2_SELF_SCORE = 3,

    /// <summary>Chi bộ xác nhận / trả lại phiếu tự chấm.</summary>
    B2_CELL_CONFIRM = 4,

    /// <summary>Ghi nhận đề xuất của tập thể lãnh đạo (kết quả phiếu kín, Mẫu 11–13).</summary>
    B3A_COLLECTIVE = 5,

    /// <summary>Thẩm định, đề xuất mức (Mẫu 10, 03, 19, 20) — luôn bật.</summary>
    B3B_APPRAISAL = 6,

    /// <summary>Nhận xét, đề xuất của cấp trực tiếp sử dụng (Mẫu 10).</summary>
    B3C_DIRECTOR = 7,

    /// <summary>Ghi nhận quyết định mức xếp loại (Mẫu 12–15) — luôn bật.</summary>
    B4_DECISION = 8,

    /// <summary>Công bố, khóa hồ sơ (Mẫu 16) — luôn bật.</summary>
    B5_PUBLISH = 9
}

/// <summary>Loại hành động ghi vào lịch sử hồ sơ.</summary>
public enum WorkflowAction
{
    /// <summary>Hoàn thành bước → chuyển sang bước bật kế tiếp.</summary>
    Complete = 1,

    /// <summary>Trả lại về bước của chủ hồ sơ (bắt buộc lý do).</summary>
    Return = 2,

    /// <summary>Mở lại hồ sơ đã công bố (bắt buộc lý do).</summary>
    Reopen = 3,

    /// <summary>Thêm người vào danh sách được đánh giá (tạo hồ sơ).</summary>
    Create = 4,

    /// <summary>Sửa ảnh chụp Phòng/Chi bộ/khung chức danh/cấp quyết định (bắt buộc lý do).</summary>
    EditSnapshot = 5,

    /// <summary>Cấu hình kỳ đổi khi kỳ còn dự thảo → trạng thái đầu của hồ sơ được tính lại.</summary>
    Recalculate = 6
}

/// <summary>Thông tin tĩnh về các bước: thứ tự, trạng thái chờ tương ứng, bước bắt buộc, bước được trả lại.</summary>
public static class WorkflowSteps
{
    /// <summary>Thứ tự cố định của các bước.</summary>
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

    /// <summary>Các bước luôn bật (không tắt được trong cấu hình kỳ).</summary>
    public static readonly IReadOnlySet<WorkflowStep> Mandatory = new HashSet<WorkflowStep>
    {
        WorkflowStep.B2_SELF_SCORE,
        WorkflowStep.B3B_APPRAISAL,
        WorkflowStep.B4_DECISION,
        WorkflowStep.B5_PUBLISH
    };

    /// <summary>Các bước do chủ hồ sơ thực hiện.</summary>
    public static readonly IReadOnlySet<WorkflowStep> OwnerSteps = new HashSet<WorkflowStep>
    {
        WorkflowStep.B1_REGISTER,
        WorkflowStep.B2_SELF_SCORE
    };

    /// <summary>
    /// Bước được trả lại và bước của chủ hồ sơ mà hồ sơ quay về (thiết kế mục 2).
    /// </summary>
    public static readonly IReadOnlyDictionary<WorkflowStep, WorkflowStep> ReturnTargets = new Dictionary<WorkflowStep, WorkflowStep>
    {
        [WorkflowStep.B1_APPROVE] = WorkflowStep.B1_REGISTER,
        [WorkflowStep.B2_CELL_CONFIRM] = WorkflowStep.B2_SELF_SCORE,
        [WorkflowStep.B3B_APPRAISAL] = WorkflowStep.B2_SELF_SCORE
    };

    /// <summary>Bước sớm nhất được chọn khi mở lại hồ sơ đã công bố.</summary>
    public const WorkflowStep EarliestReopenStep = WorkflowStep.B2_SELF_SCORE;

    /// <summary>Bước đầu tiên được thao tác khi kỳ ở trạng thái "Khóa dữ liệu" (HD03 II.2).</summary>
    public const WorkflowStep FirstStepWhenLocked = WorkflowStep.B3B_APPRAISAL;

    /// <summary>Mã bước dạng chuỗi (khóa trong cấu hình JSON, giá trị trả về API).</summary>
    public static string Code(WorkflowStep step) => step.ToString();

    /// <summary>Đọc mã bước; null nếu không hợp lệ.</summary>
    public static WorkflowStep? Parse(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        return Enum.TryParse<WorkflowStep>(code.Trim(), ignoreCase: true, out var step) && Enum.IsDefined(step) ? step : null;
    }

    /// <summary>Trạng thái hồ sơ khi đang chờ bước <paramref name="step"/>.</summary>
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

    /// <summary>Bước đang chờ của trạng thái; null với <see cref="RecordStatus.Published"/>.</summary>
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

    /// <summary>Vị trí của bước trong thứ tự (0-based).</summary>
    public static int IndexOf(WorkflowStep step) => Ordered.ToList().IndexOf(step);

    /// <summary>Tên bước hiển thị.</summary>
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

    /// <summary>Tên trạng thái hồ sơ hiển thị.</summary>
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

    /// <summary>Tên trạng thái kỳ hiển thị.</summary>
    public static string PeriodStatusDisplayName(PeriodStatus status) => status switch
    {
        PeriodStatus.Draft => "Dự thảo",
        PeriodStatus.Open => "Đang mở",
        PeriodStatus.Locked => "Khóa dữ liệu",
        PeriodStatus.Closed => "Đã đóng",
        _ => status.ToString()
    };

    /// <summary>Tên hành động lịch sử hiển thị.</summary>
    public static string ActionDisplayName(WorkflowAction action) => action switch
    {
        WorkflowAction.Complete => "Hoàn thành",
        WorkflowAction.Return => "Trả lại",
        WorkflowAction.Reopen => "Mở lại",
        WorkflowAction.Create => "Thêm vào danh sách",
        WorkflowAction.EditSnapshot => "Sửa thông tin ảnh chụp",
        WorkflowAction.Recalculate => "Tính lại theo cấu hình kỳ",
        _ => action.ToString()
    };
}
