using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Tạo tài khoản đăng nhập cho cán bộ (dùng chung cho giao diện quản trị và chức năng nhập dữ liệu).
/// Task 07 cung cấp bản v0 tối thiểu; task 08 triển khai lại đầy đủ (kiểm tra danh mục, gán vai trò, nhật ký…).
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// Tạo tài khoản với mật khẩu tạm ngẫu nhiên, bắt buộc đổi mật khẩu ở lần đăng nhập đầu.
    /// Ném <see cref="Common.Exceptions.ValidationException"/> (400) khi dữ liệu thiếu,
    /// <see cref="Common.Exceptions.ConflictException"/> (409) khi tên đăng nhập đã tồn tại.
    /// </summary>
    Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default);
}

/// <summary>Dữ liệu tạo tài khoản.</summary>
/// <param name="Username">Tên đăng nhập (bắt buộc, không trùng).</param>
/// <param name="FullName">Họ và tên (bắt buộc).</param>
/// <param name="Email">Thư điện tử.</param>
/// <param name="PartyCardNumber">Số thẻ Đảng viên; có giá trị → là Đảng viên.</param>
/// <param name="PositionTitle">Chức danh hiển thị trên văn bản.</param>
/// <param name="DepartmentId">Phòng / đơn vị.</param>
/// <param name="PartyCellId">Chi bộ sinh hoạt.</param>
/// <param name="ApprovalAuthority">Cấp có thẩm quyền quyết định xếp loại.</param>
public sealed record CreateAccountCommand(
    string Username,
    string FullName,
    string? Email,
    string? PartyCardNumber,
    string? PositionTitle,
    Guid? DepartmentId,
    Guid? PartyCellId,
    ApprovalAuthority ApprovalAuthority);

/// <summary>Kết quả tạo tài khoản; <see cref="TemporaryPassword"/> chỉ trả về một lần để giao cho người dùng.</summary>
/// <param name="UserId">Id tài khoản mới.</param>
/// <param name="Username">Tên đăng nhập đã chuẩn hóa.</param>
/// <param name="TemporaryPassword">Mật khẩu tạm (không lưu dạng rõ).</param>
public sealed record CreatedAccount(Guid UserId, string Username, string TemporaryPassword);
