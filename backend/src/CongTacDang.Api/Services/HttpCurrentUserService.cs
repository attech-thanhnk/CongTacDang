using System;
using System.Security.Claims;
using CongTacDang.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CongTacDang.Api.Services;

/// <summary>
/// Đọc actor và thông tin request hiện tại để persistence ghi audit tập trung.
/// </summary>
public sealed class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Khởi tạo provider đọc actor từ HTTP context hiện tại.</summary>
    public HttpCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    /// <summary>Lấy Id người dùng từ claim NameIdentifier hoặc sub.</summary>
    public Guid? UserId
    {
        get
        {
            var value = Context?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Context?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    /// <summary>Lấy tên hiển thị hoặc username của actor hiện tại.</summary>
    public string UserName => Context?.User.FindFirstValue(ClaimTypes.Name)
        ?? Context?.User.FindFirstValue("unique_name")
        ?? Context?.User.Identity?.Name
        ?? string.Empty;

    /// <summary>Lấy địa chỉ IP của request hiện tại.</summary>
    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    /// <summary>Lấy User-Agent của request hiện tại.</summary>
    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();

    /// <summary>Lấy đường dẫn endpoint đang thực hiện.</summary>
    public string? RequestPath => Context?.Request.Path.ToString();
}
