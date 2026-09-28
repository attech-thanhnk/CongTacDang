using System;
using System.Collections.Concurrent;
using System.Threading;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Cache quyền hiệu lực trong bộ nhớ theo UserId (một instance API). Đăng ký singleton.
/// Mỗi lần xóa cache tăng "thế hệ"; kết quả được nạp trong lúc có lệnh xóa sẽ không được ghi vào cache,
/// tránh ghi đè dữ liệu cũ sau khi đã xóa.
/// </summary>
public sealed class PermissionCache : IAccessCacheInvalidator
{
    /// <summary>Thời gian sống mặc định của một mục cache.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, Entry> _entries = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;
    private long _generation;

    /// <summary>Khởi tạo cache với đồng hồ và TTL tùy chọn (mặc định: đồng hồ hệ thống, 5 phút).</summary>
    public PermissionCache(TimeProvider? timeProvider = null, TimeSpan? ttl = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _ttl = ttl ?? DefaultTtl;
    }

    /// <summary>Thế hệ hiện tại — đọc trước khi nạp dữ liệu và truyền vào <see cref="Set"/>.</summary>
    public long Generation => Interlocked.Read(ref _generation);

    /// <summary>Lấy mục còn hạn của người dùng.</summary>
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

    /// <summary>Ghi mục cache nếu không có lệnh xóa nào xảy ra kể từ thế hệ <paramref name="generationAtLoad"/>.</summary>
    public void Set(Guid userId, EffectivePermissions permissions, long generationAtLoad)
    {
        if (Generation != generationAtLoad)
            return;

        _entries[userId] = new Entry(permissions, _timeProvider.GetUtcNow().Add(_ttl));

        // Lệnh xóa chen vào giữa lúc kiểm tra và ghi → bỏ mục vừa ghi.
        if (Generation != generationAtLoad)
            _entries.TryRemove(userId, out _);
    }

    /// <inheritdoc />
    public void InvalidateUser(Guid userId)
    {
        Interlocked.Increment(ref _generation);
        _entries.TryRemove(userId, out _);
    }

    /// <inheritdoc />
    public void InvalidateAll()
    {
        Interlocked.Increment(ref _generation);
        _entries.Clear();
    }

    private sealed record Entry(EffectivePermissions Permissions, DateTimeOffset ExpiresAt);
}
