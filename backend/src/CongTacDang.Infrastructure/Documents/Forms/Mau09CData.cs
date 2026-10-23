using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Mẫu 09C: mục đầu tiên gồm nhận xét chung và nhiệm vụ theo trục; phần II lấy điểm, mức tự đề xuất đã lưu.
/// Hướng dẫn viết của mục (chữ đỏ trên mẫu gốc) chỉ hiện trên màn hình nhập, không in.
/// </summary>
public sealed class Mau09CData
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public const string TemplateFileName = "Mau_09C_BanTuDanhGia.docx";

    /// <summary>In hoa.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    /// <summary>Ngày nộp phiếu tự chấm; chưa nộp → giữ dòng chấm.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>Quý dạng La Mã.</summary>
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }

    /// <summary>In hoa.</summary>
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }

    /// <summary>dd/MM/yyyy; chưa nhập → giữ dòng chấm.</summary>
    [TemplateField("DATE_OF_BIRTH")] public string? DateOfBirth { get; init; }

    [TemplateField("PARTY_POSITION")] public string? PartyPosition { get; init; }

    /// <summary>Không có chức vụ nào trong danh mục thì lấy chức danh trên hồ sơ cán bộ.</summary>
    [TemplateField("ADMIN_POSITION")] public string? AdministrativePosition { get; init; }
    [TemplateField("MASS_POSITION")] public string? MassPosition { get; init; }

    /// <summary>Tên công ty lấy từ tag dùng chung ORG_COMPANY_NAME.</summary>
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Đơn vị gốc (lãnh đạo Công ty) → false, chỉ in tên công ty.</summary>
    [TemplateCondition("HAS_DEPARTMENT")] public bool HasDepartment { get; init; }

    [TemplateCollection("SECTIONS")] public List<Mau09CSection> Sections { get; init; } = new();

    [TemplateField("GENERAL_SCORE")] public string? GeneralScore { get; init; }
    [TemplateField("TASKS_SCORE")] public string? TasksScore { get; init; }
    [TemplateField("TOTAL_SCORE")] public string? TotalScore { get; init; }
    [TemplateField("SELF_GRADE")] public string? SelfGrade { get; init; }

    /// <summary>Chữ thường, dưới dòng ký.</summary>
    [TemplateField("SIGNER_NAME")] public string? SignerName { get; init; }

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
            .Select(b => new Mau09CAxis
            {
                Title = b.Title,
                Lines = b.Lines.Select(l => new Mau09CLine { Text = l.Text, Level = l.Level == null ? string.Empty : " " + l.Level }).ToList()
            })
            .ToList();

        var lines = Mau09Shared.PositionLines(source);
        var fullName = FormText.OrNull(record.Member?.FullName?.Trim());
        return new Mau09CData
        {
            PartyCell = Mau09Shared.PartyCellUpper(record),
            DateLine = Mau09Shared.DateLine(source),
            Quarter = record.Period != null ? CollectiveFormText.RomanQuarter(record.Period.Quarter) : null,
            Year = Mau09Shared.Year(record),
            FullName = FormText.Spaced(fullName?.ToUpper(Vietnamese)),
            DateOfBirth = Mau09Shared.DateOfBirth(record),
            PartyPosition = lines.Party,
            AdministrativePosition = lines.Administrative,
            MassPosition = lines.MassOrganization,
            Department = Mau09Shared.Department(record),
            HasDepartment = Mau09Shared.HasDepartment(record),
            Sections = source.Criteria.SelfAssessmentSections.Select((section, index) =>
            {
                // Nhận xét chung thay cho câu dẫn của mục; mục đầu tiên thêm các khối nhiệm vụ theo trục.
                // Chưa nhập gì → giữ câu dẫn và dòng chấm để viết tay.
                var text = FormText.OrNull(texts.GetValueOrDefault(section.Code)?.Trim());
                var axes = index == 0 ? axisBlocks : new List<Mau09CAxis>();
                var intro = text ?? FormText.OrNull(section.Guidance?.Trim());
                return new Mau09CSection
                {
                    Title = section.Title,
                    Intro = intro ?? string.Empty,
                    HasIntro = intro != null,
                    ShowDots = text == null && axes.Count == 0,
                    Axes = axes
                };
            }).ToList(),
            GeneralScore = Mau09Shared.CompactScore(source, record.GeneralCriteriaScore, rounding.GeneralTotal),
            TasksScore = Mau09Shared.CompactScore(source, record.TasksScore, rounding.TasksTotal),
            TotalScore = Mau09Shared.CompactScore(source, record.TotalSelfScore, rounding.Total),
            SelfGrade = Mau09Shared.SelfGrade(record),
            SignerName = fullName
        };
    }
}

public sealed class Mau09CSection
{
    [TemplateField("S_TITLE")] public string? Title { get; init; }

    /// <summary>Nhận xét chung đã nhập; chưa nhập thì câu dẫn của mục.</summary>
    [TemplateField("S_INTRO")] public string? Intro { get; init; }

    /// <summary>Không có đoạn đầu thì bỏ đoạn, không để dòng trống.</summary>
    [TemplateCondition("S_HAS_INTRO")] public bool HasIntro { get; init; }

    /// <summary>Mục chưa nhập nhận xét và không có nhiệm vụ theo trục.</summary>
    [TemplateCondition("S_SHOW_DOTS")] public bool ShowDots { get; init; }

    /// <summary>Chỉ mục đầu tiên.</summary>
    [TemplateCollection("S_AXES")] public List<Mau09CAxis> Axes { get; init; } = new();
}

/// <summary>Tiêu đề "n. Tên trục (trục n)" (in đậm) và các dòng nhiệm vụ / ý con.</summary>
public sealed class Mau09CAxis
{
    [TemplateField("AX_TITLE")] public string? Title { get; init; }
    [TemplateCollection("AX_LINES")] public List<Mau09CLine> Lines { get; init; } = new();
}

/// <summary>"- Nhiệm vụ k: … Tự đánh giá mức N." hoặc ý con "+ …" (một đoạn văn).</summary>
public sealed class Mau09CLine
{
    [TemplateField("L_TEXT")] public string? Text { get; init; }

    /// <summary>In nghiêng, có khoảng trắng đầu; rỗng khi không kết luận mức.</summary>
    [TemplateField("L_LEVEL")] public string? Level { get; init; }
}
