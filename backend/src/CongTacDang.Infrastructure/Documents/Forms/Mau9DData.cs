using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý. Template:
/// <c>Mau_9D_PhuLucKetQuaNhiemVu.docx</c> (dựng từ file biểu mẫu gốc). Mỗi trục của bộ tiêu chí một dòng "Trục n", sau đó các dòng
/// nhiệm vụ chủ hồ sơ đã nhập cho trục đó (cột (2)–(7) của mẫu gốc).
/// </summary>
public sealed class Mau9DData
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_9D_PhuLucKetQuaNhiemVu.docx";

    /// <summary>Tên Chi bộ (in hoa) ở tiêu đề trái.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>Chức vụ (Đảng, Chính quyền, Đoàn thể) đang giữ; không có thì chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("POSITIONS")] public string? Positions { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Chi bộ đang sinh hoạt (ảnh chụp trên hồ sơ).</summary>
    [TemplateField("CELL_NAME")] public string? CellName { get; init; }

    [TemplateCollection("AXES")] public List<Mau9DAxis> Axes { get; init; } = new();

    /// <summary>Dựng dữ liệu mẫu từ hồ sơ đã lưu và bộ tiêu chí của kỳ.</summary>
    public static Mau9DData From(IndividualFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var record = source.Record;
        List<TaskResultRow> rows;
        try
        {
            rows = RecordFormContent.ParseTaskResults(record.TaskResults);
        }
        catch (JsonException)
        {
            rows = new List<TaskResultRow>();
        }

        return new Mau9DData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            Quarter = Mau09Shared.Quarter(record),
            Year = Mau09Shared.Year(record),
            FullName = FormText.OrNull(record.Member?.FullName),
            Positions = source.Positions.All() ?? FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name),
            CellName = FormText.OrNull(record.PartyCell?.Name),
            Axes = source.Criteria.Axes.Select((axis, index) => new Mau9DAxis
            {
                Label = "Trục " + (index + 1).ToString(CultureInfo.InvariantCulture),
                Name = axis.Name,
                Rows = rows
                    .Where(r => string.Equals(r.AxisCode, axis.Code, StringComparison.OrdinalIgnoreCase))
                    .Select((r, i) => new Mau9DRow
                    {
                        Order = (i + 1).ToString(CultureInfo.InvariantCulture),
                        Content = r.Content,
                        Deadline = r.Deadline ?? string.Empty,
                        Status = r.Status ?? string.Empty,
                        Product = r.Product ?? string.Empty,
                        Progress = r.Progress ?? string.Empty,
                        Note = r.Note ?? string.Empty
                    }).ToList()
            }).ToList()
        };
    }
}

/// <summary>Một trục của Mẫu 9D (dòng "Trục n" và các dòng nhiệm vụ).</summary>
public sealed class Mau9DAxis
{
    /// <summary>"Trục 1", "Trục 2"… như cột TT của mẫu gốc.</summary>
    [TemplateField("A_LABEL")] public string? Label { get; init; }

    /// <summary>Tên trục theo bộ tiêu chí của kỳ.</summary>
    [TemplateField("A_NAME")] public string? Name { get; init; }

    [TemplateCollection("ROWS")] public List<Mau9DRow> Rows { get; init; } = new();
}

/// <summary>Một dòng nhiệm vụ của Mẫu 9D.</summary>
public sealed class Mau9DRow
{
    [TemplateField("R_STT")] public string? Order { get; init; }
    [TemplateField("R_CONTENT")] public string? Content { get; init; }
    [TemplateField("R_DEADLINE")] public string? Deadline { get; init; }
    [TemplateField("R_STATUS")] public string? Status { get; init; }
    [TemplateField("R_PRODUCT")] public string? Product { get; init; }
    [TemplateField("R_PROGRESS")] public string? Progress { get; init; }
    [TemplateField("R_NOTE")] public string? Note { get; init; }
}
