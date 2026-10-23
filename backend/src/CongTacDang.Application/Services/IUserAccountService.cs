using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Vòng đời tài khoản. Khóa, xóa, đặt lại mật khẩu đổi dấu bảo mật, thu hồi refresh token và xóa cache
/// → có hiệu lực ngay ở request kế tiếp.
/// </summary>
public interface IUserAccountService
{
    /// <summary>Mật khẩu tạm ngẫu nhiên, bắt buộc đổi ở lần đăng nhập đầu; trùng tên (kể cả tài khoản đã xóa) → 409.</summary>
    Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default);

    Task<PagedResult<AccountListItemDto>> SearchAsync(AccountSearchQuery query, CancellationToken ct = default);

    Task<AccountListItemDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Trường null giữ nguyên.</summary>
    Task<AccountListItemDto> UpdateAsync(Guid id, UpdateAccountCommand cmd, CancellationToken ct = default);

    /// <summary>Không tự khóa mình, không khóa quản trị viên cuối cùng (409).</summary>
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    /// <summary>Mở khóa do nhập sai mật khẩu nhiều lần, không đổi mật khẩu.</summary>
    Task UnlockAsync(Guid id, CancellationToken ct = default);

    /// <summary>Mật khẩu tạm trả về đúng một lần; mở khóa đăng nhập tạm thời và thu hồi mọi phiên.</summary>
    Task<CreatedAccount> ResetPasswordAsync(Guid id, CancellationToken ct = default);

    /// <summary>Tên đăng nhập không được tái sử dụng; chốt chặn như <see cref="SetActiveAsync"/>.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <param name="ApprovalAuthority">Đặt tay thẩm quyền; null hoặc bằng cấp suy ra → suy ra từ chức vụ.</param>
public sealed record CreateAccountCommand(
    string Username,
    string FullName,
    string? Email,
    string? PartyCardNumber,
    string? PositionTitle,
    Guid? DepartmentId,
    Guid? PartyCellId,
    ApprovalAuthority? ApprovalAuthority = null)
{
    public string? PhoneNumber { get; init; }

    /// <summary>Bắt buộc khi <see cref="ApprovalAuthority"/> khác cấp suy ra.</summary>
    public string? ApprovalAuthorityReason { get; init; }

    /// <summary>Theo bộ tiêu chí, ví dụ "K2"; trống → chưa chọn.</summary>
    public string? WeightFrameCode { get; init; }

    /// <summary>"yyyy-MM-dd"; trống → chưa nhập.</summary>
    public string? DateOfBirth { get; init; }
}

/// <summary><see cref="TemporaryPassword"/> chỉ trả về một lần, không lưu dạng rõ.</summary>
public sealed record CreatedAccount(Guid UserId, string Username, string TemporaryPassword);

/// <summary>
/// Null → giữ nguyên; <c>Guid.Empty</c> ở đơn vị → bỏ gán; chuỗi rỗng → xóa.
/// Thẩm quyền đặt tay qua <see cref="IPositionService.SetApprovalAuthorityOverrideAsync"/>.
/// </summary>
public sealed record UpdateAccountCommand(
    string? FullName = null,
    string? Email = null,
    string? PhoneNumber = null,
    string? PartyCardNumber = null,
    string? PositionTitle = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null)
{
    /// <summary>Null → giữ nguyên, chuỗi rỗng → bỏ chọn.</summary>
    public string? WeightFrameCode { get; init; }

    /// <summary>"yyyy-MM-dd"; null → giữ nguyên, chuỗi rỗng → xóa.</summary>
    public string? DateOfBirth { get; init; }
}

/// <param name="PageSize">Mặc định 20, tối đa 200.</param>
public sealed record AccountSearchQuery(
    int? Page = null,
    int? PageSize = null,
    string? Query = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null,
    bool? IsActive = null);
