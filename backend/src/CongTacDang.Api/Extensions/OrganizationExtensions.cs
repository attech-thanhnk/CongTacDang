using System;
using CongTacDang.Api.Services;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Extensions;

public static class OrganizationExtensions
{
    /// <summary><c>Organization:ApprovalAuthorityRefreshMinutes</c>: 0 = tắt làm mới thẩm quyền.</summary>
    public static IServiceCollection AddOrganizationModel(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IPositionService, PositionService>();

        var minutes = configuration.GetValue("Organization:ApprovalAuthorityRefreshMinutes", 60);
        services.AddHostedService(sp => new ApprovalAuthorityRefreshService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<ILogger<ApprovalAuthorityRefreshService>>(),
            TimeSpan.FromMinutes(Math.Max(0, minutes))));
        return services;
    }
}
