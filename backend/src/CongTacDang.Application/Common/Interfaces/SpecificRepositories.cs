using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

public interface IUserRepository : IRepository<PartyMemberProfile>
{
    Task<PartyMemberProfile?> GetByUsernameAsync(string username);
    Task<PartyMemberProfile?> GetFirstMemberAsync();
    Task<List<PartyMemberProfile>> GetAllWithDetailsAsync();
}

public interface IAttachmentRepository : IRepository<TaskAttachment>
{
    Task<List<TaskAttachment>> GetAllAttachmentsAsync();
}

public interface IOrganizationRepository
{
    Task<List<PartyCell>> GetPartyCellsWithMembersAsync();
    Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync();
    Task<PartyCell?> GetPartyCellByIdAsync(Guid id);
    Task AddPartyCellAsync(PartyCell cell);
    Task UpdatePartyCellAsync(PartyCell cell);
    Task DeletePartyCellAsync(PartyCell cell);
}
