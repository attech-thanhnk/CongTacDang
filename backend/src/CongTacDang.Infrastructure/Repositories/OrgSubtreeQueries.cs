using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Organization;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Truy vấn quan hệ cây đơn vị theo <c>Path</c> (dùng chung cho các repository cài <c>IOrgSubtreeReader</c>).</summary>
internal static class OrgSubtreeQueries
{
    /// <summary>Đơn vị chính quyền <paramref name="unitId"/> là <paramref name="rootId"/> hoặc con cháu của nó.</summary>
    public static async Task<bool> IsDepartmentWithinAsync(CongTacDangDbContext db, Guid rootId, Guid unitId, CancellationToken ct)
    {
        if (rootId == unitId)
            return true;
        var path = await db.AdministrativeDepartments.AsNoTracking()
            .Where(d => d.Id == unitId)
            .Select(d => d.Path)
            .FirstOrDefaultAsync(ct);
        return OrgTree.IsSelfOrDescendant(path, rootId);
    }

    /// <summary>Tổ chức Đảng <paramref name="unitId"/> là <paramref name="rootId"/> hoặc con cháu của nó.</summary>
    public static async Task<bool> IsPartyCellWithinAsync(CongTacDangDbContext db, Guid rootId, Guid unitId, CancellationToken ct)
    {
        if (rootId == unitId)
            return true;
        var path = await db.PartyCells.AsNoTracking()
            .Where(c => c.Id == unitId)
            .Select(c => c.Path)
            .FirstOrDefaultAsync(ct);
        return OrgTree.IsSelfOrDescendant(path, rootId);
    }
}
