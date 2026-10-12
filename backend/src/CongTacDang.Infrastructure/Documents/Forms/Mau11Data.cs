using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 11 — Phiếu đánh giá, xếp loại cán bộ quý (bỏ phiếu kín). Template: <c>Mau_11_PhieuBoPhieuChiBo.docx</c>.
/// Danh sách chia hai mục theo thẩm quyền quyết định ảnh chụp trên hồ sơ: I — Ban Thường vụ Đảng ủy cấp trên, II — đảng ủy/chi ủy
/// cơ sở; mục không có cán bộ thì không in (cả hai mục rỗng → in phiếu trắng với dòng trống của biểu mẫu).
/// Cột (5)–(7) là mức đề xuất đã ghi nhận ở bước tập thể lãnh đạo, thẩm định, cấp có thẩm quyền trực tiếp (bước do cấp trên thực
/// hiện lấy kết quả ghi nhận); bước chưa ghi nhận thì để trống — phiếu in cho hội nghị tập thể lãnh đạo (trước các bước này) chỉ
/// có cột (1)–(4c).
/// </summary>
public sealed class Mau11Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_11_PhieuBoPhieuChiBo.docx";

    /// <summary>Tên tổ chức Đảng lập phiếu (chữ hoa); null giữ chữ mặc định trong template.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    [TemplateField("PERIOD_QUARTER_YEAR")] public string? PeriodQuarterYear { get; init; }

    /// <summary>Mục I: cán bộ thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của Ban Thường vụ Đảng ủy cấp trên.</summary>
    [TemplateCollection("SUPERIOR_ROWS")] public List<Mau11Row> SuperiorRows { get; init; } = new();
    [TemplateCondition("HAS_SUPERIOR_ROWS")] public bool HasSuperiorRows { get; init; }

    /// <summary>In mục I: có cán bộ của mục, hoặc phiếu trắng (cả hai mục rỗng).</summary>
    [TemplateCondition("SHOW_SUPERIOR")] public bool ShowSuperior { get; init; }

    /// <summary>Mục II: cán bộ thuộc thẩm quyền quyết định, phê duyệt mức xếp loại của đảng ủy/chi ủy cơ sở.</summary>
    [TemplateCollection("BASE_ROWS")] public List<Mau11Row> BaseRows { get; init; } = new();
    [TemplateCondition("HAS_BASE_ROWS")] public bool HasBaseRows { get; init; }

    /// <summary>In mục II: có cán bộ của mục, hoặc phiếu trắng (cả hai mục rỗng).</summary>
    [TemplateCondition("SHOW_BASE")] public bool ShowBase { get; init; }

    /// <summary>Cấp ủy cơ sở ở tiêu đề mục II ("… CỦA ĐẢNG ỦY/CHI ỦY CƠ SỞ…"), chữ in hoa; null giữ chữ mặc định.</summary>
    [TemplateField("BASE_AUTHORITY")] public string? BaseAuthority { get; init; }

    /// <summary>
    /// Dựng dữ liệu mẫu. <paramref name="lines"/>: hồ sơ (nạp kèm Member) kèm chuỗi chức vụ, đơn vị công tác, đã xếp thứ tự in;
    /// <paramref name="issuer"/>: tên tổ chức Đảng lập phiếu — dòng tiêu đề trái và cấp ủy ở tiêu đề mục II.
    /// </summary>
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
            // Quý in số La Mã như các biểu mẫu khác và văn bản HD03 ("QUÝ IV NĂM 2026").
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

    /// <summary>Chuỗi chức vụ mặc định khi không có chức vụ trong danh mục: chức danh trên hồ sơ cán bộ — đơn vị công tác.</summary>
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

    /// <summary>Mức đề xuất của một bước: đã ghi nhận (trong hệ thống hoặc kết quả của cấp trên) → tên mức; chưa → trống.</summary>
    private static string Recorded(DateTime? recordedAt, EvaluationGrade grade) =>
        recordedAt.HasValue ? FormText.Grade(grade) : string.Empty;
}

/// <summary>Một hồ sơ in trên Mẫu 11 kèm chuỗi chức vụ (Đảng, chính quyền, đoàn thể), đơn vị công tác.</summary>
public sealed record Mau11Line(EvaluationRecord Record, string? PositionAndUnit);

/// <summary>Một dòng cán bộ của Mẫu 11 (cùng tag cho khối lặp mục I và mục II).</summary>
public sealed class Mau11Row
{
    [TemplateField("R_STT")] public string? Order { get; init; }
    [TemplateField("R_NAME")] public string? Name { get; init; }

    /// <summary>Cột (3) Chức vụ, đơn vị công tác (Đảng, chính quyền, đoàn thể).</summary>
    [TemplateField("R_POSITION_DEPT")] public string? PositionAndDepartment { get; init; }

    /// <summary>Cột (4a) Điểm nhóm tiêu chí chung — giá trị đã lưu; chưa tự chấm → trống.</summary>
    [TemplateField("R_GENERAL_SCORE")] public string? GeneralScore { get; init; }

    /// <summary>Cột (4b) Điểm nhóm tiêu chí kết quả thực hiện nhiệm vụ — giá trị đã lưu; chưa tự chấm → trống.</summary>
    [TemplateField("R_TASKS_SCORE")] public string? TasksScore { get; init; }

    /// <summary>Cột (4c) Mức tự xếp loại đề xuất; chưa tự chấm → trống.</summary>
    [TemplateField("R_SELF_GRADE")] public string? SelfGrade { get; init; }

    /// <summary>Cột (5) Tập thể lãnh đạo, quản lý đề xuất.</summary>
    [TemplateField("R_COLLECTIVE_GRADE")] public string? CollectiveGrade { get; init; }

    /// <summary>Cột (6) Cơ quan tham mưu về tổ chức, cán bộ (thẩm định) đề xuất.</summary>
    [TemplateField("R_APPRAISAL_GRADE")] public string? AppraisalGrade { get; init; }

    /// <summary>Cột (7) Cấp có thẩm quyền trực tiếp sử dụng cán bộ đề xuất.</summary>
    [TemplateField("R_DIRECTOR_GRADE")] public string? DirectorGrade { get; init; }
}
