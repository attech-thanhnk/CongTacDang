using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Persistence;
using CongTacDang.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

    /// <summary>Áp dụng migration, baseline legacy schema và seed theo cấu hình.</summary>
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
                await BaselineLegacyDatabaseAsync(db);
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

    private static async Task BaselineLegacyDatabaseAsync(CongTacDangDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            var historyExists = await ScalarBooleanAsync(
                connection,
                "SELECT to_regclass('public.\"__EFMigrationsHistory\"') IS NOT NULL;");
            var businessTableExists = await ScalarBooleanAsync(
                connection,
                "SELECT EXISTS (SELECT 1 FROM pg_tables WHERE schemaname = 'public' AND tablename IN ('party_member_profiles', 'evaluation_periods', 'evaluation_records')); ");

            if (!businessTableExists)
                return;

            var hasAppliedMigration = historyExists && await ScalarBooleanAsync(
                connection,
                "SELECT EXISTS (SELECT 1 FROM \"__EFMigrationsHistory\");");
            if (hasAppliedMigration)
                return;

            var migrationAssembly = db.GetService<IMigrationsAssembly>();
            var initialMigration = migrationAssembly.Migrations.Keys
                .SingleOrDefault(x => x.EndsWith("_InitialCreate", StringComparison.Ordinal));
            if (initialMigration == null)
                throw new InvalidOperationException("Không tìm thấy migration InitialCreate để baseline CSDL hiện hữu.");

            await ExecuteAsync(connection, @"
                CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                    ""MigrationId"" character varying(150) NOT NULL,
                    ""ProductVersion"" character varying(32) NOT NULL,
                    CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
                );");

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                VALUES (@migrationId, @productVersion)
                ON CONFLICT (""MigrationId"") DO NOTHING;";
            AddParameter(command, "migrationId", initialMigration);
            AddParameter(command, "productVersion", "10.0.12");
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static async Task<bool> ScalarBooleanAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
