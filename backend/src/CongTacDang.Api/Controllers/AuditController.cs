using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Api.Authorization;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Api.Controllers;

/// <summary>Tra cứu nhật ký thao tác và nhật ký đăng nhập (quyền "Xem nhật ký").</summary>
[ApiController]
[Route("api/admin/audit")]
[RequirePermission(PermissionCodes.SystemAuditRead)]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>Nhật ký thao tác (mới nhất trước, phân trang).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? action = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        var logs = await _auditService.GetAuditLogsAsync(new AuditLogQuery(entityType, entityId, action, page, pageSize), ct);
        return Ok(ApiResponse<AuditLogPageDto>.Ok(logs, "Lấy nhật ký audit thành công."));
    }

    /// <summary>Nhật ký đăng nhập (mới nhất trước; khoảng thời gian theo UTC).</summary>
    [HttpGet("/api/audit/logins")]
    public async Task<IActionResult> GetLoginEvents(
        [FromQuery] Guid? userId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] LoginResult? result = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        var events = await _auditService.GetLoginEventsAsync(new LoginEventQuery(userId, from, to, result, page, pageSize), ct);
        return Ok(ApiResponse<PagedResult<LoginEventDto>>.Ok(events, "Lấy nhật ký đăng nhập thành công."));
    }
}
