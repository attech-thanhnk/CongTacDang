using CongTacDang.Domain.Entities;

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

    /// <summary>Chưa có dữ liệu lưu — giữ chữ mặc định trong template.</summary>
    [TemplateField("SUPERVISOR_COMMENT")] public string? SupervisorComment { get; init; }

    /// <summary>Ý kiến của Tổ thẩm định (đã lưu).</summary>
    [TemplateField("APPRAISAL_COMMENT")] public string? AppraisalComment { get; init; }

    /// <summary>Mức xếp loại Tổ thẩm định đề xuất (đã lưu).</summary>
    [TemplateField("PROPOSED_GRADE")] public string? ProposedGrade { get; init; }

    /// <summary>Dựng dữ liệu mẫu từ hồ sơ đã lưu (nạp kèm Period, Member, Department, PartyCell).</summary>
    public static Mau10Data From(EvaluationRecord record)
    {
        return new Mau10Data
        {
            Quarter = record.Period != null ? FormText.Quarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            GeneralSelfScore = FormText.Number(record.GeneralCriteriaScore, 1),
            TasksSelfScore = FormText.Number(record.TasksScore, 1),
            TotalSelfScore = FormText.Number(record.TotalSelfScore, 1),
            TotalAppraisalScore = FormText.Number(record.AppraisalScore, 1),
            TotalDiff = record.AppraisalScore.HasValue
                ? FormText.Number(record.AppraisalScore.Value - record.TotalSelfScore, 1)
                : null,
            AppraisalComment = FormText.OrNull(record.AppraisalComment),
            ProposedGrade = FormText.Grade(record.AppraisalProposedGrade)
        };
    }
}
