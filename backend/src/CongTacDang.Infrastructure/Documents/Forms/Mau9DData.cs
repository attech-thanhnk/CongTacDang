using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phụ lục kết quả nhiệm vụ: mỗi trục một dòng "Trục n", cột (3)–(7) là phần tóm tắt nhập trên phiếu tự chấm.</summary>
public sealed class Mau9DData
{
    public const string TemplateFileName = "Mau_9D_PhuLucKetQuaNhiemVu.docx";

    /// <summary>In hoa.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    /// <summary>Số La Mã, có khoảng trắng đầu vì chỗ điền liền sau chữ.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Không giữ chức vụ nào trong danh mục thì lấy chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }

    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }

    /// <summary>In <c>POSITIONS</c> một dòng thay cho 3 dòng (theo kiểu trình bày).</summary>
    [TemplateCondition("POSITIONS_ONE_LINE")] public bool PositionsOneLine { get; init; }

    [TemplateField("POSITIONS")] public string? Positions { get; init; }

    /// <summary>Dòng địa danh, ngày dưới "ĐẢNG CỘNG SẢN VIỆT NAM" (theo kiểu trình bày).</summary>
    [TemplateCondition("HAS_DATE_LINE")] public bool HasDateLine { get; init; }

    /// <summary>Ngày nộp phiếu tự chấm; chưa nộp → giữ dòng chấm.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Template in tiếp ", " + <c>ORG_COMPANY_NAME</c>.</summary>
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Đơn vị gốc (lãnh đạo Công ty) → false, chỉ in tên công ty.</summary>
    [TemplateCondition("HAS_DEPARTMENT")] public bool HasDepartment { get; init; }

    /// <summary>Ảnh chụp trên hồ sơ.</summary>
    [TemplateField("CELL_NAME")] public string? CellName { get; init; }

    [TemplateCollection("AXES")] public List<Mau9DAxis> Axes { get; init; } = new();

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

        var lines = Mau09Shared.PositionLines(source);
        return new Mau9DData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            Quarter = record.Period != null ? " " + CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = FormText.Spaced(Mau09Shared.Year(record)),
            FullName = FormText.OrNull(record.Member?.FullName),
            PartyPosition = lines.Party,
            AdministrativePosition = lines.Administrative,
            MassPosition = lines.MassOrganization,
            PositionsOneLine = Mau09Shared.SingleLinePositions(source),
            Positions = Mau09Shared.PositionsOneLine(source),
            HasDateLine = source.Layout?.ShowDateLineOn9D == true,
            DateLine = Mau09Shared.DateLine(source),
            Department = Mau09Shared.Department(record),
            HasDepartment = Mau09Shared.HasDepartment(record),
            CellName = Mau09Shared.CellLabelName(record),
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

public sealed class Mau9DAxis
{
    [TemplateField("A_LABEL")] public string? Label { get; init; }

    [TemplateField("A_NAME")] public string? Name { get; init; }

    [TemplateField("A_DEADLINE")] public string? Deadline { get; init; }
    [TemplateField("A_STATUS")] public string? Status { get; init; }
    [TemplateField("A_PRODUCT")] public string? Product { get; init; }
    [TemplateField("A_PROGRESS")] public string? Progress { get; init; }
    [TemplateField("A_NOTE")] public string? Note { get; init; }
}
