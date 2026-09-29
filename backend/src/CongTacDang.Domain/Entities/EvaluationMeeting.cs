using System;
using System.Collections.Generic;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Hội nghị đánh giá và biên bản Mẫu 12, 13. Hệ thống không tổ chức bỏ phiếu điện tử và không lưu phiếu của từng người:
/// chỉ lưu kết quả kiểm phiếu tổng hợp theo hồ sơ (<see cref="EvaluationMeetingVoteSummary"/>) do thư ký nhập (B-03).
/// </summary>
public class EvaluationMeeting : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>Phiên bản bản ghi (xmin) cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public EvaluationPeriod Period { get; set; } = null!;

    /// <summary>Chi bộ tổ chức hội nghị (null nếu hội nghị cấp Phòng hoặc cấp Công ty).</summary>
    public Guid? PartyCellId { get; set; }
    public PartyCell? PartyCell { get; set; }

    /// <summary>
    /// Phòng/đơn vị tổ chức hội nghị tập thể lãnh đạo cấp Phòng (task 12) — để thư ký tập thể phạm vi Phòng lập được biên bản.
    /// Null cùng <see cref="PartyCellId"/> = hội nghị cấp Công ty (chỉ phạm vi Toàn công ty).
    /// </summary>
    public Guid? DepartmentId { get; set; }
    public AdministrativeDepartment? Department { get; set; }

    /// <summary>Bước của luồng mà hội nghị phục vụ: <see cref="WorkflowStep.B3A_COLLECTIVE"/> hoặc <see cref="WorkflowStep.B4_DECISION"/> (null: biên bản không gắn bước cụ thể).</summary>
    public WorkflowStep? Stage { get; set; }

    /// <summary>Mẫu biên bản chính: M12 là biên bản hội nghị, M13 là biên bản kiểm phiếu.</summary>
    public string FormCode { get; set; } = "M12";
    public string MeetingType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
    public int InvitedCount { get; set; }
    public int PresentCount { get; set; }
    public int AbsentCount { get; set; }
    public string AbsentReasons { get; set; } = string.Empty;
    public Guid? ChairId { get; set; }
    public string ChairName { get; set; } = string.Empty;
    public Guid? SecretaryId { get; set; }
    public string SecretaryName { get; set; } = string.Empty;
    public string MinutesContent { get; set; } = string.Empty;
    public string OutcomeContent { get; set; } = string.Empty;
    public string VoteCountingContent { get; set; } = string.Empty;

    /// <summary>
    /// Các mục của biên bản Mẫu 12 chưa có cột riêng (jsonb theo mã mục): <c>workingRules</c> (Quy chế làm việc của … nhiệm kỳ …),
    /// <c>reportingUnit</c> (cơ quan, đơn vị báo cáo nhiệm vụ trọng tâm), <c>chairTitle</c>, <c>secretaryTitle</c> (chức vụ Đảng,
    /// chính quyền), <c>attendees</c> (mục 3.2: <c>[{ "name", "title" }]</c>).
    /// </summary>
    public string Details { get; set; } = "{}";

    /// <summary>Không lưu UserId của người bỏ phiếu; chỉ lưu tổng hợp kiểm phiếu theo hồ sơ.</summary>
    public ICollection<EvaluationMeetingVoteSummary> VoteSummaries { get; set; } = new List<EvaluationMeetingVoteSummary>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Tổng hợp phiếu theo từng cán bộ trong biên bản kiểm phiếu Mẫu 13 (không có thông tin người bỏ phiếu).</summary>
public class EvaluationMeetingVoteSummary
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeetingId { get; set; }
    public EvaluationMeeting Meeting { get; set; } = null!;
    public Guid RecordId { get; set; }
    public EvaluationRecord Record { get; set; } = null!;
    public int VotesExcellent { get; set; }
    public int VotesGood { get; set; }
    public int VotesSatisfactory { get; set; }
    public int VotesUnsatisfactory { get; set; }
    public int InvalidVotes { get; set; }
    public string Notes { get; set; } = string.Empty;

    /// <summary>Tổng số phiếu đã ghi (các mức + không hợp lệ).</summary>
    public int TotalBallots => VotesExcellent + VotesGood + VotesSatisfactory + VotesUnsatisfactory + InvalidVotes;
}
