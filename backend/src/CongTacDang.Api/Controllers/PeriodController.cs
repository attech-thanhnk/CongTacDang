using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>
/// Kỳ đánh giá: đọc — mọi người đã đăng nhập; ghi — <c>period.manage</c> (task 12, docs/thiet-ke/luong-danh-gia.md mục 3).
/// </summary>
[ApiController]
[Route("api/evaluations/periods")]
[Authorize]
public class PeriodController : ControllerBase
{
    private readonly IPeriodService _periods;

    public PeriodController(IPeriodService periods)
    {
        _periods = periods;
    }

    /// <summary>Danh sách kỳ (kèm cấu hình, số người được đánh giá).</summary>
    [HttpGet]
    public async Task<IActionResult> GetPeriods(CancellationToken ct) =>
        Ok(ApiResponse<List<EvaluationPeriodDto>>.Ok(await _periods.GetPeriodsAsync(ct), "Lấy danh sách kỳ đánh giá thành công."));

    /// <summary>Kỳ hiện hành (Đang mở/Khóa dữ liệu mới nhất), null nếu không có.</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct) =>
        Ok(ApiResponse<EvaluationPeriodDto?>.Ok(await _periods.GetActivePeriodAsync(ct), "Lấy kỳ đánh giá hiện hành thành công."));

    /// <summary>Hai mẫu cấu hình dựng sẵn.</summary>
    [HttpGet("presets")]
    public IActionResult GetPresets() =>
        Ok(ApiResponse<IReadOnlyList<PeriodPresetDto>>.Ok(_periods.GetPresets(), "Lấy mẫu cấu hình kỳ thành công."));

    /// <summary>Chi tiết kỳ.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPeriod(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<EvaluationPeriodDto>.Ok(await _periods.GetPeriodAsync(id, ct), "Lấy thông tin kỳ đánh giá thành công."));

    /// <summary>Tạo kỳ (trạng thái Dự thảo) từ mẫu cấu hình.</summary>
    [HttpPost]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Create([FromBody] CreatePeriodDto dto, CancellationToken ct) =>
        Ok(ApiResponse<EvaluationPeriodDto>.Ok(await _periods.CreatePeriodAsync(dto, ct), "Đã tạo kỳ đánh giá (dự thảo)."));

    /// <summary>Sửa kỳ: dự thảo sửa mọi thứ; đã mở chỉ sửa thời hạn các bước.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePeriodDto dto, CancellationToken ct) =>
        Ok(ApiResponse<EvaluationPeriodDto>.Ok(await _periods.UpdatePeriodAsync(id, dto, ct), "Đã lưu cấu hình kỳ đánh giá."));

    /// <summary>
    /// Dự thảo → Đang mở. Còn cảnh báo kẹt luồng → 409 kèm danh sách cảnh báo (<c>data</c> = kết quả kiểm tra, mã
    /// <c>PERIOD_NOT_READY</c>); mở bắt buộc: <c>force = true</c> + lý do.
    /// </summary>
    [HttpPost("{id:guid}/open")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Open(Guid id, [FromBody] PeriodTransitionDto dto, CancellationToken ct)
    {
        var outcome = await _periods.OpenAsync(id, dto, ct);
        if (outcome.Blocked)
        {
            var readiness = outcome.Readiness!;
            return Conflict(new ApiResponse<PeriodReadinessDto>
            {
                Success = false,
                Code = "PERIOD_NOT_READY",
                Message = $"Chưa mở được kỳ: có {readiness.Issues.Count} cảnh báo kẹt luồng (hồ sơ sẽ không có ai thực hiện được bước). "
                    + "Hãy gán vai trò hoặc sửa cấu hình hồ sơ luồng, hoặc mở bắt buộc kèm lý do.",
                Data = readiness,
                Errors = readiness.Issues.Select(i => i.Message).ToList()
            });
        }
        return Ok(ApiResponse<EvaluationPeriodDto>.Ok(outcome.Period!, "Đã mở kỳ đánh giá."));
    }

    /// <summary>Kiểm tra kẹt luồng của kỳ (mỗi hồ sơ × mỗi bước còn phía trước có người thực hiện được trong phạm vi).</summary>
    [HttpGet("{id:guid}/readiness")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> GetReadiness(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<PeriodReadinessDto>.Ok(await _periods.GetReadinessAsync(id, ct), "Đã kiểm tra kẹt luồng của kỳ."));

    /// <summary>Mã quyền chọn được làm quyền thực hiện bước trong hồ sơ luồng.</summary>
    [HttpGet("step-permissions")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public IActionResult GetStepPermissions() =>
        Ok(ApiResponse<IReadOnlyList<StepPermissionOptionDto>>.Ok(_periods.GetStepPermissions(), "Lấy danh sách quyền thực hiện bước thành công."));

    /// <summary>Đang mở → Khóa dữ liệu.</summary>
    [HttpPost("{id:guid}/lock")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Lock(Guid id, [FromBody] PeriodTransitionDto dto, CancellationToken ct) =>
        Ok(ApiResponse<EvaluationPeriodDto>.Ok(await _periods.LockAsync(id, dto, ct), "Đã khóa dữ liệu kỳ đánh giá."));

    /// <summary>Khóa dữ liệu → Đang mở (bắt buộc lý do).</summary>
    [HttpPost("{id:guid}/unlock")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Unlock(Guid id, [FromBody] PeriodTransitionDto dto, CancellationToken ct) =>
        Ok(ApiResponse<EvaluationPeriodDto>.Ok(await _periods.UnlockAsync(id, dto, ct), "Đã mở lại kỳ đánh giá."));

    /// <summary>Đang mở / Khóa dữ liệu → Đã đóng (mọi hồ sơ đã công bố).</summary>
    [HttpPost("{id:guid}/close")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> Close(Guid id, [FromBody] PeriodTransitionDto dto, CancellationToken ct) =>
        Ok(ApiResponse<EvaluationPeriodDto>.Ok(await _periods.CloseAsync(id, dto, ct), "Đã đóng kỳ đánh giá."));

    /// <summary>Danh sách người được đánh giá.</summary>
    [HttpGet("{id:guid}/participants")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> GetParticipants(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<PeriodParticipantDto>>.Ok(await _periods.GetParticipantsAsync(id, ct), "Lấy danh sách người được đánh giá thành công."));

    /// <summary>Cán bộ có thể thêm (lọc theo Phòng/Chi bộ/tên).</summary>
    [HttpGet("{id:guid}/candidates")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> GetCandidates(Guid id, [FromQuery] Guid? departmentId, [FromQuery] Guid? partyCellId, [FromQuery] string? q, CancellationToken ct) =>
        Ok(ApiResponse<List<ParticipantCandidateDto>>.Ok(await _periods.GetCandidatesAsync(id, departmentId, partyCellId, q, ct), "Lấy danh sách cán bộ thành công."));

    /// <summary>Thêm người được đánh giá (chọn tay / theo Phòng / theo Chi bộ).</summary>
    [HttpPost("{id:guid}/participants")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> AddParticipants(Guid id, [FromBody] AddParticipantsDto dto, CancellationToken ct)
    {
        var result = await _periods.AddParticipantsAsync(id, dto, ct);
        return Ok(ApiResponse<AddParticipantsResultDto>.Ok(result, $"Đã thêm {result.Added} người vào danh sách được đánh giá."));
    }

    /// <summary>Bỏ người khỏi danh sách (dự thảo, hoặc đang mở mà hồ sơ chưa có dữ liệu).</summary>
    [HttpDelete("{id:guid}/participants/{recordId:guid}")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> RemoveParticipant(Guid id, Guid recordId, [FromQuery] uint? version, CancellationToken ct)
    {
        await _periods.RemoveParticipantAsync(id, recordId, version, ct);
        return Ok(ApiResponse.Ok("Đã bỏ người khỏi danh sách được đánh giá."));
    }

    /// <summary>Sửa ảnh chụp Phòng/Chi bộ/khung chức danh/cấp quyết định (bắt buộc lý do, ghi lịch sử).</summary>
    [HttpPut("{id:guid}/participants/{recordId:guid}/snapshot")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> UpdateSnapshot(Guid id, Guid recordId, [FromBody] UpdateSnapshotDto dto, CancellationToken ct) =>
        Ok(ApiResponse<PeriodParticipantDto>.Ok(await _periods.UpdateSnapshotAsync(id, recordId, dto, ct), "Đã sửa thông tin ảnh chụp của hồ sơ."));

    /// <summary>Đổi hồ sơ luồng của một người được đánh giá (bắt buộc lý do; hồ sơ chưa qua bước bị ảnh hưởng).</summary>
    [HttpPut("{id:guid}/participants/{recordId:guid}/profile")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> ChangeProfile(Guid id, Guid recordId, [FromBody] ChangeProfileDto dto, CancellationToken ct) =>
        Ok(ApiResponse<PeriodParticipantDto>.Ok(await _periods.ChangeProfileAsync(id, recordId, dto, ct), "Đã đổi hồ sơ luồng của hồ sơ."));

    /// <summary>Đổi hồ sơ luồng hàng loạt (hồ sơ không đổi được trả về kèm lý do).</summary>
    [HttpPut("{id:guid}/participants/profile")]
    [RequirePermission(PermissionCodes.PeriodManage)]
    public async Task<IActionResult> BulkChangeProfile(Guid id, [FromBody] BulkChangeProfileDto dto, CancellationToken ct)
    {
        var result = await _periods.BulkChangeProfileAsync(id, dto, ct);
        return Ok(ApiResponse<BulkChangeProfileResultDto>.Ok(result, $"Đã đổi hồ sơ luồng cho {result.Updated} hồ sơ."));
    }
}
