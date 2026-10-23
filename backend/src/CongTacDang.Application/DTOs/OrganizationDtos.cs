using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Loại đơn vị (<c>GET /api/organizations/unit-types</c>).</summary>
public class OrgUnitTypeDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary><c>Party</c> (tổ chức Đảng) / <c>Administrative</c> (đơn vị chính quyền).</summary>
    public string Side { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Chỉ đếm đơn vị chưa xóa.</summary>
    public int UnitCount { get; set; }
}

/// <summary>Cập nhật: null = giữ nguyên; không đổi được bên khi đã có đơn vị dùng.</summary>
public class SaveOrgUnitTypeDto
{
    /// <summary>Bắt buộc khi thêm.</summary>
    public string? Name { get; set; }

    /// <summary><c>Party</c> / <c>Administrative</c>; bắt buộc khi thêm.</summary>
    public string? Side { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }
}

/// <summary>Chức vụ trong danh mục (<c>GET /api/positions</c>).</summary>
public class PositionDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary><c>Party</c> / <c>Administrative</c> / <c>MassOrganization</c> / <c>Other</c>.</summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>Mã thống kê Mẫu 15A/15B (M1…M26) hoặc null.</summary>
    public string? StatCode { get; set; }

    /// <summary><c>CoSo</c> / <c>CapTren</c> hoặc null.</summary>
    public string? DefaultApprovalAuthority { get; set; }

    /// <summary>Mã cách xét duyệt gợi ý khi thêm người giữ chức vụ vào kỳ; null = theo thẩm quyền.</summary>
    public string? DefaultWorkflowProfileCode { get; set; }

    public bool IsLeadership { get; set; }

    /// <summary>Không tính vào trần tỷ lệ Hoàn thành xuất sắc.</summary>
    public bool IsHead { get; set; }

    /// <summary>Kiểu trình bày biểu mẫu tự đánh giá mặc định; null = không xác định.</summary>
    public Guid? FormLayoutId { get; set; }

    public string? FormLayoutName { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Số bản ghi chức vụ đang hiệu lực.</summary>
    public int HolderCount { get; set; }
}

/// <summary>Cập nhật: null = giữ nguyên; chuỗi rỗng ở <see cref="StatCode"/> / <see cref="DefaultApprovalAuthority"/> → bỏ giá trị.</summary>
public class SavePositionDto
{
    /// <summary>Bắt buộc khi thêm, duy nhất.</summary>
    public string? Name { get; set; }

    /// <summary><c>Party</c> / <c>Administrative</c> / <c>MassOrganization</c> / <c>Other</c>; bắt buộc khi thêm.</summary>
    public string? Side { get; set; }

    /// <summary>M1…M26; rỗng = không có.</summary>
    public string? StatCode { get; set; }

    /// <summary><c>CoSo</c> / <c>CapTren</c>; rỗng = không xác định.</summary>
    public string? DefaultApprovalAuthority { get; set; }

    /// <summary>Mã cách xét duyệt gợi ý; rỗng = bỏ (theo thẩm quyền).</summary>
    public string? DefaultWorkflowProfileCode { get; set; }

    public bool? IsLeadership { get; set; }

    /// <summary>Không tính vào trần tỷ lệ Hoàn thành xuất sắc.</summary>
    public bool? IsHead { get; set; }

    /// <summary>Rỗng = không xác định.</summary>
    public string? FormLayoutId { get; set; }

    public int? SortOrder { get; set; }

    public bool? IsActive { get; set; }
}

/// <summary>Một chức vụ của cán bộ (<c>GET /api/users/{id}/positions</c>).</summary>
public class MemberPositionDto
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PositionId { get; set; }

    public string PositionName { get; set; } = string.Empty;

    public string Side { get; set; } = string.Empty;

    public string? StatCode { get; set; }

    public string? DefaultApprovalAuthority { get; set; }

    public Guid? PartyCellId { get; set; }

    public string? PartyCellName { get; set; }

    public Guid? DepartmentId { get; set; }

    public string? DepartmentName { get; set; }

    public bool IsPrimary { get; set; }

    /// <summary>Giữ dạng "Quyền" (đánh giá như chính chức vụ đó).</summary>
    public bool IsActing { get; set; }

    /// <summary>"Quyền " + tên chức vụ khi <see cref="IsActing"/>.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>UTC, bao gồm.</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>UTC, không bao gồm.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Tại thời điểm truy vấn.</summary>
    public bool IsEffective { get; set; }

    public string? Note { get; set; }
}

/// <summary>Chỉ một trong <see cref="PartyCellId"/> / <see cref="DepartmentId"/> (có thể cả hai trống).
/// Cập nhật: null = giữ nguyên; <c>Guid.Empty</c> ở đơn vị → bỏ đơn vị.</summary>
public class SaveMemberPositionDto
{
    /// <summary>Bắt buộc khi thêm; không đổi khi sửa.</summary>
    public Guid? PositionId { get; set; }

    public Guid? PartyCellId { get; set; }

    public Guid? DepartmentId { get; set; }

    /// <summary>Đặt làm chính thì bỏ chức vụ chính cũ của người đó.</summary>
    public bool? IsPrimary { get; set; }

    /// <summary>Thêm: mặc định false; sửa: null = giữ nguyên.</summary>
    public bool? IsActing { get; set; }

    /// <summary>Mặc định: bây giờ.</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Không bao gồm; null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Khi sửa: true → bỏ thời hạn.</summary>
    public bool? ClearValidTo { get; set; }

    /// <summary>Số quyết định…</summary>
    public string? Note { get; set; }
}

/// <summary>Thẩm quyền phê duyệt của cán bộ (<c>GET /api/users/{id}/approval-authority</c>).</summary>
public class ApprovalAuthorityInfoDto
{
    /// <summary><c>CoSo</c> / <c>CapTren</c>.</summary>
    public string Effective { get; set; } = string.Empty;

    /// <summary>Suy ra từ chức vụ đang hiệu lực.</summary>
    public string Derived { get; set; } = string.Empty;

    /// <summary>null = không ghi đè.</summary>
    public string? Override { get; set; }

    public string? OverrideReason { get; set; }

    /// <summary>Chức vụ đang hiệu lực làm căn cứ suy ra CapTren.</summary>
    public List<string> CapTrenPositions { get; set; } = new();

    /// <summary>Mã nhỏ nhất trong các chức vụ đang hiệu lực.</summary>
    public string? StatCode { get; set; }
}

/// <summary>Đặt tay / bỏ đặt tay thẩm quyền phê duyệt (<c>PUT /api/users/{id}/approval-authority</c>).</summary>
public class SetApprovalAuthorityOverrideDto
{
    /// <summary><c>CoSo</c> / <c>CapTren</c>; null hoặc rỗng → bỏ ghi đè (quay về suy ra từ chức vụ).</summary>
    public string? Override { get; set; }

    /// <summary>Bắt buộc khi đặt tay.</summary>
    public string? Reason { get; set; }
}

/// <summary>Cách xét duyệt có thể gắn cho chức vụ.</summary>
public class WorkflowProfileOptionDto
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
