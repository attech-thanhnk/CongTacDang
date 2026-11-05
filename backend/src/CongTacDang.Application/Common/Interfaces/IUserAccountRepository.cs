using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Models;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Vòng đời tài khoản và kiểm tra phiên. Không nạp vai trò/quyền — lấy qua <c>IPermissionResolver</c>.</summary>
public interface IUserAccountRepository
{
    /// <summary>Chưa xóa, không phân biệt hoa thường; được theo dõi.</summary>
    Task<PartyMemberProfile?> FindByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>Chưa xóa, kèm Phòng/Chi bộ; được theo dõi.</summary>
    Task<PartyMemberProfile?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tính <b>cả</b> tài khoản đã xóa mềm, không phân biệt hoa thường.</summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);

    Task<bool> DepartmentIsActiveAsync(Guid id, CancellationToken ct = default);

    Task<bool> PartyCellIsActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tổ chức Đảng có tổ chức con chưa xóa, đang hoạt động.</summary>
    Task<bool> PartyCellHasActiveChildrenAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kể cả tài khoản đã xóa; null nếu không tồn tại.</summary>
    Task<AccountState?> GetStateAsync(Guid userId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetActiveUserIdsAsync(CancellationToken ct = default);

    /// <summary>Chưa xóa, kèm Phòng/Chi bộ; không theo dõi.</summary>
    Task<PagedResult<PartyMemberProfile>> SearchAsync(AccountSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>Thêm và lưu ngay.</summary>
    Task AddAsync(PartyMemberProfile member, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>Xóa mềm và lưu ngay.</summary>
    Task SoftDeleteAsync(PartyMemberProfile member, CancellationToken ct = default);
}

/// <param name="Page">≥ 1.</param>
/// <param name="PageSize">1–200.</param>
/// <param name="Query">Tên đăng nhập, họ tên, email, số thẻ Đảng.</param>
/// <param name="Scope">null → toàn bộ.</param>
public sealed record AccountSearchCriteria(
    int Page,
    int PageSize,
    string? Query = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null,
    bool? IsActive = null,
    AccountScope? Scope = null,
    string? Flag = null);

/// <summary>Bộ lọc tài khoản cần quản trị xử lý; mã dùng trên đường dẫn trang Tài khoản.</summary>
public static class AccountFlags
{
    /// <summary>Đang hoạt động, bị khóa tạm do sai mật khẩu.</summary>
    public const string Locked = "locked";

    /// <summary>Đang hoạt động, chưa đăng nhập lần nào.</summary>
    public const string NeverLoggedIn = "neverLoggedIn";

    /// <summary>Đang hoạt động, đăng nhập trong <see cref="RecentLoginDays"/> ngày gần đây.</summary>
    public const string RecentLogin = "recentLogin";

    /// <summary>Có vai trò hết hạn trong <see cref="RoleExpiringDays"/> ngày tới.</summary>
    public const string RoleExpiring = "roleExpiring";

    /// <summary>Đảng viên đang hoạt động chưa có tổ chức Đảng.</summary>
    public const string NoPartyCell = "noPartyCell";

    /// <summary>Sinh hoạt tại tổ chức Đảng có tổ chức con.</summary>
    public const string CellWithChildren = "cellWithChildren";

    /// <summary>Đang hoạt động, chưa giữ chức vụ nào còn hiệu lực.</summary>
    public const string NoPosition = "noPosition";

    public const int RecentLoginDays = 7;
    public const int RoleExpiringDays = 30;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Locked, NeverLoggedIn, RecentLogin, RoleExpiring, NoPartyCell, CellWithChildren, NoPosition
    };
}

/// <summary>Tài khoản thuộc một trong các Phòng hoặc Chi bộ được phép.</summary>
public sealed record AccountScope(IReadOnlyList<Guid> DepartmentIds, IReadOnlyList<Guid> PartyCellIds);

public interface ILoginEventRepository
{
    /// <summary>Thêm và lưu ngay.</summary>
    Task AddAsync(LoginEvent loginEvent, CancellationToken ct = default);

    /// <summary>Mới nhất trước.</summary>
    Task<PagedResult<LoginEvent>> SearchAsync(LoginEventCriteria criteria, CancellationToken ct = default);
}

public sealed record LoginEventCriteria(
    int Page,
    int PageSize,
    Guid? UserId = null,
    DateTime? From = null,
    DateTime? To = null,
    LoginResult? Result = null);
