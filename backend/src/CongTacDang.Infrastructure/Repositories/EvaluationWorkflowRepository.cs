using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Truy cập dữ liệu cho luồng đánh giá theo cấu hình kỳ (task 12). Ghi chỉ đưa vào DbContext, lưu qua IUnitOfWork.</summary>
public sealed class EvaluationWorkflowRepository : IEvaluationWorkflowRepository
{
    private readonly CongTacDangDbContext _db;

    public EvaluationWorkflowRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<EvaluationPeriod?> FindPeriodAsync(Guid id, CancellationToken ct = default) =>
        _db.EvaluationPeriods.FirstOrDefaultAsync(p => p.Id == id, ct);

    /// <inheritdoc />
    public Task<List<EvaluationPeriod>> ListPeriodsAsync(CancellationToken ct = default) =>
        _db.EvaluationPeriods.AsNoTracking()
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Quarter)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<Dictionary<Guid, int>> CountRecordsByPeriodAsync(CancellationToken ct = default) =>
        await _db.EvaluationRecords.AsNoTracking()
            .GroupBy(r => r.PeriodId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    /// <inheritdoc />
    public void AddPeriod(EvaluationPeriod period) => _db.EvaluationPeriods.Add(period);

    private IQueryable<EvaluationRecord> RecordsWithDetails() => _db.EvaluationRecords
        .Include(r => r.Period)
        .Include(r => r.Member)
        .Include(r => r.PartyCell)
        .Include(r => r.Department)
        .Include(r => r.Tasks)
            .ThenInclude(t => t.Attachment);

    /// <inheritdoc />
    public Task<EvaluationRecord?> FindRecordAsync(Guid id, CancellationToken ct = default) =>
        RecordsWithDetails().FirstOrDefaultAsync(r => r.Id == id, ct);

    /// <inheritdoc />
    public Task<EvaluationRecord?> FindRecordIncludingDeletedAsync(Guid periodId, Guid memberId, CancellationToken ct = default) =>
        _db.EvaluationRecords.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.PeriodId == periodId && r.MemberId == memberId, ct);

    /// <inheritdoc />
    public Task<List<EvaluationRecord>> ListRecordsAsync(Guid periodId, CancellationToken ct = default) =>
        RecordsWithDetails().AsNoTracking()
            .Where(r => r.PeriodId == periodId)
            .OrderBy(r => r.Member.FullName)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<EvaluationRecord>> ListRecordsForUpdateAsync(Guid periodId, CancellationToken ct = default) =>
        _db.EvaluationRecords.Include(r => r.Tasks)
            .Where(r => r.PeriodId == periodId)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<EvaluationRecord>> ListRecordsByStatusAsync(
        IReadOnlyCollection<Guid> periodIds,
        IReadOnlyCollection<RecordStatus> statuses,
        CancellationToken ct = default) =>
        _db.EvaluationRecords.AsNoTracking()
            .Include(r => r.Member)
            .Include(r => r.PartyCell)
            .Include(r => r.Department)
            .Where(r => periodIds.Contains(r.PeriodId) && statuses.Contains(r.Status))
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetParticipantIdsAsync(Guid periodId, CancellationToken ct = default) =>
        (await _db.EvaluationRecords.AsNoTracking()
            .Where(r => r.PeriodId == periodId)
            .Select(r => r.MemberId)
            .ToListAsync(ct)).ToHashSet();

    /// <inheritdoc />
    public void AddRecord(EvaluationRecord record) => _db.EvaluationRecords.Add(record);

    /// <inheritdoc />
    public void RemoveRecord(EvaluationRecord record) => _db.EvaluationRecords.Remove(record);

    /// <inheritdoc />
    public async Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks, CancellationToken ct = default)
    {
        var existing = await _db.EvaluationTasks.Where(t => t.RecordId == recordId).ToListAsync(ct);
        _db.EvaluationTasks.RemoveRange(existing);
        _db.EvaluationTasks.AddRange(tasks);
    }

    /// <inheritdoc />
    public Task<List<EvaluationTask>> GetTasksAsync(Guid recordId, CancellationToken ct = default) =>
        _db.EvaluationTasks
            .Where(t => t.RecordId == recordId)
            .OrderBy(t => t.TaskOrder)
            .ToListAsync(ct);

    /// <inheritdoc />
    public void AddHistory(EvaluationRecordHistory history) => _db.EvaluationRecordHistories.Add(history);

    /// <inheritdoc />
    public Task<List<EvaluationRecordHistory>> ListHistoryAsync(Guid recordId, CancellationToken ct = default) =>
        _db.EvaluationRecordHistories.AsNoTracking()
            .Where(x => x.RecordId == recordId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public void AddVoteSummary(EvaluationMeetingVoteSummary summary) => _db.EvaluationMeetingVoteSummaries.Add(summary);

    /// <inheritdoc />
    public Task<EvaluationMeeting?> FindMeetingAsync(Guid id, CancellationToken ct = default) =>
        _db.EvaluationMeetings.Include(m => m.VoteSummaries).FirstOrDefaultAsync(m => m.Id == id, ct);

    private IQueryable<MemberSnapshotSource> MemberSources(IQueryable<PartyMemberProfile> query) =>
        query.Select(m => new MemberSnapshotSource(
            m.Id,
            m.Username,
            m.FullName,
            m.DepartmentId,
            m.Department != null ? m.Department.Name : null,
            m.PartyCellId,
            m.PartyCell != null ? m.PartyCell.Name : null,
            m.JobGroup,
            m.ApprovalAuthority,
            m.IsActive));

    /// <inheritdoc />
    public Task<List<MemberSnapshotSource>> GetMembersAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct = default) =>
        MemberSources(_db.PartyMemberProfiles.AsNoTracking().Where(m => memberIds.Contains(m.Id))).ToListAsync(ct);

    /// <inheritdoc />
    public Task<List<MemberSnapshotSource>> SearchMembersAsync(Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default)
    {
        var members = _db.PartyMemberProfiles.AsNoTracking().Where(m => m.IsActive);
        if (departmentId.HasValue)
            members = members.Where(m => m.DepartmentId == departmentId);
        if (partyCellId.HasValue)
            members = members.Where(m => m.PartyCellId == partyCellId);
        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim()}%";
            members = members.Where(m => EF.Functions.ILike(m.FullName, pattern) || EF.Functions.ILike(m.Username, pattern));
        }

        return MemberSources(members.OrderBy(m => m.FullName)).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<List<MemberSnapshotSource>> FindMembersByUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct = default)
    {
        var lowered = usernames.Select(u => u.Trim().ToLowerInvariant()).Distinct().ToList();
        return MemberSources(_db.PartyMemberProfiles.AsNoTracking().Where(m => lowered.Contains(m.Username.ToLower()))).ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default) =>
        _db.PartyMemberProfiles.AsNoTracking()
            .Where(m => m.Id == memberId)
            .Select(m => (string?)m.FullName)
            .FirstOrDefaultAsync(ct);

    /// <inheritdoc />
    public Task<bool> DepartmentExistsAsync(Guid id, CancellationToken ct = default) =>
        _db.AdministrativeDepartments.AnyAsync(d => d.Id == id, ct);

    /// <inheritdoc />
    public Task<bool> PartyCellExistsAsync(Guid id, CancellationToken ct = default) =>
        _db.PartyCells.AnyAsync(c => c.Id == id, ct);

    /// <inheritdoc />
    public async Task<Dictionary<Guid, (Guid AttachmentId, string FileName)>> GetCurrentEvidenceAsync(
        IReadOnlyCollection<EvaluationTask> tasks,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, (Guid, string)>();
        var linked = tasks.Where(t => t.AttachmentId.HasValue).ToList();
        if (linked.Count == 0)
            return result;

        // Nhiệm vụ có thể trỏ tới một phiên bản cũ của nhóm tệp: tìm nhóm rồi lấy phiên bản hiện hành (chưa bị thay, chưa xóa).
        var versionIds = linked.Select(t => t.AttachmentId!.Value).Distinct().ToList();
        var versions = await _db.TaskAttachments.AsNoTracking()
            .Where(a => versionIds.Contains(a.Id))
            .Select(a => new { a.Id, GroupId = a.FileGroupId ?? a.Id })
            .ToListAsync(ct);
        var groupIds = versions.Select(v => v.GroupId).Distinct().ToList();
        var currents = await _db.TaskAttachments.AsNoTracking()
            .Where(a => !a.IsSuperseded && (groupIds.Contains(a.Id) || (a.FileGroupId.HasValue && groupIds.Contains(a.FileGroupId.Value))))
            .Select(a => new { a.Id, GroupId = a.FileGroupId ?? a.Id, a.OriginalFileName, a.FileName })
            .ToListAsync(ct);

        foreach (var task in linked)
        {
            var version = versions.FirstOrDefault(v => v.Id == task.AttachmentId);
            var current = version == null ? null : currents.FirstOrDefault(c => c.GroupId == version.GroupId);
            if (current == null)
                continue;
            var name = !string.IsNullOrWhiteSpace(current.OriginalFileName) ? current.OriginalFileName : current.FileName;
            result[task.Id] = (current.Id, name ?? string.Empty);
        }

        return result;
    }
}
