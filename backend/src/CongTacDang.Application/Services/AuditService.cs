using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Models;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>Dịch vụ tra cứu nhật ký thao tác và nhật ký đăng nhập cho quản trị hệ thống.</summary>
public interface IAuditService
{
    /// <summary>Lấy danh sách audit log theo entity và giới hạn bản ghi.</summary>
    Task<List<AuditLogDto>> GetAuditLogsAsync(string? entityType, string? entityId, int limit);

    /// <summary>Tra cứu nhật ký đăng nhập (mới nhất trước), có phân trang.</summary>
    Task<PagedResult<LoginEventDto>> GetLoginEventsAsync(LoginEventQuery query, CancellationToken ct = default);
}

/// <summary>Tham số tra cứu nhật ký đăng nhập.</summary>
public sealed record LoginEventQuery(
    Guid? UserId = null,
    DateTime? From = null,
    DateTime? To = null,
    LoginResult? Result = null,
    int? Page = null,
    int? PageSize = null);

/// <summary>Triển khai mapping audit entity sang DTO.</summary>
public class AuditService : IAuditService
{
    private readonly IAuditRepository _auditRepository;
    private readonly ILoginEventRepository? _loginEvents;

    /// <summary>Khởi tạo dịch vụ audit.</summary>
    public AuditService(IAuditRepository auditRepository, ILoginEventRepository? loginEvents = null)
    {
        _auditRepository = auditRepository;
        _loginEvents = loginEvents;
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

    /// <inheritdoc />
    public async Task<PagedResult<LoginEventDto>> GetLoginEventsAsync(LoginEventQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (_loginEvents == null)
            throw new InvalidOperationException("Chưa đăng ký kho nhật ký đăng nhập.");
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new Common.Exceptions.ValidationException("Thời điểm bắt đầu phải trước thời điểm kết thúc. Hãy chọn lại khoảng thời gian.");

        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await _loginEvents.SearchAsync(new LoginEventCriteria(
            page, pageSize, query.UserId, ToUtc(query.From), ToUtc(query.To), query.Result), ct);

        return new PagedResult<LoginEventDto>
        {
            Items = result.Items.Select(e => new LoginEventDto
            {
                Id = e.Id,
                UserId = e.UserId,
                UsernameAttempted = e.UsernameAttempted,
                Result = e.Result.ToString(),
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                CreatedAt = e.CreatedAt
            }).ToList(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount
        };
    }

    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        null => null,
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value!.Value, DateTimeKind.Utc)
    };
}
