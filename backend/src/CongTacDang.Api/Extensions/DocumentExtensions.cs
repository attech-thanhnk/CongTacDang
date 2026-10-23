using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Services;

namespace CongTacDang.Api.Extensions;

/// <summary>Sinh biểu mẫu Word, chuyển PDF, thông tin đơn vị và quản lý file mẫu.</summary>
public static class DocumentExtensions
{
    /// <summary>
    /// <c>Documents:TemplatePath</c>: thư mục mẫu gốc; bản quản trị tải lên đang kích hoạt được ưu tiên.
    /// </summary>
    public static IServiceCollection AddDocumentGeneration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IBundledWordTemplates>(
            new FileWordTemplateStore(configuration["Documents:TemplatePath"]));
        services.AddScoped<IActiveWordTemplateLookup, DbActiveWordTemplateLookup>();
        services.AddScoped<IWordTemplateStore, WordTemplateStore>();
        services.AddScoped<IWordTemplateService, WordTemplateService>();

        services.AddSingleton<OrganizationSettingsCache>();
        services.AddScoped<IOrganizationSettingsService, OrganizationSettingsService>();

        var pdfOptions = configuration.GetSection("Documents:Pdf").Get<PdfConversionOptions>() ?? new PdfConversionOptions();
        services.AddSingleton(pdfOptions);
        services.AddSingleton<IPdfConverter, LibreOfficePdfConverter>();
        services.AddScoped<IRecordFormService, RecordFormService>();
        return services;
    }
}
