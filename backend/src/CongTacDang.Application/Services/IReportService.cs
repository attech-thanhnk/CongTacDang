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

/// <summary>Định dạng tệp xuất.</summary>
public enum ReportFormat
{
    /// <summary>Định dạng gốc của biểu mẫu: Word (.docx) hoặc Excel (.xlsx).</summary>
    Original = 0,

    /// <summary>PDF chuyển phía máy chủ từ định dạng gốc.</summary>
    Pdf = 1
}

/// <summary>
/// Giao diện dịch vụ kết xuất báo cáo và dữ liệu bảng tính
/// </summary>
public interface IReportService
{
    /// <summary>Xuất báo cáo danh sách trích ngang cán bộ lãnh đạo, quản lý</summary>
    Task<ReportFileResult> ExportCadresReportAsync();

    // Báo cáo Excel 14/15/15A/15B/16: periodId null = kỳ đang hoạt động; partyCellId null = toàn Đảng bộ
    // (phạm vi đã được kiểm tra quyền trước khi gọi).

    /// <summary>Xuất bảng tổng hợp hồ sơ cán bộ theo Chi bộ và đơn vị</summary>
    Task<ReportFileResult> ExportForm14ReportAsync(System.Guid? periodId = null, System.Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất bảng thống kê cơ cấu tổ chức và sĩ số các Chi bộ</summary>
    Task<ReportFileResult> ExportForm15ReportAsync(System.Guid? periodId = null, System.Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 15A: tổng hợp kiểm soát tỷ lệ trần 20% cấp Đảng ủy Công ty</summary>
    Task<ReportFileResult> ExportForm15AReportAsync(System.Guid? periodId = null, System.Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 15B: kiểm soát tỷ lệ trần 20% theo từng Chi bộ</summary>
    Task<ReportFileResult> ExportForm15BReportAsync(System.Guid? periodId = null, System.Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 16: tổng hợp kết quả xếp loại theo nhóm chức vụ</summary>
    Task<ReportFileResult> ExportForm16ReportAsync(System.Guid? periodId = null, System.Guid? partyCellId = null, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 01: Phiếu giao/đăng ký sản phẩm, công việc chuyên môn (.docx)</summary>
    Task<ReportFileResult> ExportMau01DocxAsync(System.Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 02: Phiếu tự đánh giá kết quả thực hiện sản phẩm (.docx)</summary>
    Task<ReportFileResult> ExportMau02DocxAsync(System.Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 10: Phiếu thẩm định, nhận xét, ghi nhận giải trình (.docx)</summary>
    Task<ReportFileResult> ExportMau10DocxAsync(System.Guid recordId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 11: Phiếu đánh giá, xếp loại cán bộ quý bỏ phiếu kín (.docx)</summary>
    Task<ReportFileResult> ExportMau11DocxAsync(System.Guid periodId, System.Guid? branchId, ReportFormat format = ReportFormat.Original);

    /// <summary>Xuất Mẫu 13: Biên bản kiểm phiếu đánh giá, xếp loại cán bộ quý (.docx)</summary>
    Task<ReportFileResult> ExportMau13DocxAsync(System.Guid periodId, System.Guid? branchId, ReportFormat format = ReportFormat.Original);
}
