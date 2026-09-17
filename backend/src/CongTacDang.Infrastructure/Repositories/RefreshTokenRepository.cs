using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>
/// Cài đặt repository quản lý Refresh Token
/// </summary>
public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
{
    public RefreshTokenRepository(CongTacDangDbContext db) : base(db)
    {
    }

    /// <summary>Truy vấn Refresh Token cùng với dữ liệu người dùng, vai trò và quyền hạn liên quan</summary>
    public async Task<RefreshToken?> GetByTokenWithUserAsync(string token)
    {
        return await _db.RefreshTokens
            .Include(t => t.User)
                .ThenInclude(u => u.Roles)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    /// <summary>Thu hồi toàn bộ Refresh Token còn hiệu lực của một người dùng (sử dụng khi phát hiện tái sử dụng token hoặc đăng xuất toàn cục)</summary>
    public async Task RevokeAllUserTokensAsync(Guid userId)
    {
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync();

        foreach (var t in activeTokens)
        {
            t.IsRevoked = true;
        }

        await _db.SaveChangesAsync();
    }
}
