using System;
using System.IO;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Dịch vụ lưu trữ tệp tin vật lý trên ổ đĩa máy chủ cục bộ
/// Lưu tệp trên đĩa cục bộ của máy chủ backend (thư mục cấu hình Storage:Local:Path)
/// </summary>
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

    /// <summary>
    /// Lưu luồng dữ liệu tệp vào thư mục cục bộ theo cấu trúc phân cấp ObjectKey
    /// </summary>
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

    /// <summary>
    /// Đọc luồng dữ liệu của tệp tin từ ổ đĩa
    /// </summary>
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

    /// <summary>
    /// Xóa tệp khỏi kho lưu trữ cục bộ
    /// </summary>
    public Task DeleteFileAsync(string objectKey)
    {
        var fullPath = GetSafePath(objectKey);

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
        var fullPath = GetSafePath(objectKey);
        return File.Exists(fullPath);
    }

    /// <summary>
    /// Sinh liên kết tương đối tải tệp tin qua API
    /// </summary>
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
