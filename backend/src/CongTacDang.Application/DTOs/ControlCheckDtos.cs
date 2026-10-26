using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

public class ControlCheckItemDto
{
    /// <summary><c>K01</c>…<c>K10</c>.</summary>
    public string ItemCode { get; set; } = string.Empty;
    public string ItemText { get; set; } = string.Empty;
    /// <summary><c>Pass</c> / <c>Fail</c> / <c>NotApplicable</c>; null khi chưa chọn.</summary>
    public string? Result { get; set; }
    public string? ResultName { get; set; }
    public string? Evidence { get; set; }
}

public class ControlCheckRecordDto
{
    public Guid RecordId { get; set; }
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    /// <summary>Còn thuộc phiếu hiệu lực; false khi hồ sơ đã chuyển sang phiếu mới.</summary>
    public bool IsCurrent { get; set; }
    /// <summary>Hồ sơ này đã đổi kết quả hoặc bước sau lúc xác nhận nên cổng của riêng hồ sơ đang khóa.</summary>
    public bool IsStale { get; set; }
    /// <summary>Mức/điểm lúc kiểm soát (làm mới khi gửi xác nhận).</summary>
    public string GradeAtCheck { get; set; } = string.Empty;
    public double ScoreAtCheck { get; set; }
}

public class ControlCheckHistoryDto
{
    public string Action { get; set; } = string.Empty;
    public string ByUserName { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string? Reason { get; set; }
}

public class ControlCheckSummaryDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public Guid? PartyCellId { get; set; }
    /// <summary><c>Draft</c> / <c>Submitted</c> / <c>Confirmed</c> / <c>Cancelled</c>.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    /// <summary><c>Eligible</c> / <c>NeedsWork</c>; null khi chưa kết luận.</summary>
    public string? Conclusion { get; set; }
    public string? ConclusionName { get; set; }
    public int Revision { get; set; }
    public int RecordCount { get; set; }
    public string PerformedByName { get; set; } = string.Empty;
    public DateTime? PerformedAt { get; set; }
    public string? ConfirmedByName { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    /// <summary>Phiếu đã xác nhận nhưng hồ sơ trong phiếu đã đổi kết quả hoặc bước sau lúc xác nhận.</summary>
    public bool IsStale { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ControlCheckDto : ControlCheckSummaryDto
{
    public int ItemsVersion { get; set; }
    public string? NeedsWorkContent { get; set; }
    public string? ReturnReason { get; set; }
    public List<ControlCheckItemDto> Items { get; set; } = new();
    public List<ControlCheckRecordDto> Records { get; set; } = new();
    public List<ControlCheckHistoryDto> History { get; set; } = new();
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanConfirm { get; set; }
    public bool CanReturn { get; set; }
    public bool CanReopen { get; set; }
    public bool CanCancel { get; set; }
}

/// <summary>Hồ sơ có thể đưa vào phiếu kiểm soát mới.</summary>
public class ControlCheckCandidateDto
{
    public Guid RecordId { get; set; }
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    public string WorkflowProfileCode { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public double Score { get; set; }
}

public class CreateControlCheckDto
{
    public Guid PeriodId { get; set; }
    public string? GroupName { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PartyCellId { get; set; }
    public List<Guid> RecordIds { get; set; } = new();
}

public class ControlCheckItemInputDto
{
    public string? ItemCode { get; set; }
    public string? Result { get; set; }
    public string? Evidence { get; set; }
}

/// <summary>Lưu bản nháp: <see cref="Version"/> là phiên bản phiếu đã đọc; trường null = giữ nguyên.</summary>
public class UpdateControlCheckDto
{
    public uint? Version { get; set; }
    public string? GroupName { get; set; }
    public List<ControlCheckItemInputDto>? Items { get; set; }
    /// <summary><c>Eligible</c> / <c>NeedsWork</c>; chuỗi rỗng = bỏ kết luận.</summary>
    public string? Conclusion { get; set; }
    public string? NeedsWorkContent { get; set; }
    /// <summary>Danh sách hồ sơ đầy đủ sau khi sửa; null = giữ nguyên.</summary>
    public List<Guid>? RecordIds { get; set; }
}

/// <summary>Thao tác trên phiếu: Version bắt buộc; Reason bắt buộc với trả lại, mở lại, hủy.</summary>
public class ControlCheckActionDto
{
    public uint? Version { get; set; }
    public string? Reason { get; set; }
}
