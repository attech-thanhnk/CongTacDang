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

    /// <summary>Khởi tạo controller audit.</summary>
    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>Lấy audit log mới nhất, có thể lọc theo loại và Id entity.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] int limit = 100)
    {
        var logs = await _auditService.GetAuditLogsAsync(entityType, entityId, limit);
        return Ok(ApiResponse<List<AuditLogDto>>.Ok(logs, "Lấy nhật ký audit thành công."));
    }

    /// <summary>
    /// Nhật ký đăng nhập (mới nhất trước). Lọc theo tài khoản, khoảng thời gian (UTC) và kết quả
    /// (<c>Success</c>, <c>InvalidPassword</c>, <c>UnknownUser</c>, <c>LockedOut</c>, <c>Disabled</c>).
    /// </summary>
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
