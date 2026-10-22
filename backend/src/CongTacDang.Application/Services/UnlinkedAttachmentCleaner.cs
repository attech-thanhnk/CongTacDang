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

/// <summary>
/// Cấu hình tệp đính kèm (mục <c>Attachments</c> của appsettings).
/// </summary>
public sealed class AttachmentOptions
{
    /// <summary>
    /// Số tệp tải lên chưa gắn đối tượng tối đa của mỗi người (<c>Attachments:MaxPendingUnlinkedPerUser</c>, mặc định 30; 0 = không giới hạn).
    /// </summary>
    public int MaxPendingUnlinkedPerUser { get; set; } = 30;

    /// <summary>Bật tác vụ nền dọn tệp chưa gắn (<c>Attachments:UnlinkedCleanup:Enabled</c>, mặc định bật).</summary>
    public bool CleanupEnabled { get; set; } = true;

    /// <summary>Tệp chưa gắn quá số giờ này thì bị dọn (<c>Attachments:UnlinkedCleanup:RetentionHours</c>, mặc định 24).</summary>
    public int CleanupRetentionHours { get; set; } = 24;

    /// <summary>Chu kỳ chạy dọn (<c>Attachments:UnlinkedCleanup:IntervalMinutes</c>, mặc định 60).</summary>
    public int CleanupIntervalMinutes { get; set; } = 60;
}

/// <summary>Dọn tệp tải lên chưa gắn đối tượng quá thời hạn.</summary>
public interface IUnlinkedAttachmentCleaner
{
    /// <summary>Dọn một lượt; trả số nhóm tệp đã dọn.</summary>
    Task<int> CleanupAsync(DateTime utcNow, CancellationToken ct = default);
}

/// <summary>
/// Dọn tệp tải lên chưa gắn đối tượng (tải lên rồi không lưu nhiệm vụ / kết quả của cấp trên) quá
/// <see cref="AttachmentOptions.CleanupRetentionHours"/> giờ: xóa bản ghi (xóa mềm, ghi nhật ký) rồi xóa tệp trên kho lưu trữ.
/// Chỉ dọn nhóm tệp mà mọi phiên bản đều chưa gắn, không phiên bản nào được tham chiếu và đều quá hạn.
/// </summary>
public sealed class UnlinkedAttachmentCleaner : IUnlinkedAttachmentCleaner
{
    /// <summary>Số nhóm tệp tối đa mỗi lượt.</summary>
    public const int BatchGroups = 500;

    private readonly IUnlinkedAttachmentRepository _repo;
    private readonly IFileStorageService _storage;
    private readonly AttachmentOptions _options;
    private readonly ILogger<UnlinkedAttachmentCleaner> _logger;

    /// <summary>Khởi tạo dịch vụ dọn tệp.</summary>
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

    /// <summary>
    /// Chọn các phiên bản cần dọn: theo nhóm, mọi phiên bản có loại sở hữu <see cref="AttachmentOwnerTypes.Unlinked"/>, không phiên bản nào
    /// nằm trong <paramref name="referencedIds"/> và phiên bản mới nhất tải lên trước <paramref name="cutoff"/>.
    /// </summary>
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
