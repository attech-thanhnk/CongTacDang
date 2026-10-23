using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Tổng hợp kết quả nhiệm vụ của đơn vị; luôn đủ 13 dòng theo nhóm nội dung.</summary>
public sealed class Mau08Data
{
    public const string TemplateFileName = "Mau_08_TongHopKetQuaNhiemVu.docx";

    [TemplateField("PARTY_PARENT")] public string? PartyParent { get; init; }
    [TemplateField("PARTY_ORG")] public string? PartyOrganization { get; init; }

    /// <summary>Lần cập nhật hồ sơ tập thể gần nhất (chưa sửa → ngày tạo).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Chỗ điền liền với chữ trước nên giá trị có khoảng trắng đầu.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }

    /// <summary>Luôn đủ 13 dòng, đúng thứ tự.</summary>
    [TemplateCollection("ROWS")] public List<Mau08Row> Rows { get; init; } = new();

    public static Mau08Data From(CollectiveEvaluationRecord record, PartyHeader header)
    {
        var items = record.Items.OrderBy(i => i.ItemOrder).ToList();
        return new Mau08Data
        {
            PartyParent = PartyHeader.Lines(header.Parent),
            PartyOrganization = PartyHeader.Lines(header.Organization),
            DateLine = CollectiveFormText.DateLine(record.UpdatedAt ?? record.CreatedAt),
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
                    // Không có nhiệm vụ: nhóm có dòng "- Nhiệm vụ 1: …" giữ chữ mẫu, nhóm khác để trống.
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
