using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Danh mục kiểu trình bày biểu mẫu: kiểm tra đầu vào, một kiểu mặc định, không xóa kiểu đang dùng, kiểu của cán bộ.</summary>
public class FormLayoutServiceTests
{
    private sealed class FakeRepo : IFormLayoutRepository
    {
        public List<FormLayout> Items { get; } = new();
        public List<Position> Positions { get; } = new();
        public List<PartyMemberProfile> Members { get; } = new();
        public List<MemberPosition> Held { get; } = new();

        public Task<List<FormLayout>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult(Items.OrderBy(l => l.SortOrder).ThenBy(l => l.Name).ToList());

        public Task<FormLayout?> FindAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(l => l.Id == id));

        public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default) =>
            Task.FromResult(Items.Any(l => string.Equals(l.Name, name.Trim(), StringComparison.OrdinalIgnoreCase) && l.Id != excludeId));

        public Task<List<FormLayout>> ListDefaultsAsync(CancellationToken ct = default) => Task.FromResult(Items.Where(l => l.IsDefault).ToList());

        public Task<Dictionary<Guid, int>> CountPositionsAsync(CancellationToken ct = default) =>
            Task.FromResult(Positions.Where(p => p.FormLayoutId != null).GroupBy(p => p.FormLayoutId!.Value).ToDictionary(g => g.Key, g => g.Count()));

        public Task<Dictionary<Guid, int>> CountMembersAsync(CancellationToken ct = default) =>
            Task.FromResult(Members.Where(m => m.FormLayoutOverrideId != null).GroupBy(m => m.FormLayoutOverrideId!.Value).ToDictionary(g => g.Key, g => g.Count()));

        public Task<PartyMemberProfile?> FindMemberAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Members.FirstOrDefault(m => m.Id == userId));

        public Task<List<MemberPosition>> ListHeldPositionsAsync(Guid userId, DateTime at, CancellationToken ct = default) =>
            Task.FromResult(Held.Where(mp => mp.UserId == userId).ToList());

        public void Add(FormLayout layout) => Items.Add(layout);

        public void Remove(FormLayout layout) => Items.Remove(layout);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();

        public void SetOriginalVersion(object entity, uint? version) { }

        public uint GetVersion(object entity) => 0;
    }

    private sealed class FakeGuard : IAuthorizationGuard
    {
        private readonly bool _allow;

        public FakeGuard(bool allow = true) => _allow = allow;

        public bool HasAny(string permission) => _allow;
        public bool Can(string permission, AccessTarget target) => _allow;
        public void Ensure(string permission, AccessTarget target)
        {
            if (!_allow)
                throw new ForbiddenException(permission);
        }
        public ScopeFilter GetScope(string permission) => _allow ? ScopeFilter.Global : ScopeFilter.None;
    }

    private static (FormLayoutService Service, FakeRepo Repo) Create(bool allow = true)
    {
        var repo = new FakeRepo();
        return (new FormLayoutService(repo, new FakeUnitOfWork(), new FakeGuard(allow)), repo);
    }

    private static SaveFormLayoutDto Input(string name, bool isDefault = false) => new()
    {
        Name = name, IsDefault = isDefault
    };

    [Fact]
    public async Task Create_Validates_AndKeepsSingleDefault()
    {
        var (service, repo) = Create();
        var first = await service.CreateAsync(Input(" Cán bộ phòng ban ", isDefault: true));
        Assert.Equal("Cán bộ phòng ban", first.Name);

        var second = await service.CreateAsync(new SaveFormLayoutDto
        {
            Name = "Bí thư Đảng ủy", SingleLinePositions = true, EmptyPositionText = " không ", IsDefault = true
        });
        Assert.Equal((true, "không"), (second.SingleLinePositions, second.EmptyPositionText));
        Assert.Single(repo.Items, l => l.IsDefault);
        Assert.True(repo.Items.Single(l => l.Id == second.Id).IsDefault);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(Input("cán bộ PHÒNG BAN")));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(Input(" ")));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new SaveFormLayoutDto { Name = "X", EmptyPositionText = new string('x', 51) }));
    }

    [Fact]
    public async Task Delete_InUse_Conflict()
    {
        var (service, repo) = Create();
        var layout = await service.CreateAsync(Input("Lãnh đạo Công ty"));
        repo.Positions.Add(new Position { Name = "Giám đốc", FormLayoutId = layout.Id });

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(layout.Id));
        repo.Positions.Clear();
        await service.DeleteAsync(layout.Id);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task MemberLayout_FromPositions_OverrideAndScope()
    {
        var (service, repo) = Create();
        var secretary = new FormLayout { Name = "Bí thư Đảng ủy", SortOrder = 10 };
        var leader = new FormLayout { Name = "Lãnh đạo Công ty", SortOrder = 20 };
        var staff = new FormLayout { Name = "Cán bộ phòng ban", SortOrder = 30, IsDefault = true };
        repo.Items.AddRange(new[] { secretary, leader, staff });
        var member = new PartyMemberProfile { FullName = "Chủ tịch" };
        repo.Members.Add(member);
        repo.Held.Add(new MemberPosition { UserId = member.Id, Position = new Position { Name = "Chủ tịch Công ty", FormLayout = leader } });
        repo.Held.Add(new MemberPosition { UserId = member.Id, Position = new Position { Name = "Bí thư Đảng ủy", FormLayout = secretary } });

        var info = await service.GetMemberLayoutAsync(member.Id);
        Assert.Equal((secretary.Id, secretary.Id, (Guid?)null), (info.EffectiveId, info.DerivedId, info.OverrideId));
        Assert.Equal(2, info.PositionLayouts.Count);

        info = await service.SetMemberLayoutAsync(member.Id, new SetMemberFormLayoutDto { LayoutId = leader.Id });
        Assert.Equal((leader.Id, secretary.Id, (Guid?)leader.Id), (info.EffectiveId, info.DerivedId, info.OverrideId));

        info = await service.SetMemberLayoutAsync(member.Id, new SetMemberFormLayoutDto { LayoutId = null });
        Assert.Equal(secretary.Id, info.EffectiveId);
        await Assert.ThrowsAsync<ValidationException>(() => service.SetMemberLayoutAsync(member.Id, new SetMemberFormLayoutDto { LayoutId = Guid.NewGuid() }));

        // Không giữ chức vụ nào gắn kiểu → kiểu mặc định.
        repo.Held.Clear();
        Assert.Equal(staff.Id, (await service.GetMemberLayoutAsync(member.Id)).EffectiveId);

        var (denied, deniedRepo) = Create(allow: false);
        deniedRepo.Members.Add(member);
        await Assert.ThrowsAsync<ForbiddenException>(() => denied.SetMemberLayoutAsync(member.Id, new SetMemberFormLayoutDto()));
    }
}
