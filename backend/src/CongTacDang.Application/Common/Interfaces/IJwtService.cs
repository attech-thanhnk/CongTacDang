using System;
using System.Collections.Generic;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ sinh mã xác thực JWT và Refresh Token
/// </summary>
public interface IJwtService
{
    /// <summary>Sinh Access Token kèm danh sách roles và permissions</summary>
    (string Token, DateTime ExpiresAt) GenerateToken(
        PartyMemberProfile member,
        IEnumerable<string> roles,
        IEnumerable<string> permissions);

    /// <summary>Sinh Refresh Token ngẫu nhiên mã hóa an toàn cao</summary>
    RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress = null);
}
