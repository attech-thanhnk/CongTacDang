using System;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 10 — Phiếu thẩm định, nhận xét, đề xuất xếp loại và ghi nhận giải trình.
/// Template: <c>Mau_10_PhieuThamDinh.docx</c>. Mọi con số lấy từ giá trị đã lưu trên hồ sơ;
/// ô chưa có dữ liệu lưu (điểm thẩm định từng nhóm) giữ chữ mặc định trong template.
/// </summary>
public sealed class Mau10Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_10_PhieuThamDinh.docx";

    /// <summary>"ngày … tháng … năm …" sau địa danh: ngày thẩm định (chưa thẩm định → giữ dòng chấm).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Điểm nhóm tiêu chí chung tự chấm (đã lưu).</summary>
    [TemplateField("GENERAL_SELF_SCORE")] public string? GeneralSelfScore { get; init; }

    /// <summary>Chưa lưu điểm thẩm định theo nhóm — giữ chữ mặc định trong template.</summary>
    [TemplateField("GENERAL_APPRAISAL_SCORE")] public string? GeneralAppraisalScore { get; init; }

    /// <summary>Chưa lưu điểm thẩm định theo nhóm — giữ chữ mặc định trong template.</summary>
    [TemplateField("GENERAL_DIFF")] public string? GeneralDiff { get; init; }

    /// <summary>Điểm nhóm sản phẩm chuyên môn tự chấm (đã lưu).</summary>
    [TemplateField("TASKS_SELF_SCORE")] public string? TasksSelfScore { get; init; }

    /// <summary>Chưa lưu điểm thẩm định theo nhóm — giữ chữ mặc định trong template.</summary>
    [TemplateField("TASKS_APPRAISAL_SCORE")] public string? TasksAppraisalScore { get; init; }

    /// <summary>Chưa lưu điểm thẩm định theo nhóm — giữ chữ mặc định trong template.</summary>
    [TemplateField("TASKS_DIFF")] public string? TasksDiff { get; init; }

    /// <summary>Tổng điểm tự chấm (đã lưu).</summary>
    [TemplateField("TOTAL_SELF_SCORE")] public string? TotalSelfScore { get; init; }

    /// <summary>Điểm thẩm định (đã lưu); chưa thẩm định thì giữ chữ mặc định.</summary>
    [TemplateField("TOTAL_APPRAISAL_SCORE")] public string? TotalAppraisalScore { get; init; }

    /// <summary>Chênh lệch = điểm thẩm định − tổng điểm tự chấm (hai giá trị đã lưu).</summary>
    [TemplateField("TOTAL_DIFF")] public string? TotalDiff { get; init; }

    /// <summary>Nhận xét của cấp trực tiếp sử dụng (B3c, task 12); chưa có thì giữ chữ mặc định trong template.</summary>
    [TemplateField("SUPERVISOR_COMMENT")] public string? SupervisorComment { get; init; }

    /// <summary>Ý kiến của Tổ thẩm định (đã lưu).</summary>
    [TemplateField("APPRAISAL_COMMENT")] public string? AppraisalComment { get; init; }

    /// <summary>
    /// Nội dung giải trình/căn cứ khi chênh lệch tự chấm – thẩm định vượt ngưỡng của bộ tiêu chí (B-09, đã lưu);
    /// không có thì giữ chữ mặc định trong template.
    /// </summary>
    [TemplateField("APPRAISAL_EXPLANATION")] public string? AppraisalExplanation { get; init; }

    /// <summary>Ô ☒/☐ của mức xếp loại Tổ thẩm định đề xuất (đã lưu); chưa đề xuất → mọi ô ☐.</summary>
    [TemplateField("CHECK_EXCELLENT")] public string? CheckExcellent { get; init; }
    [TemplateField("CHECK_GOOD")] public string? CheckGood { get; init; }
    [TemplateField("CHECK_DONE")] public string? CheckDone { get; init; }
    [TemplateField("CHECK_FAILED")] public string? CheckFailed { get; init; }

    /// <summary>Dựng dữ liệu mẫu từ hồ sơ đã lưu (nạp kèm Period, Member, Department, PartyCell).</summary>
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
            // Điểm in gọn: số nguyên "26", số lẻ một chữ số thập phân "26,5".
            GeneralSelfScore = CollectiveFormText.Score(record.GeneralCriteriaScore),
            TasksSelfScore = CollectiveFormText.Score(record.TasksScore),
            TotalSelfScore = CollectiveFormText.Score(record.TotalSelfScore),
            TotalAppraisalScore = record.AppraisalScore.HasValue ? CollectiveFormText.Score(record.AppraisalScore.Value) : null,
            TotalDiff = record.AppraisalScore.HasValue
                ? CollectiveFormText.Score(Math.Round(record.AppraisalScore.Value - record.TotalSelfScore, 1))
                : null,
            SupervisorComment = FormText.OrNull(record.DirectorComment),
            AppraisalComment = FormText.OrNull(record.AppraisalComment),
            AppraisalExplanation = FormText.OrNull(record.AppraisalExplanation),
            CheckExcellent = Box(EvaluationGrade.HoanThanhXuatSac),
            CheckGood = Box(EvaluationGrade.HoanThanhTot),
            CheckDone = Box(EvaluationGrade.HoanThanh),
            CheckFailed = Box(EvaluationGrade.KhongHoanThanh)
        };
    }
}
