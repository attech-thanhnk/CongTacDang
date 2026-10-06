using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace CongTacDang.Api.Extensions;

/// <summary>
/// Kiểm tra khóa ký JWT và mật khẩu trong cấu hình khi khởi động.
/// <para>Mọi môi trường: khóa JWT bắt buộc, ≥ 32 byte, không phải chuỗi mẫu cũ; phải có chuỗi kết nối.</para>
/// <para>Môi trường chạy thật (mọi môi trường trừ Development và Testing) kiểm tra thêm: khóa JWT không phải giá trị
/// giữ chỗ, không quá đơn điệu, không trùng mật khẩu CSDL; mật khẩu CSDL và mật khẩu quản trị ban đầu không phải giá trị
/// mẫu/mặc định từng có trong repo, không phải giá trị giữ chỗ, mật khẩu CSDL đủ dài.</para>
/// </summary>
public static class StartupSecretPolicy
{
    public const int MinJwtSecretBytes = 32;
    public const int MinJwtDistinctChars = 10;
    public const int MinDatabasePasswordLength = 12;

    /// <summary>
    /// SHA-256 (hex, chữ thường) của các giá trị khóa/mật khẩu mẫu hoặc mặc định từng nằm trong repo
    /// (appsettings, docker-compose, .env mẫu, CI, dữ liệu mẫu) và vài mật khẩu mặc định phổ biến.
    /// Chỉ lưu mã băm để không ghi lại giá trị cũ vào mã nguồn.
    /// </summary>
    private static readonly HashSet<string> KnownSampleValueHashes = new(StringComparer.Ordinal)
    {
        "0c9000722c3abc9953e84ea8877e0f8cde080fb90192bc8a95a9bffc667e2d74", // khóa JWT mẫu cũ trong appsettings
        "fc73856e1a6521713ae22ce76a56d5b4dad9899276d468a6eb76f3cbd7c01cd2", // mật khẩu CSDL mặc định cũ trong docker-compose
        "d7cde70755674662756a55779eff2427b4329d47e68c809703e156e996321702", // mật khẩu CSDL cũ trong appsettings.Development
        "aff83411888dce20ffc707c0dade69512499072c33476da563747bc47e2e18ca", // mật khẩu CSDL của CI
        "a0d43d3a214a0c9f9d0c1de84fa96bf8cf35825ce163a9be7d2ede9087c1afdf", // mật khẩu pgAdmin mặc định cũ
        "23efcf612a32a8c534c6192d22d0b7df4ab27462bf5df6337f01a992ad2b16c1", // mật khẩu tài khoản mẫu trong test
        "ad9858116e63b0c5a4d7dc7f50f034c7247e56838dae22c1832712ffde48e694", // khóa MinIO mặc định cũ
        "8d969eef6ecad3c29a3a629280e686cf0c3f5d5a86aff3ca12020c923adc6c92", // mật khẩu số đơn giản từng dùng cho tài khoản mẫu
        "a942b37ccfaf5a813b1432caa209a43b9d144e47ad0de1549c289c253e556cd5", // mật khẩu mặc định của PostgreSQL
    };

    /// <summary>Môi trường được nới lỏng (giữ cách kiểm tra cũ): Development và Testing (test tích hợp).</summary>
    public static bool IsRelaxedEnvironment(string environmentName) =>
        string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);

    /// <summary>Trả về danh sách lỗi cấu hình (rỗng = hợp lệ). Thông báo không chứa giá trị secret.</summary>
    public static IReadOnlyList<string> Validate(IConfiguration configuration, string environmentName)
    {
        var errors = new List<string>();
        var strict = !IsRelaxedEnvironment(environmentName);

        var jwtSecret = configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            errors.Add("Thiếu cấu hình bắt buộc 'Jwt:Secret' (biến môi trường Jwt__Secret). Hãy đặt bằng biến môi trường hoặc dotnet user-secrets.");
        }
        else
        {
            if (Encoding.UTF8.GetByteCount(jwtSecret) < MinJwtSecretBytes)
            {
                errors.Add($"Cấu hình 'Jwt:Secret' phải có ít nhất {MinJwtSecretBytes} byte. Sinh khóa mới bằng: openssl rand -base64 48");
            }

            if (IsKnownSampleValue(jwtSecret))
            {
                errors.Add("Cấu hình 'Jwt:Secret' vẫn là chuỗi mẫu cũ. Hãy sinh secret mới trước khi khởi động (openssl rand -base64 48).");
            }
            else if (strict && IsPlaceholder(jwtSecret))
            {
                errors.Add("Cấu hình 'Jwt:Secret' vẫn là giá trị giữ chỗ (CHANGE_ME) của file mẫu. Hãy sinh khóa mới bằng: openssl rand -base64 48");
            }
            else if (strict && jwtSecret.Distinct().Count() < MinJwtDistinctChars)
            {
                errors.Add("Cấu hình 'Jwt:Secret' quá đơn điệu (ít ký tự khác nhau), không phải khóa ngẫu nhiên. Hãy sinh khóa mới bằng: openssl rand -base64 48");
            }
        }

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("Thiếu cấu hình bắt buộc 'ConnectionStrings:Default'. Hãy đặt bằng biến môi trường hoặc dotnet user-secrets.");
        }
        else if (strict)
        {
            var dbPassword = TryGetPassword(connectionString);
            if (string.IsNullOrEmpty(dbPassword))
            {
                errors.Add("Chuỗi kết nối 'ConnectionStrings:Default' không có mật khẩu CSDL. Hãy đặt mật khẩu PostgreSQL mạnh (POSTGRES_PASSWORD trong docker/.env).");
            }
            else if (IsKnownSampleValue(dbPassword) || IsPlaceholder(dbPassword))
            {
                errors.Add("Mật khẩu CSDL trong 'ConnectionStrings:Default' là giá trị mẫu/mặc định từng có trong mã nguồn. Hãy đổi mật khẩu PostgreSQL (ALTER USER) và cập nhật POSTGRES_PASSWORD trong docker/.env.");
            }
            else if (dbPassword.Length < MinDatabasePasswordLength)
            {
                errors.Add($"Mật khẩu CSDL trong 'ConnectionStrings:Default' phải có ít nhất {MinDatabasePasswordLength} ký tự. Sinh mật khẩu mới bằng: openssl rand -base64 32");
            }

            if (!string.IsNullOrEmpty(dbPassword) && string.Equals(dbPassword, jwtSecret, StringComparison.Ordinal))
            {
                errors.Add("'Jwt:Secret' không được trùng mật khẩu CSDL. Hãy dùng hai giá trị ngẫu nhiên khác nhau.");
            }
        }

        if (strict)
        {
            var adminPassword = configuration["Seed:InitialAdmin:Password"];
            if (!string.IsNullOrEmpty(adminPassword) && (IsKnownSampleValue(adminPassword) || IsPlaceholder(adminPassword)))
            {
                errors.Add("Mật khẩu quản trị ban đầu 'Seed:InitialAdmin:Password' là giá trị mẫu/mặc định. Hãy đặt mật khẩu khác hoặc xóa biến này nếu đã có quản trị.");
            }
        }

        return errors;
    }

    /// <summary>Giá trị trùng (sau khi bỏ khoảng trắng hai đầu) một khóa/mật khẩu mẫu đã biết.</summary>
    public static bool IsKnownSampleValue(string value) =>
        KnownSampleValueHashes.Contains(Sha256Hex(value.Trim()));

    /// <summary>Giá trị giữ chỗ của file mẫu (<c>CHANGE_ME…</c>).</summary>
    public static bool IsPlaceholder(string value) =>
        value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
        || value.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase);

    private static string? TryGetPassword(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (var key in new[] { "Password", "Pwd" })
            {
                if (builder.TryGetValue(key, out var value) && value is not null)
                {
                    return value.ToString();
                }
            }

            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
