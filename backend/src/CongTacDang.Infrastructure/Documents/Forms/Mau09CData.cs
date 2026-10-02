using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 09C — Bản tự đánh giá, xếp loại của cá nhân. Template: <c>Mau_09C_BanTuDanhGia.docx</c> (dựng từ file biểu mẫu
/// gốc). Phần I lặp theo các mục khai báo trong bộ tiêu chí của kỳ, nội dung lấy từ hồ sơ; phần II lấy điểm và mức tự đề xuất đã lưu.
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

        List<TaskResultRow> taskRows;
        try
        {
            taskRows = RecordFormContent.ParseTaskResults(record.TaskResults);
        }
        catch (JsonException)
        {
            taskRows = new List<TaskResultRow>();
        }
        var structured = BuildTaskLines(source.Criteria, taskRows);

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
            Sections = source.Criteria.SelfAssessmentSections.Select((section, index) => new Mau09CSection
            {
                Title = section.Title,
                Intro = section.Guidance ?? string.Empty,
                // Chưa nhập → giữ dòng chấm của template để viết tay.
                // Mục đầu tiên nhận thêm các câu "Nhiệm vụ n: … Tự đánh giá mức N." dựng từ các dòng nhiệm vụ đã nhập, đứng trước
                // phần nhận xét chung do chủ hồ sơ viết.
                Content = index == 0
                    ? FormText.OrNull(string.Join("\n\n", new[] { structured, texts.GetValueOrDefault(section.Code) }
                        .Where(t => !string.IsNullOrWhiteSpace(t))))
                    : FormText.OrNull(texts.GetValueOrDefault(section.Code)),
                Note = section.Note ?? string.Empty
            }).ToList(),
            GeneralScore = Mau09Shared.Score(source, record.GeneralCriteriaScore, rounding.GeneralTotal),
            TasksScore = Mau09Shared.Score(source, record.TasksScore, rounding.TasksTotal),
            TotalScore = Mau09Shared.Score(source, record.TotalSelfScore, rounding.Total),
            SelfGrade = Mau09Shared.SelfGrade(record)
        };
    }

    /// <summary>
    /// Dựng phần tự đánh giá theo trục từ các dòng nhiệm vụ: "n. Tên trục" rồi "Nhiệm vụ k: nội dung. Tự đánh giá mức N."
    /// (bỏ câu mức nếu chưa chọn). Không có dòng nào → chuỗi rỗng.
    /// </summary>
    internal static string BuildTaskLines(CriteriaSetContent criteria, IReadOnlyList<TaskResultRow> rows)
    {
        var blocks = new List<string>();
        for (var i = 0; i < criteria.Axes.Count; i++)
        {
            var axis = criteria.Axes[i];
            var own = rows.Where(r => string.Equals(r.AxisCode, axis.Code, StringComparison.OrdinalIgnoreCase)
                                      && !string.IsNullOrWhiteSpace(r.Content)).ToList();
            if (own.Count == 0)
                continue;
            var lines = new List<string> { $"{i + 1}. {axis.Name}" };
            for (var k = 0; k < own.Count; k++)
            {
                var r = own[k];
                var text = EndSentence(r.Content);
                if (!string.IsNullOrWhiteSpace(r.Status))
                    text += " " + EndSentence(r.Status);
                if (r.Level.HasValue && criteria.TaskLevels.Any(l => l.Code == r.Level.Value) && !string.IsNullOrWhiteSpace(criteria.TaskLevelPhrase))
                    text += " " + criteria.TaskLevelPhrase.Replace("{level}", r.Level.Value.ToString(CultureInfo.InvariantCulture));
                var prefix = string.IsNullOrWhiteSpace(criteria.TaskLineLabel) ? "{n}." : criteria.TaskLineLabel;
                lines.Add($"{prefix.Replace("{n}", (k + 1).ToString(CultureInfo.InvariantCulture))} {text}");
            }
            blocks.Add(string.Join("\n", lines));
        }
        return string.Join("\n", blocks);
    }

    private static string EndSentence(string? value)
    {
        var t = (value ?? string.Empty).Trim().Replace("\r\n", " ").Replace('\n', ' ');
        return t.Length == 0 || ".!?;:".Contains(t[^1]) ? t : t + ".";
    }
}

/// <summary>Một mục tự đánh giá của Mẫu 09C.</summary>
public sealed class Mau09CSection
{
    [TemplateField("S_TITLE")] public string? Title { get; init; }
    [TemplateField("S_INTRO")] public string? Intro { get; init; }
    [TemplateField("S_CONTENT")] public string? Content { get; init; }
    [TemplateField("S_NOTE")] public string? Note { get; init; }
}
