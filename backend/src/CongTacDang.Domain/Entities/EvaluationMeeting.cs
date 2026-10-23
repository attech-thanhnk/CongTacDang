using System;
using System.Collections.Generic;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Domain.Entities;

/// <summary>Hội nghị và biên bản Mẫu 12, 13. Không bỏ phiếu điện tử, không lưu phiếu từng người: thư ký nhập kết quả
/// kiểm phiếu tổng hợp theo hồ sơ.</summary>
public class EvaluationMeeting : IAuditableEntity, ISoftDeletable, IVersioned
{
    /// <summary>xmin, dùng cho optimistic concurrency.</summary>
    public uint Version { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public EvaluationPeriod Period { get; set; } = null!;

    /// <summary>Null nếu hội nghị cấp Phòng hoặc cấp Công ty.</summary>
    public Guid? PartyCellId { get; set; }
    public PartyCell? PartyCell { get; set; }

    /// <summary>Phòng tổ chức hội nghị cấp Phòng; null cùng <see cref="PartyCellId"/> = hội nghị cấp Công ty.</summary>
    public Guid? DepartmentId { get; set; }
    public AdministrativeDepartment? Department { get; set; }

    /// <summary>B3A_COLLECTIVE hoặc B4_DECISION; null = không gắn bước.</summary>
    public WorkflowStep? Stage { get; set; }

    /// <summary>M12 = biên bản hội nghị, M13 = biên bản kiểm phiếu.</summary>
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

    /// <summary>Các mục biên bản chưa có cột riêng (jsonb theo mã mục), ví dụ <c>workingRules</c>, <c>attendees</c>,
    /// <c>countingCommittee</c>, <c>ballotsIssued</c>.</summary>
    public string Details { get; set; } = "{}";

    /// <summary>Chỉ tổng hợp kiểm phiếu theo hồ sơ, không lưu người bỏ phiếu.</summary>
    public ICollection<EvaluationMeetingVoteSummary> VoteSummaries { get; set; } = new List<EvaluationMeetingVoteSummary>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

/// <summary>Tổng hợp phiếu theo cán bộ (Mẫu 13), không có thông tin người bỏ phiếu.</summary>
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

    /// <summary>Cột 8 Mẫu 13 "Chưa đánh giá, xếp loại".</summary>
    public int VotesNotRated { get; set; }
    public int InvalidVotes { get; set; }
    public string Notes { get; set; } = string.Empty;

    /// <summary>Các mức + không hợp lệ.</summary>
    public int TotalBallots => VotesExcellent + VotesGood + VotesSatisfactory + VotesUnsatisfactory + VotesNotRated + InvalidVotes;
}
