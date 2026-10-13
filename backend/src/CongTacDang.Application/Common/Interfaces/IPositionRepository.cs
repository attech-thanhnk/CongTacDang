using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Repository danh mục chức vụ (<c>positions</c>) và chức vụ của cán bộ (<c>member_positions</c>) — task 14.
/// Thao tác ghi chỉ đưa vào DbContext; lưu qua <see cref="SaveChangesAsync"/>.
/// </summary>
public interface IPositionRepository
{
    /// <summary>Chức vụ chưa xóa (không theo dõi), sắp theo thứ tự rồi tên.</summary>
    Task<List<Position>> ListPositionsAsync(CancellationToken ct = default);

    /// <summary>Kiểu trình bày biểu mẫu chưa xóa có tồn tại.</summary>
    Task<bool> FormLayoutExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chức vụ chưa xóa theo Id (được theo dõi).</summary>
    Task<Position?> FindPositionAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đã có chức vụ chưa xóa cùng tên (không phân biệt hoa thường), trừ <paramref name="excludeId"/>.</summary>
    Task<bool> PositionNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Số người đang giữ (bản ghi đang hiệu lực tại <paramref name="now"/>) theo từng chức vụ.</summary>
    Task<Dictionary<Guid, int>> CountHoldersAsync(DateTime now, CancellationToken ct = default);

    /// <summary>Số bản ghi chức vụ của cán bộ (chưa xóa, kể cả đã kết thúc) tham chiếu tới chức vụ.</summary>
    Task<int> CountMemberPositionsAsync(Guid positionId, CancellationToken ct = default);

    /// <summary>Đưa chức vụ mới vào DbContext.</summary>
    void AddPosition(Position position);

    /// <summary>Đánh dấu xóa chức vụ (xóa mềm khi lưu).</summary>
    void RemovePosition(Position position);

    /// <summary>Chức vụ (chưa xóa) của một cán bộ kèm chức vụ, đơn vị (không theo dõi), mới nhất trước.</summary>
    Task<List<MemberPosition>> ListMemberPositionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Chức vụ (chưa xóa) của một cán bộ kèm chức vụ (được theo dõi) — để sửa và tính lại thẩm quyền.</summary>
    Task<List<MemberPosition>> ListMemberPositionsForUpdateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Bản ghi chức vụ của cán bộ theo Id (được theo dõi, kèm chức vụ).</summary>
    Task<MemberPosition?> FindMemberPositionAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đưa bản ghi chức vụ mới vào DbContext.</summary>
    void AddMemberPosition(MemberPosition memberPosition);

    /// <summary>Đánh dấu xóa bản ghi chức vụ (xóa mềm khi lưu).</summary>
    void RemoveMemberPosition(MemberPosition memberPosition);

    /// <summary>Chức vụ đang hiệu lực tại <paramref name="at"/> của các cán bộ (null = mọi cán bộ).</summary>
    Task<Dictionary<Guid, List<HeldPosition>>> GetHeldPositionsAsync(IReadOnlyCollection<Guid>? userIds, DateTime at, CancellationToken ct = default);

    /// <summary>Cán bộ (chưa xóa) có ít nhất một bản ghi chức vụ và không đặt tay thẩm quyền.</summary>
    Task<List<PartyMemberProfile>> ListMembersWithDerivedAuthorityAsync(CancellationToken ct = default);

    /// <summary>Hồ sơ cán bộ chưa xóa (được theo dõi).</summary>
    Task<PartyMemberProfile?> FindMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tổ chức Đảng tồn tại, chưa xóa, đang hoạt động.</summary>
    Task<bool> PartyCellIsActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đơn vị chính quyền tồn tại, chưa xóa, đang hoạt động.</summary>
    Task<bool> DepartmentIsActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Chức vụ đang hiệu lực tại <paramref name="at"/> kèm cán bộ (chưa xóa) và đơn vị giữ chức vụ (không theo dõi).
    /// Lọc theo cán bộ (<paramref name="userIds"/>) và/hoặc đúng đơn vị chính quyền / tổ chức Đảng; null = không lọc.
    /// </summary>
    Task<List<PositionHolding>> ListActiveHoldingsAsync(
        IReadOnlyCollection<Guid>? userIds, Guid? departmentId, Guid? partyCellId, DateTime at, CancellationToken ct = default);

    /// <summary>Đơn vị chính quyền và tổ chức Đảng của các cán bộ (chưa xóa) theo Id.</summary>
    Task<Dictionary<Guid, MemberUnits>> GetMemberUnitsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>Đường dẫn cây (<c>Path</c>) của mọi đơn vị chính quyền và tổ chức Đảng (kể cả đã ngừng hoạt động).</summary>
    Task<OrgPaths> GetOrgPathsAsync(CancellationToken ct = default);

    /// <summary>Lưu thay đổi (audit tự động qua DbContext).</summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Một chức vụ đang hiệu lực của cán bộ tại một đơn vị.</summary>
public sealed record PositionHolding(
    Guid UserId,
    string FullName,
    string Username,
    bool UserActive,
    string PositionName,
    int PositionSortOrder,
    Guid? DepartmentId,
    Guid? PartyCellId);

/// <summary>Đơn vị chính quyền và tổ chức Đảng (nơi sinh hoạt) của một cán bộ.</summary>
public sealed record MemberUnits(Guid? DepartmentId, Guid? PartyCellId);

/// <summary>Đường dẫn cây theo Id của đơn vị chính quyền và tổ chức Đảng.</summary>
public sealed record OrgPaths(IReadOnlyDictionary<Guid, string> Departments, IReadOnlyDictionary<Guid, string> PartyCells);
