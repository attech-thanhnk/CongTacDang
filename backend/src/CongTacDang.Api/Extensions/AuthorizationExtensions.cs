using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

/// <summary>
/// Đăng ký phân quyền theo mã quyền (task 07/09): policy động theo mã, nguồn quyền mỗi request
/// (<see cref="IPermissionResolver"/> + cache), guard kiểm tra quyền theo đối tượng.
/// Không đọc claim role/perm trong JWT để phân quyền.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>Đăng ký toàn bộ dịch vụ phân quyền.</summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization();

        // Cache quyền dùng chung toàn ứng dụng (một instance API), TTL 5 phút, xóa ngay khi phân quyền thay đổi.
        services.AddSingleton(sp => new PermissionCache(sp.GetService<System.TimeProvider>()));
        services.AddSingleton<IAccessCacheInvalidator>(sp => sp.GetRequiredService<PermissionCache>());
        services.AddScoped<IPermissionResolver, PermissionResolver>();

        // Kiểm tra quyền theo đối tượng (v0: adapter sang IAccessPolicy; task 09 thay bằng bản có phạm vi).
        services.AddScoped<IAuthorizationGuard, LegacyAuthorizationGuard>();

        // Policy tên = mã quyền (cũ và mới) + policy composite cũ, đánh giá từ IPermissionResolver.
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, PermissionAuthorizationResultHandler>();

        return services;
    }
}
