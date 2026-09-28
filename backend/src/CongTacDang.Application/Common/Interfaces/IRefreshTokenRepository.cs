using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện repository quản lý Refresh Token theo chuẩn Token Rotation
/// </summary>
public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    /// <summary>Tìm Refresh Token kèm theo tài khoản sở hữu</summary>
    Task<RefreshToken?> GetByTokenWithUserAsync(string token);

    /// <summary>Thu hồi token cũ và thêm token mới trong một lần lưu thay đổi.</summary>
    Task<bool> RotateAsync(string oldToken, RefreshToken newToken);

    /// <summary>Thu hồi toàn bộ Refresh Token đang hoạt động của người dùng (Token Reuse Detection / Force Logout)</summary>
    Task RevokeAllUserTokensAsync(Guid userId);

    /// <summary>Thu hồi các token khác, giữ lại token hiện tại nếu được cung cấp.</summary>
    Task RevokeOtherUserTokensAsync(Guid userId, string? currentToken);
}
