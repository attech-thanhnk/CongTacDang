using System;

namespace CongTacDang.Domain.Entities;

/// <summary>Refresh token, xoay vòng mỗi lần làm mới.</summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public PartyMemberProfile User { get; set; } = null!;

    /// <summary>Chỉ tồn tại trong bộ nhớ để trả về cookie.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex).</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>SHA-256 của token mới thay thế.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? CreatedByIp { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsActive => !IsRevoked && !IsExpired;
}
