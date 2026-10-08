using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Repository danh mục kiểu trình bày biểu mẫu tự đánh giá.</summary>
public sealed class FormLayoutRepository : IFormLayoutRepository
{
    private readonly CongTacDangDbContext _db;

    public FormLayoutRepository(CongTacDangDbContext db) => _db = db;

    /// <inheritdoc />
    public Task<List<FormLayout>> ListAsync(CancellationToken ct = default)
        => _db.FormLayouts.AsNoTracking().OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ToListAsync(ct);

    /// <inheritdoc />
    public Task<FormLayout?> FindAsync(Guid id, CancellationToken ct = default)
        => _db.FormLayouts.FirstOrDefaultAsync(l => l.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return _db.FormLayouts.AnyAsync(l => l.Name.ToLower() == normalized && (excludeId == null || l.Id != excludeId), ct);
    }

    /// <inheritdoc />
    public Task<List<FormLayout>> ListDefaultsAsync(CancellationToken ct = default)
        => _db.FormLayouts.Where(l => l.IsDefault).ToListAsync(ct);

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> CountPositionsAsync(CancellationToken ct = default)
        => (await _db.Positions.AsNoTracking()
                .Where(p => p.FormLayoutId != null)
                .GroupBy(p => p.FormLayoutId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> CountMembersAsync(CancellationToken ct = default)
        => (await _db.PartyMemberProfiles.AsNoTracking()
                .Where(m => m.FormLayoutOverrideId != null)
                .GroupBy(m => m.FormLayoutOverrideId!.Value)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);

    /// <inheritdoc />
    public Task<PartyMemberProfile?> FindMemberAsync(Guid userId, CancellationToken ct = default)
        => _db.PartyMemberProfiles.Include(m => m.FormLayoutOverride).FirstOrDefaultAsync(m => m.Id == userId, ct);

    /// <inheritdoc />
    public Task<List<MemberPosition>> ListHeldPositionsAsync(Guid userId, DateTime at, CancellationToken ct = default)
        => _db.MemberPositions.AsNoTracking()
            .Include(mp => mp.Position).ThenInclude(p => p!.FormLayout)
            .Where(mp => mp.UserId == userId && mp.Position != null && mp.ValidFrom <= at && (mp.ValidTo == null || mp.ValidTo > at))
            .OrderByDescending(mp => mp.IsPrimary).ThenBy(mp => mp.Position!.SortOrder)
            .ToListAsync(ct);

    /// <inheritdoc />
    public void Add(FormLayout layout) => _db.FormLayouts.Add(layout);

    /// <inheritdoc />
    public void Remove(FormLayout layout) => _db.FormLayouts.Remove(layout);
}
