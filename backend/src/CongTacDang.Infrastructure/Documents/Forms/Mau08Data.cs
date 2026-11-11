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

    /// <summary>Ô ký "T/M ĐẢNG ỦY" hoặc "T/M CHI BỘ" theo tổ chức lập báo cáo; không xác định → giữ chữ mặc định.</summary>
    [TemplateField("SIGN_ON_BEHALF")] public string? SignOnBehalf { get; init; }

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
            SignOnBehalf = CollectiveFormText.SignOnBehalf(header.Organization),
            DateLine = CollectiveFormText.DateLine(record.UpdatedAt ?? record.CreatedAt),
            Quarter = " " + CollectiveFormText.RomanQuarter(record.Period.Quarter),
            Year = " " + FormText.Year(record.Period.Year),
            Rows = Hd03FormCatalog.Form08Categories.Select(category =>
            {
                var tasks = items.Where(i => i.Category == category.Code).ToList();
                return new Mau08Row
                {
                    Order = category.Code,
                    Title = category.Title,
                    // Không có nhiệm vụ: nhóm có dòng "- Nhiệm vụ 1: …" giữ chữ mẫu, nhóm khác để trống.
                    Tasks = tasks.Count > 0
                        ? TaskLines(tasks.Select(t => t.TaskName), "…")
                        : category.HasTaskLines ? null : string.Empty,
                    Plan = TaskLines(tasks.Select(t => t.PlanOrDirection)),
                    Result = TaskLines(tasks.Select(t => t.Result)),
                    Issues = TaskLines(tasks.Select(t => t.Limitations)),
                    Notes = TaskLines(tasks.Select(t => t.Notes))
                };
            }).ToList()
        };
    }

    /// <summary>
    /// Mỗi nhiệm vụ một dòng "- …" sau dòng tên nhóm, cùng thứ tự ở mọi cột để cột 3–6 thẳng hàng với nhiệm vụ ở cột 2;
    /// nhiệm vụ chưa nhập ô này để dòng trống (cột 2 in <paramref name="empty"/>). Cả cột chưa nhập → ô trống.
    /// </summary>
    public static string TaskLines(IEnumerable<string?> values, string? empty = null)
    {
        var list = values.Select(v => FormText.OrNull(v?.Trim())).ToList();
        if (empty == null && list.All(v => v == null))
            return string.Empty;
        // Giá trị nhiều dòng gộp một dòng để không đẩy lệch các nhiệm vụ sau.
        return string.Concat(list.Select(v => "\n" + (v != null ? "- " + v.Replace("\r", "").Replace('\n', ' ')
            : empty != null ? "- " + empty : string.Empty)));
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
