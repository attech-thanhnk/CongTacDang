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
    /// <summary>Mã enum; <c>ChuaXepLoai</c> khi bước không có mức.</summary>
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
    /// <summary>Bắt buộc với B3a, B3b, B3c, B4.</summary>
    public string? Grade { get; set; }
    /// <summary>Tùy chọn, 0–100.</summary>
    public double? Score { get; set; }
    public Guid? AttachmentId { get; set; }
    /// <summary>Chỉ bước B4; tùy chọn, cột 13 Mẫu 14.</summary>
    public string? CadreWorkProposal { get; set; }
}

/// <summary>Đổi diện quyết định của một hồ sơ.</summary>
public class ChangeProfileDto
{
    public uint? Version { get; set; }
    public string? WorkflowProfileCode { get; set; }
    /// <summary>Bắt buộc (ghi lịch sử hồ sơ).</summary>
    public string? Reason { get; set; }
}

public class ProfileChangeItemDto
{
    public Guid RecordId { get; set; }
    public uint? Version { get; set; }
}

/// <summary>Từng hồ sơ kiểm tra riêng; hồ sơ không đổi được thì liệt kê lý do.</summary>
public class BulkChangeProfileDto
{
    public List<ProfileChangeItemDto> Items { get; set; } = new();
    public string? WorkflowProfileCode { get; set; }
    public string? Reason { get; set; }
}

public class BulkChangeProfileResultDto
{
    public int Updated { get; set; }
    public List<string> Skipped { get; set; } = new();
}

/// <summary>Cảnh báo kẹt luồng: không ai có quyền thực hiện bước trong phạm vi của hồ sơ.</summary>
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
    /// <summary>Vai trò đang chứa quyền này; quản trị gán một trong các vai trò đó.</summary>
    public List<string> RoleNames { get; set; } = new();
    /// <summary>Nơi cần gán; gán ở cấp trên hoặc Toàn công ty cũng được.</summary>
    public string ScopeName { get; set; } = string.Empty;
    /// <summary>Phạm vi bản gán cần bao trùm.</summary>
    public string Scope { get; set; } = string.Empty;
    /// <summary>Để mở màn hình "Người thực hiện" đúng đơn vị.</summary>
    public Guid? DepartmentId { get; set; }
    public Guid? PartyCellId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class PeriodReadinessDto
{
    public Guid PeriodId { get; set; }
    /// <summary>Không có cảnh báo nào.</summary>
    public bool Ready { get; set; }
    /// <summary>Số hồ sơ chưa công bố đã kiểm tra.</summary>
    public int CheckedRecords { get; set; }
    public List<ReadinessIssueDto> Issues { get; set; } = new();
    /// <summary>Chỗ dễ gây kẹt hoặc chưa đúng thực tế; không chặn mở kỳ.</summary>
    public List<DataReviewItemDto> DataReview { get; set; } = new();
}

public class DataReviewItemDto
{
    /// <summary>DirectInPartyCommittee | MissingOrg | SinglePerformer.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public List<string> Names { get; set; } = new();
}

/// <summary>Kết quả mở kỳ: kỳ đã mở, hoặc bị chặn vì còn cảnh báo kẹt luồng (<see cref="Readiness"/>).</summary>
public class OpenPeriodOutcome
{
    public EvaluationPeriodDto? Period { get; set; }
    public PeriodReadinessDto? Readiness { get; set; }
    public bool Blocked => Period == null;
}

/// <summary>Mã quyền chọn được làm quyền thực hiện bước.</summary>
public class StepPermissionOptionDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
