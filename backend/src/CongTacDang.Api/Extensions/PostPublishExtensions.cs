using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using CongTacDang.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

public static class PostPublishExtensions
{
    /// <summary><c>Notifications:DueSoonDays</c>: số ngày trước hạn bước được báo "sắp tới hạn".</summary>
    public static IServiceCollection AddPostPublish(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPostPublishRepository, PostPublishRepository>();
        services.AddScoped<IPublishedResultService, PublishedResultService>();
        services.AddScoped<IAppealService, AppealService>();
        services.AddScoped<IImprovementPlanService, ImprovementPlanService>();
        services.AddScoped<IImprovementPlanDocument, ImprovementPlanDocumentService>();
        services.AddScoped<IPostPublishWorkQueue, PostPublishWorkQueue>();

        var notifications = configuration.GetSection("Notifications").Get<NotificationOptions>() ?? new NotificationOptions();
        services.AddSingleton(notifications);
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
