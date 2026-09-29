using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>
/// Quản lý bộ tiêu chí và thang điểm theo phiên bản (task 16): danh sách, xem, tạo nháp, nhân bản, sửa nháp, xuất bản (kiểm tra
/// đầy đủ), lưu trữ, xóa nháp. Xem: <c>criteria.manage</c> hoặc <c>period.manage</c>; ghi: <c>criteria.manage</c>.
/// </summary>
public interface ICriteriaSetService
{
    Task<List<CriteriaSetListItemDto>> ListAsync(CancellationToken ct = default);
    Task<CriteriaSetDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<CriteriaSetDto> CreateAsync(CreateCriteriaSetDto dto, CancellationToken ct = default);
    Task<CriteriaSetDto> CloneAsync(Guid id, CloneCriteriaSetDto dto, CancellationToken ct = default);
    Task<CriteriaSetDto> UpdateAsync(Guid id, UpdateCriteriaSetDto dto, CancellationToken ct = default);
    Task<CriteriaSetDto> PublishAsync(Guid id, CriteriaSetActionDto dto, CancellationToken ct = default);
    Task<CriteriaSetDto> ArchiveAsync(Guid id, CriteriaSetActionDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CriteriaSetActionDto dto, CancellationToken ct = default);

    /// <summary>Nội dung mặc định (theo bản trích xuất HD03) cho mẫu tự chấm — dùng khi tạo bộ mới.</summary>
    CriteriaSetContent DefaultContent(string selfScoreForm);

    /// <summary>Khung tỷ trọng chọn được cho cán bộ: từ bộ của kỳ gần nhất đã chọn bộ, không có thì bộ đã xuất bản mới nhất.</summary>
    Task<WeightFrameOptionsDto> GetWeightFrameOptionsAsync(CancellationToken ct = default);
}

/// <summary>Triển khai quản lý bộ tiêu chí.</summary>
public sealed class CriteriaSetService : ICriteriaSetService
{
    private const int MaxCodeLength = 50;
    private const int MaxNameLength = 200;
    private const int MaxNotesLength = 4000;

    private readonly ICriteriaSetRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public CriteriaSetService(ICriteriaSetRepository repo, IUnitOfWork unitOfWork, IAuthorizationGuard guard, ICurrentUserService currentUser)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
    }

    #region Đọc

    /// <inheritdoc />
    public async Task<List<CriteriaSetListItemDto>> ListAsync(CancellationToken ct = default)
    {
        EnsureRead();
        var usages = await _repo.ListUsagesAsync(ct);
        return (await _repo.ListAsync(ct))
            .OrderBy(s => s.Status == CriteriaSetStatus.Archived)
            .ThenByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .Select(s => Fill(new CriteriaSetListItemDto(), s, SafeContent(s), usages))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CriteriaSetDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        EnsureRead();
        var set = await _repo.FindAsync(id, ct) ?? throw NotFound(id);
        return await ToDtoAsync(set, ct);
    }

    /// <inheritdoc />
    public CriteriaSetContent DefaultContent(string selfScoreForm) =>
        string.Equals(selfScoreForm, CriteriaSetContent.Form09B, StringComparison.OrdinalIgnoreCase)
            ? CriteriaSetDefaults.Build09B()
            : CriteriaSetDefaults.Build09A();

    /// <inheritdoc />
    public async Task<WeightFrameOptionsDto> GetWeightFrameOptionsAsync(CancellationToken ct = default)
    {
        CriteriaSnapshot? snapshot = null;
        try
        {
            snapshot = CriteriaSnapshot.Parse(await _repo.LatestPeriodSnapshotAsync(ct));
        }
        catch (FormatException)
        {
            // Ảnh chụp lỗi định dạng → dùng bộ đã xuất bản mới nhất.
        }

        string? sourceName;
        CriteriaSetContent? content;
        if (snapshot != null)
        {
            sourceName = snapshot.Name;
            content = snapshot.Content;
        }
        else
        {
            var latest = await _repo.LatestPublishedAsync(null, ct);
            sourceName = latest?.Name;
            content = latest == null ? null : SafeContent(latest);
        }

        return new WeightFrameOptionsDto
        {
            SourceName = sourceName,
            Frames = (content?.WeightFrames ?? new List<WeightFrame>())
                .Select(f => new WeightFrameOptionDto { Code = f.Code, Name = f.Name, A = f.A, B = f.B, C = f.C, D = f.D })
                .ToList()
        };
    }

    #endregion

    #region Ghi

    /// <inheritdoc />
    public async Task<CriteriaSetDto> CreateAsync(CreateCriteriaSetDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var form = NormalizeForm(dto.SelfScoreForm);
        var code = await ValidateCodeAsync(dto.Code, null, ct);
        var content = (dto.Content ?? DefaultContent(form)).Normalize();
        EnsureStorable(content);

        var now = DateTime.UtcNow;
        var set = new CriteriaSet
        {
            Code = code,
            Name = ValidateName(dto.Name),
            SelfScoreForm = form,
            Notes = ValidateNotes(dto.Notes),
            Status = CriteriaSetStatus.Draft,
            Content = content.ToJson(),
            CreatedAt = now,
            UpdatedAt = now
        };
        _repo.Add(set);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(set.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CriteriaSetDto> CloneAsync(Guid id, CloneCriteriaSetDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var source = await _repo.FindAsync(id, ct) ?? throw NotFound(id);
        var code = string.IsNullOrWhiteSpace(dto?.Code)
            ? await NextCloneCodeAsync(source.Code, ct)
            : await ValidateCodeAsync(dto!.Code, null, ct);
        var name = string.IsNullOrWhiteSpace(dto?.Name) ? Truncate($"{source.Name} (bản nháp)", MaxNameLength) : ValidateName(dto!.Name);

        var now = DateTime.UtcNow;
        var set = new CriteriaSet
        {
            Code = code,
            Name = name,
            SelfScoreForm = source.SelfScoreForm,
            Notes = source.Notes,
            Status = CriteriaSetStatus.Draft,
            Content = SafeContent(source).ToJson(),
            SourceSetId = source.Id,
            CreatedAt = now,
            UpdatedAt = now
        };
        _repo.Add(set);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(set.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CriteriaSetDto> UpdateAsync(Guid id, UpdateCriteriaSetDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        ArgumentNullException.ThrowIfNull(dto);
        var set = await LoadForWriteAsync(id, dto.Version, ct);
        if (set.Status != CriteriaSetStatus.Draft)
            throw new ConflictException($"Bộ tiêu chí \"{set.Name}\" đã xuất bản nên không sửa được. Hãy nhân bản thành bản nháp mới rồi sửa bản nháp.");

        if (dto.Code != null)
            set.Code = await ValidateCodeAsync(dto.Code, set.Id, ct);
        if (dto.Name != null)
            set.Name = ValidateName(dto.Name);
        if (dto.SelfScoreForm != null)
            set.SelfScoreForm = NormalizeForm(dto.SelfScoreForm);
        if (dto.Notes != null)
            set.Notes = ValidateNotes(dto.Notes);
        if (dto.Content != null)
        {
            var content = dto.Content.Normalize();
            EnsureStorable(content);
            set.Content = content.ToJson();
        }

        set.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.SetOriginalVersion(set, dto.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(set.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CriteriaSetDto> PublishAsync(Guid id, CriteriaSetActionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var set = await LoadForWriteAsync(id, dto?.Version, ct);
        if (set.Status != CriteriaSetStatus.Draft)
            throw new ConflictException($"Chỉ xuất bản được bản nháp; bộ \"{set.Name}\" đang ở trạng thái \"{StatusName(set.Status)}\".");
        var errors = SafeContent(set).Validate(set.SelfScoreForm);
        if (errors.Count > 0)
            throw new ValidationException("Bộ tiêu chí chưa xuất bản được vì: " + string.Join(" ", errors));

        var now = DateTime.UtcNow;
        set.Status = CriteriaSetStatus.Published;
        set.PublishedAt = now;
        set.PublishedBy = _currentUser.UserId;
        set.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(set, dto?.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(set.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CriteriaSetDto> ArchiveAsync(Guid id, CriteriaSetActionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var set = await LoadForWriteAsync(id, dto?.Version, ct);
        if (set.Status != CriteriaSetStatus.Published)
            throw new ConflictException("Chỉ lưu trữ được bộ đã xuất bản (bản nháp thì xóa được).");

        var now = DateTime.UtcNow;
        set.Status = CriteriaSetStatus.Archived;
        set.ArchivedAt = now;
        set.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(set, dto?.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await GetAsync(set.Id, ct);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CriteriaSetActionDto dto, CancellationToken ct = default)
    {
        EnsureManage();
        var set = await LoadForWriteAsync(id, dto?.Version, ct);
        if (set.Status != CriteriaSetStatus.Draft)
            throw new ConflictException($"Không xóa được bộ \"{set.Name}\" vì đã xuất bản. Bộ không còn dùng thì lưu trữ.");
        var usedBy = (await _repo.ListUsagesAsync(ct)).Where(u => u.CriteriaSetId == set.Id).Select(u => u.PeriodName).ToList();
        if (usedBy.Count > 0)
            throw new ConflictException($"Không xóa được bộ \"{set.Name}\" vì đang được kỳ {string.Join(", ", usedBy)} dùng.");

        _repo.Remove(set);
        _unitOfWork.SetOriginalVersion(set, dto?.Version);
        await _unitOfWork.SaveChangesAsync(ct);
    }

    #endregion

    #region Hỗ trợ

    /// <summary>Tên hiển thị trạng thái.</summary>
    public static string StatusName(CriteriaSetStatus status) => status switch
    {
        CriteriaSetStatus.Draft => "Bản nháp",
        CriteriaSetStatus.Published => "Đã xuất bản",
        CriteriaSetStatus.Archived => "Lưu trữ",
        _ => status.ToString()
    };

    private void EnsureManage() => _guard.Ensure(PermissionCodes.CriteriaManage, AccessTarget.None);

    private void EnsureRead()
    {
        if (!_guard.HasAny(PermissionCodes.PeriodManage))
            _guard.Ensure(PermissionCodes.CriteriaManage, AccessTarget.None);
    }

    private async Task<CriteriaSetDto> ToDtoAsync(CriteriaSet set, CancellationToken ct)
    {
        var usages = await _repo.ListUsagesAsync(ct);
        var content = SafeContent(set);
        var dto = Fill(new CriteriaSetDto(), set, content, usages);
        dto.Content = content;
        dto.ValidationErrors = content.Validate(set.SelfScoreForm);
        return dto;
    }

    private static T Fill<T>(T dto, CriteriaSet s, CriteriaSetContent content, IReadOnlyList<CriteriaSetUsage> usages)
        where T : CriteriaSetListItemDto
    {
        dto.Id = s.Id;
        dto.Version = s.Version;
        dto.Code = s.Code;
        dto.Name = s.Name;
        dto.Status = s.Status.ToString();
        dto.StatusDisplayName = StatusName(s.Status);
        dto.SelfScoreForm = s.SelfScoreForm;
        dto.Notes = s.Notes;
        dto.SourceSetId = s.SourceSetId;
        dto.PublishedAt = s.PublishedAt;
        dto.ArchivedAt = s.ArchivedAt;
        dto.CreatedAt = s.CreatedAt;
        dto.UpdatedAt = s.UpdatedAt;
        dto.GeneralItemCount = content.AllItems.Count();
        dto.AxisCount = content.Axes.Count;
        dto.WeightFrameCount = content.WeightFrames.Count;
        dto.UsedByPeriods = usages.Where(u => u.CriteriaSetId == s.Id).Select(u => u.PeriodName).ToList();
        return dto;
    }

    /// <summary>Đọc nội dung; lỗi định dạng → nội dung rỗng (hiển thị lỗi kiểm tra để sửa).</summary>
    private static CriteriaSetContent SafeContent(CriteriaSet set)
    {
        try
        {
            return set.GetContent();
        }
        catch (FormatException)
        {
            return new CriteriaSetContent();
        }
    }

    /// <summary>Bản nháp được lưu dù chưa đầy đủ; chỉ chặn nội dung không đọc lại được (phiên bản schema lạ).</summary>
    private static void EnsureStorable(CriteriaSetContent content)
    {
        if (content.SchemaVersion != CriteriaSetContent.CurrentSchemaVersion)
            throw new ValidationException($"Phiên bản nội dung bộ tiêu chí {content.SchemaVersion} không được hỗ trợ (chỉ hỗ trợ phiên bản {CriteriaSetContent.CurrentSchemaVersion}).");
    }

    private async Task<CriteriaSet> LoadForWriteAsync(Guid id, uint? version, CancellationToken ct)
    {
        if (version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của bộ tiêu chí. Hãy tải lại trang rồi thực hiện lại.");
        var set = await _repo.FindAsync(id, ct) ?? throw NotFound(id);
        if (set.Version != version)
            throw new ConflictException("Bộ tiêu chí đã được người khác cập nhật. Hãy tải lại trang để xem dữ liệu mới nhất.");
        return set;
    }

    private async Task<string> ValidateCodeAsync(string? code, Guid? exceptId, CancellationToken ct)
    {
        var clean = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(clean))
            throw new ValidationException("Hãy nhập mã bộ tiêu chí (ví dụ \"HD03-09B-Q3-2026\").");
        if (clean.Length > MaxCodeLength || !clean.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            throw new ValidationException($"Mã bộ tiêu chí \"{clean}\" không hợp lệ: chỉ gồm chữ không dấu, số, dấu chấm, gạch nối, gạch dưới (tối đa {MaxCodeLength} ký tự).");
        if (await _repo.CodeExistsAsync(clean, exceptId, ct))
            throw new ConflictException($"Mã bộ tiêu chí \"{clean}\" đã được dùng. Hãy chọn mã khác.");
        return clean;
    }

    private async Task<string> NextCloneCodeAsync(string sourceCode, CancellationToken ct)
    {
        for (var i = 2; i < 1000; i++)
        {
            var suffix = $"-V{i}";
            var candidate = Truncate(sourceCode, MaxCodeLength - suffix.Length) + suffix;
            if (!await _repo.CodeExistsAsync(candidate, null, ct))
                return candidate;
        }
        throw new ConflictException("Không sinh được mã cho bản nháp mới. Hãy nhập mã.");
    }

    private static string ValidateName(string? name)
    {
        var clean = name?.Trim();
        if (string.IsNullOrEmpty(clean))
            throw new ValidationException("Hãy nhập tên bộ tiêu chí.");
        if (clean.Length > MaxNameLength)
            throw new ValidationException($"Tên bộ tiêu chí không được dài quá {MaxNameLength} ký tự.");
        return clean;
    }

    private static string? ValidateNotes(string? notes)
    {
        var clean = notes?.Trim();
        if (string.IsNullOrEmpty(clean))
            return null;
        if (clean.Length > MaxNotesLength)
            throw new ValidationException($"Ghi chú không được dài quá {MaxNotesLength} ký tự.");
        return clean;
    }

    private static string NormalizeForm(string? form)
    {
        var clean = form?.Trim().ToUpperInvariant();
        if (!CriteriaSetContent.IsValidForm(clean))
            throw new ValidationException("Hãy chọn mẫu tự chấm: 09A (chấm theo nhiệm vụ Mẫu 01/02) hoặc 09B (chấm trực tiếp theo trục).");
        return clean!;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private static NotFoundException NotFound(Guid id) => new($"Không tìm thấy bộ tiêu chí với Id: {id}.");

    #endregion
}
