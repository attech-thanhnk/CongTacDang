using CongTacDang.IntegrationTests.Infrastructure;
using Npgsql;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Băm mật khẩu bằng pgcrypto của docker/dat-lai-mat-khau.sh phải tương thích với BCrypt.Net của ứng dụng.</summary>
public sealed class DatLaiMatKhauScriptTests
{
    private static readonly string? AdminConnection = Environment.GetEnvironmentVariable(ApiFactory.ConnectionEnvVar);

    [SkippableFact]
    public async Task HashCuaPgcrypto_DangNhapDuocBangBCryptNet_VaNguocLai()
    {
        Skip.If(string.IsNullOrWhiteSpace(AdminConnection), $"Bỏ qua: chưa đặt biến môi trường {ApiFactory.ConnectionEnvVar}.");
        var admin = TestDatabaseAdmin.AdminConnectionString(AdminConnection!);
        var name = TestDatabaseNames.NewName(DateTime.UtcNow);
        await TestDatabaseAdmin.CreateAsync(admin, name);
        try
        {
            const string password = "Tam2026xyzAB";
            await using var conn = new NpgsqlConnection(TestDatabaseAdmin.TargetConnectionString(admin, name));
            await conn.OpenAsync();
            await using (var ext = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS pgcrypto", conn))
                await ext.ExecuteNonQueryAsync();

            // Mật khẩu đi vào SQL dạng hex như trong script.
            var hex = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(password)).ToLowerInvariant();
            await using var hashCmd = new NpgsqlCommand(
                "SELECT crypt(convert_from(decode(@hex, 'hex'), 'UTF8'), gen_salt('bf', 10))", conn);
            hashCmd.Parameters.AddWithValue("hex", hex);
            var pgHash = (string)(await hashCmd.ExecuteScalarAsync())!;

            Assert.StartsWith("$2a$10$", pgHash);
            Assert.True(BCrypt.Net.BCrypt.Verify(password, pgHash));
            Assert.False(BCrypt.Net.BCrypt.Verify("Sai2026xyzAB", pgHash));

            await using var verifyCmd = new NpgsqlCommand("SELECT crypt(@pw, @hash) = @hash", conn);
            verifyCmd.Parameters.AddWithValue("pw", password);
            verifyCmd.Parameters.AddWithValue("hash", BCrypt.Net.BCrypt.HashPassword(password));
            Assert.True((bool)(await verifyCmd.ExecuteScalarAsync())!);
        }
        finally
        {
            await TestDatabaseAdmin.DropAsync(admin, name);
        }
    }
}
