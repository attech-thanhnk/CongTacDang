using System;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu thẩm định; ô chưa có giá trị lưu (điểm thẩm định từng nhóm khi chỉ có điểm tổng) giữ chữ mặc định.</summary>
public sealed class Mau10Data
{
    public const string TemplateFileName = "Mau_10_PhieuThamDinh.docx";

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

    /// <summary>Nhận xét của cấp trực tiếp sử dụng (B3c); chưa có → giữ chữ mặc định.</summary>
    [TemplateField("SUPERVISOR_COMMENT")] public string? SupervisorComment { get; init; }

    [TemplateField("APPRAISAL_COMMENT")] public string? AppraisalComment { get; init; }

    /// <summary>Giải trình khi chênh lệch tự chấm – thẩm định vượt ngưỡng của bộ tiêu chí.</summary>
    [TemplateField("APPRAISAL_EXPLANATION")] public string? AppraisalExplanation { get; init; }

    /// <summary>Ô ☒/☐ theo mức Tổ thẩm định đề xuất; chưa đề xuất → mọi ô ☐.</summary>
    [TemplateField("CHECK_EXCELLENT")] public string? CheckExcellent { get; init; }
    [TemplateField("CHECK_GOOD")] public string? CheckGood { get; init; }
    [TemplateField("CHECK_DONE")] public string? CheckDone { get; init; }
    [TemplateField("CHECK_FAILED")] public string? CheckFailed { get; init; }

    public static Mau10Data From(EvaluationRecord record)
    {
        string Box(EvaluationGrade grade) => record.AppraisalProposedGrade == grade ? "☒" : "☐";
        return new Mau10Data
        {
            DateLine = CollectiveFormText.DateLine(record.AppraisedAt),
            Quarter = record.Period != null ? CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            // Số nguyên "26", số lẻ một chữ số thập phân "26,5".
            GeneralSelfScore = CollectiveFormText.Score(record.GeneralCriteriaScore),
            TasksSelfScore = CollectiveFormText.Score(record.TasksScore),
            GeneralAppraisalScore = Score(record.AppraisalGeneralScore),
            GeneralDiff = Diff(record.AppraisalGeneralScore, record.GeneralCriteriaScore),
            TasksAppraisalScore = Score(record.AppraisalTasksScore),
            TasksDiff = Diff(record.AppraisalTasksScore, record.TasksScore),
            TotalSelfScore = CollectiveFormText.Score(record.TotalSelfScore),
            TotalAppraisalScore = Score(record.AppraisalScore),
            TotalDiff = Diff(record.AppraisalScore, record.TotalSelfScore),
            SupervisorComment = FormText.OrNull(record.DirectorComment),
            AppraisalComment = FormText.OrNull(record.AppraisalComment),
            AppraisalExplanation = FormText.OrNull(record.AppraisalExplanation),
            CheckExcellent = Box(EvaluationGrade.HoanThanhXuatSac),
            CheckGood = Box(EvaluationGrade.HoanThanhTot),
            CheckDone = Box(EvaluationGrade.HoanThanh),
            CheckFailed = Box(EvaluationGrade.KhongHoanThanh)
        };
    }

    private static string? Score(double? value) => value.HasValue ? CollectiveFormText.Score(value.Value) : null;

    /// <summary>Làm tròn 1 chữ số; chưa có điểm thẩm định → null.</summary>
    private static string? Diff(double? appraisal, double self) =>
        appraisal.HasValue ? CollectiveFormText.Score(Math.Round(appraisal.Value - self, 1)) : null;
}
