using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>Tra cứu chỉ đọc cho import gán vai trò (task 13).</summary>
public sealed class RoleAssignmentImportLookup : IRoleAssignmentImportLookup
{
    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo.</summary>
    public RoleAssignmentImportLookup(CongTacDangDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentUserEntry>> FindUsersAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        var wanted = usernames.Select(u => u.ToLowerInvariant()).Distinct().ToList();
        if (wanted.Count == 0)
            return Array.Empty<AssignmentUserEntry>();

        return await _db.PartyMemberProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(m => wanted.Contains(m.Username.ToLower()))
            .Select(m => new AssignmentUserEntry(m.Id, m.Username, m.FullName, m.IsActive, m.IsDeleted))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentRoleEntry>> GetRolesAsync(CancellationToken ct)
    {
        var roles = await _db.Roles.AsNoTracking()
            .Where(r => !r.IsDeleted)
            .Select(r => new { r.Id, r.Name, Codes = r.Permissions.Select(p => p.Code).ToList() })
            .ToListAsync(ct);
        return roles.Select(r => new AssignmentRoleEntry(r.Id, r.Name, r.Codes)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExistingAssignmentEntry>> GetCurrentOrFutureAssignmentsAsync(
        IReadOnlyCollection<Guid> userIds, DateTime nowUtc, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return Array.Empty<ExistingAssignmentEntry>();

        var rows = await _db.Set<UserRoleAssignment>().AsNoTracking()
            .Where(a => !a.IsDeleted && ids.Contains(a.UserId) && (a.ValidTo == null || a.ValidTo > nowUtc))
            .Select(a => new { a.UserId, a.RoleId, a.ScopeType, a.ScopeId, a.ValidFrom, a.ValidTo })
            .ToListAsync(ct);
        return rows.Select(a => new ExistingAssignmentEntry(a.UserId, a.RoleId, (ScopeType)(int)a.ScopeType, a.ScopeId, a.ValidFrom, a.ValidTo))
            .ToList();
    }
}
