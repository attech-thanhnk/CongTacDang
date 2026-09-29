using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Giao diện xử lý nghiệp vụ mô hình tổ chức: cây tổ chức Đảng, cây đơn vị chính quyền và danh mục loại đơn vị.
/// Lỗi nghiệp vụ ném <see cref="ValidationException"/> (400), <see cref="NotFoundException"/> (404),
/// <see cref="ConflictException"/> (409) — middleware chuyển thành <c>ApiResponse</c>.
/// </summary>
public interface IOrganizationService
{
    /// <summary>Danh sách tổ chức Đảng (phẳng, sắp theo cây: cha trước, con sau).</summary>
    Task<List<BranchDto>> GetBranchesAsync();

    /// <summary>Chi tiết tổ chức Đảng theo Id.</summary>
    Task<BranchDto?> GetBranchByIdAsync(Guid id);

    /// <summary>Thêm tổ chức Đảng (có thể chọn tổ chức cha, loại đơn vị).</summary>
    Task<BranchDto> CreateBranchAsync(SaveCatalogItemDto input);

    /// <summary>Cập nhật tổ chức Đảng (kể cả đổi cha — chặn tạo vòng).</summary>
    Task<BranchDto> UpdateBranchAsync(Guid id, SaveCatalogItemDto input);

    /// <summary>Xóa tổ chức Đảng (chặn khi còn tổ chức con, cán bộ, hồ sơ, bản gán, chức vụ đang hiệu lực).</summary>
    Task DeleteBranchAsync(Guid id);

    /// <summary>Danh sách đơn vị chính quyền (phẳng, sắp theo cây).</summary>
    Task<List<DepartmentDto>> GetDepartmentsAsync();

    /// <summary>Chi tiết đơn vị chính quyền theo Id.</summary>
    Task<DepartmentDto?> GetDepartmentByIdAsync(Guid id);

    /// <summary>Thêm đơn vị chính quyền.</summary>
    Task<DepartmentDto> CreateDepartmentAsync(SaveCatalogItemDto input);

    /// <summary>Cập nhật đơn vị chính quyền (kể cả đổi cha — chặn tạo vòng).</summary>
    Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveCatalogItemDto input);

    /// <summary>Xóa đơn vị chính quyền (chặn khi còn đơn vị con, cán bộ, hồ sơ, bản gán, chức vụ đang hiệu lực).</summary>
    Task DeleteDepartmentAsync(Guid id);

    /// <summary>Danh mục loại đơn vị (cả hai bên).</summary>
    Task<List<OrgUnitTypeDto>> GetUnitTypesAsync();

    /// <summary>Thêm loại đơn vị.</summary>
    Task<OrgUnitTypeDto> CreateUnitTypeAsync(SaveOrgUnitTypeDto input);

    /// <summary>Sửa loại đơn vị.</summary>
    Task<OrgUnitTypeDto> UpdateUnitTypeAsync(Guid id, SaveOrgUnitTypeDto input);

    /// <summary>Xóa loại đơn vị (409 khi còn đơn vị dùng).</summary>
    Task DeleteUnitTypeAsync(Guid id);
}

/// <summary>Quy tắc chung của mã/tên danh mục — dùng cho màn hình quản lý và chức năng nhập dữ liệu.</summary>
public static class CatalogRules
{
    /// <summary>Độ dài tối đa của mã (khớp cấu hình cột).</summary>
    public const int MaxCodeLength = 50;

    /// <summary>Độ dài tối đa của tên (khớp cấu hình cột).</summary>
    public const int MaxNameLength = 200;

    /// <summary>Độ dài tối đa tên loại đơn vị.</summary>
    public const int MaxUnitTypeNameLength = 100;

    /// <summary>Nhãn hiển thị bên Đảng.</summary>
    public const string PartyLabel = "tổ chức Đảng";

    /// <summary>Nhãn hiển thị bên chính quyền.</summary>
    public const string AdministrativeLabel = "đơn vị";

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

    /// <summary>Tên hiển thị của bên.</summary>
    public static string SideName(OrgSide side) => side == OrgSide.Party ? "Tổ chức Đảng" : "Đơn vị chính quyền";

    /// <summary>Đọc bên từ chuỗi (<c>Party</c>/<c>Administrative</c>, không phân biệt hoa thường).</summary>
    public static bool TryParseSide(string? value, out OrgSide side)
        => Enum.TryParse(value?.Trim(), ignoreCase: true, out side) && Enum.IsDefined(side);
}

/// <summary>
/// Nghiệp vụ mô hình tổ chức (task 14): mỗi bên là một cây (<c>ParentId</c> + đường dẫn vật hóa <c>Path</c>).
/// Mỗi lần đổi cấu trúc cây, đường dẫn của toàn bộ bên đó được tính lại (<see cref="OrgTree.BuildPaths"/>) và cache quyền
/// được xóa (phạm vi gán vai trò bao trùm cây con được tính theo đường dẫn).
/// </summary>
public class OrganizationService : IOrganizationService
{
    private const string DepartmentLabel = CatalogRules.AdministrativeLabel;
    private const string BranchLabel = CatalogRules.PartyLabel;

    private readonly IOrganizationRepository _orgRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAccessCacheInvalidator _accessCache;
    private readonly TimeProvider _time;

    public OrganizationService(IOrganizationRepository orgRepo, IUnitOfWork unitOfWork, IAccessCacheInvalidator accessCache,
        TimeProvider? time = null)
    {
        _orgRepo = orgRepo;
        _unitOfWork = unitOfWork;
        _accessCache = accessCache;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // ===================== Tổ chức Đảng =====================

    /// <inheritdoc />
    public async Task<List<BranchDto>> GetBranchesAsync()
    {
        var cells = await _orgRepo.ListPartyCellsAsync();
        var members = await _orgRepo.CountMembersByPartyCellAsync();
        var children = await _orgRepo.CountChildPartyCellsAsync();
        return OrderAsTree(cells).Select(c => Fill(new BranchDto(), c, c.UnitType, cells, members, children)).ToList();
    }

    /// <inheritdoc />
    public async Task<BranchDto?> GetBranchByIdAsync(Guid id)
        => (await GetBranchesAsync()).FirstOrDefault(c => c.Id == id);

    /// <inheritdoc />
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

        var all = (await _orgRepo.ListPartyCellsIncludingDeletedAsync()).Where(c => !c.IsDeleted).ToList();
        var cell = new PartyCell
        {
            Code = code,
            Name = name,
            Description = input.Description?.Trim() ?? string.Empty,
            SortOrder = input.SortOrder ?? 0,
            IsActive = input.IsActive ?? true,
            ParentId = ResolveNewParent(input.ParentId, all, BranchLabel),
            UnitTypeId = await ResolveUnitTypeAsync(input.UnitTypeId, null, OrgSide.Party)
        };
        _orgRepo.AddPartyCell(cell);
        all.Add(cell);
        RecomputePaths(all, BranchLabel);
        await _unitOfWork.SaveChangesAsync();
        _accessCache.InvalidateAll();
        return (await GetBranchByIdAsync(cell.Id))!;
    }

    /// <inheritdoc />
    public async Task<BranchDto> UpdateBranchAsync(Guid id, SaveCatalogItemDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var all = (await _orgRepo.ListPartyCellsIncludingDeletedAsync()).Where(c => !c.IsDeleted).ToList();
        var cell = all.FirstOrDefault(c => c.Id == id)
            ?? throw new NotFoundException("Không tìm thấy tổ chức Đảng cần cập nhật. Có thể tổ chức đã bị xóa; hãy tải lại danh sách.");

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
        cell.UnitTypeId = await ResolveUnitTypeAsync(input.UnitTypeId, cell.UnitTypeId, OrgSide.Party);

        var structureChanged = ApplyParentChange(cell, input.ParentId, all, BranchLabel);
        await _unitOfWork.SaveChangesAsync();
        if (structureChanged)
            _accessCache.InvalidateAll();
        return (await GetBranchByIdAsync(id))!;
    }

    /// <inheritdoc />
    public async Task DeleteBranchAsync(Guid id)
    {
        var cell = await _orgRepo.FindPartyCellAsync(id)
            ?? throw new NotFoundException("Không tìm thấy tổ chức Đảng cần xóa. Có thể tổ chức đã bị xóa; hãy tải lại danh sách.");

        var usage = await _orgRepo.GetPartyCellUsageAsync(id, Now);
        if (!usage.IsUnused)
            throw new ConflictException(BuildInUseMessage(BranchLabel, cell.Name, usage, "tổ chức Đảng khác"));

        _orgRepo.RemovePartyCell(cell);
        await _unitOfWork.SaveChangesAsync();
        _accessCache.InvalidateAll();
    }

    // ===================== Đơn vị chính quyền =====================

    /// <inheritdoc />
    public async Task<List<DepartmentDto>> GetDepartmentsAsync()
    {
        var departments = await _orgRepo.ListDepartmentsAsync();
        var members = await _orgRepo.CountMembersByDepartmentAsync();
        var children = await _orgRepo.CountChildDepartmentsAsync();
        return OrderAsTree(departments).Select(d => Fill(new DepartmentDto(), d, d.UnitType, departments, members, children)).ToList();
    }

    /// <inheritdoc />
    public async Task<DepartmentDto?> GetDepartmentByIdAsync(Guid id)
        => (await GetDepartmentsAsync()).FirstOrDefault(d => d.Id == id);

    /// <inheritdoc />
    public async Task<DepartmentDto> CreateDepartmentAsync(SaveCatalogItemDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var code = CatalogRules.NormalizeCode(input.Code);
        var name = input.Name?.Trim() ?? string.Empty;
        EnsureValid(CatalogRules.ValidateCode(code, DepartmentLabel), CatalogRules.ValidateName(name, DepartmentLabel));

        if (await _orgRepo.DepartmentCodeExistsAsync(code))
            throw DuplicateCode(DepartmentLabel, code);

        var all = (await _orgRepo.ListDepartmentsIncludingDeletedAsync()).Where(d => !d.IsDeleted).ToList();
        var department = new AdministrativeDepartment
        {
            Code = code,
            Name = name,
            Description = input.Description?.Trim() ?? string.Empty,
            SortOrder = input.SortOrder ?? 0,
            IsActive = input.IsActive ?? true,
            ParentId = ResolveNewParent(input.ParentId, all, DepartmentLabel),
            UnitTypeId = await ResolveUnitTypeAsync(input.UnitTypeId, null, OrgSide.Administrative)
        };
        _orgRepo.AddDepartment(department);
        all.Add(department);
        RecomputePaths(all, DepartmentLabel);
        await _unitOfWork.SaveChangesAsync();
        _accessCache.InvalidateAll();
        return (await GetDepartmentByIdAsync(department.Id))!;
    }

    /// <inheritdoc />
    public async Task<DepartmentDto> UpdateDepartmentAsync(Guid id, SaveCatalogItemDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var all = (await _orgRepo.ListDepartmentsIncludingDeletedAsync()).Where(d => !d.IsDeleted).ToList();
        var department = all.FirstOrDefault(d => d.Id == id)
            ?? throw new NotFoundException("Không tìm thấy đơn vị cần cập nhật. Có thể đơn vị đã bị xóa; hãy tải lại danh sách.");

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
        department.UnitTypeId = await ResolveUnitTypeAsync(input.UnitTypeId, department.UnitTypeId, OrgSide.Administrative);

        var structureChanged = ApplyParentChange(department, input.ParentId, all, DepartmentLabel);
        await _unitOfWork.SaveChangesAsync();
        if (structureChanged)
            _accessCache.InvalidateAll();
        return (await GetDepartmentByIdAsync(id))!;
    }

    /// <inheritdoc />
    public async Task DeleteDepartmentAsync(Guid id)
    {
        var department = await _orgRepo.FindDepartmentAsync(id)
            ?? throw new NotFoundException("Không tìm thấy đơn vị cần xóa. Có thể đơn vị đã bị xóa; hãy tải lại danh sách.");

        var usage = await _orgRepo.GetDepartmentUsageAsync(id, Now);
        if (!usage.IsUnused)
            throw new ConflictException(BuildInUseMessage(DepartmentLabel, department.Name, usage, "đơn vị khác"));

        _orgRepo.RemoveDepartment(department);
        await _unitOfWork.SaveChangesAsync();
        _accessCache.InvalidateAll();
    }

    // ===================== Loại đơn vị =====================

    /// <inheritdoc />
    public async Task<List<OrgUnitTypeDto>> GetUnitTypesAsync()
    {
        var types = await _orgRepo.ListUnitTypesAsync();
        var counts = await _orgRepo.CountUnitsByTypeAsync();
        return types.Select(t => ToDto(t, counts.GetValueOrDefault(t.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<OrgUnitTypeDto> CreateUnitTypeAsync(SaveOrgUnitTypeDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var name = input.Name?.Trim() ?? string.Empty;
        ValidateUnitTypeName(name);
        if (!CatalogRules.TryParseSide(input.Side, out var side))
            throw new ValidationException("Hãy chọn bên của loại đơn vị: Tổ chức Đảng (Party) hoặc Đơn vị chính quyền (Administrative).");
        if (await _orgRepo.UnitTypeNameExistsAsync(side, name))
            throw new ConflictException($"Loại đơn vị \"{name}\" ({CatalogRules.SideName(side)}) đã có. Hãy dùng tên khác.");

        var type = new OrgUnitType
        {
            Name = name,
            Side = side,
            SortOrder = input.SortOrder ?? 0,
            IsActive = input.IsActive ?? true
        };
        _orgRepo.AddUnitType(type);
        await _unitOfWork.SaveChangesAsync();
        return ToDto(type, 0);
    }

    /// <inheritdoc />
    public async Task<OrgUnitTypeDto> UpdateUnitTypeAsync(Guid id, SaveOrgUnitTypeDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var type = await _orgRepo.FindUnitTypeAsync(id)
            ?? throw new NotFoundException("Không tìm thấy loại đơn vị cần cập nhật. Hãy tải lại danh sách.");
        var counts = await _orgRepo.CountUnitsByTypeAsync();
        var used = counts.GetValueOrDefault(id);

        var side = type.Side;
        if (input.Side != null)
        {
            if (!CatalogRules.TryParseSide(input.Side, out side))
                throw new ValidationException("Bên của loại đơn vị không hợp lệ. Hãy chọn Tổ chức Đảng hoặc Đơn vị chính quyền.");
            if (side != type.Side && used > 0)
                throw new ConflictException(
                    $"Không thể đổi bên của loại \"{type.Name}\" vì đang có {used} đơn vị thuộc loại này. Hãy đổi loại của các đơn vị đó trước.");
        }

        var name = input.Name == null ? type.Name : input.Name.Trim();
        ValidateUnitTypeName(name);
        if ((side != type.Side || !string.Equals(name, type.Name, StringComparison.OrdinalIgnoreCase))
            && await _orgRepo.UnitTypeNameExistsAsync(side, name, id))
            throw new ConflictException($"Loại đơn vị \"{name}\" ({CatalogRules.SideName(side)}) đã có. Hãy dùng tên khác.");

        type.Name = name;
        type.Side = side;
        if (input.SortOrder.HasValue) type.SortOrder = input.SortOrder.Value;
        if (input.IsActive.HasValue) type.IsActive = input.IsActive.Value;
        await _unitOfWork.SaveChangesAsync();
        return ToDto(type, used);
    }

    /// <inheritdoc />
    public async Task DeleteUnitTypeAsync(Guid id)
    {
        var type = await _orgRepo.FindUnitTypeAsync(id)
            ?? throw new NotFoundException("Không tìm thấy loại đơn vị cần xóa. Hãy tải lại danh sách.");
        var used = (await _orgRepo.CountUnitsByTypeAsync()).GetValueOrDefault(id);
        if (used > 0)
            throw new ConflictException(
                $"Không thể xóa loại \"{type.Name}\" vì còn {used} đơn vị thuộc loại này. Hãy đổi loại của các đơn vị đó hoặc chuyển loại sang \"Ngừng dùng\".");

        _orgRepo.RemoveUnitType(type);
        await _unitOfWork.SaveChangesAsync();
    }

    // ===================== Hỗ trợ cây =====================

    /// <summary>Đơn vị cha cho đơn vị mới: null/Empty = gốc; phải tồn tại (chưa xóa) cùng bên.</summary>
    private static Guid? ResolveNewParent<T>(Guid? parentId, IReadOnlyList<T> all, string label) where T : IOrgUnit
    {
        if (!parentId.HasValue || parentId.Value == Guid.Empty)
            return null;
        if (all.All(u => u.Id != parentId.Value))
            throw new ValidationException($"Không tìm thấy {label} cha đã chọn (có thể đã bị xóa). Hãy chọn lại {label} cha.");
        return parentId.Value;
    }

    /// <summary>
    /// Đổi cha (null = giữ nguyên, Empty = thành gốc); chặn chọn chính nó hoặc con cháu của nó (tạo vòng).
    /// Trả true nếu cấu trúc cây thay đổi.
    /// </summary>
    private static bool ApplyParentChange<T>(T unit, Guid? requestedParent, List<T> all, string label) where T : IOrgUnit
    {
        if (!requestedParent.HasValue)
            return false;
        var newParent = requestedParent.Value == Guid.Empty ? (Guid?)null : requestedParent.Value;
        if (newParent == unit.ParentId)
            return false;

        if (newParent.HasValue)
        {
            if (newParent.Value == unit.Id)
                throw new ValidationException($"Không thể chọn chính {label} \"{unit.Name}\" làm {label} cha.");
            var parent = all.FirstOrDefault(u => u.Id == newParent.Value)
                ?? throw new ValidationException($"Không tìm thấy {label} cha đã chọn (có thể đã bị xóa). Hãy chọn lại {label} cha.");
            if (OrgTree.IsSelfOrDescendant(parent.Path, unit.Id))
                throw new ValidationException(
                    $"Không thể chọn \"{parent.Name}\" làm {label} cha của \"{unit.Name}\" vì \"{parent.Name}\" đang là {label} cấp dưới của "
                    + $"\"{unit.Name}\" (sẽ tạo vòng). Hãy chọn {label} cha khác.");
        }

        unit.ParentId = newParent;
        RecomputePaths(all, label);
        return true;
    }

    /// <summary>Tính lại đường dẫn của mọi nút một bên; phát hiện vòng/thiếu cha → 400.</summary>
    public static void RecomputePaths<T>(IReadOnlyList<T> all, string label) where T : IOrgUnit
    {
        var result = OrgTree.BuildPaths(all.Select(u => new OrgNodeLink(u.Id, u.ParentId)));
        if (result.CycleNodes.Count > 0)
        {
            var names = string.Join(", ", all.Where(u => result.CycleNodes.Contains(u.Id)).Select(u => $"\"{u.Name}\"").Take(5));
            throw new ValidationException($"Quan hệ cấp trên – cấp dưới của {label} tạo thành vòng ({names}). Hãy chọn lại {label} cha.");
        }
        if (result.MissingParentNodes.Count > 0)
        {
            var names = string.Join(", ", all.Where(u => result.MissingParentNodes.Contains(u.Id)).Select(u => $"\"{u.Name}\"").Take(5));
            throw new ValidationException($"{names}: {label} cha không tồn tại hoặc đã bị xóa. Hãy chọn lại {label} cha.");
        }

        foreach (var unit in all)
        {
            var path = result.Paths[unit.Id];
            if (!string.Equals(unit.Path, path, StringComparison.Ordinal))
                unit.Path = path;
        }
    }

    /// <summary>Sắp phẳng theo cây: gốc trước (thứ tự, mã), mỗi nút theo sau là các con của nó.</summary>
    public static List<T> OrderAsTree<T>(IReadOnlyList<T> units) where T : IOrgUnit
    {
        var ids = new HashSet<Guid>(units.Select(u => u.Id));
        var children = units
            .GroupBy(u => u.ParentId.HasValue && ids.Contains(u.ParentId.Value) ? u.ParentId : null)
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(Order).ThenBy(u => u.Code, StringComparer.OrdinalIgnoreCase).ToList());

        var result = new List<T>(units.Count);
        var visited = new HashSet<Guid>();
        void Visit(T unit)
        {
            if (!visited.Add(unit.Id))
                return;
            result.Add(unit);
            if (children.TryGetValue(unit.Id, out var list))
                foreach (var child in list)
                    Visit(child);
        }

        if (children.TryGetValue(Guid.Empty, out var roots))
            foreach (var root in roots)
                Visit(root);
        foreach (var unit in units.Where(u => !visited.Contains(u.Id)))
            Visit(unit); // dữ liệu hỏng (vòng) — vẫn hiển thị
        return result;

        static int Order(T unit) => unit switch
        {
            PartyCell c => c.SortOrder,
            AdministrativeDepartment d => d.SortOrder,
            _ => 0
        };
    }

    private async Task<Guid?> ResolveUnitTypeAsync(Guid? requested, Guid? current, OrgSide side)
    {
        if (!requested.HasValue)
            return current;
        if (requested.Value == Guid.Empty)
            return null;
        if (requested == current)
            return current;
        var type = await _orgRepo.FindUnitTypeAsync(requested.Value)
            ?? throw new ValidationException("Loại đơn vị đã chọn không tồn tại. Hãy chọn loại khác trong danh mục.");
        if (type.Side != side)
            throw new ValidationException(
                $"Loại \"{type.Name}\" thuộc bên {CatalogRules.SideName(type.Side)}, không dùng được cho {CatalogRules.SideName(side)}.");
        if (!type.IsActive)
            throw new ValidationException($"Loại \"{type.Name}\" đã ngừng dùng. Hãy chọn loại khác.");
        return type.Id;
    }

    private static TDto Fill<TDto, TUnit>(TDto dto, TUnit unit, OrgUnitType? type, IReadOnlyList<TUnit> all,
        IReadOnlyDictionary<Guid, int> members, IReadOnlyDictionary<Guid, int> children)
        where TDto : OrgUnitDto
        where TUnit : IOrgUnit
    {
        dto.Id = unit.Id;
        dto.Code = unit.Code;
        dto.Name = unit.Name;
        dto.ParentId = unit.ParentId;
        dto.ParentName = unit.ParentId.HasValue ? all.FirstOrDefault(u => u.Id == unit.ParentId.Value)?.Name : null;
        dto.UnitTypeId = type?.Id;
        dto.UnitTypeName = type?.Name;
        dto.Path = unit.Path;
        dto.Depth = Math.Max(0, unit.Path.Count(ch => ch == '/') - 2);
        dto.MemberCount = members.GetValueOrDefault(unit.Id);
        dto.ChildCount = children.GetValueOrDefault(unit.Id);
        switch (unit)
        {
            case PartyCell c:
                dto.Description = c.Description;
                dto.SortOrder = c.SortOrder;
                dto.IsActive = c.IsActive;
                break;
            case AdministrativeDepartment d:
                dto.Description = d.Description;
                dto.SortOrder = d.SortOrder;
                dto.IsActive = d.IsActive;
                break;
        }
        return dto;
    }

    // ===================== Hỗ trợ chung =====================

    private static void EnsureValid(params string?[] errors)
    {
        var first = errors.FirstOrDefault(e => e != null);
        if (first != null)
            throw new ValidationException(first);
    }

    private static void ValidateUnitTypeName(string name)
    {
        if (name.Length == 0)
            throw new ValidationException("Tên loại đơn vị không được để trống. Hãy nhập tên loại (ví dụ: Phòng, Chi bộ).");
        if (name.Length > CatalogRules.MaxUnitTypeNameLength)
            throw new ValidationException($"Tên loại đơn vị dài quá {CatalogRules.MaxUnitTypeNameLength} ký tự. Hãy rút ngắn tên.");
    }

    private static ConflictException DuplicateCode(string label, string code)
        => new($"Mã {label} \"{code}\" đã được dùng (kể cả cho {label} đã xóa). Hãy chọn mã khác.");

    /// <summary>Thông báo 409 nêu số lượng dữ liệu còn tham chiếu và cách xử lý.</summary>
    public static string BuildInUseMessage(string label, string name, CatalogUsage usage, string moveTarget)
    {
        var parts = new List<string>();
        if (usage.Children > 0) parts.Add($"{usage.Children} {label} cấp dưới");
        if (usage.Members > 0) parts.Add($"{usage.Members} cán bộ");
        if (usage.Records > 0) parts.Add($"{usage.Records} hồ sơ đánh giá");
        if (usage.Assignments > 0) parts.Add($"{usage.Assignments} bản gán vai trò đang/sắp hiệu lực");
        if (usage.Positions > 0) parts.Add($"{usage.Positions} chức vụ đang/sắp hiệu lực");
        var reason = string.Join(", ", parts);

        var advice = new List<string>();
        if (usage.Children > 0) advice.Add($"Hãy chuyển hoặc xóa các {label} cấp dưới trước.");
        if (usage.Members > 0) advice.Add($"Hãy chuyển cán bộ sang {moveTarget} trước.");
        if (usage.Assignments > 0) advice.Add("Hãy kết thúc hoặc thu hồi các bản gán vai trò có phạm vi này.");
        if (usage.Positions > 0) advice.Add("Hãy kết thúc các chức vụ giữ tại đơn vị này.");
        if (usage.Records > 0)
            advice.Add($"Hồ sơ đánh giá phải được giữ lại, vì vậy hãy chuyển {label} sang \"Ngừng hoạt động\" thay vì xóa.");

        return $"Không thể xóa {label} \"{name}\" vì còn {reason}. {string.Join(" ", advice)}".Trim();
    }

    private static OrgUnitTypeDto ToDto(OrgUnitType t, int unitCount) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Side = t.Side.ToString(),
        SortOrder = t.SortOrder,
        IsActive = t.IsActive,
        UnitCount = unitCount
    };
}
