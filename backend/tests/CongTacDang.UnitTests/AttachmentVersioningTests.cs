using System.Linq.Expressions;
using System.Text;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Tệp theo đối tượng, phiên bản, tệp chưa gắn và kiểm tra quyền.</summary>
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

    /// <summary>Minh chứng chỉ chủ hồ sơ và người có <c>evaluation.read</c> trong phạm vi bao trùm hồ sơ xem được.</summary>
    [Fact]
    public async Task Evidence_ReadableOnlyByOwnerAndInScopeReaders()
    {
        var owner = _world.Cadre(CellA);
        var (record, task) = _world.RecordWithTask(owner);
        record.DepartmentId = Guid.NewGuid();
        var onRecord = await _world.Service.UploadAttachmentAsync(Pdf("r"), "r.pdf", 10, "MAU02", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationRecord, record.Id);
        var onTask = await _world.Service.UploadAttachmentAsync(Pdf("t"), "t.pdf", 10, "MAU02", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationTask, task.Id);
        var ids = new[] { onRecord.Id, onTask.Id };

        // Được xem: chủ hồ sơ (không cần evaluation.read), Bí thư cùng Chi bộ, người có evaluation.read Toàn công ty,
        // người có evaluation.read phạm vi đơn vị chính quyền của hồ sơ.
        var deptReader = _world.UserWithScope(PermissionCodes.EvaluationRead, ScopeType.Department, record.DepartmentId);
        foreach (var reader in new[] { owner, _world.Secretary(CellA), _world.UserWith(PermissionCodes.EvaluationRead), deptReader })
        {
            var visible = (await _world.Service.GetAttachmentsAsync(reader.Id)).Select(a => a.Id).ToList();
            Assert.All(ids, id => Assert.Contains(id, visible));
            foreach (var id in ids)
            {
                Assert.NotNull(await _world.Service.GetAttachmentByIdAsync(id, reader.Id));
                Assert.NotNull(await _world.Service.DownloadAttachmentAsync(id, reader.Id));
                Assert.Single(await _world.Service.GetVersionsAsync(id, reader.Id));
            }
        }

        // Không được xem: cán bộ khác (chỉ evaluation.self), Bí thư Chi bộ khác, evaluation.read phạm vi đơn vị khác,
        // tài khoản chỉ có quyền quản trị hệ thống.
        var outsiders = new[]
        {
            _world.Cadre(CellA),
            _world.Secretary(CellB),
            _world.UserWithScope(PermissionCodes.EvaluationRead, ScopeType.Department, Guid.NewGuid()),
            _world.UserWith(PermissionCodes.SystemUsersManage, PermissionCodes.CatalogManage),
        };
        foreach (var outsider in outsiders)
        {
            var visible = (await _world.Service.GetAttachmentsAsync(outsider.Id)).Select(a => a.Id).ToList();
            Assert.All(ids, id => Assert.DoesNotContain(id, visible));
            Assert.Empty(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationRecord, record.Id, outsider.Id));
            Assert.Empty(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationTask, task.Id, outsider.Id));
            foreach (var id in ids)
            {
                await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.GetAttachmentByIdAsync(id, outsider.Id));
                await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.DownloadAttachmentAsync(id, outsider.Id));
                await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.DownloadVersionAsync(id, 1, outsider.Id));
                await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.GetVersionsAsync(id, outsider.Id));
            }
        }
    }

    [Fact]
    public async Task Unlinked_VisibleOnlyToUploader_UntilLinked()
    {
        var owner = _world.Cadre(CellA);
        var v1 = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "a.pdf", 10, "MAU02", "", "A", owner.Id);
        Assert.Equal(AttachmentOwnerTypes.Unlinked, _world.Files.Items.Single().OwnerType);

        // Không có tệp dùng chung: người khác (kể cả Bí thư cùng Chi bộ) không thấy, không tải, không thay.
        var other = _world.Cadre(CellA);
        var secretary = _world.Secretary(CellA);
        Assert.Contains(await _world.Service.GetAttachmentsAsync(owner.Id), a => a.Id == v1.Id);
        Assert.Empty(await _world.Service.GetAttachmentsAsync(other.Id));
        Assert.Empty(await _world.Service.GetAttachmentsAsync(secretary.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.DownloadAttachmentAsync(v1.Id, secretary.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.GetAttachmentByIdAsync(v1.Id, other.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _world.Service.ReplaceAttachmentAsync(v1.Id, Pdf("v2"), "b.pdf", 10, "B", other.Id));

        // Gắn vào nhiệm vụ của chủ hồ sơ → Bí thư cùng Chi bộ xem được theo quyền trên hồ sơ.
        var (_, task) = _world.RecordWithTask(owner);
        task.AttachmentId = v1.Id;
        Assert.Contains(await _world.Service.GetAttachmentsAsync(secretary.Id), a => a.Id == v1.Id);
        Assert.Empty(await _world.Service.GetAttachmentsAsync(other.Id));
    }

    [Fact]
    public async Task Upload_Unlinked_LimitedPerUser_LinkedFilesDoNotCount()
    {
        var pending = new PendingCounter(_world);
        var service = new AttachmentService(_world.Files, _world.Storage, new AccessReader(_world), new Resolver(_world), _world.Files,
            pending, new AttachmentOptions { MaxPendingUnlinkedPerUser = 2 });
        var owner = _world.Cadre(CellA);
        var first = await service.UploadAttachmentAsync(Pdf("1"), "a.pdf", 10, "MAU02", "", "A", owner.Id);
        await service.UploadAttachmentAsync(Pdf("2"), "b.pdf", 10, "MAU02", "", "A", owner.Id);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UploadAttachmentAsync(Pdf("3"), "c.pdf", 10, "MAU02", "", "A", owner.Id));
        Assert.Contains("tối đa 2 tệp", ex.Message);
        // Người khác không bị ảnh hưởng.
        await service.UploadAttachmentAsync(Pdf("x"), "x.pdf", 10, "MAU02", "", "B", _world.Cadre(CellA).Id);

        // Tệp đã gắn vào nhiệm vụ không còn tính là chờ gắn → tải tiếp được.
        var (_, task) = _world.RecordWithTask(owner);
        task.AttachmentId = first.Id;
        await service.UploadAttachmentAsync(Pdf("3"), "c.pdf", 10, "MAU02", "", "A", owner.Id);
        // Tải thẳng vào hồ sơ của mình không bị giới hạn.
        var (record, _) = _world.RecordWithTask(owner);
        await service.UploadAttachmentAsync(Pdf("r"), "r.pdf", 10, "MAU02", "", "A", owner.Id, AttachmentOwnerTypes.EvaluationRecord, record.Id);
    }

    [Fact]
    public async Task List_IsLimited_NewestFirst()
    {
        var owner = _world.Cadre(CellA);
        for (var i = 0; i < AttachmentService.ListLimit + 5; i++)
        {
            await _world.Service.UploadAttachmentAsync(Pdf("f" + i), $"f{i}.pdf", 10, "MAU02", "", "A", owner.Id);
            _world.Files.Items[^1].UploadedAt = DateTime.UtcNow.AddMinutes(i);
        }

        var list = await _world.Service.GetAttachmentsAsync(owner.Id);
        Assert.Equal(AttachmentService.ListLimit, list.Count);
        Assert.Equal($"f{AttachmentService.ListLimit + 4}.pdf", list[0].FileName);
    }

    /// <summary>Đếm tệp chưa gắn của người tải lên theo dữ liệu của World (giống truy vấn ở repository).</summary>
    private sealed class PendingCounter(World world) : IUnlinkedAttachmentRepository
    {
        public Task<int> CountPendingUnlinkedAsync(Guid userId, CancellationToken ct = default)
        {
            var referenced = world.Tasks.Values.Where(t => t.AttachmentId.HasValue).Select(t => t.AttachmentId!.Value).ToHashSet();
            return Task.FromResult(world.Files.Items.Count(a => !a.IsDeleted && !a.IsSuperseded && a.UploadedById == userId
                && a.EffectiveOwnerType == AttachmentOwnerTypes.Unlinked && !referenced.Contains(a.Id)));
        }

        public Task<List<UnlinkedAttachmentVersion>> GetCleanupCandidatesAsync(DateTime uploadedBefore, int maxGroups, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<HashSet<Guid>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task DeleteAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Upload_Unlinked_RequiresEvidencePermission()
    {
        // Người không có evaluation.self / evaluation.external.record (VD: chỉ quản trị hệ thống) không tải tệp tự do được.
        var admin = _world.UserWith(PermissionCodes.SystemUsersManage, PermissionCodes.CatalogManage);
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _world.Service.UploadAttachmentAsync(Pdf("x"), "x.pdf", 1, "MAU02", "", "Q", admin.Id));
        Assert.Empty(_world.Files.Items);
        Assert.Empty(_world.Storage.Objects);

        // Văn bản của cấp trên: evaluation.external.record được tải tệp chưa gắn (gắn khi ghi nhận kết quả).
        var office = _world.UserWith(PermissionCodes.EvaluationExternalRecord);
        await _world.Service.UploadAttachmentAsync(Pdf("x"), "x.pdf", 1, "CAPTREN", "", "VP", office.Id);
        Assert.Single(_world.Files.Items);
    }

    [Fact]
    public async Task Upload_WithoutValidOwner_IsRejected()
    {
        var owner = _world.Cadre(CellA);
        // Loại "văn bản chung" và loại chưa gắn không nhận từ client.
        await Assert.ThrowsAsync<ArgumentException>(() => _world.Service.UploadAttachmentAsync(
            Pdf("x"), "x.pdf", 1, "MAU02", "", "A", owner.Id, "General", null));
        await Assert.ThrowsAsync<ArgumentException>(() => _world.Service.UploadAttachmentAsync(
            Pdf("x"), "x.pdf", 1, "MAU02", "", "A", owner.Id, AttachmentOwnerTypes.Unlinked, null));
        await Assert.ThrowsAsync<ArgumentException>(() => _world.Service.UploadAttachmentAsync(
            Pdf("x"), "x.pdf", 1, "MAU02", "", "A", owner.Id, AttachmentOwnerTypes.EvaluationTask, null));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _world.Service.UploadAttachmentAsync(
            Pdf("x"), "x.pdf", 1, "MAU02", "", "A", owner.Id, AttachmentOwnerTypes.EvaluationTask, Guid.NewGuid()));
        Assert.Empty(_world.Files.Items);
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

    /// <summary>Phụ lục hồ sơ tập thể: collective.manage tải lên/thay/gỡ; evaluation.read trong phạm vi chỉ xem; kỳ đóng không cập nhật.</summary>
    [Fact]
    public async Task CollectiveAttachment_FollowsCollectiveRecordPermissions()
    {
        var collective = new CollectiveAttachmentOwner(Guid.NewGuid(), null, CellA, false);
        _world.Collectives[collective.Id] = collective;
        var manager = _world.UserWithScope(PermissionCodes.CollectiveManage, ScopeType.PartyCell, CellA);
        var otherManager = _world.UserWithScope(PermissionCodes.CollectiveManage, ScopeType.PartyCell, CellA);
        var reader = _world.Secretary(CellA);
        var outsider = _world.Secretary(CellB);

        var v1 = await _world.Service.UploadAttachmentAsync(Pdf("v1"), "pl.pdf", 10, "M07", "Phụ lục", "Q", manager.Id,
            AttachmentOwnerTypes.CollectiveEvaluationRecord, collective.Id);
        var stored = _world.Files.Items.Single();
        Assert.Equal(AttachmentOwnerTypes.CollectiveEvaluationRecord, stored.OwnerType);
        Assert.Null(stored.RecordId);

        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.UploadAttachmentAsync(Pdf("x"), "x.pdf", 1, "M07", "", "S",
            reader.Id, AttachmentOwnerTypes.CollectiveEvaluationRecord, collective.Id));
        Assert.Single(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.CollectiveEvaluationRecord, collective.Id, reader.Id));
        Assert.NotNull(await _world.Service.DownloadAttachmentAsync(v1.Id, reader.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.ReplaceAttachmentAsync(v1.Id, Pdf("v2"), "b.pdf", 1, "S", reader.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.RemoveAttachmentAsync(v1.Id, reader.Id));
        Assert.Empty(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.CollectiveEvaluationRecord, collective.Id, outsider.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.DownloadAttachmentAsync(v1.Id, outsider.Id));
        await Assert.ThrowsAsync<ValidationException>(() => _world.Service.EnsureCanLinkAttachmentsAsync(new[] { v1.Id }, manager.Id));

        // Người quản lý khác cùng tổ chức thay bản mới được (tệp thuộc tập thể).
        var v2 = await _world.Service.ReplaceAttachmentAsync(v1.Id, Pdf("v2"), "b.pdf", 1, "Q2", otherManager.Id);
        Assert.Equal(2, v2.VersionNumber);

        // Kỳ đóng: chỉ xem.
        _world.Collectives[collective.Id] = collective with { PeriodClosed = true };
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.RemoveAttachmentAsync(v2.Id, manager.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.UploadAttachmentAsync(Pdf("x"), "x.pdf", 1, "M07", "", "Q",
            manager.Id, AttachmentOwnerTypes.CollectiveEvaluationRecord, collective.Id));
        Assert.NotNull(await _world.Service.DownloadAttachmentAsync(v2.Id, manager.Id));

        _world.Collectives[collective.Id] = collective;
        await _world.Service.RemoveAttachmentAsync(v2.Id, otherManager.Id);
        Assert.All(_world.Files.Items, a => Assert.True(a.IsDeleted));
    }

    /// <summary>Minh chứng theo giai đoạn: trước khi nộp tự do; sau khi nộp chỉ bổ sung; công bố hoặc kỳ khóa dữ liệu → khóa.</summary>
    [Fact]
    public async Task OwnerEvidence_FollowsRecordStage()
    {
        var owner = _world.Cadre(CellA);
        var (record, task) = _world.RecordWithTask(owner);
        record.Status = RecordStatus.AwaitingSelfScore;
        var first = await _world.Service.UploadAttachmentAsync(Pdf("1"), "a.pdf", 10, "MC-T1", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationRecord, record.Id);
        var onTask = await _world.Service.UploadAttachmentAsync(Pdf("t"), "t.pdf", 10, "MAU02", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationTask, task.Id);
        var rights = await _world.Service.GetEvidenceRightsAsync(record.Id, owner.Id);
        Assert.True(rights.CanAdd && rights.CanReplace && rights.CanRemove);
        Assert.Null(rights.Reason);

        // Đã nộp phiếu tự chấm: chỉ bổ sung tệp mới.
        record.Status = RecordStatus.AwaitingCellConfirm;
        var added = await _world.Service.UploadAttachmentAsync(Pdf("2"), "b.pdf", 10, "MC-T1", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationRecord, record.Id);
        await Assert.ThrowsAsync<ConflictException>(() => _world.Service.ReplaceAttachmentAsync(first.Id, Pdf("1b"), "a2.pdf", 10, "A", owner.Id));
        await Assert.ThrowsAsync<ConflictException>(() => _world.Service.ReplaceAttachmentAsync(onTask.Id, Pdf("t2"), "t2.pdf", 10, "A", owner.Id));
        await Assert.ThrowsAsync<ConflictException>(() => _world.Service.RemoveAttachmentAsync(added.Id, owner.Id));
        Assert.DoesNotContain(_world.Files.Items, a => a.IsDeleted || a.IsSuperseded);
        rights = await _world.Service.GetEvidenceRightsAsync(record.Id, owner.Id);
        Assert.Equal("AddOnly", rights.Stage);
        Assert.True(rights.CanAdd);
        Assert.False(rights.CanReplace || rights.CanRemove);
        Assert.NotNull(rights.Reason);
        // Người xem khác (Bí thư) không có thao tác nào; vẫn bị 403 trước khi xét giai đoạn.
        var secretary = _world.Secretary(CellA);
        var seen = await _world.Service.GetEvidenceRightsAsync(record.Id, secretary.Id);
        Assert.False(seen.CanAdd || seen.CanReplace || seen.CanRemove);
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.RemoveAttachmentAsync(added.Id, secretary.Id));

        // Trả lại về bước tự chấm: thay, gỡ lại được.
        record.Status = RecordStatus.AwaitingSelfScore;
        await _world.Service.ReplaceAttachmentAsync(first.Id, Pdf("1c"), "a3.pdf", 10, "A", owner.Id);
        await _world.Service.RemoveAttachmentAsync(added.Id, owner.Id);

        // Kỳ khóa dữ liệu / hồ sơ đã công bố: khóa minh chứng.
        record.Period.Status = PeriodStatus.Locked;
        await Assert.ThrowsAsync<ConflictException>(() => _world.Service.UploadAttachmentAsync(Pdf("3"), "c.pdf", 10, "MC-T1", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationRecord, record.Id));
        record.Period.Status = PeriodStatus.Open;
        record.Status = RecordStatus.Published;
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _world.Service.UploadAttachmentAsync(Pdf("3"), "c.pdf", 10, "MC-T1", "", "A",
            owner.Id, AttachmentOwnerTypes.EvaluationRecord, record.Id));
        Assert.Contains("kiến nghị", ex.Message);
        rights = await _world.Service.GetEvidenceRightsAsync(record.Id, owner.Id);
        Assert.False(rights.CanAdd || rights.CanReplace || rights.CanRemove);
    }

    /// <summary>Sau công bố, minh chứng bổ sung đi theo kiến nghị còn xử lý; người xử lý trong phạm vi đọc được.</summary>
    [Fact]
    public async Task AppealFiles_AddWhileOpen_ReadableByResolvers()
    {
        var owner = _world.Cadre(CellA);
        var (record, _) = _world.RecordWithTask(owner);
        record.Status = RecordStatus.Published;
        record.Period.Status = PeriodStatus.Closed;
        var appealId = Guid.NewGuid();
        _world.Appeals[appealId] = (record.Id, true);

        var file = await _world.Service.UploadAttachmentAsync(Pdf("kn"), "kn.pdf", 10, "KN", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationAppeal, appealId);
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.UploadAttachmentAsync(Pdf("x"), "x.pdf", 10, "KN", "", "B",
            _world.Cadre(CellA).Id, AttachmentOwnerTypes.EvaluationAppeal, appealId));
        await Assert.ThrowsAsync<ConflictException>(() => _world.Service.ReplaceAttachmentAsync(file.Id, Pdf("kn2"), "kn2.pdf", 10, "A", owner.Id));

        // Người xử lý kiến nghị (chỉ có evaluation.appeal.resolve trong phạm vi Chi bộ) đọc được; ngoài phạm vi thì không.
        var resolver = _world.UserWithScope(PermissionCodes.EvaluationAppealResolve, ScopeType.PartyCell, CellA);
        Assert.Single(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationAppeal, appealId, resolver.Id));
        Assert.NotNull(await _world.Service.DownloadAttachmentAsync(file.Id, resolver.Id));
        var outsider = _world.UserWithScope(PermissionCodes.EvaluationAppealResolve, ScopeType.PartyCell, CellB);
        await Assert.ThrowsAsync<ForbiddenException>(() => _world.Service.DownloadAttachmentAsync(file.Id, outsider.Id));
        // Quyền xử lý kiến nghị không cho đọc minh chứng của hồ sơ.
        Assert.Empty(await _world.Service.GetAttachmentsByOwnerAsync(AttachmentOwnerTypes.EvaluationRecord, record.Id, resolver.Id));

        _world.Appeals[appealId] = (record.Id, false);
        await Assert.ThrowsAsync<ConflictException>(() => _world.Service.UploadAttachmentAsync(Pdf("kn3"), "kn3.pdf", 10, "KN", "", "A", owner.Id,
            AttachmentOwnerTypes.EvaluationAppeal, appealId));
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
        public Dictionary<Guid, CollectiveAttachmentOwner> Collectives { get; } = new();
        /// <summary>Kiến nghị: Id → (hồ sơ, còn đang xử lý).</summary>
        public Dictionary<Guid, (Guid RecordId, bool Open)> Appeals { get; } = new();
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

        /// <summary>Người dùng có các quyền cho trước, phạm vi Toàn công ty.</summary>
        public PartyMemberProfile UserWith(params string[] codes) =>
            User(null, codes.Select(code => new PermissionGrant(code, ScopeType.Global, null, Guid.NewGuid(), "Vai trò thử")).ToArray());

        /// <summary>Người dùng có một quyền ở phạm vi cho trước.</summary>
        public PartyMemberProfile UserWithScope(string code, ScopeType scopeType, Guid? scopeId) =>
            User(null, new PermissionGrant(code, scopeType, scopeId, Guid.NewGuid(), "Vai trò thử"));

        public (EvaluationRecord Record, EvaluationTask Task) RecordWithTask(PartyMemberProfile owner)
        {
            var record = new EvaluationRecord
            {
                Id = Guid.NewGuid(), MemberId = owner.Id, Member = owner, PartyCellId = owner.PartyCellId,
                Period = new EvaluationPeriod { Status = PeriodStatus.Open }
            };
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
                AttachmentOwnerTypes.EvaluationAppeal => _world.Appeals.TryGetValue(ownerId, out var a) ? _world.Records[a.RecordId] : null,
                _ => null
            };
            return Task.FromResult(record);
        }

        public Task<bool?> IsAppealOpenAsync(Guid appealId) =>
            Task.FromResult(_world.Appeals.TryGetValue(appealId, out var a) ? a.Open : (bool?)null);

        public Task<Dictionary<Guid, CollectiveAttachmentOwner>> GetCollectiveOwnersAsync(IReadOnlyCollection<Guid> collectiveRecordIds) =>
            Task.FromResult(collectiveRecordIds
                .Where(_world.Collectives.ContainsKey)
                .Distinct()
                .ToDictionary(id => id, id => _world.Collectives[id]));
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
        public Task<List<TaskAttachment>> GetCurrentPageAsync(int skip, int take) =>
            Task.FromResult(Visible.Where(a => !a.IsSuperseded).OrderByDescending(a => a.UploadedAt).ThenByDescending(a => a.Id).Skip(skip).Take(take).ToList());

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

        public Task RemoveGroupAsync(Guid groupId)
        {
            foreach (var version in Items.Where(a => a.GroupId == groupId))
                version.IsDeleted = true;
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
