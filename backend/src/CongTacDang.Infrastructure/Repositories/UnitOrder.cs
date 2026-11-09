using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Thứ tự đơn vị khi hiển thị danh sách cán bộ.</summary>
internal static class UnitOrder
{
    public static async Task<Dictionary<string, int>> LoadAsync(CongTacDangDbContext db, CancellationToken ct)
    {
        var units = await db.AdministrativeDepartments.AsNoTracking()
            .Select(d => new { d.Id, d.ParentId, d.Name, d.SortOrder })
            .ToListAsync(ct);
        var children = units.ToLookup(d => d.ParentId);
        var order = new Dictionary<string, int>();

        void Visit(Guid? parentId)
        {
            foreach (var unit in children[parentId].OrderBy(d => d.SortOrder).ThenBy(d => d.Name, StringComparer.CurrentCulture))
            {
                order.TryAdd(unit.Name, order.Count);
                Visit(unit.Id);
            }
        }

        Visit(null);
        return order;
    }
}
