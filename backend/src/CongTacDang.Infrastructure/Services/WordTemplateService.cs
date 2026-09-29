using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Quản lý file mẫu Word (task 17 — T-81): danh mục biểu mẫu, tải lên phiên bản mới (kiểm tra định dạng, magic bytes,
/// dung lượng, tag, sinh thử), kích hoạt/quay về file gốc, tải xuống. Nội dung lưu qua <see cref="IFileStorageService"/>,
/// metadata trong bảng <c>word_template_versions</c>. Quyền <c>system.templates.manage</c> kiểm tra ở controller.
/// </summary>
public sealed class WordTemplateService : IWordTemplateService
{
    /// <summary>Dung lượng tối đa của file mẫu.</summary>
    public const long MaxFileSize = 10 * 1024 * 1024;

    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly CongTacDangDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly IBundledWordTemplates _bundled;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    /// <summary>Khởi tạo.</summary>
    public WordTemplateService(
        CongTacDangDbContext db,
        IFileStorageService storage,
        IBundledWordTemplates bundled,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _db = db;
        _storage = storage;
        _bundled = bundled;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private DbSet<WordTemplateVersion> Versions => _db.Set<WordTemplateVersion>();

    /// <inheritdoc />
    public async Task<List<WordTemplateSummaryDto>> GetTemplatesAsync(CancellationToken ct = default)
    {
        var versions = await Versions.AsNoTracking().ToListAsync(ct);
        return WordFormCatalog.All.Select(form =>
        {
            var own = versions.Where(v => v.TemplateCode == form.Code).ToList();
            var active = own.FirstOrDefault(v => v.IsActive);
            return new WordTemplateSummaryDto
            {
                Code = form.Code,
                Name = form.Name,
                TemplateFileName = form.TemplateFileName,
                ActiveVersion = active == null ? null : ToDto(active),
                VersionCount = own.Count,
                Tags = TagCatalog(form)
            };
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<WordTemplateVersionDto>> GetVersionsAsync(string code, CancellationToken ct = default)
    {
        var form = RequireForm(code);
        var versions = await Versions.AsNoTracking()
            .Where(v => v.TemplateCode == form.Code)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
        return versions.Select(ToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<WordTemplateCheckDto> CheckAsync(string code, Stream content, string fileName, long size, CancellationToken ct = default)
    {
        var form = RequireForm(code);
        var bytes = await ReadUploadAsync(content, fileName, size, ct);
        return WordTemplateValidator.Check(form, bytes);
    }

    /// <inheritdoc />
    public async Task<WordTemplateCheckDto> UploadAsync(string code, Stream content, string fileName, long size, string? note, bool activate, CancellationToken ct = default)
    {
        var form = RequireForm(code);
        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote?.Length > WordTemplateVersion.MaxNoteLength)
            throw new ValidationException($"Ghi chú dài tối đa {WordTemplateVersion.MaxNoteLength} ký tự.");

        var bytes = await ReadUploadAsync(content, fileName, size, ct);
        var check = WordTemplateValidator.Check(form, bytes);
        if (!check.IsValid)
            return check;

        var nextNumber = (await Versions.Where(v => v.TemplateCode == form.Code).MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0) + 1;
        var id = Guid.NewGuid();
        var objectKey = $"word-templates/{form.Code.ToLowerInvariant()}/{DateTime.UtcNow:yyyyMM}/{id:N}_v{nextNumber}.docx";
        using (var stream = new MemoryStream(bytes, writable: false))
            await _storage.SaveFileAsync(stream, objectKey, DocxContentType);

        var (userId, userName) = await CurrentUserAsync(ct);
        var now = DateTime.UtcNow;
        var version = new WordTemplateVersion
        {
            Id = id,
            TemplateCode = form.Code,
            VersionNumber = nextNumber,
            OriginalFileName = SafeFileName(fileName),
            ObjectKey = objectKey,
            FileSize = bytes.LongLength,
            Checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Note = trimmedNote,
            TagsJson = JsonSerializer.Serialize(check.Tags),
            WarningsJson = JsonSerializer.Serialize(check.Warnings),
            UploadedById = userId,
            UploadedByName = userName,
            UploadedAt = now
        };

        try
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                Versions.Add(version);
                if (activate)
                {
                    await DeactivateAllAsync(form.Code, ct);
                    MarkActive(version, userId, userName, now);
                }
                await _db.SaveChangesAsync(ct);
            }, ct);
        }
        catch
        {
            // Không để tệp mồ côi trong kho khi lưu metadata thất bại.
            await _storage.DeleteFileAsync(objectKey);
            throw;
        }

        check.Version = ToDto(version);
        return check;
    }

    /// <inheritdoc />
    public async Task<WordTemplateVersionDto> ActivateAsync(string code, Guid versionId, CancellationToken ct = default)
    {
        var form = RequireForm(code);
        var version = await Versions.FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateCode == form.Code, ct)
            ?? throw new NotFoundException($"Không tìm thấy phiên bản file mẫu của {form.Name}. Hãy tải lại trang để xem lịch sử mới nhất.");
        if (!_storage.FileExists(version.ObjectKey))
            throw new ValidationException($"Tệp của phiên bản {version.VersionNumber} không còn trong kho tệp nên không kích hoạt được. Hãy tải lên phiên bản mới.");

        var (userId, userName) = await CurrentUserAsync(ct);
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await DeactivateAllAsync(form.Code, ct);
            MarkActive(version, userId, userName, DateTime.UtcNow);
            await _db.SaveChangesAsync(ct);
        }, ct);
        return ToDto(version);
    }

    /// <inheritdoc />
    public async Task UseOriginalAsync(string code, CancellationToken ct = default)
    {
        var form = RequireForm(code);
        await DeactivateAllAsync(form.Code, ct);
    }

    /// <inheritdoc />
    public async Task<FileDownload> DownloadCurrentAsync(string code, CancellationToken ct = default)
    {
        var form = RequireForm(code);
        var active = await Versions.AsNoTracking().FirstOrDefaultAsync(v => v.TemplateCode == form.Code && v.IsActive, ct);
        return active == null
            ? Original(form)
            : await ReadVersionAsync(form, active);
    }

    /// <inheritdoc />
    public Task<FileDownload> DownloadOriginalAsync(string code, CancellationToken ct = default) =>
        Task.FromResult(Original(RequireForm(code)));

    /// <inheritdoc />
    public async Task<FileDownload> DownloadVersionAsync(string code, Guid versionId, CancellationToken ct = default)
    {
        var form = RequireForm(code);
        var version = await Versions.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionId && v.TemplateCode == form.Code, ct)
            ?? throw new NotFoundException($"Không tìm thấy phiên bản file mẫu của {form.Name}.");
        return await ReadVersionAsync(form, version);
    }

    /// <summary>Danh mục tag của biểu mẫu: tag riêng (bắt buộc) và tag thông tin đơn vị (tùy chọn).</summary>
    public static List<WordTemplateTagDto> TagCatalog(WordFormDefinition form) =>
        form.FormTags.Select(t => new WordTemplateTagDto { Tag = t.Tag, Kind = t.Kind, Within = t.Within, Required = true })
            .Concat(OrganizationTemplateFields.Tags.Select(t => new WordTemplateTagDto { Tag = t.Tag, Kind = t.Kind, IsCommon = true }))
            .ToList();

    private static WordFormDefinition RequireForm(string code) =>
        WordFormCatalog.Find(code)
            ?? throw new NotFoundException($"Không có biểu mẫu mã \"{code}\". Các mã hợp lệ: {string.Join(", ", WordFormCatalog.All.Select(f => f.Code))}.");

    /// <summary>Kiểm tra phần mở rộng, dung lượng, magic bytes (ZIP) rồi đọc tệp vào bộ nhớ (tối đa <see cref="MaxFileSize"/>).</summary>
    private static async Task<byte[]> ReadUploadAsync(Stream content, string fileName, long size, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!string.Equals(Path.GetExtension(fileName ?? string.Empty), ".docx", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Chỉ nhận tệp Word .docx. Hãy lưu file mẫu bằng Word với kiểu \"Word Document (*.docx)\".");
        if (size <= 0)
            throw new ValidationException("Tệp tải lên rỗng. Hãy chọn lại file mẫu.");
        if (size > MaxFileSize)
            throw new ValidationException($"File mẫu vượt quá dung lượng cho phép ({MaxFileSize / 1024 / 1024} MB). Hãy nén ảnh trong tài liệu rồi tải lại.");

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxFileSize)
                throw new ValidationException($"File mẫu vượt quá dung lượng cho phép ({MaxFileSize / 1024 / 1024} MB).");
            buffer.Write(chunk, 0, read);
        }

        var bytes = buffer.ToArray();
        // .docx là gói ZIP: bắt đầu bằng "PK\x03\x04".
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B || bytes[2] != 0x03 || bytes[3] != 0x04)
            throw new ValidationException("Nội dung tệp không phải tài liệu Word .docx (sai chữ ký tệp). Hãy mở bằng Word và lưu lại dạng .docx.");
        return bytes;
    }

    private async Task DeactivateAllAsync(string code, CancellationToken ct)
    {
        var active = await Versions.Where(v => v.TemplateCode == code && v.IsActive).ToListAsync(ct);
        if (active.Count == 0)
            return;
        foreach (var v in active)
            v.IsActive = false;
        // Lưu trước khi kích hoạt bản khác: chỉ mục duy nhất "một phiên bản kích hoạt" kiểm tra theo từng câu lệnh.
        await _db.SaveChangesAsync(ct);
    }

    private static void MarkActive(WordTemplateVersion version, Guid? userId, string? userName, DateTime at)
    {
        version.IsActive = true;
        version.ActivatedAt = at;
        version.ActivatedById = userId;
        version.ActivatedByName = userName;
    }

    private FileDownload Original(WordFormDefinition form) =>
        new(_bundled.Load(form.TemplateFileName), DocxContentType, form.TemplateFileName);

    private async Task<FileDownload> ReadVersionAsync(WordFormDefinition form, WordTemplateVersion version)
    {
        var stream = await _storage.GetFileStreamAsync(version.ObjectKey)
            ?? throw new NotFoundException($"Tệp của phiên bản {version.VersionNumber} ({form.Name}) không còn trong kho tệp.");
        await using (stream)
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            var name = $"{Path.GetFileNameWithoutExtension(form.TemplateFileName)}_v{version.VersionNumber}.docx";
            return new FileDownload(buffer.ToArray(), DocxContentType, name);
        }
    }

    private async Task<(Guid? Id, string? Name)> CurrentUserAsync(CancellationToken ct)
    {
        var id = _currentUser.UserId;
        if (id == null)
            return (null, null);
        var name = await _db.PartyMemberProfiles.AsNoTracking().Where(u => u.Id == id).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        return (id, name);
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(name))
            name = "mau.docx";
        return name.Length > WordTemplateVersion.MaxFileNameLength ? name[^WordTemplateVersion.MaxFileNameLength..] : name;
    }

    private static WordTemplateVersionDto ToDto(WordTemplateVersion v) => new()
    {
        Id = v.Id,
        TemplateCode = v.TemplateCode,
        VersionNumber = v.VersionNumber,
        OriginalFileName = v.OriginalFileName,
        FileSize = v.FileSize,
        Checksum = v.Checksum,
        IsActive = v.IsActive,
        Note = v.Note,
        Tags = ParseList(v.TagsJson),
        Warnings = ParseList(v.WarningsJson),
        UploadedAt = v.UploadedAt,
        UploadedByName = v.UploadedByName,
        ActivatedAt = v.ActivatedAt,
        ActivatedByName = v.ActivatedByName
    };

    private static List<string> ParseList(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
