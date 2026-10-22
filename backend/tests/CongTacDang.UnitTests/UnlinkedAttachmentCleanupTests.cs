using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Chọn và dọn tệp tải lên chưa gắn đối tượng quá hạn.</summary>
public sealed class UnlinkedAttachmentCleanupTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Cutoff = Now.AddHours(-24);

    private static UnlinkedAttachmentVersion V(Guid group, string ownerType, DateTime uploadedAt, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), group, ownerType, uploadedAt, $"mau02/202610/{Guid.NewGuid():N}.pdf");

    [Fact]
    public void Select_OnlyGroupsFullyUnlinkedUnreferencedAndExpired()
    {
        var oldUnlinked = Guid.NewGuid();
        var recent = Guid.NewGuid();
        var referenced = Guid.NewGuid();
        var mixed = Guid.NewGuid();
        var oldVersionsOneRecent = Guid.NewGuid();
        var referencedId = Guid.NewGuid();
        var versions = new[]
        {
            V(oldUnlinked, AttachmentOwnerTypes.Unlinked, Cutoff.AddHours(-5)),
            V(oldUnlinked, AttachmentOwnerTypes.Unlinked, Cutoff.AddHours(-1)),
            V(recent, AttachmentOwnerTypes.Unlinked, Cutoff.AddMinutes(1)),
            // Nhiệm vụ trỏ tới phiên bản cũ của nhóm → cả nhóm còn được dùng.
            V(referenced, AttachmentOwnerTypes.Unlinked, Cutoff.AddHours(-3), referencedId),
            V(referenced, AttachmentOwnerTypes.Unlinked, Cutoff.AddHours(-2)),
            V(mixed, AttachmentOwnerTypes.Unlinked, Cutoff.AddHours(-3)),
            V(mixed, AttachmentOwnerTypes.EvaluationTask, Cutoff.AddHours(-2)),
            V(Guid.NewGuid(), AttachmentOwnerTypes.CollectiveEvaluationRecord, Cutoff.AddDays(-30)),
            V(oldVersionsOneRecent, AttachmentOwnerTypes.Unlinked, Cutoff.AddDays(-3)),
            V(oldVersionsOneRecent, AttachmentOwnerTypes.Unlinked, Now)
        };

        var selected = UnlinkedAttachmentCleaner.SelectForDeletion(versions, new HashSet<Guid> { referencedId }, Cutoff);

        Assert.Equal(2, selected.Count);
        Assert.All(selected, v => Assert.Equal(oldUnlinked, v.GroupId));
    }

    [Fact]
    public async Task Cleanup_DeletesRecordsThenFiles_SkipsReferenced_DisabledWhenRetentionZero()
    {
        var expired = V(Guid.NewGuid(), AttachmentOwnerTypes.Unlinked, Cutoff.AddHours(-1));
        var linkedMeanwhile = V(Guid.NewGuid(), AttachmentOwnerTypes.Unlinked, Cutoff.AddHours(-1));
        var repo = new FakeRepo(new List<UnlinkedAttachmentVersion> { expired, linkedMeanwhile }, new HashSet<Guid> { linkedMeanwhile.Id });
        var storage = new FakeStorage(expired.ObjectKey, linkedMeanwhile.ObjectKey);

        var cleaner = new UnlinkedAttachmentCleaner(repo, storage, new AttachmentOptions { CleanupRetentionHours = 24 });
        Assert.Equal(1, await cleaner.CleanupAsync(Now));
        Assert.Equal(new[] { expired.Id }, repo.Deleted);
        Assert.DoesNotContain(expired.ObjectKey, storage.Keys);
        Assert.Contains(linkedMeanwhile.ObjectKey, storage.Keys);
        Assert.Equal(Cutoff, repo.LastCutoff);

        var disabled = new UnlinkedAttachmentCleaner(repo, storage, new AttachmentOptions { CleanupRetentionHours = 0 });
        Assert.Equal(0, await disabled.CleanupAsync(Now));
    }

    private sealed class FakeRepo(List<UnlinkedAttachmentVersion> candidates, HashSet<Guid> referenced) : IUnlinkedAttachmentRepository
    {
        public List<Guid> Deleted { get; } = new();
        public DateTime? LastCutoff { get; private set; }

        public Task<int> CountPendingUnlinkedAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(0);

        public Task<List<UnlinkedAttachmentVersion>> GetCleanupCandidatesAsync(DateTime uploadedBefore, int maxGroups, CancellationToken ct = default)
        {
            LastCutoff = uploadedBefore;
            return Task.FromResult(candidates.Where(c => !Deleted.Contains(c.Id)).ToList());
        }

        public Task<HashSet<Guid>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default) =>
            Task.FromResult(attachmentIds.Where(referenced.Contains).ToHashSet());

        public Task DeleteAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default)
        {
            Deleted.AddRange(attachmentIds);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStorage(params string[] keys) : IFileStorageService
    {
        public HashSet<string> Keys { get; } = keys.ToHashSet();
        public Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream") =>
            throw new NotSupportedException();
        public Task<Stream?> GetFileStreamAsync(string objectKey) => throw new NotSupportedException();
        public Task DeleteFileAsync(string objectKey) { Keys.Remove(objectKey); return Task.CompletedTask; }
        public bool FileExists(string objectKey) => Keys.Contains(objectKey);
        public Task<string?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan? expiry = null) => throw new NotSupportedException();
    }
}
