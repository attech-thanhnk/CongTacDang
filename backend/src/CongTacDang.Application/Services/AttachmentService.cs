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
/// Giao diện xử lý nghiệp vụ tệp đính kèm văn bản và minh chứng.
/// Một tệp gắn với một đối tượng (<see cref="AttachmentOwnerTypes"/>), có nhiều phiên bản; mọi thao tác kiểm tra quyền qua <see cref="IAccessPolicy"/>.
/// </summary>
public interface IAttachmentService
{
    /// <summary>Lấy danh sách tệp (phiên bản hiện hành) mà người yêu cầu được xem</summary>
    Task<List<AttachmentDto>> GetAttachmentsAsync(Guid requesterId);

    /// <summary>Lấy danh sách tệp (phiên bản hiện hành) của một đối tượng mà người yêu cầu được xem</summary>
    Task<List<AttachmentDto>> GetAttachmentsByOwnerAsync(string ownerType, Guid ownerId, Guid requesterId);

    /// <summary>Lấy chi tiết thông tin một phiên bản tệp theo Id (kiểm tra quyền xem)</summary>
    Task<AttachmentDto?> GetAttachmentByIdAsync(Guid id, Guid requesterId);

    /// <summary>Lấy lịch sử phiên bản của tệp chứa phiên bản <paramref name="id"/> (kiểm tra quyền xem)</summary>
    Task<List<AttachmentDto>> GetVersionsAsync(Guid id, Guid requesterId);

    /// <summary>Tải phiên bản hiện hành của tệp chứa phiên bản <paramref name="id"/> (kiểm tra quyền xem)</summary>
    Task<AttachmentDownloadResult> DownloadAttachmentAsync(Guid id, Guid requesterId);

    /// <summary>Tải một phiên bản cụ thể của tệp chứa phiên bản <paramref name="id"/> (kiểm tra quyền xem)</summary>
    Task<AttachmentDownloadResult> DownloadVersionAsync(Guid id, int versionNumber, Guid requesterId);

    /// <summary>
    /// Tải lên tệp mới, tính mã băm SHA-256 và lưu metadata kèm người tải lên.
    /// Truyền <paramref name="ownerType"/>/<paramref name="ownerId"/> để gắn tệp vào một đối tượng (cần quyền cập nhật đối tượng).
    /// </summary>
    Task<AttachmentDto> UploadAttachmentAsync(
        Stream stream,
        string originalFileName,
        long size,
        string formCode,
        string description,
        string uploadedBy,
        Guid? uploadedById = null,
        string? ownerType = null,
        Guid? ownerId = null);

    /// <summary>Thay tệp bằng phiên bản mới; phiên bản cũ được giữ lại, đánh dấu không hiện hành (kiểm tra quyền cập nhật)</summary>
    Task<AttachmentDto> ReplaceAttachmentAsync(Guid id, Stream stream, string originalFileName, long size, string uploadedBy, Guid requesterId);

    /// <summary>Xóa mềm tệp (mọi phiên bản), giữ file vật lý để khôi phục (kiểm tra quyền xóa)</summary>
    Task DeleteAttachmentAsync(Guid id, Guid requesterId);

    /// <summary>
    /// Bảo đảm các tệp client gửi kèm (ví dụ <c>EvaluationTask.AttachmentId</c>) tồn tại và người gửi có quyền cập nhật trên tệp
    /// (người tải lên, chủ hồ sơ đang gắn tệp hoặc quản trị). Báo 400 nếu tệp không tồn tại, 403 nếu không có quyền.
    /// </summary>
    Task EnsureCanLinkAttachmentsAsync(IReadOnlyCollection<Guid> attachmentIds, Guid requesterId);
}

public class AttachmentService : IAttachmentService
{
    private readonly IAttachmentRepository _attachmentRepo;
    private readonly IFileStorageService _fileStorage;
    private readonly IAttachmentAccessReader? _accessReader;
    private readonly IUserRepository? _userRepo;
    private readonly IAccessPolicy? _accessPolicy;
    private readonly IAttachmentVersionRepository? _versionRepo;
    private static readonly string[] AllowedExtensions = { ".pdf", ".docx", ".xlsx", ".jpg", ".jpeg", ".png" };
    private const long MaxFileSize = 25 * 1024 * 1024; // 25 MB
    private const string DefaultUploaderName = "Cán bộ quản trị";

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
        IAccessPolicy accessPolicy,
        IAttachmentVersionRepository versionRepo)
        : this(attachmentRepo, fileStorage)
    {
        _accessReader = accessReader;
        _userRepo = userRepo;
        _accessPolicy = accessPolicy;
        _versionRepo = versionRepo;
    }

    /// <summary>
    /// Lấy danh sách tệp đính kèm (phiên bản hiện hành) mà người yêu cầu được xem
    /// </summary>
    public async Task<List<AttachmentDto>> GetAttachmentsAsync(Guid requesterId)
    {
        var all = await _attachmentRepo.GetAllAttachmentsAsync();
        var list = await FilterAccessibleAsync(requesterId, all, AccessOperation.Read);
        return list.Select(a => ToDto(a)).ToList();
    }

    /// <summary>Lấy danh sách tệp hiện hành của một đối tượng mà người yêu cầu được xem</summary>
    public async Task<List<AttachmentDto>> GetAttachmentsByOwnerAsync(string ownerType, Guid ownerId, Guid requesterId)
    {
        var normalizedType = AttachmentOwnerTypes.Normalize(ownerType)
            ?? throw new ValidationException("Loại đối tượng sở hữu tệp không hợp lệ.");

        var files = await VersionRepo.GetCurrentByOwnerAsync(normalizedType, ownerId);
        var list = await FilterAccessibleAsync(requesterId, files, AccessOperation.Read);
        return list.Select(a => ToDto(a)).ToList();
    }

    /// <summary>
    /// Lấy chi tiết thông tin một phiên bản tệp kèm link tải phiên bản hiện hành
    /// </summary>
    public async Task<AttachmentDto?> GetAttachmentByIdAsync(Guid id, Guid requesterId)
    {
        var a = await _attachmentRepo.GetByIdAsync(id);
        if (a == null) return null;

        await EnsureAccessAsync(requesterId, await GetCurrentOrSelfAsync(a), AccessOperation.Read, "Bạn không có quyền xem tệp đính kèm này.");

        return ToDto(a, await _fileStorage.GetDownloadUrlAsync(a.Id));
    }

    /// <summary>Lấy lịch sử phiên bản của tệp</summary>
    public async Task<List<AttachmentDto>> GetVersionsAsync(Guid id, Guid requesterId)
    {
        var current = await GetCurrentRequiredAsync(id);
        await EnsureAccessAsync(requesterId, current, AccessOperation.Read, "Bạn không có quyền xem tệp đính kèm này.");

        var versions = await VersionRepo.GetVersionsAsync(current.GroupId);
        return versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => ToDto(v))
            .ToList();
    }

    /// <summary>
    /// Đọc luồng dữ liệu phiên bản hiện hành của tệp phục vụ tải về trực tiếp
    /// </summary>
    public async Task<AttachmentDownloadResult> DownloadAttachmentAsync(Guid id, Guid requesterId)
    {
        var current = await GetCurrentRequiredAsync(id);
        await EnsureAccessAsync(requesterId, current, AccessOperation.Read, "Bạn không có quyền xem tệp đính kèm này.");
        return await OpenAsync(current);
    }

    /// <summary>Đọc luồng dữ liệu của một phiên bản cụ thể</summary>
    public async Task<AttachmentDownloadResult> DownloadVersionAsync(Guid id, int versionNumber, Guid requesterId)
    {
        var current = await GetCurrentRequiredAsync(id);
        // Quyền trên mọi phiên bản bằng quyền trên phiên bản hiện hành của tệp.
        await EnsureAccessAsync(requesterId, current, AccessOperation.Read, "Bạn không có quyền xem tệp đính kèm này.");

        var versions = await VersionRepo.GetVersionsAsync(current.GroupId);
        var version = versions.FirstOrDefault(v => v.VersionNumber == versionNumber)
            ?? throw new KeyNotFoundException($"Không tìm thấy phiên bản {versionNumber} của tệp đính kèm.");
        return await OpenAsync(version);
    }

    /// <summary>
    /// Tải tệp mới lên kho lưu trữ, kiểm tra định dạng/dung lượng, tính SHA-256 và lưu metadata
    /// </summary>
    public async Task<AttachmentDto> UploadAttachmentAsync(
        Stream stream,
        string originalFileName,
        long size,
        string formCode,
        string description,
        string uploadedBy,
        Guid? uploadedById = null,
        string? ownerType = null,
        Guid? ownerId = null)
    {
        var normalizedFormCode = NormalizeFormCode(formCode);
        var owner = await ResolveOwnerAsync(ownerType, ownerId, uploadedById);

        var stored = await ValidateAndStoreAsync(stream, originalFileName, size, normalizedFormCode);

        var attachment = new TaskAttachment
        {
            Id = stored.FileId,
            FileName = originalFileName,
            OriginalFileName = originalFileName,
            ObjectKey = stored.ObjectKey,
            Checksum = stored.Checksum,
            ContentType = stored.ContentType,
            FileSize = stored.Size,
            FormCode = normalizedFormCode.ToUpperInvariant(),
            Description = description ?? string.Empty,
            UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? DefaultUploaderName : uploadedBy,
            UploadedById = uploadedById,
            UploadedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            OwnerType = owner.Type,
            OwnerId = owner.Id,
            // Giữ các cột cũ để các truy vấn/kiểm tra quyền hiện có vẫn nhận ra liên kết hồ sơ.
            RecordId = owner.RecordId,
            RelatedId = owner.Type == AttachmentOwnerTypes.EvaluationTask ? owner.Id : null,
            FileGroupId = stored.FileId,
            VersionNumber = 1
        };

        // Rollback tệp trên storage nếu lưu database thất bại
        try
        {
            await _attachmentRepo.AddAsync(attachment);
        }
        catch (Exception)
        {
            await _fileStorage.DeleteFileAsync(stored.ObjectKey);
            throw;
        }

        return ToDto(attachment);
    }

    /// <summary>Thay tệp bằng phiên bản mới, giữ phiên bản cũ</summary>
    public async Task<AttachmentDto> ReplaceAttachmentAsync(Guid id, Stream stream, string originalFileName, long size, string uploadedBy, Guid requesterId)
    {
        var current = await GetCurrentRequiredAsync(id);
        await EnsureAccessAsync(requesterId, current, AccessOperation.Update, "Bạn không có quyền thay tệp đính kèm này.");

        var versions = await VersionRepo.GetVersionsAsync(current.GroupId);
        var nextNumber = versions.Count == 0 ? current.VersionNumber + 1 : versions.Max(v => v.VersionNumber) + 1;

        var stored = await ValidateAndStoreAsync(stream, originalFileName, size, current.FormCode);
        var now = DateTime.UtcNow;

        var next = new TaskAttachment
        {
            Id = stored.FileId,
            FileName = originalFileName,
            OriginalFileName = originalFileName,
            ObjectKey = stored.ObjectKey,
            Checksum = stored.Checksum,
            ContentType = stored.ContentType,
            FileSize = stored.Size,
            FormCode = current.FormCode,
            Description = current.Description,
            UploadedBy = string.IsNullOrWhiteSpace(uploadedBy) ? DefaultUploaderName : uploadedBy,
            UploadedById = requesterId,
            UploadedAt = now,
            CreatedAt = now,
            IsActive = true,
            OwnerType = current.OwnerType,
            OwnerId = current.OwnerId,
            RecordId = current.RecordId,
            RelatedId = current.RelatedId,
            FileGroupId = current.GroupId,
            VersionNumber = nextNumber
        };

        current.FileGroupId = current.GroupId;
        current.IsSuperseded = true;
        current.SupersededAt = now;
        current.SupersededById = requesterId;

        try
        {
            await VersionRepo.AddVersionAsync(current, next);
        }
        catch (Exception)
        {
            await _fileStorage.DeleteFileAsync(stored.ObjectKey);
            throw;
        }

        return ToDto(next);
    }

    /// <summary>
    /// Xóa mềm tệp (mọi phiên bản). Giữ file vật lý để có thể khôi phục.
    /// </summary>
    public async Task DeleteAttachmentAsync(Guid id, Guid requesterId)
    {
        var attachment = await _attachmentRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tệp đính kèm cần xóa.");
        var current = await GetCurrentOrSelfAsync(attachment);

        await EnsureAccessAsync(requesterId, current, AccessOperation.Delete, "Bạn không có quyền xóa tệp đính kèm này.");

        if (_versionRepo != null)
            await _versionRepo.SoftDeleteGroupAsync(current.GroupId);
        else
            await _attachmentRepo.DeleteAsync(attachment);
    }

    /// <summary>Kiểm tra tệp gắn vào dữ liệu nghiệp vụ (T-47)</summary>
    public async Task EnsureCanLinkAttachmentsAsync(IReadOnlyCollection<Guid> attachmentIds, Guid requesterId)
    {
        var ids = attachmentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return;

        var attachments = new List<TaskAttachment>();
        foreach (var id in ids)
        {
            var attachment = await _attachmentRepo.GetByIdAsync(id)
                ?? throw new ValidationException("Tệp minh chứng đính kèm không tồn tại hoặc đã bị xóa.");
            attachments.Add(await GetCurrentOrSelfAsync(attachment));
        }

        var allowed = await FilterAccessibleAsync(requesterId, attachments, AccessOperation.Update);
        if (allowed.Count != attachments.Count)
            throw new ForbiddenException("Bạn không có quyền gắn tệp minh chứng này vào hồ sơ.");
    }

    #region Hỗ trợ

    private IAttachmentVersionRepository VersionRepo => _versionRepo
        ?? throw new InvalidOperationException("AttachmentService chưa được cấu hình kho phiên bản tệp.");

    /// <summary>Phiên bản hiện hành của nhóm chứa <paramref name="id"/>; báo 404 nếu không có.</summary>
    private async Task<TaskAttachment> GetCurrentRequiredAsync(Guid id)
    {
        if (_versionRepo == null)
        {
            return await _attachmentRepo.GetByIdAsync(id)
                ?? throw new KeyNotFoundException("Không tìm thấy tệp đính kèm trong hệ thống.");
        }

        return await _versionRepo.GetCurrentVersionAsync(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tệp đính kèm trong hệ thống.");
    }

    private async Task<TaskAttachment> GetCurrentOrSelfAsync(TaskAttachment attachment)
    {
        if (attachment.IsCurrent || _versionRepo == null)
            return attachment;
        return await _versionRepo.GetCurrentVersionAsync(attachment.Id) ?? attachment;
    }

    private async Task<AttachmentDownloadResult> OpenAsync(TaskAttachment attachment)
    {
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

    private sealed record OwnerInfo(string Type, Guid? Id, Guid? RecordId);

    /// <summary>Kiểm tra đối tượng sở hữu khi tải lên: người tải lên phải có quyền cập nhật hồ sơ liên quan.</summary>
    private async Task<OwnerInfo> ResolveOwnerAsync(string? ownerType, Guid? ownerId, Guid? uploadedById)
    {
        if (string.IsNullOrWhiteSpace(ownerType))
        {
            if (ownerId.HasValue)
                throw new ArgumentException("Thiếu loại đối tượng sở hữu tệp.");
            return new OwnerInfo(AttachmentOwnerTypes.General, null, null);
        }

        var normalizedType = AttachmentOwnerTypes.Normalize(ownerType)
            ?? throw new ArgumentException("Loại đối tượng sở hữu tệp không hợp lệ.");

        if (normalizedType == AttachmentOwnerTypes.General)
        {
            if (ownerId.HasValue)
                throw new ArgumentException("Văn bản chung không gắn mã đối tượng.");
            return new OwnerInfo(AttachmentOwnerTypes.General, null, null);
        }

        if (!ownerId.HasValue || ownerId.Value == Guid.Empty)
            throw new ArgumentException("Thiếu mã đối tượng sở hữu tệp.");

        if (_accessReader == null || _userRepo == null || _accessPolicy == null)
            throw new InvalidOperationException("AttachmentService chưa được cấu hình kiểm tra quyền.");
        if (!uploadedById.HasValue)
            throw new ForbiddenException("Không xác định được người tải lên.");

        var record = await _accessReader.GetOwnerRecordAsync(normalizedType, ownerId.Value)
            ?? throw new KeyNotFoundException("Không tìm thấy đối tượng cần gắn tệp.");
        var requester = await _userRepo.GetWithRolesAndPermissionsByIdAsync(uploadedById.Value)
            ?? throw new ForbiddenException("Không tìm thấy hồ sơ người dùng hiện tại.");

        if (!_accessPolicy.CanAccessRecord(requester, record, AccessOperation.Update))
            throw new ForbiddenException("Bạn không có quyền gắn tệp vào hồ sơ đánh giá này.");

        return new OwnerInfo(normalizedType, ownerId.Value, record.Id);
    }

    private static string NormalizeFormCode(string? formCode)
    {
        var normalized = formCode?.Trim() ?? string.Empty;
        if (!Regex.IsMatch(normalized, "^[A-Za-z0-9_-]{1,20}$"))
            throw new ArgumentException("Mã biểu mẫu không hợp lệ.");
        return normalized;
    }

    private sealed record StoredFile(Guid FileId, string ObjectKey, string Checksum, string ContentType, long Size);

    /// <summary>Kiểm tra dung lượng, phần mở rộng, magic bytes rồi ghi tệp vào storage (băm SHA-256 khi ghi).</summary>
    private async Task<StoredFile> ValidateAndStoreAsync(Stream stream, string originalFileName, long size, string formCode)
    {
        if (size > MaxFileSize)
            throw new ArgumentException("Dung lượng tệp vượt quá giới hạn cho phép (25MB).");

        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new ArgumentException($"Định dạng tệp '{ext}' không được chấp nhận. Chỉ cho phép PDF, DOCX, XLSX, JPG, PNG.");

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
        var cleanCode = formCode.ToLowerInvariant();
        var dateFolder = DateTime.UtcNow.ToString("yyyyMM");
        var sanitizedBaseName = Path.GetFileNameWithoutExtension(originalFileName).Replace(" ", "_");
        if (sanitizedBaseName.Length > 40) sanitizedBaseName = sanitizedBaseName.Substring(0, 40);

        var objectKey = $"{cleanCode}/{dateFolder}/{fileId}_{sanitizedBaseName}{ext}";

        var savedKey = await _fileStorage.SaveFileAsync(hashingStream, objectKey, contentType);
        return new StoredFile(fileId, savedKey, hashingStream.GetChecksum(), contentType, hashingStream.BytesRead);
    }

    private static AttachmentDto ToDto(TaskAttachment a, string? downloadUrl = null) => new()
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
        UploadedBy = string.IsNullOrWhiteSpace(a.UploadedBy) ? DefaultUploaderName : a.UploadedBy,
        UploadedById = a.UploadedById,
        OwnerType = a.EffectiveOwnerType,
        OwnerId = a.EffectiveOwnerId,
        FileGroupId = a.GroupId,
        VersionNumber = a.VersionNumber,
        IsCurrent = a.IsCurrent,
        SupersededAt = a.SupersededAt,
        SupersededById = a.SupersededById
    };

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

    #endregion
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
