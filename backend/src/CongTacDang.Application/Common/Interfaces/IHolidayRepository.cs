using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

public interface IHolidayRepository
{
    /// <summary>Khoảng đóng, ngày tăng dần; không theo dõi.</summary>
    Task<List<Holiday>> ListAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Được theo dõi.</summary>
    Task<Holiday?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Ngày đã có trong khoảng đóng [<paramref name="from"/>, <paramref name="to"/>].</summary>
    Task<HashSet<DateOnly>> ExistingDatesAsync(DateOnly from, DateOnly to, Guid? excludeId = null, CancellationToken ct = default);

    void Add(Holiday holiday);

    void Remove(Holiday holiday);
}
