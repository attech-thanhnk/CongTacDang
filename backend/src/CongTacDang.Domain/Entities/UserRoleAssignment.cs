using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>Gán vai trò cho người dùng kèm phạm vi và thời hạn (xem docs/thiet-ke/phan-quyen.md).</summary>
public class UserRoleAssignment : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public PartyMemberProfile? User { get; set; }

    public Guid RoleId { get; set; }

    public AppRole? Role { get; set; }

    public RoleScopeType ScopeType { get; set; } = RoleScopeType.Global;

    /// <summary>Id đơn vị hoặc tổ chức Đảng; null khi <see cref="RoleScopeType.Global"/>.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>UTC, bao gồm.</summary>
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    /// <summary>UTC, không bao gồm; null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Căn cứ quyết định, lý do…</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Chưa xóa và <c>ValidFrom ≤ now &lt; ValidTo</c> (hoặc ValidTo null).</summary>
    public bool IsEffectiveAt(DateTime now) =>
        !IsDeleted && ValidFrom <= now && (ValidTo == null || now < ValidTo.Value);

    /// <summary>Đang hiệu lực hoặc sẽ hiệu lực.</summary>
    public bool IsCurrentOrFutureAt(DateTime now) =>
        !IsDeleted && (ValidTo == null || now < ValidTo.Value);
}
