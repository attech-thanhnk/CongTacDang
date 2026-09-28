using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CongTacDang.Api.Services;

/// <summary>
/// Dịch vụ sinh JWT (chỉ danh tính + dấu bảo mật) và Refresh Token.
/// Token được đọc từ HttpOnly Cookie bởi middleware JwtBearer; trạng thái tài khoản và dấu bảo mật
/// được kiểm tra lại ở mỗi request (<c>SecurityExtensions.AddAccountSessionValidation</c>).
/// </summary>
public class JwtService : IJwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpiryMinutes;
    private readonly int _refreshTokenExpiryDays;

    public JwtService(IConfiguration config)
    {
        _secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret chưa được cấu hình.");
        _issuer = config["Jwt:Issuer"] ?? "CongTacDang.Api";
        _audience = config["Jwt:Audience"] ?? "CongTacDang.Client";
        _accessTokenExpiryMinutes = int.TryParse(config["Jwt:AccessTokenExpiryMinutes"], out var m) ? m : 15;
        _refreshTokenExpiryDays = int.TryParse(config["Jwt:RefreshTokenExpiryDays"], out var d) ? d : 7;
    }

    /// <summary>Sinh JWT Access Token ngắn hạn (mặc định 15 phút) chỉ chứa danh tính và dấu bảo mật.</summary>
    public (string Token, DateTime ExpiresAt) GenerateToken(PartyMemberProfile member)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, member.Username),
            new("username", member.Username),
            new(JwtRegisteredClaimNames.Name, member.FullName),
            new(IJwtService.SecurityStampClaim, member.SecurityStamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    /// <summary>
    /// Sinh Refresh Token ngẫu nhiên 64 bytes có độ an toàn mã hóa cao (hạn 7 ngày)
    /// </summary>
    public RefreshToken GenerateRefreshToken(Guid userId, string? ipAddress = null)
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(randomBytes);
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant(),
            ExpiresAt = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress,
            IsRevoked = false
        };
    }
}
