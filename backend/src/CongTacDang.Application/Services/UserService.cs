using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện xử lý nghiệp vụ quản lý người dùng và hồ sơ cán bộ
/// </summary>
public interface IUserService
{
    /// <summary>Lấy thông tin hồ sơ và danh sách quyền của người dùng</summary>
    Task<UserProfileDto> GetProfileAsync(string? username = null);

    /// <summary>Lấy danh sách tất cả cán bộ / Đảng viên trong hệ thống</summary>
    Task<List<CadreDto>> GetCadresAsync();

    /// <summary>Lấy chi tiết thông tin một cán bộ theo Id</summary>
    Task<CadreDto?> GetUserByIdAsync(Guid id);

    /// <summary>Lấy danh mục các vai trò và quyền hạn hệ thống</summary>
    Task<List<RoleDto>> GetRolesAsync();

    /// <summary>Thêm mới cán bộ / Đảng viên</summary>
    Task<CadreDto> CreateUserAsync(CreateUserDto input);

    /// <summary>Cập nhật thông tin cán bộ / Đảng viên</summary>
    Task<CadreDto> UpdateUserAsync(Guid id, UpdateUserDto input);

    /// <summary>Xóa hồ sơ cán bộ khỏi hệ thống</summary>
    Task DeleteUserAsync(Guid id);
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;

    public UserService(IUserRepository userRepo, IRoleRepository roleRepo)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
    }

    /// <summary>Lấy hồ sơ và vai trò hệ thống của người dùng theo tên đăng nhập</summary>
    public async Task<UserProfileDto> GetProfileAsync(string? username = null)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Tên đăng nhập không được để trống.");
        }

        var member = await _userRepo.GetWithRolesAndPermissionsAsync(username);
        if (member == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ cán bộ với tên đăng nhập: {username}");
        }

        var roles = member.Roles.Select(r => r.Code).Distinct().ToList();
        var perms = member.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().ToList();

        if (!roles.Any())
        {
            roles.Add(AppRoles.CAN_BO);
            if (member.PartyRole == PartyRole.BiThuDangUy || member.PartyRole == PartyRole.PhoBiThuDangUy || member.PartyRole == PartyRole.UyVienBanThuongVu)
            {
                roles.Add(AppRoles.BAN_THUONG_VU);
            }
            if (member.PartyRole == PartyRole.BiThuChiBo || member.PartyRole == PartyRole.PhoBiThuChiBo)
            {
                roles.Add(AppRoles.BI_THU_CHI_BO);
            }
        }

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
            Roles = roles.ToArray(),
            Permissions = perms.ToArray()
        };
    }

    /// <summary>Lấy danh sách tất cả cán bộ kèm thông tin Chi bộ và Phòng ban</summary>
    public async Task<List<CadreDto>> GetCadresAsync()
    {
        var members = await _userRepo.GetAllWithDetailsAsync();
        return members.Select(m => new CadreDto
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

    /// <summary>Thêm mới hồ sơ cán bộ vào hệ thống</summary>
    public async Task<CadreDto> CreateUserAsync(CreateUserDto input)
    {
        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new ArgumentException("Họ và tên cán bộ không được để trống.");

        var username = "cb_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        var newMember = new PartyMemberProfile
        {
            Username = username,
            FullName = input.FullName.Trim(),
            PartyCardNumber = input.PartyCardNumber?.Trim(),
            PositionTitle = input.AdminTitle?.Trim() ?? "Cán bộ",
            PartyCellId = input.PartyCellId,
            DepartmentId = input.DepartmentId,
            IsPartyMember = !string.IsNullOrWhiteSpace(input.PartyCardNumber),
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456")
        };

        await _userRepo.AddAsync(newMember);

        return new CadreDto
        {
            Id = newMember.Id,
            FullName = newMember.FullName,
            PartyCardNumber = newMember.PartyCardNumber,
            PartyRole = newMember.PartyRole.ToString(),
            AdminTitle = newMember.PositionTitle,
            IsPartyMember = newMember.IsPartyMember,
            IsActive = newMember.IsActive
        };
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

    /// <summary>Cập nhật thông tin hồ sơ cán bộ</summary>
    public async Task<CadreDto> UpdateUserAsync(Guid id, UpdateUserDto input)
    {
        var member = await _userRepo.GetByIdAsync(id);
        if (member == null)
            throw new KeyNotFoundException("Không tìm thấy hồ sơ cán bộ cần cập nhật.");

        if (string.IsNullOrWhiteSpace(input.FullName))
            throw new ArgumentException("Họ và tên cán bộ không được để trống.");

        member.FullName = input.FullName.Trim();
        if (input.PartyCardNumber != null)
        {
            member.PartyCardNumber = string.IsNullOrWhiteSpace(input.PartyCardNumber) ? null : input.PartyCardNumber.Trim();
            member.IsPartyMember = !string.IsNullOrWhiteSpace(member.PartyCardNumber);
        }
        if (input.AdminTitle != null)
        {
            member.PositionTitle = input.AdminTitle.Trim();
        }
        if (input.PartyCellId.HasValue)
        {
            member.PartyCellId = input.PartyCellId.Value == Guid.Empty ? null : input.PartyCellId;
        }
        if (input.DepartmentId.HasValue)
        {
            member.DepartmentId = input.DepartmentId.Value == Guid.Empty ? null : input.DepartmentId;
        }
        if (input.IsActive.HasValue)
        {
            member.IsActive = input.IsActive.Value;
        }

        await _userRepo.UpdateAsync(member);

        return new CadreDto
        {
            Id = member.Id,
            FullName = member.FullName,
            PartyCardNumber = member.PartyCardNumber,
            PartyRole = member.PartyRole.ToString(),
            AdminTitle = member.PositionTitle,
            PartyCellName = member.PartyCell?.Name,
            DepartmentName = member.Department?.Name,
            IsPartyMember = member.IsPartyMember,
            IsActive = member.IsActive
        };
    }

    /// <summary>Xóa hồ sơ cán bộ khỏi hệ thống</summary>
    public async Task DeleteUserAsync(Guid id)
    {
        var member = await _userRepo.GetByIdAsync(id);
        if (member == null)
            throw new KeyNotFoundException("Không tìm thấy hồ sơ cán bộ cần xóa.");

        await _userRepo.DeleteAsync(member);
    }
}
