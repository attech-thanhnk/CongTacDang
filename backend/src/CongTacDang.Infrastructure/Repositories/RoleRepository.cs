using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Repository quản trị vai trò và danh mục quyền.</summary>
public class RoleRepository : IRoleRepository
{
    private readonly CongTacDangDbContext _db;
    private readonly ICurrentUserService _currentUser;

    /// <summary>Khởi tạo repository.</summary>
    public RoleRepository(CongTacDangDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<List<AppRole>> GetAllRolesWithPermissionsAsync()
    {
        return await _db.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .OrderBy(r => r.Name)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<Permission>> GetAllPermissionsAsync()
    {
        return await _db.Permissions
            .AsNoTracking()
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Code)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<AppRole?> GetRoleByIdWithPermissionsAsync(Guid roleId)
    {
        return await _db.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);
    }

    /// <inheritdoc />
    public async Task<AppRole?> GetRoleForUpdateAsync(Guid roleId, CancellationToken ct = default)
    {
        return await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
    }

    /// <inheritdoc />
    public async Task<bool> RoleNameExistsAsync(string name, Guid? excludeRoleId, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return await _db.Roles
            .AnyAsync(r => r.Name.ToLower() == normalized && (excludeRoleId == null || r.Id != excludeRoleId), ct);
    }

    /// <inheritdoc />
    public async Task<List<Permission>> GetPermissionsByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken ct = default)
    {
        var list = codes.Distinct(StringComparer.Ordinal).ToList();
        return await _db.Permissions
            .Where(p => list.Contains(p.Code))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public void AddRole(AppRole role) => _db.Roles.Add(role);

    /// <inheritdoc />
    public void AddRolePermissionsAudit(Guid roleId, IReadOnlyCollection<string> previousCodes, IReadOnlyCollection<string> currentCodes)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = _currentUser.UserId,
            ActorName = string.IsNullOrWhiteSpace(_currentUser.UserName) ? "system" : _currentUser.UserName,
            Action = "UpdatePermissions",
            EntityType = nameof(AppRole),
            EntityId = roleId.ToString(),
            OldValues = JsonSerializer.Serialize(new { permissionCodes = previousCodes.OrderBy(c => c, StringComparer.Ordinal) }),
            NewValues = JsonSerializer.Serialize(new { permissionCodes = currentCodes.OrderBy(c => c, StringComparer.Ordinal) }),
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            RequestPath = _currentUser.RequestPath,
            CreatedAt = DateTime.UtcNow
        });
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
