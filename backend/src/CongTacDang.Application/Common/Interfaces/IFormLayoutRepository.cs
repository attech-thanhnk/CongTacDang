using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Truy cập danh mục kiểu trình bày biểu mẫu (<c>form_layouts</c>) và kiểu áp dụng cho cán bộ.</summary>
public interface IFormLayoutRepository
{
    /// <summary>Chưa xóa, không theo dõi; theo thứ tự rồi tên.</summary>
    Task<List<FormLayout>> ListAsync(CancellationToken ct = default);

    /// <summary>Được theo dõi.</summary>
    Task<FormLayout?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Không phân biệt hoa thường.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Được theo dõi.</summary>
    Task<List<FormLayout>> ListDefaultsAsync(CancellationToken ct = default);

    /// <summary>Số chức vụ gắn từng kiểu.</summary>
    Task<Dictionary<Guid, int>> CountPositionsAsync(CancellationToken ct = default);

    /// <summary>Số cán bộ đặt riêng từng kiểu.</summary>
    Task<Dictionary<Guid, int>> CountMembersAsync(CancellationToken ct = default);

    /// <summary>Chưa xóa, được theo dõi, kèm kiểu đặt riêng.</summary>
    Task<PartyMemberProfile?> FindMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Kèm chức vụ và kiểu của chức vụ; không theo dõi.</summary>
    Task<List<MemberPosition>> ListHeldPositionsAsync(Guid userId, DateTime at, CancellationToken ct = default);

    void Add(FormLayout layout);

    void Remove(FormLayout layout);
}
