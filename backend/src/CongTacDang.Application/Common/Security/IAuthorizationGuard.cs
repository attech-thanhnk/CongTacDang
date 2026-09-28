using System;
using System.Collections.Generic;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Đối tượng cần kiểm tra quyền (docs/thiet-ke/phan-quyen.md mục 4).
/// </summary>
/// <param name="OwnerId">Chủ hồ sơ (người được đánh giá / người tải tệp / chủ tài khoản).</param>
/// <param name="DepartmentId">Phòng của hồ sơ (ảnh chụp trên EvaluationRecord).</param>
/// <param name="PartyCellId">Chi bộ của hồ sơ.</param>
/// <param name="ApprovalAuthority">Cấp có thẩm quyền quyết định (CoSo / CapTren).</param>
public sealed record AccessTarget(
    Guid? OwnerId = null,
    Guid? DepartmentId = null,
    Guid? PartyCellId = null,
    ApprovalAuthority? ApprovalAuthority = null)
{
    /// <summary>Không gắn đối tượng cụ thể (chỉ xét phạm vi Global).</summary>
    public static AccessTarget None { get; } = new();

    /// <summary>Dựng đối tượng kiểm tra từ hồ sơ đánh giá (dùng các trường ảnh chụp trên hồ sơ).</summary>
    public static AccessTarget ForRecord(EvaluationRecord record) =>
        new(record.MemberId, record.DepartmentId, record.PartyCellId, record.ApprovalAuthority);
}

/// <summary>
/// Bộ lọc phạm vi để service dựng điều kiện WHERE cho truy vấn danh sách:
/// <c>IsGlobal</c> OR <c>DepartmentId IN DepartmentIds</c> OR <c>PartyCellId IN PartyCellIds</c> OR <c>OwnerId = OwnerId</c>.
/// </summary>
/// <param name="IsGlobal">Được thấy mọi đối tượng.</param>
/// <param name="DepartmentIds">Các Phòng được thấy.</param>
/// <param name="PartyCellIds">Các Chi bộ được thấy.</param>
/// <param name="OwnerId">Luôn thấy đối tượng của chủ này (vd. <c>evaluation.read</c>: chủ hồ sơ luôn xem được hồ sơ của mình).</param>
public sealed record ScopeFilter(
    bool IsGlobal,
    IReadOnlyList<Guid> DepartmentIds,
    IReadOnlyList<Guid> PartyCellIds,
    Guid? OwnerId = null)
{
    /// <summary>Không thấy gì.</summary>
    public static ScopeFilter None { get; } = new(false, Array.Empty<Guid>(), Array.Empty<Guid>());

    /// <summary>Thấy mọi đối tượng.</summary>
    public static ScopeFilter Global { get; } = new(true, Array.Empty<Guid>(), Array.Empty<Guid>());

    /// <summary>Chỉ thấy đối tượng của một chủ.</summary>
    public static ScopeFilter OwnerOnly(Guid ownerId) => new(false, Array.Empty<Guid>(), Array.Empty<Guid>(), ownerId);

    /// <summary>Bộ lọc không cho thấy đối tượng nào.</summary>
    public bool IsEmpty => !IsGlobal && DepartmentIds.Count == 0 && PartyCellIds.Count == 0 && OwnerId == null;
}

/// <summary>
/// Điểm kiểm tra quyền duy nhất theo đối tượng cho người dùng của request hiện tại
/// (docs/thiet-ke/phan-quyen.md mục 4). Controller dùng [RequirePermission] = "có quyền ở phạm vi nào đó";
/// service bắt buộc gọi <see cref="Ensure"/>/<see cref="GetScope"/> trên đối tượng cụ thể.
/// </summary>
public interface IAuthorizationGuard
{
    /// <summary>Người dùng hiện tại được thực hiện <paramref name="permission"/> trên <paramref name="target"/>.</summary>
    bool Can(string permission, AccessTarget target);

    /// <summary>Như <see cref="Can"/> nhưng ném <see cref="Exceptions.ForbiddenException"/> (403) nếu không được phép.</summary>
    void Ensure(string permission, AccessTarget target);

    /// <summary>Người dùng hiện tại có <paramref name="permission"/> ở phạm vi bất kỳ (dùng cho policy và menu).</summary>
    bool HasAny(string permission);

    /// <summary>Bộ lọc phạm vi của <paramref name="permission"/> cho truy vấn danh sách.</summary>
    ScopeFilter GetScope(string permission);
}
