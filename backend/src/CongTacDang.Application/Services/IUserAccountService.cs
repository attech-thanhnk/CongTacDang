using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Vòng đời tài khoản đăng nhập của cán bộ (dùng chung cho giao diện quản trị và chức năng nhập dữ liệu):
/// tạo, sửa, khóa/mở, mở khóa đăng nhập, đặt lại mật khẩu, xóa, tra cứu.
/// Mọi thao tác làm mất hiệu lực phiên (khóa, xóa, đặt lại mật khẩu) đổi dấu bảo mật, thu hồi refresh token
/// và xóa cache quyền/trạng thái → có hiệu lực ngay ở request kế tiếp.
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// Tạo tài khoản với mật khẩu tạm ngẫu nhiên, bắt buộc đổi mật khẩu ở lần đăng nhập đầu.
    /// Ném <see cref="Common.Exceptions.ValidationException"/> (400) khi dữ liệu thiếu/sai
    /// (tên đăng nhập sai mẫu, email sai định dạng, Phòng/Chi bộ không tồn tại hoặc ngừng hoạt động),
    /// <see cref="Common.Exceptions.ConflictException"/> (409) khi tên đăng nhập đã được dùng (kể cả tài khoản đã xóa).
    /// </summary>
    Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Như <see cref="CreateAsync"/> (cùng kiểm tra, cùng lỗi) nhưng <b>không lưu</b>: tài khoản chỉ được đưa vào đơn vị công việc
    /// hiện tại. Người gọi phải gọi <see cref="Common.Interfaces.IUnitOfWork.SaveChangesAsync"/> — thường trong
    /// <see cref="Common.Interfaces.IUnitOfWork.ExecuteInTransactionAsync"/> — để ghi cả lô một lần (ví dụ nhập dữ liệu):
    /// một dòng lỗi → không tài khoản nào được ghi. Trùng tên đăng nhập được kiểm tra cả với tài khoản đã đưa vào
    /// nhưng chưa lưu trong cùng đơn vị công việc.
    /// </summary>
    Task<CreatedAccount> StageCreateAsync(CreateAccountCommand cmd, CancellationToken ct = default);

    /// <summary>Tra cứu, phân trang tài khoản trong phạm vi người xem được phép.</summary>
    Task<PagedResult<AccountListItemDto>> SearchAsync(AccountSearchQuery query, CancellationToken ct = default);

    /// <summary>Chi tiết một tài khoản; <see cref="Common.Exceptions.NotFoundException"/> nếu không có.</summary>
    Task<AccountListItemDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Cập nhật thông tin tài khoản (trường null giữ nguyên).</summary>
    Task<AccountListItemDto> UpdateAsync(Guid id, UpdateAccountCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Mở (<paramref name="isActive"/> = true) hoặc khóa tài khoản. Khóa: đổi dấu bảo mật, thu hồi mọi phiên;
    /// chốt chặn: không tự khóa mình, không khóa quản trị viên cuối cùng (409).
    /// </summary>
    Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default);

    /// <summary>Mở khóa đăng nhập tạm thời (do nhập sai mật khẩu nhiều lần), không đổi mật khẩu.</summary>
    Task UnlockAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Đặt lại mật khẩu tạm (trả về đúng một lần), bắt buộc đổi mật khẩu, mở khóa đăng nhập tạm thời,
    /// đổi dấu bảo mật và thu hồi mọi phiên.
    /// </summary>
    Task<CreatedAccount> ResetPasswordAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Xóa mềm tài khoản: thu hồi mọi phiên, tên đăng nhập không được tái sử dụng;
    /// chốt chặn như <see cref="SetActiveAsync"/>.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Dữ liệu tạo tài khoản.</summary>
/// <param name="Username">Tên đăng nhập (bắt buộc, không trùng).</param>
/// <param name="FullName">Họ và tên (bắt buộc).</param>
/// <param name="Email">Thư điện tử.</param>
/// <param name="PartyCardNumber">Số thẻ Đảng viên; có giá trị → là Đảng viên.</param>
/// <param name="PositionTitle">Chức danh hiển thị trên văn bản.</param>
/// <param name="DepartmentId">Đơn vị công tác chính (đơn vị chính quyền).</param>
/// <param name="PartyCellId">Nơi sinh hoạt Đảng (tổ chức Đảng).</param>
/// <param name="ApprovalAuthority">
/// Thẩm quyền phê duyệt <b>đặt tay</b> (ghi đè); null → suy ra từ chức vụ (tài khoản mới chưa có chức vụ → CoSo).
/// </param>
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
    /// <summary>Số điện thoại (tùy chọn).</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Lý do đặt tay thẩm quyền (khi <see cref="ApprovalAuthority"/> có giá trị; trống → "Đặt khi tạo tài khoản").</summary>
    public string? ApprovalAuthorityReason { get; init; }

    /// <summary>Mã khung tỷ trọng A-B-C-D mặc định (theo bộ tiêu chí, ví dụ "K2"); trống → chưa chọn.</summary>
    public string? WeightFrameCode { get; init; }
}

/// <summary>Kết quả tạo tài khoản; <see cref="TemporaryPassword"/> chỉ trả về một lần để giao cho người dùng.</summary>
/// <param name="UserId">Id tài khoản mới.</param>
/// <param name="Username">Tên đăng nhập đã chuẩn hóa.</param>
/// <param name="TemporaryPassword">Mật khẩu tạm (không lưu dạng rõ).</param>
public sealed record CreatedAccount(Guid UserId, string Username, string TemporaryPassword);

/// <summary>
/// Dữ liệu cập nhật tài khoản; null → giữ nguyên; <c>Guid.Empty</c> ở đơn vị/tổ chức Đảng → bỏ gán; chuỗi rỗng → xóa.
/// Thẩm quyền phê duyệt không sửa ở đây — suy ra từ chức vụ, đặt tay qua <see cref="IPositionService.SetApprovalAuthorityOverrideAsync"/>.
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
    /// <summary>Mã khung tỷ trọng A-B-C-D mặc định; null → giữ nguyên, chuỗi rỗng → bỏ chọn.</summary>
    public string? WeightFrameCode { get; init; }
}

/// <summary>Tham số tra cứu tài khoản.</summary>
/// <param name="Page">Trang (mặc định 1).</param>
/// <param name="PageSize">Kích thước trang (mặc định 20, tối đa 200).</param>
/// <param name="Query">Từ khóa.</param>
/// <param name="DepartmentId">Lọc theo Phòng.</param>
/// <param name="PartyCellId">Lọc theo Chi bộ.</param>
/// <param name="IsActive">Lọc theo trạng thái.</param>
public sealed record AccountSearchQuery(
    int? Page = null,
    int? PageSize = null,
    string? Query = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null,
    bool? IsActive = null);
