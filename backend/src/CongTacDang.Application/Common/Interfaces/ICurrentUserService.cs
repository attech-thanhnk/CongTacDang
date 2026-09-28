using System;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Cung cấp thông tin actor cho audit ở tầng persistence mà không phụ thuộc ASP.NET Core.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Id actor đang thực hiện request.</summary>
    Guid? UserId { get; }

    /// <summary>Tên actor dùng để hiển thị trong audit.</summary>
    string UserName { get; }

    /// <summary>Địa chỉ IP của request hiện tại.</summary>
    string? IpAddress { get; }

    /// <summary>User-Agent của request hiện tại.</summary>
    string? UserAgent { get; }

    /// <summary>Đường dẫn endpoint đang được gọi.</summary>
    string? RequestPath { get; }
}
