using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

public static class EvaluationExtensions
{
    public static IServiceCollection AddEvaluationWorkflow(this IServiceCollection services)
    {
        services.AddScoped<IEvaluationWorkflowRepository, EvaluationWorkflowRepository>();
        services.AddScoped<IEvaluationWorkflowService, EvaluationWorkflowService>();
        services.AddScoped<IPeriodService, PeriodService>();
        services.AddScoped<ICriteriaSetRepository, CriteriaSetRepository>();
        services.AddScoped<ICriteriaSetService, CriteriaSetService>();
        services.AddScoped<ITaskChangeRepository, TaskChangeRepository>();
        services.AddScoped<TaskChangeService>();
        services.AddScoped<ITaskChangeService>(sp => sp.GetRequiredService<TaskChangeService>());
        services.AddScoped<ITaskChangeWorkQueue>(sp => sp.GetRequiredService<TaskChangeService>());
        services.AddScoped<ICalibrationRepository, CalibrationRepository>();
        services.AddScoped<ICalibrationService, CalibrationService>();
        return services;
    }
}
