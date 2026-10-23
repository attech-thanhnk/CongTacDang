using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Middlewares;

/// <summary>
/// Nhận <c>X-Request-Id</c> nếu hợp lệ, không thì sinh mới; trả trong response header và đưa vào scope log.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Request-Id";
    public const string LogScopeKey = "CorrelationId";
    public const int MaxLength = 64;

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context.Request.Headers[HeaderName]);

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (_logger.BeginScope(new Dictionary<string, object> { [LogScopeKey] = correlationId }))
        {
            await _next(context);
        }
    }

    /// <summary>Chỉ nhận id ngắn gồm chữ, số và <c>- _ . :</c> để tránh chèn dòng log/header.</summary>
    public static string ResolveCorrelationId(string? incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming))
        {
            var candidate = incoming.Trim();
            if (candidate.Length <= MaxLength && candidate.All(IsAllowedChar))
                return candidate;
        }

        return Guid.NewGuid().ToString("N");
    }

    private static bool IsAllowedChar(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':';
}
