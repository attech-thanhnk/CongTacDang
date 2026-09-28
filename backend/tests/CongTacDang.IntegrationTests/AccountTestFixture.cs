using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>
/// Hạ tầng test tích hợp của task 08 (tài khoản & phiên). Dùng <b>CSDL tạm riêng</b> (collection riêng) vì các luồng
/// L9 cần kiểm soát ai đang giữ quyền quản trị, và một host dẫn xuất nâng giới hạn tốc độ đăng nhập
/// (<c>Security:RateLimit:Auth:PermitLimit</c>) vì các luồng khóa tài khoản cần nhiều lần đăng nhập.
/// Không đổi hành vi mặc định của <see cref="ApiFactory"/>.
/// </summary>
public sealed class AccountTestFixture : IAsyncLifetime
{
    /// <summary>Mã quyền của "quản trị tài khoản" dùng trong test (không có quyền quản trị vai trò/gán vai trò).</summary>
    public static readonly string[] AccountAdminPermissions =
    {
        PermissionCodes.SystemUsersRead,
        PermissionCodes.SystemUsersManage,
        PermissionCodes.SystemAuditRead
    };

    /// <summary>Factory gốc: tạo/xóa CSDL tạm, helper tạo người dùng.</summary>
    public ApiFactory Base { get; } = new();

    /// <summary>Host API dùng trong test (cùng CSDL, giới hạn đăng nhập cao).</summary>
    public WebApplicationFactory<Program> App { get; private set; } = null!;

    /// <summary>Lý do bỏ qua (null nếu chạy được).</summary>
    public string? SkipReason => Base.SkipReason;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await Base.InitializeAsync();
        if (Base.SkipReason != null)
            return;

        App = Base.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Security:RateLimit:Auth:PermitLimit", "1000");
        });
        _ = App.Server;
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await ((IAsyncLifetime)Base).DisposeAsync();
    }

    /// <summary>HttpClient giữ cookie, chưa đăng nhập.</summary>
    public HttpClient NewClient() =>
        App.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>Gọi đăng nhập, trả client (giữ cookie nếu thành công) và phản hồi.</summary>
    public async Task<(HttpClient Client, HttpResponseMessage Response)> TryLoginAsync(string username, string password)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        return (client, response);
    }

    /// <summary>Đăng nhập, ném lỗi nếu thất bại.</summary>
    public async Task<HttpClient> LoginAsync(string username, string password)
    {
        var (client, response) = await TryLoginAsync(username, password);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            client.Dispose();
            throw new InvalidOperationException($"Đăng nhập {username} thất bại: {(int)response.StatusCode} {body}");
        }

        return client;
    }

    /// <summary>Tạo quản trị tài khoản và đăng nhập.</summary>
    public async Task<(TestUser User, HttpClient Client)> CreateAccountAdminAsync()
    {
        var admin = await Base.CreateUserWithPermissionsAsync(AccountAdminPermissions);
        return (admin, await LoginAsync(admin.Username, admin.Password));
    }

    /// <summary>Tạo tài khoản qua API (<c>POST /api/users</c>), trả Id và mật khẩu tạm.</summary>
    public static async Task<(Guid Id, string Username, string TemporaryPassword)> CreateViaApiAsync(HttpClient admin, string username)
    {
        var response = await admin.PostAsJsonAsync("/api/users", new { username, fullName = "Cán bộ " + username });
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        var data = json.RootElement.GetProperty("data");
        return (data.GetProperty("userId").GetGuid(), data.GetProperty("username").GetString()!, data.GetProperty("temporaryPassword").GetString()!);
    }

    /// <summary>Tên đăng nhập ngẫu nhiên hợp lệ.</summary>
    public static string NewUsername(string prefix) => $"{prefix}.{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 50)];

    /// <summary>Đọc JSON phản hồi.</summary>
    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        return json.RootElement.Clone();
    }
}

/// <summary>Collection riêng của task 08 (CSDL tạm riêng).</summary>
[CollectionDefinition(Name)]
public sealed class AccountCollection : ICollectionFixture<AccountTestFixture>
{
    /// <summary>Tên collection.</summary>
    public const string Name = "accounts";
}
