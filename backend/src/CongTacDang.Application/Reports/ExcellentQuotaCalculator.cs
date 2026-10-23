using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Reports;

/// <summary>Dữ liệu tối thiểu của một hồ sơ để tính trần Hoàn thành xuất sắc.</summary>
/// <param name="Authority">Cấp quyết định cuối cùng (ảnh chụp trên hồ sơ).</param>
/// <param name="StatCode">M1…M26; null = chưa có.</param>
/// <param name="IsHead">Người đứng đầu không tính vào trần.</param>
public sealed record ExcellentQuotaEntry(ApprovalAuthority Authority, string? StatCode, bool IsHead, EvaluationGrade Grade);

/// <summary>Kết quả trần của một nhóm chức danh trong một cấp quyết định.</summary>
/// <param name="StatCode">null = cán bộ chưa có mã.</param>
/// <param name="Total">Kể cả người đứng đầu.</param>
/// <param name="Good">Số Hoàn thành tốt (không kể người đứng đầu) — mẫu số.</param>
/// <param name="Excellent">Không kể người đứng đầu.</param>
public sealed record ExcellentQuotaGroup(
    ApprovalAuthority Authority, string? StatCode, int Total, int Heads, int Good, int Excellent, int MaxExcellent)
{
    /// <summary>Vượt trần (chỉ cảnh báo, không chặn).</summary>
    public bool IsExceeding => Excellent > MaxExcellent;

    public int Excess => Math.Max(0, Excellent - MaxExcellent);

    /// <summary>% Xuất sắc trên số Hoàn thành tốt (1 chữ số thập phân); null khi chưa có ai Hoàn thành tốt.</summary>
    public double? ExcellentPercentOfGood => Good > 0 ? Math.Round((double)Excellent / Good * 100, 1) : null;
}

/// <summary>
/// Trần Xuất sắc (chỉ cảnh báo): theo nhóm mã chức danh trong từng cấp quyết định, không theo Chi bộ; nơi gọi lọc phạm vi.
/// Trần = làm tròn(tỷ lệ × số Hoàn thành tốt), 0,5 lên 1; người đứng đầu không tính ở cả hai vế.
/// </summary>
public static class ExcellentQuotaCalculator
{
    /// <summary>Mức quyết định nếu có, ngược lại mức đề xuất gần nhất (cấp trực tiếp → thẩm định → tập thể → tự đề xuất).</summary>
    public static EvaluationGrade GradeFor(EvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.DecisionRecordedAt != null ? record.FinalGrade : record.EffectiveGrade();
    }

    public static bool IsHead(IEnumerable<HeldPosition>? heldPositions) => heldPositions?.Any(p => p.IsHead) == true;

    /// <summary>Dữ liệu tính trần của hồ sơ từ chức vụ đang giữ của cán bộ (tại thời điểm xét của kỳ).</summary>
    public static ExcellentQuotaEntry EntryFor(EvaluationRecord record, IReadOnlyCollection<HeldPosition>? heldPositions)
    {
        ArgumentNullException.ThrowIfNull(record);
        var held = heldPositions ?? Array.Empty<HeldPosition>();
        return new ExcellentQuotaEntry(record.ApprovalAuthority, PositionRules.PersonStatCode(held), IsHead(held), GradeFor(record));
    }

    /// <summary>Tính trần theo nhóm; sắp cấp trên trước, rồi theo mã, nhóm chưa có mã cuối.</summary>
    public static List<ExcellentQuotaGroup> Compute(IEnumerable<ExcellentQuotaEntry> entries, double ratio)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries
            .GroupBy(e => (e.Authority, Code: NormalizeCode(e.StatCode)))
            .Select(g =>
            {
                var counted = g.Where(e => !e.IsHead).ToList();
                var good = counted.Count(e => e.Grade == EvaluationGrade.HoanThanhTot);
                return new ExcellentQuotaGroup(
                    g.Key.Authority,
                    g.Key.Code,
                    g.Count(),
                    g.Count(e => e.IsHead),
                    good,
                    counted.Count(e => e.Grade == EvaluationGrade.HoanThanhXuatSac),
                    EvaluationScoring.ExcellentQuota(good, ratio));
            })
            .OrderByDescending(x => x.Authority == ApprovalAuthority.CapTren)
            .ThenBy(x => PositionRules.StatCodeOrder(x.StatCode) ?? int.MaxValue)
            .ToList();
    }

    /// <summary>Thời điểm xét chức vụ đang giữ: cuối kỳ nếu kỳ đã kết thúc, ngược lại <paramref name="now"/>.</summary>
    public static DateTime PositionDate(EvaluationPeriod? period, DateTime now)
    {
        if (period == null || period.EndDate == default)
            return now;
        var end = DateTime.SpecifyKind(period.EndDate, DateTimeKind.Utc);
        return end < now ? end : now;
    }

    public static DTOs.ExcellentQuotaGroupDto ToDto(ExcellentQuotaGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return new DTOs.ExcellentQuotaGroupDto
        {
            ApprovalAuthority = group.Authority.ToString(),
            AuthorityName = PositionRules.AuthorityName(group.Authority),
            StatCode = group.StatCode,
            GroupName = GroupName(group.StatCode),
            TotalCadres = group.Total,
            HeadCount = group.Heads,
            GoodCount = group.Good,
            ExcellentCount = group.Excellent,
            MaxExcellentAllowed = group.MaxExcellent,
            ExcellentPercentOfGood = group.ExcellentPercentOfGood,
            IsExceedingQuota = group.IsExceeding,
            ExcessCount = group.Excess
        };
    }

    public static string GroupName(string? statCode) => statCode == null
        ? Hd03FormCatalog.NoStatCodeSubject
        : Hd03FormCatalog.StatCodeSubjects.GetValueOrDefault(statCode) ?? statCode;

    private static string? NormalizeCode(string? code) =>
        PositionRules.StatCodeOrder(code) is { } order ? "M" + order.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
}
