using System;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Application.Common.Security;

/// <summary>
/// Xóa cache quyền theo đơn vị công việc của request: nếu đang trong transaction (<c>IUnitOfWork.ExecuteInTransactionAsync</c>)
/// thì chỉ xóa <b>sau</b> khi transaction kết thúc — nếu xóa trước commit, request chen giữa sẽ nạp lại quyền cũ và giữ
/// trong cache tới 5 phút. Đăng ký scoped; cache dùng chung vẫn là <see cref="PermissionCache"/> (singleton).
/// </summary>
public sealed class DeferredAccessCacheInvalidator : IAccessCacheInvalidator
{
    private readonly PermissionCache _cache;
    private readonly IAfterCommitActions _afterCommit;

    /// <summary>Khởi tạo.</summary>
    public DeferredAccessCacheInvalidator(PermissionCache cache, IAfterCommitActions afterCommit)
    {
        _cache = cache;
        _afterCommit = afterCommit;
    }

    /// <inheritdoc />
    public void InvalidateUser(Guid userId) => _afterCommit.Run(() => _cache.InvalidateUser(userId));

    /// <inheritdoc />
    public void InvalidateAll() => _afterCommit.Run(_cache.InvalidateAll);
}
