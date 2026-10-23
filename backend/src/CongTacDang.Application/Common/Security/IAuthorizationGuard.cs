using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Security;

/// <summary>Đối tượng cần kiểm tra quyền.</summary>
/// <param name="OwnerId">Người được đánh giá / người tải tệp / chủ tài khoản.</param>
/// <param name="DepartmentId">Với hồ sơ: ảnh chụp trên EvaluationRecord.</param>
public sealed record AccessTarget(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null)
{
    /// <summary>Chỉ phạm vi Global thỏa.</summary>
    public static AccessTarget None { get; } = new();

    /// <summary>Dùng các trường ảnh chụp trên hồ sơ.</summary>
    public static AccessTarget ForRecord(EvaluationRecord record) =>
        new(record.MemberId, record.DepartmentId, record.PartyCellId);

    /// <summary>
    /// Chế độ ghi: <b>mọi</b> đơn vị khác null phải nằm trong phạm vi người thao tác.
    /// Mặc định (đọc) chỉ cần một đơn vị.
    /// </summary>
    public bool RequireAllUnits { get; init; }

    public AccessTarget ForWrite() => this with { RequireAllUnits = true };

    public static AccessTarget ForUnitsWrite(Guid? departmentId, Guid? partyCellId) =>
        new AccessTarget(DepartmentId: departmentId, PartyCellId: partyCellId).ForWrite();
}

/// <summary>
/// Bộ lọc phạm vi để service dựng điều kiện WHERE cho truy vấn danh sách:
/// <c>IsGlobal</c> OR <c>DepartmentId IN DepartmentIds</c> OR <c>PartyCellId IN PartyCellIds</c> OR <c>OwnerId = OwnerId</c>.
/// </summary>
/// <param name="DepartmentIds">Đã mở rộng xuống mọi đơn vị con cháu của nút được gán.</param>
/// <param name="PartyCellIds">Đã mở rộng xuống mọi tổ chức con cháu của nút được gán.</param>
/// <param name="OwnerId">Luôn thấy đối tượng của chủ này (vd. chủ hồ sơ luôn xem được hồ sơ của mình).</param>
public sealed record ScopeFilter(
    bool IsGlobal,
    IReadOnlyList<Guid> DepartmentIds,
    IReadOnlyList<Guid> PartyCellIds,
    Guid? OwnerId = null)
{
    public static ScopeFilter None { get; } = new(false, Array.Empty<Guid>(), Array.Empty<Guid>());

    public static ScopeFilter Global { get; } = new(true, Array.Empty<Guid>(), Array.Empty<Guid>());

    public static ScopeFilter OwnerOnly(Guid ownerId) => new(false, Array.Empty<Guid>(), Array.Empty<Guid>(), ownerId);

    public bool IsEmpty => !IsGlobal && DepartmentIds.Count == 0 && PartyCellIds.Count == 0 && OwnerId == null;

    /// <summary>Lọc danh sách trong bộ nhớ.</summary>
    public bool Matches(Guid? ownerId, Guid? departmentId, Guid? partyCellId)
    {
        if (IsGlobal)
            return true;
        if (OwnerId.HasValue && ownerId == OwnerId)
            return true;
        if (departmentId.HasValue && DepartmentIds.Contains(departmentId.Value))
            return true;
        return partyCellId.HasValue && PartyCellIds.Contains(partyCellId.Value);
    }

    /// <summary>Hợp hai bộ lọc; chủ hồ sơ lấy theo bộ lọc đầu nếu có.</summary>
    public ScopeFilter Union(ScopeFilter other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new ScopeFilter(
            IsGlobal || other.IsGlobal,
            DepartmentIds.Concat(other.DepartmentIds).Distinct().ToList(),
            PartyCellIds.Concat(other.PartyCellIds).Distinct().ToList(),
            OwnerId ?? other.OwnerId);
    }
}

/// <summary>
/// Điểm kiểm tra quyền theo đối tượng cho người dùng của request hiện tại.
/// [RequirePermission] chỉ xét "có quyền ở phạm vi nào đó"; service bắt buộc gọi <see cref="Ensure"/>/<see cref="GetScope"/>.
/// </summary>
public interface IAuthorizationGuard
{
    bool Can(string permission, AccessTarget target);

    /// <summary>Ném <see cref="Exceptions.ForbiddenException"/> (403) nếu không được phép.</summary>
    void Ensure(string permission, AccessTarget target);

    /// <summary>Có quyền ở phạm vi bất kỳ (dùng cho policy và menu).</summary>
    bool HasAny(string permission);

    /// <summary>Bộ lọc cho truy vấn danh sách.</summary>
    ScopeFilter GetScope(string permission);
}
