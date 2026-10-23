using System.Collections.Generic;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Báo cáo tự đánh giá, xếp loại của tập thể Đảng ủy (Chi ủy, Chi bộ).</summary>
public sealed class Mau07Data
{
    public const string TemplateFileName = "Mau_07_BaoCaoTuDanhGiaTapThe.docx";

    /// <summary>Tổ chức Đảng cấp trên của tập thể.</summary>
    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }

    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>Lần cập nhật hồ sơ tập thể gần nhất (chưa sửa → ngày tạo).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Chữ in hoa, trong tiêu đề "CỦA TẬP THỂ …".</summary>
    [TemplateField("SUBJECT_NAME_UPPER")] public string? SubjectNameUpper { get; init; }

    /// <summary>Có khoảng trắng hai bên vì chỗ điền là "Quý……Năm".</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }

    [TemplateField("YEAR")] public string? Year { get; init; }

    [TemplateField("UNIT_NAME")] public string? UnitName { get; init; }

    [TemplateField("SUBJECT_NAME")] public string? SubjectName { get; init; }

    /// <summary>Mục A.I.1–4; để trống → giữ dòng chấm.</summary>
    [TemplateField("STRENGTH_1")] public string? Strength1 { get; init; }
    [TemplateField("STRENGTH_2")] public string? Strength2 { get; init; }
    [TemplateField("STRENGTH_3")] public string? Strength3 { get; init; }
    [TemplateField("STRENGTH_4")] public string? Strength4 { get; init; }

    /// <summary>Mục A.II.</summary>
    [TemplateField("LIMITATIONS")] public string? Limitations { get; init; }
    [TemplateField("CAUSES")] public string? Causes { get; init; }

    /// <summary>Mục A.III.</summary>
    [TemplateField("PREVIOUS_REMEDIATION")] public string? PreviousRemediation { get; init; }

    /// <summary>Mục A.IV.</summary>
    [TemplateField("EXPLANATION")] public string? Explanation { get; init; }

    /// <summary>Mục A.V.</summary>
    [TemplateField("RESPONSIBILITIES")] public string? Responsibilities { get; init; }

    /// <summary>Mục A.VI.</summary>
    [TemplateField("REMEDIATION_PLAN")] public string? RemediationPlan { get; init; }

    /// <summary>Mục B: điểm đã lưu, không tính lại.</summary>
    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASK_SCORE")] public string? TaskScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }

    public static Mau07Data From(CollectiveEvaluationRecord record, PartyHeader header, IReadOnlyDictionary<string, string> sections)
    {
        string? Section(string code) => sections.TryGetValue(code, out var text) ? FormText.OrNull(text) : null;
        return new Mau07Data
        {
            PartyParent = PartyHeader.Lines(header.Parent),
            PartyOrganization = PartyHeader.Lines(header.Organization),
            DateLine = CollectiveFormText.DateLine(record.UpdatedAt ?? record.CreatedAt),
            SubjectNameUpper = PartyHeader.Upper(record.SubjectName),
            Quarter = " " + CollectiveFormText.RomanQuarter(record.Period.Quarter) + " ",
            Year = FormText.Year(record.Period.Year),
            UnitName = header.UnitName,
            SubjectName = FormText.OrNull(record.SubjectName),
            Strength1 = Section("I.1"),
            Strength2 = Section("I.2"),
            Strength3 = Section("I.3"),
            Strength4 = Section("I.4"),
            Limitations = FormText.OrNull(record.Limitations),
            Causes = FormText.OrNull(record.Causes),
            PreviousRemediation = FormText.OrNull(record.PreviousRemediation),
            Explanation = FormText.OrNull(record.Explanation),
            Responsibilities = FormText.OrNull(record.Responsibilities),
            RemediationPlan = FormText.OrNull(record.RemediationPlan),
            GeneralScore = CollectiveFormText.Score(record.GeneralCriteriaScore),
            TaskScore = CollectiveFormText.Score(record.TaskCriteriaScore),
            TotalScore = CollectiveFormText.Score(record.TotalScore)
        };
    }
}
