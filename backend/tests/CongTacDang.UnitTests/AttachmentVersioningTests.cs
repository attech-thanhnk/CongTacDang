using System.Linq.Expressions;
using System.Text;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Kiểm thử mô hình tệp theo đối tượng, phiên bản, xóa mềm và kiểm tra quyền (T-36, T-47, T-42).</summary>
public class AttachmentVersioningTests
{
    private static readonly Guid CellA = Guid.NewGuid();
    private static readonly Guid CellB = Guid.NewGuid();

    private readonly World _world = new();

    [Fact]
    public async Task Upload_ToOwnTask_SetsOwnerAndRecordLink()
    {
        var owner = _world.Cadre(CellA);
        var (record, task) = _world.RecordWithTask(owner);

        var dto = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "minh-chung.pdf", 10, "MAU02", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationTask, task.Id);

        var stored = _world.Files.Items.Single();
        Assert.Equal(AttachmentOwnerTypes.EvaluationTask, stored.OwnerType);
        Assert.Equal(task.Id, stored.OwnerId);
        Assert.Equal(record.Id, stored.RecordId);
        Assert.Equal(1, dto.VersionNumber);
        Assert.True(dto.IsCurrent);
        Assert.Equal(dto.Id, dto.FileGroupId);
    }

    [Fact]
    public async Task Upload_ToSomeoneElsesRecord_IsForbidden()
    {
        var owner = _world.Cadre(CellA);
        var (record, _) = _world.RecordWithTask(owner);
        var other = _world.Cadre(CellA);

        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.UploadAttachmentAsync(
            Pdf("x"), "x.pdf", 1, "MAU02", "", "B", other.Id, AttachmentOwnerTypes.EvaluationRecord, record.Id));
        Assert.Empty(_world.Files.Items);
        Assert.Empty(_world.Storage.Objects);
    }

    [Fact]
    public async Task Upload_InvalidOwnerType_IsRejected()
    {
        var owner = _world.Cadre(CellA);
        await Assert.ThrowsAsync<ArgumentException>(() => _world.Service.UploadAttachmentAsync(
            Pdf("x"), "x.pdf", 1, "MAU02", "", "A", owner.Id, "Unknown", Guid.NewGuid()));
    }

    [Fact]
    public async Task Replace_CreatesNewVersion_KeepsOldVersion()
    {
        var owner = _world.Cadre(CellA);
        var (_, task) = _world.RecordWithTask(owner);
        var v1 = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "a.pdf", 10, "MAU02", "mô tả", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationTask, task.Id);

        var v2 = await _world.Service.ReplaceAttachmentAsync(v1.Id, Pdf("v2"), "b.pdf", 10, "A", owner.Id);

        Assert.Equal(2, v2.VersionNumber);
        Assert.True(v2.IsCurrent);
        Assert.Equal(v1.Id, v2.FileGroupId);
        Assert.Equal("mô tả", v2.Description);

        var old = _world.Files.Items.Single(a => a.Id == v1.Id);
        Assert.True(old.IsSuperseded);
        Assert.Equal(owner.Id, old.SupersededById);
        Assert.NotNull(old.SupersededAt);

        var history = await _world.Service.GetVersionsAsync(v1.Id, owner.Id);
        Assert.Equal(new[] { 2, 1 }, history.Select(h => h.VersionNumber));
        Assert.Equal(new[] { true, false }, history.Select(h => h.IsCurrent));

        // Id của phiên bản nào cũng tải về phiên bản hiện hành; phiên bản cũ tải được theo số.
        Assert.Equal("%PDF v2", await ReadAsync(await _world.Service.DownloadAttachmentAsync(v1.Id, owner.Id)));
        Assert.Equal("%PDF v1", await ReadAsync(await _world.Service.DownloadVersionAsync(v2.Id, 1, owner.Id)));

        // Danh sách của đối tượng chỉ có phiên bản hiện hành.
        var listed = await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationTask, task.Id, owner.Id);
        Assert.Equal(v2.Id, Assert.Single(listed).Id);
    }

    [Fact]
    public async Task Replace_WithoutUpdateRight_IsForbidden()
    {
        var owner = _world.Cadre(CellA);
        var (_, task) = _world.RecordWithTask(owner);
        var v1 = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "a.pdf", 10, "MAU02", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationTask, task.Id);

        // Bí thư cùng Chi bộ được xem nhưng không được thay tệp của cán bộ.
        var secretary = _world.Secretary(CellA);
        Assert.Single(await _world.Service.GetVersionsAsync(v1.Id, secretary.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _world.Service.ReplaceAttachmentAsync(v1.Id, Pdf("v2"), "b.pdf", 10, "S", secretary.Id));
        Assert.Single(_world.Files.Items);
    }

    [Fact]
    public async Task Versions_AndDownloads_FollowRecordScope()
    {
        var owner = _world.Cadre(CellA);
        var (record, _) = _world.RecordWithTask(owner);
        var v1 = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "a.pdf", 10, "MAU02", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationRecord, record.Id);

        var otherSecretary = _world.Secretary(CellB);
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.GetVersionsAsync(v1.Id, otherSecretary.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.DownloadVersionAsync(v1.Id, 1, otherSecretary.Id));
        Assert.Empty(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationRecord, record.Id, otherSecretary.Id));
        Assert.Single(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationRecord, record.Id, _world.Secretary(CellA).Id));
    }

    [Fact]
    public async Task Delete_SoftDeletesAllVersions()
    {
        var owner = _world.Cadre(CellA);
        var (_, task) = _world.RecordWithTask(owner);
        var v1 = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "a.pdf", 10, "MAU02", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationTask, task.Id);
        await _world.Service.ReplaceAttachmentAsync(v1.Id, Pdf("v2"), "b.pdf", 10, "A", owner.Id);

        await _world.Service.DeleteAttachmentAsync(v1.Id, owner.Id);

        Assert.All(_world.Files.Items, a => Assert.True(a.IsDeleted));
        Assert.Equal(2, _world.Storage.Objects.Count); // File vật lý được giữ để khôi phục.
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _world.Service.DownloadAttachmentAsync(v1.Id, owner.Id));
        Assert.Empty(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationTask, task.Id, owner.Id));
    }

    [Fact]
    public async Task Delete_BySomeoneElse_IsForbidden()
    {
        var owner = _world.Cadre(CellA);
        var v1 = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "a.pdf", 10, "MAU02", "", "A", owner.Id);

        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.DeleteAttachmentAsync(v1.Id, _world.Cadre(CellA).Id));
        Assert.False(_world.Files.Items.Single().IsDeleted);
    }

    [Fact]
    public async Task LinkCheck_RequiresExistingFileWithUpdateRight()
    {
        var owner = _world.Cadre(CellA);
        var own = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "a.pdf", 10, "MAU02", "", "A", owner.Id);
        var other = _world.Cadre(CellA);
        var foreign = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "b.pdf", 10, "MAU02", "", "B", other.Id);

        await _world.Service.EnsureCanLinkAttachmentsAsync(new[] { own.Id }, owner.Id);
        await Assert.ThrowsAsync<ValidationException>(() =>
            _world.Service.EnsureCanLinkAttachmentsAsync(new[] { Guid.NewGuid() }, owner.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _world.Service.EnsureCanLinkAttachmentsAsync(new[] { own.Id, foreign.Id }, owner.Id));
    }

    #region Hỗ trợ

    private static MemoryStream Pdf(string content) => new(Encoding.ASCII.GetBytes("%PDF " + content));

    private static async Task<string> ReadAsync(AttachmentDownloadResult result)
    {
        using var reader = new StreamReader(result.Stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class World
    {
        public InMemoryFiles Files { get; } = new();
        public InMemoryStorage Storage { get; } = new();
        public Dictionary<Guid, PartyMemberProfile> Users { get; } = new();
        public Dictionary<Guid, EvaluationRecord> Records { get; } = new();
        public Dictionary<Guid, EvaluationTask> Tasks { get; } = new();
        public Dictionary<Guid, List<PermissionGrant>> Grants { get; } = new();
        public AttachmentService Service { get; }

        public World()
        {
            var reader = new AccessReader(this);
            // Quyền lấy từ bản gán có phạm vi (IPermissionResolver).
            Service = new AttachmentService(Files, Storage, reader, new Resolver(this), Files);
        }

        private PartyMemberProfile User(Guid? cellId, params PermissionGrant[] grants)
        {
            var user = new PartyMemberProfile { Id = Guid.NewGuid(), PartyCellId = cellId };
            Users[user.Id] = user;
            Grants[user.Id] = grants.ToList();
            return user;
        }

        /// <summary>Cán bộ: "Người được đánh giá" (evaluation.self, Toàn công ty).</summary>
        public PartyMemberProfile Cadre(Guid? cellId) =>
            User(cellId, new PermissionGrant(PermissionCodes.EvaluationSelf, ScopeType.Global, null, Guid.NewGuid(), "Người được đánh giá"));

        /// <summary>Bí thư Chi bộ: evaluation.self + evaluation.read phạm vi Chi bộ của mình.</summary>
        public PartyMemberProfile Secretary(Guid? cellId) =>
            User(cellId,
                new PermissionGrant(PermissionCodes.EvaluationSelf, ScopeType.Global, null, Guid.NewGuid(), "Người được đánh giá"),
                new PermissionGrant(PermissionCodes.EvaluationRead, ScopeType.PartyCell, cellId, Guid.NewGuid(), "Chi ủy / Bí thư Chi bộ"));

        public (EvaluationRecord Record, EvaluationTask Task) RecordWithTask(PartyMemberProfile owner)
        {
            var record = new EvaluationRecord { Id = Guid.NewGuid(), MemberId = owner.Id, Member = owner, PartyCellId = owner.PartyCellId };
            var task = new EvaluationTask { Id = Guid.NewGuid(), RecordId = record.Id, Record = record };
            record.Tasks.Add(task);
            Records[record.Id] = record;
            Tasks[task.Id] = task;
            return (record, task);
        }
    }

    private sealed class AccessReader : IAttachmentAccessReader
    {
        private readonly World _world;
        public AccessReader(World world) => _world = world;

        public Task<Dictionary<Guid, List<AttachmentRecordLink>>> GetRecordLinksAsync(IReadOnlyCollection<TaskAttachment> attachments)
        {
            var result = new Dictionary<Guid, List<AttachmentRecordLink>>();
            foreach (var a in attachments)
            {
                var links = new List<AttachmentRecordLink>();
                if (a.RecordId.HasValue && _world.Records.TryGetValue(a.RecordId.Value, out var record))
                    links.Add(new AttachmentRecordLink(record, AttachmentLinkKind.Record));
                foreach (var task in _world.Tasks.Values.Where(t => t.AttachmentId.HasValue
                    && _world.Files.Items.Any(f => f.Id == t.AttachmentId && f.GroupId == a.GroupId)))
                {
                    links.Add(new AttachmentRecordLink(_world.Records[task.RecordId], AttachmentLinkKind.Task));
                }
                if (links.Count > 0)
                    result[a.Id] = links;
            }
            return Task.FromResult(result);
        }

        public Task<EvaluationRecord?> GetOwnerRecordAsync(string ownerType, Guid ownerId)
        {
            EvaluationRecord? record = ownerType switch
            {
                AttachmentOwnerTypes.EvaluationRecord => _world.Records.GetValueOrDefault(ownerId),
                AttachmentOwnerTypes.EvaluationTask => _world.Tasks.TryGetValue(ownerId, out var t) ? _world.Records[t.RecordId] : null,
                _ => null
            };
            return Task.FromResult(record);
        }
    }

    private sealed class Resolver : IPermissionResolver
    {
        private readonly World _world;
        public Resolver(World world) => _world = world;

        public Task<EffectivePermissions> GetAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_world.Grants.TryGetValue(userId, out var grants)
                ? new EffectivePermissions(userId, grants)
                : EffectivePermissions.Empty(userId));
    }

    private sealed class Users : IUserRepository
    {
        private readonly World _world;
        public Users(World world) => _world = world;

        public Task<PartyMemberProfile?> GetWithOrganizationByIdAsync(Guid id) => Task.FromResult(_world.Users.GetValueOrDefault(id));
        public Task<PartyMemberProfile?> GetByIdAsync(Guid id) => Task.FromResult(_world.Users.GetValueOrDefault(id));
        public Task<PartyMemberProfile?> GetByUsernameAsync(string username) => throw new NotSupportedException();
        public Task<PartyMemberProfile?> GetFirstMemberAsync() => throw new NotSupportedException();
        public Task<List<PartyMemberProfile>> GetAllWithDetailsAsync() => throw new NotSupportedException();
        public Task<List<PartyMemberProfile>> ListAsync() => throw new NotSupportedException();
        public Task<List<PartyMemberProfile>> FindAsync(Expression<Func<PartyMemberProfile, bool>> predicate) => throw new NotSupportedException();
        public Task AddAsync(PartyMemberProfile entity) => throw new NotSupportedException();
        public Task UpdateAsync(PartyMemberProfile entity) => throw new NotSupportedException();
        public Task DeleteAsync(PartyMemberProfile entity) => throw new NotSupportedException();
    }

    /// <summary>Kho metadata tệp trong bộ nhớ, mô phỏng query filter xóa mềm của DbContext.</summary>
    private sealed class InMemoryFiles : IAttachmentRepository, IAttachmentVersionRepository
    {
        public List<TaskAttachment> Items { get; } = new();

        private IEnumerable<TaskAttachment> Visible => Items.Where(a => !a.IsDeleted);

        public Task<TaskAttachment?> GetByIdAsync(Guid id) => Task.FromResult(Visible.FirstOrDefault(a => a.Id == id));
        public Task<List<TaskAttachment>> ListAsync() => Task.FromResult(Visible.ToList());
        public Task<List<TaskAttachment>> FindAsync(Expression<Func<TaskAttachment, bool>> predicate) =>
            Task.FromResult(Visible.Where(predicate.Compile()).ToList());
        public Task AddAsync(TaskAttachment entity) { Items.Add(entity); return Task.CompletedTask; }
        public Task UpdateAsync(TaskAttachment entity) => Task.CompletedTask;
        public Task DeleteAsync(TaskAttachment entity) { entity.IsDeleted = true; return Task.CompletedTask; }
        public Task<List<TaskAttachment>> GetAllAttachmentsAsync() => Task.FromResult(Visible.Where(a => !a.IsSuperseded).ToList());

        public Task<List<TaskAttachment>> GetCurrentByOwnerAsync(string ownerType, Guid ownerId) =>
            Task.FromResult(Visible.Where(a => !a.IsSuperseded && a.EffectiveOwnerType == ownerType && a.EffectiveOwnerId == ownerId).ToList());

        public Task<TaskAttachment?> GetCurrentVersionAsync(Guid anyVersionId)
        {
            var version = Visible.FirstOrDefault(a => a.Id == anyVersionId);
            return Task.FromResult(version == null ? null : Visible.FirstOrDefault(a => a.GroupId == version.GroupId && !a.IsSuperseded));
        }

        public Task<List<TaskAttachment>> GetVersionsAsync(Guid groupId) =>
            Task.FromResult(Visible.Where(a => a.GroupId == groupId).OrderBy(a => a.VersionNumber).ToList());

        public Task AddVersionAsync(TaskAttachment previous, TaskAttachment next)
        {
            var stored = Items.Single(a => a.Id == previous.Id);
            stored.IsSuperseded = previous.IsSuperseded;
            stored.SupersededAt = previous.SupersededAt;
            stored.SupersededById = previous.SupersededById;
            stored.FileGroupId = previous.FileGroupId;
            Items.Add(next);
            return Task.CompletedTask;
        }

        public Task SoftDeleteGroupAsync(Guid groupId)
        {
            foreach (var a in Items.Where(a => a.GroupId == groupId))
                a.IsDeleted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryStorage : IFileStorageService
    {
        public Dictionary<string, byte[]> Objects { get; } = new();

        public async Task<string> SaveFileAsync(Stream fileStream, string objectKey, string contentType = "application/octet-stream")
        {
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            Objects[objectKey] = ms.ToArray();
            return objectKey;
        }

        public Task<Stream?> GetFileStreamAsync(string objectKey) =>
            Task.FromResult<Stream?>(Objects.TryGetValue(objectKey, out var bytes) ? new MemoryStream(bytes) : null);

        public Task DeleteFileAsync(string objectKey) { Objects.Remove(objectKey); return Task.CompletedTask; }
        public bool FileExists(string objectKey) => Objects.ContainsKey(objectKey);
        public Task<string?> GetDownloadUrlAsync(Guid attachmentId, TimeSpan? expiry = null) => Task.FromResult<string?>(null);
    }

    #endregion
}
