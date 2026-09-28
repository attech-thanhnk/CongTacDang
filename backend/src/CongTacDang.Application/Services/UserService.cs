using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>
/// Tra cứu hồ sơ cán bộ. Vòng đời tài khoản (tạo, sửa, khóa, xóa, đặt lại mật khẩu) và danh sách tài khoản
/// nằm ở <see cref="IUserAccountService"/>.
/// </summary>
public interface IUserService
{
    /// <summary>Lấy hồ sơ và quyền thật (từ <see cref="IPermissionResolver"/>) của người dùng theo Id.</summary>
    Task<UserProfileDto> GetProfileByIdAsync(Guid userId);

    /// <summary>
    /// Lấy hồ sơ theo yêu cầu của một người dùng: mặc định là hồ sơ của chính người yêu cầu;
    /// hồ sơ người khác chỉ trả về khi người yêu cầu có quyền <c>system.users.read</c> bao trùm Phòng/Chi bộ của hồ sơ đó.
    /// </summary>
    Task<UserProfileDto> GetProfileForRequesterAsync(Guid requesterId, string? username = null);
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IAuthorizationGuard _guard;
    private readonly IPermissionResolver _permissions;

    private static readonly string ProfileForbiddenMessage =
        $"Bạn chỉ được xem hồ sơ của chính mình hoặc hồ sơ trong phạm vi quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemUsersRead)}\" được giao. "
        + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.";

    public UserService(IUserRepository userRepo, IAuthorizationGuard guard, IPermissionResolver permissions)
    {
        _userRepo = userRepo;
        _guard = guard;
        _permissions = permissions;
    }

    /// <inheritdoc />
    public async Task<UserProfileDto> GetProfileByIdAsync(Guid userId)
    {
        var member = await _userRepo.GetWithOrganizationByIdAsync(userId)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ của tài khoản đang đăng nhập (có thể đã bị xóa). Vui lòng đăng nhập lại.");
        return await MapToProfileDtoAsync(member);
    }

    /// <summary>Lấy hồ sơ của chính người yêu cầu, hoặc hồ sơ người khác nếu được phép xem.</summary>
    public async Task<UserProfileDto> GetProfileForRequesterAsync(Guid requesterId, string? username = null)
    {
        var requester = await _userRepo.GetWithOrganizationByIdAsync(requesterId)
            ?? throw new ForbiddenException("Không tìm thấy hồ sơ người dùng hiện tại.");

        var targetUsername = username?.Trim();
        if (string.IsNullOrEmpty(targetUsername)
            || string.Equals(targetUsername, requester.Username, StringComparison.OrdinalIgnoreCase))
        {
            return await MapToProfileDtoAsync(requester);
        }

        var target = await _userRepo.GetByUsernameAsync(targetUsername);
        if (target == null)
        {
            // Không tiết lộ tài khoản có tồn tại hay không: chỉ người xem được mọi hồ sơ (phạm vi Toàn công ty) nhận 404.
            if (!_guard.Can(PermissionCodes.SystemUsersRead, AccessTarget.None))
                throw new ForbiddenException(ProfileForbiddenMessage);
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ cán bộ với tên đăng nhập: {targetUsername}");
        }

        if (!_guard.Can(PermissionCodes.SystemUsersRead, new AccessTarget(target.Id, target.DepartmentId, target.PartyCellId)))
            throw new ForbiddenException(ProfileForbiddenMessage);

        return await MapToProfileDtoAsync(target);
    }

    /// <summary>
    /// Ánh xạ hồ sơ sang DTO; tên vai trò/mã quyền lấy từ <see cref="IPermissionResolver"/> (bản gán đang hiệu lực —
    /// người chưa được gán vai trò thì không có vai trò/quyền nào).
    /// </summary>
    private async Task<UserProfileDto> MapToProfileDtoAsync(PartyMemberProfile member)
    {
        var effective = await _permissions.GetAsync(member.Id);

        return new UserProfileDto
        {
            Id = member.Id,
            FullName = member.FullName,
            UserName = member.Username,
            PartyRole = member.PartyRole.ToString(),
            AdminTitle = member.PositionTitle,
            PartyBranchName = member.PartyCell?.Name ?? string.Empty,
            AdminDeptName = member.Department?.Name ?? string.Empty,
            JobGroup = member.JobGroup.ToString(),
            Roles = effective.RoleNames.ToArray(),
            Permissions = effective.Codes.ToArray(),
            MustChangePassword = member.MustChangePassword
        };
    }
}
