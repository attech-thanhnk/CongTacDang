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
    /// <summary>Tìm Refresh Token kèm theo thông tin cán bộ, vai trò và quyền hạn</summary>
    Task<RefreshToken?> GetByTokenWithUserAsync(string token);

    /// <summary>Thu hồi toàn bộ Refresh Token đang hoạt động của người dùng (Token Reuse Detection / Force Logout)</summary>
    Task RevokeAllUserTokensAsync(Guid userId);
}
