using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện repository quản lý hồ sơ Cán bộ / Đảng viên
/// </summary>
public interface IUserRepository : IRepository<PartyMemberProfile>
{
    /// <summary>Tìm kiếm cán bộ theo tên đăng nhập</summary>
    Task<PartyMemberProfile?> GetByUsernameAsync(string username);

    /// <summary>Lấy thông tin cán bộ đầu tiên trong hệ thống</summary>
    Task<PartyMemberProfile?> GetFirstMemberAsync();

    /// <summary>Lấy toàn bộ danh sách cán bộ kèm thông tin Chi bộ và Phòng ban</summary>
    Task<List<PartyMemberProfile>> GetAllWithDetailsAsync();
}

/// <summary>
/// Giao diện repository quản lý tệp đính kèm và minh chứng
/// </summary>
public interface IAttachmentRepository : IRepository<TaskAttachment>
{
    /// <summary>Lấy toàn bộ danh sách tệp đính kèm theo thời gian mới nhất</summary>
    Task<List<TaskAttachment>> GetAllAttachmentsAsync();
}

/// <summary>
/// Giao diện repository quản lý tổ chức Chi bộ và Phòng ban
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>Lấy danh sách Chi bộ kèm danh sách Đảng viên</summary>
    Task<List<PartyCell>> GetPartyCellsWithMembersAsync();

    /// <summary>Lấy danh sách Phòng ban chuyên môn kèm danh sách cán bộ</summary>
    Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync();

    /// <summary>Lấy thông tin Chi bộ theo Id</summary>
    Task<PartyCell?> GetPartyCellByIdAsync(Guid id);

    /// <summary>Thêm mới Chi bộ</summary>
    Task AddPartyCellAsync(PartyCell cell);

    /// <summary>Cập nhật thông tin Chi bộ</summary>
    Task UpdatePartyCellAsync(PartyCell cell);

    /// <summary>Xóa Chi bộ</summary>
    Task DeletePartyCellAsync(PartyCell cell);
}
