using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CongTacDang.Api.Services;

/// <summary>
/// Sinh JWT Bearer Token với đầy đủ claims vai trò.
/// Token được đọc từ HttpOnly Cookie bởi middleware JwtBearer.
/// </summary>
public class JwtService
{
    private readonly string _secret;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryDays;

    public JwtService(IConfiguration config)
    {
        _secret = config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret chưa được cấu hình.");
        _issuer = config["Jwt:Issuer"] ?? "CongTacDang.Api";
        _audience = config["Jwt:Audience"] ?? "CongTacDang.Client";
        _expiryDays = int.TryParse(config["Jwt:ExpiryDays"], out var d) ? d : 7;
    }

    /// <summary>Sinh JWT Token với claims: sub, name, unique_name, party_role, role[]</summary>
    public (string Token, DateTime ExpiresAt) GenerateToken(PartyMemberProfile member)
    {
        var roles = BuildRoles(member);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, member.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, member.Username),
            new Claim(JwtRegisteredClaimNames.Name, member.FullName),
            new Claim("party_role", member.PartyRole.ToString()),
        };

        // Mỗi role là một claim riêng — ASP.NET Authorization đọc ClaimTypes.Role
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddDays(_expiryDays);

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
    /// Ma trận vai trò theo quy trình 03-HD/TVĐU:
    /// - CAN_BO: tất cả cán bộ đã xác thực
    /// - BI_THU_CHI_BO: Bí thư / Phó Bí thư Chi bộ
    /// - BAN_THUONG_VU: Bí thư / Phó BT / Ủy viên BTV Đảng ủy
    /// - QUAN_TRI_HE_THONG: cùng Ban Thường vụ (toàn quyền hệ thống)
    /// </summary>
    public static string[] BuildRoles(PartyMemberProfile member)
    {
        var roles = new List<string> { AppRoles.CAN_BO };

        if (member.PartyRole == PartyRole.BiThuChiBo || member.PartyRole == PartyRole.PhoBiThuChiBo)
            roles.Add(AppRoles.BI_THU_CHI_BO);

        if (member.PartyRole == PartyRole.BiThuDangUy
            || member.PartyRole == PartyRole.PhoBiThuDangUy
            || member.PartyRole == PartyRole.UyVienBanThuongVu)
        {
            roles.Add(AppRoles.BAN_THUONG_VU);
            roles.Add(AppRoles.QUAN_TRI_HE_THONG);
        }

        return roles.ToArray();
    }
}
