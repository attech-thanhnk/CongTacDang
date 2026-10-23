using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

/// <summary>Quyền trong danh mục cố định <c>PermissionCodes</c>, được seeder đồng bộ vào CSDL.</summary>
public class Permission : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Ví dụ <c>evaluation.read</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Phân hệ, đồng bộ từ PermissionCodes.</summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>Đồng bộ từ PermissionCodes.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public ICollection<AppRole> Roles { get; set; } = new List<AppRole>();
}
