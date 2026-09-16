using System.Threading.Tasks;

namespace CongTacDang.Application.Services;

public class ReportFileResult
{
    public byte[] FileBytes { get; set; } = System.Array.Empty<byte>();
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileName { get; set; } = string.Empty;
}

public interface IReportService
{
    Task<ReportFileResult> ExportCadresReportAsync();
    Task<ReportFileResult> ExportForm14ReportAsync();
    Task<ReportFileResult> ExportForm15ReportAsync();
}
