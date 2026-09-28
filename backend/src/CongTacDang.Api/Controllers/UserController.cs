using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Api.Controllers;

/// <summary>Quản lý tài khoản và hồ sơ cán bộ.</summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IUserAccountService _accounts;

    public UserController(IUserService userService, IUserAccountService accounts)
    {
        _userService = userService;
        _accounts = accounts;
    }

    /// <summary>
    /// Lấy hồ sơ và vai trò của cán bộ đang đăng nhập. Tham số <paramref name="username"/> chỉ dùng được
    /// cho hồ sơ của chính mình hoặc khi người yêu cầu có quyền xem hồ sơ người khác ở phạm vi quản trị.
    /// </summary>
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile([FromQuery] string? username = null)
    {
        try
        {
            var requesterId = User.GetUserId()
                ?? throw new UnauthorizedAccessException("Không xác thực được danh tính người dùng hiện tại.");

            var profile = await _userService.GetProfileForRequesterAsync(requesterId, username);
            return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Lấy thông tin hồ sơ cán bộ thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Danh sách tài khoản có phân trang và tìm kiếm (trong phạm vi được phép xem).
    /// <paramref name="q"/> tìm theo tên đăng nhập, họ tên, email, số thẻ Đảng; <paramref name="pageSize"/> tối đa 200.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCodes.SystemUsersRead)]
    public async Task<IActionResult> Search(
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        [FromQuery] string? q = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] Guid? partyCellId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _accounts.SearchAsync(new AccountSearchQuery(page, pageSize, q, departmentId, partyCellId, isActive), ct);
        return Ok(ApiResponse<PagedResult<AccountListItemDto>>.Ok(result, "Lấy danh sách tài khoản thành công."));
    }

    /// <summary>(Tạm giữ cho giao diện cũ) Danh sách toàn bộ cán bộ, không phân trang. Dùng <c>GET /api/users</c>.</summary>
    [HttpGet("list")]
    [Authorize(Policy = AppPermissions.UsersRead)]
    public async Task<IActionResult> GetUserList()
    {
        var cadres = await _userService.GetCadresAsync();
        return Ok(ApiResponse<List<CadreDto>>.Ok(cadres, "Lấy danh sách cán bộ thành công."));
    }

    /// <summary>Chi tiết tài khoản theo Id.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(PermissionCodes.SystemUsersRead)]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken ct)
    {
        var account = await _accounts.GetAsync(id, ct);
        return Ok(ApiResponse<AccountListItemDto>.Ok(account, "Lấy thông tin tài khoản thành công."));
    }

    /// <summary>Danh mục vai trò hệ thống</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> GetSystemRoles()
    {
        var roles = await _userService.GetRolesAsync();
        return Ok(ApiResponse<List<RoleDto>>.Ok(roles, "Lấy danh mục vai trò thành công."));
    }

    /// <summary>
    /// Tạo tài khoản: trả tên đăng nhập và mật khẩu tạm (chỉ hiển thị một lần). Người dùng phải đổi mật khẩu ở lần đăng nhập đầu.
    /// Route cũ <c>POST /api/users/create</c> là bí danh tạm thời (bắt buộc có <c>username</c>).
    /// </summary>
    [HttpPost]
    [HttpPost("create")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> CreateUser([FromBody] CreateAccountRequestDto request, CancellationToken ct)
    {
        var created = await _accounts.CreateAsync(new CreateAccountCommand(
            request.Username ?? string.Empty,
            request.FullName ?? string.Empty,
            request.Email,
            request.PartyCardNumber,
            request.PositionTitle ?? request.AdminTitle,
            request.DepartmentId,
            request.PartyCellId,
            request.ApprovalAuthority ?? ApprovalAuthority.CoSo)
        {
            PhoneNumber = request.PhoneNumber
        }, ct);

        return Ok(ApiResponse<CreatedAccount>.Ok(created,
            "Đã tạo tài khoản. Hãy giao tên đăng nhập và mật khẩu tạm cho cán bộ qua kênh an toàn; mật khẩu tạm chỉ hiển thị một lần."));
    }

    /// <summary>Cập nhật thông tin tài khoản (trường không gửi giữ nguyên).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateAccountRequestDto request, CancellationToken ct)
    {
        if (request.IsActive.HasValue)
            await _accounts.SetActiveAsync(id, request.IsActive.Value, ct);

        var result = await _accounts.UpdateAsync(id, new UpdateAccountCommand(
            request.FullName,
            request.Email,
            request.PhoneNumber,
            request.PartyCardNumber,
            request.PositionTitle ?? request.AdminTitle,
            request.DepartmentId,
            request.PartyCellId,
            request.ApprovalAuthority), ct);
        return Ok(ApiResponse<AccountListItemDto>.Ok(result, "Cập nhật tài khoản thành công."));
    }

    /// <summary>Mở lại tài khoản đã bị khóa.</summary>
    [HttpPost("{id:guid}/activate")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _accounts.SetActiveAsync(id, true, ct);
        return Ok(ApiResponse.Ok("Đã mở lại tài khoản. Cán bộ có thể đăng nhập lại."));
    }

    /// <summary>Khóa tài khoản: mọi phiên đang dùng bị thu hồi ngay.</summary>
    [HttpPost("{id:guid}/deactivate")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _accounts.SetActiveAsync(id, false, ct);
        return Ok(ApiResponse.Ok("Đã khóa tài khoản và đăng xuất mọi phiên của tài khoản này."));
    }

    /// <summary>Mở khóa đăng nhập tạm thời (do nhập sai mật khẩu nhiều lần), không đổi mật khẩu.</summary>
    [HttpPost("{id:guid}/unlock")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken ct)
    {
        await _accounts.UnlockAsync(id, ct);
        return Ok(ApiResponse.Ok("Đã mở khóa đăng nhập. Cán bộ có thể đăng nhập lại bằng mật khẩu hiện tại."));
    }

    /// <summary>Đặt lại mật khẩu tạm (chỉ hiển thị một lần); mọi phiên của tài khoản bị thu hồi.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        var result = await _accounts.ResetPasswordAsync(id, ct);
        var dto = new ResetPasswordResponseDto
        {
            UserId = result.UserId,
            UserName = result.Username,
            TemporaryPassword = result.TemporaryPassword,
            MustChangePassword = true
        };
        return Ok(ApiResponse<ResetPasswordResponseDto>.Ok(dto,
            "Đã đặt lại mật khẩu tạm và đăng xuất mọi phiên. Hãy cung cấp mật khẩu này cho cán bộ qua kênh an toàn."));
    }

    /// <summary>Xóa (mềm) tài khoản; tên đăng nhập không được tái sử dụng.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(PermissionCodes.SystemUsersManage)]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken ct)
    {
        await _accounts.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok("Đã xóa tài khoản và đăng xuất mọi phiên của tài khoản này."));
    }
}
