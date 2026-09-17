using System.Threading.Tasks;

namespace CongTacDang.Application.Services;

/// <summary>
/// Kết quả xuất file báo cáo nhị phân (Excel)
/// </summary>
public class ReportFileResult
{
    /// <summary>Mảng byte nội dung file</summary>
    public byte[] FileBytes { get; set; } = System.Array.Empty<byte>();

    /// <summary>Định dạng MIME của file</summary>
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Tên file xuất ra kèm phần mở rộng (.xlsx)</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Giao diện dịch vụ kết xuất báo cáo và dữ liệu bảng tính
/// </summary>
public interface IReportService
{
    /// <summary>Xuất báo cáo danh sách trích ngang cán bộ lãnh đạo, quản lý</summary>
    Task<ReportFileResult> ExportCadresReportAsync();

    /// <summary>Xuất bảng tổng hợp hồ sơ cán bộ theo Chi bộ và đơn vị</summary>
    Task<ReportFileResult> ExportForm14ReportAsync();

    /// <summary>Xuất bảng thống kê cơ cấu tổ chức và sĩ số các Chi bộ</summary>
    Task<ReportFileResult> ExportForm15ReportAsync();
}
