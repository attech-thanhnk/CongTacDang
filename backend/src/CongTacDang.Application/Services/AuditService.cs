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
    /// <summary>Tra cứu nhật ký thao tác (mới nhất trước), có phân trang và số đếm trên toàn bộ kết quả lọc.</summary>
    Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogQuery query, CancellationToken ct = default);

    /// <summary>Tra cứu nhật ký đăng nhập (mới nhất trước), có phân trang.</summary>
    Task<PagedResult<LoginEventDto>> GetLoginEventsAsync(LoginEventQuery query, CancellationToken ct = default);
}

/// <summary>Tham số tra cứu nhật ký thao tác.</summary>
public sealed record AuditLogQuery(
    string? EntityType = null,
    string? EntityId = null,
    string? Action = null,
    int? Page = null,
    int? PageSize = null);

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

    /// <inheritdoc />
    public async Task<AuditLogPageDto> GetAuditLogsAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize);
        var result = await _auditRepository.SearchAuditLogsAsync(new AuditLogCriteria(
            page, pageSize, Clean(query.EntityType), Clean(query.EntityId), Clean(query.Action)), ct);

        return new AuditLogPageDto
        {
            Items = result.Items.Select(x => new AuditLogDto
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
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
            ActionCounts = new Dictionary<string, int>(result.ActionCounts),
            ActorCount = result.ActorCount,
            EntityTypes = result.EntityTypes.ToList()
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
