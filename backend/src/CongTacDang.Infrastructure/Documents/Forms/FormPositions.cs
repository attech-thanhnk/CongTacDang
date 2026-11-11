using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Reports;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Nạp chức vụ đang hiệu lực kèm đơn vị để in chức danh đầy đủ (<see cref="PositionRules.FormTitles"/>).</summary>
internal static class FormPositions
{
    /// <summary>Thời điểm xét chức vụ của kỳ: cuối kỳ, kỳ chưa kết thúc thì hôm nay.</summary>
    public static DateTime At(EvaluationPeriod? period) => ExcellentQuotaCalculator.PositionDate(period, DateTime.UtcNow);

    /// <summary>Chức vụ đang hiệu lực của từng cán bộ (kèm chức vụ, tổ chức Đảng, đơn vị), theo thứ tự danh mục.</summary>
    public static async Task<Dictionary<Guid, List<MemberPosition>>> HeldAsync(
        CongTacDangDbContext db, IEnumerable<Guid> memberIds, DateTime at, CancellationToken ct = default)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, List<MemberPosition>>();
        var rows = await db.MemberPositions.AsNoTracking()
            .Include(mp => mp.Position)
            .Include(mp => mp.PartyCell)
            .Include(mp => mp.Department)
            .Where(mp => ids.Contains(mp.UserId) && mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at) && mp.Position != null)
            .ToListAsync(ct);
        return rows.GroupBy(mp => mp.UserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(mp => mp.Position!.SortOrder).ToList());
    }

    /// <summary>Chức danh in một ô của cán bộ (xem <see cref="PositionRules.PrimaryFormTitle"/>); không có tài khoản → null.</summary>
    public static async Task<string?> PrimaryTitleAsync(CongTacDangDbContext db, Guid memberId, DateTime at, CancellationToken ct = default)
    {
        var profileTitle = await db.PartyMemberProfiles.AsNoTracking()
            .Where(p => p.Id == memberId)
            .Select(p => p.PositionTitle)
            .FirstOrDefaultAsync(ct);
        var held = await HeldAsync(db, new[] { memberId }, at, ct);
        return PositionRules.PrimaryFormTitle(held.GetValueOrDefault(memberId), profileTitle);
    }
}
