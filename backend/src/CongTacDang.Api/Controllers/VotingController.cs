using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VotingController : ControllerBase
{
    private readonly CongTacDangDbContext _db;

    public VotingController(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Tạo phiên bỏ phiếu kín cho Chi bộ / Hội nghị lãnh đạo ATTECH (Mẫu 11)</summary>
    [HttpPost("create-session")]
    public async Task<IActionResult> CreateSession([FromBody] CreateVotingSessionDto input)
    {
        var session = new VotingSession
        {
            PeriodId = input.PeriodId,
            PartyCellId = input.PartyCellId,
            Title = input.Title,
            TotalEligibleVoters = input.TotalEligibleVoters,
            StartedAt = DateTime.UtcNow,
            IsClosed = false
        };

        await _db.VotingSessions.AddAsync(session);
        await _db.SaveChangesAsync();
        return Ok(session);
    }

    /// <summary>Gửi lá phiếu nặc danh (Bảo mật: Không lưu UserId người bỏ phiếu)</summary>
    [HttpPost("cast-ballot")]
    public async Task<IActionResult> CastBallot([FromBody] CastBallotDto input)
    {
        var session = await _db.VotingSessions.FirstOrDefaultAsync(s => s.Id == input.SessionId);
        if (session == null) return NotFound("Phiên bỏ phiếu không tồn tại.");
        if (session.IsClosed) return BadRequest("Phiên bỏ phiếu đã kết thúc.");

        // Lưu lá phiếu nặc danh
        foreach (var vote in input.Votes)
        {
            var ballot = new SecretBallot
            {
                SessionId = session.Id,
                TargetMemberId = vote.TargetMemberId,
                VotedGrade = vote.Grade,
                VotedAt = DateTime.UtcNow
            };
            await _db.SecretBallots.AddAsync(ballot);
        }

        session.TotalVoted++;
        await _db.SaveChangesAsync();
        return Ok(new { Message = "Đã bỏ phiếu thành công." });
    }

    /// <summary>Đóng hòm phiếu và TỰ ĐỘNG KIỂM PHIẾU sinh Biên bản Mẫu 12, 13</summary>
    [HttpPost("{sessionId}/close-and-tally")]
    public async Task<IActionResult> CloseAndTally(Guid sessionId)
    {
        var session = await _db.VotingSessions
            .Include(s => s.Ballots)
            .Include(s => s.Results)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return NotFound();

        session.IsClosed = true;
        session.ClosedAt = DateTime.UtcNow;

        _db.VotingResults.RemoveRange(session.Results);

        // Nhóm các phiếu theo từng cán bộ được đánh giá
        var targetMemberIds = session.Ballots.Select(b => b.TargetMemberId).Distinct().ToList();
        var members = await _db.PartyMemberProfiles.Where(m => targetMemberIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.FullName);

        foreach (var memberId in targetMemberIds)
        {
            var memberBallots = session.Ballots.Where(b => b.TargetMemberId == memberId).ToList();
            int total = memberBallots.Count;
            if (total == 0) continue;

            int excellent = memberBallots.Count(b => b.VotedGrade == EvaluationGrade.HoanThanhXuatSac);
            int good = memberBallots.Count(b => b.VotedGrade == EvaluationGrade.HoanThanhTot);
            int pass = memberBallots.Count(b => b.VotedGrade == EvaluationGrade.HoanThanh);
            int fail = memberBallots.Count(b => b.VotedGrade == EvaluationGrade.KhongHoanThanh);

            var result = new VotingResult
            {
                SessionId = session.Id,
                TargetMemberId = memberId,
                TargetMemberName = members.GetValueOrDefault(memberId, "Cán bộ"),
                ExcellentVotes = excellent,
                GoodVotes = good,
                PassVotes = pass,
                FailVotes = fail,
                ExcellentPercentage = Math.Round((decimal)excellent / total * 100m, 1),
                GoodPercentage = Math.Round((decimal)good / total * 100m, 1),
                PassPercentage = Math.Round((decimal)pass / total * 100m, 1),
                FailPercentage = Math.Round((decimal)fail / total * 100m, 1),
                SuggestedGrade = excellent >= total * 0.5m ? EvaluationGrade.HoanThanhXuatSac :
                                 (excellent + good) >= total * 0.5m ? EvaluationGrade.HoanThanhTot :
                                 (excellent + good + pass) >= total * 0.5m ? EvaluationGrade.HoanThanh : EvaluationGrade.KhongHoanThanh
            };

            session.Results.Add(result);
        }

        await _db.SaveChangesAsync();
        return Ok(session.Results);
    }
}

public class CreateVotingSessionDto
{
    public Guid PeriodId { get; set; }
    public Guid PartyCellId { get; set; }
    public string Title { get; set; } = string.Empty;
    public int TotalEligibleVoters { get; set; }
}

public class CastBallotDto
{
    public Guid SessionId { get; set; }
    public System.Collections.Generic.List<MemberVoteDto> Votes { get; set; } = new();
}

public class MemberVoteDto
{
    public Guid TargetMemberId { get; set; }
    public EvaluationGrade Grade { get; set; }
}
