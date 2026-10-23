using System;
using System.Security.Claims;
using CongTacDang.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace CongTacDang.Api.Services;

/// <summary>Actor và thông tin request cho audit log.</summary>
public sealed class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? Context => _httpContextAccessor.HttpContext;

    public Guid? UserId
    {
        get
        {
            var value = Context?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? Context?.User.FindFirstValue("sub");
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string UserName => Context?.User.FindFirstValue(ClaimTypes.Name)
        ?? Context?.User.FindFirstValue("unique_name")
        ?? Context?.User.Identity?.Name
        ?? string.Empty;

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();

    public string? RequestPath => Context?.Request.Path.ToString();
}
