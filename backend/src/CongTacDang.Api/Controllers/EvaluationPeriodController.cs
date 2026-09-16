using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace CongTacDang.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EvaluationPeriodController : ControllerBase
{
    private readonly CongTacDangDbContext _db;

    public EvaluationPeriodController(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy danh sách các kỳ đánh giá</summary>
    [HttpGet]
    public async Task<IActionResult> GetPeriods()
    {
        var periods = await _db.EvaluationPeriods
            .Include(p => p.Setting)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Quarter)
            .ToListAsync();
        return Ok(periods);
    }

    /// <summary>Lấy chi tiết cấu hình tham số động của một kỳ</summary>
    [HttpGet("{id}/settings")]
    public async Task<IActionResult> GetSettings(Guid id)
    {
        var setting = await _db.EvaluationSettings.FirstOrDefaultAsync(s => s.PeriodId == id);
        if (setting == null) return NotFound("Chưa có cấu hình cho kỳ này.");
        return Ok(setting);
    }

    /// <summary>Cập nhật động các tham số đánh giá (thang điểm, tỷ lệ trần 20%, hạn chót)</summary>
    [HttpPut("{id}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] EvaluationSetting input)
    {
        var setting = await _db.EvaluationSettings.FirstOrDefaultAsync(s => s.PeriodId == id);
        if (setting == null) return NotFound();

        setting.MaxTotalScore = input.MaxTotalScore;
        setting.GeneralCriteriaMaxScore = input.GeneralCriteriaMaxScore;
        setting.TaskCriteriaMaxScore = input.TaskCriteriaMaxScore;
        setting.Tc1Score = input.Tc1Score;
        setting.Tc2Score = input.Tc2Score;
        setting.Tc3Score = input.Tc3Score;
        setting.MaxExcellentPercentage = input.MaxExcellentPercentage;
        setting.MaxExcellentSpecialPercentage = input.MaxExcellentSpecialPercentage;
        setting.MinTasks = input.MinTasks;
        setting.MaxTasks = input.MaxTasks;
        setting.ExcellentMinScore = input.ExcellentMinScore;
        setting.GoodMinScore = input.GoodMinScore;
        setting.PassMinScore = input.PassMinScore;

        await _db.SaveChangesAsync();
        return Ok(setting);
    }
}
