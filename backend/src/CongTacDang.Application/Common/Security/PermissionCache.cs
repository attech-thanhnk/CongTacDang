using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Cache trong bộ nhớ (một instance API), singleton. Mỗi lần xóa tăng "thế hệ"; kết quả nạp
/// trong lúc có lệnh xóa không được ghi, tránh giữ lại quyền cũ.
/// </summary>
public sealed class PermissionCache : IAccessCacheInvalidator
{
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;
    private long _generation;

    public PermissionCache(TimeProvider? timeProvider = null, TimeSpan? ttl = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _ttl = ttl ?? DefaultTtl;
    }

    /// <summary>Đọc trước khi nạp dữ liệu và truyền vào <see cref="Set(Guid, EffectivePermissions, long)"/>.</summary>
    public long Generation => Interlocked.Read(ref _generation);

    /// <summary>Resolver dùng chung đồng hồ này để tính hiệu lực bản gán.</summary>
    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public bool TryGet(Guid userId, out EffectivePermissions permissions)
    {
        if (_entries.TryGetValue(userId, out var entry) && entry.ExpiresAt > _timeProvider.GetUtcNow())
        {
            permissions = entry.Permissions;
            return true;
        }

        permissions = null!;
        return false;
    }

    /// <summary>Chỉ ghi nếu không có lệnh xóa nào kể từ thế hệ <paramref name="generationAtLoad"/>.</summary>
    public void Set(Guid userId, EffectivePermissions permissions, long generationAtLoad)
        => Set(userId, permissions, generationAtLoad, notAfter: null);

    /// <summary>Hết hạn sớm hơn TTL tại <paramref name="notAfter"/> (bản gán bắt đầu/hết hiệu lực).</summary>
    public void Set(Guid userId, EffectivePermissions permissions, long generationAtLoad, DateTimeOffset? notAfter)
    {
        if (Generation != generationAtLoad)
            return;

        var expiresAt = _timeProvider.GetUtcNow().Add(_ttl);
        if (notAfter.HasValue && notAfter.Value < expiresAt)
            expiresAt = notAfter.Value;

        _entries[userId] = new Entry(permissions, expiresAt);

        // Lệnh xóa chen vào giữa lúc kiểm tra và ghi → bỏ mục vừa ghi.
        if (Generation != generationAtLoad)
            _entries.TryRemove(userId, out _);
    }

    public void InvalidateUser(Guid userId)
    {
        Interlocked.Increment(ref _generation);
        _entries.TryRemove(userId, out _);
    }

    public void InvalidateAll()
    {
        Interlocked.Increment(ref _generation);
        _entries.Clear();
    }

    private sealed record Entry(EffectivePermissions Permissions, DateTimeOffset ExpiresAt);
}
