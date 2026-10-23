using System;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

public interface IJwtService
{
    /// <summary>Tên claim chứa dấu bảo mật của tài khoản tại thời điểm cấp token.</summary>
    public const string SecurityStampClaim = "sstamp";

    /// <summary>Chỉ chứa danh tính; quyền được tính lại mỗi request qua <c>IPermissionResolver</c>.</summary>
    (string Token, DateTime ExpiresAt) GenerateToken(PartyMemberProfile member);

    RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress = null);
}
