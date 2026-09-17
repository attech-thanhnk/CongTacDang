using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

/// <summary>
/// Kết quả luồng dữ liệu tệp tin phục vụ tải về
/// </summary>
public class AttachmentDownloadResult
{
    /// <summary>Luồng dữ liệu đọc tệp</summary>
    public Stream Stream { get; set; } = Stream.Null;

    /// <summary>Định dạng MIME của tệp</summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Tên tệp tin đính kèm</summary>
    public string FileName { get; set; } = string.Empty;
}

/// <summary>
/// Giao diện xử lý nghiệp vụ tệp đính kèm văn bản và minh chứng
/// </summary>
public interface IAttachmentService
{
    /// <summary>Lấy danh sách tất cả tệp đính kèm</summary>
    Task<List<AttachmentDto>> GetAttachmentsAsync();

    /// <summary>Lấy chi tiết thông tin tệp đính kèm theo Id</summary>
    Task<AttachmentDto?> GetAttachmentByIdAsync(Guid id);

    /// <summary>Tải luồng tệp vật lý phục vụ download trực tiếp từ backend</summary>
    Task<AttachmentDownloadResult> DownloadAttachmentAsync(Guid id);

    /// <summary>Tải lên tệp mới, tính mã băm SHA-256 và lưu metadata</summary>
    Task<AttachmentDto> UploadAttachmentAsync(Stream stream, string originalFileName, string contentType, long size, string formCode, string description, string uploadedBy);

    /// <summary>Xóa tệp khỏi storage và cơ sở dữ liệu</summary>
    Task DeleteAttachmentAsync(Guid id);
}

public class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachmentRepo;
    private readonly IFileStorageService _fileStorage;
    private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png" };
    private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB

    public AttachmentService(IAttachmentRepository attachmentRepo, IFileStorageService fileStorage)
    {
        _attachmentRepo = attachmentRepo;
        _fileStorage = fileStorage;
    }

    /// <summary>
    /// Lấy toàn bộ danh sách tệp đính kèm trong hệ thống
    /// </summary>
    public async Task<List<AttachmentDto>> GetAttachmentsAsync()
    {
        var list = await _attachmentRepo.GetAllAttachmentsAsync();
        return list.Select(a => new AttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            FileSize = a.FileSize,
            FormCode = a.FormCode,
            Category = a.FormCode,
            Description = a.Description,
            Checksum = a.Checksum,
            UploadedAt = a.UploadedAt,
            UploadedBy = string.IsNullOrWhiteSpace(a.UploadedBy) ? "Cán bộ quản trị" : a.UploadedBy
        }).ToList();
    }

    /// <summary>
    /// Lấy chi tiết thông tin tệp đính kèm kèm link tải (nếu có)
    /// </summary>
    public async Task<AttachmentDto?> GetAttachmentByIdAsync(Guid id)
    {
        var a = await _attachmentRepo.GetByIdAsync(id);
        if (a == null) return null;

        var downloadUrl = await _fileStorage.GetDownloadUrlAsync(a.ObjectKey, a.FileName);

        return new AttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            FileSize = a.FileSize,
            FormCode = a.FormCode,
            Category = a.FormCode,
            Description = a.Description,
            Checksum = a.Checksum,
            DownloadUrl = downloadUrl,
            UploadedAt = a.UploadedAt,
            UploadedBy = string.IsNullOrWhiteSpace(a.UploadedBy) ? "Cán bộ quản trị" : a.UploadedBy
        };
    }

    /// <summary>
    /// Đọc luồng dữ liệu tệp phục vụ tải về trực tiếp
    /// </summary>
    public async Task<AttachmentDownloadResult> DownloadAttachmentAsync(Guid id)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(id);
        if (attachment == null)
            throw new KeyNotFoundException("Không tìm thấy tệp đính kèm trong hệ thống.");

        var stream = await _fileStorage.GetFileStreamAsync(attachment.ObjectKey);
        if (stream == null)
            throw new FileNotFoundException("Tệp tin vật lý không tồn tại trên máy chủ lưu trữ.");

        return new AttachmentDownloadResult
        {
            Stream = stream,
            ContentType = attachment.ContentType,
            FileName = attachment.FileName
        };
    }

    /// <summary>
    /// Tải tệp mới lên MinIO, kiểm tra định dạng/dung lượng, tính SHA-256 và lưu metadata
    /// </summary>
    public async Task<AttachmentDto> UploadAttachmentAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        long size,
        string formCode,
        string description,
        string uploadedBy)
    {
        if (size > MaxFileSize)
            throw new ArgumentException("Dung lượng tệp vượt quá giới hạn cho phép (25MB).");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"Định dạng tệp '{ext}' không được chấp nhận. Chỉ cho phép PDF, DOCX, XLSX, JPG, PNG.");

        // Tính SHA-256 checksum kiểm tra toàn vẹn
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var hashBytes = SHA256.HashData(memoryStream.ToArray());
        var checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();
        memoryStream.Position = 0;

        // Sinh ObjectKey phân cấp: {formCode}/{yyyyMM}/{fileId}_{fileName}.ext
        var fileId = Guid.NewGuid();
        var cleanCode = string.IsNullOrWhiteSpace(formCode) ? "general" : formCode.Trim().ToLowerInvariant();
        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var sanitizedBaseName = Path.GetFileNameWithoutExtension(originalFileName).Replace(" ", "_");
        if (sanitizedBaseName.Length > 40) sanitizedBaseName = sanitizedBaseName.Substring(0, 40);

        var objectKey = $"{cleanCode}/{dateFolder}/{fileId}_{sanitizedBaseName}{ext}";

        var savedKey = await _fileStorage.SaveFileAsync(memoryStream, objectKey, contentType);

        var attachment = new TaskAttachment
        {
            Id = fileId,
            FileName = originalFileName,
            OriginalFileName = originalFileName,
            ObjectKey = savedKey,
            Checksum = checksum,
            ContentType = contentType,
            FileSize = memoryStream.Length,
            FormCode = string.IsNullOrWhiteSpace(formCode) ? "GENERAL" : formCode.Trim().ToUpperInvariant(),
            Description = description ?? string.Empty,
            UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "Cán bộ quản trị" : uploadedBy,
            UploadedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Rollback tệp trên storage nếu lưu database thất bại
        try
        {
            await _attachmentRepo.AddAsync(attachment);
        }
        catch (Exception)
        {
            await _fileStorage.DeleteFileAsync(savedKey);
            throw;
        }

        return new AttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            FormCode = attachment.FormCode,
            Category = attachment.FormCode,
            Description = attachment.Description,
            Checksum = attachment.Checksum,
            UploadedAt = attachment.UploadedAt,
            UploadedBy = attachment.UploadedBy
        };
    }

    /// <summary>
    /// Xóa tệp khỏi storage và bản ghi metadata trong cơ sở dữ liệu
    /// </summary>
    public async Task DeleteAttachmentAsync(Guid id)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(id);
        if (attachment == null)
            throw new KeyNotFoundException("Không tìm thấy tệp đính kèm cần xóa.");

        await _fileStorage.DeleteFileAsync(attachment.ObjectKey);
        await _attachmentRepo.DeleteAsync(attachment);
    }
}
