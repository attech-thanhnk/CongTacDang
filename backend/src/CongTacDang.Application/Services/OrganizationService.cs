using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện xử lý nghiệp vụ quản lý tổ chức Chi bộ và Phòng ban
/// </summary>
public interface IOrganizationService
{
    /// <summary>Lấy danh sách tất cả Chi bộ trong hệ thống</summary>
    Task<List<BranchDto>> GetBranchesAsync();

    /// <summary>Lấy chi tiết thông tin Chi bộ theo Id</summary>
    Task<BranchDto?> GetBranchByIdAsync(Guid id);

    /// <summary>Thêm mới Chi bộ</summary>
    Task<BranchDto> CreateBranchAsync(CreateBranchDto input);

    /// <summary>Cập nhật thông tin Chi bộ</summary>
    Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchDto input);

    /// <summary>Xóa Chi bộ (yêu cầu không có Đảng viên sinh hoạt)</summary>
    Task DeleteBranchAsync(Guid id);

    /// <summary>Lấy danh sách Phòng ban chuyên môn</summary>
    Task<List<DepartmentDto>> GetDepartmentsAsync();
}

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _orgRepo;

    public OrganizationService(IOrganizationRepository orgRepo)
    {
        _orgRepo = orgRepo;
    }

    /// <summary>Lấy danh sách tất cả Chi bộ kèm số lượng Đảng viên</summary>
    public async Task<List<BranchDto>> GetBranchesAsync()
    {
        var cells = await _orgRepo.GetPartyCellsWithMembersAsync();
        return cells.Select(c => new BranchDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            Description = c.Description,
            MemberCount = c.Members.Count
        }).ToList();
    }

    /// <summary>Lấy chi tiết Chi bộ theo Id kèm danh sách Đảng viên</summary>
    public async Task<BranchDto?> GetBranchByIdAsync(Guid id)
    {
        var c = await _orgRepo.GetPartyCellByIdAsync(id);
        if (c == null) return null;

        return new BranchDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            Description = c.Description,
            MemberCount = c.Members.Count
        };
    }

    /// <summary>Tạo mới Chi bộ, tự động sinh mã nếu không có</summary>
    public async Task<BranchDto> CreateBranchAsync(CreateBranchDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Tên Chi bộ không được để trống.");

        var cell = new PartyCell
        {
            Code = string.IsNullOrWhiteSpace(input.Code) ? $"CB-{Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper()}" : input.Code.Trim().ToUpper(),
            Name = input.Name.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            IsActive = true
        };

        await _orgRepo.AddPartyCellAsync(cell);

        return new BranchDto
        {
            Id = cell.Id,
            Code = cell.Code,
            Name = cell.Name,
            Description = cell.Description,
            MemberCount = 0
        };
    }

    /// <summary>Cập nhật tên và mô tả Chi bộ</summary>
    public async Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchDto input)
    {
        var cell = await _orgRepo.GetPartyCellByIdAsync(id);
        if (cell == null)
            throw new KeyNotFoundException("Không tìm thấy Chi bộ cần cập nhật.");

        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("Tên Chi bộ không được để trống.");

        cell.Name = input.Name.Trim();
        cell.Description = input.Description?.Trim() ?? string.Empty;

        await _orgRepo.UpdatePartyCellAsync(cell);

        return new BranchDto
        {
            Id = cell.Id,
            Code = cell.Code,
            Name = cell.Name,
            Description = cell.Description,
            MemberCount = cell.Members.Count
        };
    }

    /// <summary>Xóa Chi bộ (chặn nếu vẫn còn Đảng viên sinh hoạt)</summary>
    public async Task DeleteBranchAsync(Guid id)
    {
        var cell = await _orgRepo.GetPartyCellByIdAsync(id);
        if (cell == null)
            throw new KeyNotFoundException("Không tìm thấy Chi bộ cần xóa.");

        if (cell.Members.Count > 0)
            throw new InvalidOperationException($"Không thể xóa Chi bộ vì đang có {cell.Members.Count} cán bộ sinh hoạt.");

        await _orgRepo.DeletePartyCellAsync(cell);
    }

    /// <summary>Lấy danh sách Phòng ban chuyên môn kèm số lượng cán bộ</summary>
    public async Task<List<DepartmentDto>> GetDepartmentsAsync()
    {
        var deps = await _orgRepo.GetDepartmentsWithMembersAsync();
        return deps.Select(d => new DepartmentDto
        {
            Id = d.Id,
            Code = d.Code,
            Name = d.Name,
            Description = d.Description,
            MemberCount = d.Members.Count
        }).ToList();
    }
}
