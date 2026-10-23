using System;
using System.IO;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Infrastructure.Services;

/// <summary>Lưu tệp trên đĩa cục bộ của máy chủ (cấu hình Storage:Local:Path).</summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storageRoot;

    public LocalFileStorageService(string? storagePath = null)
    {
        if (!string.IsNullOrWhiteSpace(storagePath))
        {
            _storageRoot = Path.GetFullPath(storagePath);
        }
        else
        {
            _storageRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "uploads"));
        }

        if (!Directory.Exists(_storageRoot))
        {
            Directory.CreateDirectory(_storageRoot);
        }
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
    {
        var fullPath = GetSafePath(objectKey);

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
        var fullPath = GetSafePath(objectKey);

        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string objectKey)
    {
        var fullPath = GetSafePath(objectKey);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public bool FileExists(string objectKey)
    {
        var fullPath = GetSafePath(objectKey);
        return File.Exists(fullPath);
    }

    /// <summary>Liên kết tương đối tải tệp qua API.</summary>
    public Task<string?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan? expiry = null)
    {
        return Task.FromResult<string?>($"/api/attachments/{attachmentId}/download");
    }

    private string GetSafePath(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("Khóa tệp không được để trống.", nameof(objectKey));

        var normalizedKey = objectKey.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, normalizedKey));
        var rootWithSeparator = _storageRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Đường dẫn tệp nằm ngoài thư mục lưu trữ cho phép.");

        return fullPath;
    }
}
