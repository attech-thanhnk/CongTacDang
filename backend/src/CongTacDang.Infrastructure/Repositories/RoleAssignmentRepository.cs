using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Repository bản gán vai trò có phạm vi và thời hạn (bảng <c>user_role_assignments</c>).</summary>
public class RoleAssignmentRepository : IRoleAssignmentRepository
{
    private static readonly string[] AdministratorCodes =
    {
        PermissionCodes.SystemRolesManage,
        PermissionCodes.SystemAssignmentsManage
    };

    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo repository.</summary>
    public RoleAssignmentRepository(CongTacDangDbContext db) => _db = db;

    private IQueryable<UserRoleAssignment> Assignments => _db.Set<UserRoleAssignment>();

    /// <inheritdoc />
    public async Task<UserAccessSnapshot?> GetAccessSnapshotAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var user = await _db.PartyMemberProfiles
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsActive })
            .FirstOrDefaultAsync(ct);
        if (user == null)
            return null;

        // Bản gán chưa xóa, chưa hết hạn, vai trò chưa xóa (query filter của AppRole/Permission áp dụng qua navigation).
        var rows = await Assignments
            .AsNoTracking()
            .Where(a => a.UserId == userId && (a.ValidTo == null || a.ValidTo > now) && a.Role != null && !a.Role.IsDeleted)
            .Select(a => new
            {
                a.Id,
                a.RoleId,
                RoleName = a.Role!.Name,
                a.ScopeType,
                a.ScopeId,
                a.ValidFrom,
                a.ValidTo,
                Codes = a.Role.Permissions.Where(p => !p.IsDeleted).Select(p => p.Code).ToList()
            })
            .ToListAsync(ct);

        return new UserAccessSnapshot(
            user.IsActive,
            rows.Select(r => new AssignmentGrantSource(r.Id, r.RoleId, r.RoleName, r.ScopeType, r.ScopeId, r.ValidFrom, r.ValidTo, r.Codes))
                .ToList());
    }

    /// <inheritdoc />
    public async Task<List<UserRoleAssignment>> QueryAsync(RoleAssignmentFilter filter, CancellationToken ct = default)
    {
        var query = Assignments
            .AsNoTracking()
            .Include(a => a.Role)
            .Include(a => a.User)
            .AsQueryable();

        if (filter.Id.HasValue)
            query = query.Where(a => a.Id == filter.Id.Value);
        if (filter.UserId.HasValue)
            query = query.Where(a => a.UserId == filter.UserId.Value);
        if (filter.RoleId.HasValue)
            query = query.Where(a => a.RoleId == filter.RoleId.Value);
        if (filter.ScopeType.HasValue)
            query = query.Where(a => a.ScopeType == filter.ScopeType.Value);
        if (filter.ScopeId.HasValue)
            query = query.Where(a => a.ScopeId == filter.ScopeId.Value);
        if (filter.ActiveOn.HasValue)
        {
            var on = filter.ActiveOn.Value;
            query = query.Where(a => a.ValidFrom <= on && (a.ValidTo == null || a.ValidTo > on));
        }

        return await query
            .OrderBy(a => a.UserId)
            .ThenByDescending(a => a.ValidFrom)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<UserRoleAssignment?> GetForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await Assignments
            .Include(a => a.Role!)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    /// <inheritdoc />
    public async Task<List<UserRoleAssignment>> GetCurrentOrFutureForUserAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        return await Assignments
            .AsNoTracking()
            .Include(a => a.Role)
            .Where(a => a.UserId == userId && (a.ValidTo == null || a.ValidTo > now))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<int> CountCurrentOrFutureByRoleAsync(Guid roleId, DateTime now, CancellationToken ct = default)
    {
        return await Assignments.CountAsync(a => a.RoleId == roleId && (a.ValidTo == null || a.ValidTo > now), ct);
    }

    /// <inheritdoc />
    public async Task<bool> HasNonGlobalCurrentOrFutureAssignmentsAsync(Guid roleId, DateTime now, CancellationToken ct = default)
    {
        return await Assignments.AnyAsync(
            a => a.RoleId == roleId && a.ScopeType != RoleScopeType.Global && (a.ValidTo == null || a.ValidTo > now), ct);
    }

    /// <inheritdoc />
    public async Task<List<AdministratorGrantRow>> GetAdministratorGrantsAsync(DateTime now, CancellationToken ct = default)
    {
        var rows = await Assignments
            .AsNoTracking()
            .Where(a => a.ScopeType == RoleScopeType.Global
                && a.ValidFrom <= now && (a.ValidTo == null || a.ValidTo > now)
                && a.User != null && a.User.IsActive && !a.User.IsDeleted
                && a.Role != null && !a.Role.IsDeleted)
            .Select(a => new
            {
                a.Id,
                a.UserId,
                a.RoleId,
                Codes = a.Role!.Permissions
                    .Where(p => !p.IsDeleted && AdministratorCodes.Contains(p.Code))
                    .Select(p => p.Code)
                    .ToList()
            })
            .ToListAsync(ct);

        return rows
            .Where(r => r.Codes.Count > 0)
            .Select(r => new AdministratorGrantRow(r.Id, r.UserId, r.RoleId, r.Codes))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.PartyMemberProfiles.AnyAsync(u => u.Id == userId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> ScopeExistsAsync(RoleScopeType scopeType, Guid scopeId, CancellationToken ct = default)
    {
        return scopeType switch
        {
            RoleScopeType.Department => await _db.AdministrativeDepartments.AnyAsync(d => d.Id == scopeId && d.IsActive, ct),
            RoleScopeType.PartyCell => await _db.PartyCells.AnyAsync(c => c.Id == scopeId && c.IsActive, ct),
            _ => false
        };
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, string>> GetScopeNamesAsync(
        IReadOnlyCollection<Guid> departmentIds,
        IReadOnlyCollection<Guid> partyCellIds,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, string>();
        if (departmentIds.Count > 0)
        {
            var ids = departmentIds.Distinct().ToList();
            foreach (var d in await _db.AdministrativeDepartments.IgnoreQueryFilters().AsNoTracking()
                         .Where(d => ids.Contains(d.Id)).Select(d => new { d.Id, d.Name }).ToListAsync(ct))
                result[d.Id] = d.Name;
        }

        if (partyCellIds.Count > 0)
        {
            var ids = partyCellIds.Distinct().ToList();
            foreach (var c in await _db.PartyCells.IgnoreQueryFilters().AsNoTracking()
                         .Where(c => ids.Contains(c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync(ct))
                result[c.Id] = c.Name;
        }

        return result;
    }

    /// <inheritdoc />
    public void Add(UserRoleAssignment assignment) => _db.Set<UserRoleAssignment>().Add(assignment);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
