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

    private readonly CongTacDangDbContext _db;
    private readonly OrganizationSettingsCache _cache;

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
            ShortName = settings.ShortName
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
        if (errors.Count > 0)
            throw new ValidationException(string.Join(" ", errors));

        var entity = await _db.Set<OrganizationSettings>().OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).FirstOrDefaultAsync(ct);
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
        entity.AppraisalUnitName = appraisalUnit;
        entity.SystemName = systemName;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        _cache.Invalidate();
        return await GetAsync(ct);
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
