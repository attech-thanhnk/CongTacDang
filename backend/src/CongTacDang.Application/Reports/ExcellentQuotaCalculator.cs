using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Reports;

/// <summary>Dữ liệu tối thiểu của một hồ sơ để tính trần tỷ lệ Hoàn thành xuất sắc.</summary>
/// <param name="Authority">Cấp quyết định cuối cùng (ảnh chụp trên hồ sơ).</param>
/// <param name="StatCode">Mã chức danh thống kê của người (M1…M26; null = chưa có).</param>
/// <param name="IsHead">Người đứng đầu — không tính vào trần.</param>
/// <param name="Grade">Mức dùng để tính (<see cref="ExcellentQuotaCalculator.GradeFor"/>).</param>
public sealed record ExcellentQuotaEntry(ApprovalAuthority Authority, string? StatCode, bool IsHead, EvaluationGrade Grade);

/// <summary>Kết quả trần của một nhóm chức danh trong một cấp quyết định.</summary>
/// <param name="Authority">Cấp quyết định cuối cùng.</param>
/// <param name="StatCode">Mã chức danh thống kê (null = cán bộ chưa có mã).</param>
/// <param name="Total">Tổng số hồ sơ của nhóm (kể cả người đứng đầu).</param>
/// <param name="Heads">Số người đứng đầu (không tính vào trần).</param>
/// <param name="Good">Số "Hoàn thành tốt nhiệm vụ" (không kể người đứng đầu) — mẫu số.</param>
/// <param name="Excellent">Số "Hoàn thành xuất sắc nhiệm vụ" (không kể người đứng đầu).</param>
/// <param name="MaxExcellent">Số Xuất sắc tối đa.</param>
public sealed record ExcellentQuotaGroup(
    ApprovalAuthority Authority, string? StatCode, int Total, int Heads, int Good, int Excellent, int MaxExcellent)
{
    /// <summary>Vượt trần (chỉ cảnh báo, không chặn).</summary>
    public bool IsExceeding => Excellent > MaxExcellent;

    /// <summary>Số Xuất sắc vượt trần.</summary>
    public int Excess => Math.Max(0, Excellent - MaxExcellent);

    /// <summary>Tỷ lệ % Xuất sắc trên số "Hoàn thành tốt" (1 chữ số thập phân); null khi chưa có ai "Hoàn thành tốt".</summary>
    public double? ExcellentPercentOfGood => Good > 0 ? Math.Round((double)Excellent / Good * 100, 1) : null;
}

/// <summary>
/// Trần tỷ lệ Hoàn thành xuất sắc (chỉ cảnh báo, không chặn):
/// <list type="bullet">
/// <item>Đơn vị tính: nhóm chức danh (mã thống kê M1–M26 của người — mã đứng trước khi giữ nhiều chức vụ) trong từng cấp
/// quyết định cuối cùng (Đảng ủy cơ sở / cấp trên, ảnh chụp trên hồ sơ); không tính theo từng Chi bộ. Phạm vi hồ sơ (cả
/// Đảng bộ hay một tổ chức Đảng, theo phạm vi được xem) do nơi gọi lọc trước theo ảnh chụp đơn vị trên hồ sơ.</item>
/// <item>Trần = làm tròn (tỷ lệ của bộ tiêu chí × số "Hoàn thành tốt nhiệm vụ"), 0,5 trở lên thành 1.</item>
/// <item>Người đứng đầu (chức vụ đánh dấu "Người đứng đầu") không tính ở cả số Xuất sắc và số Hoàn thành tốt.</item>
/// <item>Mức dùng: mức quyết định nếu đã ghi quyết định; chưa có thì mức đề xuất gần nhất theo thứ tự bước (cấp trực tiếp
/// sử dụng → thẩm định → tập thể lãnh đạo → tự đề xuất đã nộp).</item>
/// </list>
/// </summary>
public static class ExcellentQuotaCalculator
{
    /// <summary>Mức dùng để tính trần của hồ sơ.</summary>
    public static EvaluationGrade GradeFor(EvaluationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record.DecisionRecordedAt != null ? record.FinalGrade : record.EffectiveGrade();
    }

    /// <summary>Người đứng đầu: ít nhất một chức vụ đang giữ được đánh dấu người đứng đầu.</summary>
    public static bool IsHead(IEnumerable<HeldPosition>? heldPositions) => heldPositions?.Any(p => p.IsHead) == true;

    /// <summary>Dữ liệu tính trần của hồ sơ từ chức vụ đang giữ của cán bộ (tại thời điểm xét của kỳ).</summary>
    public static ExcellentQuotaEntry EntryFor(EvaluationRecord record, IReadOnlyCollection<HeldPosition>? heldPositions)
    {
        ArgumentNullException.ThrowIfNull(record);
        var held = heldPositions ?? Array.Empty<HeldPosition>();
        return new ExcellentQuotaEntry(record.ApprovalAuthority, PositionRules.PersonStatCode(held), IsHead(held), GradeFor(record));
    }

    /// <summary>
    /// Tính trần theo nhóm (cấp quyết định, mã chức danh); sắp cấp trên trước, rồi theo thứ tự mã, nhóm chưa có mã cuối.
    /// </summary>
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

    /// <summary>
    /// Thời điểm xét chức vụ đang giữ (mã chức danh, người đứng đầu) cho số liệu của kỳ: cuối kỳ nếu kỳ đã kết thúc, ngược lại
    /// <paramref name="now"/>.
    /// </summary>
    public static DateTime PositionDate(EvaluationPeriod? period, DateTime now)
    {
        if (period == null || period.EndDate == default)
            return now;
        var end = DateTime.SpecifyKind(period.EndDate, DateTimeKind.Utc);
        return end < now ? end : now;
    }

    /// <summary>Đổi kết quả nhóm sang DTO.</summary>
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

    /// <summary>Tên nhóm chức danh để hiển thị.</summary>
    public static string GroupName(string? statCode) => statCode == null
        ? Hd03FormCatalog.NoStatCodeSubject
        : Hd03FormCatalog.StatCodeSubjects.GetValueOrDefault(statCode) ?? statCode;

    private static string? NormalizeCode(string? code) =>
        PositionRules.StatCodeOrder(code) is { } order ? "M" + order.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
}
