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
        services.AddScoped<IRecordOpinionRepository, RecordOpinionRepository>();
        services.AddScoped<IRecordOpinionService, RecordOpinionService>();
        services.AddScoped<IControlCheckRepository, ControlCheckRepository>();
        services.AddScoped<ControlCheckService>();
        services.AddScoped<IControlCheckService>(sp => sp.GetRequiredService<ControlCheckService>());
        services.AddScoped<IControlCheckGate>(sp => sp.GetRequiredService<ControlCheckService>());
        services.AddScoped<IExpertCrosscheckRepository, ExpertCrosscheckRepository>();
        services.AddScoped<ExpertCrosscheckService>();
        services.AddScoped<IExpertCrosscheckService>(sp => sp.GetRequiredService<ExpertCrosscheckService>());
        services.AddScoped<IExpertCrosscheckGate>(sp => sp.GetRequiredService<ExpertCrosscheckService>());
        services.AddScoped<ICrosscheckWorkQueue>(sp => sp.GetRequiredService<ExpertCrosscheckService>());
        services.AddScoped<ICalibrationRepository, CalibrationRepository>();
        services.AddScoped<CalibrationService>();
        services.AddScoped<ICalibrationService>(sp => sp.GetRequiredService<CalibrationService>());
        services.AddScoped<ICalibrationGate>(sp => sp.GetRequiredService<CalibrationService>());
        return services;
    }
}
