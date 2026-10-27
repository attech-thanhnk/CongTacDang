using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Nội dung mười mục kiểm soát và luật kết luận, hiệu lực của phiếu Mẫu 19.</summary>
public static class ControlCheckRules
{
    /// <summary>Tăng khi đổi chữ của mười mục; phiếu cũ giữ nguyên văn đã chụp.</summary>
    public const int ItemsVersion = 1;

    public const int MaxTextLength = 4000;

    public const int MaxGroupNameLength = 500;

    public static readonly IReadOnlyList<(string Code, string Text)> Items = new[]
    {
        ("K01", "Nhiệm vụ, chỉ tiêu, trọng số được giao/xác nhận đúng thẩm quyền; thay đổi trong kỳ có căn cứ và không hồi tố bất lợi."),
        ("K02", "Tiêu chí áp dụng phù hợp chức danh; cùng nhóm công việc được đánh giá theo cùng chuẩn và điều kiện tương đương."),
        ("K03", "Mỗi điểm số, nhận xét, đề xuất xếp loại có minh chứng hợp lệ, phát sinh trong kỳ và có thể truy xuất."),
        ("K04", "Đã loại trừ việc tính trùng kết quả; tiêu chí K/AD được giải thích và xử lý trọng số đúng quy định."),
        ("K05", "Đã xem xét điều kiện nguồn lực, yếu tố khách quan, nhiệm vụ phát sinh và mức độ chủ động ứng phó của cá nhân."),
        ("K06", "Không có thông tin chưa kiểm chứng, định kiến, quan hệ thân quen hoặc xung đột lợi ích chi phối kết quả."),
        ("K07", "Đối với vị trí an toàn trọng yếu, việc báo cáo trung thực sự cố/nguy cơ không bị sử dụng làm chỉ báo bất lợi."),
        ("K08", "Chênh lệch bất thường giữa tự đánh giá, người quản lý, tập thể và dữ liệu khách quan đã được giải trình."),
        ("K09", "Cá nhân đã được phản hồi, giải trình; ý kiến bảo lưu hoặc đề nghị xem xét lại đã được ghi nhận và xử lý."),
        ("K10", "Kết quả cuối cùng, kế hoạch phát triển và hồ sơ lưu trữ đã được phê duyệt/hoàn thiện đúng thẩm quyền.")
    };

    public static bool IsKnownItem(string? code) => Items.Any(i => i.Code == code);

    public static string StatusName(ControlCheckStatus status) => status switch
    {
        ControlCheckStatus.Draft => "Đang soạn",
        ControlCheckStatus.Submitted => "Chờ xác nhận",
        ControlCheckStatus.Confirmed => "Đã xác nhận",
        ControlCheckStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static string ConclusionName(ControlConclusion conclusion) => conclusion switch
    {
        ControlConclusion.Eligible => "Đủ điều kiện trình phê duyệt",
        ControlConclusion.NeedsWork => "Cần hoàn thiện trước khi trình",
        _ => conclusion.ToString()
    };

    public static string ResultName(ControlItemResult result) => result switch
    {
        ControlItemResult.Pass => "Đạt",
        ControlItemResult.Fail => "Chưa đạt",
        ControlItemResult.NotApplicable => "K/AD",
        _ => result.ToString()
    };

    /// <summary>Kiểm tra phiếu trước khi gửi xác nhận; null nếu hợp lệ.</summary>
    public static string? ValidateForSubmit(
        IReadOnlyCollection<(string Code, ControlItemResult? Result, string? Evidence)> items,
        ControlConclusion? conclusion,
        string? needsWorkContent)
    {
        foreach (var (code, text) in Items)
        {
            var item = items.FirstOrDefault(i => i.Code == code);
            if (item.Code == null || item.Result == null)
                return $"Hãy chọn kết quả cho mục {code}.";
            if (item.Result == ControlItemResult.Fail && string.IsNullOrWhiteSpace(item.Evidence))
                return $"Mục {code} chưa đạt: hãy ghi minh chứng/biện pháp.";
            if (item.Result == ControlItemResult.NotApplicable && string.IsNullOrWhiteSpace(item.Evidence))
                return $"Mục {code} không áp dụng: hãy ghi lý do.";
        }

        if (conclusion == null)
            return "Hãy chọn kết luận kiểm tra.";
        if (conclusion == ControlConclusion.Eligible && items.Any(i => i.Result == ControlItemResult.Fail))
            return "Còn mục chưa đạt nên không thể kết luận đủ điều kiện trình phê duyệt.";
        if (conclusion == ControlConclusion.NeedsWork && string.IsNullOrWhiteSpace(needsWorkContent))
            return "Hãy ghi nội dung cần hoàn thiện.";
        return null;
    }

    /// <summary>Thao tác làm đổi kết quả hồ sơ hoặc bước của hồ sơ nên phiếu đã xác nhận hết hiệu lực.</summary>
    public static bool InvalidatesCheck(EvaluationRecordHistory history)
    {
        ArgumentNullException.ThrowIfNull(history);
        if (GateHistory.IsDecisionOrLater(history))
            return false;
        switch (history.Action)
        {
            case WorkflowAction.Complete:
            case WorkflowAction.Return:
            case WorkflowAction.Reopen:
            case WorkflowAction.RecordExternal:
            case WorkflowAction.ChangeProfile:
            case WorkflowAction.EditSnapshot:
            case WorkflowAction.RevokeTaskApproval:
                return true;
            default:
                return history.ScoreBefore != history.ScoreAfter || history.GradeBefore != history.GradeAfter;
        }
    }

    /// <summary>Phiếu đã xác nhận hết hiệu lực nếu sau lúc xác nhận lịch sử hồ sơ có thao tác làm đổi kết quả.</summary>
    public static bool IsStale(DateTime confirmedAt, IEnumerable<EvaluationRecordHistory> histories) =>
        histories.Any(h => h.CreatedAt > confirmedAt && InvalidatesCheck(h));
}
