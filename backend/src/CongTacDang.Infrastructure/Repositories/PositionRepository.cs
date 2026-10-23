using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

public sealed class PositionRepository : IPositionRepository
{
    private readonly CongTacDangDbContext _db;

    public PositionRepository(CongTacDangDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<List<Position>> ListPositionsAsync(CancellationToken ct = default)
        => _db.Positions.AsNoTracking().Include(p => p.FormLayout).OrderBy(p => p.SortOrder).ThenBy(p => p.Name).ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> FormLayoutExistsAsync(Guid id, CancellationToken ct = default)
        => _db.FormLayouts.AnyAsync(l => l.Id == id, ct);

    /// <inheritdoc />
    public Task<Position?> FindPositionAsync(Guid id, CancellationToken ct = default)
        => _db.Positions.Include(p => p.FormLayout).FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> PositionNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return _db.Positions.AnyAsync(p => p.Name.ToLower() == normalized && (excludeId == null || p.Id != excludeId), ct);
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> CountHoldersAsync(DateTime now, CancellationToken ct = default)
    {
        var rows = await _db.MemberPositions.AsNoTracking()
            .Where(mp => mp.ValidFrom <= now && (mp.ValidTo == null || mp.ValidTo > now) && mp.User != null && !mp.User.IsDeleted)
            .GroupBy(mp => mp.PositionId)
            .Select(g => new { Id = g.Key, Count = g.Select(x => x.UserId).Distinct().Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => r.Count);
    }

    /// <inheritdoc />
    public Task<int> CountMemberPositionsAsync(Guid positionId, CancellationToken ct = default)
        => _db.MemberPositions.CountAsync(mp => mp.PositionId == positionId, ct);

    /// <inheritdoc />
    public void AddPosition(Position position) => _db.Positions.Add(position);

    /// <inheritdoc />
    public void RemovePosition(Position position) => _db.Positions.Remove(position);

    /// <inheritdoc />
    public Task<List<MemberPosition>> ListMemberPositionsAsync(Guid userId, CancellationToken ct = default)
        => _db.MemberPositions.AsNoTracking()
            .Include(mp => mp.Position)
            .Include(mp => mp.PartyCell)
            .Include(mp => mp.Department)
            .Where(mp => mp.UserId == userId)
            .OrderByDescending(mp => mp.ValidTo == null)
            .ThenByDescending(mp => mp.ValidFrom)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<MemberPosition>> ListMemberPositionsForUpdateAsync(Guid userId, CancellationToken ct = default)
        => _db.MemberPositions
            .Include(mp => mp.Position)
            .Where(mp => mp.UserId == userId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<MemberPosition?> FindMemberPositionAsync(Guid id, CancellationToken ct = default)
        => _db.MemberPositions.Include(mp => mp.Position).FirstOrDefaultAsync(mp => mp.Id == id, ct);

    /// <inheritdoc />
    public void AddMemberPosition(MemberPosition memberPosition) => _db.MemberPositions.Add(memberPosition);

    /// <inheritdoc />
    public void RemoveMemberPosition(MemberPosition memberPosition) => _db.MemberPositions.Remove(memberPosition);

    /// <inheritdoc />
    public async Task<Dictionary<Guid, List<HeldPosition>>> GetHeldPositionsAsync(
        IReadOnlyCollection<Guid>? userIds, DateTime at, CancellationToken ct = default)
    {
        var query = _db.MemberPositions.AsNoTracking()
            .Where(mp => mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at) && mp.Position != null);
        if (userIds != null)
        {
            var ids = userIds.Distinct().ToList();
            query = query.Where(mp => ids.Contains(mp.UserId));
        }

        var rows = await query
            .Select(mp => new { mp.UserId, mp.Position!.Name, mp.IsActing, mp.Position.StatCode, mp.Position.DefaultApprovalAuthority, mp.Position.IsHead })
            .ToListAsync(ct);
        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Select(r => new HeldPosition(PositionRules.DisplayName(r.Name, r.IsActing), r.StatCode, r.DefaultApprovalAuthority, r.IsHead)).ToList());
    }

    /// <inheritdoc />
    public async Task<List<PositionHolding>> ListActiveHoldingsAsync(
        IReadOnlyCollection<Guid>? userIds, Guid? departmentId, Guid? partyCellId, DateTime at, CancellationToken ct = default)
    {
        var query = _db.MemberPositions.AsNoTracking()
            .Where(mp => mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at)
                && mp.Position != null && mp.User != null && !mp.User.IsDeleted);
        if (userIds != null)
        {
            var ids = userIds.Distinct().ToList();
            query = query.Where(mp => ids.Contains(mp.UserId));
        }
        if (departmentId.HasValue)
            query = query.Where(mp => mp.DepartmentId == departmentId.Value);
        if (partyCellId.HasValue)
            query = query.Where(mp => mp.PartyCellId == partyCellId.Value);

        var rows = await query
            .Select(mp => new
            {
                mp.UserId,
                mp.User!.FullName,
                mp.User.Username,
                mp.User.IsActive,
                mp.Position!.Name,
                mp.IsActing,
                mp.Position.SortOrder,
                mp.DepartmentId,
                mp.PartyCellId
            })
            .ToListAsync(ct);
        return rows
            .Select(r => new PositionHolding(r.UserId, r.FullName, r.Username, r.IsActive, PositionRules.DisplayName(r.Name, r.IsActing), r.SortOrder, r.DepartmentId, r.PartyCellId))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<Dictionary<Guid, MemberUnits>> GetMemberUnitsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<Guid, MemberUnits>();
        var rows = await _db.PartyMemberProfiles.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .Select(m => new { m.Id, m.DepartmentId, m.PartyCellId })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => new MemberUnits(r.DepartmentId, r.PartyCellId));
    }

    /// <inheritdoc />
    public async Task<OrgPaths> GetOrgPathsAsync(CancellationToken ct = default)
    {
        var departments = await _db.AdministrativeDepartments.AsNoTracking()
            .Select(d => new { d.Id, d.Path }).ToDictionaryAsync(d => d.Id, d => d.Path, ct);
        var cells = await _db.PartyCells.AsNoTracking()
            .Select(c => new { c.Id, c.Path }).ToDictionaryAsync(c => c.Id, c => c.Path, ct);
        return new OrgPaths(departments, cells);
    }

    /// <inheritdoc />
    public Task<List<PartyMemberProfile>> ListMembersWithDerivedAuthorityAsync(CancellationToken ct = default)
        => _db.PartyMemberProfiles
            .Where(m => m.ApprovalAuthorityOverride == null && _db.MemberPositions.Any(mp => mp.UserId == m.Id))
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<PartyMemberProfile?> FindMemberAsync(Guid userId, CancellationToken ct = default)
        => _db.PartyMemberProfiles.FirstOrDefaultAsync(m => m.Id == userId, ct);

    /// <inheritdoc />
    public Task<bool> PartyCellIsActiveAsync(Guid id, CancellationToken ct = default)
        => _db.PartyCells.AnyAsync(c => c.Id == id && c.IsActive, ct);

    /// <inheritdoc />
    public Task<bool> DepartmentIsActiveAsync(Guid id, CancellationToken ct = default)
        => _db.AdministrativeDepartments.AnyAsync(d => d.Id == id && d.IsActive, ct);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
