using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Một mã quyền trong danh mục (hiển thị ở ma trận quyền).</summary>
public class PermissionDefinitionDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>true: có nghĩa theo phạm vi gán; false: chỉ có nghĩa khi gán Toàn công ty.</summary>
    public bool AppliesScope { get; set; }

    public int SortOrder { get; set; }
}

public class PermissionModuleDto
{
    public string Module { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public List<PermissionDefinitionDto> Permissions { get; set; } = new();
}

public class AdminRoleDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Không xóa, không gỡ 2 quyền quản trị.</summary>
    public bool IsProtected { get; set; }

    public bool IsSystem { get; set; }

    /// <summary>Chỉ mã thuộc danh mục hiện hành.</summary>
    public List<string> PermissionCodes { get; set; } = new();

    /// <summary>Bản gán chưa hết hạn (đang hoặc sắp hiệu lực).</summary>
    public int AssignmentCount { get; set; }
}

public class CreateRoleRequestDto
{
    /// <summary>Bắt buộc, duy nhất.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> PermissionCodes { get; set; } = new();
}

public class UpdateRoleRequestDto
{
    /// <summary>Bắt buộc, duy nhất.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public class RoleAssignmentDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Username { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public Guid RoleId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    /// <summary><c>Global</c> | <c>Department</c> | <c>PartyCell</c>.</summary>
    public string ScopeType { get; set; } = "Global";

    /// <summary>null khi Global.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>"Toàn công ty" hoặc tên đơn vị chính quyền / tổ chức Đảng.</summary>
    public string ScopeName { get; set; } = string.Empty;

    /// <summary>UTC.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>UTC, không bao gồm; null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    public string? Note { get; set; }

    /// <summary>Tại thời điểm tra cứu: <c>Active</c> | <c>Future</c> | <c>Expired</c>.</summary>
    public string Status { get; set; } = "Active";
}

public class CreateRoleAssignmentRequestDto
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    /// <summary><c>Global</c> | <c>Department</c> | <c>PartyCell</c>; mặc định Global.</summary>
    public string? ScopeType { get; set; }

    /// <summary>Bắt buộc khi khác Global; phạm vi gồm cả đơn vị cấp dưới.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Mặc định: bây giờ.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Không bao gồm; trống = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    public string? Note { get; set; }
}

public class UpdateRoleAssignmentRequestDto
{
    /// <summary>Trống = giữ nguyên.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Trống = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    public string? Note { get; set; }
}

/// <summary>Quyền kèm phạm vi (trường <c>grants</c> của <c>/api/auth/me</c>).</summary>
public class AccessGrantDto
{
    public string Code { get; set; } = string.Empty;

    /// <summary><c>Global</c> | <c>Department</c> | <c>PartyCell</c>.</summary>
    public string ScopeType { get; set; } = "Global";

    /// <summary>null khi Global.</summary>
    public Guid? ScopeId { get; set; }

    public string ScopeName { get; set; } = string.Empty;
}

/// <summary>Nơi người dùng hiện tại thực hiện được một quyền (<c>/api/auth/scope</c>).</summary>
public class PermissionScopeDto
{
    public bool IsGlobal { get; set; }

    /// <summary>Đã gồm đơn vị con; rỗng khi toàn công ty.</summary>
    public List<Guid> DepartmentIds { get; set; } = new();

    /// <summary>Đã gồm tổ chức cấp dưới; rỗng khi toàn công ty.</summary>
    public List<Guid> PartyCellIds { get; set; } = new();
}

/// <summary>Nguồn của một quyền: phạm vi + vai trò + bản gán.</summary>
public class EffectiveGrantSourceDto
{
    public string ScopeType { get; set; } = "Global";

    public Guid? ScopeId { get; set; }

    public string ScopeName { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public Guid AssignmentId { get; set; }
}

public class EffectivePermissionItemDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public List<EffectiveGrantSourceDto> Sources { get; set; } = new();
}

/// <summary>"Người X làm được gì": quyền hiệu lực kèm phạm vi và nguồn.</summary>
public class UserEffectivePermissionsDto
{
    public Guid UserId { get; set; }

    /// <summary>false → không có quyền nào.</summary>
    public bool IsActive { get; set; }

    /// <summary>Theo thứ tự danh mục.</summary>
    public List<EffectivePermissionItemDto> Permissions { get; set; } = new();

    /// <summary>Chỉ bản gán đang hiệu lực.</summary>
    public List<RoleAssignmentDto> Assignments { get; set; } = new();
}
