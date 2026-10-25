using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Application.Common.Interfaces;

/// <summary>Biên bản hiệu chuẩn (Mẫu 20). Ghi chỉ đưa vào DbContext; lưu qua <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
public interface ICalibrationRepository
{
    /// <summary>Kèm kỳ, đơn vị, hồ sơ (cán bộ, đơn vị) và các bảng con; được theo dõi.</summary>
    Task<CalibrationSession?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kèm kỳ, đơn vị và số hồ sơ; không theo dõi. Mới nhất trước.</summary>
    Task<List<CalibrationSession>> ListAsync(Guid? periodId, CancellationToken ct = default);

    /// <summary>Hồ sơ đang thuộc biên bản còn hiệu lực (không hủy), khác <paramref name="exceptSessionId"/>: Id hồ sơ → Id biên bản.</summary>
    Task<Dictionary<Guid, Guid>> GetActiveSessionByRecordAsync(IReadOnlyCollection<Guid> recordIds, Guid? exceptSessionId, CancellationToken ct = default);

    /// <summary>Kèm kỳ, cán bộ, đơn vị, Chi bộ; được theo dõi.</summary>
    Task<List<EvaluationRecord>> GetRecordsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);

    void Add(CalibrationSession session);

    void AddRecord(CalibrationRecord record);

    void RemoveRecord(CalibrationRecord record);

    /// <summary>Xóa cứng các dòng con để thay bằng danh sách mới.</summary>
    void RemoveChildren(IEnumerable<CalibrationAttendee> attendees, IEnumerable<CalibrationSummaryRow> rows, IEnumerable<CalibrationCase> cases);

    void AddAttendee(CalibrationAttendee attendee);

    void AddSummaryRow(CalibrationSummaryRow row);

    void AddCase(CalibrationCase item);

    void AddHistory(EvaluationFormHistory history);

    /// <summary>Theo thời gian tăng dần; không theo dõi.</summary>
    Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid sessionId, CancellationToken ct = default);
}
