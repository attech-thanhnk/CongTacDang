using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Chức vụ và chức vụ của cán bộ. Ghi chỉ đưa vào DbContext; lưu qua <see cref="SaveChangesAsync"/>.</summary>
public interface IPositionRepository
{
    /// <summary>Chưa xóa, không theo dõi; sắp theo thứ tự rồi tên.</summary>
    Task<List<Position>> ListPositionsAsync(CancellationToken ct = default);

    Task<bool> FormLayoutExistsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chưa xóa, được theo dõi.</summary>
    Task<Position?> FindPositionAsync(Guid id, CancellationToken ct = default);

    /// <summary>Không phân biệt hoa thường.</summary>
    Task<bool> PositionNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Số người đang giữ tại <paramref name="now"/> theo từng chức vụ.</summary>
    Task<Dictionary<Guid, int>> CountHoldersAsync(DateTime now, CancellationToken ct = default);

    /// <summary>Tính cả bản ghi đã kết thúc (chưa xóa).</summary>
    Task<int> CountMemberPositionsAsync(Guid positionId, CancellationToken ct = default);

    void AddPosition(Position position);

    /// <summary>Xóa mềm khi lưu.</summary>
    void RemovePosition(Position position);

    /// <summary>Kèm chức vụ, đơn vị; không theo dõi; mới nhất trước.</summary>
    Task<List<MemberPosition>> ListMemberPositionsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Được theo dõi — để sửa và tính lại thẩm quyền.</summary>
    Task<List<MemberPosition>> ListMemberPositionsForUpdateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Kèm chức vụ; được theo dõi.</summary>
    Task<MemberPosition?> FindMemberPositionAsync(Guid id, CancellationToken ct = default);

    void AddMemberPosition(MemberPosition memberPosition);

    /// <summary>Xóa mềm khi lưu.</summary>
    void RemoveMemberPosition(MemberPosition memberPosition);

    /// <summary>Chức vụ hiệu lực tại <paramref name="at"/>; <paramref name="userIds"/> null = mọi cán bộ.</summary>
    Task<Dictionary<Guid, List<HeldPosition>>> GetHeldPositionsAsync(IReadOnlyCollection<Guid>? userIds, DateTime at, CancellationToken ct = default);

    /// <summary>Cán bộ có ít nhất một chức vụ và không đặt tay thẩm quyền.</summary>
    Task<List<PartyMemberProfile>> ListMembersWithDerivedAuthorityAsync(CancellationToken ct = default);

    /// <summary>Chưa xóa, được theo dõi.</summary>
    Task<PartyMemberProfile?> FindMemberAsync(Guid userId, CancellationToken ct = default);

    Task<bool> PartyCellIsActiveAsync(Guid id, CancellationToken ct = default);

    Task<bool> DepartmentIsActiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>Chức vụ hiệu lực tại <paramref name="at"/>; các bộ lọc null = không lọc, đơn vị so khớp đúng (không gồm con).</summary>
    Task<List<PositionHolding>> ListActiveHoldingsAsync(
        IReadOnlyCollection<Guid>? userIds, Guid? departmentId, Guid? partyCellId, DateTime at, CancellationToken ct = default);

    Task<Dictionary<Guid, MemberUnits>> GetMemberUnitsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default);

    /// <summary>Kể cả đơn vị đã ngừng hoạt động.</summary>
    Task<OrgPaths> GetOrgPathsAsync(CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary><c>PositionName</c> là chức danh hiển thị, có tiền tố "Quyền " nếu giữ dạng quyền.</summary>
public sealed record PositionHolding(
    Guid UserId,
    string FullName,
    string Username,
    bool UserActive,
    string PositionName,
    int PositionSortOrder,
    Guid? DepartmentId,
    Guid? PartyCellId);

/// <summary>PartyCellId là nơi sinh hoạt Đảng.</summary>
public sealed record MemberUnits(Guid? DepartmentId, Guid? PartyCellId);

public sealed record OrgPaths(IReadOnlyDictionary<Guid, string> Departments, IReadOnlyDictionary<Guid, string> PartyCells);
