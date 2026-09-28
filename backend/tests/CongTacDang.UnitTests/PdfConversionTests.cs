using System.Text;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Infrastructure.Documents;
using CongTacDang.Infrastructure.Documents.Forms;
using CongTacDang.Infrastructure.Services;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Chỉ chạy khi máy có LibreOffice (soffice); ngược lại bỏ qua có lý do.</summary>
public sealed class LibreOfficeFactAttribute : FactAttribute
{
    public LibreOfficeFactAttribute()
    {
        if (LibreOfficeLocator.Find() == null)
            Skip = "Máy không cài LibreOffice (soffice) — bỏ qua test chuyển PDF thật.";
    }
}

/// <summary>Kiểm thử chuyển PDF phía máy chủ (T-38, T-42).</summary>
public class PdfConversionTests
{
    [Fact]
    public async Task MissingLibreOffice_ThrowsServiceUnavailable()
    {
        var converter = new LibreOfficePdfConverter(new PdfConversionOptions
        {
            SofficePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "soffice")
        });

        var ex = await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => converter.ConvertToPdfAsync(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, ".docx"));
        Assert.Equal(503, ex.StatusCode);
        Assert.Contains("LibreOffice", ex.Message);
    }

    [Fact]
    public async Task UnsupportedExtension_IsRejected()
    {
        var converter = new LibreOfficePdfConverter(new PdfConversionOptions());
        await Assert.ThrowsAsync<ArgumentException>(() => converter.ConvertToPdfAsync(new byte[] { 1 }, ".exe"));
    }

    [LibreOfficeFact]
    public async Task Mau01_ConvertsToPdf_AndCleansWorkDirectory()
    {
        var (_, records) = DocumentTemplateTests.SampleData();
        var template = new FileWordTemplateStore().Load(Mau01Data.TemplateFileName);
        var docx = DocxTemplateEngine.Render(template, TemplateDataBinder.Bind(Mau01Data.From(records[0]))).Content;
        var workDirectory = Path.Combine(Path.GetTempPath(), "congtacdang-pdf-test-" + Guid.NewGuid().ToString("N"));

        try
        {
            var converter = new LibreOfficePdfConverter(new PdfConversionOptions { WorkDirectory = workDirectory, TimeoutSeconds = 120 });
            var pdf = await converter.ConvertToPdfAsync(docx, ".docx");

            Assert.True(pdf.Length > 1000);
            Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
            Assert.Empty(Directory.GetFileSystemEntries(workDirectory));
        }
        finally
        {
            if (Directory.Exists(workDirectory))
                Directory.Delete(workDirectory, recursive: true);
        }
    }
}
