using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Phiếu tự chấm theo trục; nhóm tiêu chí chung và các trục lặp theo bộ tiêu chí của kỳ.</summary>
public sealed class Mau09BData
{
    public const string TemplateFileName = "Mau_09B_PhieuTuChamTheoTruc.docx";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>In hoa.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    /// <summary>Ngày nộp phiếu tự chấm; chưa nộp → giữ dòng chấm.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Số La Mã, có khoảng trắng đầu.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>dd/MM/yyyy; chưa nhập → giữ dòng chấm.</summary>
    [TemplateField("DATE_OF_BIRTH")] public string? DateOfBirth { get; init; }

    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Không có chức vụ nào trong danh mục thì lấy chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }
    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }

    /// <summary>In <c>POSITIONS</c> một dòng thay cho 3 dòng (theo kiểu trình bày).</summary>
    [TemplateCondition("POSITIONS_ONE_LINE")] public bool PositionsOneLine { get; init; }

    [TemplateField("POSITIONS")] public string? Positions { get; init; }

    /// <summary>Chưa có kiểu trình bày → có.</summary>
    [TemplateCondition("HAS_CELL_CONFIRMATION")] public bool HasCellConfirmation { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Đơn vị gốc (lãnh đạo Công ty) → false, chỉ in tên công ty.</summary>
    [TemplateCondition("HAS_DEPARTMENT")] public bool HasDepartment { get; init; }

    /// <summary>Ảnh chụp trên hồ sơ.</summary>
    [TemplateField("CELL_NAME")] public string? CellName { get; init; }

    [TemplateCollection("GROUPS")] public List<GeneralGroupRow> Groups { get; init; } = new();
    [TemplateCollection("AXES")] public List<Mau09BAxisRow> Axes { get; init; } = new();

    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASKS_SCORE")] public string? TasksScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }
    [TemplateField("SELF_GRADE")] public string? SelfGrade { get; init; }

    /// <summary>
    /// Kiểu "Đảng ủy cơ sở": in tổ chức Đảng cấp trên và "ĐẢNG ỦY …" thay cho tên Đảng bộ và tên Chi bộ.
    /// Gọi sau khi gắn dữ liệu chung (<c>ORG_*</c>) vì ghi đè ORG_PARTY_NAME.
    /// </summary>
    public static void ApplyHeader(TemplateData data, FormLayout? layout, OrganizationTemplateFields org)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(org);
        if (layout?.HeaderStyle != FormHeaderStyle.PartyCommittee)
            return;
        data.Field("ORG_PARTY_NAME", FormText.OrNull(org.SuperiorPartyName?.Trim()));
        data.Field("PARTY_CELL", CollectiveFormText.CommitteeUpper(org.PartyCommitteeName));
    }

    public static Mau09BData From(IndividualFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var record = source.Record;
        var rounding = source.Criteria.Parameters.Rounding;

        Dictionary<string, double> axisScores;
        Dictionary<string, AxisNote> notes;
        try
        {
            axisScores = new Dictionary<string, double>(EvaluationScoring.ParseAxisScores(record.AxisScores) ?? new(), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            axisScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }
        try
        {
            notes = RecordFormContent.ParseAxisNotes(record.AxisNotes);
        }
        catch (JsonException)
        {
            notes = new Dictionary<string, AxisNote>(StringComparer.OrdinalIgnoreCase);
        }

        var lines = Mau09Shared.PositionLines(source);
        return new Mau09BData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            DateLine = Mau09Shared.DateLine(source),
            Quarter = record.Period != null ? " " + CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = Mau09Shared.Year(record),
            FullName = FormText.OrNull(record.Member?.FullName),
            DateOfBirth = Mau09Shared.DateOfBirth(record),
            PartyPosition = lines.Party,
            AdministrativePosition = lines.Administrative,
            MassPosition = lines.MassOrganization,
            PositionsOneLine = Mau09Shared.SingleLinePositions(source),
            Positions = Mau09Shared.PositionsOneLine(source),
            HasCellConfirmation = source.Layout?.ShowCellConfirmation ?? true,
            Department = Mau09Shared.Department(record),
            HasDepartment = Mau09Shared.HasDepartment(record),
            CellName = FormText.Spaced(Mau09Shared.CellLabelName(record)),
            Groups = Mau09Shared.GeneralGroups(source),
            Axes = source.Criteria.Axes.Select((axis, index) =>
            {
                notes.TryGetValue(axis.Code, out var note);
                return new Mau09BAxisRow
                {
                    Number = (index + 1).ToString(CultureInfo.InvariantCulture),
                    Title = axis.FormTitle ?? $"TRỤC ({index + 1}) – {axis.Name.ToUpper(Vietnamese)}",
                    Guidance = axis.FormGuidance ?? axis.Description ?? string.Empty,
                    Target = note?.Target ?? string.Empty,
                    MaxScore = Mau09Shared.Compact(axis.MaxScore),
                    Score = axisScores.TryGetValue(axis.Code, out var score) ? Mau09Shared.Compact(score) : string.Empty,
                    Result = note?.Result ?? string.Empty,
                    Note = note?.Note ?? string.Empty
                };
            }).ToList(),
            GeneralScore = Mau09Shared.CompactScore(source, record.GeneralCriteriaScore, rounding.GeneralTotal),
            TasksScore = Mau09Shared.CompactScore(source, record.TasksScore, rounding.TasksTotal),
            TotalScore = Mau09Shared.CompactScore(source, record.TotalSelfScore, rounding.Total),
            SelfGrade = Mau09Shared.SelfGrade(record)
        };
    }
}

public sealed class Mau09BAxisRow
{
    [TemplateField("A_NO")] public string? Number { get; init; }

    [TemplateField("A_TITLE")] public string? Title { get; init; }

    /// <summary>Mỗi dòng một gạch đầu dòng.</summary>
    [TemplateField("A_GUIDANCE")] public string? Guidance { get; init; }

    /// <summary>Chủ hồ sơ nhập khi tự chấm.</summary>
    [TemplateField("A_TARGET")] public string? Target { get; init; }

    [TemplateField("A_MAX")] public string? MaxScore { get; init; }
    [TemplateField("A_SCORE")] public string? Score { get; init; }

    [TemplateField("A_RESULT")] public string? Result { get; init; }

    [TemplateField("A_NOTE")] public string? Note { get; init; }
}
