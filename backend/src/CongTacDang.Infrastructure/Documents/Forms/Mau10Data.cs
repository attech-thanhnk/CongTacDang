using System;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu thẩm định; ô chưa có giá trị lưu (điểm thẩm định từng nhóm khi chỉ có điểm tổng) giữ chữ mặc định.</summary>
public sealed class Mau10Data
{
    public const string TemplateFileName = "Mau_10_PhieuThamDinh.docx";

    /// <summary>Ô điểm tự chấm khi chưa tự chấm (chữ mặc định của template là tên trường).</summary>
    public const string NotScored = "…";

    /// <summary>Ngày thẩm định; chưa thẩm định → giữ dòng chấm.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    [TemplateField("GENERAL_SELF_SCORE")] public string? GeneralSelfScore { get; init; }

    /// <summary>Hồ sơ chỉ có điểm tổng → giữ chữ mặc định.</summary>
    [TemplateField("GENERAL_APPRAISAL_SCORE")] public string? GeneralAppraisalScore { get; init; }

    /// <summary>Điểm thẩm định − điểm tự chấm của nhóm.</summary>
    [TemplateField("GENERAL_DIFF")] public string? GeneralDiff { get; init; }

    [TemplateField("TASKS_SELF_SCORE")] public string? TasksSelfScore { get; init; }

    /// <summary>Hồ sơ chỉ có điểm tổng → giữ chữ mặc định.</summary>
    [TemplateField("TASKS_APPRAISAL_SCORE")] public string? TasksAppraisalScore { get; init; }

    /// <summary>Điểm thẩm định − điểm tự chấm của nhóm.</summary>
    [TemplateField("TASKS_DIFF")] public string? TasksDiff { get; init; }

    [TemplateField("TOTAL_SELF_SCORE")] public string? TotalSelfScore { get; init; }

    /// <summary>Chưa thẩm định → giữ chữ mặc định.</summary>
    [TemplateField("TOTAL_APPRAISAL_SCORE")] public string? TotalAppraisalScore { get; init; }

    /// <summary>Điểm thẩm định − tổng điểm tự chấm.</summary>
    [TemplateField("TOTAL_DIFF")] public string? TotalDiff { get; init; }

    /// <summary>Số sản phẩm theo mức thẩm định, mỗi mức một dòng; chưa thẩm định nhiệm vụ nào → giữ chữ mặc định.</summary>
    [TemplateField("TASK_LEVELS")] public string? TaskLevels { get; init; }

    /// <summary>Nhận xét của cấp trực tiếp sử dụng (B3c); chưa có → giữ chữ mặc định.</summary>
    [TemplateField("SUPERVISOR_COMMENT")] public string? SupervisorComment { get; init; }

    [TemplateField("APPRAISAL_COMMENT")] public string? AppraisalComment { get; init; }

    /// <summary>Căn cứ của người thẩm định khi chênh lệch tự chấm – thẩm định vượt ngưỡng của bộ tiêu chí.</summary>
    [TemplateField("APPRAISAL_EXPLANATION")] public string? AppraisalExplanation { get; init; }

    /// <summary>Giải trình mới nhất của chính cán bộ sau thẩm định; chưa có → giữ dòng chấm.</summary>
    [TemplateField("MEMBER_EXPLANATION")] public string? MemberExplanation { get; init; }

    /// <summary>Ô ☒/☐ theo mức Tổ thẩm định đề xuất; chưa đề xuất → mọi ô ☐.</summary>
    [TemplateField("CHECK_EXCELLENT")] public string? CheckExcellent { get; init; }
    [TemplateField("CHECK_GOOD")] public string? CheckGood { get; init; }
    [TemplateField("CHECK_DONE")] public string? CheckDone { get; init; }
    [TemplateField("CHECK_FAILED")] public string? CheckFailed { get; init; }

    /// <summary>Mục 4 cột Cá nhân: ô Đạt/Không theo phiếu tự chấm (hoặc "Không đề xuất") kèm điều kiện không đạt, mỗi dòng một lý do.</summary>
    [TemplateField("SELF_EXCELLENT")] public string? SelfExcellent { get; init; }

    /// <summary>Mục 4 cột Thẩm định: kết quả sau khi người thẩm định chọn; chưa đủ để kết luận → giữ ô trống.</summary>
    [TemplateField("APPRAISAL_EXCELLENT")] public string? AppraisalExcellent { get; init; }

    /// <summary>Mục 5 cột Cá nhân: "Không vi phạm" khi cán bộ đã cam kết; chưa đánh dấu → dòng chấm.</summary>
    [TemplateField("SELF_CAP")] public string? SelfCap { get; init; }

    /// <summary>Mục 5 cột Thẩm định: "Không vi phạm" hoặc mức khống chế kèm lý do; chưa thẩm định → dòng chấm.</summary>
    [TemplateField("APPRAISAL_CAP")] public string? AppraisalCap { get; init; }

    // Ô ký: bước chưa làm hoặc do cấp trên thực hiện (không có người thực hiện trong hệ thống) → giữ dòng chấm.

    /// <summary>Ô "Người trực tiếp sử dụng": người đã nhận xét, đề xuất ở B3c.</summary>
    [TemplateField("SUPERVISOR_NAME")] public string? SupervisorName { get; init; }

    /// <summary>Ô "Đại diện cơ quan thẩm định": người đã thẩm định.</summary>
    [TemplateField("APPRAISER_NAME")] public string? AppraiserName { get; init; }

    public static Mau10Data From(EvaluationRecord record, string? memberExplanation = null, ExcellenceConditionReport? conditions = null)
    {
        string Box(EvaluationGrade grade) => record.AppraisalProposedGrade == grade ? "☒" : "☐";
        // Chưa tự chấm (vd Chưa xếp loại): điểm tự chấm và chênh lệch để dòng chấm, không in 0.
        var scored = record.SelfScoredAt.HasValue || record.TotalSelfScore > 0;
        string Self(double value) => scored ? CollectiveFormText.Score(value) : NotScored;
        string? Difference(double? appraisal, double self) => scored ? Diff(appraisal, self) : null;
        return new Mau10Data
        {
            DateLine = CollectiveFormText.DateLine(record.AppraisedAt),
            Quarter = record.Period != null ? CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            // Số nguyên "26", số lẻ một chữ số thập phân "26,5".
            GeneralSelfScore = Self(record.GeneralCriteriaScore),
            TasksSelfScore = Self(record.TasksScore),
            GeneralAppraisalScore = Score(record.AppraisalGeneralScore),
            GeneralDiff = Difference(record.AppraisalGeneralScore, record.GeneralCriteriaScore),
            TasksAppraisalScore = Score(record.AppraisalTasksScore),
            TasksDiff = Difference(record.AppraisalTasksScore, record.TasksScore),
            TotalSelfScore = Self(record.TotalSelfScore),
            TotalAppraisalScore = Score(record.AppraisalScore),
            TotalDiff = Difference(record.AppraisalScore, record.TotalSelfScore),
            TaskLevels = TaskLevelText(record),
            SupervisorComment = FormText.OrNull(record.DirectorComment),
            AppraisalComment = FormText.OrNull(record.AppraisalComment),
            AppraisalExplanation = FormText.OrNull(record.AppraisalExplanation),
            MemberExplanation = FormText.OrNull(memberExplanation),
            CheckExcellent = Box(EvaluationGrade.HoanThanhXuatSac),
            CheckGood = Box(EvaluationGrade.HoanThanhTot),
            CheckDone = Box(EvaluationGrade.HoanThanh),
            CheckFailed = Box(EvaluationGrade.KhongHoanThanh),
            SupervisorName = record.DirectorReviewedById != null ? FormText.OrNull(record.DirectorReviewedByName) : null,
            AppraiserName = record.AppraisedById != null ? FormText.OrNull(record.AppraisedByName) : null,
            SelfExcellent = SelfExcellentText(record, conditions),
            AppraisalExcellent = AppraisalExcellentText(record, conditions),
            SelfCap = record.SelfDeclaresNoCap == true ? NoViolation : null,
            AppraisalCap = AppraisalCapText(record)
        };
    }

    public const string NoViolation = "Không vi phạm";

    public const string NotProposed = "Không đề xuất";

    public static string Boxes(bool met) => met ? "☒ Đạt  ☐ Không" : "☐ Đạt  ☒ Không";

    /// <summary>Chỉ xét điều kiện tính được lúc tự chấm; không đề xuất Xuất sắc → "Không đề xuất".</summary>
    public static string? SelfExcellentText(EvaluationRecord record, ExcellenceConditionReport? conditions)
    {
        if (!record.SelfScoredAt.HasValue || conditions == null)
            return null;
        if (!conditions.SelfProposesExcellent)
            return NotProposed;
        var failed = conditions.Self.Where(c => c.State == ConditionState.NotMet).Select(c => c.Line).ToList();
        return string.Join("\n", failed.Prepend(Boxes(failed.Count == 0)));
    }

    /// <summary>Có điều kiện Không → Không; đủ cả sáu → Đạt; còn điều kiện chưa chọn → không đánh dấu.</summary>
    public static string? AppraisalExcellentText(EvaluationRecord record, ExcellenceConditionReport? conditions)
    {
        if (!record.AppraisedAt.HasValue || conditions == null || conditions.FinalOverall == ConditionState.Unknown)
            return null;
        var failed = conditions.Final.Where(c => c.State == ConditionState.NotMet).Select(c => c.Line);
        return string.Join("\n", failed.Prepend(Boxes(conditions.FinalOverall == ConditionState.Met)));
    }

    public static string? AppraisalCapText(EvaluationRecord record)
    {
        if (record.AppraisalGradeCap is { } cap && cap != EvaluationGrade.ChuaXepLoai)
        {
            var reason = FormText.OrNull(record.AppraisalCapReason?.Trim());
            return $"Khống chế tối đa mức {CriteriaSetContent.GradeName(cap)}" + (reason != null ? $": {reason}" : string.Empty);
        }
        return record.AppraisedAt.HasValue ? NoViolation : null;
    }

    private static string? TaskLevelText(EvaluationRecord record)
    {
        var levels = record.Tasks.Where(t => t.AppraisalLevel.HasValue).Select(t => t.AppraisalLevel!.Value).ToList();
        if (levels.Count == 0)
            return null;
        return string.Join("\n", Enumerable.Range(1, 3).Select(level => $"Mức {level}: {levels.Count(l => l == level)} SP"));
    }

    private static string? Score(double? value) => value.HasValue ? CollectiveFormText.Score(value.Value) : null;

    /// <summary>Làm tròn 1 chữ số; chưa có điểm thẩm định → null.</summary>
    private static string? Diff(double? appraisal, double self) =>
        appraisal.HasValue ? CollectiveFormText.Score(Math.Round(appraisal.Value - self, 1)) : null;
}
