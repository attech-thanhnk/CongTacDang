using CongTacDang.Application.Imports;
using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Infrastructure.Imports;
using Microsoft.Extensions.DependencyInjection;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký chức năng nhập dữ liệu từ Excel và các loại nhập.</summary>
public static class ImportExtensions
{
    /// <summary>Khung nhập (dịch vụ, đọc/ghi Excel, phiên trong bộ nhớ) và bốn loại nhập: đơn vị, tổ chức Đảng, cán bộ, gán vai trò.</summary>
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

    /// <summary>Đăng ký một loại nhập — controller và trang nhập dữ liệu tự hiện loại mới.</summary>
    public static IServiceCollection AddImportDefinition<T>(this IServiceCollection services) where T : class, IImportDefinition
    {
        services.AddScoped<IImportDefinition, T>();
        return services;
    }
}
