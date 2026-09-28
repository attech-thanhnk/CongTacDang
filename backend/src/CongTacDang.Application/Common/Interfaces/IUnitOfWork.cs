using System;
using System.Threading;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Điều phối lưu thay đổi và transaction cho một đơn vị nghiệp vụ.</summary>
public interface IUnitOfWork
{
    /// <summary>Lưu toàn bộ thay đổi đang được theo dõi bởi DbContext.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Thực hiện một nhóm thao tác trong transaction có retry của provider.</summary>
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default);

    /// <summary>Đặt phiên bản đọc được làm giá trị OriginalValue cho optimistic concurrency.</summary>
    void SetOriginalVersion(object entity, uint version);

    /// <summary>Đọc giá trị xmin hiện tại của entity.</summary>
    uint GetVersion(object entity);
}
