using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Persistence;

/// <summary>Unit of Work dựa trên DbContext và execution strategy của Npgsql.</summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CongTacDangDbContext _db;

    public UnitOfWork(CongTacDangDbContext db)
    {
        _db = db;
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
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
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
        });
    }

    public void SetOriginalVersion(object entity, uint? version)
    {
        // Client chưa gửi version (frontend cũ) thì giữ xmin vừa đọc từ DB, không chặn cập nhật.
        if (version is null)
            return;

        _db.Entry(entity).Property("xmin").OriginalValue = version.Value;
    }

    public uint GetVersion(object entity)
    {
        var value = _db.Entry(entity).Property("xmin").CurrentValue;
        return value is uint version ? version : Convert.ToUInt32(value ?? 0u);
    }
}
