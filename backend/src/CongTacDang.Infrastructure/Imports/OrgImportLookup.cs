using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>Tra cứu phục vụ import mô hình tổ chức (task 14).</summary>
public sealed class OrgImportLookup : IOrgImportLookup
{
    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo.</summary>
    public OrgImportLookup(CongTacDangDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnitTypeLookupEntry>> GetUnitTypesAsync(CancellationToken ct)
        => await _db.OrgUnitTypes.AsNoTracking()
            .Select(t => new UnitTypeLookupEntry(t.Id, t.Name, t.Side, t.IsActive))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PositionLookupEntry>> GetPositionsAsync(CancellationToken ct)
        => await _db.Positions.AsNoTracking()
            .Select(p => new PositionLookupEntry(p.Id, p.Name, p.Side, p.IsActive))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MemberLookupEntry>> FindMembersAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        var wanted = usernames.Select(u => u.ToLowerInvariant()).Distinct().ToList();
        if (wanted.Count == 0)
            return Array.Empty<MemberLookupEntry>();
        return await _db.PartyMemberProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(m => wanted.Contains(m.Username.ToLower()))
            .Select(m => new MemberLookupEntry(m.Id, m.Username, m.FullName, m.DepartmentId, m.PartyCellId, m.IsDeleted))
            .ToListAsync(ct);
    }
}
