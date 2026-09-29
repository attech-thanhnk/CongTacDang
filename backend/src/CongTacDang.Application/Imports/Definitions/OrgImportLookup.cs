using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Imports.Definitions;

/// <summary>
/// Tra cứu chỉ đọc phục vụ import mô hình tổ chức (task 14): loại đơn vị, chức vụ, cán bộ kèm đơn vị.
/// Đơn vị (tổ chức Đảng / đơn vị chính quyền) tra qua <see cref="IImportLookup"/> của khung import.
/// </summary>
public interface IOrgImportLookup
{
    /// <summary>Loại đơn vị chưa xóa.</summary>
    Task<IReadOnlyList<UnitTypeLookupEntry>> GetUnitTypesAsync(CancellationToken ct);

    /// <summary>Chức vụ chưa xóa.</summary>
    Task<IReadOnlyList<PositionLookupEntry>> GetPositionsAsync(CancellationToken ct);

    /// <summary>Cán bộ có tên đăng nhập (không phân biệt hoa thường) trong <paramref name="usernames"/>, kể cả đã xóa mềm.</summary>
    Task<IReadOnlyList<MemberLookupEntry>> FindMembersAsync(IReadOnlyCollection<string> usernames, CancellationToken ct);
}

/// <summary>Loại đơn vị phục vụ tra cứu.</summary>
public sealed record UnitTypeLookupEntry(Guid Id, string Name, OrgSide Side, bool IsActive);

/// <summary>Chức vụ phục vụ tra cứu.</summary>
public sealed record PositionLookupEntry(Guid Id, string Name, PositionSide Side, bool IsActive);

/// <summary>Cán bộ phục vụ tra cứu (đơn vị để kiểm tra phạm vi quyền của người nhập).</summary>
public sealed record MemberLookupEntry(Guid Id, string Username, string FullName, Guid? DepartmentId, Guid? PartyCellId, bool IsDeleted);
