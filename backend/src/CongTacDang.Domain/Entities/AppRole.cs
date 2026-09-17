using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Vai trò người dùng trong hệ thống (Role-Based Access Control)
/// Hỗ trợ cấu hình động quyền hạn thông qua CSDL
/// </summary>
public class AppRole
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã vai trò duy nhất (VD: CAN_BO, BI_THU_CHI_BO, BAN_THUONG_VU)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị vai trò</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả vai trò</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Vai trò hệ thống (không thể xóa nếu là true)</summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>Danh sách quyền hạn được gán cho vai trò này</summary>
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    /// <summary>Danh sách cán bộ mang vai trò này</summary>
    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}
