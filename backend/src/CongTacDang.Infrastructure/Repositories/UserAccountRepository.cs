using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Truy vấn tài khoản cho vòng đời tài khoản và kiểm tra phiên (task 08).</summary>
public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo repository.</summary>
    public UserAccountRepository(CongTacDangDbContext db) => _db = db;

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
        // Tài khoản đã đưa vào đơn vị công việc nhưng chưa lưu (nhập theo lô) cũng tính là đã dùng.
        if (_db.ChangeTracker.Entries<PartyMemberProfile>().Any(e =>
                e.State == EntityState.Added && AccountRules.NormalizeUsername(e.Entity.Username) == normalized))
            return Task.FromResult(true);
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

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(m => m.Department)
            .Include(m => m.PartyCell)
            .OrderBy(m => m.FullName)
            .ThenBy(m => m.Username)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);

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
    public void Stage(PartyMemberProfile member) => _db.PartyMemberProfiles.Add(member);

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    /// <inheritdoc />
    public async Task SoftDeleteAsync(PartyMemberProfile member, CancellationToken ct = default)
    {
        // DbContext đổi thao tác xóa thành xóa mềm (IsDeleted, IsActive = false) và ghi audit.
        _db.PartyMemberProfiles.Remove(member);
        await _db.SaveChangesAsync(ct);
    }

    private static string EscapeLike(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}

/// <summary>Ghi và tra cứu nhật ký đăng nhập.</summary>
public sealed class LoginEventRepository : ILoginEventRepository
{
    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo repository.</summary>
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
