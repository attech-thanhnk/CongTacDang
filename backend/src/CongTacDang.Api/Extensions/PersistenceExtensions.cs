using System;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Persistence;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký và khởi tạo persistence cho API.</summary>
public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:Default chưa được cấu hình.");

        services.AddDbContext<CongTacDangDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        services.AddHealthChecks()
            .AddDbContextCheck<CongTacDangDbContext>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<IAttachmentAccessReader, AttachmentRepository>();
        services.AddScoped<IAttachmentVersionRepository, AttachmentRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IEvaluationRepository, EvaluationRepository>();
        services.AddScoped<ICollectiveEvaluationRepository, CollectiveEvaluationRepository>();
        services.AddScoped<IEvaluationMeetingRepository, EvaluationMeetingRepository>();

        return services;
    }

    /// <summary>Áp dụng migration và seed theo cấu hình.</summary>
    public static async Task ApplyPersistenceAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        var autoMigrate = app.Configuration.GetValue<bool?>("Database:AutoMigrate")
            ?? app.Environment.IsDevelopment();
        var seedSampleData = app.Configuration.GetValue<bool>("Database:SeedSampleData");

        try
        {
            if (autoMigrate)
            {
                await db.Database.MigrateAsync();
            }

            await DataSeeder.SeedAsync(
                db,
                seedSampleData,
                app.Configuration.GetValue<bool>("Database:ResetRolePermissions"),
                app.Logger);
        }
        catch (Exception ex)
        {
            app.Logger.LogCritical(ex, "Không thể khởi tạo persistence; ứng dụng sẽ dừng.");
            throw;
        }
    }
}
