using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 09B — Phiếu tự chấm điểm đánh giá, xếp loại chất lượng hằng quý (chấm trực tiếp theo trục, Quý III/2026).
/// Template: <c>Mau_09B_PhieuTuChamTheoTruc.docx</c> (dựng từ file biểu mẫu gốc). Nhóm tiêu chí chung và các trục lặp theo bộ
/// tiêu chí của kỳ; điểm, mức, nội dung tự luận theo trục lấy từ giá trị đã lưu trên hồ sơ.
/// </summary>
public sealed class Mau09BData
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_09B_PhieuTuChamTheoTruc.docx";

    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Tên Chi bộ (in hoa) ở tiêu đề trái.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    /// <summary>"ngày … tháng … năm …" sau địa danh: ngày nộp phiếu tự chấm (chưa nộp → giữ dòng chấm).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Quý (số La Mã, "Quý III Năm 2026" như bản đã nộp).</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>Ngày sinh dd/MM/yyyy; chưa nhập → giữ dòng chấm.</summary>
    [TemplateField("DATE_OF_BIRTH")] public string? DateOfBirth { get; init; }

    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Chức vụ chính quyền; không có chức vụ nào trong danh mục thì chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }
    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }

    /// <summary>In chức vụ trên một dòng (<c>POSITIONS</c>) thay cho 3 dòng — theo kiểu trình bày.</summary>
    [TemplateCondition("POSITIONS_ONE_LINE")] public bool PositionsOneLine { get; init; }

    /// <summary>Chức vụ trên một dòng "(Đảng, Đoàn thể, Chính quyền)".</summary>
    [TemplateField("POSITIONS")] public string? Positions { get; init; }

    /// <summary>Có ô "Xác nhận của chi bộ" — theo kiểu trình bày (chưa có kiểu → có).</summary>
    [TemplateCondition("HAS_CELL_CONFIRMATION")] public bool HasCellConfirmation { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>In "DEPARTMENT, " trước tên công ty; hồ sơ thuộc đơn vị gốc (lãnh đạo Công ty) → false, chỉ in tên công ty.</summary>
    [TemplateCondition("HAS_DEPARTMENT")] public bool HasDepartment { get; init; }

    /// <summary>Chi bộ sinh hoạt (ảnh chụp trên hồ sơ).</summary>
    [TemplateField("CELL_NAME")] public string? CellName { get; init; }

    [TemplateCollection("GROUPS")] public List<GeneralGroupRow> Groups { get; init; } = new();
    [TemplateCollection("AXES")] public List<Mau09BAxisRow> Axes { get; init; } = new();

    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASKS_SCORE")] public string? TasksScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }
    [TemplateField("SELF_GRADE")] public string? SelfGrade { get; init; }

    /// <summary>
    /// Tiêu đề trái theo kiểu trình bày: kiểu "Đảng ủy cơ sở" in tên tổ chức Đảng cấp trên và "ĐẢNG ỦY …" (từ tên Đảng bộ) thay cho
    /// tên Đảng bộ và tên Chi bộ, như phiếu của Bí thư Đảng ủy. Gọi sau khi gắn dữ liệu chung (<c>ORG_*</c>).
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

    /// <summary>Dựng dữ liệu mẫu từ hồ sơ đã lưu và bộ tiêu chí của kỳ.</summary>
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
            CellName = FormText.Spaced(record.PartyCell?.Name),
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

/// <summary>Một trục của phần II Mẫu 09B.</summary>
public sealed class Mau09BAxisRow
{
    [TemplateField("A_NO")] public string? Number { get; init; }

    /// <summary>Tiêu đề trục ("TRỤC (1) – …").</summary>
    [TemplateField("A_TITLE")] public string? Title { get; init; }

    /// <summary>Nội dung gợi ý của trục (mỗi dòng một gạch đầu dòng).</summary>
    [TemplateField("A_GUIDANCE")] public string? Guidance { get; init; }

    /// <summary>Mục tiêu, nhiệm vụ đề ra (chủ hồ sơ nhập khi tự chấm).</summary>
    [TemplateField("A_TARGET")] public string? Target { get; init; }

    [TemplateField("A_MAX")] public string? MaxScore { get; init; }
    [TemplateField("A_SCORE")] public string? Score { get; init; }

    /// <summary>Tóm tắt kết quả sản phẩm thực tế; tài liệu minh chứng.</summary>
    [TemplateField("A_RESULT")] public string? Result { get; init; }

    [TemplateField("A_NOTE")] public string? Note { get; init; }
}
