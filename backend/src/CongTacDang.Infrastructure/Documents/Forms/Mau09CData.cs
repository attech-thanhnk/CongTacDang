using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân. Template: <c>Mau_09C_BanTuDanhGia.docx</c> (dựng từ file biểu mẫu
/// gốc). Phần I lặp theo các mục khai báo trong bộ tiêu chí của kỳ: mục đầu tiên gồm nhận xét chung và nhiệm vụ theo 6 trục (kèm
/// mức tự đánh giá) nhập trên phiếu tự chấm; phần II lấy điểm và mức tự đề xuất đã lưu. Lưu ý hướng dẫn viết của mục (chữ đỏ trên
/// biểu mẫu gốc) chỉ hiện trên màn hình nhập, không in — như bản tự đánh giá đã nộp.
/// </summary>
public sealed class Mau09CData
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_09C_BanTuDanhGia.docx";

    /// <summary>Tên Chi bộ (in hoa) ở tiêu đề trái.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    /// <summary>"ngày … tháng … năm …" sau địa danh: ngày nộp phiếu tự chấm (chưa nộp → giữ dòng chấm).</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Chức vụ chính quyền; không có chức vụ nào trong danh mục thì chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }
    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    [TemplateCollection("SECTIONS")] public List<Mau09CSection> Sections { get; init; } = new();

    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASKS_SCORE")] public string? TasksScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }
    [TemplateField("SELF_GRADE")] public string? SelfGrade { get; init; }

    /// <summary>Dựng dữ liệu mẫu từ hồ sơ đã lưu và bộ tiêu chí của kỳ.</summary>
    public static Mau09CData From(IndividualFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var record = source.Record;
        var rounding = source.Criteria.Parameters.Rounding;
        Dictionary<string, string> texts;
        try
        {
            texts = RecordFormContent.ParseSelfAssessment(record.SelfAssessment);
        }
        catch (JsonException)
        {
            texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        Dictionary<string, AxisReport> reports;
        try
        {
            reports = RecordFormContent.ParseAxisReports(record.AxisReports);
        }
        catch (JsonException)
        {
            reports = new Dictionary<string, AxisReport>(StringComparer.OrdinalIgnoreCase);
        }
        var axisBlocks = RecordFormContent.BuildAxisTaskBlocks(source.Criteria, reports)
            .Select(b => new Mau09CAxis { Title = b.Title, Lines = b.Lines.Select(l => new Mau09CLine { Text = l }).ToList() })
            .ToList();

        var positions = source.Positions;
        var noPositions = positions.All() == null;
        return new Mau09CData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            DateLine = Mau09Shared.DateLine(source),
            Quarter = Mau09Shared.Quarter(record),
            Year = Mau09Shared.Year(record),
            FullName = FormText.Spaced(record.Member?.FullName),
            PartyPosition = positions.Party,
            AdministrativePosition = noPositions ? FormText.OrNull(record.Member?.PositionTitle) : positions.Administrative,
            MassPosition = positions.MassOrganization,
            Department = FormText.OrNull(record.Department?.Name),
            Sections = source.Criteria.SelfAssessmentSections.Select((section, index) =>
            {
                // Mục đầu tiên: phần nhận xét chung rồi các khối "n. Tên trục" / "Nhiệm vụ k: … Tự đánh giá mức N." dựng từ nhiệm
                // vụ theo trục. Chưa nhập gì → giữ dòng chấm của template để viết tay; chỉ có nhiệm vụ → bỏ đoạn nhận xét.
                var text = FormText.OrNull(texts.GetValueOrDefault(section.Code));
                var axes = index == 0 ? axisBlocks : new List<Mau09CAxis>();
                return new Mau09CSection
                {
                    Title = section.Title,
                    Intro = section.Guidance ?? string.Empty,
                    Content = text,
                    ShowContent = text != null || axes.Count == 0,
                    Axes = axes
                };
            }).ToList(),
            GeneralScore = Mau09Shared.Score(source, record.GeneralCriteriaScore, rounding.GeneralTotal),
            TasksScore = Mau09Shared.Score(source, record.TasksScore, rounding.TasksTotal),
            TotalScore = Mau09Shared.Score(source, record.TotalSelfScore, rounding.Total),
            SelfGrade = Mau09Shared.SelfGrade(record)
        };
    }
}

/// <summary>Một mục tự đánh giá của Mẫu 09C.</summary>
public sealed class Mau09CSection
{
    [TemplateField("S_TITLE")] public string? Title { get; init; }
    [TemplateField("S_INTRO")] public string? Intro { get; init; }
    [TemplateField("S_CONTENT")] public string? Content { get; init; }

    /// <summary>Hiện đoạn nội dung (có nhận xét chung, hoặc mục không có nhiệm vụ theo trục — giữ dòng chấm).</summary>
    [TemplateCondition("S_SHOW_CONTENT")] public bool ShowContent { get; init; }

    /// <summary>Các khối nhiệm vụ theo trục (chỉ mục đầu tiên).</summary>
    [TemplateCollection("S_AXES")] public List<Mau09CAxis> Axes { get; init; } = new();
}

/// <summary>Một trục trong mục I Mẫu 09C: tiêu đề "n. Tên trục" và các dòng nhiệm vụ / ý con.</summary>
public sealed class Mau09CAxis
{
    [TemplateField("AX_TITLE")] public string? Title { get; init; }
    [TemplateCollection("AX_LINES")] public List<Mau09CLine> Lines { get; init; } = new();
}

/// <summary>Một dòng "Nhiệm vụ k: … Tự đánh giá mức N." hoặc ý con "+ …" (một đoạn văn).</summary>
public sealed class Mau09CLine
{
    [TemplateField("L_TEXT")] public string? Text { get; init; }
}
