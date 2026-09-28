using System;

namespace CongTacDang.Domain.Entities;

/// <summary>
/// Nhật ký audit tập trung cho các thay đổi dữ liệu và thao tác nghiệp vụ.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string OldValues { get; set; } = "{}";
    public string NewValues { get; set; } = "{}";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? RequestPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
