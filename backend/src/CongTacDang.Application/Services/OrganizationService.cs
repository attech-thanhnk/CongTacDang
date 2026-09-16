using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

public interface IOrganizationService
{
    Task<List<BranchDto>> GetBranchesAsync();
    Task<BranchDto?> GetBranchByIdAsync(Guid id);
    Task<BranchDto> CreateBranchAsync(CreateBranchDto input);
    Task<BranchDto> UpdateBranchAsync(Guid id, UpdateBranchDto input);
    Task DeleteBranchAsync(Guid id);
    Task<List<DepartmentDto>> GetDepartmentsAsync();
}

public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _orgRepo;

    public OrganizationService(IOrganizationRepository orgRepo)
    {
        _orgRepo = orgRepo;
    }

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

    public async Task DeleteBranchAsync(Guid id)
    {
        var cell = await _orgRepo.GetPartyCellByIdAsync(id);
        if (cell == null)
            throw new KeyNotFoundException("Không tìm thấy Chi bộ cần xóa.");

        if (cell.Members.Count > 0)
            throw new InvalidOperationException($"Không thể xóa Chi bộ vì đang có {cell.Members.Count} cán bộ sinh hoạt.");

        await _orgRepo.DeletePartyCellAsync(cell);
    }

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
