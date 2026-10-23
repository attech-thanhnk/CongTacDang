using System;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Trong transaction chỉ xóa cache <b>sau</b> khi kết thúc — xóa trước commit thì request chen giữa
/// sẽ nạp lại quyền cũ và giữ tới hết TTL.
/// </summary>
public sealed class DeferredAccessCacheInvalidator : IAccessCacheInvalidator
{
    private readonly PermissionCache _cache;
    private readonly IAfterCommitActions _afterCommit;

    public DeferredAccessCacheInvalidator(PermissionCache cache, IAfterCommitActions afterCommit)
    {
        _cache = cache;
        _afterCommit = afterCommit;
    }

    public void InvalidateUser(Guid userId) => _afterCommit.Run(() => _cache.InvalidateUser(userId));

    public void InvalidateAll() => _afterCommit.Run(_cache.InvalidateAll);
}
