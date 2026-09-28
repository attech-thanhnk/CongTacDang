using System.Globalization;
using System.Text.RegularExpressions;

namespace CongTacDang.IntegrationTests.Infrastructure;

/// <summary>
/// Quy ước tên CSDL tạm của test tích hợp và các chốt an toàn trên máy chủ PostgreSQL dùng chung:
/// chỉ tạo/xóa CSDL tên <c>ctd_it_&lt;yyyyMMddHHmmss&gt;_&lt;guid8&gt;</c>, không bao giờ đụng CSDL khác.
/// </summary>
public static partial class TestDatabaseNames
{
    /// <summary>Tiền tố bắt buộc của mọi CSDL do test tạo.</summary>
    public const string Prefix = "ctd_it_";

    /// <summary>CSDL không bao giờ được kết nối tới (CSDL thử nghiệm của người điều phối).</summary>
    public static readonly string[] ForbiddenDatabases = { "congtacdang_test", "congtacdang" };

    /// <summary>Tuổi tối đa của CSDL sót lại từ lần chạy bị ngắt trước khi bị dọn.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    [GeneratedRegex("^ctd_it_(?<ts>[0-9]{14})_[0-9a-f]{8}$", RegexOptions.CultureInvariant)]
    private static partial Regex OwnNamePattern();

    /// <summary>Sinh tên CSDL mới theo thời điểm UTC.</summary>
    public static string NewName(DateTime utcNow) =>
        $"{Prefix}{utcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)}_{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>Tên có đúng định dạng CSDL do test tạo hay không.</summary>
    public static bool IsOwnName(string name) => OwnNamePattern().IsMatch(name);

    /// <summary>Từ chối thao tác trên CSDL không đúng định dạng test (chặn DROP/CREATE nhầm).</summary>
    public static void EnsureSafe(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal) || !IsOwnName(name))
            throw new InvalidOperationException(
                $"Từ chối thao tác trên CSDL \"{name}\": test tích hợp chỉ được tạo/xóa CSDL tên {Prefix}<yyyyMMddHHmmss>_<guid8>.");
    }

    /// <summary>Từ chối chuỗi kết nối trỏ tới CSDL bị cấm.</summary>
    public static void EnsureNotForbidden(string? database)
    {
        if (database != null && ForbiddenDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Từ chối kết nối tới CSDL \"{database}\". CONGTACDANG_TEST_PG phải trỏ tới CSDL quản trị (vd. postgres) bằng tài khoản có quyền CREATEDB.");
    }

    /// <summary>CSDL do test tạo đã quá hạn (sót lại từ lần chạy bị ngắt).</summary>
    public static bool IsStale(string name, DateTime utcNow)
    {
        var match = OwnNamePattern().Match(name);
        if (!match.Success)
            return false;

        return DateTime.TryParseExact(match.Groups["ts"].Value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var createdAt)
               && utcNow - createdAt > StaleAfter;
    }
}
