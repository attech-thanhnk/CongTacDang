using CongTacDang.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.IntegrationTests.Infrastructure;

public static class EvaluablePositionExtensions
{
    /// <summary>Cho mọi người của đơn vị chưa có chức vụ giữ chức vụ thuộc diện đánh giá, để "Thêm cả đơn vị" lấy họ.</summary>
    public static Task GiveEvaluablePositionsAsync(this ApiFactory factory, Guid departmentId) =>
        factory.WithDbAsync(async db =>
        {
            var positionId = await db.Positions.Where(p => p.StatCode == "M26" && !p.IsDeleted).Select(p => p.Id).FirstAsync();
            var from = DateTime.UtcNow.AddYears(-1);
            var members = await db.PartyMemberProfiles
                .Where(m => m.DepartmentId == departmentId && !db.MemberPositions.Any(mp => mp.UserId == m.Id))
                .Select(m => m.Id)
                .ToListAsync();
            foreach (var id in members)
                db.Add(new MemberPosition { UserId = id, PositionId = positionId, IsPrimary = true, ValidFrom = from });
            await db.SaveChangesAsync();
        });
}
