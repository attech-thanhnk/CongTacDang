using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Một dòng chỉ tiêu của phiếu đối soát.</summary>
public class ExpertCrosscheckItemDto
{
    public Guid Id { get; set; }
    public int Order { get; set; }
    public Guid? TaskId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    /// <summary>Dữ liệu cá nhân tự khai.</summary>
    public string SelfData { get; set; } = string.Empty;
    /// <summary>Kết quả đối soát với dữ liệu gốc.</summary>
    public string SourceData { get; set; } = string.Empty;
    /// <summary><c>Pass</c> / <c>Exceed</c> / <c>Defect</c>; null khi chưa lập.</summary>
    public string? Result { get; set; }
    public string? ResultName { get; set; }
    /// <summary><c>None</c> / <c>Decrease</c> / <c>RecognizeExceed</c>.</summary>
    public string AdjustmentType { get; set; } = "None";
    public double? AdjustmentPoints { get; set; }
    /// <summary>Chuỗi cột "Đề xuất tăng/giảm điểm" đã định dạng.</summary>
    public string AdjustmentText { get; set; } = "0";
    public string BasisText { get; set; } = string.Empty;
    public string BasisDocNumber { get; set; } = string.Empty;
    public DateOnly? BasisDocDate { get; set; }
    public string BasisAgency { get; set; } = string.Empty;
    /// <summary>In "K/AD".</summary>
    public bool NotApplicable { get; set; }
}

public class ExpertCrosscheckHistoryDto
{
    public string Action { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string ByUserName { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Dòng danh sách phiếu (không kèm các dòng chỉ tiêu).</summary>
public class ExpertCrosscheckSummaryDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public string MemberFullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    public Guid? ExpertDepartmentId { get; set; }
    /// <summary>Tên đơn vị đối soát trong danh mục hoặc tên cơ quan ngoài hệ thống.</summary>
    public string ExpertUnitName { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = new();
    public List<string> FieldNames { get; set; } = new();
    /// <summary><c>Requested</c> / <c>InProgress</c> / <c>Submitted</c> / <c>Confirmed</c> / <c>Cancelled</c>.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
}

/// <summary>Chi tiết phiếu và cờ hành động của người xem.</summary>
public class ExpertCrosscheckDto : ExpertCrosscheckSummaryDto
{
    public string AddresseeName { get; set; } = string.Empty;
    public string RequestReason { get; set; } = string.Empty;
    /// <summary>Cơ quan ngoài hệ thống: người có quyền yêu cầu nhập thay.</summary>
    public bool EnteredOnBehalf { get; set; }
    public string? SubmittedByName { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public string? ReturnReason { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledByName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<ExpertCrosscheckItemDto> Items { get; set; } = new();
    public List<ExpertCrosscheckHistoryDto> History { get; set; } = new();

    /// <summary>Sửa được nội dung yêu cầu (lĩnh vực, lý do, các dòng cần đối soát): có <c>crosscheck.request</c>, phiếu chưa được lập.</summary>
    public bool CanEditRequest { get; set; }
    /// <summary>Tải lên / thay / gỡ tệp đính kèm: người sửa được phiếu, phiếu chưa gửi và kỳ đang mở.</summary>
    public bool CanAttach { get; set; }
    /// <summary>Nhập kết quả đối soát.</summary>
    public bool CanRespond { get; set; }
    /// <summary>Gửi xác nhận.</summary>
    public bool CanSubmit { get; set; }
    public bool CanConfirm { get; set; }
    public bool CanReturn { get; set; }
    public bool CanCancel { get; set; }
}

/// <summary>Khối "Đối soát chuyên ngành" trên trang hồ sơ.</summary>
public class RecordCrosschecksDto
{
    public Guid RecordId { get; set; }
    /// <summary>Diện quyết định của hồ sơ có dùng đối soát (cổng khác Off).</summary>
    public bool Applicable { get; set; }
    public bool CanRequest { get; set; }
    /// <summary>Lý do chưa yêu cầu được (kỳ chưa mở, hồ sơ chưa ở giai đoạn thẩm định...).</summary>
    public string? BlockedReason { get; set; }
    /// <summary>Còn phiếu chưa xác nhận, chưa hủy: chặn hoàn thành thẩm định.</summary>
    public bool HasOpen { get; set; }
    public List<ExpertCrosscheckSummaryDto> Items { get; set; } = new();
}

public class CrosscheckRequestItemDto
{
    /// <summary>Sản phẩm đăng ký của hồ sơ; bỏ trống với dòng tự do.</summary>
    public Guid? TaskId { get; set; }
    /// <summary>Bỏ trống khi có <see cref="TaskId"/>: lấy tên sản phẩm.</summary>
    public string? ItemName { get; set; }
    public string? SelfData { get; set; }
}

/// <summary>Yêu cầu đối soát (tạo) hoặc sửa yêu cầu khi phiếu chưa được lập.</summary>
public class SaveCrosscheckRequestDto
{
    /// <summary>Phiên bản phiếu đã đọc; bắt buộc khi sửa, bỏ qua khi tạo.</summary>
    public uint? Version { get; set; }
    /// <summary>Đơn vị đối soát trong danh mục; bỏ trống khi dùng <see cref="ExpertAgencyName"/>.</summary>
    public Guid? ExpertDepartmentId { get; set; }
    /// <summary>Cơ quan ngoài hệ thống; người yêu cầu nhập thay kết quả.</summary>
    public string? ExpertAgencyName { get; set; }
    /// <summary>Mã lĩnh vực: <c>SAFETY_QUALITY</c>, <c>CNS_TECHNICAL</c>, <c>ATM</c>, <c>PLANNING_PROJECT</c>, <c>FINANCE</c>, <c>LEGAL_INSPECTION_AUDIT</c>.</summary>
    public List<string>? Fields { get; set; }
    public string? AddresseeName { get; set; }
    public string? RequestReason { get; set; }
    public List<CrosscheckRequestItemDto>? Items { get; set; }
}

public class CrosscheckResponseItemDto
{
    /// <summary>Dòng của phiếu.</summary>
    public Guid Id { get; set; }
    public string? SourceData { get; set; }
    /// <summary><c>Pass</c> / <c>Exceed</c> / <c>Defect</c>.</summary>
    public string? Result { get; set; }
    /// <summary><c>None</c> / <c>Decrease</c> / <c>RecognizeExceed</c>; bỏ trống = <c>None</c>.</summary>
    public string? AdjustmentType { get; set; }
    public double? AdjustmentPoints { get; set; }
    public string? BasisText { get; set; }
    public string? BasisDocNumber { get; set; }
    public DateOnly? BasisDocDate { get; set; }
    public string? BasisAgency { get; set; }
    public bool NotApplicable { get; set; }
}

/// <summary>Cơ quan đối soát lưu kết quả; dòng không nêu giữ nguyên.</summary>
public class SaveCrosscheckResponseDto
{
    public uint? Version { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public List<CrosscheckResponseItemDto>? Items { get; set; }
}

/// <summary>Gửi xác nhận, xác nhận, trả lại, hủy.</summary>
public class CrosscheckActionDto
{
    public uint? Version { get; set; }
    /// <summary>Bắt buộc khi trả lại và hủy.</summary>
    public string? Reason { get; set; }
}
