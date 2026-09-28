using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>
/// Repository quản lý hồ sơ Cán bộ / Đảng viên
/// </summary>
public class UserRepository : GenericRepository<PartyMemberProfile>, IUserRepository
{
    public UserRepository(CongTacDangDbContext db) : base(db)
    {
    }

    /// <summary>Tìm kiếm cán bộ theo tên đăng nhập</summary>
    public async Task<PartyMemberProfile?> GetByUsernameAsync(string username)
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .FirstOrDefaultAsync(m => m.Username == username);
    }

    /// <summary>Lấy thông tin cán bộ đầu tiên trong hệ thống</summary>
    public async Task<PartyMemberProfile?> GetFirstMemberAsync()
    {
        return await _db.PartyMemberProfiles
            .AsNoTracking()
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.CreatedAt)
            .FirstOrDefaultAsync();
    }

    /// <summary>Lấy toàn bộ danh sách cán bộ kèm thông tin Chi bộ và Phòng ban</summary>
    public async Task<List<PartyMemberProfile>> GetAllWithDetailsAsync()
    {
        return await _db.PartyMemberProfiles
            .AsNoTracking()
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .OrderBy(m => m.FullName)
            .ToListAsync();
    }

    /// <summary>Tìm kiếm cán bộ theo tên đăng nhập kèm thông tin Vai trò và Quyền hạn</summary>
    public async Task<PartyMemberProfile?> GetWithRolesAndPermissionsAsync(string username)
    {
        return await _db.PartyMemberProfiles
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .Include(m => m.Roles)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(m => m.Username == username);
    }

    /// <summary>Tìm kiếm cán bộ theo Id kèm thông tin Vai trò và Quyền hạn</summary>
    public async Task<PartyMemberProfile?> GetWithRolesAndPermissionsByIdAsync(Guid id)
    {
        return await _db.PartyMemberProfiles
            .AsNoTracking()
            .Include(m => m.PartyCell)
            .Include(m => m.Department)
            .Include(m => m.Roles)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(m => m.Id == id);
    }
}

/// <summary>
/// Repository quản lý tệp đính kèm và minh chứng
/// </summary>
public class AttachmentRepository : GenericRepository<TaskAttachment>, IAttachmentRepository, IAttachmentAccessReader, IAttachmentVersionRepository
{
    public AttachmentRepository(CongTacDangDbContext db) : base(db)
    {
    }

    /// <summary>Lấy toàn bộ danh sách tệp đính kèm (phiên bản hiện hành) theo thời gian mới nhất</summary>
    public async Task<List<TaskAttachment>> GetAllAttachmentsAsync()
    {
        return await _db.TaskAttachments
            .AsNoTracking()
            .Where(a => !a.IsSuperseded)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync();
    }

    /// <summary>Lấy các tệp hiện hành của một đối tượng (kể cả dữ liệu cũ chưa có OwnerType).</summary>
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
        }
        else if (ownerType == AttachmentOwnerTypes.EvaluationTask)
        {
            owned = await query
                .Where(a => (a.OwnerType == ownerType && a.OwnerId == ownerId)
                    || (a.OwnerType == null && a.RecordId == null && a.RelatedId == ownerId))
                .ToListAsync();

            // Dữ liệu cũ: tệp minh chứng gắn qua EvaluationTask.AttachmentId (có thể trỏ tới một phiên bản cũ).
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

    /// <summary>Lấy phiên bản hiện hành của nhóm chứa phiên bản cho trước.</summary>
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

    /// <summary>Lấy mọi phiên bản chưa xóa của một nhóm.</summary>
    public async Task<List<TaskAttachment>> GetVersionsAsync(Guid groupId)
    {
        return await _db.TaskAttachments
            .AsNoTracking()
            .Where(a => a.Id == groupId || a.FileGroupId == groupId)
            .OrderBy(a => a.VersionNumber)
            .ThenBy(a => a.UploadedAt)
            .ToListAsync();
    }

    /// <summary>Lưu phiên bản mới và đánh dấu phiên bản trước đã bị thay trong một lần SaveChanges.</summary>
    public async Task AddVersionAsync(TaskAttachment previous, TaskAttachment next)
    {
        _db.TaskAttachments.Update(previous);
        await _db.TaskAttachments.AddAsync(next);
        await _db.SaveChangesAsync();
    }

    /// <summary>Xóa mềm mọi phiên bản của một nhóm (DbContext chuyển Remove thành xóa mềm).</summary>
    public async Task SoftDeleteGroupAsync(Guid groupId)
    {
        var versions = await _db.TaskAttachments
            .Where(a => a.Id == groupId || a.FileGroupId == groupId)
            .ToListAsync();
        _db.TaskAttachments.RemoveRange(versions);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lấy hồ sơ đánh giá của đối tượng sở hữu tệp.</summary>
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
            _ => null
        };
        if (!recordId.HasValue)
            return null;

        return await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Member)
            .FirstOrDefaultAsync(r => r.Id == recordId.Value);
    }

    /// <summary>Lọc các người dùng đang có vai trò cho trước.</summary>
    public async Task<HashSet<Guid>> GetUserIdsInRoleAsync(IReadOnlyCollection<Guid> userIds, string roleCode)
    {
        if (userIds.Count == 0)
            return new HashSet<Guid>();

        var ids = userIds.Distinct().ToList();
        var matched = await _db.PartyMemberProfiles
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id) && u.Roles.Any(r => r.Code == roleCode))
            .Select(u => u.Id)
            .ToListAsync();
        return matched.ToHashSet();
    }

    /// <summary>Lấy các hồ sơ đánh giá mà mỗi tệp đang gắn vào (qua RecordId hoặc qua nhiệm vụ).</summary>
    public async Task<Dictionary<Guid, List<AttachmentRecordLink>>> GetRecordLinksAsync(IReadOnlyCollection<TaskAttachment> attachments)
    {
        var result = new Dictionary<Guid, List<AttachmentRecordLink>>();
        if (attachments.Count == 0)
            return result;

        // EvaluationTask.AttachmentId có thể trỏ tới bất kỳ phiên bản nào của nhóm (thường là phiên bản đầu),
        // nên liên kết qua nhiệm vụ được tính theo nhóm phiên bản.
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

        var explicitLinks = attachments
            .Where(a => a.RecordId.HasValue)
            .Select(a => new { AttachmentId = a.Id, RecordId = a.RecordId!.Value })
            .ToList();

        var recordIds = taskLinks.Select(x => x.RecordId)
            .Concat(explicitLinks.Select(x => x.RecordId))
            .Distinct()
            .ToList();
        if (recordIds.Count == 0)
            return result;

        var records = await _db.EvaluationRecords
            .AsNoTracking()
            .Include(r => r.Member)
            .Where(r => recordIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id);

        void Add(Guid attachmentId, Guid recordId, bool viaTask)
        {
            if (!records.TryGetValue(recordId, out var record))
                return;
            if (!result.TryGetValue(attachmentId, out var list))
                result[attachmentId] = list = new List<AttachmentRecordLink>();
            list.Add(new AttachmentRecordLink(record, viaTask));
        }

        foreach (var link in explicitLinks)
            Add(link.AttachmentId, link.RecordId, viaTask: false);
        foreach (var link in taskLinks)
            Add(link.AttachmentId, link.RecordId, viaTask: true);

        return result;
    }
}

/// <summary>Repository chỉ đọc nhật ký audit tập trung.</summary>
public class AuditRepository : IAuditRepository
{
    private readonly CongTacDangDbContext _db;

    /// <summary>Khởi tạo repository audit.</summary>
    public AuditRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Truy vấn audit log mới nhất theo bộ lọc tùy chọn.</summary>
    public async Task<List<AuditLog>> GetAuditLogsAsync(string? entityType, string? entityId, int limit)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(x => x.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(entityId))
            query = query.Where(x => x.EntityId == entityId);

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync();
    }
}

/// <summary>
/// Repository quản lý tổ chức Chi bộ và Phòng ban
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    private readonly CongTacDangDbContext _db;

    public OrganizationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy danh sách Chi bộ kèm Đảng viên trực thuộc</summary>
    public async Task<List<PartyCell>> GetPartyCellsWithMembersAsync()
    {
        return await _db.PartyCells
            .AsNoTracking()
            .Include(c => c.Members)
            .OrderBy(c => c.Code)
            .ToListAsync();
    }

    /// <summary>Lấy danh sách Phòng ban chuyên môn kèm cán bộ trực thuộc</summary>
    public async Task<List<AdministrativeDepartment>> GetDepartmentsWithMembersAsync()
    {
        return await _db.AdministrativeDepartments
            .AsNoTracking()
            .Include(d => d.Members)
            .OrderBy(d => d.Code)
            .ToListAsync();
    }

    /// <summary>Lấy thông tin Chi bộ theo Id</summary>
    public async Task<PartyCell?> GetPartyCellByIdAsync(Guid id)
    {
        return await _db.PartyCells
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>Thêm mới Chi bộ</summary>
    public async Task AddPartyCellAsync(PartyCell cell)
    {
        await _db.PartyCells.AddAsync(cell);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cập nhật thông tin Chi bộ</summary>
    public async Task UpdatePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Update(cell);
        await _db.SaveChangesAsync();
    }

    /// <summary>Đánh dấu Chi bộ xóa mềm qua DbContext.</summary>
    public async Task DeletePartyCellAsync(PartyCell cell)
    {
        _db.PartyCells.Remove(cell);
        await _db.SaveChangesAsync();
    }
}

/// <summary>
/// Repository triển khai quản lý kỳ đánh giá và hồ sơ đánh giá cán bộ theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class EvaluationRepository : IEvaluationRepository
{
    private readonly CongTacDangDbContext _db;

    public EvaluationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy danh sách tất cả các kỳ đánh giá</summary>
    public async Task<List<EvaluationPeriod>> GetPeriodsAsync()
    {
        return await _db.EvaluationPeriods
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Quarter)
            .ToListAsync();
    }

    /// <summary>Lấy thông tin kỳ đánh giá theo Id</summary>
    public async Task<EvaluationPeriod?> GetPeriodByIdAsync(Guid id)
    {
        return await _db.EvaluationPeriods.FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <summary>Lấy kỳ đánh giá đang kích hoạt</summary>
    public async Task<EvaluationPeriod?> GetActivePeriodAsync()
    {
        return await _db.EvaluationPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsActive);
    }

    /// <summary>Thêm mới kỳ đánh giá</summary>
    public async Task AddPeriodAsync(EvaluationPeriod period)
    {
        _db.EvaluationPeriods.Add(period);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cập nhật kỳ đánh giá</summary>
    public async Task UpdatePeriodAsync(EvaluationPeriod period)
    {
        _db.EvaluationPeriods.Update(period);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lấy hồ sơ đánh giá của một cán bộ trong kỳ cụ thể</summary>
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

    /// <summary>Lấy hồ sơ đánh giá theo Id</summary>
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

    /// <summary>Truy vấn các mốc trạng thái của hồ sơ theo thời gian tăng dần.</summary>
    public async Task<List<EvaluationRecordHistory>> GetRecordHistoriesAsync(Guid recordId)
    {
        return await _db.EvaluationRecordHistories
            .AsNoTracking()
            .Where(x => x.RecordId == recordId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
    }

    /// <summary>Lấy toàn bộ hồ sơ đánh giá trong một kỳ</summary>
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

    /// <summary>Lấy danh sách hồ sơ đánh giá của một Chi bộ</summary>
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

    /// <summary>Thêm mới hồ sơ đánh giá</summary>
    public async Task AddRecordAsync(EvaluationRecord record)
    {
        _db.EvaluationRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    /// <summary>Cập nhật hồ sơ đánh giá</summary>
    public async Task UpdateRecordAsync(EvaluationRecord record)
    {
        _db.EvaluationRecords.Update(record);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lưu một mốc chuyển trạng thái của hồ sơ đánh giá.</summary>
    public async Task AddRecordHistoryAsync(EvaluationRecordHistory history)
    {
        _db.EvaluationRecordHistories.Add(history);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lấy danh sách công việc đăng ký</summary>
    public async Task<List<EvaluationTask>> GetTasksByRecordIdAsync(Guid recordId)
    {
        return await _db.EvaluationTasks
            .Include(t => t.Attachment)
            .Where(t => t.RecordId == recordId)
            .OrderBy(t => t.TaskOrder)
            .ToListAsync();
    }

    /// <summary>Thay thế danh sách công việc của hồ sơ đánh giá</summary>
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

/// <summary>Repository triển khai hồ sơ đánh giá tập thể.</summary>
public class CollectiveEvaluationRepository : ICollectiveEvaluationRepository
{
    private readonly CongTacDangDbContext _db;

    public CollectiveEvaluationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy hồ sơ tập thể kèm các dòng nội dung chi tiết.</summary>
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

    /// <summary>Lấy danh sách hồ sơ tập thể theo kỳ và tùy chọn biểu mẫu.</summary>
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

    /// <summary>Lưu mới hồ sơ tập thể.</summary>
    public async Task AddAsync(CollectiveEvaluationRecord record)
    {
        _db.CollectiveEvaluationRecords.Add(record);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lưu thay đổi hồ sơ tập thể.</summary>
    public async Task UpdateAsync(CollectiveEvaluationRecord record)
    {
        _db.CollectiveEvaluationRecords.Update(record);
        await _db.SaveChangesAsync();
    }
}

/// <summary>Repository triển khai biên bản hội nghị và kiểm phiếu.</summary>
public class EvaluationMeetingRepository : IEvaluationMeetingRepository
{
    private readonly CongTacDangDbContext _db;

    public EvaluationMeetingRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <summary>Lấy biên bản kèm tổng hợp phiếu.</summary>
    public async Task<EvaluationMeeting?> GetByIdAsync(Guid id)
    {
        return await _db.EvaluationMeetings
            .AsNoTracking()
            .Include(x => x.Period)
            .Include(x => x.PartyCell)
            .Include(x => x.VoteSummaries)
                .ThenInclude(x => x.Record)
                    .ThenInclude(x => x.Member)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    /// <summary>Lấy biên bản theo kỳ và tùy chọn Chi bộ.</summary>
    public async Task<List<EvaluationMeeting>> GetByPeriodAsync(Guid periodId, Guid? partyCellId = null)
    {
        var query = _db.EvaluationMeetings
            .AsNoTracking()
            .Include(x => x.PartyCell)
            .Include(x => x.VoteSummaries)
            .Where(x => x.PeriodId == periodId);

        if (partyCellId.HasValue)
            query = query.Where(x => x.PartyCellId == partyCellId);

        return await query.OrderByDescending(x => x.StartedAt).ToListAsync();
    }

    /// <summary>Lưu mới biên bản hội nghị.</summary>
    public async Task AddAsync(EvaluationMeeting meeting)
    {
        _db.EvaluationMeetings.Add(meeting);
        await _db.SaveChangesAsync();
    }

    /// <summary>Lưu thay đổi biên bản hội nghị.</summary>
    public async Task UpdateAsync(EvaluationMeeting meeting)
    {
        _db.EvaluationMeetings.Update(meeting);
        await _db.SaveChangesAsync();
    }
}
