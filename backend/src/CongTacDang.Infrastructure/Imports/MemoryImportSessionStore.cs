using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using CongTacDang.Application.Imports;

namespace CongTacDang.Infrastructure.Imports;

/// <summary>Lưu phiên nhập và file kết quả trong bộ nhớ (singleton, có hạn); file mật khẩu tạm không ghi ra đĩa,
/// xóa sau lần tải đầu. Khởi động lại máy chủ thì mất phiên đang chờ.</summary>
public sealed class MemoryImportSessionStore : IImportSessionStore
{
    private sealed class SessionEntry
    {
        public required ImportSession Session { get; init; }
        public bool Committing { get; set; }
    }

    private sealed record ResultEntry(Guid OwnerId, ImportFile File, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();
    private readonly ConcurrentDictionary<string, ResultEntry> _results = new(StringComparer.Ordinal);
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    public MemoryImportSessionStore(TimeProvider? time = null) => _time = time ?? TimeProvider.System;

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <inheritdoc />
    public ImportSession Create(string kind, Guid ownerId, string fileName, IReadOnlyList<ImportRawRow> rows)
    {
        Purge();
        var session = new ImportSession(Guid.NewGuid(), kind, ownerId, fileName, rows, Now.Add(ImportLimits.SessionLifetime));
        _sessions[session.Id] = new SessionEntry { Session = session };
        return session;
    }

    /// <inheritdoc />
    public ImportSession? Get(Guid id)
    {
        Purge();
        return _sessions.TryGetValue(id, out var entry) ? entry.Session : null;
    }

    /// <inheritdoc />
    public bool TryBeginCommit(Guid id)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(id, out var entry) || entry.Committing)
                return false;
            entry.Committing = true;
            return true;
        }
    }

    /// <inheritdoc />
    public void EndCommit(Guid id, bool succeeded)
    {
        lock (_gate)
        {
            if (succeeded)
                _sessions.TryRemove(id, out _);
            else if (_sessions.TryGetValue(id, out var entry))
                entry.Committing = false;
        }
    }

    /// <inheritdoc />
    public (string Token, DateTime ExpiresAt) SaveResult(Guid ownerId, ImportFile file)
    {
        Purge();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var expiresAt = Now.Add(ImportLimits.SessionLifetime);
        _results[token] = new ResultEntry(ownerId, file, expiresAt);
        return (token, expiresAt);
    }

    /// <inheritdoc />
    public ImportFile? TakeResult(string token, Guid ownerId)
    {
        Purge();
        lock (_gate)
        {
            if (!_results.TryGetValue(token, out var entry) || entry.OwnerId != ownerId)
                return null;
            _results.TryRemove(token, out _);
            return entry.File;
        }
    }

    /// <summary>Xóa phiên và file kết quả đã hết hạn.</summary>
    private void Purge()
    {
        var now = Now;
        foreach (var id in _sessions.Where(p => p.Value.Session.ExpiresAt <= now && !p.Value.Committing).Select(p => p.Key).ToList())
            _sessions.TryRemove(id, out _);
        foreach (var token in _results.Where(p => p.Value.ExpiresAt <= now).Select(p => p.Key).ToList())
            _results.TryRemove(token, out _);
    }
}
