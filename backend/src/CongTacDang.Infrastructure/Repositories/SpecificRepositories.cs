using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Infrastructure.Repositories;

public class UserRepository : GenericRepository<PartyMemberProfile>, IUserRepository
{
    public UserRepository(CongTacDangDbContext db) : base(db)
    {
    }

    public async Task<PartyMemberProfile?> GetByUsernameAsync(string username)
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .FirstOrDefaultAsync(m => m.Username == username);
    }

    public async Task<PartyMemberProfile?> GetFirstMemberAsync()
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PartyMemberProfile>> GetAllWithDetailsAsync()
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.FullName)
            .ToListAsync();
    }
}

public class AttachmentRepository : GenericRepository<TaskAttachment>, IAttachmentRepository
{
    public AttachmentRepository(CongTacDangDbContext db) : base(db)
    {
    }

    public async Task<List<TaskAttachment>> GetAllAttachmentsAsync()
    {
        return await _db.TaskAttachments
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }
}

public class OrganizationRepository : IOrganizationRepository
{
    private readonly CongTacDangDbContext _db;

    public OrganizationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    public async Task<List<PartyCell>> GetPartyCellsWithMembersAsync()
    {
        return await _db.PartyCells
            .Include(c => c.Members)
            .OrderBy(c => c.Code)
            .ToListAsync();
    }

    public async Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync()
    {
        return await _db.AdministrativeDepartments
            .Include(d => d.Members)
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    public async Task<PartyCell?> GetPartyCellByIdAsync(Guid id)
    {
        return await _db.PartyCells
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddPartyCellAsync(PartyCell cell)
    {
        await _db.PartyCells.AddAsync(cell);
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Update(cell);
        await _db.SaveChangesAsync();
    }

    public async Task DeletePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Remove(cell);
        await _db.SaveChangesAsync();
    }
}
