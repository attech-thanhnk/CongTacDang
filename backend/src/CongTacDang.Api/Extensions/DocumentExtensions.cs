using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CongTacDang.Application.Services;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Services;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký bộ sinh biểu mẫu (template Word) và chuyển PDF — task 05; thông tin đơn vị, quản lý file mẫu — task 17.</summary>
public static class DocumentExtensions
{
    /// <summary>
    /// Cấu hình:
    /// <list type="bullet">
    /// <item><c>Documents:TemplatePath</c> — thư mục file mẫu gốc (mặc định <c>Templates/Word</c> trong thư mục chạy ứng dụng).
    /// Phiên bản do quản trị tải lên (lưu trong kho tệp) được ưu tiên khi đang kích hoạt.</item>
    /// <item><c>Documents:Pdf:SofficePath</c> — đường dẫn <c>soffice</c> (mặc định tự tìm).</item>
    /// <item><c>Documents:Pdf:TimeoutSeconds</c> (60), <c>Documents:Pdf:MaxConcurrency</c> (2), <c>Documents:Pdf:WorkDirectory</c> (thư mục tạm).</item>
    /// </list>
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
