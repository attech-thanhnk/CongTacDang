using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Quyền hạn nguyên tử (Atomic Permission) trong hệ thống
/// Ví dụ: users.read, branches.create, attachments.delete
/// </summary>
public class Permission : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Mã định danh duy nhất của quyền (VD: users.read)</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị của quyền</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Tài nguyên áp dụng (users, branches, attachments, reports, roles)</summary>
    public string Resource { get; set; } = string.Empty;

    /// <summary>Hành động (read, create, update, delete, upload, export, manage)</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Mô tả chi tiết mục đích của quyền</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Phân hệ (system, catalog, period, evaluation, collective, meeting, report, attachment) — đồng bộ từ PermissionCodes.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Thứ tự hiển thị — đồng bộ từ PermissionCodes.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách các vai trò được cấp quyền này</summary>
    public ICollection<AppRole> Roles { get; set; } = new List<AppRole>();
}
