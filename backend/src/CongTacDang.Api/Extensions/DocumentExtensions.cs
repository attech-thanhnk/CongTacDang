using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CongTacDang.Infrastructure.Documents;

namespace CongTacDang.Api.Extensions;

/// <summary>Đăng ký bộ sinh biểu mẫu (template Word) — task 05.</summary>
public static class DocumentExtensions
{
    /// <summary>
    /// Cấu hình:
    /// <c>Documents:TemplatePath</c> — thư mục template Word (mặc định <c>Templates/Word</c> trong thư mục chạy ứng dụng).
    /// </summary>
    public static IServiceCollection AddDocumentGeneration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IWordTemplateStore>(
            new FileWordTemplateStore(configuration["Documents:TemplatePath"]));
        return services;
    }
}
