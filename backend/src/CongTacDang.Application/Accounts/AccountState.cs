using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Accounts;

/// <summary>Trạng thái tài khoản cần cho việc kiểm tra phiên ở mỗi request.</summary>
/// <param name="UserId">Id tài khoản.</param>
/// <param name="IsActive">Đang hoạt động (chưa bị quản trị khóa).</param>
/// <param name="IsDeleted">Đã xóa mềm.</param>
/// <param name="SecurityStamp">Dấu bảo mật hiện tại; token mang dấu khác bị từ chối.</param>
/// <param name="MustChangePassword">Đang dùng mật khẩu tạm, phải đổi trước khi dùng hệ thống.</param>
public sealed record AccountState(Guid UserId, bool IsActive, bool IsDeleted, string SecurityStamp, bool MustChangePassword)
{
    /// <summary>Phiên mang dấu bảo mật <paramref name="tokenStamp"/> còn hợp lệ không.</summary>
    public bool AcceptsSession(string? tokenStamp) =>
        IsActive && !IsDeleted
        && !string.IsNullOrEmpty(tokenStamp)
        && string.Equals(tokenStamp, SecurityStamp, StringComparison.Ordinal);
}

/// <summary>Đọc trạng thái tài khoản (có cache) và xóa cache khi tài khoản thay đổi.</summary>
public interface IAccountStateProvider
{
    /// <summary>Trạng thái hiện tại; null nếu tài khoản không tồn tại.</summary>
    Task<AccountState?> GetAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Xóa cache trạng thái của một tài khoản (gọi sau mọi thay đổi khóa/xóa/mật khẩu/dấu bảo mật).</summary>
    void Invalidate(Guid userId);
}

/// <summary>
/// Cache trạng thái tài khoản trong bộ nhớ theo UserId (singleton, một instance API).
/// TTL ngắn làm lưới an toàn khi dữ liệu bị đổi ngoài <see cref="IAccountStateProvider"/>;
/// "thế hệ" chống ghi đè cache bằng dữ liệu nạp trước lệnh xóa.
/// </summary>
public sealed class AccountStateCache
{
    /// <summary>Thời gian sống mặc định.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly TimeProvider _time;
    private readonly TimeSpan _ttl;
    private long _generation;

    /// <summary>Khởi tạo cache (mặc định: đồng hồ hệ thống, TTL 60 giây).</summary>
    public AccountStateCache(TimeProvider? timeProvider = null, TimeSpan? ttl = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _ttl = ttl ?? DefaultTtl;
    }

    /// <summary>Thế hệ hiện tại, đọc trước khi nạp dữ liệu.</summary>
    public long Generation => Interlocked.Read(ref _generation);

    /// <summary>Lấy mục còn hạn.</summary>
    public bool TryGet(Guid userId, out AccountState? state)
    {
        if (_entries.TryGetValue(userId, out var entry) && entry.ExpiresAt > _time.GetUtcNow())
        {
            state = entry.State;
            return true;
        }

        state = null;
        return false;
    }

    /// <summary>Ghi mục nếu không có lệnh xóa nào kể từ <paramref name="generationAtLoad"/>.</summary>
    public void Set(Guid userId, AccountState? state, long generationAtLoad)
    {
        if (Generation != generationAtLoad)
            return;

        _entries[userId] = new Entry(state, _time.GetUtcNow().Add(_ttl));
        if (Generation != generationAtLoad)
            _entries.TryRemove(userId, out _);
    }

    /// <summary>Xóa mục của một tài khoản.</summary>
    public void Invalidate(Guid userId)
    {
        Interlocked.Increment(ref _generation);
        _entries.TryRemove(userId, out _);
    }

    private sealed record Entry(AccountState? State, DateTimeOffset ExpiresAt);
}

/// <summary>Đọc trạng thái tài khoản từ CSDL qua cache dùng chung.</summary>
public sealed class AccountStateProvider : IAccountStateProvider
{
    private readonly IUserAccountRepository _accounts;
    private readonly AccountStateCache _cache;

    /// <summary>Khởi tạo provider.</summary>
    public AccountStateProvider(IUserAccountRepository accounts, AccountStateCache cache)
    {
        _accounts = accounts;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<AccountState?> GetAsync(Guid userId, CancellationToken ct = default)
    {
        if (_cache.TryGet(userId, out var cached))
            return cached;

        var generation = _cache.Generation;
        var state = await _accounts.GetStateAsync(userId, ct);
        _cache.Set(userId, state, generation);
        return state;
    }

    /// <inheritdoc />
    public void Invalidate(Guid userId) => _cache.Invalidate(userId);
}
