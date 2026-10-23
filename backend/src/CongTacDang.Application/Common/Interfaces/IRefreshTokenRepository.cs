using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

public interface IRefreshTokenRepository : IRepository<RefreshToken>
{
    Task<RefreshToken?> GetByTokenWithUserAsync(string token);

    /// <summary>Thu hồi token cũ và thêm token mới trong một lần lưu thay đổi.</summary>
    Task<bool> RotateAsync(string oldToken, RefreshToken newToken);

    /// <summary>Dùng khi phát hiện dùng lại token hoặc buộc đăng xuất.</summary>
    Task RevokeAllUserTokensAsync(Guid userId);

    /// <summary>Thu hồi các token khác, giữ lại token hiện tại nếu được cung cấp.</summary>
    Task RevokeOtherUserTokensAsync(Guid userId, string? currentToken);
}
