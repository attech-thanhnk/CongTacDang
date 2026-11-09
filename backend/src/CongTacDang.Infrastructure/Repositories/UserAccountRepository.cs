using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly CongTacDangDbContext _db;
    private readonly IPositionRepository _positions;

    public UserAccountRepository(CongTacDangDbContext db, IPositionRepository positions)
    {
        _db = db;
        _positions = positions;
    }

    /// <inheritdoc />
    public Task<PartyMemberProfile?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = AccountRules.NormalizeUsername(username);
        return _db.PartyMemberProfiles
            .FirstOrDefaultAsync(m => m.Username.ToLower() == normalized, ct);
    }

    /// <inheritdoc />
    public Task<PartyMemberProfile?> FindByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.PartyMemberProfiles
            .Include(m => m.Department)
            .Include(m => m.PartyCell)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
    {
        var normalized = AccountRules.NormalizeUsername(username);
        return _db.PartyMemberProfiles
            .IgnoreQueryFilters()
            .AnyAsync(m => m.Username.ToLower() == normalized, ct);
    }

    /// <inheritdoc />
    public Task<bool> DepartmentIsActiveAsync(Guid id, CancellationToken ct = default) =>
        _db.AdministrativeDepartments.AnyAsync(d => d.Id == id && d.IsActive, ct);

    /// <inheritdoc />
    public Task<bool> PartyCellIsActiveAsync(Guid id, CancellationToken ct = default) =>
        _db.PartyCells.AnyAsync(c => c.Id == id && c.IsActive, ct);

    /// <inheritdoc />
    public Task<bool> PartyCellHasActiveChildrenAsync(Guid id, CancellationToken ct = default) =>
        _db.PartyCells.AnyAsync(c => c.ParentId == id && c.IsActive, ct);

    /// <inheritdoc />
    public Task<AccountState?> GetStateAsync(Guid userId, CancellationToken ct = default) =>
        _db.PartyMemberProfiles
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Id == userId)
            .Select(m => new AccountState(m.Id, m.IsActive, m.IsDeleted, m.SecurityStamp, m.MustChangePassword))
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(CancellationToken ct = default) =>
        await _db.PartyMemberProfiles
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<PagedResult<PartyMemberProfile>> SearchAsync(AccountSearchCriteria criteria, CancellationToken ct = default)
    {
        var query = _db.PartyMemberProfiles.AsNoTracking();

        if (criteria.Scope != null)
        {
            var departments = criteria.Scope.DepartmentIds.ToList();
            var cells = criteria.Scope.PartyCellIds.ToList();
            query = query.Where(m =>
                (m.DepartmentId.HasValue && departments.Contains(m.DepartmentId.Value))
                || (m.PartyCellId.HasValue && cells.Contains(m.PartyCellId.Value)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Query))
        {
            var pattern = "%" + EscapeLike(criteria.Query.Trim()) + "%";
            query = query.Where(m =>
                EF.Functions.ILike(m.Username, pattern, "\\")
                || EF.Functions.ILike(m.FullName, pattern, "\\")
                || EF.Functions.ILike(m.Email, pattern, "\\")
                || (m.PartyCardNumber != null && EF.Functions.ILike(m.PartyCardNumber, pattern, "\\")));
        }

        if (criteria.DepartmentId.HasValue)
            query = query.Where(m => m.DepartmentId == criteria.DepartmentId);
        if (criteria.PartyCellId.HasValue)
            query = query.Where(m => m.PartyCellId == criteria.PartyCellId);
        if (criteria.IsActive.HasValue)
            query = query.Where(m => m.IsActive == criteria.IsActive.Value);
        if (criteria.Flag != null)
            query = ApplyFlag(query, criteria.Flag, DateTime.UtcNow);

        var total = await query.CountAsync(ct);
        // Hạng chức danh tính ngoài CSDL nên sắp xếp trước rồi mới lấy đúng trang.
        var rows = await query.Select(m => new { m.Id, m.FullName, Department = m.Department != null ? m.Department.Name : null }).ToListAsync(ct);
        var held = await _positions.GetHeldPositionsAsync(rows.Select(r => r.Id).ToList(), DateTime.UtcNow, ct);
        var pageIds = PositionRules.OrderBySeniority(rows, held, await _positions.GetUnitOrderAsync(ct), r => r.Id, r => r.Department, r => r.FullName)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .Select(r => r.Id)
            .ToList();
        var items = (await query
            .Include(m => m.Department)
            .Include(m => m.PartyCell)
            .Where(m => pageIds.Contains(m.Id))
            .ToListAsync(ct))
            .OrderBy(m => pageIds.IndexOf(m.Id))
            .ToList();

        return new PagedResult<PartyMemberProfile>
        {
            Items = items,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalCount = total
        };
    }

    /// <inheritdoc />
    public async Task AddAsync(PartyMemberProfile member, CancellationToken ct = default)
    {
        _db.PartyMemberProfiles.Add(member);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    /// <inheritdoc />
    public async Task SoftDeleteAsync(PartyMemberProfile member, CancellationToken ct = default)
    {
        // DbContext đổi thành xóa mềm (IsDeleted, IsActive = false) và ghi audit.
        _db.PartyMemberProfiles.Remove(member);
        await _db.SaveChangesAsync(ct);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private IQueryable<PartyMemberProfile> ApplyFlag(IQueryable<PartyMemberProfile> query, string flag, DateTime now)
    {
        var active = query.Where(m => m.IsActive);
        var recentFrom = now.AddDays(-AccountFlags.RecentLoginDays);
        var expiringTo = now.AddDays(AccountFlags.RoleExpiringDays);
        return flag switch
        {
            AccountFlags.Locked => active.Where(m => m.LockoutEnd != null && m.LockoutEnd > now),
            AccountFlags.NeverLoggedIn => active.Where(m => m.LastLoginAt == null),
            AccountFlags.RecentLogin => active.Where(m => m.LastLoginAt != null && m.LastLoginAt >= recentFrom),
            AccountFlags.RoleExpiring => active.Where(m => _db.Set<UserRoleAssignment>()
                .Any(a => a.UserId == m.Id && !a.IsDeleted && a.ValidTo != null && a.ValidTo > now && a.ValidTo <= expiringTo)),
            AccountFlags.NoPartyCell => active.Where(m => m.IsPartyMember && m.PartyCellId == null),
            AccountFlags.CellWithChildren => active.Where(m => m.PartyCellId != null
                && _db.PartyCells.Any(c => c.ParentId == m.PartyCellId && c.IsActive)),
            // Tài khoản kỹ thuật không tham gia đánh giá nên không cần chức vụ.
            AccountFlags.NoPosition => active.Where(m => _db.Set<UserRoleAssignment>()
                    .Any(a => a.UserId == m.Id && !a.IsDeleted && a.ValidFrom <= now && (a.ValidTo == null || a.ValidTo > now)
                        && a.Role!.Permissions.Any(p => p.Code == PermissionCodes.EvaluationSelf))
                && !_db.MemberPositions
                .Any(p => p.UserId == m.Id && !p.IsDeleted && p.ValidFrom <= now && (p.ValidTo == null || p.ValidTo > now))),
            _ => query
        };
    }
}

public sealed class LoginEventRepository : ILoginEventRepository
{
    private readonly CongTacDangDbContext _db;

    public LoginEventRepository(CongTacDangDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task AddAsync(LoginEvent loginEvent, CancellationToken ct = default)
    {
        _db.Set<LoginEvent>().Add(loginEvent);
        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<PagedResult<LoginEvent>> SearchAsync(LoginEventCriteria criteria, CancellationToken ct = default)
    {
        var query = _db.Set<LoginEvent>().AsNoTracking();
        if (criteria.UserId.HasValue)
            query = query.Where(e => e.UserId == criteria.UserId);
        if (criteria.From.HasValue)
            query = query.Where(e => e.CreatedAt >= criteria.From.Value);
        if (criteria.To.HasValue)
            query = query.Where(e => e.CreatedAt <= criteria.To.Value);
        if (criteria.Result.HasValue)
            query = query.Where(e => e.Result == criteria.Result.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .ThenByDescending(e => e.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

        return new PagedResult<LoginEvent>
        {
            Items = items,
            Page = criteria.Page,
            PageSize = criteria.PageSize,
            TotalCount = total
        };
    }
}
