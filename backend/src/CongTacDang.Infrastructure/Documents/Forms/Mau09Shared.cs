using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Chức vụ đang giữ của cán bộ theo từng khối (Đảng, chính quyền, đoàn thể) — dùng ở phần thông tin chung Mẫu 09A–9D.</summary>
/// <param name="Party">Chức vụ Đảng (ngăn cách bằng dấu phẩy); null nếu không có.</param>
/// <param name="Administrative">Chức vụ chính quyền.</param>
/// <param name="MassOrganization">Chức vụ đoàn thể.</param>
public sealed record MemberPositionNames(string? Party, string? Administrative, string? MassOrganization)
{
    /// <summary>Không có chức vụ nào.</summary>
    public static MemberPositionNames Empty { get; } = new(null, null, null);

    /// <summary>Dựng từ danh sách (khối, tên chức vụ) theo thứ tự hiển thị.</summary>
    public static MemberPositionNames From(IEnumerable<(PositionSide Side, string Name)> positions)
    {
        var list = positions.Where(p => !string.IsNullOrWhiteSpace(p.Name)).ToList();
        string? Join(PositionSide side)
        {
            var names = list.Where(p => p.Side == side).Select(p => p.Name.Trim()).Distinct().ToList();
            return names.Count == 0 ? null : string.Join(", ", names);
        }
        return new MemberPositionNames(Join(PositionSide.Party), Join(PositionSide.Administrative), Join(PositionSide.MassOrganization));
    }

    /// <summary>Mọi chức vụ theo thứ tự in trên Mẫu 09B "(Đảng, Đoàn thể, Chính quyền)"; null nếu không có.</summary>
    public string? All()
    {
        var parts = new[] { Party, MassOrganization, Administrative }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }
}

/// <summary>
/// Dữ liệu nguồn của các biểu mẫu cá nhân 09A/09B/09C/9D (task 18 — T-83): hồ sơ đã lưu (nạp kèm Period, Member, Department,
/// PartyCell, Tasks), bộ tiêu chí của kỳ (ảnh chụp) và chức vụ đang giữ. Lớp dữ liệu mẫu chỉ đọc giá trị đã lưu, không tính lại điểm.
/// </summary>
/// <param name="Record">Hồ sơ đánh giá.</param>
/// <param name="Criteria">Nội dung bộ tiêu chí của kỳ (ảnh chụp).</param>
/// <param name="Positions">Chức vụ đang giữ.</param>
public sealed record IndividualFormSource(EvaluationRecord Record, CriteriaSetContent Criteria, MemberPositionNames Positions)
{
    /// <summary>Đã nộp phiếu tự chấm (có điểm lưu) — chưa nộp thì các ô điểm giữ chữ mặc định.</summary>
    public bool HasSelfScore => Record.SelfScoredAt.HasValue;
}

/// <summary>Một nhóm tiêu chí chung (dòng nhóm của bảng Nhóm I, Mẫu 09A/09B).</summary>
public sealed class GeneralGroupRow
{
    [TemplateField("G_NO")] public string? Number { get; init; }
    [TemplateField("G_NAME")] public string? Name { get; init; }
    [TemplateField("G_MAX")] public string? MaxScore { get; init; }
    [TemplateCollection("ITEMS")] public List<GeneralItemRow> Items { get; init; } = new();
}

/// <summary>Một tiêu chí con (dòng của bảng Nhóm I, Mẫu 09A/09B).</summary>
public sealed class GeneralItemRow
{
    [TemplateField("I_CODE")] public string? Code { get; init; }
    [TemplateField("I_TEXT")] public string? Text { get; init; }

    /// <summary>"X" ở cột "Đảm bảo".</summary>
    [TemplateField("I_MET")] public string? Met { get; init; }

    /// <summary>"X" ở cột "Không đảm bảo".</summary>
    [TemplateField("I_NOT_MET")] public string? NotMet { get; init; }

    [TemplateField("I_MAX")] public string? MaxScore { get; init; }

    /// <summary>Điểm đạt đã lưu; "K/AD" khi không áp dụng.</summary>
    [TemplateField("I_SCORE")] public string? Score { get; init; }

    /// <summary>Lý do/căn cứ đã lưu (cột "Tóm tắt kết quả/Tài liệu minh chứng; Ghi chú khác").</summary>
    [TemplateField("I_NOTE")] public string? Note { get; init; }
}

/// <summary>Phần dùng chung khi dựng dữ liệu Mẫu 09A/09B/09C/9D.</summary>
internal static class Mau09Shared
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private static readonly NumberFormatInfo CompactNumbers = new() { NumberDecimalSeparator = ",", NumberGroupSeparator = "." };

    /// <summary>Số gọn (bỏ số 0 thừa, dấu phẩy thập phân) như chữ in sẵn "18", "2", "1,5".</summary>
    public static string Compact(double value) => value.ToString("0.##", CompactNumbers);

    /// <summary>Tên Chi bộ in hoa ở tiêu đề trái (giống Mẫu 11, 13); null giữ chữ mặc định.</summary>
    public static string? PartyCellUpper(EvaluationRecord record) => FormText.OrNull(record.PartyCell?.Name)?.ToUpper(Vietnamese);

    /// <summary>Quý của kỳ; null nếu không nạp kỳ.</summary>
    public static string? Quarter(EvaluationRecord record) => record.Period != null ? FormText.Quarter(record.Period.Quarter) : null;

    /// <summary>Năm của kỳ; null nếu không nạp kỳ.</summary>
    public static string? Year(EvaluationRecord record) => record.Period != null ? FormText.Year(record.Period.Year) : null;

    /// <summary>Điểm đã lưu theo số chữ số làm tròn của bộ tiêu chí; chưa tự chấm → null.</summary>
    public static string? Score(IndividualFormSource source, double value, RoundingRule rule) =>
        source.HasSelfScore ? FormText.Number(value, Math.Clamp(rule.Decimals, 0, 4)) : null;

    /// <summary>Mức tự đề xuất đã lưu; chưa có → null.</summary>
    public static string? SelfGrade(EvaluationRecord record) =>
        record.SelfProposedGrade == EvaluationGrade.ChuaXepLoai ? null : FormText.Grade(record.SelfProposedGrade);

    /// <summary>
    /// Bảng nhóm tiêu chí chung theo bộ tiêu chí của kỳ và điểm đã lưu: "X" ở cột Đảm bảo khi đạt tối đa, ở cột Không đảm bảo khi
    /// 0 điểm (nhóm chấm "Đảm bảo/Không đảm bảo": mọi mức dưới tối đa); "K/AD" khi không áp dụng.
    /// </summary>
    public static List<GeneralGroupRow> GeneralGroups(IndividualFormSource source)
    {
        Dictionary<string, GeneralItemScore> scores;
        try
        {
            scores = new Dictionary<string, GeneralItemScore>(EvaluationScoring.ParseGeneralScores(source.Record.GeneralScores), StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            scores = new Dictionary<string, GeneralItemScore>(StringComparer.OrdinalIgnoreCase);
        }

        return source.Criteria.GeneralGroups.Select(group => new GeneralGroupRow
        {
            Number = group.Code,
            Name = group.Name,
            MaxScore = Compact(group.MaxScore),
            Items = group.Items.Select(item =>
            {
                if (!scores.TryGetValue(item.Code, out var s) || s == null)
                    return new GeneralItemRow { Code = item.Code, Text = item.Text, MaxScore = Compact(item.MaxScore), Met = "", NotMet = "", Score = "", Note = "" };
                if (s.NotApplicable)
                    return new GeneralItemRow { Code = item.Code, Text = item.Text, MaxScore = Compact(item.MaxScore), Met = "", NotMet = "", Score = "K/AD", Note = s.Reason?.Trim() ?? "" };

                var full = s.Score + CriteriaSetContent.Epsilon >= item.MaxScore;
                var notMet = group.ScoringMode == CriteriaScoringMode.Binary ? !full : s.Score <= CriteriaSetContent.Epsilon;
                return new GeneralItemRow
                {
                    Code = item.Code,
                    Text = item.Text,
                    MaxScore = Compact(item.MaxScore),
                    Met = full ? "X" : "",
                    NotMet = notMet ? "X" : "",
                    Score = Compact(s.Score),
                    Note = s.Reason?.Trim() ?? ""
                };
            }).ToList()
        }).ToList();
    }
}
