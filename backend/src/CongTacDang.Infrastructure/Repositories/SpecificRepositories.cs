using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>
/// Repository quản lý hồ sơ Cán bộ / Đảng viên
/// </summary>
public class UserRepository : GenericRepository<PartyMemberProfile>, IUserRepository
{
    public UserRepository(CongTacDangDbContext db) : base(db)
    {
    }

    /// <summary>Tìm kiếm cán bộ theo tên đăng nhập</summary>
    public async Task<PartyMemberProfile?> GetByUsernameAsync(string username)
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .FirstOrDefaultAsync(m => m.Username == username);
    }

    /// <summary>Lấy thông tin cán bộ đầu tiên trong hệ thống</summary>
    public async Task<PartyMemberProfile?> GetFirstMemberAsync()
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>Lấy toàn bộ danh sách cán bộ kèm thông tin Chi bộ và Phòng ban</summary>
    public async Task<List<PartyMemberProfile>> GetAllWithDetailsAsync()
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.FullName)
            .ToListAsync();
    }

    /// <summary>Tìm kiếm cán bộ theo tên đăng nhập kèm thông tin Vai trò và Quyền hạn</summary>
    public async Task<PartyMemberProfile?> GetWithRolesAndPermissionsAsync(string username)
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .Include(m => m.Roles)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(m => m.Username == username);
    }

    /// <summary>Tìm kiếm cán bộ theo Id kèm thông tin Vai trò và Quyền hạn</summary>
    public async Task<PartyMemberProfile?> GetWithRolesAndPermissionsByIdAsync(Guid id)
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .Include(m => m.Roles)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}

/// <summary>
/// Repository quản lý tệp đính kèm và minh chứng
/// </summary>
public class AttachmentRepository : GenericRepository<TaskAttachment>, IAttachmentRepository
{
    public AttachmentRepository(CongTacDangDbContext db) : base(db)
    {
    }

    /// <summary>Lấy toàn bộ danh sách tệp đính kèm theo thời gian mới nhất</summary>
    public async Task<List<TaskAttachment>> GetAllAttachmentsAsync()
    {
        return await _db.TaskAttachments
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }
}

/// <summary>
/// Repository quản lý tổ chức Chi bộ và Phòng ban
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    private readonly CongTacDangDbContext _db;

    public OrganizationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy danh sách Chi bộ kèm Đảng viên trực thuộc</summary>
    public async Task<List<PartyCell>> GetPartyCellsWithMembersAsync()
    {
        return await _db.PartyCells
            .Include(c => c.Members)
            .OrderBy(c => c.Code)
            .ToListAsync();
    }

    /// <summary>Lấy danh sách Phòng ban chuyên môn kèm cán bộ trực thuộc</summary>
    public async Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync()
    {
        return await _db.AdministrativeDepartments
            .Include(d => d.Members)
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    /// <summary>Lấy thông tin Chi bộ theo Id</summary>
    public async Task<PartyCell?> GetPartyCellByIdAsync(Guid id)
    {
        return await _db.PartyCells
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>Thêm mới Chi bộ</summary>
    public async Task AddPartyCellAsync(PartyCell cell)
    {
        await _db.PartyCells.AddAsync(cell);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cập nhật thông tin Chi bộ</summary>
    public async Task UpdatePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Update(cell);
        await _db.SaveChangesAsync();
    }

    /// <summary>Xóa Chi bộ khỏi cơ sở dữ liệu</summary>
    public async Task DeletePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Remove(cell);
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Repository quản trị Vai trò và Quyền hạn
/// </summary>
public class RoleRepository : IRoleRepository
{
    private readonly CongTacDangDbContext _db;

    public RoleRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    public async Task<List<AppRole>> GetAllRolesWithPermissionsAsync()
    {
        return await _db.Roles
            .Include(r => r.Permissions)
            .OrderBy(r => r.Code)
            .ToListAsync();
    }

    public async Task<List<Permission>> GetAllPermissionsAsync()
    {
        return await _db.Permissions
            .OrderBy(p => p.Resource)
            .ThenBy(p => p.Action)
            .ToListAsync();
    }

    public async Task<AppRole?> GetRoleByIdWithPermissionsAsync(Guid roleId)
    {
        return await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);
    }

    public async Task<AppRole?> GetRoleByCodeAsync(string roleCode)
    {
        return await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Code == roleCode);
    }

    public async Task UpdateRolePermissionsAsync(Guid roleId, IEnumerable<string> permissionCodes)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null)
            throw new KeyNotFoundException($"Không tìm thấy vai trò với Id: {roleId}");

        var targetCodes = permissionCodes.Distinct().ToList();
        var permissions = await _db.Permissions
            .Where(p => targetCodes.Contains(p.Code))
            .ToListAsync();

        role.Permissions.Clear();
        foreach (var p in permissions)
        {
            role.Permissions.Add(p);
        }

        await _db.SaveChangesAsync();
    }

    public async Task AssignRolesToUserAsync(Guid userId, IEnumerable<string> roleCodes)
    {
        var member = await _db.PartyMemberProfiles
            .Include(m => m.Roles)
            .FirstOrDefaultAsync(m => m.Id == userId);

        if (member == null)
            throw new KeyNotFoundException($"Không tìm thấy cán bộ với Id: {userId}");

        var targetCodes = roleCodes.Distinct().ToList();
        var roles = await _db.Roles
            .Where(r => targetCodes.Contains(r.Code))
            .ToListAsync();

        member.Roles.Clear();
        foreach (var r in roles)
        {
            member.Roles.Add(r);
        }

        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Repository triển khai quản lý kỳ đánh giá và hồ sơ đánh giá cán bộ theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class EvaluationRepository : IEvaluationRepository
{
    private readonly CongTacDangDbContext _db;

    public EvaluationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy danh sách tất cả các kỳ đánh giá</summary>
    public async Task<List<EvaluationPeriod>> GetPeriodsAsync()
    {
        return await _db.EvaluationPeriods
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Quarter)
            .ToListAsync();
    }

    /// <summary>Lấy thông tin kỳ đánh giá theo Id</summary>
    public async Task<EvaluationPeriod?> GetPeriodByIdAsync(Guid id)
    {
        return await _db.EvaluationPeriods.FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>Lấy kỳ đánh giá đang kích hoạt</summary>
    public async Task<EvaluationPeriod?> GetActivePeriodAsync()
    {
        return await _db.EvaluationPeriods.FirstOrDefaultAsync(p => p.IsActive);
    }

    /// <summary>Thêm mới kỳ đánh giá</summary>
    public async Task AddPeriodAsync(EvaluationPeriod period)
    {
        _db.EvaluationPeriods.Add(period);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cập nhật kỳ đánh giá</summary>
    public async Task UpdatePeriodAsync(EvaluationPeriod period)
    {
        _db.EvaluationPeriods.Update(period);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lấy hồ sơ đánh giá của một cán bộ trong kỳ cụ thể</summary>
    public async Task<EvaluationRecord?> GetRecordAsync(Guid periodId, Guid memberId)
    {
        return await _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .FirstOrDefaultAsync(r => r.PeriodId == periodId && r.MemberId == memberId);
    }

    /// <summary>Lấy hồ sơ đánh giá theo Id</summary>
    public async Task<EvaluationRecord?> GetRecordByIdAsync(Guid id)
    {
        return await _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    /// <summary>Lấy toàn bộ hồ sơ đánh giá trong một kỳ</summary>
    public async Task<List<EvaluationRecord>> GetRecordsByPeriodAsync(Guid periodId)
    {
        return await _db.EvaluationRecords
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .Where(r => r.PeriodId == periodId)
            .OrderBy(r => r.Member.FullName)
            .ToListAsync();
    }

    /// <summary>Lấy danh sách hồ sơ đánh giá của một Chi bộ</summary>
    public async Task<List<EvaluationRecord>> GetRecordsByBranchAsync(Guid periodId, Guid branchId)
    {
        return await _db.EvaluationRecords
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .Where(r => r.PeriodId == periodId && r.Member.PartyCellId == branchId)
            .OrderBy(r => r.Member.FullName)
            .ToListAsync();
    }

    /// <summary>Thêm mới hồ sơ đánh giá</summary>
    public async Task AddRecordAsync(EvaluationRecord record)
    {
        _db.EvaluationRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cập nhật hồ sơ đánh giá</summary>
    public async Task UpdateRecordAsync(EvaluationRecord record)
    {
        _db.EvaluationRecords.Update(record);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lấy danh sách công việc đăng ký</summary>
    public async Task<List<EvaluationTask>> GetTasksByRecordIdAsync(Guid recordId)
    {
        return await _db.EvaluationTasks
            .Include(t => t.Attachment)
            .Where(t => t.RecordId == recordId)
            .OrderBy(t => t.TaskOrder)
            .ToListAsync();
    }

    /// <summary>Thay thế danh sách công việc của hồ sơ đánh giá</summary>
    public async Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks)
    {
        var existingTasks = await _db.EvaluationTasks
            .Where(t => t.RecordId == recordId)
            .ToListAsync();

        _db.EvaluationTasks.RemoveRange(existingTasks);
        _db.EvaluationTasks.AddRange(tasks);
        await _db.SaveChangesAsync();
    }
}


