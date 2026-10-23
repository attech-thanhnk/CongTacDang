using CongTacDang.Application.Imports;
using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Infrastructure.Imports;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

public static class ImportExtensions
{
    public static IServiceCollection AddImports(this IServiceCollection services)
    {
        services.AddSingleton<IImportSessionStore, MemoryImportSessionStore>();
        services.AddSingleton<IImportWorkbook, ExcelImportWorkbook>();
        services.AddScoped<ImportLookup>();
        services.AddScoped<IImportLookup>(sp => sp.GetRequiredService<ImportLookup>());
        services.AddScoped<IImportAuditWriter>(sp => sp.GetRequiredService<ImportLookup>());
        services.AddScoped<IImportService, ImportService>();

        services.AddImportDefinition<DepartmentImportDefinition>();
        services.AddImportDefinition<PartyCellImportDefinition>();
        services.AddImportDefinition<UserImportDefinition>();
        services.AddImportDefinition<RoleAssignmentImportDefinition>();
        return services;
    }

    /// <summary>Controller và trang nhập dữ liệu tự hiện loại mới đăng ký.</summary>
    public static IServiceCollection AddImportDefinition<T>(this IServiceCollection services) where T : class, IImportDefinition
    {
        services.AddScoped<IImportDefinition, T>();
        return services;
    }
}
