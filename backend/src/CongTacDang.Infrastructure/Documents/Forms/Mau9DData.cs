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

    /// <summary>"QUÝ… NĂM…" — quý in số La Mã như bản chuẩn ("QUÝ III NĂM 2026"); chỗ điền liền sau chữ nên giá trị có khoảng trắng đầu.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>Chức vụ Đảng (dòng "Chức vụ: Chức vụ Đảng: …").</summary>
    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Chức vụ chính quyền; không giữ chức vụ nào trong danh mục thì dùng chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }

    /// <summary>Chức vụ đoàn thể.</summary>
    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }

    /// <summary>Tên phòng/đơn vị; template in tiếp ", " + tên công ty (<c>ORG_COMPANY_NAME</c>) như bản chuẩn.</summary>
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

        var positions = source.Positions;
        var noPositions = positions.All() == null;
        return new Mau9DData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            Quarter = record.Period != null ? " " + CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = FormText.Spaced(Mau09Shared.Year(record)),
            FullName = FormText.OrNull(record.Member?.FullName),
            PartyPosition = positions.Party,
            AdministrativePosition = noPositions ? FormText.OrNull(record.Member?.PositionTitle) : positions.Administrative,
            MassPosition = positions.MassOrganization,
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
