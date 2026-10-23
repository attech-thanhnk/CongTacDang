using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

public class AppRole : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Không dùng để phân quyền; chỉ để seeder tìm vai trò mặc định. Vai trò tạo qua API được sinh mã tự động.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Do seeder tạo; chỉ để hiển thị.</summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>Không xóa được, không gỡ được quyền <c>system.roles.manage</c>/<c>system.assignments.manage</c>.</summary>
    public bool IsProtected { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
