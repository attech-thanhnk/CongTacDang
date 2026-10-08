using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Truy cập danh mục kiểu trình bày biểu mẫu (<c>form_layouts</c>) và kiểu áp dụng cho cán bộ.</summary>
public interface IFormLayoutRepository
{
    /// <summary>Kiểu chưa xóa (không theo dõi), theo thứ tự rồi tên.</summary>
    Task<List<FormLayout>> ListAsync(CancellationToken ct = default);

    /// <summary>Theo Id (được theo dõi).</summary>
    Task<FormLayout?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Đã có kiểu cùng tên (không phân biệt hoa thường), trừ <paramref name="excludeId"/>.</summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Các kiểu đang là mặc định (được theo dõi).</summary>
    Task<List<FormLayout>> ListDefaultsAsync(CancellationToken ct = default);

    /// <summary>Số chức vụ gắn từng kiểu.</summary>
    Task<Dictionary<Guid, int>> CountPositionsAsync(CancellationToken ct = default);

    /// <summary>Số cán bộ đặt riêng từng kiểu.</summary>
    Task<Dictionary<Guid, int>> CountMembersAsync(CancellationToken ct = default);

    /// <summary>Cán bộ chưa xóa theo Id (được theo dõi), kèm kiểu đặt riêng.</summary>
    Task<PartyMemberProfile?> FindMemberAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Chức vụ cán bộ đang giữ tại thời điểm <paramref name="at"/> (kèm chức vụ và kiểu của chức vụ, không theo dõi).</summary>
    Task<List<MemberPosition>> ListHeldPositionsAsync(Guid userId, DateTime at, CancellationToken ct = default);

    void Add(FormLayout layout);

    void Remove(FormLayout layout);
}
