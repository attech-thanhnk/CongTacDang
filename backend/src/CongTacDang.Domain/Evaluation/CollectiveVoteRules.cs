using System;
using System.Globalization;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Kết quả kiểm phiếu của một cán bộ (Mẫu 13).</summary>
public sealed record VoteTally(int Excellent, int Good, int Satisfactory, int Unsatisfactory, int NotRated, int Invalid)
{
    public int Total => Excellent + Good + Satisfactory + Unsatisfactory + NotRated + Invalid;

    public int VotesFor(EvaluationGrade grade) => grade switch
    {
        EvaluationGrade.HoanThanhXuatSac => Excellent,
        EvaluationGrade.HoanThanhTot => Good,
        EvaluationGrade.HoanThanh => Satisfactory,
        EvaluationGrade.KhongHoanThanh => Unsatisfactory,
        _ => 0
    };
}

/// <summary>
/// Mức được biểu quyết thông qua: mức cao nhất có số phiếu ở mức đó và các mức cao hơn (cộng dồn từ mức cao xuống) quá nửa số người
/// có mặt. HD03 không quy định tỷ lệ; đây là nguyên tắc biểu quyết quá bán thường dùng, chờ nghiệp vụ xác nhận.
/// </summary>
public static class CollectiveVoteRules
{
    /// <summary>Số người có mặt chưa ghi (0) → tính trên tổng số phiếu.</summary>
    public static int Basis(VoteTally tally, int presentCount)
    {
        ArgumentNullException.ThrowIfNull(tally);
        return presentCount > 0 ? presentCount : tally.Total;
    }

    /// <summary>Null = chưa có phiếu hoặc không mức nào đạt quá bán.</summary>
    public static EvaluationGrade? PassedGrade(VoteTally tally, int presentCount)
    {
        var basis = Basis(tally, presentCount);
        if (basis <= 0)
            return null;
        var cumulative = 0;
        foreach (var grade in CriteriaSetContent.RankedGrades)
        {
            cumulative += tally.VotesFor(grade);
            if (cumulative * 2 > basis)
                return grade;
        }
        return null;
    }

    /// <summary>Số phiếu cộng dồn từ mức cao nhất tới <paramref name="grade"/>.</summary>
    public static int CumulativeVotes(VoteTally tally, EvaluationGrade grade)
    {
        ArgumentNullException.ThrowIfNull(tally);
        var cumulative = 0;
        foreach (var ranked in CriteriaSetContent.RankedGrades)
        {
            cumulative += tally.VotesFor(ranked);
            if (ranked == grade)
                return cumulative;
        }
        return 0;
    }

    /// <summary>Lỗi (nêu số phiếu) khi mức ghi nhận khác mức được thông qua hoặc không mức nào đạt; null = khớp.</summary>
    public static string? Validate(EvaluationGrade proposed, VoteTally tally, int presentCount)
    {
        ArgumentNullException.ThrowIfNull(tally);
        if (tally.Total == 0)
            return null;
        var passed = PassedGrade(tally, presentCount);
        if (passed == proposed)
            return null;

        var basis = Basis(tally, presentCount);
        var summary = string.Create(CultureInfo.InvariantCulture,
            $"Kết quả kiểm phiếu ({(presentCount > 0 ? $"{presentCount} người có mặt" : $"{basis} phiếu")}): Xuất sắc {tally.Excellent}, "
            + $"Tốt {tally.Good}, Hoàn thành {tally.Satisfactory}, Không hoàn thành {tally.Unsatisfactory}, Chưa đánh giá {tally.NotRated}, "
            + $"không hợp lệ {tally.Invalid}.");
        if (passed is not { } grade)
        {
            return summary + $" Không mức nào đạt quá nửa ({basis / 2 + 1}/{basis}) kể cả khi cộng dồn phiếu từ mức cao xuống nên chưa ghi nhận "
                + "được mức đề xuất. Hãy kiểm tra lại số phiếu hoặc tổ chức biểu quyết lại.";
        }
        return summary + $" Mức đạt quá nửa (cộng dồn phiếu từ mức cao xuống) là \"{CriteriaSetContent.GradeName(grade)}\" "
            + $"({CumulativeVotes(tally, grade)}/{basis} phiếu); mức \"{CriteriaSetContent.GradeName(proposed)}\" "
            + $"({CumulativeVotes(tally, proposed)}/{basis} phiếu) không khớp kết quả kiểm phiếu. Hãy ghi nhận đúng mức đã được biểu quyết.";
    }
}
