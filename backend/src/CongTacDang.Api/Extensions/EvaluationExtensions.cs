using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký luồng đánh giá theo cấu hình kỳ (task 12).</summary>
public static class EvaluationExtensions
{
    /// <summary>Repository, service luồng 9 bước, quản lý kỳ và bộ tiêu chí.</summary>
    public static IServiceCollection AddEvaluationWorkflow(this IServiceCollection services)
    {
        services.AddScoped<IEvaluationWorkflowRepository, EvaluationWorkflowRepository>();
        services.AddScoped<IEvaluationWorkflowService, EvaluationWorkflowService>();
        services.AddScoped<IPeriodService, PeriodService>();
        // Task 16: bộ tiêu chí và thang điểm theo phiên bản.
        services.AddScoped<ICriteriaSetRepository, CriteriaSetRepository>();
        services.AddScoped<ICriteriaSetService, CriteriaSetService>();
        return services;
    }
}
