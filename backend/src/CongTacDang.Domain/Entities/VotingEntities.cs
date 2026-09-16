using System;
using System.Collections.Generic;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Entities;

public class VotingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PeriodId { get; set; }
    public EvaluationPeriod? Period { get; set; }

    public Guid PartyCellId { get; set; } // Tổ chức tại Chi bộ nào
    public PartyCell? PartyCell { get; set; }

    public string Title { get; set; } = string.Empty; // Phiên họp bỏ phiếu kín chi bộ
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    public bool IsClosed { get; set; } = false;

    public int TotalEligibleVoters { get; set; } // Tổng số người được triệu tập
    public int TotalVoted { get; set; }          // Số người đã thực tế bỏ phiếu

    public ICollection<SecretBallot> Ballots { get; set; } = new List<SecretBallot>();
    public ICollection<VotingResult> Results { get; set; } = new List<VotingResult>();
}

// Lá phiếu nặc danh hoàn toàn: KHÔNG lưu UserId người bỏ phiếu
public class SecretBallot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public VotingSession? Session { get; set; }

    public Guid TargetMemberId { get; set; } // Cán bộ được bỏ phiếu
    public EvaluationGrade VotedGrade { get; set; } // Mức được bình bầu
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}

// Bảng kiểm phiếu tổng hợp (Biên bản Mẫu 12, 13)
public class VotingResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public VotingSession? Session { get; set; }

    public Guid TargetMemberId { get; set; }
    public string TargetMemberName { get; set; } = string.Empty;

    public int ExcellentVotes { get; set; } // Số phiếu Xuất sắc
    public int GoodVotes { get; set; }      // Số phiếu Tốt
    public int PassVotes { get; set; }      // Số phiếu Hoàn thành
    public int FailVotes { get; set; }      // Số phiếu Không hoàn thành

    public decimal ExcellentPercentage { get; set; }
    public decimal GoodPercentage { get; set; }
    public decimal PassPercentage { get; set; }
    public decimal FailPercentage { get; set; }

    public EvaluationGrade SuggestedGrade { get; set; } // Mức đề xuất từ kết quả bỏ phiếu
}
