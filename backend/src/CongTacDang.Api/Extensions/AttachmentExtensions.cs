using System;
using CongTacDang.Api.Services;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký dịch vụ tệp đính kèm, giới hạn tệp chưa gắn và tác vụ nền dọn tệp chưa gắn.</summary>
public static class AttachmentExtensions
{
    /// <summary>
    /// Cấu hình mục <c>Attachments</c>: <c>MaxPendingUnlinkedPerUser</c> (mặc định 30; 0 = không giới hạn),
    /// <c>UnlinkedCleanup:Enabled</c> (mặc định true), <c>UnlinkedCleanup:RetentionHours</c> (mặc định 24),
    /// <c>UnlinkedCleanup:IntervalMinutes</c> (mặc định 60).
    /// </summary>
    public static IServiceCollection AddAttachments(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new AttachmentOptions
        {
            MaxPendingUnlinkedPerUser = Math.Max(0, configuration.GetValue("Attachments:MaxPendingUnlinkedPerUser", 30)),
            CleanupEnabled = configuration.GetValue("Attachments:UnlinkedCleanup:Enabled", true),
            CleanupRetentionHours = Math.Max(0, configuration.GetValue("Attachments:UnlinkedCleanup:RetentionHours", 24)),
            CleanupIntervalMinutes = Math.Max(0, configuration.GetValue("Attachments:UnlinkedCleanup:IntervalMinutes", 60))
        };
        services.AddSingleton(options);
        services.AddScoped<IUnlinkedAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentService, AttachmentService>();
        services.AddScoped<IUnlinkedAttachmentCleaner, UnlinkedAttachmentCleaner>();

        if (options.CleanupEnabled && options.CleanupRetentionHours > 0 && options.CleanupIntervalMinutes > 0)
        {
            services.AddHostedService(sp => new UnlinkedAttachmentCleanupService(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<UnlinkedAttachmentCleanupService>>(),
                TimeSpan.FromMinutes(options.CleanupIntervalMinutes)));
        }
        return services;
    }
}
