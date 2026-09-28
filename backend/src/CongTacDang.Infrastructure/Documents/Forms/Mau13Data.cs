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

    /// <summary>
    /// Số phiếu không hợp lệ (task 12, B-07): lấy từ kết quả kiểm phiếu tổng hợp khi mọi hồ sơ trên biên bản có cùng số phiếu
    /// không hợp lệ; không xác định được thì giữ chữ mặc định trong template.
    /// </summary>
    [TemplateField("INVALID_BALLOTS")] public string? InvalidBallots { get; init; }

    [TemplateCollection("RECORDS")] public List<Mau13Row> Records { get; init; } = new();

    /// <summary>
    /// Dựng dữ liệu mẫu từ kỳ và các hồ sơ đã lưu (nạp kèm Member, Department, PartyCell).
    /// <paramref name="totalVoters"/>: số người bỏ phiếu (số có mặt trên biên bản, hoặc giá trị cũ lưu trên hồ sơ, hoặc sĩ số Chi bộ);
    /// null nếu không xác định. <paramref name="tallies"/>: kết quả kiểm phiếu tổng hợp theo hồ sơ (task 12 — lưu trên biên bản);
    /// hồ sơ không có thì dùng số phiếu cũ lưu trên hồ sơ (dữ liệu trước task 12).
    /// </summary>
    public static Mau13Data From(
        EvaluationPeriod period,
        IEnumerable<EvaluationRecord> records,
        string? branchName,
        int? totalVoters,
        IReadOnlyDictionary<Guid, EvaluationMeetingVoteSummary>? tallies = null)
    {
        var voters = totalVoters is > 0 ? totalVoters : null;
        var list = records.ToList();
        var invalidValues = tallies == null
            ? new List<int>()
            : list.Where(r => tallies.ContainsKey(r.Id)).Select(r => tallies[r.Id].InvalidVotes).Distinct().ToList();

        return new Mau13Data
        {
            PartyCell = FormText.OrNull(branchName)?.ToUpperInvariant(),
            PeriodQuarterYear = $"QUÝ {FormText.Quarter(period.Quarter)} NĂM {FormText.Year(period.Year)}",
            TotalVoters = voters?.ToString(),
            InvalidBallots = invalidValues.Count == 1 ? invalidValues[0].ToString() : null,
            Records = list.Select((r, index) =>
            {
                var tally = tallies != null && tallies.TryGetValue(r.Id, out var found) ? found : null;
                var excellent = tally?.VotesExcellent ?? r.VotesExcellent;
                var good = tally?.VotesGood ?? r.VotesGood;
                var satisfactory = tally?.VotesSatisfactory ?? r.VotesSatisfactory;
                var unsatisfactory = tally?.VotesUnsatisfactory ?? r.VotesUnsatisfactory;
                var recordVoters = tally == null && r.TotalVoters > 0 ? r.TotalVoters : voters ?? 0;
                // Mức đề xuất của tập thể lãnh đạo (task 12); hồ sơ cũ: mức Chi bộ đề xuất.
                var grade = FormText.Grade(r.CollectiveProposedGrade != Domain.Enums.EvaluationGrade.ChuaXepLoai
                    ? r.CollectiveProposedGrade
                    : r.PartyCellProposedGrade);
                // Tỷ lệ phiếu (xuất sắc + tốt) trên số người bỏ phiếu đã lưu; không có số người bỏ phiếu thì chỉ ghi mức xếp loại.
                var ratio = recordVoters > 0
                    ? FormText.Number(Math.Round((double)(excellent + good) / recordVoters * 100, 1), 1) + "% (" + grade + ")"
                    : grade;

                return new Mau13Row
                {
                    Order = (index + 1).ToString(),
                    Name = FormText.OrNull(r.Member?.FullName),
                    PositionAndDepartment = $"{r.Member?.PositionTitle} • {r.Department?.Name ?? r.PartyCell?.Name}",
                    VotesExcellent = $"{excellent} phiếu",
                    VotesGood = $"{good} phiếu",
                    VotesSatisfactory = $"{satisfactory} phiếu",
                    VotesUnsatisfactory = $"{unsatisfactory} phiếu",
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
