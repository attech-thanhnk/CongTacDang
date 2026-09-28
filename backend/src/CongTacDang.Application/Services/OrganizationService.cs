using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện xử lý nghiệp vụ danh mục tổ chức: Chi bộ và Phòng/đơn vị chuyên môn.
/// Lỗi nghiệp vụ ném <see cref="ValidationException"/> (400), <see cref="NotFoundException"/> (404),
/// <see cref="ConflictException"/> (409) — middleware chuyển thành <c>ApiResponse</c>.
/// </summary>
public interface IOrganizationService
{
    /// <summary>Lấy danh sách tất cả Chi bộ trong hệ thống</summary>
    Task<List<BranchDto>> GetBranchesAsync();

    /// <summary>Lấy chi tiết thông tin Chi bộ theo Id</summary>
    Task<BranchDto?> GetBranchByIdAsync(Guid id);

    /// <summary>Thêm mới Chi bộ</summary>
    Task<BranchDto> CreateBranchAsync(SaveCatalogItemDto input);

    /// <summary>Cập nhật thông tin Chi bộ (mã, tên, mô tả, thứ tự, trạng thái hoạt động)</summary>
    Task<BranchDto> UpdateBranchAsync(Guid id, SaveCatalogItemDto input);

    /// <summary>Xóa Chi bộ (chặn khi còn cán bộ hoặc hồ sơ đánh giá)</summary>
    Task DeleteBranchAsync(Guid id);

    /// <summary>Lấy danh sách Phòng/đơn vị chuyên môn</summary>
    Task<List<DepartmentDto>> GetDepartmentsAsync();

    /// <summary>Lấy chi tiết Phòng/đơn vị theo Id</summary>
    Task<DepartmentDto?> GetDepartmentByIdAsync(Guid id);

    /// <summary>Thêm mới Phòng/đơn vị</summary>
    Task<DepartmentDto> CreateDepartmentAsync(SaveCatalogItemDto input);

    /// <summary>Cập nhật Phòng/đơn vị (mã, tên, mô tả, thứ tự, trạng thái hoạt động)</summary>
    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveCatalogItemDto input);

    /// <summary>Xóa Phòng/đơn vị (chặn khi còn cán bộ hoặc hồ sơ đánh giá)</summary>
    Task DeleteDepartmentAsync(Guid id);
}

/// <summary>Quy tắc chung của mã/tên danh mục — dùng cho màn hình quản lý và chức năng nhập dữ liệu.</summary>
public static class CatalogRules
{
    /// <summary>Độ dài tối đa của mã (khớp cấu hình cột).</summary>
    public const int MaxCodeLength = 50;

    /// <summary>Độ dài tối đa của tên (khớp cấu hình cột).</summary>
    public const int MaxNameLength = 200;

    /// <summary>Chuẩn hóa mã: cắt khoảng trắng hai đầu, chữ hoa.</summary>
    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Kiểm tra mã đã chuẩn hóa; trả thông báo lỗi hoặc null nếu hợp lệ.</summary>
    public static string? ValidateCode(string code, string label)
    {
        if (code.Length == 0)
            return $"Mã {label} không được để trống. Hãy nhập mã {label}.";
        if (code.Length > MaxCodeLength)
            return $"Mã {label} dài quá {MaxCodeLength} ký tự. Hãy rút ngắn mã.";
        if (code.Any(char.IsWhiteSpace))
            return $"Mã {label} không được chứa khoảng trắng. Hãy dùng dấu gạch ngang thay khoảng trắng (ví dụ PH-KH).";
        return null;
    }

    /// <summary>Kiểm tra tên đã cắt khoảng trắng; trả thông báo lỗi hoặc null nếu hợp lệ.</summary>
    public static string? ValidateName(string name, string label)
    {
        if (name.Length == 0)
            return $"Tên {label} không được để trống. Hãy nhập tên {label}.";
        if (name.Length > MaxNameLength)
            return $"Tên {label} dài quá {MaxNameLength} ký tự. Hãy rút ngắn tên.";
        return null;
    }
}

public class OrganizationService : IOrganizationService
{
    private const string DepartmentLabel = "Phòng";
    private const string BranchLabel = "Chi bộ";

    private readonly IOrganizationRepository _orgRepo;
    private readonly IUnitOfWork _unitOfWork;

    public OrganizationService(IOrganizationRepository orgRepo, IUnitOfWork unitOfWork)
    {
        _orgRepo = orgRepo;
        _unitOfWork = unitOfWork;
    }

    // ===================== Chi bộ =====================

    /// <summary>Lấy danh sách Chi bộ kèm số lượng cán bộ sinh hoạt</summary>
    public async Task<List<BranchDto>> GetBranchesAsync()
    {
        var cells = await _orgRepo.ListPartyCellsAsync();
        var counts = await _orgRepo.CountMembersByPartyCellAsync();
        return cells.Select(c => ToDto(c, counts.GetValueOrDefault(c.Id))).ToList();
    }

    /// <summary>Lấy chi tiết Chi bộ theo Id</summary>
    public async Task<BranchDto?> GetBranchByIdAsync(Guid id)
    {
        var cell = await _orgRepo.FindPartyCellAsync(id);
        if (cell == null) return null;
        var usage = await _orgRepo.GetPartyCellUsageAsync(id);
        return ToDto(cell, usage.Members);
    }

    /// <summary>Tạo mới Chi bộ; để trống mã → tự sinh mã CB-xxxx</summary>
    public async Task<BranchDto> CreateBranchAsync(SaveCatalogItemDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var code = string.IsNullOrWhiteSpace(input.Code)
            ? $"CB-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}"
            : CatalogRules.NormalizeCode(input.Code);
        var name = input.Name?.Trim() ?? string.Empty;
        EnsureValid(CatalogRules.ValidateCode(code, BranchLabel), CatalogRules.ValidateName(name, BranchLabel));

        if (await _orgRepo.PartyCellCodeExistsAsync(code))
            throw DuplicateCode(BranchLabel, code);

        var cell = new PartyCell
        {
            Code = code,
            Name = name,
            Description = input.Description?.Trim() ?? string.Empty,
            SortOrder = input.SortOrder ?? 0,
            IsActive = input.IsActive ?? true
        };
        _orgRepo.AddPartyCell(cell);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(cell, 0);
    }

    /// <summary>Cập nhật Chi bộ; trường null được giữ nguyên</summary>
    public async Task<BranchDto> UpdateBranchAsync(Guid id, SaveCatalogItemDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var cell = await _orgRepo.FindPartyCellAsync(id)
            ?? throw new NotFoundException("Không tìm thấy Chi bộ cần cập nhật. Có thể Chi bộ đã bị xóa; hãy tải lại danh sách.");

        var code = input.Code == null ? cell.Code : CatalogRules.NormalizeCode(input.Code);
        var name = input.Name == null ? cell.Name : input.Name.Trim();
        EnsureValid(CatalogRules.ValidateCode(code, BranchLabel), CatalogRules.ValidateName(name, BranchLabel));

        if (!string.Equals(code, cell.Code, StringComparison.OrdinalIgnoreCase)
            && await _orgRepo.PartyCellCodeExistsAsync(code, cell.Id))
            throw DuplicateCode(BranchLabel, code);

        cell.Code = code;
        cell.Name = name;
        if (input.Description != null) cell.Description = input.Description.Trim();
        if (input.SortOrder.HasValue) cell.SortOrder = input.SortOrder.Value;
        if (input.IsActive.HasValue) cell.IsActive = input.IsActive.Value;
        await _unitOfWork.SaveChangesAsync();

        var usage = await _orgRepo.GetPartyCellUsageAsync(id);
        return ToDto(cell, usage.Members);
    }

    /// <summary>Xóa (mềm) Chi bộ; chặn khi còn cán bộ sinh hoạt hoặc còn hồ sơ đánh giá</summary>
    public async Task DeleteBranchAsync(Guid id)
    {
        var cell = await _orgRepo.FindPartyCellAsync(id)
            ?? throw new NotFoundException("Không tìm thấy Chi bộ cần xóa. Có thể Chi bộ đã bị xóa; hãy tải lại danh sách.");

        var usage = await _orgRepo.GetPartyCellUsageAsync(id);
        if (!usage.IsUnused)
            throw new ConflictException(BuildInUseMessage(BranchLabel, cell.Name, usage, "Chi bộ khác"));

        _orgRepo.RemovePartyCell(cell);
        await _unitOfWork.SaveChangesAsync();
    }

    // ===================== Phòng / đơn vị =====================

    /// <summary>Lấy danh sách Phòng/đơn vị kèm số lượng cán bộ</summary>
    public async Task<List<DepartmentDto>> GetDepartmentsAsync()
    {
        var departments = await _orgRepo.ListDepartmentsAsync();
        var counts = await _orgRepo.CountMembersByDepartmentAsync();
        return departments.Select(d => ToDto(d, counts.GetValueOrDefault(d.Id))).ToList();
    }

    /// <summary>Lấy chi tiết Phòng/đơn vị theo Id</summary>
    public async Task<DepartmentDto?> GetDepartmentByIdAsync(Guid id)
    {
        var department = await _orgRepo.FindDepartmentAsync(id);
        if (department == null) return null;
        var usage = await _orgRepo.GetDepartmentUsageAsync(id);
        return ToDto(department, usage.Members);
    }

    /// <summary>Tạo mới Phòng/đơn vị; mã bắt buộc và duy nhất</summary>
    public async Task<DepartmentDto> CreateDepartmentAsync(SaveCatalogItemDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var code = CatalogRules.NormalizeCode(input.Code);
        var name = input.Name?.Trim() ?? string.Empty;
        EnsureValid(CatalogRules.ValidateCode(code, DepartmentLabel), CatalogRules.ValidateName(name, DepartmentLabel));

        if (await _orgRepo.DepartmentCodeExistsAsync(code))
            throw DuplicateCode(DepartmentLabel, code);

        var department = new AdministrativeDepartment
        {
            Code = code,
            Name = name,
            Description = input.Description?.Trim() ?? string.Empty,
            SortOrder = input.SortOrder ?? 0,
            IsActive = input.IsActive ?? true
        };
        _orgRepo.AddDepartment(department);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(department, 0);
    }

    /// <summary>Cập nhật Phòng/đơn vị; trường null được giữ nguyên</summary>
    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveCatalogItemDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var department = await _orgRepo.FindDepartmentAsync(id)
            ?? throw new NotFoundException("Không tìm thấy Phòng/đơn vị cần cập nhật. Có thể đơn vị đã bị xóa; hãy tải lại danh sách.");

        var code = input.Code == null ? department.Code : CatalogRules.NormalizeCode(input.Code);
        var name = input.Name == null ? department.Name : input.Name.Trim();
        EnsureValid(CatalogRules.ValidateCode(code, DepartmentLabel), CatalogRules.ValidateName(name, DepartmentLabel));

        if (!string.Equals(code, department.Code, StringComparison.OrdinalIgnoreCase)
            && await _orgRepo.DepartmentCodeExistsAsync(code, department.Id))
            throw DuplicateCode(DepartmentLabel, code);

        department.Code = code;
        department.Name = name;
        if (input.Description != null) department.Description = input.Description.Trim();
        if (input.SortOrder.HasValue) department.SortOrder = input.SortOrder.Value;
        if (input.IsActive.HasValue) department.IsActive = input.IsActive.Value;
        await _unitOfWork.SaveChangesAsync();

        var usage = await _orgRepo.GetDepartmentUsageAsync(id);
        return ToDto(department, usage.Members);
    }

    /// <summary>Xóa (mềm) Phòng/đơn vị; chặn khi còn cán bộ hoặc còn hồ sơ đánh giá</summary>
    public async Task DeleteDepartmentAsync(Guid id)
    {
        var department = await _orgRepo.FindDepartmentAsync(id)
            ?? throw new NotFoundException("Không tìm thấy Phòng/đơn vị cần xóa. Có thể đơn vị đã bị xóa; hãy tải lại danh sách.");

        var usage = await _orgRepo.GetDepartmentUsageAsync(id);
        if (!usage.IsUnused)
            throw new ConflictException(BuildInUseMessage(DepartmentLabel, department.Name, usage, "Phòng khác"));

        _orgRepo.RemoveDepartment(department);
        await _unitOfWork.SaveChangesAsync();
    }

    // ===================== Hỗ trợ =====================

    private static void EnsureValid(params string?[] errors)
    {
        var first = errors.FirstOrDefault(e => e != null);
        if (first != null)
            throw new ValidationException(first);
    }

    private static ConflictException DuplicateCode(string label, string code)
        => new($"Mã {label} \"{code}\" đã được dùng (kể cả cho {label} đã xóa). Hãy chọn mã khác.");

    /// <summary>Thông báo 409 nêu số lượng dữ liệu còn tham chiếu và cách xử lý.</summary>
    public static string BuildInUseMessage(string label, string name, CatalogUsage usage, string moveTarget)
    {
        var parts = new List<string>();
        if (usage.Members > 0) parts.Add($"{usage.Members} cán bộ");
        if (usage.Records > 0) parts.Add($"{usage.Records} hồ sơ đánh giá");
        var reason = string.Join(" và ", parts);

        var advice = usage.Members > 0
            ? $"Hãy chuyển cán bộ sang {moveTarget} trước."
            : string.Empty;
        if (usage.Records > 0)
            advice = (advice + $" Hồ sơ đánh giá phải được giữ lại, vì vậy hãy chuyển {label} sang \"Ngừng hoạt động\" thay vì xóa.").Trim();

        return $"Không thể xóa {label} \"{name}\" vì còn {reason}. {advice}";
    }

    private static BranchDto ToDto(PartyCell c, int memberCount) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Description = c.Description,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive,
        MemberCount = memberCount
    };

    private static DepartmentDto ToDto(AdministrativeDepartment d, int memberCount) => new()
    {
        Id = d.Id,
        Code = d.Code,
        Name = d.Name,
        Description = d.Description,
        SortOrder = d.SortOrder,
        IsActive = d.IsActive,
        MemberCount = memberCount
    };
}
