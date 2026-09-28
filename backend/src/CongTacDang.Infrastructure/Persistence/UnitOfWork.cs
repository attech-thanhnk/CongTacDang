using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Persistence;

/// <summary>Unit of Work dựa trên DbContext và execution strategy của Npgsql.</summary>
public sealed class UnitOfWork : IUnitOfWork, IAfterCommitActions
{
    private readonly CongTacDangDbContext _db;
    private readonly List<Action> _afterTransaction = new();
    private bool _inTransaction;

    public UnitOfWork(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_inTransaction)
            _afterTransaction.Add(action);
        else
            action();
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = _db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                _inTransaction = true;
                try
                {
                    await operation();
                    await transaction.CommitAsync(cancellationToken);
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
                finally
                {
                    _inTransaction = false;
                }
            });
        }
        finally
        {
            // Tác vụ hoãn (xóa cache…) chạy sau khi transaction đã commit hoặc rollback — cả hai đều an toàn.
            var pending = _afterTransaction.ToArray();
            _afterTransaction.Clear();
            foreach (var action in pending)
                action();
        }
    }

    public void SetOriginalVersion(object entity, uint? version)
    {
        // Client chưa gửi version (frontend cũ) thì giữ xmin vừa đọc từ DB, không chặn cập nhật.
        if (version is null)
            return;

        _db.Entry(entity).Property(nameof(IVersioned.Version)).OriginalValue = version.Value;
    }

    public uint GetVersion(object entity)
    {
        // Đọc từ thuộc tính thật: có giá trị cả khi entity được truy vấn bằng AsNoTracking.
        return entity is IVersioned versioned ? versioned.Version : 0u;
    }
}
