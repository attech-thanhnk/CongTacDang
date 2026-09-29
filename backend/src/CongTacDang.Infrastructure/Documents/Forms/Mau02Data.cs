using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

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
        // Số chữ số thập phân của điểm sản phẩm theo bộ tiêu chí của kỳ (mặc định 2 nếu kỳ chưa có bộ).
        var scoreDecimals = FormCriteria.Read(record.Period)?.Content.Parameters.Rounding.TaskScore.Decimals ?? 2;
        return new Mau02Data
        {
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            Quarter = record.Period != null ? FormText.Quarter(record.Period.Quarter) : null,
            Year = record.Period != null ? FormText.Year(record.Period.Year) : null,
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = FormText.OrNull(record.Member?.PositionTitle),
            Tasks = tasks.Select((t, index) =>
            {
                // Một nguồn số liệu (T-38): điểm đạt = SelfScore đã lưu (đã tính theo tỷ trọng Khung chức danh của hồ sơ
                // khi tự chấm). Kết quả SP (%) chỉ là cách viết khác của cùng giá trị: SelfScore / Trọng số.
                double? resultPct = t.Weight > 0 ? Math.Round(t.SelfScore / t.Weight * 100, 1) : null;

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
                    ResultPercent = resultPct.HasValue ? FormText.Number(resultPct.Value, 1) + "%" : "-",
                    Score = FormText.Number(t.SelfScore, scoreDecimals),
                    IsExceedStandard = t.IsExceedStandard,
                    Evidence = evidence
                };
            }).ToList()
        };
    }
}

/// <summary>Đọc ảnh chụp bộ tiêu chí của kỳ cho biểu mẫu (lỗi/không có → null).</summary>
internal static class FormCriteria
{
    public static CriteriaSnapshot? Read(EvaluationPeriod? period)
    {
        if (period == null)
            return null;
        try
        {
            return period.GetCriteria();
        }
        catch (FormatException)
        {
            return null;
        }
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
