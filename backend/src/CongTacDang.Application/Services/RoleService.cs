using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>Triển khai <see cref="IRoleService"/> kèm chốt chặn quản trị (docs/thiet-ke/phan-quyen.md mục 5).</summary>
public sealed class RoleService : IRoleService
{
    private const int MaxNameLength = 100;
    private const int MaxDescriptionLength = 1000;

    private static readonly IReadOnlyDictionary<string, string> ModuleNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["system"] = "Quản trị hệ thống",
        ["catalog"] = "Danh mục",
        ["period"] = "Kỳ đánh giá",
        ["evaluation"] = "Đánh giá cá nhân",
        ["collective"] = "Đánh giá tập thể",
        ["meeting"] = "Hội nghị, kiểm phiếu",
        ["report"] = "Báo cáo",
        ["attachment"] = "Văn bản, tài liệu"
    };

    private readonly IRoleRepository _roles;
    private readonly IRoleAssignmentRepository _assignments;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccessCacheInvalidator _cache;
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo dịch vụ.</summary>
    public RoleService(
        IRoleRepository roles,
        IRoleAssignmentRepository assignments,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IAccessCacheInvalidator cache,
        TimeProvider? time = null)
    {
        _roles = roles;
        _assignments = assignments;
        _guard = guard;
        _currentUser = currentUser;
        _cache = cache;
        _time = time ?? TimeProvider.System;
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <inheritdoc />
    public async Task<List<AdminRoleDto>> GetRolesAsync(CancellationToken ct = default)
    {
        EnsureCanReadRoles();
        var roles = await _roles.GetAllRolesWithPermissionsAsync();
        var result = new List<AdminRoleDto>(roles.Count);
        foreach (var role in roles)
            result.Add(Map(role, await _assignments.CountCurrentOrFutureByRoleAsync(role.Id, Now, ct)));
        return result;
    }

    /// <inheritdoc />
    public async Task<AdminRoleDto> GetRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        EnsureCanReadRoles();
        var role = await _roles.GetRoleByIdWithPermissionsAsync(roleId) ?? throw RoleNotFound();
        return Map(role, await _assignments.CountCurrentOrFutureByRoleAsync(role.Id, Now, ct));
    }

    /// <inheritdoc />
    public Task<List<PermissionModuleDto>> GetPermissionCatalogAsync(CancellationToken ct = default)
    {
        EnsureCanReadRoles();
        var result = PermissionCodes.Definitions
            .Select((d, index) => new PermissionDefinitionDto
            {
                Code = d.Code,
                Name = d.Name,
                Module = d.Module,
                Description = d.Description,
                AppliesScope = d.AppliesScope,
                SortOrder = index
            })
            .GroupBy(d => d.Module, StringComparer.Ordinal)
            .Select(g => new PermissionModuleDto
            {
                Module = g.Key,
                ModuleName = ModuleNames.TryGetValue(g.Key, out var name) ? name : g.Key,
                Permissions = g.OrderBy(p => p.SortOrder).ToList()
            })
            .ToList();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public async Task<AdminRoleDto> CreateRoleAsync(CreateRoleRequestDto request, CancellationToken ct = default)
    {
        EnsureCanManageRoles();
        var name = ValidateName(request.Name);
        var description = ValidateDescription(request.Description);
        if (await _roles.RoleNameExistsAsync(name, null, ct))
            throw new ConflictException($"Đã có vai trò tên \"{name}\". Hãy chọn tên khác.");

        var codes = ValidateCodes(request.PermissionCodes);
        var role = new AppRole
        {
            Id = Guid.NewGuid(),
            // Mã kỹ thuật sinh tự động — không dùng để phân quyền.
            Code = "ROLE_" + Guid.NewGuid().ToString("N").ToUpperInvariant(),
            Name = name,
            Description = description,
            IsSystem = false,
            IsProtected = false,
            Permissions = await _roles.GetPermissionsByCodesAsync(codes, ct)
        };
        EnsurePermissionRowsExist(codes, role.Permissions);

        _roles.AddRole(role);
        if (codes.Count > 0)
            _roles.AddRolePermissionsAudit(role.Id, Array.Empty<string>(), codes);
        await _roles.SaveChangesAsync(ct);
        return Map(role, 0);
    }

    /// <inheritdoc />
    public async Task<AdminRoleDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequestDto request, CancellationToken ct = default)
    {
        EnsureCanManageRoles();
        var role = await _roles.GetRoleForUpdateAsync(roleId, ct) ?? throw RoleNotFound();
        var name = ValidateName(request.Name);
        if (await _roles.RoleNameExistsAsync(name, roleId, ct))
            throw new ConflictException($"Đã có vai trò tên \"{name}\". Hãy chọn tên khác.");

        role.Name = name;
        role.Description = ValidateDescription(request.Description);
        await _roles.SaveChangesAsync(ct);
        // Tên vai trò xuất hiện trong nguồn quyền (tra cứu "người X làm được gì").
        _cache.InvalidateAll();
        return Map(role, await _assignments.CountCurrentOrFutureByRoleAsync(role.Id, Now, ct));
    }

    /// <inheritdoc />
    public async Task<AdminRoleDto> UpdateRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default)
    {
        EnsureCanManageRoles();
        var role = await _roles.GetRoleForUpdateAsync(roleId, ct) ?? throw RoleNotFound();
        var codes = ValidateCodes(permissionCodes);
        var now = Now;

        // Chốt "không tự nâng quyền": không sửa quyền của vai trò mình đang được gán.
        if (_currentUser.UserId.HasValue)
        {
            var mine = await _assignments.GetCurrentOrFutureForUserAsync(_currentUser.UserId.Value, now, ct);
            if (mine.Any(a => a.RoleId == roleId))
            {
                throw new ForbiddenException(
                    $"Bạn đang được gán vai trò \"{role.Name}\" nên không thể tự sửa quyền của vai trò này. "
                    + "Hãy nhờ một quản trị viên khác thực hiện.");
            }
        }

        // Chốt "vai trò bảo vệ": không gỡ được 2 quyền quản trị.
        if (role.IsProtected)
        {
            var missing = AdministratorInvariant.Codes.Where(c => !codes.Contains(c)).ToList();
            if (missing.Count > 0)
            {
                throw new ConflictException(
                    $"Vai trò \"{role.Name}\" được bảo vệ: không thể gỡ quyền "
                    + string.Join(", ", missing.Select(c => $"\"{PermissionCodes.DisplayName(c)}\"")) + ".");
            }
        }

        // Chốt "kiểm tra phạm vi hợp lệ": vai trò đang được gán theo Phòng/Chi bộ không được nhận quyền chỉ áp dụng Toàn công ty.
        var globalOnly = codes
            .Select(PermissionCodes.Find)
            .Where(d => d != null && !d.AppliesScope)
            .Select(d => d!)
            .ToList();
        var previous = role.Permissions.Select(p => p.Code).ToList();
        var addedGlobalOnly = globalOnly.Where(d => !previous.Contains(d.Code)).ToList();
        if (addedGlobalOnly.Count > 0 && await _assignments.HasNonGlobalCurrentOrFutureAssignmentsAsync(roleId, now, ct))
        {
            throw new ValidationException(
                $"Vai trò \"{role.Name}\" đang được gán theo phạm vi Phòng/Chi bộ nên không thể thêm quyền chỉ áp dụng cho Toàn công ty ("
                + string.Join(", ", addedGlobalOnly.Select(d => d.Name)) + "). Hãy tạo vai trò riêng cho các quyền này.");
        }

        // Chốt "luôn còn quản trị".
        var before = await _assignments.GetAdministratorGrantsAsync(now, ct);
        var after = before.Select(row => row.RoleId != roleId
            ? row
            : row with { PermissionCodes = AdministratorInvariant.Codes.Where(codes.Contains).ToList() });
        if (AdministratorInvariant.IsBrokenBy(before, after))
        {
            throw new ConflictException(
                "Không thể gỡ quyền quản trị khỏi vai trò này vì hệ thống sẽ không còn tài khoản đang hoạt động nào giữ quyền "
                + $"\"{PermissionCodes.DisplayName(PermissionCodes.SystemRolesManage)}\" hoặc "
                + $"\"{PermissionCodes.DisplayName(PermissionCodes.SystemAssignmentsManage)}\". Hãy gán quyền này cho người khác trước.");
        }

        var permissions = await _roles.GetPermissionsByCodesAsync(codes, ct);
        EnsurePermissionRowsExist(codes, permissions);

        // Giữ lại mã không còn trong danh mục (không hiển thị, không có hiệu lực) để không mất dữ liệu cũ ngoài ý muốn.
        var retainedUnknown = role.Permissions.Where(p => !PermissionCodes.IsDefined(p.Code)).ToList();
        role.Permissions.Clear();
        foreach (var permission in permissions.Concat(retainedUnknown))
            role.Permissions.Add(permission);

        _roles.AddRolePermissionsAudit(role.Id, previous.Where(PermissionCodes.IsDefined).ToList(), codes);
        await _roles.SaveChangesAsync(ct);
        // Quyền của vai trò ảnh hưởng mọi người mang vai trò → xóa toàn bộ cache quyền.
        _cache.InvalidateAll();
        return Map(role, await _assignments.CountCurrentOrFutureByRoleAsync(role.Id, now, ct));
    }

    /// <inheritdoc />
    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        EnsureCanManageRoles();
        var role = await _roles.GetRoleForUpdateAsync(roleId, ct) ?? throw RoleNotFound();
        if (role.IsProtected)
            throw new ConflictException($"Vai trò \"{role.Name}\" được bảo vệ, không thể xóa.");

        var count = await _assignments.CountCurrentOrFutureByRoleAsync(roleId, Now, ct);
        if (count > 0)
        {
            throw new ConflictException(
                $"Không thể xóa vai trò \"{role.Name}\" vì đang có {count} bản gán còn hiệu lực hoặc sắp hiệu lực. "
                + "Hãy kết thúc hoặc xóa các bản gán này trước.");
        }

        role.IsDeleted = true;
        role.DeletedAt = Now;
        role.DeletedBy = _currentUser.UserId;
        await _roles.SaveChangesAsync(ct);
        _cache.InvalidateAll();
    }

    #region Hỗ trợ

    /// <summary>Đọc vai trò / danh mục quyền: người quản lý vai trò hoặc người gán vai trò (cần chọn vai trò khi gán).</summary>
    private void EnsureCanReadRoles()
    {
        if (!_guard.Can(PermissionCodes.SystemRolesManage, AccessTarget.None)
            && !_guard.Can(PermissionCodes.SystemAssignmentsManage, AccessTarget.None))
        {
            throw new ForbiddenException(
                $"Bạn cần quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemRolesManage)}\" hoặc "
                + $"\"{PermissionCodes.DisplayName(PermissionCodes.SystemAssignmentsManage)}\" (phạm vi Toàn công ty) để xem danh sách vai trò. "
                + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }
    }

    private void EnsureCanManageRoles()
    {
        if (!_guard.Can(PermissionCodes.SystemRolesManage, AccessTarget.None))
        {
            throw new ForbiddenException(
                $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.SystemRolesManage)}\" (phạm vi Toàn công ty). "
                + "Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.");
        }
    }

    private static NotFoundException RoleNotFound() => new("Không tìm thấy vai trò (có thể đã bị xóa).");

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new ValidationException("Tên vai trò không được để trống.");
        if (trimmed.Length > MaxNameLength)
            throw new ValidationException($"Tên vai trò tối đa {MaxNameLength} ký tự.");
        return trimmed;
    }

    private static string ValidateDescription(string? description)
    {
        var trimmed = description?.Trim() ?? string.Empty;
        if (trimmed.Length > MaxDescriptionLength)
            throw new ValidationException($"Mô tả vai trò tối đa {MaxDescriptionLength} ký tự.");
        return trimmed;
    }

    /// <summary>Chuẩn hóa danh sách mã quyền; mã không có trong danh mục → 400.</summary>
    private static List<string> ValidateCodes(IEnumerable<string>? codes)
    {
        var list = (codes ?? Array.Empty<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var unknown = list.Where(c => !PermissionCodes.IsDefined(c)).ToList();
        if (unknown.Count > 0)
            throw new ValidationException($"Mã quyền không hợp lệ: {string.Join(", ", unknown)}. Chỉ chọn quyền trong danh mục.");
        return list;
    }

    private static void EnsurePermissionRowsExist(IReadOnlyCollection<string> codes, IEnumerable<Permission> rows)
    {
        var found = rows.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
        var missing = codes.Where(c => !found.Contains(c)).ToList();
        if (missing.Count > 0)
        {
            // Bảng permissions được seeder đồng bộ khi khởi động; thiếu là lỗi cấu hình triển khai.
            throw new InvalidOperationException($"Thiếu bản ghi quyền trong CSDL: {string.Join(", ", missing)}. Hãy khởi động lại để đồng bộ danh mục quyền.");
        }
    }

    private static AdminRoleDto Map(AppRole role, int assignmentCount) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        IsProtected = role.IsProtected,
        IsSystem = role.IsSystem,
        PermissionCodes = role.Permissions
            .Where(p => !p.IsDeleted && PermissionCodes.IsDefined(p.Code))
            .Select(p => p.Code)
            .OrderBy(c => Array.IndexOf(PermissionCodes.All, c))
            .ToList(),
        AssignmentCount = assignmentCount
    };

    #endregion
}
