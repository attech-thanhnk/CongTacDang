using System.Text.Json;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Imports;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>Tra cứu phục vụ kiểm tra chéo khi import (chỉ đọc, kể cả bản ghi đã xóa mềm).</summary>
public sealed class ImportLookup : IImportLookup
{
    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo.</summary>
    public ImportLookup(CongTacDangDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogLookupEntry>> GetDepartmentsAsync(CancellationToken ct)
        => await _db.AdministrativeDepartments.IgnoreQueryFilters().AsNoTracking()
            .Select(d => new CatalogLookupEntry(d.Id, d.Code, d.Name, d.IsActive, d.IsDeleted, d.ParentId))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CatalogLookupEntry>> GetPartyCellsAsync(CancellationToken ct)
        => await _db.PartyCells.IgnoreQueryFilters().AsNoTracking()
            .Select(c => new CatalogLookupEntry(c.Id, c.Code, c.Name, c.IsActive, c.IsDeleted, c.ParentId))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> FindExistingUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        var wanted = usernames.Select(u => u.ToLowerInvariant()).Distinct().ToList();
        if (wanted.Count == 0)
            return new HashSet<string>();

        var found = await _db.PartyMemberProfiles.IgnoreQueryFilters().AsNoTracking()
            .Where(m => wanted.Contains(m.Username.ToLower()))
            .Select(m => m.Username.ToLower())
            .ToListAsync(ct);
        return found.ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>Ghi một bản ghi audit tóm tắt cho mỗi lần xác nhận import (lưu cùng transaction của khung).</summary>
public sealed class ImportAuditLog : IImportAuditLog
{
    private readonly CongTacDangDbContext _db;
    private readonly ICurrentUserService _currentUser;

    /// <summary>Khởi tạo.</summary>
    public ImportAuditLog(CongTacDangDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public void Add(string kind, string displayName, string? fileName, int rowCount, int created, int updated)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = _currentUser.UserId,
            ActorName = string.IsNullOrWhiteSpace(_currentUser.UserName) ? "system" : _currentUser.UserName,
            Action = "Import",
            EntityType = "Import",
            EntityId = kind,
            OldValues = "{}",
            NewValues = JsonSerializer.Serialize(new { kind, displayName, fileName, rows = rowCount, created, updated }),
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            RequestPath = _currentUser.RequestPath,
            CreatedAt = DateTime.UtcNow
        });
    }
}
