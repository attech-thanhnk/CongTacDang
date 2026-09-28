using System;
using System.Collections.Generic;

namespace CongTacDang.Application.Common.Models;

/// <summary>Một trang kết quả truy vấn danh sách.</summary>
/// <typeparam name="T">Kiểu phần tử.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>Các phần tử của trang hiện tại.</summary>
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    /// <summary>Số trang (bắt đầu từ 1).</summary>
    public int Page { get; init; }

    /// <summary>Số phần tử tối đa mỗi trang.</summary>
    public int PageSize { get; init; }

    /// <summary>Tổng số phần tử khớp điều kiện.</summary>
    public int TotalCount { get; init; }

    /// <summary>Tổng số trang.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Chuẩn hóa tham số phân trang.</summary>
public static class Paging
{
    /// <summary>Số phần tử mặc định mỗi trang.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Số phần tử tối đa mỗi trang.</summary>
    public const int MaxPageSize = 200;

    /// <summary>Trang ≥ 1; kích thước trang trong [1, 200], mặc định 20.</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var size = pageSize is null or < 1 ? DefaultPageSize : Math.Min(pageSize.Value, MaxPageSize);
        return (p, size);
    }
}
