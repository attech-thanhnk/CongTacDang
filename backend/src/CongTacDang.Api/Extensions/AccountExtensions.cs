using CongTacDang.Application.Accounts;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

public static class AccountExtensions
{
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
