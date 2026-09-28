using System;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Một lần gán vai trò cho người dùng kèm phạm vi và thời hạn: ai — vai trò gì — ở đâu — từ ngày, đến ngày
/// (docs/thiet-ke/phan-quyen.md mục 2). Thay bảng nhiều-nhiều user ↔ role cũ.
/// </summary>
public class UserRoleAssignment : IAuditableEntity, ISoftDeletable
{
    /// <summary>Mã bản gán.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Người được gán.</summary>
    public Guid UserId { get; set; }

    /// <summary>Người được gán (navigation).</summary>
    public PartyMemberProfile? User { get; set; }

    /// <summary>Vai trò được gán.</summary>
    public Guid RoleId { get; set; }

    /// <summary>Vai trò được gán (navigation).</summary>
    public AppRole? Role { get; set; }

    /// <summary>Loại phạm vi.</summary>
    public RoleScopeType ScopeType { get; set; } = RoleScopeType.Global;

    /// <summary>Id Phòng hoặc Chi bộ; null khi <see cref="RoleScopeType.Global"/>.</summary>
    public Guid? ScopeId { get; set; }

    /// <summary>Hiệu lực từ (UTC, bao gồm).</summary>
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    /// <summary>Hiệu lực đến (UTC, không bao gồm); null = không thời hạn.</summary>
    public DateTime? ValidTo { get; set; }

    /// <summary>Ghi chú (căn cứ quyết định, lý do…).</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }

    /// <summary>Bản gán đang hiệu lực tại <paramref name="now"/>: chưa xóa và <c>ValidFrom ≤ now &lt; ValidTo</c> (hoặc ValidTo null).</summary>
    public bool IsEffectiveAt(DateTime now) =>
        !IsDeleted && ValidFrom <= now && (ValidTo == null || now < ValidTo.Value);

    /// <summary>Bản gán chưa hết hạn tại <paramref name="now"/> (đang hiệu lực hoặc sẽ hiệu lực).</summary>
    public bool IsCurrentOrFutureAt(DateTime now) =>
        !IsDeleted && (ValidTo == null || now < ValidTo.Value);
}
