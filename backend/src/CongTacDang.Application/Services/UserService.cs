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
/// Giao diện tra cứu hồ sơ cán bộ. Vòng đời tài khoản (tạo, sửa, khóa, xóa, đặt lại mật khẩu)
/// nằm ở <see cref="IUserAccountService"/>.
/// </summary>
public interface IUserService
{
    /// <summary>Lấy thông tin hồ sơ và danh sách quyền của người dùng theo tên đăng nhập</summary>
    Task<UserProfileDto> GetProfileAsync(string? username = null);

    /// <summary>Lấy hồ sơ và quyền thật (từ <see cref="IPermissionResolver"/>) của người dùng theo Id.</summary>
    Task<UserProfileDto> GetProfileByIdAsync(Guid userId);

    /// <summary>
    /// Lấy hồ sơ theo yêu cầu của một người dùng: mặc định là hồ sơ của chính người yêu cầu;
    /// hồ sơ người khác chỉ trả về khi người yêu cầu có quyền <c>system.users.read</c> bao trùm Phòng/Chi bộ của hồ sơ đó.
    /// </summary>
    Task<UserProfileDto> GetProfileForRequesterAsync(Guid requesterId, string? username = null);

    /// <summary>Danh sách cán bộ / Đảng viên trong phạm vi <c>system.users.read</c> của người dùng hiện tại.</summary>
    Task<List<CadreDto>> GetCadresAsync();

    /// <summary>Lấy chi tiết thông tin một cán bộ theo Id</summary>
    Task<CadreDto?> GetUserByIdAsync(Guid id);

    /// <summary>Lấy danh mục các vai trò và quyền hạn hệ thống</summary>
    Task<List<RoleDto>> GetRolesAsync();
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;
    private readonly IAuthorizationGuard _guard;
    private readonly IPermissionResolver _permissions;

    private static readonly string ProfileForbiddenMessage =
        $"Bạn chỉ được xem hồ sơ của chính mình hoặc hồ sơ trong phạm vi quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemUsersRead)}\" được giao. "
        + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.";

    public UserService(IUserRepository userRepo, IRoleRepository roleRepo, IAuthorizationGuard guard, IPermissionResolver permissions)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _guard = guard;
        _permissions = permissions;
    }

    /// <summary>Lấy hồ sơ và quyền của người dùng theo tên đăng nhập</summary>
    public async Task<UserProfileDto> GetProfileAsync(string? username = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Tên đăng nhập không được để trống.");
        }

        var member = await _userRepo.GetByUsernameAsync(username.Trim());
        if (member == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ cán bộ với tên đăng nhập: {username}");
        }

        return await MapToProfileDtoAsync(member);
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
    /// Ánh xạ hồ sơ sang DTO; vai trò/quyền lấy từ <see cref="IPermissionResolver"/> (T-60: không suy vai trò từ
    /// chức vụ Đảng — người chưa được gán vai trò thì không có vai trò/quyền nào).
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

    /// <summary>
    /// Danh sách cán bộ kèm Chi bộ, Phòng — chỉ cán bộ thuộc Phòng/Chi bộ trong phạm vi <c>system.users.read</c>
    /// của người dùng hiện tại (T-61); không có phạm vi nào → danh sách rỗng.
    /// </summary>
    public async Task<List<CadreDto>> GetCadresAsync()
    {
        var scope = _guard.GetScope(PermissionCodes.SystemUsersRead);
        if (scope.IsEmpty)
            return new List<CadreDto>();

        var members = await _userRepo.GetAllWithDetailsAsync();
        return members.Where(m => scope.Matches(null, m.DepartmentId, m.PartyCellId)).Select(m => new CadreDto
        {
            Id = m.Id,
            FullName = m.FullName,
            PartyCardNumber = m.PartyCardNumber,
            PartyRole = m.PartyRole.ToString(),
            AdminTitle = m.PositionTitle,
            PartyCellName = m.PartyCell?.Name,
            DepartmentName = m.Department?.Name,
            IsPartyMember = m.IsPartyMember,
            IsActive = m.IsActive
        }).ToList();
    }

    /// <summary>Lấy danh mục vai trò và quyền hạn thực tế từ CSDL (Dynamic RBAC)</summary>
    public async Task<List<RoleDto>> GetRolesAsync()
    {
        var roles = await _roleRepo.GetAllRolesWithPermissionsAsync();
        return roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Code = r.Code,
            Name = r.Name,
            Description = r.Description,
            IsSystem = r.IsSystem,
            Permissions = r.Permissions.Select(p => new PermissionDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                Resource = p.Resource,
                Action = p.Action,
                Description = p.Description
            }).ToList()
        }).ToList();
    }

    /// <summary>Lấy chi tiết thông tin một cán bộ theo Id</summary>
    public async Task<CadreDto?> GetUserByIdAsync(Guid id)
    {
        var m = await _userRepo.GetByIdAsync(id);
        if (m == null) return null;

        return new CadreDto
        {
            Id = m.Id,
            FullName = m.FullName,
            PartyCardNumber = m.PartyCardNumber,
            PartyRole = m.PartyRole.ToString(),
            AdminTitle = m.PositionTitle,
            PartyCellName = m.PartyCell?.Name,
            DepartmentName = m.Department?.Name,
            IsPartyMember = m.IsPartyMember,
            IsActive = m.IsActive
        };
    }
}
