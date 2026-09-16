using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện generic repository cho các thao tác CRUD cơ bản
/// </summary>
public interface IRepository<T> where T : class
{
    /// <summary>Lấy thực thể theo định danh Id</summary>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>Lấy toàn bộ danh sách thực thể</summary>
    Task<List<T>> ListAsync();

    /// <summary>Tìm kiếm thực thể theo biểu thức điều kiện</summary>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>Thêm mới thực thể vào cơ sở dữ liệu</summary>
    Task AddAsync(T entity);

    /// <summary>Cập nhật thông tin thực thể</summary>
    Task UpdateAsync(T entity);

    /// <summary>Xóa thực thể khỏi cơ sở dữ liệu</summary>
    Task DeleteAsync(T entity);
}
