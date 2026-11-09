using System;
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

/// <summary>Số liệu trang Tổng quan; service lọc theo quyền và phạm vi.</summary>
[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard) => _dashboard = dashboard;

    /// <summary>Lịch kỳ, kết quả gần nhất của tôi, tiến độ hồ sơ trong phạm vi.</summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview([FromQuery] Guid periodId, CancellationToken ct) =>
        Ok(ApiResponse<DashboardOverviewDto>.Ok(await _dashboard.GetOverviewAsync(periodId, ct), "Lấy số liệu tổng quan thành công."));

    /// <summary>Việc quản trị cần xử lý.</summary>
    [HttpGet("admin")]
    [RequireAnyPermission(PermissionCodes.SystemUsersManage, PermissionCodes.PeriodManage)]
    public async Task<IActionResult> GetAdmin(CancellationToken ct) =>
        Ok(ApiResponse<AdminSummaryDto>.Ok(await _dashboard.GetAdminSummaryAsync(ct), "Lấy việc quản trị cần xử lý thành công."));
}
