using System;
using System.Globalization;
using System.Linq;
using System.Net.Mail;
using System.Text.RegularExpressions;
using CongTacDang.Application.Common.Exceptions;

namespace CongTacDang.Application.Accounts;

/// <summary>Quy tắc tên đăng nhập và email của tài khoản.</summary>
public static partial class AccountRules
{
    public const int UsernameMinLength = 3;

    public const int UsernameMaxLength = 50;

    /// <summary>Độ dài tối đa email (khớp cấu hình cột).</summary>
    public const int EmailMaxLength = 200;

    [GeneratedRegex("^[a-z0-9._-]{3,50}$", RegexOptions.CultureInvariant)]
    private static partial Regex UsernamePattern();

    /// <summary>Chuẩn hóa tên đăng nhập để so khớp/lưu: cắt khoảng trắng hai đầu, chữ thường (invariant).</summary>
    public static string NormalizeUsername(string? username) =>
        (username ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Tên đăng nhập (đã chuẩn hóa) hợp lệ theo mẫu <c>^[a-z0-9._-]{3,50}$</c>.</summary>
    public static bool IsValidUsername(string normalized) => UsernamePattern().IsMatch(normalized);

    /// <summary>Chuẩn hóa và kiểm tra tên đăng nhập; ném <see cref="ValidationException"/> kèm hướng dẫn khi không hợp lệ.</summary>
    public static string NormalizeAndValidateUsername(string? username)
    {
        var normalized = NormalizeUsername(username);
        if (normalized.Length == 0)
            throw new ValidationException("Tên đăng nhập không được để trống. Hãy nhập tên đăng nhập cho tài khoản.");
        if (normalized.Length < UsernameMinLength || normalized.Length > UsernameMaxLength)
            throw new ValidationException(
                $"Tên đăng nhập phải dài từ {UsernameMinLength} đến {UsernameMaxLength} ký tự. Hãy nhập lại tên đăng nhập.");
        if (!IsValidUsername(normalized))
            throw new ValidationException(
                "Tên đăng nhập chỉ được gồm chữ cái không dấu, chữ số và các ký tự \". _ -\", không có khoảng trắng. "
                + "Hãy nhập lại, ví dụ \"nguyen.van.a\".");
        return normalized;
    }

    /// <summary>Chuẩn hóa email tùy chọn: trống → chuỗi rỗng; sai định dạng → <see cref="ValidationException"/>.</summary>
    public static string NormalizeOptionalEmail(string? email)
    {
        var value = email?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return string.Empty;
        if (value.Length > EmailMaxLength || !IsValidEmail(value))
            throw new ValidationException($"Email \"{value}\" không đúng định dạng. Hãy nhập email dạng ten@donvi.vn hoặc để trống.");
        return value;
    }

    public const int DateOfBirthMaxYears = 100;

    /// <summary>Ngày sinh tùy chọn "yyyy-MM-dd": trống → null; sai định dạng, ở tương lai hoặc quá xa → <see cref="ValidationException"/>.</summary>
    public static DateOnly? ParseOptionalDateOfBirth(string? value, DateOnly today)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return null;
        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new ValidationException($"Ngày sinh \"{text}\" không hợp lệ. Hãy chọn ngày sinh hoặc để trống.");
        if (date > today)
            throw new ValidationException("Ngày sinh không được ở tương lai. Hãy kiểm tra lại ngày sinh.");
        if (date < today.AddYears(-DateOfBirthMaxYears))
            throw new ValidationException($"Ngày sinh không được trước quá {DateOfBirthMaxYears} năm. Hãy kiểm tra lại ngày sinh.");
        return date;
    }

    /// <summary>Email có định dạng hợp lệ (một ký tự @, tên miền có dấu chấm, không khoảng trắng).</summary>
    public static bool IsValidEmail(string value)
    {
        if (value.Any(char.IsWhiteSpace) || value.Count(c => c == '@') != 1)
            return false;
        var at = value.IndexOf('@');
        var domain = value[(at + 1)..];
        if (at == 0 || domain.Length < 3 || !domain.Contains('.') || domain.StartsWith('.') || domain.EndsWith('.'))
            return false;
        return MailAddress.TryCreate(value, out var parsed) && string.Equals(parsed.Address, value, StringComparison.Ordinal);
    }
}

/// <summary>Chính sách mật khẩu (cấu hình <c>Security:Password:MinLength</c>, mặc định 8; luôn yêu cầu có chữ và số).</summary>
public sealed class PasswordPolicy
{
    public const int DefaultMinLength = 8;

    /// <summary>Độ dài tối đa (giới hạn của BCrypt là 72 byte).</summary>
    public const int MaxLength = 72;

    /// <summary>Khởi tạo chính sách; giá trị &lt; 8 bị nâng lên 8.</summary>
    public PasswordPolicy(int minLength = DefaultMinLength)
    {
        MinLength = Math.Clamp(minLength, DefaultMinLength, MaxLength);
    }

    public int MinLength { get; }

    /// <summary>Ném <see cref="ValidationException"/> nếu mật khẩu không đạt chính sách.</summary>
    public void Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            throw new ValidationException(
                $"Mật khẩu mới phải có tối thiểu {MinLength} ký tự, bao gồm cả chữ và số. Hãy chọn mật khẩu khác.");
        if (System.Text.Encoding.UTF8.GetByteCount(password) > MaxLength)
            throw new ValidationException($"Mật khẩu mới quá dài (tối đa {MaxLength} ký tự không dấu). Hãy chọn mật khẩu ngắn hơn.");
    }
}
