using System;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using CongTacDang.Api.Authorization;
using CongTacDang.Api.Middlewares;
using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

public static class SecurityExtensions
{
    public const int DefaultAuthPermitLimit = 10;

    public const int DefaultAuthWindowSeconds = 60;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Giới hạn tần suất endpoint xác thực theo IP (<c>Security:RateLimit:Auth</c>).</summary>
    public static IServiceCollection AddSecurityRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "60";
                return ValueTask.CompletedTask;
            };
            options.AddPolicy("auth", httpContext =>
            {
                var config = httpContext.RequestServices.GetRequiredService<IConfiguration>();
                var permitLimit = Math.Max(1, config.GetValue("Security:RateLimit:Auth:PermitLimit", DefaultAuthPermitLimit));
                var windowSeconds = Math.Max(1, config.GetValue("Security:RateLimit:Auth:WindowSeconds", DefaultAuthWindowSeconds));
                return RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    /// <summary>
    /// Mỗi request: 401 nếu tài khoản không còn, bị khóa hoặc sstamp khác. Trạng thái lưu vào
    /// <see cref="HttpContext.Items"/> cho middleware bắt buộc đổi mật khẩu.
    /// </summary>
    public static IServiceCollection AddAccountSessionValidation(this IServiceCollection services)
    {
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var previousValidated = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                await previousValidated(context);
                if (context.Result is { Failure: not null } || context.Principal == null)
                    return;

                var userId = context.Principal.GetUserId();
                if (userId == null)
                {
                    context.Fail("Token không chứa danh tính người dùng.");
                    return;
                }

                var provider = context.HttpContext.RequestServices.GetRequiredService<IAccountStateProvider>();
                var state = await provider.GetAsync(userId.Value, context.HttpContext.RequestAborted);
                var stamp = context.Principal.FindFirst(IJwtService.SecurityStampClaim)?.Value;
                if (state == null || !state.AcceptsSession(stamp))
                {
                    context.Fail("Phiên đăng nhập đã bị thu hồi.");
                    return;
                }

                context.HttpContext.Items[PasswordChangeRequiredMiddleware.AccountStateItemKey] = state;
            };

            var previousChallenge = options.Events.OnChallenge;
            options.Events.OnChallenge = async context =>
            {
                await previousChallenge(context);
                if (context.Handled || context.Response.HasStarted)
                    return;

                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                context.Response.ContentType = "application/json";
                var message = context.AuthenticateFailure != null
                    ? "Phiên đăng nhập đã hết hạn hoặc đã bị thu hồi (tài khoản bị khóa, xóa hoặc đổi mật khẩu). Vui lòng đăng nhập lại."
                    : "Bạn chưa đăng nhập. Vui lòng đăng nhập để tiếp tục.";
                await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse.Fail(message), JsonOptions));
            };
        });

        return services;
    }

    /// <summary>Gọi sau <c>UseAuthentication</c>.</summary>
    public static IApplicationBuilder UsePasswordChangeEnforcement(this IApplicationBuilder app) =>
        app.UseMiddleware<PasswordChangeRequiredMiddleware>();
}
