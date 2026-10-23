using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CongTacDang.Application.Common.Security;
using CongTacDang.Infrastructure.Data;
using CongTacDang.IntegrationTests.Infrastructure;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Ảnh nền và vị trí khung màn hình đăng nhập; test luôn khôi phục về không ảnh, khung giữa.</summary>
[Collection(ApiCollection.Name)]
public sealed class LoginBackgroundIntegrationTests
{
    private const string Url = "/api/settings/organization/login-background";

    /// <summary>PNG 1×1 hợp lệ.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");

    /// <summary>Đầu tệp JPEG (SOI + APP0 JFIF).</summary>
    private static readonly byte[] Jpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
        0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9
    ];

    private readonly ApiFactory _factory;

    public LoginBackgroundIntegrationTests(ApiFactory factory) => _factory = factory;

    private void SkipIfNoDatabase() => Skip.If(_factory.SkipReason != null, _factory.SkipReason);

    private async Task<HttpClient> LoginWithAsync(params string[] codes)
    {
        var user = await _factory.CreateUserWithPermissionsAsync(codes);
        return await _factory.LoginAsAsync(user.Username, user.Password, distinctClientIp: true);
    }

    [SkippableFact]
    public async Task Upload_Replace_AnonymousGet_Delete()
    {
        SkipIfNoDatabase();
        var manager = await LoginWithAsync(PermissionCodes.SystemSettingsManage);
        using var anonymous = _factory.CreateClient();

        try
        {
            var first = await DataAsync(await UploadAsync(manager, Png, "nen.png", "image/png"));
            var firstUrl = first.GetProperty("loginBackgroundUrl").GetString();
            Assert.StartsWith($"{Url}?v=", firstUrl);
            Assert.Equal("nen.png", first.GetProperty("loginBackgroundFileName").GetString());

            var pub = await DataAsync(await anonymous.GetAsync("/api/settings/organization/public"));
            Assert.Equal(firstUrl, pub.GetProperty("loginBackgroundUrl").GetString());

            var image = await anonymous.GetAsync(firstUrl);
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
            Assert.Contains("nosniff", image.Headers.GetValues("X-Content-Type-Options"));
            Assert.True(image.Headers.CacheControl?.MaxAge > TimeSpan.FromDays(1));

            // Thay ảnh: URL đổi phiên bản, nội dung mới.
            var second = await DataAsync(await UploadAsync(manager, Jpeg, "nen.jpg", "image/jpeg"));
            var secondUrl = second.GetProperty("loginBackgroundUrl").GetString();
            Assert.NotEqual(firstUrl, secondUrl);
            var replaced = await anonymous.GetAsync(secondUrl);
            Assert.Equal("image/jpeg", replaced.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Jpeg, await replaced.Content.ReadAsByteArrayAsync());

            var removed = await DataAsync(await manager.DeleteAsync(Url));
            Assert.Equal(JsonValueKind.Null, removed.GetProperty("loginBackgroundUrl").ValueKind);
            Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(Url)).StatusCode);
            var pubAfter = await DataAsync(await anonymous.GetAsync("/api/settings/organization/public"));
            Assert.Equal(JsonValueKind.Null, pubAfter.GetProperty("loginBackgroundUrl").ValueKind);
        }
        finally
        {
            await manager.DeleteAsync(Url);
        }
    }

    [SkippableFact]
    public async Task Upload_RejectsInvalidFiles_AndRequiresPermission()
    {
        SkipIfNoDatabase();
        var manager = await LoginWithAsync(PermissionCodes.SystemSettingsManage);

        try
        {
            // Sai định dạng.
            Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(manager, Png, "nen.gif", "image/gif")).StatusCode);
            // Đổi đuôi: nội dung PNG khai báo là JPG, văn bản khai báo là PNG.
            Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(manager, Png, "nen.jpg", "image/jpeg")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await UploadAsync(manager, "khong phai anh"u8.ToArray(), "nen.png", "image/png")).StatusCode);
            // Quá 5 MB.
            var tooLarge = new byte[5 * 1024 * 1024 + 1];
            Png.CopyTo(tooLarge, 0);
            Assert.Equal(HttpStatusCode.BadRequest, (await UploadAsync(manager, tooLarge, "lon.png", "image/png")).StatusCode);

            var settings = await DataAsync(await manager.GetAsync("/api/settings/organization"));
            Assert.Equal(JsonValueKind.Null, settings.GetProperty("loginBackgroundUrl").ValueKind);

            var plain = await LoginWithAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await UploadAsync(plain, Png, "nen.png", "image/png")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await plain.DeleteAsync(Url)).StatusCode);

            using var anonymous = _factory.CreateClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(anonymous, Png, "nen.png", "image/png")).StatusCode);
        }
        finally
        {
            await manager.DeleteAsync(Url);
        }
    }

    [SkippableFact]
    public async Task PanelPosition_Validated_KeptWhenOmitted()
    {
        SkipIfNoDatabase();
        var manager = await LoginWithAsync(PermissionCodes.SystemSettingsManage);
        using var anonymous = _factory.CreateClient();

        try
        {
            var invalid = await PutSettingsAsync(manager, "top");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            var message = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync()).RootElement.GetProperty("message").GetString();
            Assert.Contains("Vị trí khung đăng nhập", message);

            Assert.Equal(HttpStatusCode.OK, (await PutSettingsAsync(manager, "left")).StatusCode);
            var pub = await DataAsync(await anonymous.GetAsync("/api/settings/organization/public"));
            Assert.Equal("left", pub.GetProperty("loginPanelPosition").GetString());

            // Không gửi trường → giữ nguyên.
            Assert.Equal(HttpStatusCode.OK, (await PutSettingsAsync(manager, null)).StatusCode);
            var kept = await DataAsync(await manager.GetAsync("/api/settings/organization"));
            Assert.Equal("left", kept.GetProperty("loginPanelPosition").GetString());
        }
        finally
        {
            await PutSettingsAsync(manager, "center");
        }
    }

    private static Task<HttpResponseMessage> PutSettingsAsync(HttpClient client, string? position)
    {
        var d = DataSeeder.DefaultOrganizationSettings();
        object body = position == null
            ? new { d.PartyCommitteeName, d.SuperiorPartyName, d.CompanyName, d.ParentCompanyName, d.ShortName, d.Location, d.AppraisalUnitName, d.SystemName }
            : new { d.PartyCommitteeName, d.SuperiorPartyName, d.CompanyName, d.ParentCompanyName, d.ShortName, d.Location, d.AppraisalUnitName, d.SystemName, loginPanelPosition = position };
        return client.PutAsJsonAsync("/api/settings/organization", body);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return client.PostAsync(Url, form);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }
}
