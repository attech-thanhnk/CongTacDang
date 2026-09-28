using System.Net.Http.Json;
using System.Security.Cryptography;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CongTacDang.IntegrationTests.Infrastructure;

/// <summary>Người dùng tạo cho test (mật khẩu dạng rõ để đăng nhập).</summary>
public sealed record TestUser(Guid Id, string Username, string Password, Guid RoleId, string RoleCode);

/// <summary>
/// Dựng API thật trên PostgreSQL thật cho test tích hợp.
/// <list type="bullet">
/// <item>Chuỗi kết nối quản trị đọc từ biến môi trường <c>CONGTACDANG_TEST_PG</c> (CSDL quản trị, tài khoản có CREATEDB).
/// Thiếu biến → mọi test <b>skip</b> kèm lý do.</item>
/// <item>Mỗi lần chạy tạo CSDL <c>ctd_it_&lt;yyyyMMddHHmmss&gt;_&lt;guid8&gt;</c>, <c>EnsureCreated</c>, seed tối thiểu
/// (vai trò + quyền qua DataSeeder), xóa CSDL khi xong; dọn CSDL <c>ctd_it_*</c> sót lại quá 24 giờ.</item>
/// <item>Chỉ tạo/xóa CSDL đúng định dạng <c>ctd_it_*</c>; từ chối chuỗi kết nối trỏ tới <c>congtacdang_test</c>.</item>
/// </list>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>Tên biến môi trường chứa chuỗi kết nối quản trị.</summary>
    public const string ConnectionEnvVar = "CONGTACDANG_TEST_PG";

    /// <summary>Mật khẩu mặc định của người dùng do test tạo.</summary>
    public const string DefaultPassword = "Test12345678";

    private readonly string _jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    private readonly string _storagePath = Path.Combine(Path.GetTempPath(), "congtacdang-it", Guid.NewGuid().ToString("N"));
    private string? _adminConnectionString;
    private string? _targetConnectionString;
    private bool _databaseCreated;

    /// <summary>Tên CSDL tạm của lần chạy này.</summary>
    public string DatabaseName { get; } = TestDatabaseNames.NewName(DateTime.UtcNow);

    /// <summary>Lý do bỏ qua (null nếu môi trường đủ điều kiện chạy).</summary>
    public string? SkipReason { get; private set; }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            SkipReason = $"Bỏ qua test tích hợp: chưa đặt biến môi trường {ConnectionEnvVar} "
                + "(chuỗi kết nối PostgreSQL tới CSDL quản trị, tài khoản có quyền CREATEDB).";
            Console.WriteLine(SkipReason);
            return;
        }

        var admin = new NpgsqlConnectionStringBuilder(connectionString);
        TestDatabaseNames.EnsureNotForbidden(admin.Database);
        _adminConnectionString = admin.ConnectionString;

        await DropStaleDatabasesAsync();

        TestDatabaseNames.EnsureSafe(DatabaseName);
        await ExecuteAdminAsync($"CREATE DATABASE \"{DatabaseName}\"");
        _databaseCreated = true;

        var target = new NpgsqlConnectionStringBuilder(connectionString) { Database = DatabaseName };
        _targetConnectionString = target.ConnectionString;

        // Tạo schema trực tiếp từ model (chưa cần migration), rồi khởi động host → DataSeeder tạo quyền + vai trò.
        var options = new DbContextOptionsBuilder<CongTacDangDbContext>().UseNpgsql(_targetConnectionString).Options;
        await using (var db = new CongTacDangDbContext(options, new SystemCurrentUser()))
        {
            await db.Database.EnsureCreatedAsync();
        }

        _ = Server;
    }

    /// <inheritdoc />
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();

        if (_databaseCreated)
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabaseAsync(DatabaseName);
        }

        try
        {
            if (Directory.Exists(_storagePath))
                Directory.Delete(_storagePath, recursive: true);
        }
        catch (IOException)
        {
            // Thư mục tạm, bỏ qua nếu không xóa được.
        }
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_targetConnectionString == null)
            throw new InvalidOperationException(SkipReason ?? "CSDL test chưa được khởi tạo.");

        // Môi trường "Testing": không nạp user-secrets / appsettings.Development.json của máy.
        builder.UseEnvironment("Testing");
        // UseSetting có hiệu lực ngay khi Program đọc cấu hình (trước Build).
        builder.UseSetting("ConnectionStrings:Default", _targetConnectionString);
        builder.UseSetting("Jwt:Secret", _jwtSecret);
        builder.UseSetting("Database:AutoMigrate", "false");
        builder.UseSetting("Database:SeedSampleData", "false");
        builder.UseSetting("Database:ResetRolePermissions", "false");
        builder.UseSetting("Storage:Local:Path", _storagePath);
        builder.UseSetting("Swagger:Enabled", "false");
        builder.UseSetting("Logging:File:Enabled", "false");
        builder.UseSetting("Logging:Console:Json", "false");
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
    }

    /// <summary>Đăng nhập và trả về HttpClient giữ cookie phiên (auth_token, refresh_token).</summary>
    public async Task<HttpClient> LoginAsAsync(string username, string password)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            client.Dispose();
            throw new InvalidOperationException($"Đăng nhập {username} thất bại: {(int)response.StatusCode} {body}");
        }

        return client;
    }

    /// <summary>Đăng nhập bằng người dùng test.</summary>
    public Task<HttpClient> LoginAsAsync(TestUser user) => LoginAsAsync(user.Username, user.Password);

    /// <summary>
    /// Tạo người dùng đang hoạt động (không bắt buộc đổi mật khẩu) với một vai trò riêng chứa đúng các mã quyền cho trước.
    /// Mã quyền chưa có trong CSDL được tạo thêm.
    /// </summary>
    public async Task<TestUser> CreateUserWithPermissionsAsync(params string[] codes)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var distinct = codes.Distinct(StringComparer.Ordinal).ToList();
        var permissions = await db.Permissions.Where(p => distinct.Contains(p.Code)).ToListAsync();
        foreach (var missing in distinct.Except(permissions.Select(p => p.Code)))
        {
            var permission = new Permission { Code = missing, Name = missing, Resource = "test", Action = "test" };
            db.Permissions.Add(permission);
            permissions.Add(permission);
        }

        var role = new AppRole
        {
            Code = $"IT_{suffix.ToUpperInvariant()}",
            Name = $"Vai trò test {suffix}",
            Description = "Tạo bởi test tích hợp",
            Permissions = permissions
        };
        var user = new PartyMemberProfile
        {
            Username = $"it_{suffix}",
            FullName = $"Người dùng test {suffix}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            MustChangePassword = false,
            IsActive = true,
            Roles = new List<AppRole> { role }
        };

        db.Roles.Add(role);
        db.PartyMemberProfiles.Add(user);
        await db.SaveChangesAsync();

        return new TestUser(user.Id, user.Username, DefaultPassword, role.Id, role.Code);
    }

    private async Task DropStaleDatabasesAsync()
    {
        var stale = new List<string>();
        await using (var connection = new NpgsqlConnection(_adminConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(
                "SELECT datname FROM pg_database WHERE datname LIKE 'ctd\\_it\\_%'", connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                if (TestDatabaseNames.IsStale(name, DateTime.UtcNow))
                    stale.Add(name);
            }
        }

        foreach (var name in stale)
        {
            try
            {
                await DropDatabaseAsync(name);
                Console.WriteLine($"Đã dọn CSDL test sót lại: {name}");
            }
            catch (PostgresException ex)
            {
                Console.WriteLine($"Không dọn được CSDL test sót lại {name}: {ex.MessageText}");
            }
        }
    }

    private Task DropDatabaseAsync(string name)
    {
        TestDatabaseNames.EnsureSafe(name);
        return ExecuteAdminAsync($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)");
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Actor hệ thống khi tạo schema ngoài request.</summary>
    private sealed class SystemCurrentUser : ICurrentUserService
    {
        public Guid? UserId => null;
        public string UserName => "integration-test";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }
}

/// <summary>Collection dùng chung một <see cref="ApiFactory"/> (một CSDL tạm) cho mọi lớp test tích hợp.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    /// <summary>Tên collection.</summary>
    public const string Name = "api";
}
