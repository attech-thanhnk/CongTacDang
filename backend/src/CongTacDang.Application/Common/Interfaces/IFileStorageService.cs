using System;
using System.IO;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

public interface IFileStorageService
{
    string ProviderName { get; }

    Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream");
    Task<Stream?> GetFileStreamAsync(string objectKey);
    Task DeleteFileAsync(string objectKey);
    bool FileExists(string objectKey);

    Task<string?> GetDownloadUrlAsync(string objectKey, string fileName, TimeSpan? expiry = null);
}
