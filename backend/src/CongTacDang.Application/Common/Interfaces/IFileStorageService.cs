using System;
using System.IO;
using System.Threading.Tasks;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ lưu trữ tệp tin hệ thống
/// </summary>
public interface IFileStorageService
{
    /// <summary>Lưu luồng tệp lên kho lưu trữ và trả về ObjectKey</summary>
    Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream");

    /// <summary>Đọc luồng dữ liệu của tệp theo ObjectKey</summary>
    Task<Stream?> GetFileStreamAsync(string objectKey);

    /// <summary>Xóa tệp khỏi kho lưu trữ theo ObjectKey</summary>
    Task DeleteFileAsync(string objectKey);

    /// <summary>Kiểm tra tệp có tồn tại trên kho lưu trữ hay không</summary>
    bool FileExists(string objectKey);

    /// <summary>Sinh liên kết tải xuống trực tiếp cho người dùng</summary>
    Task<string?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan? expiry = null);
}
