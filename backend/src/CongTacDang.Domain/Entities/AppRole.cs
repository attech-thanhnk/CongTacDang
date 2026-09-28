using System;
using System.Collections.Generic;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Vai trò người dùng trong hệ thống (Role-Based Access Control)
/// Hỗ trợ cấu hình động quyền hạn thông qua CSDL
/// </summary>
public class AppRole : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Mã kỹ thuật duy nhất. <b>Không dùng để phân quyền</b> (code chỉ kiểm tra mã quyền);
    /// chỉ giữ để seeder tìm vai trò mặc định. Vai trò tạo qua API được sinh mã tự động.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị vai trò</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả vai trò</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Vai trò do seeder tạo (chỉ để hiển thị; không dùng trong luật phân quyền).</summary>
    public bool IsSystem { get; set; } = false;

    /// <summary>
    /// Vai trò được bảo vệ: không xóa được, không gỡ được quyền <c>system.roles.manage</c>/<c>system.assignments.manage</c>
    /// (docs/thiet-ke/phan-quyen.md mục 5).
    /// </summary>
    public bool IsProtected { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Danh sách quyền hạn được gán cho vai trò này</summary>
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    /// <summary>
    /// Quan hệ nhiều-nhiều user ↔ role cũ (bảng <c>user_roles</c>). <b>Không còn dùng để phân quyền</b> — thay bằng
    /// <see cref="UserRoleAssignment"/>; chỉ giữ tới khi các chỗ dùng ngoài phạm vi task 09 được gỡ.
    /// </summary>
    [Obsolete("Dùng UserRoleAssignment (bản gán có phạm vi, thời hạn). Quan hệ user_roles sẽ bị xóa.")]
    public ICollection<PartyMemberProfile> Members { get; set; } = new List<PartyMemberProfile>();
}
