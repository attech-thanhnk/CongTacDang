using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Gọi API tệp đính kèm của phiếu/biên bản đúng kiểu multipart như giao diện.</summary>
internal static class FormAttachmentClient
{
    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string ownerType, Guid ownerId, string fileName = "can-cu.pdf")
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 test"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", fileName);
        content.Add(new StringContent("MAU00"), "formCode");
        content.Add(new StringContent(ownerType), "ownerType");
        content.Add(new StringContent(ownerId.ToString()), "ownerId");
        return await client.PostAsync("/api/attachments/upload", content);
    }

    public static async Task<HttpResponseMessage> ReplaceAsync(HttpClient client, Guid fileId)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent("%PDF-1.4 v2"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "can-cu-v2.pdf");
        return await client.PostAsync($"/api/attachments/{fileId}/versions", content);
    }

    public static async Task<Guid> UploadOkAsync(HttpClient client, string ownerType, Guid ownerId)
    {
        var response = await UploadAsync(client, ownerType, ownerId);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").GetProperty("id").GetGuid();
    }

    /// <summary>Mã nhóm phiên bản (bằng mã của bản đầu tiên) của các tệp hiện hành.</summary>
    public static async Task<List<Guid>> ListAsync(HttpClient client, string ownerType, Guid ownerId)
    {
        var response = await client.GetAsync($"/api/attachments?ownerType={ownerType}&ownerId={ownerId}");
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        return json.RootElement.GetProperty("data").EnumerateArray().Select(e => e.GetProperty("fileGroupId").GetGuid()).ToList();
    }

    /// <summary>Xem, tải xuống và xem phiên bản thành công.</summary>
    public static async Task AssertCanReadAsync(HttpClient client, string ownerType, Guid ownerId, Guid fileId)
    {
        Assert.Contains(fileId, await ListAsync(client, ownerType, ownerId));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/attachments/{fileId}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/attachments/{fileId}/versions")).StatusCode);
    }

    /// <summary>Danh sách rỗng, tải xuống và xem phiên bản bị chặn.</summary>
    public static async Task AssertCannotReadAsync(HttpClient client, string ownerType, Guid ownerId, Guid fileId)
    {
        Assert.Empty(await ListAsync(client, ownerType, ownerId));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/attachments/{fileId}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/attachments/{fileId}/versions")).StatusCode);
    }
}
