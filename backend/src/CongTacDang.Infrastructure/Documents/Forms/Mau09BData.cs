using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
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
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>Chức vụ (Đảng, Đoàn thể, Chính quyền) đang giữ; không có thì chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("POSITIONS")] public string? Positions { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Chi bộ sinh hoạt (ảnh chụp trên hồ sơ).</summary>
    [TemplateField("CELL_NAME")] public string? CellName { get; init; }

    [TemplateCollection("GROUPS")] public List<GeneralGroupRow> Groups { get; init; } = new();
    [TemplateCollection("AXES")] public List<Mau09BAxisRow> Axes { get; init; } = new();

    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASKS_SCORE")] public string? TasksScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }
    [TemplateField("SELF_GRADE")] public string? SelfGrade { get; init; }

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

        return new Mau09BData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            Quarter = Mau09Shared.Quarter(record),
            Year = Mau09Shared.Year(record),
            FullName = FormText.OrNull(record.Member?.FullName),
            Positions = source.Positions.All() ?? FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name),
            CellName = FormText.OrNull(record.PartyCell?.Name),
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
            GeneralScore = Mau09Shared.Score(source, record.GeneralCriteriaScore, rounding.GeneralTotal),
            TasksScore = Mau09Shared.Score(source, record.TasksScore, rounding.TasksTotal),
            TotalScore = Mau09Shared.Score(source, record.TotalSelfScore, rounding.Total),
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
