using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 9D — Phụ lục kết quả thực hiện nhiệm vụ, công việc được giao trong quý. Template:
/// <c>Mau_9D_PhuLucKetQuaNhiemVu.docx</c> (dựng từ file biểu mẫu gốc). Đúng như biểu mẫu gốc: mỗi trục của bộ tiêu chí một dòng
/// "Trục n" — cột (2) tên trục, cột (3)–(7) phần tóm tắt của trục chủ hồ sơ nhập trên phiếu tự chấm.
/// </summary>
public sealed class Mau9DData
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_9D_PhuLucKetQuaNhiemVu.docx";

    /// <summary>Tên Chi bộ (in hoa) ở tiêu đề trái.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    /// <summary>"ngày … tháng … năm …" sau địa danh: ngày nộp phiếu tự chấm (chưa nộp → giữ dòng chấm).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>"QUÝ… NĂM…" — chỗ điền liền sau chữ nên giá trị có khoảng trắng đầu.</summary>
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
        Dictionary<string, AxisReport> reports;
        try
        {
            reports = RecordFormContent.ParseAxisReports(record.AxisReports);
        }
        catch (JsonException)
        {
            reports = new Dictionary<string, AxisReport>(StringComparer.OrdinalIgnoreCase);
        }

        return new Mau9DData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            DateLine = Mau09Shared.DateLine(source),
            Quarter = FormText.Spaced(Mau09Shared.Quarter(record)),
            Year = FormText.Spaced(Mau09Shared.Year(record)),
            FullName = FormText.OrNull(record.Member?.FullName),
            Positions = source.Positions.All() ?? FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name),
            CellName = FormText.OrNull(record.PartyCell?.Name),
            Axes = source.Criteria.Axes.Select((axis, index) =>
            {
                var report = reports.GetValueOrDefault(axis.Code);
                return new Mau9DAxis
                {
                    Label = "Trục " + (index + 1).ToString(CultureInfo.InvariantCulture),
                    Name = axis.Name,
                    Deadline = report?.Deadline ?? string.Empty,
                    Status = report?.Status ?? string.Empty,
                    Product = report?.Product ?? string.Empty,
                    Progress = report?.Progress ?? string.Empty,
                    Note = report?.Note ?? string.Empty
                };
            }).ToList()
        };
    }
}

/// <summary>Một dòng "Trục n" của Mẫu 9D.</summary>
public sealed class Mau9DAxis
{
    /// <summary>"Trục 1", "Trục 2"… như cột TT của mẫu gốc.</summary>
    [TemplateField("A_LABEL")] public string? Label { get; init; }

    /// <summary>(2) Tên trục theo bộ tiêu chí của kỳ.</summary>
    [TemplateField("A_NAME")] public string? Name { get; init; }

    [TemplateField("A_DEADLINE")] public string? Deadline { get; init; }
    [TemplateField("A_STATUS")] public string? Status { get; init; }
    [TemplateField("A_PRODUCT")] public string? Product { get; init; }
    [TemplateField("A_PROGRESS")] public string? Progress { get; init; }
    [TemplateField("A_NOTE")] public string? Note { get; init; }
}
