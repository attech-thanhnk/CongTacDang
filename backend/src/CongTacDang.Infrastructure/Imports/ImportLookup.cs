using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Imports;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>Đọc dữ liệu tham chiếu theo lô cho chức năng nhập dữ liệu và ghi nhật ký tóm tắt mỗi lần nhập.</summary>
public sealed class ImportLookup : IImportLookup, IImportAuditWriter
{
    private readonly CongTacDangDbContext _db;
    private readonly ICurrentUserService _currentUser;

    /// <summary>Khởi tạo.</summary>
    public ImportLookup(CongTacDangDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgUnitRef>> GetDepartmentsAsync(CancellationToken ct = default)
        => await _db.AdministrativeDepartments.IgnoreQueryFilters().AsNoTracking()
            .Select(d => new OrgUnitRef(d.Id, d.Code, d.Name, d.ParentId, d.UnitTypeId, d.IsActive, d.IsDeleted))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgUnitRef>> GetPartyCellsAsync(CancellationToken ct = default)
        => await _db.PartyCells.IgnoreQueryFilters().AsNoTracking()
            .Select(c => new OrgUnitRef(c.Id, c.Code, c.Name, c.ParentId, c.UnitTypeId, c.IsActive, c.IsDeleted))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnitTypeRef>> GetUnitTypesAsync(CancellationToken ct = default)
        => await _db.OrgUnitTypes.AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new UnitTypeRef(t.Id, t.Name, t.Side, t.IsActive))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PositionRef>> GetPositionsAsync(CancellationToken ct = default)
        => await _db.Positions.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Name)
            .Select(p => new PositionRef(p.Id, p.Name, p.StatCode, p.Side, p.IsActive))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FormLayoutRef>> GetFormLayoutsAsync(CancellationToken ct = default)
        => await _db.FormLayouts.AsNoTracking()
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Name)
            .Select(l => new FormLayoutRef(l.Id, l.Name))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleRef>> GetRolesAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles.AsNoTracking()
            .Where(r => !r.IsDeleted)
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        return roles
            .Select(r => new RoleRef(r.Id, r.Code, r.Name, r.Permissions.Where(p => !p.IsDeleted).Select(p => p.Code).ToList()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserRef>> FindUsersAsync(IReadOnlyCollection<string> usernames, DateTime now, CancellationToken ct = default)
    {
        var keys = usernames.Select(u => u.ToLowerInvariant()).Distinct().ToList();
        if (keys.Count == 0)
            return Array.Empty<UserRef>();

        var users = await _db.PartyMemberProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(u => keys.Contains(u.Username.ToLower()))
            .Select(u => new { u.Id, u.Username, u.FullName, u.IsDeleted, u.DepartmentId, u.PartyCellId })
            .ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToList();
        var held = await _db.MemberPositions.AsNoTracking()
            .Where(mp => ids.Contains(mp.UserId) && !mp.IsDeleted && mp.ValidFrom <= now && (mp.ValidTo == null || mp.ValidTo > now))
            .Select(mp => new { mp.UserId, mp.PositionId })
            .ToListAsync(ct);
        var heldByUser = held.GroupBy(h => h.UserId).ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(h => h.PositionId).Distinct().ToList());

        return users
            .Select(u => new UserRef(u.Id, u.Username.ToLowerInvariant(), u.FullName, u.IsDeleted, u.DepartmentId, u.PartyCellId,
                heldByUser.GetValueOrDefault(u.Id) ?? Array.Empty<Guid>()))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssignmentRef>> GetCurrentOrFutureAssignmentsAsync(IReadOnlyCollection<Guid> userIds, DateTime now, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
            return Array.Empty<AssignmentRef>();
        var ids = userIds.Distinct().ToList();
        return await _db.Set<UserRoleAssignment>().AsNoTracking()
            .Where(a => ids.Contains(a.UserId) && !a.IsDeleted && (a.ValidTo == null || a.ValidTo > now))
            .Select(a => new AssignmentRef(a.UserId, a.RoleId, a.ScopeType, a.ScopeId, a.ValidFrom, a.ValidTo))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public void AddSummary(string kind, string fileName, int rows, int created, int updated)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = _currentUser.UserId,
            ActorName = string.IsNullOrWhiteSpace(_currentUser.UserName) ? "system" : _currentUser.UserName,
            Action = "Import",
            EntityType = "Import",
            EntityId = kind,
            OldValues = "{}",
            NewValues = JsonSerializer.Serialize(new { kind, fileName, rows, created, updated }),
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            RequestPath = _currentUser.RequestPath,
            CreatedAt = DateTime.UtcNow
        });
    }
}
