using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CongTacDang.Application.Services;

public sealed class AttachmentOptions
{
    /// <summary>Số tệp chưa gắn đối tượng tối đa mỗi người; 0 = không giới hạn.</summary>
    public int MaxPendingUnlinkedPerUser { get; set; } = 30;

    public bool CleanupEnabled { get; set; } = true;

    /// <summary>Tệp chưa gắn quá số giờ này thì bị dọn.</summary>
    public int CleanupRetentionHours { get; set; } = 24;

    public int CleanupIntervalMinutes { get; set; } = 60;
}

public interface IUnlinkedAttachmentCleaner
{
    /// <summary>Dọn một lượt; trả số nhóm tệp đã dọn.</summary>
    Task<int> CleanupAsync(DateTime utcNow, CancellationToken ct = default);
}

/// <summary>Chỉ dọn nhóm tệp mà mọi phiên bản đều chưa gắn, không được tham chiếu và đều quá hạn.</summary>
public sealed class UnlinkedAttachmentCleaner : IUnlinkedAttachmentCleaner
{
    public const int BatchGroups = 500;

    private readonly IUnlinkedAttachmentRepository _repo;
    private readonly IFileStorageService _storage;
    private readonly AttachmentOptions _options;
    private readonly ILogger<UnlinkedAttachmentCleaner> _logger;

    public UnlinkedAttachmentCleaner(
        IUnlinkedAttachmentRepository repo,
        IFileStorageService storage,
        AttachmentOptions options,
        ILogger<UnlinkedAttachmentCleaner>? logger = null)
    {
        _repo = repo;
        _storage = storage;
        _options = options;
        _logger = logger ?? NullLogger<UnlinkedAttachmentCleaner>.Instance;
    }

    public static List<UnlinkedAttachmentVersion> SelectForDeletion(
        IEnumerable<UnlinkedAttachmentVersion> versions, IReadOnlySet<Guid> referencedIds, DateTime cutoff)
    {
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(referencedIds);
        return versions
            .GroupBy(v => v.GroupId)
            .Where(g => g.All(v => v.EffectiveOwnerType == AttachmentOwnerTypes.Unlinked)
                && g.All(v => !referencedIds.Contains(v.Id))
                && g.Max(v => v.UploadedAt) < cutoff)
            .SelectMany(g => g)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> CleanupAsync(DateTime utcNow, CancellationToken ct = default)
    {
        if (_options.CleanupRetentionHours <= 0)
            return 0;
        var cutoff = utcNow.AddHours(-_options.CleanupRetentionHours);
        var candidates = await _repo.GetCleanupCandidatesAsync(cutoff, BatchGroups, ct);
        if (candidates.Count == 0)
            return 0;

        var referenced = await _repo.GetReferencedAttachmentIdsAsync(candidates.Select(c => c.Id).ToList(), ct);
        var selected = SelectForDeletion(candidates, referenced, cutoff);
        if (selected.Count == 0)
            return 0;

        // Xóa bản ghi trước: nếu xóa tệp trên đĩa lỗi chỉ còn tệp mồ côi vô hại, không có bản ghi trỏ tới tệp đã mất.
        await _repo.DeleteAsync(selected.Select(v => v.Id).ToList(), ct);
        foreach (var version in selected)
        {
            try
            {
                await _storage.DeleteFileAsync(version.ObjectKey);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Không xóa được tệp chưa gắn {ObjectKey} trên kho lưu trữ (bản ghi đã xóa).", version.ObjectKey);
            }
        }
        return selected.Select(v => v.GroupId).Distinct().Count();
    }
}
