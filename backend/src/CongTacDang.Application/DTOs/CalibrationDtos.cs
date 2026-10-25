using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

#region Đọc

/// <summary>Dòng danh sách biên bản hiệu chuẩn.</summary>
public class CalibrationSessionListItemDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    public string GroupName { get; set; } = string.Empty;
    /// <summary>Draft | Concluded | Void.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public DateTime? HeldAt { get; set; }
    public int RecordCount { get; set; }
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime? ConcludedAt { get; set; }
}

/// <summary>Hồ sơ trong biên bản, kèm các mốc điểm/mức để so sánh cạnh nhau.</summary>
public class CalibrationRecordDto
{
    public Guid RecordId { get; set; }
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    public string WorkflowProfileCode { get; set; } = string.Empty;
    /// <summary>Bước hiện tại của hồ sơ (RecordStatus).</summary>
    public string RecordStatus { get; set; } = string.Empty;

    /// <summary>Mốc trước hiệu chuẩn đã chụp vào biên bản.</summary>
    public string GradeBefore { get; set; } = string.Empty;
    public double? ScoreBefore { get; set; }

    public double? SelfScore { get; set; }
    public string SelfGrade { get; set; } = string.Empty;
    public string CollectiveGrade { get; set; } = string.Empty;
    public double? AppraisalScore { get; set; }
    public string AppraisalGrade { get; set; } = string.Empty;

    /// <summary>Kết quả hiện đang áp vào hồ sơ (rỗng/null = chưa áp).</summary>
    public double? CalibratedScore { get; set; }
    public string CalibratedGrade { get; set; } = string.Empty;

    /// <summary>Mốc trước hiệu chuẩn trên hồ sơ đã khác ảnh chụp: lưu lại biên bản để cập nhật.</summary>
    public bool SnapshotOutdated { get; set; }
}

public class CalibrationAttendeeDto
{
    public int Order { get; set; }
    public string FullName { get; set; } = string.Empty;
    public Guid? MemberId { get; set; }
    public string? Capacity { get; set; }
    /// <summary>Null = thống nhất.</summary>
    public string? DissentNote { get; set; }
}

/// <summary>Dòng "Kết quả rà soát chung"; số lượng, tỷ lệ tính từ hồ sơ, chỉ hiển thị, không so với chỉ tiêu.</summary>
public class CalibrationSummaryRowDto
{
    /// <summary>Tên EvaluationGrade; "TOTAL" cho dòng tổng.</summary>
    public string GradeCode { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public int CountBefore { get; set; }
    public double? PercentBefore { get; set; }
    public int CountAfter { get; set; }
    public double? PercentAfter { get; set; }
    public string? NotableDifference { get; set; }
    public string? PreliminaryConclusion { get; set; }
}

public class CalibrationCaseDto
{
    public int Order { get; set; }
    public Guid RecordId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string GradeBefore { get; set; } = string.Empty;
    public double? ScoreBefore { get; set; }
    public string? Clarification { get; set; }
    /// <summary>Rỗng = giữ mức trước.</summary>
    public string GradeAfter { get; set; } = string.Empty;
    public double? ScoreAfter { get; set; }
    public string? Basis { get; set; }
    public string? ResponsibleName { get; set; }
    public Guid? ResponsibleMemberId { get; set; }
    /// <summary>Điểm hoặc mức sau khác trước.</summary>
    public bool Changed { get; set; }
}

public class CalibrationSessionDto
{
    public Guid Id { get; set; }
    public uint Version { get; set; }
    public Guid PeriodId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public Guid? PartyCellId { get; set; }
    public string? PartyCellName { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public DateTime? HeldAt { get; set; }
    public string? Location { get; set; }
    public string ChairName { get; set; } = string.Empty;
    public Guid? ChairMemberId { get; set; }
    public string SecretaryName { get; set; } = string.Empty;
    public Guid? SecretaryMemberId { get; set; }
    /// <summary>Phạm vi, số lượng hồ sơ: gợi ý sinh từ danh sách khi người dùng chưa nhập.</summary>
    public string? ScopeNote { get; set; }
    public string ScopeSuggestion { get; set; } = string.Empty;
    public string? ExtraConclusion { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int Revision { get; set; }
    public string PreparedByName { get; set; } = string.Empty;
    public DateTime PreparedAt { get; set; }
    public string? ConcludedByName { get; set; }
    public DateTime? ConcludedAt { get; set; }
    public string? ReopenReason { get; set; }
    public string? VoidReason { get; set; }

    public List<CalibrationRecordDto> Records { get; set; } = new();
    public List<CalibrationAttendeeDto> Attendees { get; set; } = new();
    public List<CalibrationSummaryRowDto> Summary { get; set; } = new();
    public List<CalibrationCaseDto> Cases { get; set; } = new();

    /// <summary>Cờ hành động cho người xem; giao diện hiện nút theo cờ, không tự suy luật.</summary>
    public bool CanEdit { get; set; }
    public bool CanConclude { get; set; }
    public bool CanReopen { get; set; }
    public bool CanVoid { get; set; }
    /// <summary>Lý do chốt chưa được (thiếu nội dung, cùng người lập...), null nếu chốt được.</summary>
    public string? ConcludeBlockedReason { get; set; }
}

/// <summary>Hồ sơ có thể đưa vào biên bản mới.</summary>
public class CalibrationCandidateDto
{
    public Guid RecordId { get; set; }
    public Guid MemberId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? PartyCellName { get; set; }
    public string WorkflowProfileCode { get; set; } = string.Empty;
    public string WorkflowProfileName { get; set; } = string.Empty;
    public string RecordStatus { get; set; } = string.Empty;
    public string GradeBefore { get; set; } = string.Empty;
    public double? ScoreBefore { get; set; }
}

public class CalibrationHistoryDto
{
    public string Action { get; set; } = string.Empty;
    public string ActionName { get; set; } = string.Empty;
    public string ByUserName { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public string? Reason { get; set; }
}

#endregion

#region Ghi

public class CalibrationAttendeeInputDto
{
    public string? FullName { get; set; }
    public Guid? MemberId { get; set; }
    public string? Capacity { get; set; }
    public string? DissentNote { get; set; }
}

public class CalibrationSummaryRowInputDto
{
    /// <summary>Tên EvaluationGrade.</summary>
    public string? GradeCode { get; set; }
    public string? NotableDifference { get; set; }
    public string? PreliminaryConclusion { get; set; }
}

public class CalibrationCaseInputDto
{
    public Guid RecordId { get; set; }
    public string? Clarification { get; set; }
    /// <summary>Rỗng = giữ mức trước.</summary>
    public string? GradeAfter { get; set; }
    public double? ScoreAfter { get; set; }
    public string? Basis { get; set; }
    public string? ResponsibleName { get; set; }
    public Guid? ResponsibleMemberId { get; set; }
}

public class CreateCalibrationSessionDto
{
    public Guid PeriodId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PartyCellId { get; set; }
    public string? GroupName { get; set; }
    public List<Guid> RecordIds { get; set; } = new();
}

/// <summary>Lưu toàn bộ nội dung bản nháp; danh sách gửi lên thay thế danh sách cũ.</summary>
public class UpdateCalibrationSessionDto
{
    /// <summary>Phiên bản biên bản đã đọc.</summary>
    public uint? Version { get; set; }
    public string? GroupName { get; set; }
    public DateTime? HeldAt { get; set; }
    public string? Location { get; set; }
    public string? ChairName { get; set; }
    public Guid? ChairMemberId { get; set; }
    public string? SecretaryName { get; set; }
    public Guid? SecretaryMemberId { get; set; }
    public string? ScopeNote { get; set; }
    public string? ExtraConclusion { get; set; }
    public List<Guid> RecordIds { get; set; } = new();
    public List<CalibrationAttendeeInputDto> Attendees { get; set; } = new();
    public List<CalibrationSummaryRowInputDto> Summary { get; set; } = new();
    public List<CalibrationCaseInputDto> Cases { get; set; } = new();
}

public class ConcludeCalibrationDto
{
    public uint? Version { get; set; }
}

/// <summary>Mở lại hoặc hủy biên bản; bắt buộc lý do.</summary>
public class CalibrationReasonDto
{
    public uint? Version { get; set; }
    public string? Reason { get; set; }
}

#endregion
