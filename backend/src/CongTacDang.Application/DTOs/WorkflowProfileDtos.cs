using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Kết quả của một bước do cấp trên thực hiện (đã ghi nhận).</summary>
public class ExternalResultDto
{
    public string Step { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string AuthorityName { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? Comment { get; set; }
    /// <summary>Mức đề xuất/quyết định (mã enum; <c>ChuaXepLoai</c> khi bước không có mức).</summary>
    public string Grade { get; set; } = string.Empty;
    public double? Score { get; set; }
    public Guid? AttachmentId { get; set; }
    public string? AttachmentName { get; set; }
    public string? RecordedByName { get; set; }
    public DateTime RecordedAt { get; set; }
}

/// <summary>Ghi nhận kết quả của bước do cấp trên thực hiện.</summary>
public class ExternalResultRequestDto : WorkflowRequestDto
{
    /// <summary>Cơ quan / cấp thực hiện (bắt buộc).</summary>
    public string? AuthorityName { get; set; }
    public string? DocumentNumber { get; set; }
    public DateTime? DocumentDate { get; set; }
    public string? Comment { get; set; }
    /// <summary>Mức đề xuất/quyết định — bắt buộc với B3a, B3b, B3c, B4.</summary>
    public string? Grade { get; set; }
    /// <summary>Điểm (tùy chọn, 0–100).</summary>
    public double? Score { get; set; }
    /// <summary>Tệp đính kèm đã tải lên (tùy chọn).</summary>
    public Guid? AttachmentId { get; set; }
}

/// <summary>Đổi hồ sơ luồng của một hồ sơ.</summary>
public class ChangeProfileDto
{
    public uint? Version { get; set; }
    public string? WorkflowProfileCode { get; set; }
    /// <summary>Bắt buộc (ghi lịch sử hồ sơ).</summary>
    public string? Reason { get; set; }
}

/// <summary>Một hồ sơ trong yêu cầu đổi hồ sơ luồng hàng loạt.</summary>
public class ProfileChangeItemDto
{
    public Guid RecordId { get; set; }
    public uint? Version { get; set; }
}

/// <summary>Đổi hồ sơ luồng hàng loạt (từng hồ sơ kiểm tra riêng; hồ sơ không đổi được được liệt kê lý do).</summary>
public class BulkChangeProfileDto
{
    public List<ProfileChangeItemDto> Items { get; set; } = new();
    public string? WorkflowProfileCode { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Kết quả đổi hồ sơ luồng hàng loạt.</summary>
public class BulkChangeProfileResultDto
{
    public int Updated { get; set; }
    public List<string> Skipped { get; set; } = new();
}

/// <summary>Một cảnh báo kẹt luồng: hồ sơ sẽ kẹt ở bước vì không ai có quyền thực hiện trong phạm vi của hồ sơ.</summary>
public class ReadinessIssueDto
{
    public Guid RecordId { get; set; }
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string WorkflowProfileCode { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    /// <summary>Internal | External.</summary>
    public string Mode { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public string PermissionName { get; set; } = string.Empty;
    /// <summary>Phạm vi của hồ sơ (Phòng / Chi bộ) cần được bao trùm.</summary>
    public string Scope { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>Kết quả kiểm tra kẹt luồng của kỳ.</summary>
public class PeriodReadinessDto
{
    public Guid PeriodId { get; set; }
    /// <summary>Không có cảnh báo nào.</summary>
    public bool Ready { get; set; }
    /// <summary>Số hồ sơ chưa công bố đã kiểm tra.</summary>
    public int CheckedRecords { get; set; }
    public List<ReadinessIssueDto> Issues { get; set; } = new();
}

/// <summary>Kết quả mở kỳ: kỳ đã mở, hoặc bị chặn vì còn cảnh báo kẹt luồng (<see cref="Readiness"/>).</summary>
public class OpenPeriodOutcome
{
    public EvaluationPeriodDto? Period { get; set; }
    public PeriodReadinessDto? Readiness { get; set; }
    public bool Blocked => Period == null;
}

/// <summary>Mã quyền chọn được làm quyền thực hiện bước (hiển thị ở cấu hình hồ sơ luồng).</summary>
public class StepPermissionOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
