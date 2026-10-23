using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

/// <summary>Phân quyền động có phạm vi; quyền tính mỗi request qua <see cref="IPermissionResolver"/>, không đọc claim JWT.</summary>
public static class AuthorizationExtensions
{
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();

        // Cache trong bộ nhớ (giả định một instance API), TTL 5 phút, xóa khi phân quyền thay đổi.
        services.AddSingleton(sp => new PermissionCache(sp.GetService<System.TimeProvider>()));
        // Trong transaction thì hoãn xóa cache tới sau commit.
        services.AddScoped<IAccessCacheInvalidator, DeferredAccessCacheInvalidator>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();

        services.AddScoped<IAuthorizationGuard, AuthorizationGuard>();

        services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();
        services.AddScoped<IPerformerService, PerformerService>();

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PermissionAuthorizationResultHandler>();

        return services;
    }
}
