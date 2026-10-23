using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);

    Task<List<T>> ListAsync();

    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);

    Task AddAsync(T entity);

    Task UpdateAsync(T entity);

    /// <summary>Đánh dấu thực thể xóa mềm nếu entity hỗ trợ, ngược lại xóa vật lý.</summary>
    Task DeleteAsync(T entity);
}
