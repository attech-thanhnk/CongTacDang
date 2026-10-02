using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Danh mục ngày nghỉ dùng chung: thêm theo khoảng, bỏ qua ngày trùng, kiểm tra đầu vào.</summary>
public class HolidayServiceTests
{
    private sealed class FakeRepo : IHolidayRepository
    {
        public List<Holiday> Items { get; } = new();

        public Task<List<Holiday>> ListAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date).ToList());

        public Task<Holiday?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(h => h.Id == id));

        public Task<HashSet<DateOnly>> ExistingDatesAsync(DateOnly from, DateOnly to, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(h => h.Date >= from && h.Date <= to && h.Id != excludeId).Select(h => h.Date).ToHashSet());

        public void Add(Holiday holiday) => Items.Add(holiday);

        public void Remove(Holiday holiday) => Items.Remove(holiday);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();

        public void SetOriginalVersion(object entity, uint? version) { }

        public uint GetVersion(object entity) => 0;
    }

    private static (HolidayService Service, FakeRepo Repo) Create()
    {
        var repo = new FakeRepo();
        return (new HolidayService(repo, new FakeUnitOfWork()), repo);
    }

    [Fact]
    public async Task Create_Range_AddsEachDay_AndSkipsExisting()
    {
        var (service, repo) = Create();
        repo.Items.Add(new Holiday { Date = new DateOnly(2027, 2, 8), Name = "Cũ" });

        var created = await service.CreateAsync(new SaveHolidayDto { Date = new DateOnly(2027, 2, 6), EndDate = new DateOnly(2027, 2, 10), Name = " Tết " });

        Assert.Equal(4, created.Count);
        Assert.All(created, h => Assert.Equal("Tết", h.Name));
        Assert.Equal(5, repo.Items.Count);
        Assert.Equal(5, (await service.GetByYearAsync(2027)).Count);
        Assert.Empty(await service.GetByYearAsync(2026));
    }

    [Fact]
    public async Task Create_Rejects_Duplicates_BadRange_AndBlankName()
    {
        var (service, repo) = Create();
        repo.Items.Add(new Holiday { Date = new DateOnly(2026, 9, 2), Name = "Quốc khánh" });

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new SaveHolidayDto { Date = new DateOnly(2026, 9, 2), Name = "X" }));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new SaveHolidayDto { Date = new DateOnly(2026, 9, 5), EndDate = new DateOnly(2026, 9, 3), Name = "X" }));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new SaveHolidayDto { Date = new DateOnly(2026, 1, 1), EndDate = new DateOnly(2026, 3, 1), Name = "X" }));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new SaveHolidayDto { Date = new DateOnly(2026, 9, 3), Name = "  " }));
    }

    [Fact]
    public async Task Update_And_Delete()
    {
        var (service, repo) = Create();
        var a = (await service.CreateAsync(new SaveHolidayDto { Date = new DateOnly(2026, 9, 2), Name = "A" })).Single();
        await service.CreateAsync(new SaveHolidayDto { Date = new DateOnly(2026, 9, 3), Name = "B" });

        var updated = await service.UpdateAsync(a.Id, new SaveHolidayDto { Date = new DateOnly(2026, 9, 2), Name = "Quốc khánh" });
        Assert.Equal("Quốc khánh", updated.Name);
        await Assert.ThrowsAsync<ConflictException>(() => service.UpdateAsync(a.Id, new SaveHolidayDto { Date = new DateOnly(2026, 9, 3), Name = "A" }));

        await service.DeleteAsync(a.Id);
        Assert.Single(repo.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(a.Id));
    }
}
