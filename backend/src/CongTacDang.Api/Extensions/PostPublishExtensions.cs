using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using CongTacDang.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký chức năng sau công bố (task 20): công khai kết quả, kiến nghị, kế hoạch 30-60-90 ngày (Mẫu 17), nhắc việc.</summary>
public static class PostPublishExtensions
{
    /// <summary>
    /// Cấu hình: <c>Notifications:DueSoonDays</c> — số ngày trước hạn của bước được báo "sắp tới hạn" (mặc định 2).
    /// </summary>
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
