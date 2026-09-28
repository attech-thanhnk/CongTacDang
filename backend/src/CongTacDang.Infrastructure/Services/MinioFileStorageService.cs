using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Infrastructure.Services;

public class MinioStorageOptions
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string BucketName { get; set; } = "congtacdang-files";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool UseSsl { get; set; } = false;
    public string? PublicEndpoint { get; set; }
}

/// <summary>
/// Storage adapter cho MinIO / S3 Object Storage
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
    private readonly MinioStorageOptions _options;
    private readonly HttpClient _httpClient;

    public MinioFileStorageService(MinioStorageOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Lưu luồng dữ liệu tệp lên MinIO bucket theo ObjectKey
    /// </summary>
    public async Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
    {
        await EnsureBucketExistsAsync();

        var protocol = _options.UseSsl ? "https" : "http";
        var url = $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectKey}";

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Lỗi tải tệp lên MinIO: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
        }

        return objectKey;
    }

    /// <summary>
    /// Kiểm tra và tự động khởi tạo bucket nếu chưa tồn tại
    /// </summary>
    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            var protocol = _options.UseSsl ? "https" : "http";
            var bucketUrl = $"{protocol}://{_options.Endpoint}/{_options.BucketName}";
            using var checkReq = new HttpRequestMessage(HttpMethod.Head, bucketUrl);
            var checkRes = await _httpClient.SendAsync(checkReq);
            if (checkRes.StatusCode == HttpStatusCode.NotFound)
            {
                using var createReq = new HttpRequestMessage(HttpMethod.Put, bucketUrl);
                await _httpClient.SendAsync(createReq);
            }
        }
        catch
        {
            // Bỏ qua nếu bucket đã tồn tại
        }
    }

    /// <summary>
    /// Đọc luồng dữ liệu (Stream) của tệp từ MinIO bucket
    /// </summary>
    public async Task<Stream?> GetFileStreamAsync(string objectKey)
    {
        var protocol = _options.UseSsl ? "https" : "http";
        var url = $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectKey}";

        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadAsStreamAsync();
    }

    /// <summary>
    /// Xóa tệp khỏi MinIO bucket theo ObjectKey
    /// </summary>
    public async Task DeleteFileAsync(string objectKey)
    {
        var protocol = _options.UseSsl ? "https" : "http";
        var url = $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectKey}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        await _httpClient.SendAsync(request);
    }

    /// <summary>
    /// Kiểm tra tệp có tồn tại trên MinIO bucket hay không
    /// </summary>
    public bool FileExists(string objectKey)
    {
        var protocol = _options.UseSsl ? "https" : "http";
        var url = $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectKey}";

        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        var response = _httpClient.Send(request);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Sinh URL tải xuống trực tiếp tệp từ MinIO
    /// </summary>
    public Task<string?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan? expiry = null)
    {
        return Task.FromResult<string?>($"/api/attachments/{attachmentId}/download");
    }
}
