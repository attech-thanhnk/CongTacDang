using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>
/// Triển khai v0 của <see cref="IUserAccountService"/>: kiểm tra tên đăng nhập, băm mật khẩu tạm ngẫu nhiên,
/// bắt buộc đổi mật khẩu và trả mật khẩu tạm cho người gọi. Không gán vai trò (task 08/09).
/// </summary>
public sealed class UserAccountService : IUserAccountService
{
    /// <summary>Độ dài tối đa tên đăng nhập (khớp cấu hình cột Username).</summary>
    public const int MaxUsernameLength = 100;

    private const int TemporaryPasswordLength = 12;
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";

    private readonly IUserRepository _users;

    /// <summary>Khởi tạo dịch vụ.</summary>
    public UserAccountService(IUserRepository users) => _users = users;

    /// <inheritdoc />
    public async Task<CreatedAccount> CreateAsync(CreateAccountCommand cmd, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cmd);
        ct.ThrowIfCancellationRequested();

        var username = cmd.Username?.Trim() ?? string.Empty;
        if (username.Length == 0)
            throw new ValidationException("Tên đăng nhập không được để trống. Hãy nhập tên đăng nhập cho tài khoản.");
        if (username.Length > MaxUsernameLength)
            throw new ValidationException($"Tên đăng nhập dài quá {MaxUsernameLength} ký tự. Hãy rút ngắn tên đăng nhập.");
        if (username.Any(char.IsWhiteSpace))
            throw new ValidationException("Tên đăng nhập không được chứa khoảng trắng. Hãy bỏ khoảng trắng trong tên đăng nhập.");

        var fullName = cmd.FullName?.Trim() ?? string.Empty;
        if (fullName.Length == 0)
            throw new ValidationException("Họ và tên không được để trống. Hãy nhập họ và tên cán bộ.");

        var normalized = username.ToLowerInvariant();
        var duplicates = await _users.FindAsync(m => m.Username.ToLower() == normalized);
        if (duplicates.Count > 0)
            throw new ConflictException($"Tên đăng nhập \"{username}\" đã được dùng cho tài khoản khác. Hãy chọn tên đăng nhập khác.");

        var temporaryPassword = GenerateTemporaryPassword();
        var partyCardNumber = string.IsNullOrWhiteSpace(cmd.PartyCardNumber) ? null : cmd.PartyCardNumber.Trim();
        var member = new PartyMemberProfile
        {
            Username = username,
            FullName = fullName,
            Email = cmd.Email?.Trim() ?? string.Empty,
            PartyCardNumber = partyCardNumber,
            IsPartyMember = partyCardNumber != null,
            PositionTitle = string.IsNullOrWhiteSpace(cmd.PositionTitle) ? "Cán bộ" : cmd.PositionTitle.Trim(),
            DepartmentId = cmd.DepartmentId == Guid.Empty ? null : cmd.DepartmentId,
            PartyCellId = cmd.PartyCellId == Guid.Empty ? null : cmd.PartyCellId,
            ApprovalAuthority = cmd.ApprovalAuthority,
            IsActive = true,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(temporaryPassword),
            MustChangePassword = true
        };

        await _users.AddAsync(member);
        return new CreatedAccount(member.Id, member.Username, temporaryPassword);
    }

    /// <summary>Sinh mật khẩu tạm ngẫu nhiên an toàn, luôn có cả chữ và số, không dùng ký tự dễ nhầm (0/O, 1/l/I).</summary>
    public static string GenerateTemporaryPassword()
    {
        const string alphabet = Letters + Digits;
        var password = new char[TemporaryPasswordLength];
        for (var i = 0; i < password.Length; i++)
            password[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        // Bảo đảm có ít nhất một chữ và một số ở vị trí ngẫu nhiên khác nhau.
        var letterIndex = RandomNumberGenerator.GetInt32(password.Length);
        var digitIndex = (letterIndex + 1 + RandomNumberGenerator.GetInt32(password.Length - 1)) % password.Length;
        password[letterIndex] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        password[digitIndex] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        return new string(password);
    }
}
