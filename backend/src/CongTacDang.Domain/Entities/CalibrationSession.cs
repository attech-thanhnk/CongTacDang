using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

public enum CalibrationStatus
{
    Draft = 0,

    /// <summary>Đã chốt và áp kết quả vào hồ sơ.</summary>
    Concluded = 1,

    /// <summary>Hủy có lý do; các hồ sơ được giải phóng cho biên bản khác.</summary>
    Void = 2
}

/// <summary>Biên bản hiệu chuẩn kết quả đánh giá (Mẫu 20) cho một nhóm hồ sơ tương đồng.</summary>
public class CalibrationSession : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PeriodId { get; set; }

    public EvaluationPeriod Period { get; set; } = null!;

    /// <summary>Đơn vị lập biên bản; dùng làm đích kiểm tra quyền xem danh sách.</summary>
    public Guid? DepartmentId { get; set; }

    public AdministrativeDepartment? Department { get; set; }

    public Guid? PartyCellId { get; set; }

    public PartyCell? PartyCell { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public DateTime? HeldAt { get; set; }

    public string? Location { get; set; }

    public string ChairName { get; set; } = string.Empty;

    public Guid? ChairMemberId { get; set; }

    public string SecretaryName { get; set; } = string.Empty;

    public Guid? SecretaryMemberId { get; set; }

    /// <summary>Phần "phạm vi, số lượng hồ sơ"; sinh từ danh sách hồ sơ, sửa được.</summary>
    public string? ScopeNote { get; set; }

    /// <summary>Ghi chú thêm ở mục kết luận; bắt buộc khi biên bản chỉ có một hồ sơ.</summary>
    public string? ExtraConclusion { get; set; }

    public CalibrationStatus Status { get; set; } = CalibrationStatus.Draft;

    /// <summary>Tăng mỗi lần mở lại biên bản đã chốt.</summary>
    public int Revision { get; set; } = 1;

    public Guid? PreparedById { get; set; }

    public string PreparedByName { get; set; } = string.Empty;

    public DateTime PreparedAt { get; set; } = DateTime.UtcNow;

    public Guid? ConcludedById { get; set; }

    public string? ConcludedByName { get; set; }

    public DateTime? ConcludedAt { get; set; }

    public string? ReopenReason { get; set; }

    public string? VoidReason { get; set; }

    public ICollection<CalibrationRecord> Records { get; set; } = new List<CalibrationRecord>();

    public ICollection<CalibrationAttendee> Attendees { get; set; } = new List<CalibrationAttendee>();

    public ICollection<CalibrationSummaryRow> SummaryRows { get; set; } = new List<CalibrationSummaryRow>();

    public ICollection<CalibrationCase> Cases { get; set; } = new List<CalibrationCase>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Hồ sơ nằm trong phạm vi biên bản, kèm điểm/mức trước hiệu chuẩn.</summary>
public class CalibrationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }

    public CalibrationSession Session { get; set; } = null!;

    public Guid RecordId { get; set; }

    public EvaluationRecord Record { get; set; } = null!;

    /// <summary>False khi biên bản bị hủy; một hồ sơ chỉ thuộc tối đa một biên bản còn hiệu lực.</summary>
    public bool IsActive { get; set; } = true;

    public EvaluationGrade GradeBefore { get; set; }

    public double? ScoreBefore { get; set; }
}

/// <summary>Thành phần dự họp; ý kiến bảo lưu để trống = thống nhất.</summary>
public class CalibrationAttendee
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }

    public CalibrationSession Session { get; set; } = null!;

    public int Order { get; set; }

    public string FullName { get; set; } = string.Empty;

    public Guid? MemberId { get; set; }

    /// <summary>Thành phần, tư cách tham dự.</summary>
    public string? Capacity { get; set; }

    public string? DissentNote { get; set; }
}

/// <summary>Nội dung chữ của dòng tổng hợp theo mức; số lượng và tỷ lệ luôn tính từ hồ sơ.</summary>
public class CalibrationSummaryRow
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }

    public CalibrationSession Session { get; set; } = null!;

    /// <summary>Tên <see cref="EvaluationGrade"/>.</summary>
    public string GradeCode { get; set; } = string.Empty;

    public string? NotableDifference { get; set; }

    public string? PreliminaryConclusion { get; set; }
}

/// <summary>Trường hợp thảo luận của một hồ sơ trong biên bản.</summary>
public class CalibrationCase
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }

    public CalibrationSession Session { get; set; } = null!;

    public int Order { get; set; }

    /// <summary>Tham chiếu mềm tới hồ sơ trong <see cref="CalibrationSession.Records"/>.</summary>
    public Guid RecordId { get; set; }

    public EvaluationGrade GradeBefore { get; set; }

    public double? ScoreBefore { get; set; }

    public string? Clarification { get; set; }

    /// <summary>Null = giữ mức trước hiệu chuẩn.</summary>
    public EvaluationGrade? GradeAfter { get; set; }

    /// <summary>Null = giữ điểm trước hiệu chuẩn.</summary>
    public double? ScoreAfter { get; set; }

    public string? Basis { get; set; }

    public string? ResponsibleName { get; set; }

    public Guid? ResponsibleMemberId { get; set; }
}
