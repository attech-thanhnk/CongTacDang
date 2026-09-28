using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký dịch vụ tài khoản và phiên làm việc (task 08).</summary>
public static class AccountExtensions
{
    /// <summary>
    /// Repository tài khoản/nhật ký đăng nhập, cache trạng thái tài khoản, chính sách mật khẩu
    /// (<c>Security:Password:MinLength</c>, mặc định 8), dịch vụ tài khoản và kiểm tra phiên mỗi request.
    /// </summary>
    public static IServiceCollection AddAccountServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<ILoginEventRepository, LoginEventRepository>();

        services.AddSingleton(sp => new AccountStateCache(sp.GetService<System.TimeProvider>()));
        services.AddScoped<IAccountStateProvider, AccountStateProvider>();
        services.AddSingleton(new PasswordPolicy(
            configuration.GetValue("Security:Password:MinLength", PasswordPolicy.DefaultMinLength)));
        services.AddScoped<IUserAccountService, UserAccountService>();

        services.AddAccountSessionValidation();
        return services;
    }
}
