using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Models;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Truy vấn phục vụ vòng đời tài khoản và kiểm tra phiên (task 08). Không nạp vai trò/quyền —
/// thông tin quyền lấy qua <c>IPermissionResolver</c>.
/// </summary>
public interface IUserAccountRepository
{
    /// <summary>Tìm tài khoản chưa xóa theo tên đăng nhập, không phân biệt hoa thường (có theo dõi thay đổi).</summary>
    Task<PartyMemberProfile?> FindByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Tìm tài khoản chưa xóa theo Id kèm Phòng/Chi bộ (có theo dõi thay đổi).</summary>
    Task<PartyMemberProfile?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tên đăng nhập đã được dùng, <b>kể cả</b> tài khoản đã xóa mềm (không phân biệt hoa thường).</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);

    /// <summary>Phòng/đơn vị tồn tại, chưa xóa và đang hoạt động.</summary>
    Task<bool> DepartmentIsActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chi bộ tồn tại, chưa xóa và đang hoạt động.</summary>
    Task<bool> PartyCellIsActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Trạng thái tài khoản dùng để kiểm tra phiên (kể cả tài khoản đã xóa); null nếu không tồn tại.</summary>
    Task<AccountState?> GetStateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Id các tài khoản đang hoạt động, chưa xóa.</summary>
    Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(CancellationToken ct = default);

    /// <summary>Tìm kiếm, phân trang danh sách tài khoản chưa xóa (kèm Phòng/Chi bộ, không theo dõi thay đổi).</summary>
    Task<PagedResult<PartyMemberProfile>> SearchAsync(AccountSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>Thêm tài khoản mới và lưu.</summary>
    Task AddAsync(PartyMemberProfile member, CancellationToken ct = default);

    /// <summary>Đưa tài khoản mới vào đơn vị công việc, <b>chưa lưu</b> (lưu cùng lần <c>SaveChanges</c> của người gọi).</summary>
    void Stage(PartyMemberProfile member);

    /// <summary>Lưu các thay đổi trên tài khoản đang được theo dõi.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Xóa mềm tài khoản đang được theo dõi và lưu.</summary>
    Task SoftDeleteAsync(PartyMemberProfile member, CancellationToken ct = default);
}

/// <summary>Điều kiện tìm kiếm tài khoản.</summary>
/// <param name="Page">Trang (≥ 1).</param>
/// <param name="PageSize">Kích thước trang (1–200).</param>
/// <param name="Query">Từ khóa: tên đăng nhập, họ tên, email, số thẻ Đảng.</param>
/// <param name="DepartmentId">Lọc theo Phòng.</param>
/// <param name="PartyCellId">Lọc theo Chi bộ.</param>
/// <param name="IsActive">Lọc theo trạng thái hoạt động.</param>
/// <param name="Scope">Giới hạn phạm vi dữ liệu người xem được phép (null → toàn bộ).</param>
public sealed record AccountSearchCriteria(
    int Page,
    int PageSize,
    string? Query = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null,
    bool? IsActive = null,
    AccountScope? Scope = null);

/// <summary>Phạm vi dữ liệu: tài khoản thuộc một trong các Phòng hoặc Chi bộ được phép.</summary>
public sealed record AccountScope(IReadOnlyList<Guid> DepartmentIds, IReadOnlyList<Guid> PartyCellIds);

/// <summary>Ghi và tra cứu nhật ký đăng nhập.</summary>
public interface ILoginEventRepository
{
    /// <summary>Thêm một bản ghi đăng nhập và lưu ngay.</summary>
    Task AddAsync(LoginEvent loginEvent, CancellationToken ct = default);

    /// <summary>Tra cứu nhật ký đăng nhập (mới nhất trước).</summary>
    Task<PagedResult<LoginEvent>> SearchAsync(LoginEventCriteria criteria, CancellationToken ct = default);
}

/// <summary>Điều kiện tra cứu nhật ký đăng nhập.</summary>
public sealed record LoginEventCriteria(
    int Page,
    int PageSize,
    Guid? UserId = null,
    DateTime? From = null,
    DateTime? To = null,
    LoginResult? Result = null);
