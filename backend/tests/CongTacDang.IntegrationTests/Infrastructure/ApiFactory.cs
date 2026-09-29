using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
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
/// <item>Mỗi lần chạy tạo CSDL <c>ctd_it_&lt;yyyyMMddHHmmss&gt;_&lt;guid8&gt;</c>, áp dụng migration, seed tối thiểu
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

        _adminConnectionString = TestDatabaseAdmin.AdminConnectionString(connectionString);

        await DropStaleDatabasesAsync();

        await TestDatabaseAdmin.CreateAsync(_adminConnectionString, DatabaseName);
        _databaseCreated = true;
        _targetConnectionString = TestDatabaseAdmin.TargetConnectionString(_adminConnectionString, DatabaseName);

        // Tạo schema bằng migration (như triển khai thật), rồi khởi động host → DataSeeder tạo quyền + vai trò.
        var options = new DbContextOptionsBuilder<CongTacDangDbContext>().UseNpgsql(_targetConnectionString).Options;
        await using (var db = new CongTacDangDbContext(options, new SystemCurrentUser()))
        {
            await db.Database.MigrateAsync();
        }

        _ = Server;
    }

    /// <inheritdoc />
    async Task IAsyncLifetime.DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        catch (NpgsqlException ex)
        {
            // Lỗi CSDL khi dừng host sau khi mọi test đã xong (vd. kết nối bị máy chủ dùng chung ngắt): ghi rõ, vẫn dọn CSDL.
            Console.WriteLine($"Lỗi CSDL khi dừng host test ({DatabaseName}): {TestDatabaseAdmin.Describe(ex)}");
        }

        // Dọn CSDL là việc phụ: đã thử lại mà vẫn lỗi thì ghi rõ lỗi và để lượt dọn CSDL sót lại (quá 24 giờ) xóa sau,
        // không làm hỏng kết quả của các test đã chạy xong.
        if (_databaseCreated)
            await TestDatabaseAdmin.TryDropAsync(_adminConnectionString!, DatabaseName);

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

        // Tùy chọn cho test (không đổi hành vi mặc định): request có header X-Test-Client-Ip được gán địa chỉ IP đó,
        // để test nhiều người dùng không cùng rơi vào một phân vùng giới hạn đăng nhập (10 lần/phút/IP).
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>());
    }

    /// <summary>Header dùng để giả lập địa chỉ IP client trong test (xem <see cref="LoginAsAsync(string, string, bool)"/>).</summary>
    public const string TestClientIpHeader = "X-Test-Client-Ip";

    /// <summary>Đăng nhập và trả về HttpClient giữ cookie phiên (auth_token, refresh_token).</summary>
    public Task<HttpClient> LoginAsAsync(string username, string password) => LoginAsAsync(username, password, distinctClientIp: false);

    /// <summary>
    /// Đăng nhập; <paramref name="distinctClientIp"/> = true → client dùng một địa chỉ IP giả riêng (header
    /// <see cref="TestClientIpHeader"/>) nên không chia chung giới hạn 10 lần đăng nhập/phút với test khác.
    /// </summary>
    public async Task<HttpClient> LoginAsAsync(string username, string password, bool distinctClientIp)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        if (distinctClientIp)
        {
            var bytes = RandomNumberGenerator.GetBytes(3);
            client.DefaultRequestHeaders.Add(TestClientIpHeader, $"10.{bytes[0]}.{bytes[1]}.{bytes[2]}");
        }

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
    /// Tạo người dùng đang hoạt động (không bắt buộc đổi mật khẩu) với một vai trò riêng chứa đúng các mã quyền cho trước,
    /// gán phạm vi Toàn công ty (task 09: bản gán <see cref="UserRoleAssignment"/>). Mã quyền chưa có trong CSDL được tạo thêm
    /// (mã không thuộc danh mục <c>PermissionCodes</c> không có hiệu lực).
    /// </summary>
    public async Task<TestUser> CreateUserWithPermissionsAsync(params string[] codes)
    {
        var role = await CreateRoleAsync(codes);
        var user = await CreateUserAsync();
        await AssignAsync(user.Id, role.Id, RoleScopeType.Global, null);
        return user with { RoleId = role.Id, RoleCode = role.Code };
    }

    /// <summary>Tạo vai trò test chứa đúng các mã quyền cho trước.</summary>
    public async Task<(Guid Id, string Code)> CreateRoleAsync(params string[] codes)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var distinct = codes.Distinct(StringComparer.Ordinal).ToList();
        var permissions = await db.Permissions.Where(p => distinct.Contains(p.Code)).ToListAsync();
        foreach (var missing in distinct.Except(permissions.Select(p => p.Code)))
        {
            var permission = new Permission { Code = missing, Name = missing, Module = "test" };
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
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return (role.Id, role.Code);
    }

    /// <summary>Tạo người dùng đang hoạt động, chưa có bản gán vai trò nào.</summary>
    public async Task<TestUser> CreateUserAsync(
        Guid? departmentId = null,
        Guid? partyCellId = null,
        ApprovalAuthority approvalAuthority = ApprovalAuthority.CoSo,
        string? fullName = null)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = new PartyMemberProfile
        {
            Username = $"it_{suffix}",
            FullName = fullName ?? $"Người dùng test {suffix}",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultPassword),
            MustChangePassword = false,
            IsActive = true,
            DepartmentId = departmentId,
            PartyCellId = partyCellId,
            ApprovalAuthority = approvalAuthority
        };
        db.PartyMemberProfiles.Add(user);
        await db.SaveChangesAsync();
        return new TestUser(user.Id, user.Username, DefaultPassword, Guid.Empty, string.Empty);
    }

    /// <summary>Gán vai trò trực tiếp trong CSDL (bỏ qua API/chốt chặn — dùng để dựng dữ liệu test) và xóa cache quyền.</summary>
    public async Task<Guid> AssignAsync(Guid userId, Guid roleId, RoleScopeType scopeType, Guid? scopeId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        var assignment = new UserRoleAssignment
        {
            UserId = userId,
            RoleId = roleId,
            ScopeType = scopeType,
            ScopeId = scopeId,
            ValidFrom = DateTime.UtcNow.AddMinutes(-1)
        };
        db.Set<UserRoleAssignment>().Add(assignment);
        await db.SaveChangesAsync();
        Services.GetRequiredService<PermissionCache>().InvalidateUser(userId);
        return assignment.Id;
    }

    /// <summary>Id vai trò theo mã (vai trò mặc định do seeder tạo).</summary>
    public async Task<Guid> GetRoleIdAsync(string roleCode)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        return await db.Roles.Where(r => r.Code == roleCode).Select(r => r.Id).SingleAsync();
    }

    /// <summary>Id vai trò quản trị hệ thống mặc định (vai trò được bảo vệ duy nhất do seeder tạo).</summary>
    public async Task<Guid> GetAdministratorRoleIdAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        return await db.Roles.Where(r => r.IsProtected && r.IsSystem).OrderBy(r => r.CreatedAt).Select(r => r.Id).FirstAsync();
    }

    /// <summary>Thao tác trực tiếp trên CSDL test (dựng dữ liệu).</summary>
    public async Task WithDbAsync(Func<CongTacDangDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CongTacDangDbContext>();
        await action(db);
    }

    /// <summary>Dọn CSDL test sót lại quá hạn (lần chạy bị ngắt); lỗi khi dọn không làm hỏng lần chạy hiện tại.</summary>
    private async Task DropStaleDatabasesAsync()
    {
        var stale = (await TestDatabaseAdmin.ListAsync(_adminConnectionString!))
            .Where(name => TestDatabaseNames.IsStale(name, DateTime.UtcNow))
            .ToList();

        foreach (var name in stale)
        {
            try
            {
                await TestDatabaseAdmin.DropAsync(_adminConnectionString!, name);
                Console.WriteLine($"Đã dọn CSDL test sót lại: {name}");
            }
            catch (NpgsqlException ex)
            {
                Console.WriteLine($"Không dọn được CSDL test sót lại {name}: {ex.Message}");
            }
        }
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

/// <summary>
/// Middleware đầu pipeline (chỉ trong test): request có header <see cref="ApiFactory.TestClientIpHeader"/> được gán
/// <c>RemoteIpAddress</c> theo header — để giới hạn đăng nhập theo IP phân vùng theo từng client test. Không có header → không đổi gì.
/// </summary>
internal sealed class TestClientIpStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (context.Request.Headers.TryGetValue(ApiFactory.TestClientIpHeader, out var value)
                && IPAddress.TryParse(value.ToString(), out var ip))
            {
                context.Connection.RemoteIpAddress = ip;
            }

            await nextMiddleware();
        });
        next(app);
    };
}

/// <summary>Collection dùng chung một <see cref="ApiFactory"/> (một CSDL tạm) cho mọi lớp test tích hợp.</summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    /// <summary>Tên collection.</summary>
    public const string Name = "api";
}
