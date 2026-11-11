using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents.Forms;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Services;

/// <summary>Tra cấp trên trực tiếp cho dòng 1 tiêu đề trái biểu mẫu cá nhân (cây chính quyền và cây Đảng).</summary>
public static class OrgHeaderLookup
{
    /// <summary>Cơ quan chủ quản trực tiếp của đơn vị hồ sơ (Mẫu 01, 02); null khi đơn vị trực thuộc gốc (Công ty) hoặc là gốc.</summary>
    public static async Task<string?> AdminParentLineAsync(CongTacDangDbContext db, AdministrativeDepartment? department, CancellationToken ct = default)
    {
        if (department?.ParentId is not { } parentId)
            return null;
        var parent = await db.AdministrativeDepartments.AsNoTracking()
            .Where(d => d.Id == parentId).Select(d => new { d.Name, d.ParentId }).FirstOrDefaultAsync(ct);
        return OrgHeaderLine.DirectParent(parent?.Name, parent?.ParentId == null);
    }

    /// <summary>Tổ chức Đảng cấp trên trực tiếp của Chi bộ hồ sơ (Mẫu 09A, 09C, 9D); null khi trực thuộc gốc (Đảng bộ Công ty).</summary>
    public static async Task<(string? Name, bool IsRoot)> PartyParentAsync(CongTacDangDbContext db, PartyCell? cell, CancellationToken ct = default)
    {
        if (cell?.ParentId is not { } parentId)
            return (null, true);
        var parent = await db.PartyCells.AsNoTracking()
            .Where(c => c.Id == parentId).Select(c => new { c.Name, c.ParentId }).FirstOrDefaultAsync(ct);
        return (parent?.Name, parent?.ParentId == null);
    }
}
