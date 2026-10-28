using System;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CongTacDang.Api.Controllers;

/// <summary>Ý kiến ghi nhận trước phê duyệt danh mục và trước nhận xét. Service kiểm tra quyền trên hồ sơ.</summary>
[ApiController]
[Route("api/evaluations/records/{recordId:guid}/opinions")]
[Authorize]
public class RecordOpinionController : ControllerBase
{
    private readonly IRecordOpinionService _opinions;

    public RecordOpinionController(IRecordOpinionService opinions)
    {
        _opinions = opinions;
    }

    [HttpGet]
    public async Task<IActionResult> Get(Guid recordId, CancellationToken ct)
    {
        var data = await _opinions.GetAsync(recordId, ct);
        return Ok(ApiResponse<RecordOpinionsDto>.Ok(data, "Lấy danh sách ý kiến thành công."));
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid recordId, [FromBody] CreateRecordOpinionDto dto, CancellationToken ct)
    {
        var data = await _opinions.CreateAsync(recordId, dto, ct);
        return Ok(ApiResponse<RecordOpinionsDto>.Ok(data, "Đã ghi nhận ý kiến."));
    }
}
