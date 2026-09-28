using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

/// <summary>
/// Đăng ký phân quyền động có phạm vi (task 07/09, docs/thiet-ke/phan-quyen.md): policy theo mã quyền,
/// nguồn quyền mỗi request từ bản gán vai trò (<see cref="IPermissionResolver"/> + cache), guard theo đối tượng,
/// quản trị vai trò và bản gán. Không đọc claim role/perm trong JWT để phân quyền.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>Đăng ký toàn bộ dịch vụ phân quyền.</summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();

        // Cache quyền dùng chung toàn ứng dụng (một instance API), TTL 5 phút, xóa ngay khi phân quyền thay đổi.
        services.AddSingleton(sp => new PermissionCache(sp.GetService<System.TimeProvider>()));
        // Xóa cache theo request: trong transaction thì hoãn tới sau commit (DeferredAccessCacheInvalidator).
        services.AddScoped<IAccessCacheInvalidator, DeferredAccessCacheInvalidator>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IPermissionResolver, PermissionResolver>();

        // Điểm kiểm tra quyền theo đối tượng duy nhất.
        services.AddScoped<IAuthorizationGuard, AuthorizationGuard>();

        // Quản trị bản gán vai trò (dùng cả cho import — task 13).
        services.AddScoped<IRoleAssignmentService, RoleAssignmentService>();

        // Policy tên = mã quyền (và any:a|b), đánh giá từ IPermissionResolver.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, PermissionAuthorizationResultHandler>();

        return services;
    }
}
