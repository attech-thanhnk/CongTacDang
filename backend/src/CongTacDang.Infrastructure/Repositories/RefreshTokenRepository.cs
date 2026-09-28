using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

    /// <summary>
    /// Truy vấn Refresh Token cùng tài khoản sở hữu (không nạp vai trò — quyền lấy qua IPermissionResolver).
    /// Token của tài khoản đã xóa mềm không được trả về (bộ lọc xóa mềm của tài khoản).
    /// </summary>
    public async Task<RefreshToken?> GetByTokenWithUserAsync(string token)
    {
        var hash = HashToken(token);
        return await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash);
    }

    /// <summary>Thu hồi token cũ và thêm token mới bằng một lần SaveChanges.</summary>
    public async Task<bool> RotateAsync(string oldToken, RefreshToken newToken)
    {
        if (string.IsNullOrWhiteSpace(newToken.TokenHash))
            newToken.TokenHash = HashToken(newToken.Token);

        var oldRecord = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == HashToken(oldToken));

        if (oldRecord == null || oldRecord.IsRevoked || oldRecord.IsExpired)
            return false;

        oldRecord.IsRevoked = true;
        oldRecord.RevokedAt = DateTime.UtcNow;
        oldRecord.ReplacedByTokenHash = newToken.TokenHash;
        _db.RefreshTokens.Add(newToken);
        await _db.SaveChangesAsync();
        return true;
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
            t.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task RevokeOtherUserTokensAsync(Guid userId, string? currentToken)
    {
        var currentHash = string.IsNullOrWhiteSpace(currentToken) ? null : HashToken(currentToken);
        var activeTokens = await _db.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked && (currentHash == null || t.TokenHash != currentHash))
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }
}
