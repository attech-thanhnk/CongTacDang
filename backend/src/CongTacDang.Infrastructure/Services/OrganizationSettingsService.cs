using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Services;

/// <summary>Cache thông tin đơn vị: xóa ngay khi sửa; tự hết hạn để các tiến trình máy chủ khác cũng nhận giá trị mới.</summary>
public sealed class OrganizationSettingsCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private sealed record Entry(OrganizationSettingsDto Value, DateTime ExpiresAt);

    private Entry? _entry;

    /// <summary>Null nếu chưa có hoặc đã hết hạn.</summary>
    public OrganizationSettingsDto? Get()
    {
        var entry = Volatile.Read(ref _entry);
        return entry != null && entry.ExpiresAt > DateTime.UtcNow ? entry.Value : null;
    }

    public void Set(OrganizationSettingsDto value) =>
        Volatile.Write(ref _entry, new Entry(value, DateTime.UtcNow.Add(Lifetime)));

    public void Invalidate() => Volatile.Write(ref _entry, null);
}

/// <summary>Thông tin đơn vị: bản ghi duy nhất <see cref="OrganizationSettings"/>.</summary>
public sealed class OrganizationSettingsService : IOrganizationSettingsService
{
    private static readonly Regex ShortNamePattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    /// <summary>Ảnh nền đăng nhập tối đa 5 MB.</summary>
    public const long MaxLoginBackgroundSize = 5 * 1024 * 1024;

    public const string LoginBackgroundPath = "/api/settings/organization/login-background";

    private static readonly Dictionary<string, string> LoginBackgroundTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp"
    };

    private readonly CongTacDangDbContext _db;
    private readonly OrganizationSettingsCache _cache;
    private readonly IFileStorageService _storage;

    public OrganizationSettingsService(CongTacDangDbContext db, OrganizationSettingsCache cache, IFileStorageService storage)
    {
        _db = db;
        _cache = cache;
        _storage = storage;
    }

    /// <inheritdoc />
    public async Task<OrganizationSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var cached = _cache.Get();
        if (cached != null)
            return cached;

        // Chưa seed → giá trị mặc định, không ghi. OrderBy để EF không cảnh báo First không sắp xếp.
        var entity = await _db.Set<OrganizationSettings>().AsNoTracking().OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).FirstOrDefaultAsync(ct)
            ?? DataSeeder.DefaultOrganizationSettings();
        var dto = await ToDtoAsync(entity, ct);
        _cache.Set(dto);
        return dto;
    }

    /// <inheritdoc />
    public async Task<PublicOrganizationInfoDto> GetPublicAsync(CancellationToken ct = default)
    {
        var settings = await GetAsync(ct);
        return new PublicOrganizationInfoDto
        {
            SystemName = settings.SystemName,
            CompanyName = settings.CompanyName,
            ParentCompanyName = settings.ParentCompanyName,
            ShortName = settings.ShortName,
            LoginBackgroundUrl = settings.LoginBackgroundUrl,
            LoginPanelPosition = settings.LoginPanelPosition
        };
    }

    /// <inheritdoc />
    public async Task<OrganizationSettingsDto> UpdateAsync(UpdateOrganizationSettingsDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<string>();
        var partyName = Required(request.PartyCommitteeName, "Tên Đảng bộ", OrganizationSettings.MaxNameLength, errors);
        var superiorName = Required(request.SuperiorPartyName, "Tên tổ chức Đảng cấp trên", OrganizationSettings.MaxNameLength, errors);
        var companyName = Required(request.CompanyName, "Tên công ty", OrganizationSettings.MaxNameLength, errors);
        var parentName = Required(request.ParentCompanyName, "Tên đơn vị chủ quản cấp trên", OrganizationSettings.MaxNameLength, errors);
        var shortName = Required(request.ShortName, "Tên viết tắt", OrganizationSettings.MaxShortNameLength, errors);
        var location = Required(request.Location, "Địa danh", OrganizationSettings.MaxLocationLength, errors);
        var systemName = Required(request.SystemName, "Tên hiển thị hệ thống", OrganizationSettings.MaxNameLength, errors);
        var appraisalUnit = request.AppraisalUnitName?.Trim() ?? string.Empty;
        if (appraisalUnit.Length > OrganizationSettings.MaxNameLength)
            errors.Add($"Cơ quan thẩm định không được dài quá {OrganizationSettings.MaxNameLength} ký tự.");
        if (shortName.Length > 0 && !ShortNamePattern.IsMatch(shortName))
            errors.Add("Tên viết tắt chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới (dùng trong tên tệp tải xuống), ví dụ \"ABC-CORP\".");
        var panelPosition = request.LoginPanelPosition?.Trim().ToLowerInvariant();
        if (panelPosition != null && !OrganizationSettings.LoginPanelPositions.Contains(panelPosition))
            errors.Add("Vị trí khung đăng nhập chỉ nhận một trong các giá trị: giữa, bên trái, bên phải.");
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        var entity = await LoadOrCreateAsync(ct);

        entity.PartyCommitteeName = partyName;
        entity.SuperiorPartyName = superiorName;
        entity.CompanyName = companyName;
        entity.ParentCompanyName = parentName;
        entity.ShortName = shortName;
        entity.Location = location;
        entity.AppraisalUnitName = appraisalUnit;
        entity.SystemName = systemName;
        if (panelPosition != null)
            entity.LoginPanelPosition = panelPosition;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _cache.Invalidate();
        return await GetAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OrganizationSettingsDto> SetLoginBackgroundAsync(Stream content, string fileName, long size, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (!LoginBackgroundTypes.TryGetValue(extension, out var contentType))
            throw new ValidationException("Chỉ nhận ảnh JPG, PNG hoặc WEBP.");
        if (size <= 0)
            throw new ValidationException("Tệp ảnh rỗng. Hãy chọn lại ảnh.");
        if (size > MaxLoginBackgroundSize)
            throw new ValidationException($"Ảnh vượt quá dung lượng cho phép ({MaxLoginBackgroundSize / 1024 / 1024} MB).");

        var bytes = await ReadLimitedAsync(content, ct);
        if (!FileSignatures.Matches(extension, bytes))
            throw new ValidationException("Nội dung tệp không phải ảnh đúng định dạng đã khai báo.");

        var objectKey = $"login-background/{Guid.NewGuid():N}{extension}";
        using (var stream = new MemoryStream(bytes, writable: false))
            await _storage.SaveFileAsync(stream, objectKey, contentType);

        string? previousKey;
        try
        {
            var entity = await LoadOrCreateAsync(ct);
            previousKey = entity.LoginBackgroundKey;
            var now = DateTime.UtcNow;
            entity.LoginBackgroundKey = objectKey;
            entity.LoginBackgroundContentType = contentType;
            entity.LoginBackgroundFileName = SafeFileName(fileName!);
            entity.LoginBackgroundUpdatedAt = now;
            entity.UpdatedAt = now;
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            // Không để tệp mồ côi khi lưu thất bại.
            await _storage.DeleteFileAsync(objectKey);
            throw;
        }

        _cache.Invalidate();
        await DeleteQuietlyAsync(previousKey);
        return await GetAsync(ct);
    }

    /// <inheritdoc />
    public async Task<OrganizationSettingsDto> RemoveLoginBackgroundAsync(CancellationToken ct = default)
    {
        var entity = await _db.Set<OrganizationSettings>().OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (entity?.LoginBackgroundKey is not { } previousKey)
            return await GetAsync(ct);

        var now = DateTime.UtcNow;
        entity.LoginBackgroundKey = null;
        entity.LoginBackgroundContentType = null;
        entity.LoginBackgroundFileName = null;
        entity.LoginBackgroundUpdatedAt = now;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);
        _cache.Invalidate();
        await DeleteQuietlyAsync(previousKey);
        return await GetAsync(ct);
    }

    /// <inheritdoc />
    public async Task<LoginBackgroundFile?> OpenLoginBackgroundAsync(CancellationToken ct = default)
    {
        var row = await _db.Set<OrganizationSettings>().AsNoTracking().OrderBy(s => s.CreatedAt).ThenBy(s => s.Id)
            .Select(s => new { s.LoginBackgroundKey, s.LoginBackgroundContentType })
            .FirstOrDefaultAsync(ct);
        if (row?.LoginBackgroundKey == null || row.LoginBackgroundContentType == null)
            return null;

        var stream = await _storage.GetFileStreamAsync(row.LoginBackgroundKey);
        return stream == null ? null : new LoginBackgroundFile(stream, row.LoginBackgroundContentType);
    }

    private async Task<OrganizationSettings> LoadOrCreateAsync(CancellationToken ct)
    {
        var entity = await _db.Set<OrganizationSettings>().OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (entity != null)
            return entity;

        // Chưa seed: tạo từ giá trị mặc định để không ghi chuỗi rỗng vào các trường bắt buộc.
        entity = DataSeeder.DefaultOrganizationSettings();
        _db.Set<OrganizationSettings>().Add(entity);
        return entity;
    }

    private static async Task<byte[]> ReadLimitedAsync(Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxLoginBackgroundSize)
                throw new ValidationException($"Ảnh vượt quá dung lượng cho phép ({MaxLoginBackgroundSize / 1024 / 1024} MB).");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/')).Trim();
        return name.Length > OrganizationSettings.MaxFileNameLength ? name[^OrganizationSettings.MaxFileNameLength..] : name;
    }

    private async Task DeleteQuietlyAsync(string? objectKey)
    {
        if (string.IsNullOrEmpty(objectKey))
            return;
        try
        {
            await _storage.DeleteFileAsync(objectKey);
        }
        catch (IOException)
        {
            // Tệp cũ đang bị khóa: bỏ qua, không ảnh hưởng ảnh mới.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Thông tin đơn vị dạng tag dùng chung cho biểu mẫu Word.</summary>
    public static OrganizationTemplateFields ToTemplateFields(OrganizationSettingsDto settings) =>
        OrganizationTemplateFields.From(settings.PartyCommitteeName, settings.SuperiorPartyName, settings.CompanyName,
            settings.ParentCompanyName, settings.ShortName, settings.Location, settings.AppraisalUnitName);

    private async Task<OrganizationSettingsDto> ToDtoAsync(OrganizationSettings entity, CancellationToken ct)
    {
        string? updatedBy = null;
        if (entity.UpdatedBy is { } userId)
        {
            updatedBy = await _db.PartyMemberProfiles.IgnoreQueryFilters().AsNoTracking()
                .Where(u => u.Id == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        }

        return new OrganizationSettingsDto
        {
            PartyCommitteeName = entity.PartyCommitteeName,
            SuperiorPartyName = entity.SuperiorPartyName,
            CompanyName = entity.CompanyName,
            ParentCompanyName = entity.ParentCompanyName,
            ShortName = entity.ShortName,
            Location = entity.Location,
            AppraisalUnitName = entity.AppraisalUnitName,
            SystemName = entity.SystemName,
            LoginBackgroundUrl = entity.LoginBackgroundKey != null && entity.LoginBackgroundUpdatedAt is { } version
                ? $"{LoginBackgroundPath}?v={version.Ticks}"
                : null,
            LoginBackgroundFileName = entity.LoginBackgroundKey != null ? entity.LoginBackgroundFileName : null,
            LoginPanelPosition = entity.LoginPanelPosition,
            UpdatedAt = entity.UpdatedAt,
            UpdatedByName = updatedBy
        };
    }

    private static string Required(string? value, string label, int maxLength, List<string> errors)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            errors.Add($"{label} không được để trống.");
        else if (trimmed.Length > maxLength)
            errors.Add($"{label} dài tối đa {maxLength} ký tự.");
        return trimmed;
    }
}
