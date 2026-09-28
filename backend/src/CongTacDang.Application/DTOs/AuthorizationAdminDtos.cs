using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Một mã quyền trong danh mục (hiển thị ở ma trận quyền).</summary>
public class PermissionDefinitionDto
{
    /// <summary>Mã quyền (vd. <c>evaluation.read</c>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Phân hệ.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>true: có nghĩa theo phạm vi gán; false: chỉ có nghĩa khi gán Toàn công ty.</summary>
    public bool AppliesScope { get; set; }

    /// <summary>Thứ tự hiển thị.</summary>
    public int SortOrder { get; set; }
}

/// <summary>Nhóm quyền theo phân hệ.</summary>
public class PermissionModuleDto
{
    /// <summary>Mã phân hệ.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Tên phân hệ hiển thị.</summary>
    public string ModuleName { get; set; } = string.Empty;

    /// <summary>Các quyền của phân hệ theo thứ tự hiển thị.</summary>
    public List<PermissionDefinitionDto> Permissions { get; set; } = new();
}

/// <summary>Vai trò (quản trị).</summary>
public class AdminRoleDto
{
    /// <summary>Id vai trò.</summary>
    public Guid Id { get; set; }

    /// <summary>Tên vai trò.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Vai trò được bảo vệ (không xóa, không gỡ 2 quyền quản trị).</summary>
    public bool IsProtected { get; set; }

    /// <summary>Vai trò do hệ thống khởi tạo.</summary>
    public bool IsSystem { get; set; }

    /// <summary>Mã quyền của vai trò (chỉ mã thuộc danh mục hiện hành).</summary>
    public List<string> PermissionCodes { get; set; } = new();

    /// <summary>Số bản gán chưa hết hạn (đang hoặc sắp hiệu lực).</summary>
    public int AssignmentCount { get; set; }
}

/// <summary>Yêu cầu tạo vai trò.</summary>
public class CreateRoleRequestDto
{
    /// <summary>Tên vai trò (bắt buộc, duy nhất).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả.</summary>
    public string? Description { get; set; }

    /// <summary>Mã quyền ban đầu (tùy chọn).</summary>
    public List<string> PermissionCodes { get; set; } = new();
}

/// <summary>Yêu cầu đổi tên/mô tả vai trò.</summary>
public class UpdateRoleRequestDto
{
    /// <summary>Tên vai trò (bắt buộc, duy nhất).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả.</summary>
    public string? Description { get; set; }
}

/// <summary>Bản gán vai trò.</summary>
public class RoleAssignmentDto
{
    /// <summary>Id bản gán.</summary>
    public Guid Id { get; set; }

    /// <summary>Người được gán.</summary>
    public Guid UserId { get; set; }

    /// <summary>Tên đăng nhập người được gán.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Họ tên người được gán.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Vai trò.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Tên vai trò.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Loại phạm vi: <c>Global</c> | <c>Department</c> | <c>PartyCell</c>.</summary>
    public string ScopeType { get; set; } = "Global";

    /// <summary>Id Phòng/Chi bộ (null khi Global).</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Tên phạm vi ("Toàn công ty", tên Phòng hoặc Chi bộ).</summary>
    public string ScopeName { get; set; } = string.Empty;

    /// <summary>Hiệu lực từ (UTC).</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Hiệu lực đến (UTC, không bao gồm); null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; set; }

    /// <summary>Trạng thái tại thời điểm tra cứu: <c>Active</c> | <c>Future</c> | <c>Expired</c>.</summary>
    public string Status { get; set; } = "Active";
}

/// <summary>Yêu cầu tạo bản gán vai trò.</summary>
public class CreateRoleAssignmentRequestDto
{
    /// <summary>Người được gán.</summary>
    public Guid UserId { get; set; }

    /// <summary>Vai trò.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Loại phạm vi: <c>Global</c> | <c>Department</c> | <c>PartyCell</c> (mặc định Global).</summary>
    public string? ScopeType { get; set; }

    /// <summary>Id Phòng/Chi bộ (bắt buộc khi khác Global, bỏ trống khi Global).</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Hiệu lực từ (mặc định: ngay bây giờ).</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Hiệu lực đến (không bao gồm); bỏ trống = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; set; }
}

/// <summary>Yêu cầu sửa thời hạn/ghi chú bản gán.</summary>
public class UpdateRoleAssignmentRequestDto
{
    /// <summary>Hiệu lực từ (bỏ trống = giữ nguyên).</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Hiệu lực đến (bỏ trống = không thời hạn).</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; set; }
}

/// <summary>Một quyền kèm phạm vi (dùng cho <c>/api/auth/me</c> — trường <c>grants</c>).</summary>
public class AccessGrantDto
{
    /// <summary>Mã quyền.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Loại phạm vi: <c>Global</c> | <c>Department</c> | <c>PartyCell</c>.</summary>
    public string ScopeType { get; set; } = "Global";

    /// <summary>Id Phòng/Chi bộ (null khi Global).</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Tên phạm vi.</summary>
    public string ScopeName { get; set; } = string.Empty;
}

/// <summary>Nguồn của một quyền: phạm vi + vai trò + bản gán.</summary>
public class EffectiveGrantSourceDto
{
    /// <summary>Loại phạm vi.</summary>
    public string ScopeType { get; set; } = "Global";

    /// <summary>Id Phòng/Chi bộ.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Tên phạm vi.</summary>
    public string ScopeName { get; set; } = string.Empty;

    /// <summary>Vai trò nguồn.</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Bản gán nguồn.</summary>
    public Guid AssignmentId { get; set; }
}

/// <summary>Một quyền hiệu lực kèm các nguồn cấp.</summary>
public class EffectivePermissionItemDto
{
    /// <summary>Mã quyền.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Phân hệ.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Các phạm vi/vai trò/bản gán cấp quyền này.</summary>
    public List<EffectiveGrantSourceDto> Sources { get; set; } = new();
}

/// <summary>"Người X làm được gì": quyền hiệu lực kèm phạm vi và nguồn.</summary>
public class UserEffectivePermissionsDto
{
    /// <summary>Người dùng.</summary>
    public Guid UserId { get; set; }

    /// <summary>Tài khoản đang hoạt động (false → không có quyền nào).</summary>
    public bool IsActive { get; set; }

    /// <summary>Các quyền (theo thứ tự danh mục).</summary>
    public List<EffectivePermissionItemDto> Permissions { get; set; } = new();

    /// <summary>Các bản gán đang hiệu lực.</summary>
    public List<RoleAssignmentDto> Assignments { get; set; } = new();
}
