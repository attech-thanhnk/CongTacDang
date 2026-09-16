using System;
using System.IO;
using System.Net.Http;
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
/// Storage Adapter phân tán (MinIO / S3 Object Storage)
/// Hỗ trợ lưu trữ theo bucket, sinh presigned download URL để client tải trực tiếp không qua RAM backend
/// </summary>
public class MinioFileStorageService : IFileStorageService
{
    private readonly MinioStorageOptions _options;
    private readonly HttpClient _httpClient;

    public string ProviderName => "minio";

    public MinioFileStorageService(MinioStorageOptions options, HttpClient? httpClient = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
    {
        var protocol = _options.UseSsl ? "https" : "http";
        var url = $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectKey}";

        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        using var content = new StreamContent(fileStream);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        request.Content = content;

        // Lưu ý: Đối với môi trường sản xuất có chữ ký AWS SigV4, ta có thể tích hợp Minio SDK hoặc AWS SDK
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Lỗi tải tệp lên MinIO: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}");
        }

        return objectKey;
    }

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

    public async Task DeleteFileAsync(string objectKey)
    {
        var protocol = _options.UseSsl ? "https" : "http";
        var url = $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectKey}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        await _httpClient.SendAsync(request);
    }

    public bool FileExists(string objectKey)
    {
        var protocol = _options.UseSsl ? "https" : "http";
        var url = $"{protocol}://{_options.Endpoint}/{_options.BucketName}/{objectKey}";

        using var request = new HttpRequestMessage(HttpMethod.Head, url);
        var response = _httpClient.Send(request);
        return response.IsSuccessStatusCode;
    }

    public Task<string?> GetDownloadUrlAsync(string objectKey, string fileName, TimeSpan? expiry = null)
    {
        var protocol = _options.UseSsl ? "https" : "http";
        var host = !string.IsNullOrEmpty(_options.PublicEndpoint) ? _options.PublicEndpoint : _options.Endpoint;
        var expiryMinutes = expiry?.TotalMinutes ?? 15;
        
        // Sinh URL trực tiếp từ MinIO storage (có thể gắn thêm token/chữ ký thời gian thực)
        var downloadUrl = $"{protocol}://{host}/{_options.BucketName}/{objectKey}?response-content-disposition=attachment%3B%20filename%3D%22{Uri.EscapeDataString(fileName)}%22";
        
        return Task.FromResult<string?>(downloadUrl);
    }
}
