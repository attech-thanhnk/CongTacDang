using CongTacDang.Application.Imports;
using CongTacDang.Application.Imports.Definitions;
using CongTacDang.Infrastructure.Imports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký khung nhập dữ liệu (task 10) và các loại import.</summary>
public static class ImportExtensions
{
    /// <summary>Đăng ký khung import + 3 loại mặc định (Phòng, Chi bộ, cán bộ).</summary>
    public static IServiceCollection AddImports(this IServiceCollection services)
    {
        services.TryAddSingleton<IImportWorkbook, ClosedXmlImportWorkbook>();
        services.TryAddSingleton<IImportSessionStore, InMemoryImportSessionStore>();
        services.TryAddSingleton<IImportResultStore, InMemoryImportResultStore>();
        services.TryAddScoped<IImportLookup, ImportLookup>();
        services.TryAddScoped<IImportAuditLog, ImportAuditLog>();
        services.TryAddScoped<IImportService, ImportService>();

        services.AddImportDefinition<DepartmentImportDefinition>();
        services.AddImportDefinition<PartyCellImportDefinition>();
        services.AddImportDefinition<UserImportDefinition>();
        return services;
    }

    /// <summary>
    /// Đăng ký một loại import (scoped). <typeparamref name="TDefinition"/> phải triển khai
    /// <see cref="IImportDefinition{TRow}"/>; controller và giao diện tự nhận loại mới qua <c>GET /api/imports/kinds</c>.
    /// </summary>
    public static IServiceCollection AddImportDefinition<TDefinition>(this IServiceCollection services)
        where TDefinition : class, IImportDefinition
    {
        var definitionInterface = typeof(TDefinition).GetInterfaces()
            .SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IImportDefinition<>))
            ?? throw new InvalidOperationException(
                $"{typeof(TDefinition).Name} phải triển khai đúng một IImportDefinition<TRow>.");
        var processorType = typeof(ImportProcessor<>).MakeGenericType(definitionInterface.GetGenericArguments()[0]);

        services.AddScoped<TDefinition>();
        services.AddScoped(typeof(IImportProcessor),
            sp => Activator.CreateInstance(processorType, sp.GetRequiredService<TDefinition>())!);
        return services;
    }
}
