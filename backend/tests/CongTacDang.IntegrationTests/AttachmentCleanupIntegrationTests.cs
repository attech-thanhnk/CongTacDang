using System.Text;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CongTacDang.IntegrationTests;

/// <summary>Đếm tệp chờ gắn và dọn tệp tải lên chưa gắn quá hạn trên PostgreSQL thật (truy vấn của repository).</summary>
[Collection(ApiCollection.Name)]
public sealed class AttachmentCleanupIntegrationTests
{
    private readonly ApiFactory _factory;

    public AttachmentCleanupIntegrationTests(ApiFactory factory) => _factory = factory;

    [SkippableFact]
    public async Task PendingCount_And_Cleanup_RemoveOnlyExpiredUnreferencedUnlinkedFiles()
    {
        Skip.If(_factory.SkipReason != null, _factory.SkipReason);
        var sfx = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var user = await _factory.CreateUserAsync();
        var storage = _factory.Services.GetRequiredService<IFileStorageService>();
        var old = DateTime.UtcNow.AddHours(-48);

        async Task<TaskAttachment> FileAsync(string name, DateTime uploadedAt, string? ownerType = AttachmentOwnerTypes.Unlinked,
            Guid? ownerId = null, Guid? groupId = null, int version = 1, bool superseded = false)
        {
            var id = Guid.NewGuid();
            var key = await storage.SaveFileAsync(new MemoryStream(Encoding.ASCII.GetBytes("%PDF " + name)), $"it/{sfx}/{id:N}.pdf", "application/pdf");
            return new TaskAttachment
            {
                Id = id, FileName = name, OriginalFileName = name, ObjectKey = key, ContentType = "application/pdf", FileSize = 9,
                FormCode = "MAU02", UploadedById = user.Id, UploadedAt = uploadedAt, OwnerType = ownerType, OwnerId = ownerId,
                RecordId = ownerType == AttachmentOwnerTypes.EvaluationRecord ? ownerId : null,
                FileGroupId = groupId ?? id, VersionNumber = version, IsSuperseded = superseded
            };
        }

        var expired = await FileAsync("expired.pdf", old);
        var referenced = await FileAsync("referenced.pdf", old);
        var recent = await FileAsync("recent.pdf", DateTime.UtcNow);
        // Nhóm hai phiên bản: phiên bản cũ được nhiệm vụ tham chiếu, phiên bản hiện hành chưa gắn → cả nhóm còn được dùng.
        var groupV1 = await FileAsync("v1.pdf", old, superseded: true);
        var groupV2 = await FileAsync("v2.pdf", old, groupId: groupV1.Id, version: 2);

        await _factory.WithDbAsync(async db =>
        {
            var cell = new PartyCell { Code = $"AC-{sfx}", Name = $"Chi bộ AC {sfx}" };
            cell.Path = OrgTree.BuildPath(null, cell.Id);
            var period = new EvaluationPeriod
            {
                Year = 2100 + Random.Shared.Next(0, 800), Quarter = EvaluationQuarter.Quy1, Name = $"Kỳ AC {sfx}",
                StartDate = DateTime.UtcNow.AddDays(-30), EndDate = DateTime.UtcNow.AddDays(30), Status = PeriodStatus.Open,
                CriteriaSnapshot = CriteriaTestData.Snapshot(collectiveFramework: false)
            };
            var record = new EvaluationRecord
            {
                Period = period, MemberId = user.Id, PartyCellId = cell.Id, ApprovalAuthority = ApprovalAuthority.CoSo,
                Status = RecordStatus.AwaitingSelfScore, WeightFrameCode = "K2"
            };
            db.AddRange(cell, period, record, expired, referenced, recent, groupV1, groupV2);
            db.Add(await FileAsync("on-record.pdf", old, AttachmentOwnerTypes.EvaluationRecord, record.Id));
            db.Add(new EvaluationTask { Record = record, TaskOrder = 1, TaskName = "Nhiệm vụ", AttachmentId = referenced.Id, Deadline = DateTime.UtcNow });
            db.Add(new EvaluationTask { Record = record, TaskOrder = 2, TaskName = "Nhiệm vụ 2", AttachmentId = groupV1.Id, Deadline = DateTime.UtcNow });
            await db.SaveChangesAsync();
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IUnlinkedAttachmentRepository>();
            // Chờ gắn: expired, recent (referenced và nhóm v1/v2 đã gắn qua nhiệm vụ; tệp gắn hồ sơ không tính).
            Assert.Equal(2, await repo.CountPendingUnlinkedAsync(user.Id));

            var candidates = await repo.GetCleanupCandidatesAsync(DateTime.UtcNow.AddHours(-24), 10_000);
            Assert.Contains(candidates, c => c.Id == expired.Id);
            Assert.DoesNotContain(candidates, c => c.Id == referenced.Id || c.Id == recent.Id || c.GroupId == groupV1.Id);

            await scope.ServiceProvider.GetRequiredService<IUnlinkedAttachmentCleaner>().CleanupAsync(DateTime.UtcNow);
        }

        await _factory.WithDbAsync(async db =>
        {
            var remaining = await db.TaskAttachments.Where(a => a.UploadedById == user.Id).Select(a => a.Id).ToListAsync();
            Assert.DoesNotContain(expired.Id, remaining);
            Assert.Contains(referenced.Id, remaining);
            Assert.Contains(recent.Id, remaining);
            Assert.Contains(groupV1.Id, remaining);
            Assert.Contains(groupV2.Id, remaining);
            Assert.Equal(5, remaining.Count);
            // Bản ghi xóa mềm (có nhật ký), tệp trên đĩa đã xóa.
            Assert.True((await db.TaskAttachments.IgnoreQueryFilters().SingleAsync(a => a.Id == expired.Id)).IsDeleted);
        });
        Assert.False(storage.FileExists(expired.ObjectKey));
        Assert.True(storage.FileExists(referenced.ObjectKey));
        Assert.True(storage.FileExists(recent.ObjectKey));
    }
}
