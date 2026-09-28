using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

public static class HostingExtensions
{
    public static void ValidateRequiredConfiguration(this IConfiguration configuration)
    {
        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            throw new InvalidOperationException(
                "Thiếu cấu hình bắt buộc 'Jwt:Secret'. Hãy đặt bằng biến môi trường hoặc dotnet user-secrets.");
        }

        if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
        {
            throw new InvalidOperationException("Cấu hình 'Jwt:Secret' phải có ít nhất 32 byte.");
        }

        if (jwtSecret.Contains("CongTacDang_ATTECH_2026_SecretKey", StringComparison.OrdinalIgnoreCase) ||
            jwtSecret.Contains("Minimum_32_Chars_Long", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Cấu hình 'Jwt:Secret' vẫn là chuỗi mẫu cũ. Hãy sinh secret mới trước khi khởi động.");
        }

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("Default")))
        {
            throw new InvalidOperationException(
                "Thiếu cấu hình bắt buộc 'ConnectionStrings:Default'. Hãy đặt bằng biến môi trường hoặc dotnet user-secrets.");
        }
    }

    public static IServiceCollection AddConfiguredForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;
            options.KnownProxies.Clear();
            options.KnownIPNetworks.Clear();

            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    options.KnownProxies.Add(address);
                }
                else
                {
                    throw new InvalidOperationException($"ForwardedHeaders:KnownProxies chứa IP không hợp lệ: '{proxy}'.");
                }
            }

            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
            {
                // System.Net.IPNetwork.TryParse kiểm tra cả định dạng CIDR lẫn độ dài prefix
                if (!network.Contains('/') ||
                    !System.Net.IPNetwork.TryParse(network.Trim(), out var ipNetwork))
                {
                    throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks chứa mạng không hợp lệ: '{network}'.");
                }

                options.KnownIPNetworks.Add(ipNetwork);
            }
        });

        return services;
    }

    public static IApplicationBuilder UseConfiguredSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Content-Security-Policy"] = "frame-ancestors 'none';";

                if (context.Request.IsHttps)
                {
                    headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
                }

                return Task.CompletedTask;
            });

            await next();
        });

        return app;
    }
}
