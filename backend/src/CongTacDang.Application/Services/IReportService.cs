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

    /// <summary>Xuất Mẫu 15A: tổng hợp kiểm soát tỷ lệ trần 20% cấp Đảng ủy Công ty</summary>
    Task<ReportFileResult> ExportForm15AReportAsync();

    /// <summary>Xuất Mẫu 15B: kiểm soát tỷ lệ trần 20% theo từng Chi bộ</summary>
    Task<ReportFileResult> ExportForm15BReportAsync();

    /// <summary>Xuất Mẫu 16: tổng hợp kết quả xếp loại theo nhóm chức vụ</summary>
    Task<ReportFileResult> ExportForm16ReportAsync();

    /// <summary>Xuất Mẫu 01: Phiếu giao/đăng ký sản phẩm, công việc chuyên môn (.docx)</summary>
    Task<ReportFileResult> ExportMau01DocxAsync(System.Guid recordId);

    /// <summary>Xuất Mẫu 02: Phiếu tự đánh giá kết quả thực hiện sản phẩm (.docx)</summary>
    Task<ReportFileResult> ExportMau02DocxAsync(System.Guid recordId);

    /// <summary>Xuất Mẫu 10: Phiếu thẩm định, nhận xét, ghi nhận giải trình (.docx)</summary>
    Task<ReportFileResult> ExportMau10DocxAsync(System.Guid recordId);

    /// <summary>Xuất Mẫu 11: Phiếu đánh giá, xếp loại cán bộ quý bỏ phiếu kín (.docx)</summary>
    Task<ReportFileResult> ExportMau11DocxAsync(System.Guid periodId, System.Guid? branchId);

    /// <summary>Xuất Mẫu 13: Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý (.docx)</summary>
    Task<ReportFileResult> ExportMau13DocxAsync(System.Guid periodId, System.Guid? branchId);
}
