using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Domain.Entities;
using CongTacDang.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CongTacDang.Infrastructure.Repositories;

/// <summary>Ghi chỉ đưa vào DbContext; lưu qua IUnitOfWork.</summary>
public sealed class CalibrationRepository : ICalibrationRepository
{
    private readonly CongTacDangDbContext _db;

    public CalibrationRepository(CongTacDangDbContext db)
    {
        _db = db;
    }

    private DbSet<CalibrationSession> Sessions => _db.Set<CalibrationSession>();

    /// <inheritdoc />
    public Task<CalibrationSession?> FindAsync(Guid id, CancellationToken ct = default) =>
        Sessions
            .Include(s => s.Period)
            .Include(s => s.Department)
            .Include(s => s.PartyCell)
            .Include(s => s.Records).ThenInclude(r => r.Record).ThenInclude(x => x.Member)
            .Include(s => s.Records).ThenInclude(r => r.Record).ThenInclude(x => x.Department)
            .Include(s => s.Records).ThenInclude(r => r.Record).ThenInclude(x => x.PartyCell)
            .Include(s => s.Attendees)
            .Include(s => s.SummaryRows)
            .Include(s => s.Cases)
            .AsSplitQuery()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <inheritdoc />
    public Task<List<CalibrationSession>> ListAsync(Guid? periodId, CancellationToken ct = default) =>
        Sessions.AsNoTracking()
            .Include(s => s.Period)
            .Include(s => s.Department)
            .Include(s => s.PartyCell)
            .Include(s => s.Records).ThenInclude(r => r.Record)
            .Where(s => periodId == null || s.PeriodId == periodId)
            .OrderByDescending(s => s.PreparedAt)
            .AsSplitQuery()
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task<Dictionary<Guid, Guid>> GetActiveSessionByRecordAsync(IReadOnlyCollection<Guid> recordIds, Guid? exceptSessionId,
        CancellationToken ct = default)
    {
        var ids = recordIds.ToList();
        var rows = await _db.Set<CalibrationRecord>().AsNoTracking()
            .Where(r => r.IsActive && ids.Contains(r.RecordId) && (exceptSessionId == null || r.SessionId != exceptSessionId))
            .Select(r => new { r.RecordId, r.SessionId })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.RecordId, r => r.SessionId);
    }

    /// <inheritdoc />
    public Task<List<CalibrationRecord>> ListActiveRowsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        var ids = recordIds.ToList();
        return _db.Set<CalibrationRecord>().AsNoTracking()
            .Include(r => r.Session).ThenInclude(s => s.Records)
            .Where(r => r.IsActive && ids.Contains(r.RecordId))
            .AsSplitQuery()
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<List<EvaluationRecordHistory>> ListRecordHistorySinceAsync(IReadOnlyCollection<Guid> recordIds, DateTime since, CancellationToken ct = default)
    {
        var ids = recordIds.ToList();
        return _db.EvaluationRecordHistories.AsNoTracking()
            .Where(h => ids.Contains(h.RecordId) && h.CreatedAt > since)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public Task<List<EvaluationRecord>> GetRecordsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        var ids = recordIds.ToList();
        return _db.EvaluationRecords
            .Include(r => r.Period)
            .Include(r => r.Member)
            .Include(r => r.Department)
            .Include(r => r.PartyCell)
            .Where(r => ids.Contains(r.Id))
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public void Add(CalibrationSession session) => Sessions.Add(session);

    /// <inheritdoc />
    public void AddRecord(CalibrationRecord record) => _db.Set<CalibrationRecord>().Add(record);

    /// <inheritdoc />
    public void RemoveRecord(CalibrationRecord record) => _db.Set<CalibrationRecord>().Remove(record);

    /// <inheritdoc />
    public void RemoveChildren(IEnumerable<CalibrationAttendee> attendees, IEnumerable<CalibrationSummaryRow> rows, IEnumerable<CalibrationCase> cases)
    {
        _db.Set<CalibrationAttendee>().RemoveRange(attendees);
        _db.Set<CalibrationSummaryRow>().RemoveRange(rows);
        _db.Set<CalibrationCase>().RemoveRange(cases);
    }

    /// <inheritdoc />
    public void AddAttendee(CalibrationAttendee attendee) => _db.Set<CalibrationAttendee>().Add(attendee);

    /// <inheritdoc />
    public void AddSummaryRow(CalibrationSummaryRow row) => _db.Set<CalibrationSummaryRow>().Add(row);

    /// <inheritdoc />
    public void AddCase(CalibrationCase item) => _db.Set<CalibrationCase>().Add(item);

    /// <inheritdoc />
    public void AddHistory(EvaluationFormHistory history) => _db.EvaluationFormHistories.Add(history);

    /// <inheritdoc />
    public Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid sessionId, CancellationToken ct = default) =>
        _db.EvaluationFormHistories.AsNoTracking()
            .Where(h => h.FormType == EvaluationFormType.M20 && h.FormId == sessionId)
            .OrderBy(h => h.At)
            .ToListAsync(ct);
}
