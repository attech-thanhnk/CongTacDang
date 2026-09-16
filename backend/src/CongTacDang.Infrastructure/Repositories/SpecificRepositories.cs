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
