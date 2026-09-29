using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Loại đơn vị (<c>GET /api/organizations/unit-types</c>).</summary>
public class OrgUnitTypeDto
{
    /// <summary>Mã định danh.</summary>
    public Guid Id { get; set; }

    /// <summary>Tên loại.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bên: <c>Party</c> (tổ chức Đảng) / <c>Administrative</c> (đơn vị chính quyền).</summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>Thứ tự hiển thị.</summary>
    public int SortOrder { get; set; }

    /// <summary>Đang dùng.</summary>
    public bool IsActive { get; set; }

    /// <summary>Số đơn vị (chưa xóa) đang thuộc loại này.</summary>
    public int UnitCount { get; set; }
}

/// <summary>Thêm/sửa loại đơn vị. Cập nhật: trường null giữ nguyên (không đổi được bên khi đã có đơn vị dùng).</summary>
public class SaveOrgUnitTypeDto
{
    /// <summary>Tên loại (bắt buộc khi thêm).</summary>
    public string? Name { get; set; }

    /// <summary>Bên: <c>Party</c> / <c>Administrative</c> (bắt buộc khi thêm).</summary>
    public string? Side { get; set; }

    /// <summary>Thứ tự hiển thị.</summary>
    public int? SortOrder { get; set; }

    /// <summary>Đang dùng.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Chức vụ trong danh mục (<c>GET /api/positions</c>).</summary>
public class PositionDto
{
    /// <summary>Mã định danh.</summary>
    public Guid Id { get; set; }

    /// <summary>Tên chức vụ.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Bên: <c>Party</c> / <c>Administrative</c> / <c>MassOrganization</c> / <c>Other</c>.</summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>Mã chức danh thống kê Mẫu 15A/15B (M1…M26) hoặc null.</summary>
    public string? StatCode { get; set; }

    /// <summary>Thẩm quyền mặc định: <c>CoSo</c> / <c>CapTren</c> hoặc null.</summary>
    public string? DefaultApprovalAuthority { get; set; }

    /// <summary>Chức vụ lãnh đạo, quản lý.</summary>
    public bool IsLeadership { get; set; }

    /// <summary>Thứ tự hiển thị.</summary>
    public int SortOrder { get; set; }

    /// <summary>Đang dùng.</summary>
    public bool IsActive { get; set; }

    /// <summary>Số người đang giữ chức vụ (bản ghi chức vụ đang hiệu lực).</summary>
    public int HolderCount { get; set; }
}

/// <summary>
/// Thêm/sửa chức vụ. Cập nhật: trường null giữ nguyên; <see cref="StatCode"/> hoặc <see cref="DefaultApprovalAuthority"/>
/// là chuỗi rỗng → bỏ giá trị.
/// </summary>
public class SavePositionDto
{
    /// <summary>Tên chức vụ (bắt buộc khi thêm, duy nhất).</summary>
    public string? Name { get; set; }

    /// <summary>Bên: <c>Party</c> / <c>Administrative</c> / <c>MassOrganization</c> / <c>Other</c> (bắt buộc khi thêm).</summary>
    public string? Side { get; set; }

    /// <summary>Mã chức danh thống kê (M1…M26); rỗng = không có.</summary>
    public string? StatCode { get; set; }

    /// <summary>Thẩm quyền mặc định: <c>CoSo</c> / <c>CapTren</c>; rỗng = không xác định.</summary>
    public string? DefaultApprovalAuthority { get; set; }

    /// <summary>Chức vụ lãnh đạo, quản lý.</summary>
    public bool? IsLeadership { get; set; }

    /// <summary>Thứ tự hiển thị.</summary>
    public int? SortOrder { get; set; }

    /// <summary>Đang dùng.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Một chức vụ của cán bộ (<c>GET /api/users/{id}/positions</c>).</summary>
public class MemberPositionDto
{
    /// <summary>Mã định danh bản ghi chức vụ.</summary>
    public Guid Id { get; set; }

    /// <summary>Cán bộ.</summary>
    public Guid UserId { get; set; }

    /// <summary>Chức vụ.</summary>
    public Guid PositionId { get; set; }

    /// <summary>Tên chức vụ.</summary>
    public string PositionName { get; set; } = string.Empty;

    /// <summary>Bên của chức vụ.</summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>Mã thống kê của chức vụ.</summary>
    public string? StatCode { get; set; }

    /// <summary>Thẩm quyền mặc định của chức vụ.</summary>
    public string? DefaultApprovalAuthority { get; set; }

    /// <summary>Tổ chức Đảng nơi giữ chức vụ.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Tên tổ chức Đảng.</summary>
    public string? PartyCellName { get; set; }

    /// <summary>Đơn vị chính quyền nơi giữ chức vụ.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Tên đơn vị chính quyền.</summary>
    public string? DepartmentName { get; set; }

    /// <summary>Chức vụ chính.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Hiệu lực từ (UTC, bao gồm).</summary>
    public DateTime ValidFrom { get; set; }

    /// <summary>Hiệu lực đến (UTC, không bao gồm).</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Đang hiệu lực tại thời điểm truy vấn.</summary>
    public bool IsEffective { get; set; }

    /// <summary>Ghi chú.</summary>
    public string? Note { get; set; }
}

/// <summary>
/// Thêm/sửa chức vụ của cán bộ. Chỉ một trong <see cref="PartyCellId"/> / <see cref="DepartmentId"/> (có thể cả hai trống).
/// Cập nhật: trường null giữ nguyên; <c>Guid.Empty</c> ở đơn vị → bỏ đơn vị.
/// </summary>
public class SaveMemberPositionDto
{
    /// <summary>Chức vụ trong danh mục (bắt buộc khi thêm; không đổi khi sửa).</summary>
    public Guid? PositionId { get; set; }

    /// <summary>Tổ chức Đảng nơi giữ chức vụ.</summary>
    public Guid? PartyCellId { get; set; }

    /// <summary>Đơn vị chính quyền nơi giữ chức vụ.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Chức vụ chính (bỏ chức vụ chính cũ của người đó).</summary>
    public bool? IsPrimary { get; set; }

    /// <summary>Hiệu lực từ (mặc định: bây giờ).</summary>
    public DateTime? ValidFrom { get; set; }

    /// <summary>Hiệu lực đến (không bao gồm); null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Khi sửa: true → bỏ thời hạn (ValidTo = null).</summary>
    public bool? ClearValidTo { get; set; }

    /// <summary>Ghi chú (số quyết định…).</summary>
    public string? Note { get; set; }
}

/// <summary>Thẩm quyền phê duyệt của cán bộ (<c>GET /api/users/{id}/approval-authority</c>).</summary>
public class ApprovalAuthorityInfoDto
{
    /// <summary>Thẩm quyền đang áp dụng (<c>CoSo</c>/<c>CapTren</c>).</summary>
    public string Effective { get; set; } = string.Empty;

    /// <summary>Thẩm quyền suy ra từ chức vụ đang hiệu lực.</summary>
    public string Derived { get; set; } = string.Empty;

    /// <summary>Thẩm quyền đặt tay (null = không ghi đè).</summary>
    public string? Override { get; set; }

    /// <summary>Lý do đặt tay.</summary>
    public string? OverrideReason { get; set; }

    /// <summary>Các chức vụ đang hiệu lực làm căn cứ suy ra CapTren.</summary>
    public List<string> CapTrenPositions { get; set; } = new();

    /// <summary>Mã chức danh thống kê của người (mã nhỏ nhất trong các chức vụ đang hiệu lực).</summary>
    public string? StatCode { get; set; }
}

/// <summary>Đặt tay / bỏ đặt tay thẩm quyền phê duyệt (<c>PUT /api/users/{id}/approval-authority</c>).</summary>
public class SetApprovalAuthorityOverrideDto
{
    /// <summary><c>CoSo</c> / <c>CapTren</c>; null hoặc rỗng → bỏ ghi đè (quay về suy ra từ chức vụ).</summary>
    public string? Override { get; set; }

    /// <summary>Lý do (bắt buộc khi đặt tay).</summary>
    public string? Reason { get; set; }
}
