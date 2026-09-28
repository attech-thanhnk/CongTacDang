using System.Collections.Concurrent;
using System.Security.Cryptography;
using CongTacDang.Application.Imports;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>
/// Lưu phiên xem trước trong bộ nhớ tiến trình (singleton). Mất khi khởi động lại — người dùng tải tệp lên lại.
/// Triển khai nhiều instance cần sticky session hoặc kho dùng chung.
/// </summary>
public sealed class InMemoryImportSessionStore : IImportSessionStore
{
    private readonly ConcurrentDictionary<Guid, ImportSession> _sessions = new();
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo.</summary>
    public InMemoryImportSessionStore(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <inheritdoc />
    public ImportSession Create(string kind, Guid userId, string? fileName, IReadOnlyList<ImportSourceRow> rows)
    {
        PurgeExpired();
        var session = new ImportSession(Guid.NewGuid(), kind, userId, fileName, rows, _time.GetUtcNow() + ImportLimits.SessionLifetime);
        _sessions[session.Id] = session;
        return session;
    }

    /// <inheritdoc />
    public ImportSession? Take(Guid id)
        => _sessions.TryRemove(id, out var session) && !IsExpired(session) ? session : null;

    /// <inheritdoc />
    public ImportSession? Peek(Guid id)
    {
        if (!_sessions.TryGetValue(id, out var session))
            return null;
        if (!IsExpired(session))
            return session;
        _sessions.TryRemove(id, out _);
        return null;
    }

    private bool IsExpired(ImportSession session) => session.ExpiresAt <= _time.GetUtcNow();

    private void PurgeExpired()
    {
        foreach (var pair in _sessions)
        {
            if (IsExpired(pair.Value))
                _sessions.TryRemove(pair.Key, out _);
        }
    }
}

/// <summary>
/// Lưu tệp kết quả (có mật khẩu tạm) <b>chỉ trong bộ nhớ</b>, không ghi đĩa: tải một lần, hết hạn sau 30 phút,
/// chỉ người tạo tải được. Nội dung bị xóa trắng khi hết hạn.
/// </summary>
public sealed class InMemoryImportResultStore : IImportResultStore
{
    private sealed record Entry(Guid UserId, string FileName, byte[] Content, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;

    /// <summary>Khởi tạo.</summary>
    public InMemoryImportResultStore(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    /// <inheritdoc />
    public (string Token, DateTimeOffset ExpiresAt) Add(Guid userId, string fileName, byte[] content)
    {
        PurgeExpired();
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = _time.GetUtcNow() + ImportLimits.SessionLifetime;
        _entries[token] = new Entry(userId, fileName, content, expiresAt);
        return (token, expiresAt);
    }

    /// <inheritdoc />
    public StoredImportResult? Take(string token, Guid userId)
    {
        if (!_entries.TryGetValue(token, out var entry))
            return null;
        if (entry.ExpiresAt <= _time.GetUtcNow())
        {
            if (_entries.TryRemove(token, out var expired))
                Array.Clear(expired.Content);
            return null;
        }

        // Người khác không lấy được và cũng không làm mất tệp của người tạo.
        if (entry.UserId != userId)
            return null;

        return _entries.TryRemove(token, out var taken) ? new StoredImportResult(taken.FileName, taken.Content) : null;
    }

    private void PurgeExpired()
    {
        var now = _time.GetUtcNow();
        foreach (var pair in _entries)
        {
            if (pair.Value.ExpiresAt <= now && _entries.TryRemove(pair.Key, out var expired))
                Array.Clear(expired.Content);
        }
    }
}
