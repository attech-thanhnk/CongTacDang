using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Truy cập danh mục ngày nghỉ.</summary>
public interface IHolidayRepository
{
    /// <summary>Ngày nghỉ trong khoảng [<paramref name="from"/>, <paramref name="to"/>], theo ngày tăng dần (không theo dõi thay đổi).</summary>
    Task<List<Holiday>> ListAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Tìm theo mã (có theo dõi thay đổi).</summary>
    Task<Holiday?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Các ngày đã có trong danh mục thuộc khoảng [<paramref name="from"/>, <paramref name="to"/>].</summary>
    Task<HashSet<DateOnly>> ExistingDatesAsync(DateOnly from, DateOnly to, Guid? excludeId = null, CancellationToken ct = default);

    void Add(Holiday holiday);

    void Remove(Holiday holiday);
}
