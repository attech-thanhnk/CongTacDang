using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

public interface IUserService
{
    Task<UserProfileDto> GetProfileAsync(string? username = null);
    Task<List<CadreDto>> GetCadresAsync();
    Task<CadreDto?> GetUserByIdAsync(Guid id);
    Task<List<RoleDto>> GetRolesAsync();
    Task<CadreDto> CreateUserAsync(CreateUserDto input);
    Task<CadreDto> UpdateUserAsync(Guid id, UpdateUserDto input);
    Task DeleteUserAsync(Guid id);
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepo;

    public UserService(IUserRepository userRepo)
    {
        _userRepo = userRepo;
    }

    public async Task<UserProfileDto> GetProfileAsync(string? username = null)
    {
        PartyMemberProfile? member = null;
        if (!string.IsNullOrWhiteSpace(username))
        {
            member = await _userRepo.GetByUsernameAsync(username);
        }

        if (member == null)
        {
            member = await _userRepo.GetFirstMemberAsync();
        }

        if (member == null)
        {
            throw new KeyNotFoundException("Chưa có dữ liệu cán bộ trong hệ thống.");
        }

        var roles = new List<string> { "CAN_BO" };
        if (member.PartyRole == PartyRole.BiThuDangUy || member.PartyRole == PartyRole.PhoBiThuDangUy || member.PartyRole == PartyRole.UyVienBanThuongVu)
        {
            roles.Add("BAN_THUONG_VU");
            roles.Add("QUAN_TRI_HE_THONG");
        }
        if (member.PartyRole == PartyRole.BiThuChiBo || member.PartyRole == PartyRole.PhoBiThuChiBo)
        {
            roles.Add("BI_THU_CHI_BO");
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
            Roles = roles.ToArray()
        };
    }

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

    public Task<List<RoleDto>> GetRolesAsync()
    {
        var list = new List<RoleDto>
        {
            new RoleDto { Code = "QUAN_TRI_HE_THONG", Name = "Quản trị viên Hệ thống", Description = "Toàn quyền quản trị tham số, tài khoản, cơ cấu tổ chức Đảng và phân quyền." },
            new RoleDto { Code = "BAN_THUONG_VU", Name = "Ban Thường vụ Đảng ủy", Description = "Phê duyệt kết quả đánh giá, quyết định xếp loại hoàn thành xuất sắc và áp trần 20%." },
            new RoleDto { Code = "TO_THAM_DINH", Name = "Tổ Thẩm định Đảng ủy", Description = "Thẩm định hồ sơ minh chứng, rà soát kết quả tự chấm điểm của cán bộ." },
            new RoleDto { Code = "BI_THU_CHI_BO", Name = "Bí thư / Cấp ủy Chi bộ", Description = "Nhận xét cấp ủy (Mẫu 10), tổ chức họp chi bộ và đề xuất xếp loại." },
            new RoleDto { Code = "CAN_BO", Name = "Cán bộ Lãnh đạo, Quản lý", Description = "Đăng ký nhiệm vụ (Mẫu 01), tự chấm điểm (Mẫu 02/09A) và đính kèm minh chứng." }
        };
        return Task.FromResult(list);
    }

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
            IsActive = true
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

    public async Task DeleteUserAsync(Guid id)
    {
        var member = await _userRepo.GetByIdAsync(id);
        if (member == null)
            throw new KeyNotFoundException("Không tìm thấy hồ sơ cán bộ cần xóa.");

        await _userRepo.DeleteAsync(member);
    }
}
