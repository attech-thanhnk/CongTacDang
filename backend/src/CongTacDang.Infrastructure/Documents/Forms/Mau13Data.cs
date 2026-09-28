using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 13 — Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý.
/// Template: <c>Mau_13_BienBanKiemPhieu.docx</c>.
/// </summary>
public sealed class Mau13Data
{
    /// <summary>Tên tệp template.</summary>
    public const string TemplateFileName = "Mau_13_BienBanKiemPhieu.docx";

    /// <summary>Tên Chi bộ (chữ hoa); xuất toàn Đảng bộ thì giữ chữ mặc định trong template.</summary>
    [TemplateField("PARTY_CELL")] public string? PartyCell { get; init; }

    [TemplateField("PERIOD_QUARTER_YEAR")] public string? PeriodQuarterYear { get; init; }

    /// <summary>Số đảng viên bỏ phiếu đã lưu (dùng cho số phiếu phát ra / thu về / hợp lệ như bản cũ); không có thì giữ chữ mặc định.</summary>
    [TemplateField("TOTAL_VOTERS")] public string? TotalVoters { get; init; }

    /// <summary>Chưa lưu số phiếu không hợp lệ — giữ chữ mặc định trong template.</summary>
    [TemplateField("INVALID_BALLOTS")] public string? InvalidBallots { get; init; }

    [TemplateCollection("RECORDS")] public List<Mau13Row> Records { get; init; } = new();

    /// <summary>
    /// Dựng dữ liệu mẫu từ kỳ và các hồ sơ đã lưu (nạp kèm Member, Department, PartyCell).
    /// <paramref name="totalVoters"/>: số người bỏ phiếu đã lưu trên hồ sơ, hoặc sĩ số Chi bộ khi hồ sơ chưa lưu; null nếu không xác định.
    /// </summary>
    public static Mau13Data From(EvaluationPeriod period, IEnumerable<EvaluationRecord> records, string? branchName, int? totalVoters)
    {
        var voters = totalVoters is > 0 ? totalVoters : null;
        return new Mau13Data
        {
            PartyCell = FormText.OrNull(branchName)?.ToUpperInvariant(),
            PeriodQuarterYear = $"QUÝ {FormText.Quarter(period.Quarter)} NĂM {FormText.Year(period.Year)}",
            TotalVoters = voters?.ToString(),
            Records = records.Select((r, index) =>
            {
                var recordVoters = r.TotalVoters > 0 ? r.TotalVoters : voters ?? 0;
                var grade = FormText.Grade(r.PartyCellProposedGrade);
                // Tỷ lệ phiếu (xuất sắc + tốt) trên số người bỏ phiếu đã lưu; không có số người bỏ phiếu thì chỉ ghi mức xếp loại.
                var ratio = recordVoters > 0
                    ? FormText.Number(Math.Round((double)(r.VotesExcellent + r.VotesGood) / recordVoters * 100, 1), 1) + "% (" + grade + ")"
                    : grade;

                return new Mau13Row
                {
                    Order = (index + 1).ToString(),
                    Name = FormText.OrNull(r.Member?.FullName),
                    PositionAndDepartment = $"{r.Member?.PositionTitle} • {r.Department?.Name ?? r.PartyCell?.Name}",
                    VotesExcellent = $"{r.VotesExcellent} phiếu",
                    VotesGood = $"{r.VotesGood} phiếu",
                    VotesSatisfactory = $"{r.VotesSatisfactory} phiếu",
                    VotesUnsatisfactory = $"{r.VotesUnsatisfactory} phiếu",
                    Result = ratio
                };
            }).ToList()
        };
    }
}

/// <summary>Một dòng cán bộ của Mẫu 13.</summary>
public sealed class Mau13Row
{
    [TemplateField("V_STT")] public string? Order { get; init; }
    [TemplateField("V_NAME")] public string? Name { get; init; }
    [TemplateField("V_POSITION_DEPT")] public string? PositionAndDepartment { get; init; }
    [TemplateField("V_EXC")] public string? VotesExcellent { get; init; }
    [TemplateField("V_GOOD")] public string? VotesGood { get; init; }
    [TemplateField("V_SAT")] public string? VotesSatisfactory { get; init; }
    [TemplateField("V_UNSAT")] public string? VotesUnsatisfactory { get; init; }
    [TemplateField("V_PCT")] public string? Result { get; init; }
}
