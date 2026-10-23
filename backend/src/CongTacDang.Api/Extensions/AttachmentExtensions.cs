using System;
using CongTacDang.Api.Services;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Extensions;

public static class AttachmentExtensions
{
    /// <summary><c>Attachments:MaxPendingUnlinkedPerUser</c>: 0 = không giới hạn.</summary>
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
