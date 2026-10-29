using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Infrastructure.Repositories;

public class UserRepository : GenericRepository<PartyMemberProfile>, IUserRepository
{
    public UserRepository(CongTacDangDbContext db) : base(db)
    {
    }

    /// <summary>Không phân biệt chữ hoa/thường.</summary>
    public async Task<PartyMemberProfile?> GetByUsernameAsync(string username)
    {
        var normalized = CongTacDang.Application.Accounts.AccountRules.NormalizeUsername(username);
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .FirstOrDefaultAsync(m => m.Username.ToLower() == normalized);
    }

    public async Task<PartyMemberProfile?> GetFirstMemberAsync()
    {
        return await _db.PartyMemberProfiles
            .AsNoTracking()
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<List<PartyMemberProfile>> GetAllWithDetailsAsync()
    {
        return await _db.PartyMemberProfiles
            .AsNoTracking()
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.FullName)
            .ToListAsync();
    }

    public async Task<PartyMemberProfile?> GetWithOrganizationByIdAsync(Guid id)
    {
        return await _db.PartyMemberProfiles
            .AsNoTracking()
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}

public class AttachmentRepository : GenericRepository<TaskAttachment>, IAttachmentRepository, IAttachmentAccessReader, IAttachmentVersionRepository,
    IUnlinkedAttachmentRepository
{
    public AttachmentRepository(CongTacDangDbContext db) : base(db)
    {
    }

    /// <summary>Chỉ phiên bản hiện hành, mới nhất trước.</summary>
    public async Task<List<TaskAttachment>> GetCurrentPageAsync(int skip, int take)
    {
        return await _db.TaskAttachments
            .AsNoTracking()
            .Where(a => !a.IsSuperseded)
            .OrderByDescending(a => a.UploadedAt)
            .ThenByDescending(a => a.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(0, take))
            .ToListAsync();
    }

    #region Tệp chưa gắn đối tượng

    /// <summary>Khớp <see cref="TaskAttachment.EffectiveOwnerType"/> = Unlinked.</summary>
    private IQueryable<TaskAttachment> UnlinkedAttachments() => _db.TaskAttachments.Where(a =>
        a.OwnerType == AttachmentOwnerTypes.Unlinked || (a.OwnerType == null && a.RecordId == null && a.RelatedId == null));

    /// <summary>Nhóm có phiên bản được nhiệm vụ (kể cả đã xóa mềm) hoặc kết quả cấp trên tham chiếu qua AttachmentId.</summary>
    private IQueryable<Guid> ReferencedGroupIds()
    {
        var taskRefs = _db.EvaluationTasks.IgnoreQueryFilters()
            .Where(t => t.AttachmentId != null)
            .Select(t => t.AttachmentId!.Value);
        var externalRefs = _db.Set<EvaluationExternalResult>().IgnoreQueryFilters()
            .Where(x => x.AttachmentId != null)
            .Select(x => x.AttachmentId!.Value);
        return _db.TaskAttachments.IgnoreQueryFilters()
            .Where(v => taskRefs.Contains(v.Id) || externalRefs.Contains(v.Id))
            .Select(v => v.FileGroupId ?? v.Id);
    }

    /// <inheritdoc />
    public Task<int> CountPendingUnlinkedAsync(Guid userId, CancellationToken ct = default)
    {
        var referenced = ReferencedGroupIds();
        return UnlinkedAttachments()
            .Where(a => a.UploadedById == userId && !a.IsSuperseded && !referenced.Contains(a.FileGroupId ?? a.Id))
            .CountAsync(ct);
    }

    /// <inheritdoc />
    public async Task<List<UnlinkedAttachmentVersion>> GetCleanupCandidatesAsync(DateTime uploadedBefore, int maxGroups, CancellationToken ct = default)
    {
        var referenced = ReferencedGroupIds();
        var linkedGroups = _db.TaskAttachments
            .Where(a => !(a.OwnerType == AttachmentOwnerTypes.Unlinked || (a.OwnerType == null && a.RecordId == null && a.RelatedId == null)))
            .Select(a => a.FileGroupId ?? a.Id);
        var recentGroups = _db.TaskAttachments
            .Where(a => a.UploadedAt >= uploadedBefore)
            .Select(a => a.FileGroupId ?? a.Id);
        var groupIds = await UnlinkedAttachments()
            .Where(a => a.UploadedAt < uploadedBefore)
            .Select(a => a.FileGroupId ?? a.Id)
            .Distinct()
            .Where(g => !linkedGroups.Contains(g) && !recentGroups.Contains(g) && !referenced.Contains(g))
            .OrderBy(g => g)
            .Take(Math.Max(0, maxGroups))
            .ToListAsync(ct);
        if (groupIds.Count == 0)
            return new List<UnlinkedAttachmentVersion>();

        var rows = await _db.TaskAttachments
            .AsNoTracking()
            .Where(a => groupIds.Contains(a.FileGroupId ?? a.Id))
            .ToListAsync(ct);
        return rows
            .Select(a => new UnlinkedAttachmentVersion(a.Id, a.GroupId, a.EffectiveOwnerType, a.UploadedAt, a.ObjectKey))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetReferencedAttachmentIdsAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default)
    {
        if (attachmentIds.Count == 0)
            return new HashSet<Guid>();
        var ids = attachmentIds.ToList();
        var fromTasks = await _db.EvaluationTasks.IgnoreQueryFilters()
            .Where(t => t.AttachmentId != null && ids.Contains(t.AttachmentId.Value))
            .Select(t => t.AttachmentId!.Value)
            .ToListAsync(ct);
        var fromExternal = await _db.Set<EvaluationExternalResult>().IgnoreQueryFilters()
            .Where(x => x.AttachmentId != null && ids.Contains(x.AttachmentId.Value))
            .Select(x => x.AttachmentId!.Value)
            .ToListAsync(ct);
        return fromTasks.Concat(fromExternal).ToHashSet();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(IReadOnlyCollection<Guid> attachmentIds, CancellationToken ct = default)
    {
        if (attachmentIds.Count == 0)
            return;
        var ids = attachmentIds.ToList();
        var rows = await _db.TaskAttachments.Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        _db.TaskAttachments.RemoveRange(rows); // xóa mềm + nhật ký trong SaveChangesAsync của DbContext
        await _db.SaveChangesAsync(ct);
    }

    #endregion

    /// <summary>Gồm cả dữ liệu cũ chưa có OwnerType.</summary>
    public async Task<List<TaskAttachment>> GetCurrentByOwnerAsync(string ownerType, Guid ownerId)
    {
        var query = _db.TaskAttachments.AsNoTracking().Where(a => !a.IsSuperseded);

        List<TaskAttachment> owned;
        if (ownerType == AttachmentOwnerTypes.EvaluationRecord)
        {
            owned = await query
                .Where(a => (a.OwnerType == ownerType && a.OwnerId == ownerId)
                    || (a.OwnerType == null && a.RecordId == ownerId))
                .ToListAsync();

            // Văn bản cấp trên gắn vào kết quả các bước do cấp trên thực hiện.
            var externalIds = await _db.Set<EvaluationExternalResult>()
                .AsNoTracking()
                .Where(x => x.RecordId == ownerId && x.AttachmentId.HasValue)
                .Select(x => x.AttachmentId!.Value)
                .ToListAsync();
            foreach (var externalId in externalIds)
            {
                var current = await GetCurrentVersionAsync(externalId);
                if (current != null && owned.All(a => a.GroupId != current.GroupId))
                    owned.Add(current);
            }
        }
        else if (ownerType == AttachmentOwnerTypes.EvaluationTask)
        {
            owned = await query
                .Where(a => (a.OwnerType == ownerType && a.OwnerId == ownerId)
                    || (a.OwnerType == null && a.RecordId == null && a.RelatedId == ownerId))
                .ToListAsync();

            // Dữ liệu cũ: gắn qua EvaluationTask.AttachmentId (có thể trỏ tới phiên bản cũ).
            var linkedId = await _db.EvaluationTasks
                .AsNoTracking()
                .Where(t => t.Id == ownerId && t.AttachmentId.HasValue)
                .Select(t => t.AttachmentId)
                .FirstOrDefaultAsync();
            if (linkedId.HasValue)
            {
                var current = await GetCurrentVersionAsync(linkedId.Value);
                if (current != null && owned.All(a => a.GroupId != current.GroupId))
                    owned.Add(current);
            }
        }
        else
        {
            owned = await query
                .Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId)
                .ToListAsync();
        }

        return owned.OrderByDescending(a => a.UploadedAt).ToList();
    }

    public async Task<TaskAttachment?> GetCurrentVersionAsync(Guid anyVersionId)
    {
        var version = await _db.TaskAttachments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == anyVersionId);
        if (version == null)
            return null;
        if (!version.IsSuperseded)
            return version;

        var groupId = version.GroupId;
        return await _db.TaskAttachments
            .AsNoTracking()
            .Where(a => (a.Id == groupId || a.FileGroupId == groupId) && !a.IsSuperseded)
            .OrderByDescending(a => a.VersionNumber)
            .FirstOrDefaultAsync();
    }

    public async Task<List<TaskAttachment>> GetVersionsAsync(Guid groupId)
    {
        return await _db.TaskAttachments
            .AsNoTracking()
            .Where(a => a.Id == groupId || a.FileGroupId == groupId)
            .OrderBy(a => a.VersionNumber)
            .ThenBy(a => a.UploadedAt)
            .ToListAsync();
    }

    /// <summary>Lưu bản mới và đánh dấu bản trước bị thay trong cùng một SaveChanges.</summary>
    public async Task AddVersionAsync(TaskAttachment previous, TaskAttachment next)
    {
        _db.TaskAttachments.Update(previous);
        await _db.TaskAttachments.AddAsync(next);
        await _db.SaveChangesAsync();
    }

    /// <summary>DbContext đổi Remove thành xóa mềm và ghi audit.</summary>
    public async Task RemoveGroupAsync(Guid groupId)
    {
        var versions = await _db.TaskAttachments.Where(a => a.Id == groupId || a.FileGroupId == groupId).ToListAsync();
        _db.TaskAttachments.RemoveRange(versions);
        await _db.SaveChangesAsync();
    }

    public async Task<EvaluationRecord?> GetOwnerRecordAsync(string ownerType, Guid ownerId)
    {
        Guid? recordId = ownerType switch
        {
            AttachmentOwnerTypes.EvaluationRecord => ownerId,
            AttachmentOwnerTypes.EvaluationTask => await _db.EvaluationTasks
                .AsNoTracking()
                .Where(t => t.Id == ownerId)
                .Select(t => (Guid?)t.RecordId)
                .FirstOrDefaultAsync(),
            // Tệp của kiến nghị thuộc hồ sơ của kiến nghị.
            AttachmentOwnerTypes.EvaluationAppeal => await _db.Set<EvaluationAppeal>()
                .AsNoTracking()
                .Where(a => a.Id == ownerId)
                .Select(a => (Guid?)a.RecordId)
                .FirstOrDefaultAsync(),
            _ => null
        };
        if (!recordId.HasValue)
            return null;

        return await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Member)
            .Include(r => r.Period)
            .FirstOrDefaultAsync(r => r.Id == recordId.Value);
    }

    /// <summary>Null nếu không tìm thấy.</summary>
    public async Task<bool?> IsAppealOpenAsync(Guid appealId)
    {
        var status = await _db.Set<EvaluationAppeal>()
            .AsNoTracking()
            .Where(a => a.Id == appealId)
            .Select(a => (AppealStatus?)a.Status)
            .FirstOrDefaultAsync();
        return status.HasValue ? status is AppealStatus.Submitted or AppealStatus.UnderReview : null;
    }

    /// <summary>Hồ sơ đã xóa mềm bị bộ lọc toàn cục loại ra.</summary>
    public async Task<Dictionary<Guid, CollectiveAttachmentOwner>> GetCollectiveOwnersAsync(IReadOnlyCollection<Guid> collectiveRecordIds)
    {
        if (collectiveRecordIds.Count == 0)
            return new Dictionary<Guid, CollectiveAttachmentOwner>();

        var ids = collectiveRecordIds.Distinct().ToList();
        var rows = await _db.CollectiveEvaluationRecords
            .AsNoTracking()
            .Where(r => ids.Contains(r.Id))
            .Select(r => new { r.Id, r.DepartmentId, r.PartyCellId, Closed = r.Period.Status == PeriodStatus.Closed })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => new CollectiveAttachmentOwner(r.Id, r.DepartmentId, r.PartyCellId, r.Closed));
    }

    /// <summary>Quy tắc xem/sửa dựng theo cùng điều kiện với service của từng phiếu; phiếu đã xóa mềm bị bộ lọc toàn cục loại ra.</summary>
    public async Task<Dictionary<Guid, FormAttachmentOwner>> GetFormOwnersAsync(string ownerType, IReadOnlyCollection<Guid> ownerIds)
    {
        var result = new Dictionary<Guid, FormAttachmentOwner>();
        if (ownerIds.Count == 0)
            return result;
        var ids = ownerIds.Distinct().ToList();

        if (ownerType == AttachmentOwnerTypes.ExpertCrosscheck)
        {
            var rows = await _db.Set<ExpertCrosscheck>().AsNoTracking()
                .Include(c => c.Record)
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();
            var open = await OpenPeriodIdsAsync(rows.Select(c => c.PeriodId));
            foreach (var c in rows)
            {
                var record = AccessTarget.ForRecord(c.Record);
                var expert = c.ExpertDepartmentId.HasValue
                    ? new AccessTarget(DepartmentId: c.ExpertDepartmentId)
                    : new AccessTarget(DepartmentId: c.DepartmentId, PartyCellId: c.PartyCellId);
                var view = new List<AttachmentPermissionRule>
                {
                    new(PermissionCodes.EvaluationRead, new[] { record }),
                    new(PermissionCodes.EvaluationCrosscheckRequest, new[] { record }),
                    new(PermissionCodes.EvaluationCrosscheckRespond, new[] { expert }),
                    new(PermissionCodes.EvaluationCrosscheckConfirm, new[] { expert })
                };
                // Cơ quan ngoài hệ thống do người yêu cầu nhập thay nên không có quyền lập riêng.
                var edit = new List<AttachmentPermissionRule> { new(PermissionCodes.EvaluationCrosscheckRequest, new[] { record }) };
                if (c.ExpertDepartmentId.HasValue)
                    edit.Add(new(PermissionCodes.EvaluationCrosscheckRespond, new[] { expert }));
                result[c.Id] = new FormAttachmentOwner(
                    c.Id, ownerType, new[] { c.Record.MemberId },
                    (c.Status is CrosscheckStatus.Requested or CrosscheckStatus.InProgress) && open.Contains(c.PeriodId),
                    view, edit);
            }
        }
        else if (ownerType == AttachmentOwnerTypes.ControlCheck)
        {
            var rows = await _db.Set<ControlCheck>().AsNoTracking()
                .Include(c => c.Records).ThenInclude(r => r.Record)
                .Where(c => ids.Contains(c.Id))
                .ToListAsync();
            var open = await OpenPeriodIdsAsync(rows.Select(c => c.PeriodId));
            foreach (var c in rows)
            {
                var unit = new AccessTarget(DepartmentId: c.DepartmentId, PartyCellId: c.PartyCellId);
                var records = c.Records.Select(r => r.Record).ToList();
                result[c.Id] = new FormAttachmentOwner(
                    c.Id, ownerType, records.Select(r => r.MemberId).ToList(),
                    c.Status == ControlCheckStatus.Draft && open.Contains(c.PeriodId),
                    ViewRules(unit, PermissionCodes.EvaluationControlPerform, PermissionCodes.EvaluationControlConfirm),
                    new List<AttachmentPermissionRule>
                    {
                        new(PermissionCodes.EvaluationControlPerform, records.Select(r => AccessTarget.ForRecord(r).ForWrite()).ToList())
                    });
            }
        }
        else if (ownerType == AttachmentOwnerTypes.CalibrationSession)
        {
            var rows = await _db.Set<CalibrationSession>().AsNoTracking()
                .Include(s => s.Records).ThenInclude(r => r.Record)
                .Where(s => ids.Contains(s.Id))
                .ToListAsync();
            var open = await OpenPeriodIdsAsync(rows.Select(s => s.PeriodId));
            foreach (var s in rows)
            {
                var unit = new AccessTarget(DepartmentId: s.DepartmentId, PartyCellId: s.PartyCellId);
                result[s.Id] = new FormAttachmentOwner(
                    s.Id, ownerType, s.Records.Select(r => r.Record.MemberId).ToList(),
                    s.Status == CalibrationStatus.Draft && open.Contains(s.PeriodId),
                    ViewRules(unit, PermissionCodes.EvaluationCalibrationManage, PermissionCodes.EvaluationCalibrationConclude),
                    new List<AttachmentPermissionRule>
                    {
                        new(PermissionCodes.EvaluationCalibrationManage, new[] { AccessTarget.ForUnitsWrite(s.DepartmentId, s.PartyCellId) })
                    });
            }
        }
        return result;
    }

    private static List<AttachmentPermissionRule> ViewRules(AccessTarget unit, params string[] permissions) =>
        permissions.Append(PermissionCodes.EvaluationRead).Select(p => new AttachmentPermissionRule(p, new[] { unit })).ToList();

    private async Task<HashSet<Guid>> OpenPeriodIdsAsync(IEnumerable<Guid> periodIds)
    {
        var ids = periodIds.Distinct().ToList();
        var open = await _db.Set<EvaluationPeriod>().AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.Status == PeriodStatus.Open)
            .Select(p => p.Id)
            .ToListAsync();
        return open.ToHashSet();
    }

    /// <summary>Qua RecordId hoặc qua nhiệm vụ.</summary>
    public async Task<Dictionary<Guid, List<AttachmentRecordLink>>> GetRecordLinksAsync(IReadOnlyCollection<TaskAttachment> attachments)
    {
        var result = new Dictionary<Guid, List<AttachmentRecordLink>>();
        if (attachments.Count == 0)
            return result;

        // EvaluationTask.AttachmentId có thể trỏ tới bất kỳ phiên bản nào (thường là bản đầu) nên nối theo nhóm phiên bản.
        var groupIds = attachments.Select(a => a.GroupId).Distinct().ToList();
        var versionToGroup = await _db.TaskAttachments
            .AsNoTracking()
            .Where(a => groupIds.Contains(a.Id) || (a.FileGroupId.HasValue && groupIds.Contains(a.FileGroupId.Value)))
            .Select(a => new { a.Id, GroupId = a.FileGroupId ?? a.Id })
            .ToListAsync();
        var groupOfVersion = versionToGroup.ToDictionary(v => v.Id, v => v.GroupId);
        foreach (var a in attachments)
            groupOfVersion[a.Id] = a.GroupId;
        var versionIds = groupOfVersion.Keys.ToList();

        var taskVersionLinks = await _db.EvaluationTasks
            .AsNoTracking()
            .Where(t => t.AttachmentId.HasValue && versionIds.Contains(t.AttachmentId.Value))
            .Select(t => new { VersionId = t.AttachmentId!.Value, t.RecordId })
            .Distinct()
            .ToListAsync();
        var taskLinks = attachments
            .SelectMany(a => taskVersionLinks
                .Where(l => groupOfVersion[l.VersionId] == a.GroupId)
                .Select(l => new { AttachmentId = a.Id, l.RecordId }))
            .Distinct()
            .ToList();

        var externalVersionLinks = await _db.Set<EvaluationExternalResult>()
            .AsNoTracking()
            .Where(x => x.AttachmentId.HasValue && versionIds.Contains(x.AttachmentId.Value))
            .Select(x => new { VersionId = x.AttachmentId!.Value, x.RecordId })
            .Distinct()
            .ToListAsync();
        var externalLinks = attachments
            .SelectMany(a => externalVersionLinks
                .Where(l => groupOfVersion[l.VersionId] == a.GroupId)
                .Select(l => new { AttachmentId = a.Id, l.RecordId }))
            .Distinct()
            .ToList();

        var explicitLinks = attachments
            .Where(a => a.RecordId.HasValue)
            .Select(a => new { AttachmentId = a.Id, RecordId = a.RecordId!.Value })
            .ToList();

        var recordIds = taskLinks.Select(x => x.RecordId)
            .Concat(explicitLinks.Select(x => x.RecordId))
            .Concat(externalLinks.Select(x => x.RecordId))
            .Distinct()
            .ToList();
        if (recordIds.Count == 0)
            return result;

        var records = await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Member)
            .Include(r => r.Period)
            .Where(r => recordIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id);

        void Add(Guid attachmentId, Guid recordId, AttachmentLinkKind kind)
        {
            if (!records.TryGetValue(recordId, out var record))
                return;
            if (!result.TryGetValue(attachmentId, out var list))
                result[attachmentId] = list = new List<AttachmentRecordLink>();
            list.Add(new AttachmentRecordLink(record, kind));
        }

        foreach (var link in explicitLinks)
            Add(link.AttachmentId, link.RecordId, AttachmentLinkKind.Record);
        foreach (var link in taskLinks)
            Add(link.AttachmentId, link.RecordId, AttachmentLinkKind.Task);
        foreach (var link in externalLinks)
            Add(link.AttachmentId, link.RecordId, AttachmentLinkKind.ExternalResult);

        return result;
    }
}

public class AuditRepository : IAuditRepository
{
    private readonly CongTacDangDbContext _db;

    public AuditRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Tổng và số người thao tác tính trên bản ghi khớp mọi bộ lọc; số đếm theo thao tác bỏ qua bộ lọc thao tác.</summary>
    public async Task<AuditLogSearchResult> SearchAuditLogsAsync(AuditLogCriteria criteria, CancellationToken ct = default)
    {
        var all = _db.AuditLogs.AsNoTracking();
        var scoped = all;
        if (!string.IsNullOrWhiteSpace(criteria.EntityType))
            scoped = scoped.Where(x => x.EntityType == criteria.EntityType);
        if (!string.IsNullOrWhiteSpace(criteria.EntityId))
            scoped = scoped.Where(x => x.EntityId == criteria.EntityId);
        var query = string.IsNullOrWhiteSpace(criteria.Action) ? scoped : scoped.Where(x => x.Action == criteria.Action);

        // Số đếm theo thao tác không áp bộ lọc thao tác, để ô số liệu và danh sách thao tác vẫn đủ khi đang lọc một loại.
        var actionCounts = await scoped
            .GroupBy(x => x.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Action, x => x.Count, ct);
        var actorCount = await query
            .Select(x => x.ActorId.HasValue ? x.ActorId.Value.ToString() : x.ActorName)
            .Distinct()
            .CountAsync(ct);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync(ct);
        var entityTypes = await all.Select(x => x.EntityType).Distinct().OrderBy(x => x).ToListAsync(ct);

        return new AuditLogSearchResult
        {
            Items = items,
            TotalCount = await query.CountAsync(ct),
            ActionCounts = actionCounts,
            ActorCount = actorCount,
            EntityTypes = entityTypes
        };
    }
}

public class OrganizationRepository : IOrganizationRepository
{
    private readonly CongTacDangDbContext _db;

    public OrganizationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    public async Task<List<PartyCell>> GetPartyCellsWithMembersAsync()
    {
        return await _db.PartyCells
            .AsNoTracking()
            .Include(c => c.Members)
            .OrderBy(c => c.Code)
            .ToListAsync();
    }

    public async Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync()
    {
        return await _db.AdministrativeDepartments
            .AsNoTracking()
            .Include(d => d.Members)
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    public async Task<PartyCell?> GetPartyCellByIdAsync(Guid id)
    {
        return await _db.PartyCells
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddPartyCellAsync(PartyCell cell)
    {
        await _db.PartyCells.AddAsync(cell);
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Update(cell);
        await _db.SaveChangesAsync();
    }

    public async Task DeletePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Remove(cell);
        await _db.SaveChangesAsync();
    }

    // ----- Danh mục -----

    public Task<List<AdministrativeDepartment>> ListDepartmentsAsync()
        => _db.AdministrativeDepartments.AsNoTracking()
            .Include(d => d.UnitType)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Code)
            .ToListAsync();

    public Task<List<PartyCell>> ListPartyCellsAsync()
        => _db.PartyCells.AsNoTracking()
            .Include(c => c.UnitType)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Code)
            .ToListAsync();

    public async Task<Dictionary<Guid, int>> CountMembersByDepartmentAsync()
    {
        var rows = await _db.PartyMemberProfiles.AsNoTracking()
            .Where(m => m.DepartmentId != null)
            .GroupBy(m => m.DepartmentId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.Count);
    }

    public async Task<Dictionary<Guid, int>> CountMembersByPartyCellAsync()
    {
        var rows = await _db.PartyMemberProfiles.AsNoTracking()
            .Where(m => m.PartyCellId != null)
            .GroupBy(m => m.PartyCellId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.Count);
    }

    public Task<AdministrativeDepartment?> FindDepartmentAsync(Guid id)
        => _db.AdministrativeDepartments.FirstOrDefaultAsync(d => d.Id == id);

    public Task<PartyCell?> FindPartyCellAsync(Guid id)
        => _db.PartyCells.FirstOrDefaultAsync(c => c.Id == id);

    public Task<List<AdministrativeDepartment>> ListDepartmentsIncludingDeletedAsync()
        => _db.AdministrativeDepartments.IgnoreQueryFilters().ToListAsync();

    public Task<List<PartyCell>> ListPartyCellsIncludingDeletedAsync()
        => _db.PartyCells.IgnoreQueryFilters().ToListAsync();

    public Task<bool> DepartmentCodeExistsAsync(string code, Guid? excludeId = null)
    {
        var normalized = code.Trim().ToUpper();
        return _db.AdministrativeDepartments.IgnoreQueryFilters()
            .AnyAsync(d => d.Code.ToUpper() == normalized && (excludeId == null || d.Id != excludeId));
    }

    public Task<bool> PartyCellCodeExistsAsync(string code, Guid? excludeId = null)
    {
        var normalized = code.Trim().ToUpper();
        return _db.PartyCells.IgnoreQueryFilters()
            .AnyAsync(c => c.Code.ToUpper() == normalized && (excludeId == null || c.Id != excludeId));
    }

    public void AddDepartment(AdministrativeDepartment department) => _db.AdministrativeDepartments.Add(department);

    public void AddPartyCell(PartyCell cell) => _db.PartyCells.Add(cell);

    public void RemoveDepartment(AdministrativeDepartment department) => _db.AdministrativeDepartments.Remove(department);

    public void RemovePartyCell(PartyCell cell) => _db.PartyCells.Remove(cell);

    public async Task<CatalogUsage> GetDepartmentUsageAsync(Guid id, DateTime now)
    {
        var members = await _db.PartyMemberProfiles.CountAsync(m => m.DepartmentId == id);
        var records = await _db.EvaluationRecords.CountAsync(r => r.DepartmentId == id);
        var collective = await _db.CollectiveEvaluationRecords.CountAsync(r => r.DepartmentId == id);
        var children = await _db.AdministrativeDepartments.CountAsync(d => d.ParentId == id);
        var assignments = await _db.Set<UserRoleAssignment>().CountAsync(a =>
            a.ScopeType == RoleScopeType.Department && a.ScopeId == id && (a.ValidTo == null || a.ValidTo > now));
        var positions = await _db.MemberPositions.CountAsync(p => p.DepartmentId == id && (p.ValidTo == null || p.ValidTo > now));
        return new CatalogUsage(members, records, collective, 0, children, assignments, positions);
    }

    public async Task<CatalogUsage> GetPartyCellUsageAsync(Guid id, DateTime now)
    {
        var members = await _db.PartyMemberProfiles.CountAsync(m => m.PartyCellId == id);
        var records = await _db.EvaluationRecords.CountAsync(r => r.PartyCellId == id);
        var collective = await _db.CollectiveEvaluationRecords.CountAsync(r => r.PartyCellId == id);
        var meetings = await _db.EvaluationMeetings.CountAsync(m => m.PartyCellId == id);
        var children = await _db.PartyCells.CountAsync(c => c.ParentId == id);
        var assignments = await _db.Set<UserRoleAssignment>().CountAsync(a =>
            a.ScopeType == RoleScopeType.PartyCell && a.ScopeId == id && (a.ValidTo == null || a.ValidTo > now));
        var positions = await _db.MemberPositions.CountAsync(p => p.PartyCellId == id && (p.ValidTo == null || p.ValidTo > now));
        return new CatalogUsage(members, records, collective, meetings, children, assignments, positions);
    }

    // ----- Cây đơn vị, loại đơn vị -----

    public async Task<Dictionary<Guid, int>> CountChildDepartmentsAsync()
    {
        var rows = await _db.AdministrativeDepartments.AsNoTracking()
            .Where(d => d.ParentId != null)
            .GroupBy(d => d.ParentId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.Count);
    }

    public async Task<Dictionary<Guid, int>> CountChildPartyCellsAsync()
    {
        var rows = await _db.PartyCells.AsNoTracking()
            .Where(c => c.ParentId != null)
            .GroupBy(c => c.ParentId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        return rows.ToDictionary(r => r.Id, r => r.Count);
    }

    public Task<List<OrgUnitType>> ListUnitTypesAsync()
        => _db.OrgUnitTypes.AsNoTracking()
            .OrderBy(t => t.Side).ThenBy(t => t.SortOrder).ThenBy(t => t.Name)
            .ToListAsync();

    public Task<OrgUnitType?> FindUnitTypeAsync(Guid id)
        => _db.OrgUnitTypes.FirstOrDefaultAsync(t => t.Id == id);

    public Task<bool> UnitTypeNameExistsAsync(OrgSide side, string name, Guid? excludeId = null)
    {
        var normalized = name.Trim().ToLower();
        return _db.OrgUnitTypes.AnyAsync(t => t.Side == side && t.Name.ToLower() == normalized && (excludeId == null || t.Id != excludeId));
    }

    public async Task<Dictionary<Guid, int>> CountUnitsByTypeAsync()
    {
        var departments = await _db.AdministrativeDepartments.AsNoTracking()
            .Where(d => d.UnitTypeId != null)
            .GroupBy(d => d.UnitTypeId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        var cells = await _db.PartyCells.AsNoTracking()
            .Where(c => c.UnitTypeId != null)
            .GroupBy(c => c.UnitTypeId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        return departments.Concat(cells)
            .GroupBy(r => r.Id)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Count));
    }

    public void AddUnitType(OrgUnitType type) => _db.OrgUnitTypes.Add(type);

    public void RemoveUnitType(OrgUnitType type) => _db.OrgUnitTypes.Remove(type);
}

public class EvaluationRepository : IEvaluationRepository
{
    private readonly CongTacDangDbContext _db;

    public EvaluationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<bool> IsDepartmentWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) =>
        OrgSubtreeQueries.IsDepartmentWithinAsync(_db, rootId, unitId, ct);

    /// <inheritdoc />
    public Task<bool> IsPartyCellWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) =>
        OrgSubtreeQueries.IsPartyCellWithinAsync(_db, rootId, unitId, ct);

    public async Task<List<EvaluationPeriod>> GetPeriodsAsync()
    {
        return await _db.EvaluationPeriods
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Quarter)
            .ToListAsync();
    }

    public async Task<EvaluationPeriod?> GetPeriodByIdAsync(Guid id)
    {
        return await _db.EvaluationPeriods.FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>Kỳ Đang mở/Khóa dữ liệu mới nhất.</summary>
    public async Task<EvaluationPeriod?> GetActivePeriodAsync()
    {
        return await _db.EvaluationPeriods
            .AsNoTracking()
            .Where(p => p.Status == PeriodStatus.Open || p.Status == PeriodStatus.Locked)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Quarter)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddPeriodAsync(EvaluationPeriod period)
    {
        _db.EvaluationPeriods.Add(period);
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePeriodAsync(EvaluationPeriod period)
    {
        _db.EvaluationPeriods.Update(period);
        await _db.SaveChangesAsync();
    }

    public async Task<EvaluationRecord?> GetRecordAsync(Guid periodId, Guid memberId)
    {
        return await _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .FirstOrDefaultAsync(r => r.PeriodId == periodId && r.MemberId == memberId);
    }

    public async Task<EvaluationRecord?> GetRecordByIdAsync(Guid id)
    {
        return await _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<List<EvaluationRecordHistory>> GetRecordHistoriesAsync(Guid recordId)
    {
        return await _db.EvaluationRecordHistories
            .AsNoTracking()
            .Where(x => x.RecordId == recordId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<EvaluationRecord>> GetRecordsByPeriodAsync(Guid periodId)
    {
        return await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .Where(r => r.PeriodId == periodId)
            .OrderBy(r => r.Member.FullName)
            .ToListAsync();
    }

    public async Task<List<EvaluationRecord>> GetRecordsByBranchAsync(Guid periodId, Guid branchId)
    {
        return await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Member)
                .ThenInclude(m => m.PartyCell)
            .Include(r => r.Member)
                .ThenInclude(m => m.Department)
            .Include(r => r.Tasks)
                .ThenInclude(t => t.Attachment)
            .Where(r => r.PeriodId == periodId && r.Member.PartyCellId == branchId)
            .OrderBy(r => r.Member.FullName)
            .ToListAsync();
    }

    public async Task AddRecordAsync(EvaluationRecord record)
    {
        _db.EvaluationRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateRecordAsync(EvaluationRecord record)
    {
        _db.EvaluationRecords.Update(record);
        await _db.SaveChangesAsync();
    }

    public async Task AddRecordHistoryAsync(EvaluationRecordHistory history)
    {
        _db.EvaluationRecordHistories.Add(history);
        await _db.SaveChangesAsync();
    }

    public async Task<List<EvaluationTask>> GetTasksByRecordIdAsync(Guid recordId)
    {
        return await _db.EvaluationTasks
            .Include(t => t.Attachment)
            .Where(t => t.RecordId == recordId)
            .OrderBy(t => t.TaskOrder)
            .ToListAsync();
    }

    public async Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks)
    {
        var existingTasks = await _db.EvaluationTasks
            .Where(t => t.RecordId == recordId)
            .ToListAsync();

        _db.EvaluationTasks.RemoveRange(existingTasks);
        _db.EvaluationTasks.AddRange(tasks);
        await _db.SaveChangesAsync();
    }
}

public class CollectiveEvaluationRepository : ICollectiveEvaluationRepository
{
    private readonly CongTacDangDbContext _db;

    public CollectiveEvaluationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    public async Task<CollectiveEvaluationRecord?> GetByIdAsync(Guid id)
    {
        return await _db.CollectiveEvaluationRecords
            .AsNoTracking()
            .Include(x => x.Period)
            .Include(x => x.PartyCell)
            .Include(x => x.Department)
            .Include(x => x.Head)
            .Include(x => x.Items.OrderBy(i => i.ItemOrder))
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<CollectiveEvaluationRecord>> GetByPeriodAsync(Guid periodId, CollectiveEvaluationForm? form = null)
    {
        var query = _db.CollectiveEvaluationRecords
            .AsNoTracking()
            .Include(x => x.PartyCell)
            .Include(x => x.Department)
            .Include(x => x.Items.OrderBy(i => i.ItemOrder))
            .Where(x => x.PeriodId == periodId);

        if (form.HasValue)
            query = query.Where(x => x.Form == form.Value);

        return await query.OrderBy(x => x.Form).ThenBy(x => x.SubjectName).ToListAsync();
    }

    public async Task<List<PartyCellMemberCount>> GetPartyCellsWithRecordsAsync(Guid periodId)
    {
        var counts = await _db.EvaluationRecords
            .AsNoTracking()
            .Where(r => r.PeriodId == periodId && r.PartyCellId != null)
            .GroupBy(r => r.PartyCellId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync();
        var ids = counts.Select(c => c.Id).ToList();
        var names = await _db.PartyCells.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name);
        return counts
            .Where(c => names.ContainsKey(c.Id))
            .Select(c => new PartyCellMemberCount(c.Id, names[c.Id], c.Count))
            .OrderBy(c => c.Name)
            .ToList();
    }

    public async Task AddAsync(CollectiveEvaluationRecord record)
    {
        _db.CollectiveEvaluationRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(CollectiveEvaluationRecord record)
    {
        _db.CollectiveEvaluationRecords.Update(record);
        await _db.SaveChangesAsync();
    }

    public Task<CollectiveEvaluationRecord?> GetForUpdateAsync(Guid id) =>
        _db.CollectiveEvaluationRecords
            .Include(x => x.Period)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

    /// <summary>Dòng cũ bị xóa, dòng mới đặt trạng thái Added tường minh.</summary>
    public void ReplaceItems(CollectiveEvaluationRecord record, IEnumerable<CollectiveEvaluationItem> items)
    {
        _db.CollectiveEvaluationItems.RemoveRange(record.Items);
        record.Items.Clear();
        foreach (var item in items)
        {
            item.CollectiveRecordId = record.Id;
            _db.CollectiveEvaluationItems.Add(item);
        }
    }
}

public class EvaluationMeetingRepository : IEvaluationMeetingRepository
{
    private readonly CongTacDangDbContext _db;

    public EvaluationMeetingRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    public async Task<EvaluationMeeting?> GetByIdAsync(Guid id)
    {
        return await _db.EvaluationMeetings
            .AsNoTracking()
            .Include(x => x.Period)
            .Include(x => x.PartyCell)
            .Include(x => x.Department)
            .Include(x => x.VoteSummaries)
                .ThenInclude(x => x.Record)
                    .ThenInclude(x => x.Member)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<List<EvaluationMeeting>> GetByPeriodAsync(Guid periodId, Guid? partyCellId = null)
    {
        var query = _db.EvaluationMeetings
            .AsNoTracking()
            .Include(x => x.PartyCell)
            .Include(x => x.Department)
            .Include(x => x.VoteSummaries)
                .ThenInclude(x => x.Record)
                    .ThenInclude(x => x.Member)
            .Where(x => x.PeriodId == periodId);

        if (partyCellId.HasValue)
            query = query.Where(x => x.PartyCellId == partyCellId);

        return await query.OrderByDescending(x => x.StartedAt).ToListAsync();
    }

    public async Task AddAsync(EvaluationMeeting meeting)
    {
        _db.EvaluationMeetings.Add(meeting);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(EvaluationMeeting meeting)
    {
        _db.EvaluationMeetings.Update(meeting);
        await _db.SaveChangesAsync();
    }

    public Task<EvaluationMeeting?> GetForUpdateAsync(Guid id) =>
        _db.EvaluationMeetings
            .Include(x => x.Period)
            .Include(x => x.VoteSummaries)
            .FirstOrDefaultAsync(x => x.Id == id);
}

public class HolidayRepository : IHolidayRepository
{
    private readonly CongTacDangDbContext _db;

    public HolidayRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<List<Holiday>> ListAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => _db.Holidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= to).OrderBy(h => h.Date).ToListAsync(ct);

    /// <inheritdoc />
    public Task<Holiday?> FindAsync(Guid id, CancellationToken ct = default)
        => _db.Holidays.FirstOrDefaultAsync(h => h.Id == id, ct);

    /// <inheritdoc />
    public async Task<HashSet<DateOnly>> ExistingDatesAsync(DateOnly from, DateOnly to, Guid? excludeId = null, CancellationToken ct = default)
        => (await _db.Holidays.AsNoTracking()
                .Where(h => h.Date >= from && h.Date <= to && (excludeId == null || h.Id != excludeId))
                .Select(h => h.Date).ToListAsync(ct)).ToHashSet();

    /// <inheritdoc />
    public void Add(Holiday holiday) => _db.Holidays.Add(holiday);

    /// <inheritdoc />
    public void Remove(Holiday holiday) => _db.Holidays.Remove(holiday);
}
