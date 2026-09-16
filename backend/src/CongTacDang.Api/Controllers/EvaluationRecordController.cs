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
public class EvaluationRecordController : ControllerBase
{
    private readonly CongTacDangDbContext _db;

    public EvaluationRecordController(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy danh sách hồ sơ đánh giá của một kỳ (phục vụ thẩm định & kiểm soát trần 20%)</summary>
    [HttpGet("period/{periodId}")]
    public async Task<IActionResult> GetByPeriod(Guid periodId, [FromQuery] Guid? cellId)
    {
        var query = _db.EvaluationRecords
            .Include(r => r.Member)
            .ThenInclude(m => m!.PartyCell)
            .Include(r => r.Member)
            .ThenInclude(m => m!.Department)
            .Include(r => r.Tasks)
            .Where(r => r.PeriodId == periodId);

        if (cellId.HasValue)
        {
            query = query.Where(r => r.Member != null && r.Member.PartyCellId == cellId);
        }

        var records = await query.ToListAsync();

        // Tính thống kê tỷ lệ Xuất sắc thời gian thực
        var totalGraded = records.Count(r => r.FinalApprovedGrade != EvaluationGrade.ChuaXepLoai);
        var excellentCount = records.Count(r => r.FinalApprovedGrade == EvaluationGrade.HoanThanhXuatSac);
        var goodCount = records.Count(r => r.FinalApprovedGrade == EvaluationGrade.HoanThanhTot);
        var excellentPercent = goodCount > 0 ? (decimal)excellentCount / goodCount * 100m : 0m;

        return Ok(new
        {
            Records = records,
            Statistics = new
            {
                TotalGraded = totalGraded,
                ExcellentCount = excellentCount,
                GoodCount = goodCount,
                ExcellentPercentOverGood = Math.Round(excellentPercent, 1),
                IsExceededQuota = excellentPercent > 20m // Cảnh báo vượt trần 20%
            }
        });
    }

    /// <summary>Lấy chi tiết hồ sơ cá nhân (Mẫu 01 & Mẫu 02)</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDetail(Guid id)
    {
        var record = await _db.EvaluationRecords
            .Include(r => r.Member)
            .ThenInclude(m => m!.PartyCell)
            .Include(r => r.Tasks.OrderBy(t => t.OrderNumber))
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record == null) return NotFound();
        return Ok(record);
    }

    /// <summary>MẪU 01: Đăng ký danh mục 3-7 nhiệm vụ đầu kỳ</summary>
    [HttpPost("submit-tasks")]
    public async Task<IActionResult> SubmitTasks([FromBody] SubmitTasksDto input)
    {
        var record = await _db.EvaluationRecords
            .Include(r => r.Tasks)
            .FirstOrDefaultAsync(r => r.PeriodId == input.PeriodId && r.MemberId == input.MemberId);

        if (record == null)
        {
            record = new EvaluationRecord
            {
                PeriodId = input.PeriodId,
                MemberId = input.MemberId,
                Status = RecordStatus.TasksSubmitted
            };
            await _db.EvaluationRecords.AddAsync(record);
        }
        else
        {
            record.Status = RecordStatus.TasksSubmitted;
            _db.EvaluationTasks.RemoveRange(record.Tasks);
        }

        int order = 1;
        decimal totalWeight = 0;
        foreach (var task in input.Tasks)
        {
            totalWeight += task.Weight;
            record.Tasks.Add(new EvaluationTask
            {
                OrderNumber = order++,
                TaskName = task.TaskName,
                Axis = task.Axis,
                TargetOutput = task.TargetOutput,
                CompletionQuarter = task.CompletionQuarter,
                StandardBenchmark = task.StandardBenchmark,
                ExceededBenchmark = task.ExceededBenchmark,
                Weight = task.Weight,
                WeightA = task.WeightA,
                WeightB = task.WeightB,
                WeightC = task.WeightC,
                WeightD = task.WeightD
            });
        }

        await _db.SaveChangesAsync();
        return Ok(record);
    }

    /// <summary>MẪU 02: Tự chấm điểm với công thức tính A-B-C-D tự động</summary>
    [HttpPost("{id}/self-evaluate")]
    public async Task<IActionResult> SelfEvaluate(Guid id, [FromBody] SelfEvaluateDto input)
    {
        var record = await _db.EvaluationRecords
            .Include(r => r.Tasks)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (record == null) return NotFound();

        // 1. Điểm Tiêu chí chung (30đ)
        record.SelfScoreTc1 = input.SelfScoreTc1;
        record.SelfScoreTc2 = input.SelfScoreTc2;
        record.SelfScoreTc3 = input.SelfScoreTc3;
        record.SelfExplanation = input.SelfExplanation;

        // 2. Điểm Nhiệm vụ A-B-C-D (70đ)
        decimal totalTaskScore = 0;
        foreach (var item in input.TaskScores)
        {
            var task = record.Tasks.FirstOrDefault(t => t.Id == item.TaskId);
            if (task != null)
            {
                task.ScoreA_Percent = item.ScoreA_Percent;
                task.ScoreB_Percent = item.ScoreB_Percent;
                task.ScoreC_Percent = item.ScoreC_Percent;
                task.ScoreD_Percent = item.ScoreD_Percent;
                task.IsExceededStandard = item.IsExceededStandard;
                task.EvidenceDescription = item.EvidenceDescription;

                // Công thức tính điểm chuẩn theo 03-HD/TVĐU:
                // ResultPercent = (A * wA + B * wB + C * wC + D * wD) / 100
                decimal resultPercent = (task.ScoreA_Percent * task.WeightA +
                                        task.ScoreB_Percent * task.WeightB +
                                        task.ScoreC_Percent * task.WeightC +
                                        task.ScoreD_Percent * task.WeightD) / 100m;

                task.FinalTaskScore = Math.Round(resultPercent * task.Weight / 100m, 1);
                totalTaskScore += task.FinalTaskScore;
            }
        }

        record.SelfTaskScore = Math.Min(totalTaskScore, 70m); // Tối đa 70đ
        record.SelfGrade = input.SelfSuggestedGrade;
        record.Status = RecordStatus.SelfEvaluated;

        await _db.SaveChangesAsync();
        return Ok(record);
    }
}

public class SubmitTasksDto
{
    public Guid PeriodId { get; set; }
    public Guid MemberId { get; set; }
    public System.Collections.Generic.List<TaskDto> Tasks { get; set; } = new();
}

public class TaskDto
{
    public string TaskName { get; set; } = string.Empty;
    public TaskResultAxis Axis { get; set; }
    public string TargetOutput { get; set; } = string.Empty;
    public string CompletionQuarter { get; set; } = string.Empty;
    public string StandardBenchmark { get; set; } = string.Empty;
    public string ExceededBenchmark { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public decimal WeightA { get; set; }
    public decimal WeightB { get; set; }
    public decimal WeightC { get; set; }
    public decimal WeightD { get; set; }
}

public class SelfEvaluateDto
{
    public decimal SelfScoreTc1 { get; set; }
    public decimal SelfScoreTc2 { get; set; }
    public decimal SelfScoreTc3 { get; set; }
    public string SelfExplanation { get; set; } = string.Empty;
    public EvaluationGrade SelfSuggestedGrade { get; set; }
    public System.Collections.Generic.List<TaskScoreDto> TaskScores { get; set; } = new();
}

public class TaskScoreDto
{
    public Guid TaskId { get; set; }
    public decimal ScoreA_Percent { get; set; }
    public decimal ScoreB_Percent { get; set; }
    public decimal ScoreC_Percent { get; set; }
    public decimal ScoreD_Percent { get; set; }
    public bool IsExceededStandard { get; set; }
    public string EvidenceDescription { get; set; } = string.Empty;
}
