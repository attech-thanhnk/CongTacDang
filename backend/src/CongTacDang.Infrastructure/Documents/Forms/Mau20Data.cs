using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Biên bản hiệu chuẩn kết quả đánh giá của một nhóm chức danh / công việc tương đồng.</summary>
public sealed class Mau20Data
{
    public const string TemplateFileName = "Mau_20_BienBanHieuChuan.docx";

    /// <summary>Ngày họp; chưa ghi thì ngày chốt, ngày lập biên bản.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }

    /// <summary>"08h30, ngày 15/09/2026, tại …" theo giờ Việt Nam.</summary>
    [TemplateField("MEETING_TIME_PLACE")] public string? MeetingTimePlace { get; init; }

    [TemplateField("CHAIR")] public string? Chair { get; init; }

    /// <summary>Thành phần dự, mỗi người "Họ tên (tư cách)".</summary>
    [TemplateField("ATTENDEES")] public string? Attendees { get; init; }

    /// <summary>Phạm vi, số lượng hồ sơ: phần người lập nhập, chưa có thì gợi ý sinh từ danh sách hồ sơ.</summary>
    [TemplateField("SCOPE_TEXT")] public string? Scope { get; init; }

    [TemplateField("EXTRA_CONCLUSION")] public string? ExtraConclusion { get; init; }

    [TemplateCondition("HAS_EXTRA_CONCLUSION")] public bool HasExtraConclusion { get; init; }

    [TemplateField("CHAIR_NAME")] public string? ChairName { get; init; }

    [TemplateField("SECRETARY_NAME")] public string? SecretaryName { get; init; }

    [TemplateCondition("HAS_MEMBERS")] public bool HasMembers { get; init; }

    /// <summary>Chưa chốt thì biên bản in dòng cảnh báo bản nháp ở đầu văn bản.</summary>
    [TemplateCondition("DRAFT")] public bool Draft { get; init; }

    [TemplateCollection("SUMMARY")] public List<Mau20SummaryRow> Summary { get; init; } = new();

    [TemplateCollection("CASES")] public List<Mau20CaseRow> Cases { get; init; } = new();

    [TemplateCollection("MEMBERS")] public List<Mau20MemberRow> Members { get; init; } = new();

    public static Mau20Data From(CalibrationSessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var held = session.HeldAt;
        var extra = FormText.OrNull(session.ExtraConclusion?.Trim());
        var concluded = session.Status == nameof(CalibrationStatus.Concluded);
        return new Mau20Data
        {
            DateLine = CollectiveFormText.DateLine(held ?? session.ConcludedAt ?? session.PreparedAt),
            MeetingTimePlace = TimePlace(held, session.Location),
            Chair = FormText.OrNull(session.ChairName?.Trim()),
            Attendees = Join(session.Attendees.OrderBy(a => a.Order).Select(a => Mau20MemberRow.NameOf(a))),
            Scope = FormText.OrNull(session.ScopeNote?.Trim()) ?? FormText.OrNull(session.ScopeSuggestion?.Trim()),
            ExtraConclusion = extra,
            HasExtraConclusion = extra != null,
            ChairName = FormText.OrNull(session.ChairName?.Trim()),
            SecretaryName = FormText.OrNull(session.SecretaryName?.Trim()),
            HasMembers = session.Attendees.Count > 0,
            // Mục 1: số liệu gốc trước hiệu chuẩn; biên bản đã chốt kèm số liệu sau hiệu chuẩn.
            Draft = !concluded,
            Summary = session.Summary.Select(r => Mau20SummaryRow.From(r, concluded)).ToList(),
            Cases = session.Cases.OrderBy(c => c.Order).Select((c, index) => Mau20CaseRow.From(c, index + 1)).ToList(),
            Members = session.Attendees.OrderBy(a => a.Order).Select(Mau20MemberRow.From).ToList()
        };
    }

    private static string? Join(IEnumerable<string> values)
    {
        var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        return list.Count == 0 ? null : string.Join("; ", list);
    }

    private static string? TimePlace(DateTime? held, string? location)
    {
        var parts = new List<string>();
        if (held.HasValue)
            parts.Add($"{CollectiveFormText.Time(held.Value)}, ngày {CollectiveFormText.Date(held.Value)}");
        if (!string.IsNullOrWhiteSpace(location))
            parts.Add("tại " + location.Trim());
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    /// <summary>Mức lưu dạng tên enum → tên đầy đủ; chưa xếp loại → trống.</summary>
    public static string GradeText(string? grade) =>
        EvaluationMapping.ParseGrade(grade) is { } parsed && parsed != EvaluationGrade.ChuaXepLoai ? FormText.Grade(parsed) : string.Empty;

    public static string ScoreText(double? score) => score.HasValue ? CollectiveFormText.Score(score.Value) : string.Empty;
}

public sealed class Mau20SummaryRow
{
    [TemplateField("S_GROUP")] public string? Group { get; init; }
    [TemplateField("S_COUNT")] public string? Count { get; init; }
    [TemplateField("S_PCT")] public string? Percent { get; init; }
    [TemplateField("S_DIFF")] public string? Difference { get; init; }
    [TemplateField("S_CONCLUSION")] public string? Conclusion { get; init; }

    /// <summary>Biên bản đã chốt thì kèm số liệu sau hiệu chuẩn.</summary>
    [TemplateCondition("S_HAS_AFTER")] public bool HasAfter { get; init; }
    [TemplateField("S_COUNT_AFTER")] public string? CountAfter { get; init; }
    [TemplateField("S_PCT_AFTER")] public string? PercentAfter { get; init; }

    public static Mau20SummaryRow From(CalibrationSummaryRowDto row, bool concluded) => new()
    {
        Group = row.GradeName,
        Count = row.CountBefore.ToString(CultureInfo.InvariantCulture),
        Percent = row.PercentBefore.HasValue ? FormText.Number(row.PercentBefore.Value, 1) : string.Empty,
        Difference = row.NotableDifference?.Trim() ?? string.Empty,
        Conclusion = row.PreliminaryConclusion?.Trim() ?? string.Empty,
        HasAfter = concluded,
        CountAfter = row.CountAfter.ToString(CultureInfo.InvariantCulture),
        PercentAfter = FormText.Number(row.PercentAfter ?? 0, 1)
    };
}

public sealed class Mau20CaseRow
{
    [TemplateField("C_STT")] public string? Order { get; init; }
    [TemplateField("C_NAME")] public string? Name { get; init; }
    [TemplateField("C_BEFORE")] public string? Before { get; init; }
    [TemplateField("C_CLARIFY")] public string? Clarification { get; init; }
    [TemplateField("C_AFTER")] public string? After { get; init; }

    /// <summary>Căn cứ, kèm người chịu trách nhiệm.</summary>
    [TemplateField("C_BASIS")] public string? Basis { get; init; }

    public static Mau20CaseRow From(CalibrationCaseDto c, int order) => new()
    {
        Order = order.ToString(CultureInfo.InvariantCulture),
        Name = c.FullName,
        Before = Result(c.GradeBefore, c.ScoreBefore),
        Clarification = c.Clarification?.Trim() ?? string.Empty,
        After = c.Changed ? Result(c.GradeAfter, c.ScoreAfter) : "Giữ nguyên",
        Basis = string.Join("\n", new[]
        {
            c.Basis?.Trim(),
            string.IsNullOrWhiteSpace(c.ResponsibleName) ? null : "Người chịu trách nhiệm: " + c.ResponsibleName.Trim()
        }.Where(s => !string.IsNullOrEmpty(s)))
    };

    private static string Result(string? grade, double? score)
    {
        var parts = new[] { Mau20Data.GradeText(grade), score.HasValue ? $"({Mau20Data.ScoreText(score)} điểm)" : string.Empty }
            .Where(s => s.Length > 0);
        return string.Join(" ", parts);
    }
}

public sealed class Mau20MemberRow
{
    [TemplateField("M_NAME")] public string? Name { get; init; }
    [TemplateField("M_DISSENT")] public string? Dissent { get; init; }
    [TemplateCondition("M_HAS_DISSENT")] public bool HasDissent { get; init; }

    public static string NameOf(CalibrationAttendeeDto attendee) =>
        string.IsNullOrWhiteSpace(attendee.Capacity) ? attendee.FullName.Trim() : $"{attendee.FullName.Trim()} ({attendee.Capacity.Trim()})";

    public static Mau20MemberRow From(CalibrationAttendeeDto attendee) => new()
    {
        Name = NameOf(attendee),
        Dissent = attendee.DissentNote?.Trim() ?? string.Empty,
        HasDissent = !string.IsNullOrWhiteSpace(attendee.DissentNote)
    };
}
