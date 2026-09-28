using System;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ sinh mã xác thực JWT và Refresh Token
/// </summary>
public interface IJwtService
{
    /// <summary>Tên claim chứa dấu bảo mật của tài khoản tại thời điểm cấp token.</summary>
    public const string SecurityStampClaim = "sstamp";

    /// <summary>
    /// Sinh Access Token chỉ chứa danh tính (<c>sub</c>, <c>username</c>, <c>name</c>, <c>sstamp</c>, <c>jti</c>, <c>exp</c>);
    /// không chứa vai trò/quyền — quyền được tính lại mỗi request qua <c>IPermissionResolver</c>.
    /// </summary>
    (string Token, DateTime ExpiresAt) GenerateToken(PartyMemberProfile member);

    /// <summary>Sinh Refresh Token ngẫu nhiên mã hóa an toàn cao</summary>
    RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress = null);
}
