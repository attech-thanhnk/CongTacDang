using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;

namespace CongTacDang.Api.Controllers;

/// <summary>Tra cứu nhật ký thao tác tập trung của hệ thống.</summary>
[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = AppPermissions.RolesManage)]
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
}
