using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

public static class HostingExtensions
{
    /// <summary>
    /// Kiểm tra cấu hình bắt buộc và khóa/mật khẩu (xem <see cref="StartupSecretPolicy"/>). Có lỗi → ghi thông báo
    /// tiếng Việt ra stderr (log của container) rồi ném ngoại lệ để ứng dụng dừng, không khởi động với cấu hình không an toàn.
    /// </summary>
    public static void ValidateRequiredConfiguration(this IConfiguration configuration, string environmentName)
    {
        var errors = StartupSecretPolicy.Validate(configuration, environmentName);
        if (errors.Count == 0)
        {
            return;
        }

        var message = $"Không khởi động: cấu hình khóa/mật khẩu không an toàn (môi trường {environmentName}):"
            + Environment.NewLine + string.Join(Environment.NewLine, errors.Select(e => " - " + e))
            + Environment.NewLine + "Xem docs/deployment.md, mục \"Đổi khóa và mật khẩu trước khi chạy thật\".";
        Console.Error.WriteLine("[LỖI CẤU HÌNH] " + message);
        throw new InvalidOperationException(message);
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
