using System;
using System.IO;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Dịch vụ lưu trữ tệp tin vật lý trên ổ đĩa máy chủ cục bộ
/// Đảm bảo hoạt động độc lập, không phụ thuộc vào container MinIO ngoài
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageRoot;

    public LocalFileStorageService(string? storagePath = null)
    {
        if (!string.IsNullOrWhiteSpace(storagePath))
        {
            _storageRoot = storagePath;
        }
        else
        {
            _storageRoot = Path.Combine(AppContext.BaseDirectory, "uploads");
        }

        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    /// <summary>
    /// Lưu luồng dữ liệu tệp vào thư mục cục bộ theo cấu trúc phân cấp ObjectKey
    /// </summary>
    public async Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
    {
        var sanitizedKey = objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_storageRoot, sanitizedKey);

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(output);
        }

        return objectKey;
    }

    /// <summary>
    /// Đọc luồng dữ liệu của tệp tin từ ổ đĩa
    /// </summary>
    public Task<Stream?> GetFileStreamAsync(string objectKey)
    {
        var sanitizedKey = objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_storageRoot, sanitizedKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    /// <summary>
    /// Xóa tệp khỏi kho lưu trữ cục bộ
    /// </summary>
    public Task DeleteFileAsync(string objectKey)
    {
        var sanitizedKey = objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_storageRoot, sanitizedKey);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Kiểm tra tệp có tồn tại trên đĩa hay không
    /// </summary>
    public bool FileExists(string objectKey)
    {
        var sanitizedKey = objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_storageRoot, sanitizedKey);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Sinh liên kết tương đối tải tệp tin qua API
    /// </summary>
    public Task<string?> GetDownloadUrlAsync(string objectKey, string fileName, TimeSpan? expiry = null)
    {
        return Task.FromResult<string?>($"/api/attachments/download-by-key?key={Uri.EscapeDataString(objectKey)}");
    }
}
