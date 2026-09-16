using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Services;

public class AttachmentDownloadResult
{
    public Stream Stream { get; set; } = Stream.Null;
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = string.Empty;
}

public interface IAttachmentService
{
    Task<List<AttachmentDto>> GetAttachmentsAsync();
    Task<AttachmentDto?> GetAttachmentByIdAsync(Guid id);
    Task<AttachmentDownloadResult> DownloadAttachmentAsync(Guid id);
    Task<AttachmentDto> UploadAttachmentAsync(Stream stream, string originalFileName, string contentType, long size, string category, string description, string uploadedBy);
    Task<AttachmentDto> UpdateAttachmentAsync(Guid id, UpdateAttachmentDto input);
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

    public async Task<List<AttachmentDto>> GetAttachmentsAsync()
    {
        var list = await _attachmentRepo.GetAllAttachmentsAsync();
        return list.Select(a => new AttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            ContentType = a.ContentType,
            FileSize = a.FileSize,
            Category = a.FormCode,
            Description = a.Description,
            Checksum = a.Checksum,
            Provider = a.Provider,
            UploadedAt = a.UploadedAt,
            UploadedBy = string.IsNullOrWhiteSpace(a.UploadedBy) ? "Cán bộ quản trị" : a.UploadedBy
        }).ToList();
    }

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
            Category = a.FormCode,
            Description = a.Description,
            Checksum = a.Checksum,
            Provider = a.Provider,
            DownloadUrl = downloadUrl,
            UploadedAt = a.UploadedAt,
            UploadedBy = string.IsNullOrWhiteSpace(a.UploadedBy) ? "Cán bộ quản trị" : a.UploadedBy
        };
    }

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

    public async Task<AttachmentDto> UploadAttachmentAsync(
        Stream stream,
        string originalFileName,
        string contentType,
        long size,
        string category,
        string description,
        string uploadedBy)
    {
        if (size > MaxFileSize)
            throw new ArgumentException("Dung lượng tệp vượt quá giới hạn cho phép (25MB).");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"Định dạng tệp '{ext}' không được chấp nhận. Chỉ cho phép PDF, DOCX, XLSX, JPG, PNG.");

        // 1. Đọc stream và tính mã băm SHA-256 Checksum phục vụ kiểm tra toàn vẹn & chống trùng lặp
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        var hashBytes = System.Security.Cryptography.SHA256.HashData(memoryStream.ToArray());
        var checksum = Convert.ToHexString(hashBytes).ToLowerInvariant();
        memoryStream.Position = 0;

        // 2. Tạo định danh object_key phân tầng theo danh mục và thời gian: {category}/{yyyyMM}/{file_id}.ext
        var fileId = Guid.NewGuid();
        var cleanCategory = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim().ToLowerInvariant();
        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var sanitizedBaseName = Path.GetFileNameWithoutExtension(originalFileName).Replace(" ", "_");
        if (sanitizedBaseName.Length > 40) sanitizedBaseName = sanitizedBaseName.Substring(0, 40);

        var objectKey = $"{cleanCategory}/{dateFolder}/{fileId}_{sanitizedBaseName}{ext}";

        // 3. Tải luồng dữ liệu lên Storage Adapter (Local hoặc MinIO)
        var savedKey = await _fileStorage.SaveFileAsync(memoryStream, objectKey, contentType);

        // 4. Khởi tạo Metadata
        var attachment = new TaskAttachment
        {
            Id = fileId,
            FileName = originalFileName,
            OriginalFileName = originalFileName,
            ObjectKey = savedKey,
            Provider = _fileStorage.ProviderName,
            Checksum = checksum,
            ContentType = contentType,
            FileSize = memoryStream.Length,
            FormCode = string.IsNullOrWhiteSpace(category) ? "GENERAL" : category.Trim().ToUpperInvariant(),
            Description = description ?? string.Empty,
            UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "Cán bộ quản trị" : uploadedBy,
            UploadedAt = DateTime.UtcNow,
            IsActive = true
        };

        // 5. Lưu Metadata vào Database với cơ chế chống Orphan Object (Rollback storage nếu DB lỗi)
        try
        {
            await _attachmentRepo.AddAsync(attachment);
        }
        catch (Exception)
        {
            // Tự động dọn tệp mồ côi trên Storage nếu không thể lưu Metadata
            await _fileStorage.DeleteFileAsync(savedKey);
            throw;
        }

        return new AttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            Category = attachment.FormCode,
            Description = attachment.Description,
            Checksum = attachment.Checksum,
            Provider = attachment.Provider,
            UploadedAt = attachment.UploadedAt,
            UploadedBy = attachment.UploadedBy
        };
    }

    public async Task<AttachmentDto> UpdateAttachmentAsync(Guid id, UpdateAttachmentDto input)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(id);
        if (attachment == null)
            throw new KeyNotFoundException("Không tìm thấy tệp đính kèm cần chỉnh sửa.");

        if (!string.IsNullOrWhiteSpace(input.Category))
        {
            attachment.FormCode = input.Category.Trim().ToUpperInvariant();
        }
        if (input.Description != null)
        {
            attachment.Description = input.Description.Trim();
        }

        await _attachmentRepo.UpdateAsync(attachment);

        return new AttachmentDto
        {
            Id = attachment.Id,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            Category = attachment.FormCode,
            Description = attachment.Description,
            Checksum = attachment.Checksum,
            Provider = attachment.Provider,
            UploadedAt = attachment.UploadedAt,
            UploadedBy = attachment.UploadedBy
        };
    }

    public async Task DeleteAttachmentAsync(Guid id)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(id);
        if (attachment == null)
            throw new KeyNotFoundException("Không tìm thấy tệp đính kèm cần xóa.");

        // Xóa tệp vật lý trên Storage Adapter
        await _fileStorage.DeleteFileAsync(attachment.ObjectKey);

        // Xóa Metadata trong Database
        await _attachmentRepo.DeleteAsync(attachment);
    }
}
