using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 02 — Phiếu tự đánh giá kết quả thực hiện sản phẩm, công việc hằng quý.
/// Template: <c>Mau_02_TuDanhGia.docx</c>.
/// </summary>
public sealed class Mau02Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_02_TuDanhGia.docx";

    [TemplateField("DEPARTMENT")] public string? Department { get; init; }
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }

    [TemplateCollection("TASKS")] public List<Mau02TaskRow> Tasks { get; init; } = new();

    /// <summary>
    /// Dựng dữ liệu mẫu từ hồ sơ đã lưu (nạp kèm Period, Member, Department, PartyCell, Tasks).
    /// <paramref name="evidenceNames"/>: tên tệp minh chứng (phiên bản hiện hành) theo Id nhiệm vụ.
    /// </summary>
    public static Mau02Data From(EvaluationRecord record, IReadOnlyDictionary<Guid, string>? evidenceNames = null)
    {
        var tasks = record.Tasks.OrderBy(t => t.TaskOrder).ToList();
        return new Mau02Data
        {
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            Quarter = record.Period != null ? FormText.Quarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            Tasks = tasks.Select((t, index) =>
            {
                // Giữ nguyên cách tính của bản cũ (trọng số Khung 2 cố định) — xem T-38.
                double aPct = Math.Round(t.CriteriaA_Ratio * 100);
                double bPct = Math.Round(t.CriteriaB_Ratio * 100);
                double cPct = Math.Round(t.CriteriaC_Ratio * 100);
                double dPct = Math.Round(t.CriteriaD_Ratio * 100);
                double resultPct = Math.Round((aPct * 0.15) + (bPct * 0.50) + (cPct * 0.15) + (dPct * 0.20), 1);
                double score = Math.Round((resultPct * t.Weight) / 100.0, 2);

                string? evidence = null;
                if (evidenceNames != null && evidenceNames.TryGetValue(t.Id, out var name))
                    evidence = FormText.OrNull(name);

                return new Mau02TaskRow
                {
                    Order = (index + 1).ToString(),
                    Name = t.TaskName,
                    Weight = FormText.Number(t.Weight, 1),
                    CriteriaA = FormText.Percent(t.CriteriaA_Ratio),
                    CriteriaB = FormText.Percent(t.CriteriaB_Ratio),
                    CriteriaC = FormText.Percent(t.CriteriaC_Ratio),
                    CriteriaD = FormText.Percent(t.CriteriaD_Ratio),
                    ResultPercent = FormText.Number(resultPct, 1) + "%",
                    Score = FormText.Number(score, 2),
                    IsExceedStandard = t.IsExceedStandard,
                    Evidence = evidence
                };
            }).ToList()
        };
    }
}

/// <summary>Một dòng nhiệm vụ của Mẫu 02.</summary>
public sealed class Mau02TaskRow
{
    [TemplateField("T_STT")] public string? Order { get; init; }
    [TemplateField("T_NAME")] public string? Name { get; init; }
    [TemplateField("T_WEIGHT")] public string? Weight { get; init; }
    [TemplateField("T_A")] public string? CriteriaA { get; init; }
    [TemplateField("T_B")] public string? CriteriaB { get; init; }
    [TemplateField("T_C")] public string? CriteriaC { get; init; }
    [TemplateField("T_D")] public string? CriteriaD { get; init; }
    [TemplateField("T_RESULT_PCT")] public string? ResultPercent { get; init; }
    [TemplateField("T_SCORE")] public string? Score { get; init; }

    /// <summary>Khối <c>if:T_IS_EXCEED</c> / <c>ifnot:T_IS_EXCEED</c> trong template chứa chữ hiển thị.</summary>
    [TemplateCondition("T_IS_EXCEED")] public bool IsExceedStandard { get; init; }

    /// <summary>Tên tệp minh chứng; không có thì giữ chữ mặc định trong template.</summary>
    [TemplateField("T_EVIDENCE")] public string? Evidence { get; init; }
}
