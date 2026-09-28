using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
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
    /// <summary>Lấy danh sách tệp đính kèm mà người yêu cầu được xem</summary>
    Task<List<AttachmentDto>> GetAttachmentsAsync(Guid requesterId);

    /// <summary>Lấy chi tiết thông tin tệp đính kèm theo Id (kiểm tra quyền xem)</summary>
    Task<AttachmentDto?> GetAttachmentByIdAsync(Guid id, Guid requesterId);

    /// <summary>Tải luồng tệp vật lý phục vụ download trực tiếp từ backend (kiểm tra quyền xem)</summary>
    Task<AttachmentDownloadResult> DownloadAttachmentAsync(Guid id, Guid requesterId);

    /// <summary>Tải lên tệp mới, tính mã băm SHA-256 và lưu metadata kèm người tải lên</summary>
    Task<AttachmentDto> UploadAttachmentAsync(Stream stream, string originalFileName, long size, string formCode, string description, string uploadedBy, Guid? uploadedById = null);

    /// <summary>Xóa tệp khỏi storage và cơ sở dữ liệu (kiểm tra quyền xóa)</summary>
    Task DeleteAttachmentAsync(Guid id, Guid requesterId);
}

public class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachmentRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly IAttachmentAccessReader? _accessReader;
    private readonly IUserRepository? _userRepo;
    private readonly IAccessPolicy? _accessPolicy;
    private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png" };
    private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB

    /// <summary>
    /// Khởi tạo chỉ với repository và storage — chỉ dùng cho kiểm tra hợp lệ khi tải lên (unit test).
    /// Các thao tác cần kiểm tra quyền sẽ báo lỗi khi dùng constructor này.
    /// </summary>
    public AttachmentService(IAttachmentRepository attachmentRepo, IFileStorageService fileStorage)
    {
        _attachmentRepo = attachmentRepo;
        _fileStorage = fileStorage;
    }

    /// <summary>Khởi tạo đầy đủ, dùng khi chạy ứng dụng.</summary>
    public AttachmentService(
        IAttachmentRepository attachmentRepo,
        IFileStorageService fileStorage,
        IAttachmentAccessReader accessReader,
        IUserRepository userRepo,
        IAccessPolicy accessPolicy)
        : this(attachmentRepo, fileStorage)
    {
        _accessReader = accessReader;
        _userRepo = userRepo;
        _accessPolicy = accessPolicy;
    }

    /// <summary>
    /// Lấy danh sách tệp đính kèm mà người yêu cầu được xem
    /// </summary>
    public async Task<List<AttachmentDto>> GetAttachmentsAsync(Guid requesterId)
    {
        var all = await _attachmentRepo.GetAllAttachmentsAsync();
        var list = await FilterAccessibleAsync(requesterId, all, AccessOperation.Read);
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
    public async Task<AttachmentDto?> GetAttachmentByIdAsync(Guid id, Guid requesterId)
    {
        var a = await _attachmentRepo.GetByIdAsync(id);
        if (a == null) return null;

        await EnsureAccessAsync(requesterId, a, AccessOperation.Read, "Bạn không có quyền xem tệp đính kèm này.");

        var downloadUrl = await _fileStorage.GetDownloadUrlAsync(a.Id);

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
    public async Task<AttachmentDownloadResult> DownloadAttachmentAsync(Guid id, Guid requesterId)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(id);
        if (attachment == null)
            throw new KeyNotFoundException("Không tìm thấy tệp đính kèm trong hệ thống.");

        await EnsureAccessAsync(requesterId, attachment, AccessOperation.Read, "Bạn không có quyền xem tệp đính kèm này.");

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
        long size,
        string formCode,
        string description,
        string uploadedBy,
        Guid? uploadedById = null)
    {
        if (size > MaxFileSize)
            throw new ArgumentException("Dung lượng tệp vượt quá giới hạn cho phép (25MB).");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"Định dạng tệp '{ext}' không được chấp nhận. Chỉ cho phép PDF, DOCX, XLSX, JPG, PNG.");

        var normalizedFormCode = formCode?.Trim() ?? string.Empty;
        if (!Regex.IsMatch(normalizedFormCode, "^[A-Za-z0-9_-]{1,20}$"))
            throw new ArgumentException("Mã biểu mẫu không hợp lệ.");

        var header = new byte[8];
        var headerLength = await stream.ReadAsync(header.AsMemory(0, header.Length));
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        else
        {
            stream = new PrefixStream(header, headerLength, stream);
        }

        if (!HasExpectedMagicBytes(ext, header, headerLength))
            throw new ArgumentException("Nội dung tệp không khớp với phần mở rộng đã khai báo.");

        var contentType = GetContentType(ext);

        // Băm theo từng đoạn dữ liệu trong lúc storage ghi file, không giữ toàn bộ tệp trong RAM.
        using var hashingStream = new HashingReadStream(stream);

        // Sinh ObjectKey phân cấp: {formCode}/{yyyyMM}/{fileId}_{fileName}.ext
        var fileId = Guid.NewGuid();
        var cleanCode = normalizedFormCode.ToLowerInvariant();
        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var sanitizedBaseName = Path.GetFileNameWithoutExtension(originalFileName).Replace(" ", "_");
        if (sanitizedBaseName.Length > 40) sanitizedBaseName = sanitizedBaseName.Substring(0, 40);

        var objectKey = $"{cleanCode}/{dateFolder}/{fileId}_{sanitizedBaseName}{ext}";

        var savedKey = await _fileStorage.SaveFileAsync(hashingStream, objectKey, contentType);
        var checksum = hashingStream.GetChecksum();

        var attachment = new TaskAttachment
        {
            Id = fileId,
            FileName = originalFileName,
            OriginalFileName = originalFileName,
            ObjectKey = savedKey,
            Checksum = checksum,
            ContentType = contentType,
            FileSize = hashingStream.BytesRead,
            FormCode = normalizedFormCode.ToUpperInvariant(),
            Description = description ?? string.Empty,
            UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? "Cán bộ quản trị" : uploadedBy,
            UploadedById = uploadedById,
            UploadedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
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
    public async Task DeleteAttachmentAsync(Guid id, Guid requesterId)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(id);
        if (attachment == null)
            throw new KeyNotFoundException("Không tìm thấy tệp đính kèm cần xóa.");

        await EnsureAccessAsync(requesterId, attachment, AccessOperation.Delete, "Bạn không có quyền xóa tệp đính kèm này.");

        // Giữ file vật lý để có thể khôi phục bản ghi sau khi xóa mềm.
        await _attachmentRepo.DeleteAsync(attachment);
    }

    /// <summary>Kiểm tra quyền trên một tệp, báo 403 nếu không được phép.</summary>
    private async Task EnsureAccessAsync(Guid requesterId, TaskAttachment attachment, AccessOperation operation, string message)
    {
        var allowed = await FilterAccessibleAsync(requesterId, new List<TaskAttachment> { attachment }, operation);
        if (allowed.Count == 0)
            throw new ForbiddenException(message);
    }

    /// <summary>Lọc các tệp người yêu cầu được thao tác theo <see cref="IAccessPolicy"/>.</summary>
    private async Task<List<TaskAttachment>> FilterAccessibleAsync(Guid requesterId, List<TaskAttachment> attachments, AccessOperation operation)
    {
        if (_accessReader == null || _userRepo == null || _accessPolicy == null)
            throw new InvalidOperationException("AttachmentService chưa được cấu hình kiểm tra quyền.");

        var requester = await _userRepo.GetWithRolesAndPermissionsByIdAsync(requesterId)
            ?? throw new ForbiddenException("Không tìm thấy hồ sơ người dùng hiện tại.");
        if (attachments.Count == 0)
            return attachments;

        var links = await _accessReader.GetRecordLinksAsync(attachments);

        // Chỉ cần biết người tải lên có phải quản trị hay không đối với văn bản chung.
        var generalUploaderIds = attachments
            .Where(a => a.UploadedById.HasValue
                && string.Equals(a.FormCode, AccessPolicy.GeneralFormCode, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.UploadedById!.Value)
            .Distinct()
            .ToList();
        var administratorUploaders = await _accessReader.GetUserIdsInRoleAsync(generalUploaderIds, AppRoles.QUAN_TRI_HE_THONG);

        return attachments
            .Where(a => _accessPolicy.CanAccessAttachment(
                requester,
                a,
                links.TryGetValue(a.Id, out var attachmentLinks) ? attachmentLinks : new List<AttachmentRecordLink>(),
                a.UploadedById.HasValue && administratorUploaders.Contains(a.UploadedById.Value),
                operation))
            .ToList();
    }

    private static string GetContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };

    private static bool HasExpectedMagicBytes(string extension, byte[] header, int length)
    {
        static bool StartsWith(byte[] value, int count, params byte[] signature) =>
            count >= signature.Length && signature.AsSpan().SequenceEqual(value.AsSpan(0, signature.Length));

        return extension switch
        {
            ".pdf" => StartsWith(header, length, Encoding.ASCII.GetBytes("%PDF")),
            ".png" => StartsWith(header, length, 0x89, 0x50, 0x4E, 0x47),
            ".jpg" or ".jpeg" => StartsWith(header, length, 0xFF, 0xD8, 0xFF),
            ".docx" or ".xlsx" => StartsWith(header, length, 0x50, 0x4B, 0x03, 0x04),
            _ => false
        };
    }
}

internal sealed class HashingReadStream : Stream
{
    private readonly Stream _inner;
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    public HashingReadStream(Stream inner) => _inner = inner;

    public long BytesRead { get; private set; }

    public string GetChecksum() => Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        Append(buffer.AsSpan(offset, read));
        return read;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var read = await _inner.ReadAsync(buffer, cancellationToken);
        Append(buffer.Span[..read]);
        return read;
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return;
        _hash.AppendData(data);
        BytesRead += data.Length;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _hash.Dispose();
        base.Dispose(disposing);
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => BytesRead;
    public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

internal sealed class PrefixStream : Stream
{
    private readonly byte[] _prefix;
    private readonly int _prefixLength;
    private readonly Stream _inner;
    private int _prefixPosition;

    public PrefixStream(byte[] prefix, int prefixLength, Stream inner)
    {
        _prefix = prefix;
        _prefixLength = prefixLength;
        _inner = inner;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var copied = CopyPrefix(buffer.AsSpan(offset, count));
        return copied == count ? copied : copied + _inner.Read(buffer, offset + copied, count - copied);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var copied = CopyPrefix(buffer.Span);
        return copied == buffer.Length ? copied : copied + await _inner.ReadAsync(buffer[copied..], cancellationToken);
    }

    private int CopyPrefix(Span<byte> destination)
    {
        var count = Math.Min(destination.Length, _prefixLength - _prefixPosition);
        if (count > 0)
        {
            _prefix.AsSpan(_prefixPosition, count).CopyTo(destination);
            _prefixPosition += count;
        }
        return count;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
