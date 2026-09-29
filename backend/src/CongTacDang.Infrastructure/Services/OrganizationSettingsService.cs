using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using CongTacDang.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Services;

/// <summary>
/// Cache thông tin đơn vị (singleton): đọc nhiều (mỗi lần xuất biểu mẫu, mỗi trang), sửa rất ít. Xóa ngay khi sửa;
/// tự hết hạn sau <see cref="Lifetime"/> để nhiều tiến trình máy chủ (nếu có) cũng nhận giá trị mới.
/// </summary>
public sealed class OrganizationSettingsCache
{
    /// <summary>Thời gian giữ trong cache.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private sealed record Entry(OrganizationSettingsDto Value, DateTime ExpiresAt);

    private Entry? _entry;

    /// <summary>Giá trị còn hạn; null nếu chưa có hoặc đã hết hạn.</summary>
    public OrganizationSettingsDto? Get()
    {
        var entry = Volatile.Read(ref _entry);
        return entry != null && entry.ExpiresAt > DateTime.UtcNow ? entry.Value : null;
    }

    /// <summary>Lưu giá trị.</summary>
    public void Set(OrganizationSettingsDto value) =>
        Volatile.Write(ref _entry, new Entry(value, DateTime.UtcNow.Add(Lifetime)));

    /// <summary>Xóa cache (sau khi sửa).</summary>
    public void Invalidate() => Volatile.Write(ref _entry, null);
}

/// <summary>Thông tin đơn vị (task 17 — T-80): bản ghi duy nhất <see cref="OrganizationSettings"/>.</summary>
public sealed class OrganizationSettingsService : IOrganizationSettingsService
{
    private static readonly Regex ShortNamePattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);

    private readonly CongTacDangDbContext _db;
    private readonly OrganizationSettingsCache _cache;

    /// <summary>Khởi tạo.</summary>
    public OrganizationSettingsService(CongTacDangDbContext db, OrganizationSettingsCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<OrganizationSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var cached = _cache.Get();
        if (cached != null)
            return cached;

        // Chưa có bản ghi (CSDL chưa seed) → giá trị mặc định, không ghi.
        var entity = await _db.Set<OrganizationSettings>().AsNoTracking().FirstOrDefaultAsync(ct)
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
            ParentCompanyName = settings.ParentCompanyName
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
        if (shortName.Length > 0 && !ShortNamePattern.IsMatch(shortName))
            errors.Add("Tên viết tắt chỉ gồm chữ không dấu, số, dấu gạch ngang hoặc gạch dưới (dùng trong tên tệp tải xuống), ví dụ \"ABC-CORP\".");
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        var entity = await _db.Set<OrganizationSettings>().FirstOrDefaultAsync(ct);
        if (entity == null)
        {
            entity = new OrganizationSettings { Id = OrganizationSettings.SingletonId };
            _db.Set<OrganizationSettings>().Add(entity);
        }

        entity.PartyCommitteeName = partyName;
        entity.SuperiorPartyName = superiorName;
        entity.CompanyName = companyName;
        entity.ParentCompanyName = parentName;
        entity.ShortName = shortName;
        entity.Location = location;
        entity.SystemName = systemName;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _cache.Invalidate();
        return await GetAsync(ct);
    }

    /// <summary>Thông tin đơn vị dạng tag dùng chung cho biểu mẫu Word.</summary>
    public static OrganizationTemplateFields ToTemplateFields(OrganizationSettingsDto settings) =>
        OrganizationTemplateFields.From(settings.PartyCommitteeName, settings.SuperiorPartyName, settings.CompanyName,
            settings.ParentCompanyName, settings.ShortName, settings.Location);

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
            SystemName = entity.SystemName,
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
