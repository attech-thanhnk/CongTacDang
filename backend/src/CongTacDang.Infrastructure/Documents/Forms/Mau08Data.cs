using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 08 — Báo cáo tổng hợp kết quả thực hiện các nhiệm vụ của cơ quan, đơn vị.
/// Template: <c>Mau_08_TongHopKetQuaNhiemVu.docx</c> (bảng của biểu mẫu gốc HD03, PDF tr.48–49; dòng lặp theo 13 nhóm nội dung).
/// </summary>
public sealed class Mau08Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_08_TongHopKetQuaNhiemVu.docx";

    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }
    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>"QUÝ…/NĂM..." — chỗ điền không có khoảng trắng trước nên giá trị có khoảng trắng đầu.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }

    /// <summary>13 dòng theo nhóm nội dung của biểu mẫu (luôn đủ 13 dòng, đúng thứ tự).</summary>
    [TemplateCollection("ROWS")] public List<Mau08Row> Rows { get; init; } = new();

    /// <summary>Dựng từ hồ sơ tập thể Mẫu 08 (các dòng nhiệm vụ gắn mã nhóm nội dung 1–13).</summary>
    public static Mau08Data From(CollectiveEvaluationRecord record, PartyHeader header)
    {
        var items = record.Items.OrderBy(i => i.ItemOrder).ToList();
        return new Mau08Data
        {
            PartyParent = header.Parent,
            PartyOrganization = header.Organization,
            Quarter = " " + CollectiveFormText.RomanQuarter(record.Period.Quarter),
            Year = " " + FormText.Year(record.Period.Year),
            Rows = Hd03FormCatalog.Form08Categories.Select(category =>
            {
                var tasks = items.Where(i => i.Category == category.Code).ToList();
                var names = tasks.Select(t => t.TaskName?.Trim()).Where(n => !string.IsNullOrEmpty(n)).ToList();
                return new Mau08Row
                {
                    Order = category.Code,
                    Title = category.Title,
                    // Có nhiệm vụ: liệt kê dưới tên nhóm; không có: nhóm có dòng "- Nhiệm vụ 1: …" giữ chữ mẫu, nhóm khác để trống.
                    Tasks = names.Count > 0
                        ? "\n" + string.Join("\n", names.Select(n => "- " + n))
                        : category.HasTaskLines ? null : string.Empty,
                    Plan = CollectiveFormText.Lines(tasks.Select(t => t.PlanOrDirection)),
                    Result = CollectiveFormText.Lines(tasks.Select(t => t.Result)),
                    Issues = CollectiveFormText.Lines(tasks.Select(t => t.Limitations)),
                    Notes = CollectiveFormText.Lines(tasks.Select(t => t.Notes))
                };
            }).ToList()
        };
    }
}

/// <summary>Một dòng (nhóm nội dung) của Mẫu 08.</summary>
public sealed class Mau08Row
{
    [TemplateField("R_STT")] public string? Order { get; init; }
    [TemplateField("R_TITLE")] public string? Title { get; init; }
    [TemplateField("R_TASKS")] public string? Tasks { get; init; }
    [TemplateField("R_PLAN")] public string? Plan { get; init; }
    [TemplateField("R_RESULT")] public string? Result { get; init; }
    [TemplateField("R_ISSUES")] public string? Issues { get; init; }
    [TemplateField("R_NOTES")] public string? Notes { get; init; }
}
