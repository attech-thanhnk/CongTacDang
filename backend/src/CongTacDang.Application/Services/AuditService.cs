using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>Dịch vụ tra cứu audit log tập trung cho quản trị hệ thống.</summary>
public interface IAuditService
{
    /// <summary>Lấy danh sách audit log theo entity và giới hạn bản ghi.</summary>
    Task<List<AuditLogDto>> GetAuditLogsAsync(string? entityType, string? entityId, int limit);
}

/// <summary>Triển khai mapping audit entity sang DTO.</summary>
public class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;

    /// <summary>Khởi tạo dịch vụ audit.</summary>
    public AuditService(IAuditRepository auditRepository)
    {
        _auditRepository = auditRepository;
    }

    /// <summary>Tra cứu các thao tác gần nhất theo bộ lọc.</summary>
    public async Task<List<AuditLogDto>> GetAuditLogsAsync(string? entityType, string? entityId, int limit)
    {
        var logs = await _auditRepository.GetAuditLogsAsync(entityType, entityId, limit);
        return logs.Select(x => new AuditLogDto
        {
            Id = x.Id,
            ActorId = x.ActorId,
            ActorName = x.ActorName,
            Action = x.Action,
            EntityType = x.EntityType,
            EntityId = x.EntityId,
            OldValues = x.OldValues,
            NewValues = x.NewValues,
            IpAddress = x.IpAddress,
            RequestPath = x.RequestPath,
            CreatedAt = x.CreatedAt
        }).ToList();
    }
}
