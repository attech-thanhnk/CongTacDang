using System.Collections.Generic;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 07 — Báo cáo tự đánh giá, xếp loại chất lượng của tập thể Đảng ủy (Chi ủy, Chi bộ).
/// Template: <c>Mau_07_BaoCaoTuDanhGiaTapThe.docx</c> (dựng từ biểu mẫu gốc HD03, PDF tr.46–47).
/// </summary>
public sealed class Mau07Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_07_BaoCaoTuDanhGiaTapThe.docx";

    /// <summary>Dòng "ĐẢNG BỘ …" (tổ chức Đảng cấp trên của tập thể).</summary>
    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }

    /// <summary>Dòng "ĐẢNG ỦY (CHI BỘ) …" (tổ chức Đảng của tập thể).</summary>
    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>Tên tập thể trong tiêu đề ("CỦA TẬP THỂ …"), chữ in hoa.</summary>
    [TemplateField("SUBJECT_NAME_UPPER")] public string? SubjectNameUpper { get; init; }

    /// <summary>Quý (La Mã) — chỗ điền "Quý……Năm" nên có khoảng trắng hai bên.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }

    [TemplateField("YEAR")] public string? Year { get; init; }

    /// <summary>"… thực hiện các nhiệm vụ tại &lt;Đảng bộ (Chi bộ)&gt;".</summary>
    [TemplateField("UNIT_NAME")] public string? UnitName { get; init; }

    /// <summary>"tập thể &lt;Đảng ủy (Chi ủy, Chi bộ)&gt; tự đánh giá".</summary>
    [TemplateField("SUBJECT_NAME")] public string? SubjectName { get; init; }

    /// <summary>Mục A.I.1–4 (mã I.1–I.4); để trống → giữ dòng chấm của biểu mẫu.</summary>
    [TemplateField("STRENGTH_1")] public string? Strength1 { get; init; }
    [TemplateField("STRENGTH_2")] public string? Strength2 { get; init; }
    [TemplateField("STRENGTH_3")] public string? Strength3 { get; init; }
    [TemplateField("STRENGTH_4")] public string? Strength4 { get; init; }

    /// <summary>Mục A.II — hạn chế, khuyết điểm và nguyên nhân.</summary>
    [TemplateField("LIMITATIONS")] public string? Limitations { get; init; }
    [TemplateField("CAUSES")] public string? Causes { get; init; }

    /// <summary>Mục A.III — kết quả khắc phục hạn chế, khuyết điểm kỳ trước.</summary>
    [TemplateField("PREVIOUS_REMEDIATION")] public string? PreviousRemediation { get; init; }

    /// <summary>Mục A.IV — giải trình những vấn đề được gợi ý kiểm điểm.</summary>
    [TemplateField("EXPLANATION")] public string? Explanation { get; init; }

    /// <summary>Mục A.V — trách nhiệm của tập thể, cá nhân.</summary>
    [TemplateField("RESPONSIBILITIES")] public string? Responsibilities { get; init; }

    /// <summary>Mục A.VI — phương hướng, biện pháp khắc phục.</summary>
    [TemplateField("REMEDIATION_PLAN")] public string? RemediationPlan { get; init; }

    /// <summary>Mục B — điểm đã lưu trên hồ sơ tập thể (không tính lại).</summary>
    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASK_SCORE")] public string? TaskScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }

    /// <summary>Dựng từ hồ sơ tập thể Mẫu 07 và các mục con I.1–I.4 đã lưu.</summary>
    public static Mau07Data From(CollectiveEvaluationRecord record, PartyHeader header, IReadOnlyDictionary<string, string> sections)
    {
        string? Section(string code) => sections.TryGetValue(code, out var text) ? FormText.OrNull(text) : null;
        return new Mau07Data
        {
            PartyParent = header.Parent,
            PartyOrganization = header.Organization,
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
