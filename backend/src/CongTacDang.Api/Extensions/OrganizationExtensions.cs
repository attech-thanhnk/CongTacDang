using System;
using CongTacDang.Api.Services;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký mô hình tổ chức động (task 14): danh mục chức vụ, chức vụ của cán bộ, thẩm quyền suy ra.</summary>
public static class OrganizationExtensions
{
    /// <summary>
    /// Repository/dịch vụ chức vụ và tác vụ nền làm mới thẩm quyền suy ra
    /// (<c>Organization:ApprovalAuthorityRefreshMinutes</c>, mặc định 60; 0 = tắt).
    /// </summary>
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
