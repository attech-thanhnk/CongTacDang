using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Mục I (cấp trên quyết định) / II (cơ sở); mục rỗng không in, cả hai rỗng → in phiếu trắng.
/// Cột (5)–(7) là mức đã ghi nhận của từng bước; chưa ghi nhận thì để trống.
/// </summary>
public sealed class Mau11Data
{
    public const string TemplateFileName = "Mau_11_PhieuBoPhieuChiBo.docx";

    /// <summary>Chữ hoa; null giữ chữ mặc định.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    [TemplateField("PERIOD_QUARTER_YEAR")] public string? PeriodQuarterYear { get; init; }

    /// <summary>Mục I: Ban Thường vụ Đảng ủy cấp trên quyết định.</summary>
    [TemplateCollection("SUPERIOR_ROWS")] public List<Mau11Row> SuperiorRows { get; init; } = new();
    [TemplateCondition("HAS_SUPERIOR_ROWS")] public bool HasSuperiorRows { get; init; }

    /// <summary>Có cán bộ của mục, hoặc phiếu trắng.</summary>
    [TemplateCondition("SHOW_SUPERIOR")] public bool ShowSuperior { get; init; }

    /// <summary>Mục II: đảng ủy/chi ủy cơ sở quyết định.</summary>
    [TemplateCollection("BASE_ROWS")] public List<Mau11Row> BaseRows { get; init; } = new();
    [TemplateCondition("HAS_BASE_ROWS")] public bool HasBaseRows { get; init; }

    /// <summary>Có cán bộ của mục, hoặc phiếu trắng.</summary>
    [TemplateCondition("SHOW_BASE")] public bool ShowBase { get; init; }

    /// <summary>Tiêu đề mục II, chữ in hoa; null giữ chữ mặc định.</summary>
    [TemplateField("BASE_AUTHORITY")] public string? BaseAuthority { get; init; }

    /// <summary><paramref name="lines"/> đã xếp thứ tự in, nạp kèm Member.</summary>
    public static Mau11Data From(EvaluationPeriod period, IReadOnlyList<Mau11Line> lines, string? issuer)
    {
        List<Mau11Row> Rows(ApprovalAuthority authority) => lines
            .Where(l => l.Record.ApprovalAuthority == authority)
            .Select((l, i) => Row(l, i + 1))
            .ToList();

        var superior = Rows(ApprovalAuthority.CapTren);
        var baseRows = Rows(ApprovalAuthority.CoSo);
        var blank = superior.Count == 0 && baseRows.Count == 0;
        return new Mau11Data
        {
            PartyCell = PartyHeader.Upper(issuer),
            // Quý in số La Mã ("QUÝ IV NĂM 2026").
            PeriodQuarterYear = $"QUÝ {CollectiveFormText.RomanQuarter(period.Quarter)} NĂM {FormText.Year(period.Year)}",
            SuperiorRows = superior,
            HasSuperiorRows = superior.Count > 0,
            ShowSuperior = superior.Count > 0 || blank,
            BaseRows = baseRows,
            HasBaseRows = baseRows.Count > 0,
            ShowBase = baseRows.Count > 0 || blank,
            BaseAuthority = CollectiveFormText.CommitteeUpper(issuer)
        };
    }

    /// <summary>Khi không có chức vụ trong danh mục: chức danh trên hồ sơ — đơn vị công tác.</summary>
    public static string? DefaultPosition(EvaluationRecord record)
    {
        var unit = record.Department?.Name ?? record.Member?.Department?.Name;
        var text = string.Join(" — ", new[] { record.Member?.PositionTitle, unit }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return FormText.OrNull(text);
    }

    private static Mau11Row Row(Mau11Line line, int order)
    {
        var r = line.Record;
        var scored = r.SelfScoredAt.HasValue;
        return new Mau11Row
        {
            Order = order.ToString(CultureInfo.InvariantCulture),
            Name = r.Member?.FullName?.Trim() ?? string.Empty,
            PositionAndDepartment = FormText.OrNull(line.PositionAndUnit) ?? DefaultPosition(r) ?? string.Empty,
            GeneralScore = scored ? CollectiveFormText.Score(r.GeneralCriteriaScore) : string.Empty,
            TasksScore = scored ? CollectiveFormText.Score(r.TasksScore) : string.Empty,
            SelfGrade = scored ? FormText.Grade(r.SelfProposedGrade) : string.Empty,
            CollectiveGrade = Recorded(r.CollectiveRecordedAt, r.CollectiveProposedGrade),
            AppraisalGrade = Recorded(r.AppraisedAt, r.AppraisalProposedGrade),
            DirectorGrade = Recorded(r.DirectorReviewedAt, r.DirectorProposedGrade)
        };
    }

    /// <summary>Bước đã ghi nhận (kể cả kết quả của cấp trên) → tên mức; chưa → trống.</summary>
    private static string Recorded(DateTime? recordedAt, EvaluationGrade grade) =>
        recordedAt.HasValue ? FormText.Grade(grade) : string.Empty;
}

public sealed record Mau11Line(EvaluationRecord Record, string? PositionAndUnit);

/// <summary>Cùng tag cho khối lặp mục I và mục II.</summary>
public sealed class Mau11Row
{
    [TemplateField("R_STT")] public string? Order { get; init; }
    [TemplateField("R_NAME")] public string? Name { get; init; }

    /// <summary>Cột (3).</summary>
    [TemplateField("R_POSITION_DEPT")] public string? PositionAndDepartment { get; init; }

    /// <summary>Cột (4a); chưa tự chấm → trống.</summary>
    [TemplateField("R_GENERAL_SCORE")] public string? GeneralScore { get; init; }

    /// <summary>Cột (4b); chưa tự chấm → trống.</summary>
    [TemplateField("R_TASKS_SCORE")] public string? TasksScore { get; init; }

    /// <summary>Cột (4c); chưa tự chấm → trống.</summary>
    [TemplateField("R_SELF_GRADE")] public string? SelfGrade { get; init; }

    /// <summary>Cột (5).</summary>
    [TemplateField("R_COLLECTIVE_GRADE")] public string? CollectiveGrade { get; init; }

    /// <summary>Cột (6).</summary>
    [TemplateField("R_APPRAISAL_GRADE")] public string? AppraisalGrade { get; init; }

    /// <summary>Cột (7).</summary>
    [TemplateField("R_DIRECTOR_GRADE")] public string? DirectorGrade { get; init; }
}
