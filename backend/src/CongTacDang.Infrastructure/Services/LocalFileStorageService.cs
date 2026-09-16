using System;
using System.IO;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Storage Adapter cục bộ (Local Filesystem) - hỗ trợ cấu trúc thư mục phân tầng an toàn
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageRoot;

    public string ProviderName => "local";

    public LocalFileStorageService(string storageRoot)
    {
        _storageRoot = Path.GetFullPath(storageRoot);
        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
    {
        var fullPath = GetSecurePath(objectKey);
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

    public Task<Stream?> GetFileStreamAsync(string objectKey)
    {
        var fullPath = GetSecurePath(objectKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string objectKey)
    {
        var fullPath = GetSecurePath(objectKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }

    public bool FileExists(string objectKey)
    {
        var fullPath = GetSecurePath(objectKey);
        return File.Exists(fullPath);
    }

    public Task<string?> GetDownloadUrlAsync(string objectKey, string fileName, TimeSpan? expiry = null)
    {
        // Local adapter không có endpoint presigned URL độc lập, backend sẽ stream dữ liệu trực tiếp
        return Task.FromResult<string?>(null);
    }

    private string GetSecurePath(string objectKey)
    {
        var normalizedKey = objectKey.Replace('/', Path.DirectorySeparatorChar)
                                     .Replace('\\', Path.DirectorySeparatorChar)
                                     .TrimStart(Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, normalizedKey));

        // Bảo vệ chống Path Traversal
        if (!fullPath.StartsWith(_storageRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Đường dẫn tệp không hợp lệ hoặc vi phạm giới hạn an toàn.");
        }

        return fullPath;
    }
}
