using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>Điều kiện tra cứu bản gán (API <c>GET /api/admin/assignments</c>).</summary>
/// <param name="UserId">Lọc theo người.</param>
/// <param name="RoleId">Lọc theo vai trò.</param>
/// <param name="ScopeType">Lọc theo loại phạm vi.</param>
/// <param name="ScopeId">Lọc theo Phòng/Chi bộ.</param>
/// <param name="ActiveOn">Chỉ lấy bản gán hiệu lực tại thời điểm này.</param>
public sealed record RoleAssignmentQuery(
    Guid? UserId = null,
    Guid? RoleId = null,
    ScopeType? ScopeType = null,
    Guid? ScopeId = null,
    DateTime? ActiveOn = null);

/// <summary>
/// Gán / sửa / kết thúc / xóa bản gán vai trò kèm phạm vi và thời hạn, tra cứu quyền hiệu lực
/// (docs/thiet-ke/phan-quyen.md mục 2, 5). Mọi thay đổi xóa cache quyền của người liên quan ngay.
/// <para>
/// Lỗi nghiệp vụ: <see cref="Common.Exceptions.ValidationException"/> (400, thông báo tiếng Việt) cho dữ liệu không hợp lệ
/// (vai trò/người/phạm vi không tồn tại, phạm vi sai, thời hạn sai, trùng bản gán);
/// <see cref="Common.Exceptions.ForbiddenException"/> (403) khi tự gán cho chính mình hoặc thiếu quyền;
/// <see cref="Common.Exceptions.ConflictException"/> (409) khi làm mất quản trị cuối cùng;
/// <see cref="Common.Exceptions.NotFoundException"/> (404) khi không tìm thấy bản gán.
/// </para>
/// </summary>
public interface IRoleAssignmentService
{
    /// <summary>
    /// Gán vai trò <paramref name="roleId"/> cho <paramref name="userId"/> ở phạm vi cho trước.
    /// <paramref name="validFrom"/> null = ngay bây giờ; <paramref name="validTo"/> null = không thời hạn (không bao gồm mốc này).
    /// Thời gian không có múi giờ được hiểu là UTC. Dùng được trong transaction của <c>IUnitOfWork</c> (task 13 — import).
    /// </summary>
    Task<RoleAssignmentDto> AssignAsync(
        Guid userId,
        Guid roleId,
        ScopeType scopeType,
        Guid? scopeId,
        DateTime? validFrom,
        DateTime? validTo,
        string? note,
        CancellationToken ct = default);

    /// <summary>Sửa thời hạn và ghi chú của bản gán (<paramref name="validFrom"/> null = giữ nguyên).</summary>
    Task<RoleAssignmentDto> UpdateAsync(Guid assignmentId, DateTime? validFrom, DateTime? validTo, string? note, CancellationToken ct = default);

    /// <summary>Kết thúc bản gán ngay (đặt <c>ValidTo = now</c>).</summary>
    Task<RoleAssignmentDto> EndAsync(Guid assignmentId, CancellationToken ct = default);

    /// <summary>Xóa mềm bản gán.</summary>
    Task DeleteAsync(Guid assignmentId, CancellationToken ct = default);

    /// <summary>Tra cứu bản gán.</summary>
    Task<List<RoleAssignmentDto>> QueryAsync(RoleAssignmentQuery query, CancellationToken ct = default);

    /// <summary>"Người X làm được gì": quyền hiệu lực + phạm vi (kèm tên) + vai trò/bản gán nguồn.</summary>
    Task<UserEffectivePermissionsDto> GetEffectivePermissionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Quyền kèm phạm vi của một người (cho trường <c>grants</c> của <c>/api/auth/me</c> và phản hồi đăng nhập).</summary>
    Task<List<AccessGrantDto>> GetGrantsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Ném <see cref="Common.Exceptions.ConflictException"/> nếu vô hiệu hóa/xóa <paramref name="userId"/> sẽ làm mất
    /// quản trị cuối cùng (dùng cho khóa/xóa tài khoản — task 08).
    /// </summary>
    Task EnsureAdministratorsRemainWithoutUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Tương thích giao diện cũ (<c>POST /api/admin/users/{id}/roles</c>): đặt tập vai trò phạm vi Toàn công ty của người dùng —
    /// kết thúc bản gán Global đang hiệu lực của vai trò không còn trong danh sách, tạo bản gán cho vai trò mới.
    /// </summary>
    Task<List<RoleAssignmentDto>> SetGlobalRolesAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, CancellationToken ct = default);
}
