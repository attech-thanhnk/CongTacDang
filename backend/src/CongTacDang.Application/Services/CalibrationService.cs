using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Biên bản họp thống nhất kết quả đánh giá (Mẫu 20).</summary>
public interface ICalibrationService
{
    /// <summary>Biên bản trong phạm vi lập/chốt/xem của người dùng, không có hồ sơ của chính họ.</summary>
    Task<List<CalibrationSessionListItemDto>> ListAsync(Guid? periodId, CancellationToken ct = default);

    Task<CalibrationSessionDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Hồ sơ đã thẩm định, chưa thuộc biên bản nào, trong phạm vi lập biên bản.</summary>
    Task<List<CalibrationCandidateDto>> ListCandidatesAsync(Guid periodId, Guid? departmentId, string? workflowProfileCode, CancellationToken ct = default);

    Task<CalibrationSessionDto> CreateAsync(CreateCalibrationSessionDto request, CancellationToken ct = default);

    /// <summary>Lưu toàn bộ bản nháp (hồ sơ, thành phần, tổng hợp, trường hợp); cập nhật lại mốc trước họp thống nhất của hồ sơ.</summary>
    Task<CalibrationSessionDto> UpdateAsync(Guid id, UpdateCalibrationSessionDto request, CancellationToken ct = default);

    /// <summary>Chốt: khác người lập; áp <c>Calibrated*</c> lên các hồ sơ có điều chỉnh và ghi lịch sử hồ sơ.</summary>
    Task<CalibrationSessionDto> ConcludeAsync(Guid id, ConcludeCalibrationDto request, CancellationToken ct = default);

    /// <summary>Bắt buộc lý do; hủy áp dụng kết quả đã áp lên hồ sơ.</summary>
    Task<CalibrationSessionDto> ReopenAsync(Guid id, CalibrationReasonDto request, CancellationToken ct = default);

    /// <summary>Chỉ bản nháp; bắt buộc lý do.</summary>
    Task<CalibrationSessionDto> VoidAsync(Guid id, CalibrationReasonDto request, CancellationToken ct = default);

    Task<List<CalibrationHistoryDto>> GetHistoryAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Việc "Họp thống nhất": biên bản cần mở lại, cập nhật hoặc chốt lại sau khi hồ sơ trong biên bản được mở lại.</summary>
public interface ICalibrationWorkQueue
{
    /// <summary>Nhóm rỗng không trả về.</summary>
    Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default);
}

/// <summary>
/// Chỉ lập khi kỳ đang mở, hồ sơ đã thẩm định và chưa ghi nhận quyết định; mỗi hồ sơ thuộc tối đa một biên bản còn hiệu lực.
/// Người lập khác người chốt; chủ hồ sơ không lập, chốt hay xem biên bản có hồ sơ của mình.
/// </summary>
public sealed class CalibrationService : ICalibrationService, ICalibrationGate, ICalibrationWorkQueue
{
    public const string WorkQueueGroup = "CALIBRATIONS";

    private const int MaxReasonLength = 2000;

    private readonly ICalibrationRepository _repo;
    private readonly IEvaluationWorkflowRepository _records;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IStepPerformers? _stepPerformers;

    public CalibrationService(
        ICalibrationRepository repo,
        IEvaluationWorkflowRepository records,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IStepPerformers? stepPerformers = null)
    {
        _repo = repo;
        _records = records;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
        _stepPerformers = stepPerformers;
    }

    #region Đọc

    /// <inheritdoc />
    public async Task<List<CalibrationSessionListItemDto>> ListAsync(Guid? periodId, CancellationToken ct = default)
    {
        var scope = _guard.GetScope(PermissionCodes.EvaluationCalibrationManage)
            .Union(_guard.GetScope(PermissionCodes.EvaluationCalibrationConclude))
            .Union(_guard.GetScope(PermissionCodes.EvaluationRead));
        var sessions = await _repo.ListAsync(periodId == Guid.Empty ? null : periodId, ct);
        return sessions
            .Where(s => scope.Matches(null, s.DepartmentId, s.PartyCellId) && !HasOwnRecord(s))
            .Select(s => new CalibrationSessionListItemDto
            {
                Id = s.Id,
                Version = s.Version,
                PeriodId = s.PeriodId,
                PeriodName = s.Period.Name,
                DepartmentName = s.Department?.Name,
                PartyCellName = s.PartyCell?.Name,
                GroupName = s.GroupName,
                Status = s.Status.ToString(),
                StatusName = StatusName(s.Status),
                HeldAt = s.HeldAt,
                RecordCount = s.Records.Count,
                PreparedByName = s.PreparedByName,
                ConcludedAt = s.ConcludedAt
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CalibrationSessionDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var session = await RequireSessionAsync(id, ct);
        EnsureCanView(session);
        return await BuildDtoAsync(session, ct);
    }

    /// <inheritdoc />
    public async Task<List<CalibrationCandidateDto>> ListCandidatesAsync(Guid periodId, Guid? departmentId, string? workflowProfileCode,
        CancellationToken ct = default)
    {
        var period = await _records.FindPeriodAsync(periodId, ct) ?? throw new NotFoundException("Không tìm thấy kỳ đánh giá.");
        if (period.Status != PeriodStatus.Open)
            return new List<CalibrationCandidateDto>();

        var settings = ParseSettings(period);
        // Cùng điều kiện với lúc đưa hồ sơ vào biên bản (ResolveRecordsAsync).
        var records = await _records.ListRecordsByStatusAsync(new[] { periodId }, new[] { RecordStatus.AwaitingDecision }, ct);
        var taken = await _repo.GetActiveSessionByRecordAsync(records.Select(r => r.Id).ToList(), null, ct);
        var profileCode = workflowProfileCode?.Trim();

        var result = new List<CalibrationCandidateDto>();
        var seniority = await _records.GetHeldPositionsAsync(records.Select(r => r.MemberId).Distinct().ToList(), DateTime.UtcNow, ct);
        foreach (var r in PositionRules.OrderBySeniority(records, seniority, await _records.GetUnitOrderAsync(ct), x => x.MemberId, x => x.Department?.Name, x => x.Member?.FullName))
        {
            if (IsOwn(r) || taken.ContainsKey(r.Id)
                || !_guard.Can(PermissionCodes.EvaluationCalibrationManage, AccessTarget.ForRecord(r).ForWrite()))
                continue;
            if (departmentId is { } dept && r.DepartmentId != dept)
                continue;
            if (!string.IsNullOrEmpty(profileCode) && !string.Equals(r.WorkflowProfileCode, profileCode, StringComparison.OrdinalIgnoreCase))
                continue;
            var profile = EvaluationMapping.ProfileOf(settings, r);
            var (grade, score) = CalibrationRules.Before(r);
            if (!CalibrationRules.HasAppraisal(r) || grade == EvaluationGrade.ChuaXepLoai
                || profile.GateModeOf(WorkflowGates.Calibration) != GateMode.Required)
                continue;
            result.Add(new CalibrationCandidateDto
            {
                RecordId = r.Id,
                MemberId = r.MemberId,
                FullName = r.Member?.FullName ?? string.Empty,
                DepartmentName = r.Department?.Name,
                PartyCellName = r.PartyCell?.Name,
                WorkflowProfileCode = r.WorkflowProfileCode,
                WorkflowProfileName = profile.Name,
                RecordStatus = r.Status.ToString(),
                GradeBefore = EvaluationMapping.GradeCode(grade),
                ScoreBefore = score
            });
        }
        return result;
    }

    /// <inheritdoc />
    public async Task<List<CalibrationHistoryDto>> GetHistoryAsync(Guid id, CancellationToken ct = default)
    {
        var session = await RequireSessionAsync(id, ct);
        EnsureCanView(session);
        var rows = await _repo.ListHistoryAsync(id, ct);
        return rows.Select(h => new CalibrationHistoryDto
        {
            Action = h.Action.ToString(),
            ActionName = ActionName(h.Action),
            ByUserName = h.ByUserName,
            At = h.At,
            Reason = h.Reason
        }).ToList();
    }

    #endregion

    #region Ghi

    /// <inheritdoc />
    public async Task<CalibrationSessionDto> CreateAsync(CreateCalibrationSessionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var period = await _records.FindPeriodAsync(request.PeriodId, ct)
            ?? throw new NotFoundException("Không tìm thấy kỳ đánh giá.");
        if (period.Status != PeriodStatus.Open)
            throw new ConflictException("Chỉ lập biên bản họp thống nhất khi kỳ đánh giá đang mở.");
        var departmentId = request.DepartmentId;
        var partyCellId = request.PartyCellId;
        if (departmentId is null && partyCellId is null)
        {
            // Giao diện không chọn đơn vị: lấy đơn vị chung của các hồ sơ đã chọn.
            var picked = (request.RecordIds ?? new List<Guid>()).Where(i => i != Guid.Empty).Distinct().ToList();
            if (picked.Count == 0)
                throw new ValidationException("Hãy chọn ít nhất một hồ sơ cho biên bản.");
            var chosen = (await _repo.GetRecordsAsync(picked, ct)).Where(r => !IsOwn(r)).ToList();
            if (chosen.Count == 0)
                throw new ValidationException("Hãy chọn ít nhất một hồ sơ cho biên bản.");
            (departmentId, partyCellId) = RecordGroupUnits.Resolve(_guard, PermissionCodes.EvaluationCalibrationManage, chosen, "Lập biên bản");
        }
        if (departmentId is { } d && !await _records.DepartmentExistsAsync(d, ct))
            throw new ValidationException("Đơn vị lập biên bản không tồn tại.");
        if (partyCellId is { } c && !await _records.PartyCellExistsAsync(c, ct))
            throw new ValidationException("Tổ chức Đảng lập biên bản không tồn tại.");
        _guard.Ensure(PermissionCodes.EvaluationCalibrationManage, AccessTarget.ForUnitsWrite(departmentId, partyCellId));

        var groupName = Required(request.GroupName, CalibrationRules.MaxNameLength + 100, "Tên nhóm chức danh/công việc tương đồng");
        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        var session = new CalibrationSession
        {
            PeriodId = period.Id,
            DepartmentId = departmentId,
            PartyCellId = partyCellId,
            GroupName = groupName,
            Status = CalibrationStatus.Draft,
            PreparedById = _currentUser.UserId,
            PreparedByName = actorName,
            PreparedAt = now,
            CreatedAt = now
        };
        var records = await ResolveRecordsAsync(session, period, request.RecordIds, ct);

        _repo.Add(session);
        foreach (var record in records)
            _repo.AddRecord(NewCalibrationRecord(session.Id, record));
        AddFormHistory(session, EvaluationFormAction.Create, actorName, now, null, null);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(session.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CalibrationSessionDto> UpdateAsync(Guid id, UpdateCalibrationSessionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = RequireVersion(request.Version);
        var session = await RequireSessionAsync(id, ct);
        EnsureNoOwnRecord(session);
        _guard.Ensure(PermissionCodes.EvaluationCalibrationManage, UnitWriteTarget(session));
        EnsureVersion(session, version);
        EnsureEditable(session);

        // Dòng của hồ sơ đã có quyết định sau lần chốt trước luôn giữ nguyên, kể cả khi danh sách gửi lên thiếu.
        var existing = session.Records.ToDictionary(r => r.RecordId);
        var kept = session.Records.Where(r => IsKept(session, r.Record)).ToDictionary(r => r.RecordId);
        var requested = (request.RecordIds ?? new List<Guid>()).Where(i => !kept.ContainsKey(i)).ToList();
        var records = requested.Count == 0 && kept.Count > 0
            ? new List<EvaluationRecord>()
            : await ResolveRecordsAsync(session, session.Period, requested, ct);
        var now = DateTime.UtcNow;

        // Hồ sơ: giữ dòng cũ (cập nhật mốc trước), thêm dòng mới, bỏ dòng không còn.
        foreach (var gone in existing.Values.Where(r => !kept.ContainsKey(r.RecordId) && records.All(x => x.Id != r.RecordId)))
            _repo.RemoveRecord(gone);
        foreach (var record in records)
        {
            var (grade, score) = CalibrationRules.Before(record);
            if (existing.TryGetValue(record.Id, out var row))
            {
                row.GradeBefore = grade;
                row.ScoreBefore = score;
            }
            else
            {
                _repo.AddRecord(NewCalibrationRecord(session.Id, record));
            }
        }
        var snapshot = records.ToDictionary(r => r.Id, CalibrationRules.Before);
        foreach (var row in kept.Values)
            snapshot[row.RecordId] = (row.GradeBefore, row.ScoreBefore);

        session.GroupName = Required(request.GroupName, 300, "Tên nhóm chức danh/công việc tương đồng");
        session.HeldAt = request.HeldAt is { } held ? AsUtc(held) : null;
        session.Location = Optional(request.Location, 500, "Địa điểm");
        session.ChairName = Optional(request.ChairName, CalibrationRules.MaxNameLength, "Chủ trì") ?? string.Empty;
        session.ChairMemberId = request.ChairMemberId;
        session.SecretaryName = Optional(request.SecretaryName, CalibrationRules.MaxNameLength, "Thư ký") ?? string.Empty;
        session.SecretaryMemberId = request.SecretaryMemberId;
        session.ScopeNote = Optional(request.ScopeNote, CalibrationRules.MaxTextLength, "Phạm vi, số lượng hồ sơ");
        session.ExtraConclusion = Optional(request.ExtraConclusion, CalibrationRules.MaxTextLength, "Ghi chú kết luận");

        ReplaceAttendees(session, request.Attendees ?? new List<CalibrationAttendeeInputDto>());
        UpsertSummaryRows(session, request.Summary ?? new List<CalibrationSummaryRowInputDto>());
        ReplaceCases(session, request.Cases ?? new List<CalibrationCaseInputDto>(), snapshot, kept.Keys.ToHashSet());

        session.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(session, version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(session.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CalibrationSessionDto> ConcludeAsync(Guid id, ConcludeCalibrationDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = RequireVersion(request.Version);
        var session = await RequireSessionAsync(id, ct);
        EnsureNoOwnRecord(session);
        _guard.Ensure(PermissionCodes.EvaluationCalibrationConclude, UnitWriteTarget(session));
        if (session.PreparedById is { } preparer && preparer == _currentUser.UserId)
            throw new ForbiddenException("Người chốt biên bản phải khác người lập biên bản.");
        EnsureVersion(session, version);
        EnsureDraft(session, "Chốt biên bản");
        EnsureOpenPeriod(session);

        var problems = CalibrationRules.ConclusionProblems(session);
        if (problems.Count > 0)
            throw new ValidationException("Chốt biên bản không thành công. " + string.Join(" ", problems));

        var records = await _repo.GetRecordsAsync(session.Records.Select(r => r.RecordId).ToList(), ct);
        var byId = records.ToDictionary(r => r.Id);
        // Chốt lại: hồ sơ đã có quyết định giữ nguyên kết quả cũ, chỉ hồ sơ đang chờ được kiểm tra và áp kết quả.
        var kept = records.Where(r => IsKept(session, r)).Select(r => r.Id).ToHashSet();
        foreach (var row in session.Records)
        {
            var record = byId[row.RecordId];
            if (IsOwn(record))
                throw new ForbiddenException("Bạn không được chốt biên bản có hồ sơ của chính mình (xung đột lợi ích).");
            _guard.Ensure(PermissionCodes.EvaluationCalibrationConclude, AccessTarget.ForRecord(record).ForWrite());
            if (kept.Contains(record.Id))
                continue;
            EnsureStillEligible(record, "Chốt biên bản");
            var (grade, score) = CalibrationRules.Before(record);
            if (grade != row.GradeBefore || !CalibrationRules.SameScore(score, row.ScoreBefore))
                throw new ConflictException($"Chốt biên bản không thành công: kết quả thẩm định của {NameOf(record)} đã thay đổi sau khi đưa vào biên bản. "
                    + "Hãy lưu lại biên bản để cập nhật mốc trước họp thống nhất rồi chốt lại.");
        }

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        var applied = 0;
        foreach (var item in session.Cases.OrderBy(c => c.Order))
        {
            if (!CalibrationRules.IsChanged(item) || kept.Contains(item.RecordId))
                continue;
            ApplyToRecord(session, item, byId[item.RecordId], actorName, now);
            applied++;
        }

        session.Status = CalibrationStatus.Concluded;
        session.ConcludedById = _currentUser.UserId;
        session.ConcludedByName = actorName;
        session.ConcludedAt = now;
        session.ReopenReason = null;
        session.UpdatedAt = now;
        AddFormHistory(session, EvaluationFormAction.Conclude, actorName, now, null, null);
        if (applied > 0)
            AddFormHistory(session, EvaluationFormAction.Apply, actorName, now.AddMilliseconds(1), $"Áp kết quả họp thống nhất cho {applied} hồ sơ.", null);

        _unitOfWork.SetOriginalVersion(session, version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(session.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CalibrationSessionDto> ReopenAsync(Guid id, CalibrationReasonDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = RequireVersion(request.Version);
        var reason = RequireReason(request.Reason, "mở lại biên bản");
        var session = await RequireSessionAsync(id, ct);
        EnsureNoOwnRecord(session);
        _guard.Ensure(PermissionCodes.EvaluationCalibrationConclude, UnitWriteTarget(session));
        EnsureVersion(session, version);
        if (session.Status != CalibrationStatus.Concluded)
            throw new ConflictException($"Mở lại biên bản không thành công: biên bản đang ở trạng thái \"{StatusName(session.Status)}\", chỉ mở lại được biên bản đã chốt.");
        EnsureOpenPeriod(session);

        // Hồ sơ đã sang bước sau không còn cần hiệu chuẩn: giữ nguyên kết quả đã áp, chỉ thu hồi ở hồ sơ còn chờ quyết định.
        var records = (await _repo.GetRecordsAsync(session.Records.Select(r => r.RecordId).ToList(), ct))
            .Where(r => CalibrationRules.IsEligibleStatus(r.Status)).ToList();

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        var snapshot = JsonSerializer.Serialize(new
        {
            session.Revision,
            session.ConcludedAt,
            session.ConcludedByName,
            Cases = session.Cases.OrderBy(c => c.Order).Select(c => new
            {
                c.RecordId, c.GradeBefore, c.ScoreBefore, c.GradeAfter, c.ScoreAfter, c.Basis, c.ResponsibleName
            })
        });
        foreach (var record in records.Where(r => r.CalibrationSessionId == session.Id))
            RevokeFromRecord(session, record, reason, actorName, now);

        session.Status = CalibrationStatus.Draft;
        session.Revision++;
        session.ReopenReason = reason;
        session.ConcludedById = null;
        session.ConcludedByName = null;
        session.ConcludedAt = null;
        session.UpdatedAt = now;
        AddFormHistory(session, EvaluationFormAction.Reopen, actorName, now, reason, snapshot);

        _unitOfWork.SetOriginalVersion(session, version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(session.Id, ct);
    }

    /// <inheritdoc />
    public async Task<CalibrationSessionDto> VoidAsync(Guid id, CalibrationReasonDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var version = RequireVersion(request.Version);
        var reason = RequireReason(request.Reason, "hủy biên bản");
        var session = await RequireSessionAsync(id, ct);
        EnsureNoOwnRecord(session);
        _guard.Ensure(PermissionCodes.EvaluationCalibrationManage, UnitWriteTarget(session));
        EnsureVersion(session, version);
        if (session.Status == CalibrationStatus.Concluded)
            throw new ConflictException("Hủy biên bản không thành công: biên bản đã chốt, hãy mở lại (có lý do) trước khi hủy.");
        EnsureDraft(session, "Hủy biên bản");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        foreach (var row in session.Records)
            row.IsActive = false;
        session.Status = CalibrationStatus.Void;
        session.VoidReason = reason;
        session.UpdatedAt = now;
        AddFormHistory(session, EvaluationFormAction.Cancel, actorName, now, reason, null);

        _unitOfWork.SetOriginalVersion(session, version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(session.Id, ct);
    }

    #endregion

    #region Hồ sơ trong biên bản

    private async Task<List<EvaluationRecord>> ResolveRecordsAsync(CalibrationSession session, EvaluationPeriod period, IEnumerable<Guid>? recordIds,
        CancellationToken ct)
    {
        var ids = (recordIds ?? Array.Empty<Guid>()).Where(i => i != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            throw new ValidationException("Hãy chọn ít nhất một hồ sơ cho biên bản.");
        var records = await _repo.GetRecordsAsync(ids, ct);
        if (records.Count != ids.Count)
            throw new NotFoundException("Có hồ sơ được chọn không tồn tại. Hãy tải lại danh sách hồ sơ rồi chọn lại.");

        // Hồ sơ của chính người lập tự bỏ khỏi biên bản (xung đột lợi ích), không báo lỗi.
        records = records.Where(r => !IsOwn(r)).ToList();
        ids = records.Select(r => r.Id).ToList();
        if (records.Count == 0)
            throw new ValidationException("Hãy chọn ít nhất một hồ sơ cho biên bản.");

        var settings = ParseSettings(period);
        foreach (var record in records)
        {
            if (record.PeriodId != session.PeriodId)
                throw new ValidationException($"Hồ sơ của {NameOf(record)} không thuộc kỳ của biên bản.");
            _guard.Ensure(PermissionCodes.EvaluationCalibrationManage, AccessTarget.ForRecord(record).ForWrite());
            EnsureStillEligible(record, "Đưa hồ sơ vào biên bản");
            if (!CalibrationRules.HasAppraisal(record) || CalibrationRules.Before(record).Grade == EvaluationGrade.ChuaXepLoai)
                throw new ConflictException($"Đưa hồ sơ vào biên bản không thành công: hồ sơ của {NameOf(record)} chưa có kết quả thẩm định.");
            var gate = EvaluationMapping.ProfileOf(settings, record).GateModeOf(WorkflowGates.Calibration);
            if (gate == GateMode.Off)
                throw new ConflictException($"Đưa hồ sơ vào biên bản không thành công: diện quyết định của {NameOf(record)} không dùng bước họp thống nhất.");
            // Hồ sơ đã có trong biên bản giữ điều kiện cũ; hồ sơ mới phải khớp danh sách hồ sơ chọn được.
            if (session.Records.Any(r => r.RecordId == record.Id))
                continue;
            if (!CalibrationRules.IsCandidateStatus(record.Status))
                throw new ConflictException($"Đưa hồ sơ vào biên bản không thành công: hồ sơ của {NameOf(record)} đang ở bước \"{WorkflowSteps.StatusDisplayName(record.Status)}\". "
                    + "Chỉ đưa vào họp thống nhất hồ sơ đang chờ quyết định.");
            if (gate != GateMode.Required)
                throw new ConflictException($"Đưa hồ sơ vào biên bản không thành công: diện quyết định của {NameOf(record)} không bắt buộc họp thống nhất.");
        }

        var taken = await _repo.GetActiveSessionByRecordAsync(ids, session.Id, ct);
        if (taken.Count > 0)
        {
            var names = string.Join(", ", records.Where(r => taken.ContainsKey(r.Id)).Select(NameOf));
            throw new ConflictException($"Đưa hồ sơ vào biên bản không thành công: hồ sơ của {names} đã thuộc một biên bản họp thống nhất khác.");
        }
        return records.OrderBy(NameOf, StringComparer.CurrentCulture).ToList();
    }

    private static CalibrationRecord NewCalibrationRecord(Guid sessionId, EvaluationRecord record)
    {
        var (grade, score) = CalibrationRules.Before(record);
        return new CalibrationRecord { SessionId = sessionId, RecordId = record.Id, IsActive = true, GradeBefore = grade, ScoreBefore = score };
    }

    private void EnsureStillEligible(EvaluationRecord record, string action)
    {
        if (CalibrationRules.IsEligibleStatus(record.Status))
            return;
        // Hồ sơ đã có quyết định trước lần chốt đầu thì chưa từng được hiệu chuẩn: chỉ còn cách gỡ ra.
        var hint = record.Status is RecordStatus.AwaitingPublish or RecordStatus.Published
            ? "Hồ sơ đã có quyết định: gỡ hồ sơ này khỏi biên bản rồi lưu và chốt."
            : "Chỉ đưa vào họp thống nhất hồ sơ đã thẩm định và chưa ghi nhận quyết định.";
        throw new ConflictException($"{action} không thành công: hồ sơ của {NameOf(record)} đang ở bước \"{WorkflowSteps.StatusDisplayName(record.Status)}\". {hint}");
    }

    #endregion

    #region Nội dung biên bản

    private void ReplaceAttendees(CalibrationSession session, List<CalibrationAttendeeInputDto> input)
    {
        if (input.Count > CalibrationRules.MaxAttendees)
            throw new ValidationException($"Thành phần tham dự không được quá {CalibrationRules.MaxAttendees} người.");
        var rows = new List<CalibrationAttendee>();
        foreach (var (item, index) in input.Where(i => i != null).Select((i, n) => (i, n)))
        {
            var name = Optional(item.FullName, CalibrationRules.MaxNameLength, "Họ tên người tham dự");
            if (name == null)
                throw new ValidationException($"Người tham dự thứ {index + 1} chưa có họ tên.");
            rows.Add(new CalibrationAttendee
            {
                SessionId = session.Id,
                Order = index + 1,
                FullName = name,
                MemberId = item.MemberId,
                Capacity = Optional(item.Capacity, 300, "Thành phần tham dự"),
                DissentNote = Optional(item.DissentNote, CalibrationRules.MaxTextLength, "Ý kiến bảo lưu")
            });
        }
        _repo.RemoveChildren(session.Attendees.ToList(), Array.Empty<CalibrationSummaryRow>(), Array.Empty<CalibrationCase>());
        foreach (var row in rows)
            _repo.AddAttendee(row);
    }

    private void UpsertSummaryRows(CalibrationSession session, List<CalibrationSummaryRowInputDto> input)
    {
        var wanted = new Dictionary<EvaluationGrade, CalibrationSummaryRowInputDto>();
        foreach (var item in input.Where(i => i != null))
        {
            var grade = EvaluationMapping.ParseGrade(item.GradeCode);
            if (grade is not { } g || !CalibrationRules.SummaryGrades.Contains(g))
                throw new ValidationException("Dòng tổng hợp có mức xếp loại không hợp lệ.");
            if (!wanted.TryAdd(g, item))
                throw new ValidationException($"Mức \"{CriteriaSetContent.GradeName(g)}\" xuất hiện nhiều lần trong bảng tổng hợp.");
        }

        var existing = session.SummaryRows.ToDictionary(r => r.GradeCode, StringComparer.Ordinal);
        var stale = new List<CalibrationSummaryRow>();
        foreach (var row in existing.Values)
        {
            var grade = EvaluationMapping.ParseGrade(row.GradeCode);
            var text = grade is { } known && wanted.TryGetValue(known, out var kept) ? kept : null;
            if (text == null || IsBlank(text))
                stale.Add(row);
        }
        _repo.RemoveChildren(Array.Empty<CalibrationAttendee>(), stale, Array.Empty<CalibrationCase>());

        foreach (var (grade, item) in wanted.Where(w => !IsBlank(w.Value)))
        {
            var difference = Optional(item.NotableDifference, CalibrationRules.MaxTextLength, "Chênh lệch đáng chú ý");
            var conclusion = Optional(item.PreliminaryConclusion, CalibrationRules.MaxTextLength, "Kết luận sơ bộ");
            if (existing.TryGetValue(grade.ToString(), out var row))
            {
                row.NotableDifference = difference;
                row.PreliminaryConclusion = conclusion;
            }
            else
            {
                _repo.AddSummaryRow(new CalibrationSummaryRow
                {
                    SessionId = session.Id, GradeCode = grade.ToString(), NotableDifference = difference, PreliminaryConclusion = conclusion
                });
            }
        }
    }

    private static bool IsBlank(CalibrationSummaryRowInputDto item) =>
        string.IsNullOrWhiteSpace(item.NotableDifference) && string.IsNullOrWhiteSpace(item.PreliminaryConclusion);

    private void ReplaceCases(CalibrationSession session, List<CalibrationCaseInputDto> input,
        IReadOnlyDictionary<Guid, (EvaluationGrade Grade, double? Score)> snapshot, IReadOnlySet<Guid> kept)
    {
        var seen = new HashSet<Guid>();
        var rows = new List<CalibrationCase>();
        var keptCases = session.Cases.Where(c => kept.Contains(c.RecordId)).ToDictionary(c => c.RecordId);
        foreach (var (item, index) in input.Where(i => i != null).Select((i, n) => (i, n)))
        {
            if (!snapshot.TryGetValue(item.RecordId, out var before))
                throw new ValidationException($"Trường hợp thảo luận thứ {index + 1} không thuộc hồ sơ nào trong biên bản.");
            if (!seen.Add(item.RecordId))
                throw new ValidationException($"Trường hợp thảo luận thứ {index + 1} trùng hồ sơ với một trường hợp khác.");
            // Hồ sơ đã có quyết định: giữ nguyên trường hợp cũ, bỏ qua nội dung gửi lên.
            if (kept.Contains(item.RecordId))
            {
                if (keptCases.Remove(item.RecordId, out var old))
                    rows.Add(CopyCase(old));
                continue;
            }

            EvaluationGrade? gradeAfter = null;
            if (!string.IsNullOrWhiteSpace(item.GradeAfter))
            {
                var parsed = EvaluationMapping.ParseGrade(item.GradeAfter);
                if (parsed is not { } g || g == EvaluationGrade.ChuaXepLoai)
                    throw new ValidationException($"Mức sau họp thống nhất của trường hợp thứ {index + 1} không hợp lệ.");
                gradeAfter = g;
            }
            if (item.ScoreAfter is { } score && (double.IsNaN(score) || score < 0 || score > 100))
                throw new ValidationException($"Điểm sau họp thống nhất của trường hợp thứ {index + 1} phải từ 0 đến 100.");

            rows.Add(new CalibrationCase
            {
                SessionId = session.Id,
                Order = index + 1,
                RecordId = item.RecordId,
                GradeBefore = before.Grade,
                ScoreBefore = before.Score,
                Clarification = Optional(item.Clarification, CalibrationRules.MaxTextLength, "Nội dung làm rõ"),
                GradeAfter = gradeAfter,
                ScoreAfter = item.ScoreAfter is { } s ? Math.Round(s, 2) : null,
                Basis = Optional(item.Basis, CalibrationRules.MaxTextLength, "Căn cứ"),
                ResponsibleName = Optional(item.ResponsibleName, CalibrationRules.MaxNameLength, "Người chịu trách nhiệm"),
                ResponsibleMemberId = item.ResponsibleMemberId
            });
        }
        rows.AddRange(keptCases.Values.OrderBy(c => c.Order).Select(CopyCase));
        for (var i = 0; i < rows.Count; i++)
            rows[i].Order = i + 1;
        _repo.RemoveChildren(Array.Empty<CalibrationAttendee>(), Array.Empty<CalibrationSummaryRow>(), session.Cases.ToList());
        foreach (var row in rows)
            _repo.AddCase(row);
    }

    private static CalibrationCase CopyCase(CalibrationCase c) => new()
    {
        SessionId = c.SessionId,
        Order = c.Order,
        RecordId = c.RecordId,
        GradeBefore = c.GradeBefore,
        ScoreBefore = c.ScoreBefore,
        Clarification = c.Clarification,
        GradeAfter = c.GradeAfter,
        ScoreAfter = c.ScoreAfter,
        Basis = c.Basis,
        ResponsibleName = c.ResponsibleName,
        ResponsibleMemberId = c.ResponsibleMemberId
    };

    #endregion

    #region Áp dụng vào hồ sơ

    private void ApplyToRecord(CalibrationSession session, CalibrationCase item, EvaluationRecord record, string actorName, DateTime now)
    {
        var scoreBefore = record.EffectiveScore();
        var gradeBefore = record.EffectiveGrade();
        record.CalibratedScore = CalibrationRules.ScoreAfterOf(item);
        record.CalibratedGrade = CalibrationRules.GradeAfterOf(item);
        record.CalibrationSessionId = session.Id;
        record.CalibratedAt = now;
        record.UpdatedAt = now;

        var change = string.Create(CultureInfo.InvariantCulture,
            $"{CriteriaSetContent.GradeName(item.GradeBefore)} ({FormatScore(item.ScoreBefore)}) → {CriteriaSetContent.GradeName(CalibrationRules.GradeAfterOf(item))} ({FormatScore(CalibrationRules.ScoreAfterOf(item))})");
        AddRecordHistory(record, Truncate(item.Basis, MaxReasonLength), scoreBefore, gradeBefore,
            $"Họp thống nhất theo biên bản nhóm \"{session.GroupName}\": {change}. Người chịu trách nhiệm: {item.ResponsibleName}.", actorName, now);
    }

    private void RevokeFromRecord(CalibrationSession session, EvaluationRecord record, string reason, string actorName, DateTime now)
    {
        var scoreBefore = record.EffectiveScore();
        var gradeBefore = record.EffectiveGrade();
        record.ClearCalibration();
        record.UpdatedAt = now;
        AddRecordHistory(record, reason, scoreBefore, gradeBefore,
            $"Hủy kết quả họp thống nhất do mở lại biên bản nhóm \"{session.GroupName}\".", actorName, now);
    }

    private void AddRecordHistory(EvaluationRecord record, string? reason, double scoreBefore, EvaluationGrade gradeBefore, string comment,
        string actorName, DateTime now) =>
        _records.AddHistory(new EvaluationRecordHistory
        {
            RecordId = record.Id,
            FromStatus = record.Status,
            ToStatus = record.Status,
            Step = null,
            Action = WorkflowAction.Calibrate,
            Reason = reason,
            ScoreBefore = scoreBefore,
            ScoreAfter = record.EffectiveScore(),
            GradeBefore = gradeBefore,
            GradeAfter = record.EffectiveGrade(),
            ActorId = _currentUser.UserId,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            Comment = comment,
            CreatedAt = now
        });

    private static string FormatScore(double? score) => score.HasValue ? score.Value.ToString("0.##", CultureInfo.InvariantCulture) : "chưa có điểm";

    #endregion

    #region DTO

    private async Task<CalibrationSessionDto> BuildDtoAsync(CalibrationSession s, CancellationToken ct)
    {
        var seniority = await _records.GetHeldPositionsAsync(s.Records.Select(r => r.Record.MemberId).Distinct().ToList(), DateTime.UtcNow, ct);
        var rows = PositionRules.OrderBySeniority(s.Records, seniority, await _records.GetUnitOrderAsync(ct), r => r.Record.MemberId, r => r.Record.Department?.Name, r => r.Record.Member?.FullName);
        var cases = s.Cases.OrderBy(c => c.Order).ToList();
        var names = rows.ToDictionary(r => r.RecordId, r => NameOf(r.Record));
        var lines = CalibrationRules.Summarize(s.Records.ToList(), cases);
        var text = s.SummaryRows.ToDictionary(r => r.GradeCode, StringComparer.Ordinal);
        var total = s.Records.Count;

        var summary = lines.Select(l =>
        {
            text.TryGetValue(l.Grade.ToString(), out var t);
            return new CalibrationSummaryRowDto
            {
                GradeCode = l.Grade.ToString(),
                GradeName = CriteriaSetContent.GradeName(l.Grade),
                CountBefore = l.CountBefore,
                PercentBefore = l.PercentBefore,
                CountAfter = l.CountAfter,
                PercentAfter = l.PercentAfter,
                NotableDifference = t?.NotableDifference,
                PreliminaryConclusion = t?.PreliminaryConclusion
            };
        }).ToList();
        summary.Add(new CalibrationSummaryRowDto
        {
            GradeCode = "TOTAL",
            GradeName = "Tổng",
            CountBefore = total,
            PercentBefore = CalibrationRules.Percent(total, total),
            CountAfter = total,
            PercentAfter = CalibrationRules.Percent(total, total)
        });

        var target = UnitWriteTarget(s);
        var editable = s.Status == CalibrationStatus.Draft && s.Period.Status == PeriodStatus.Open;
        var isPreparer = s.PreparedById is { } p && p == _currentUser.UserId;
        var canConclude = editable && _guard.Can(PermissionCodes.EvaluationCalibrationConclude, target);
        // Thiếu nội dung thì ai xem cũng thấy lý do, để người lập biết cần bổ sung gì trước khi nhờ chốt.
        var blocked = editable ? CalibrationRules.ConclusionHint(s) : null;
        if (blocked == null && canConclude && isPreparer)
            blocked = "Người chốt biên bản phải khác người lập biên bản.";

        return new CalibrationSessionDto
        {
            Id = s.Id,
            Version = s.Version,
            PeriodId = s.PeriodId,
            PeriodName = s.Period.Name,
            DepartmentId = s.DepartmentId,
            DepartmentName = s.Department?.Name,
            PartyCellId = s.PartyCellId,
            PartyCellName = s.PartyCell?.Name,
            GroupName = s.GroupName,
            HeldAt = s.HeldAt,
            Location = s.Location,
            ChairName = s.ChairName,
            ChairMemberId = s.ChairMemberId,
            SecretaryName = s.SecretaryName,
            SecretaryMemberId = s.SecretaryMemberId,
            ScopeNote = s.ScopeNote,
            // Tên kỳ đứng giữa câu nên viết thường chữ đầu ("quý IV/2026").
            ScopeSuggestion = $"Kiểm tra {total} hồ sơ thuộc nhóm \"{s.GroupName}\", {LowerFirst(s.Period.Name)}.",
            ExtraConclusion = s.ExtraConclusion,
            Status = s.Status.ToString(),
            StatusName = StatusName(s.Status),
            Revision = s.Revision,
            PreparedByName = s.PreparedByName,
            PreparedAt = s.PreparedAt,
            ConcludedByName = s.ConcludedByName,
            ConcludedAt = s.ConcludedAt,
            ReopenReason = s.ReopenReason,
            VoidReason = s.VoidReason,
            Records = rows.Select(r => ToRecordDto(r, IsKept(s, r.Record))).ToList(),
            Attendees = s.Attendees.OrderBy(a => a.Order).Select(a => new CalibrationAttendeeDto
            {
                Order = a.Order, FullName = a.FullName, MemberId = a.MemberId, Capacity = a.Capacity, DissentNote = a.DissentNote
            }).ToList(),
            Summary = summary,
            Cases = cases.Select(c => new CalibrationCaseDto
            {
                Order = c.Order,
                RecordId = c.RecordId,
                FullName = names.GetValueOrDefault(c.RecordId, string.Empty),
                GradeBefore = EvaluationMapping.GradeCode(c.GradeBefore),
                ScoreBefore = c.ScoreBefore,
                Clarification = c.Clarification,
                GradeAfter = c.GradeAfter is { } g ? EvaluationMapping.GradeCode(g) : string.Empty,
                ScoreAfter = c.ScoreAfter,
                Basis = c.Basis,
                ResponsibleName = c.ResponsibleName,
                ResponsibleMemberId = c.ResponsibleMemberId,
                Changed = CalibrationRules.IsChanged(c)
            }).ToList(),
            CanEdit = editable && _guard.Can(PermissionCodes.EvaluationCalibrationManage, target),
            CanAttach = editable && _guard.Can(PermissionCodes.EvaluationCalibrationManage, target),
            CanVoid = editable && _guard.Can(PermissionCodes.EvaluationCalibrationManage, target),
            CanConclude = canConclude && !isPreparer,
            CanReopen = s.Status == CalibrationStatus.Concluded && s.Period.Status == PeriodStatus.Open
                && _guard.Can(PermissionCodes.EvaluationCalibrationConclude, target),
            ConcludeBlockedReason = blocked
        };
    }

    private static CalibrationRecordDto ToRecordDto(CalibrationRecord row, bool kept)
    {
        var r = row.Record;
        var (grade, score) = CalibrationRules.Before(r);
        return new CalibrationRecordDto
        {
            RecordId = r.Id,
            MemberId = r.MemberId,
            FullName = NameOf(r),
            DepartmentName = r.Department?.Name,
            PartyCellName = r.PartyCell?.Name,
            WorkflowProfileCode = r.WorkflowProfileCode,
            RecordStatus = r.Status.ToString(),
            GradeBefore = EvaluationMapping.GradeCode(row.GradeBefore),
            ScoreBefore = row.ScoreBefore,
            SelfScore = r.SelfScoredAt.HasValue ? r.TotalSelfScore : null,
            SelfGrade = r.SelfScoredAt.HasValue ? GradeOrEmpty(r.SelfProposedGrade) : string.Empty,
            CollectiveGrade = GradeOrEmpty(r.CollectiveProposedGrade),
            AppraisalScore = r.AppraisalScore,
            AppraisalGrade = GradeOrEmpty(r.AppraisalProposedGrade),
            CalibratedScore = r.CalibratedScore,
            CalibratedGrade = r.CalibratedGrade is { } c ? EvaluationMapping.GradeCode(c) : string.Empty,
            Kept = kept,
            SnapshotOutdated = !kept && (grade != row.GradeBefore || !CalibrationRules.SameScore(score, row.ScoreBefore))
        };
    }

    private static string GradeOrEmpty(EvaluationGrade grade) =>
        grade == EvaluationGrade.ChuaXepLoai ? string.Empty : EvaluationMapping.GradeCode(grade);

    #endregion

    #region Cổng hiệu chuẩn

    /// <inheritdoc />
    public async Task<CalibrationGateResult> EvaluateAsync(Guid recordId, CancellationToken ct = default)
    {
        var all = await EvaluateManyAsync(new[] { recordId }, ct);
        return all[recordId];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, CalibrationGateResult>> EvaluateManyAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        var result = recordIds.Distinct().ToDictionary(id => id, _ => new CalibrationGateResult(CalibrationGateState.NoSession, null));
        if (result.Count == 0)
            return result;

        var rows = await _repo.ListActiveRowsAsync(result.Keys.ToList(), ct);
        var concluded = rows.Select(r => r.Session)
            .Where(s => s.Status == CalibrationStatus.Concluded && s.ConcludedAt.HasValue)
            .DistinctBy(s => s.Id).ToList();
        // Hết hiệu lực xét riêng từng hồ sơ để hồ sơ khác trong biên bản được quyết định không khóa cả nhóm.
        var stale = new HashSet<(Guid SessionId, Guid RecordId)>();
        if (concluded.Count > 0)
        {
            var since = concluded.Min(s => s.ConcludedAt!.Value);
            var histories = await _repo.ListRecordHistorySinceAsync(
                concluded.SelectMany(s => s.Records.Select(r => r.RecordId)).Distinct().ToList(), since, ct);
            var byRecord = histories.ToLookup(h => h.RecordId);
            foreach (var session in concluded)
                foreach (var row in session.Records)
                    if (CalibrationRules.IsStale(session.ConcludedAt!.Value, byRecord[row.RecordId]))
                        stale.Add((session.Id, row.RecordId));
        }

        foreach (var row in rows)
        {
            var session = row.Session;
            var state = session.Status switch
            {
                CalibrationStatus.Concluded => stale.Contains((session.Id, row.RecordId)) ? CalibrationGateState.Stale : CalibrationGateState.Satisfied,
                CalibrationStatus.Draft => CalibrationGateState.Pending,
                _ => CalibrationGateState.NoSession
            };
            result[row.RecordId] = new CalibrationGateResult(state, session.Id);
        }
        return result;
    }

    #endregion

    #region Việc cần xử lý

    /// <inheritdoc />
    public async Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default)
    {
        var none = new List<WorkQueueGroupDto>();
        if (_currentUser.UserId is not { } userId
            || (!_guard.HasAny(PermissionCodes.EvaluationCalibrationManage) && !_guard.HasAny(PermissionCodes.EvaluationCalibrationConclude)))
            return none;

        var periodIds = (await _records.ListPeriodsAsync(ct))
            .Where(p => p.Status == PeriodStatus.Open && (periodId is null || periodId == Guid.Empty || p.Id == periodId))
            .Select(p => p.Id)
            .ToList();
        if (periodIds.Count == 0)
            return none;

        var sessions = (await _repo.ListForReconcludeAsync(periodIds, ct)).Where(s => !HasOwnRecord(s)).ToList();
        var concludedPending = sessions.Where(s => s.Status == CalibrationStatus.Concluded)
            .SelectMany(s => s.Records.Where(r => CalibrationRules.IsEligibleStatus(r.Record.Status)).Select(r => r.RecordId))
            .ToList();
        var gates = concludedPending.Count > 0
            ? await EvaluateManyAsync(concludedPending, ct)
            : new Dictionary<Guid, CalibrationGateResult>();

        var items = new List<WorkQueueItemDto>();
        foreach (var s in sessions)
        {
            // Còn hồ sơ đang làm lại các bước trước thẩm định thì chưa chốt lại được.
            if (s.Records.Any(r => !CalibrationRules.IsEligibleStatus(r.Record.Status) && !CalibrationRules.IsDecidedStatus(r.Record.Status)))
                continue;
            var pending = s.Records.Where(r => CalibrationRules.IsEligibleStatus(r.Record.Status)).Select(r => (Row: r, r.Record)).ToList();
            if (pending.Count == 0)
                continue;

            string permission, status, label;
            EvaluationRecord focus;
            if (s.Status == CalibrationStatus.Concluded)
            {
                var stale = pending.FirstOrDefault(p => gates.TryGetValue(p.Record.Id, out var g) && g.State == CalibrationGateState.Stale);
                if (stale.Record == null)
                    continue;
                (permission, status, label, focus) = (PermissionCodes.EvaluationCalibrationConclude, "ReopenNeeded", "Cần mở lại biên bản để chốt lại", stale.Record);
            }
            else
            {
                var outdated = pending.FirstOrDefault(p => IsOutdated(p.Row));
                if (outdated.Record != null || CalibrationRules.ConclusionProblems(s).Count > 0)
                    (permission, status, label, focus) = (PermissionCodes.EvaluationCalibrationManage, "UpdateNeeded", "Cần lưu lại biên bản trước khi chốt",
                        outdated.Record ?? pending[0].Record);
                else
                    (permission, status, label, focus) = (PermissionCodes.EvaluationCalibrationConclude, "ConcludeNeeded", "Chờ chốt lại biên bản", pending[0].Record);
            }

            // Người lập không chốt biên bản của mình.
            var skipPreparer = status == "ConcludeNeeded";
            if (skipPreparer && s.PreparedById == userId)
                continue;
            if (!_guard.Can(permission, UnitWriteTarget(s))
                || pending.Any(p => !_guard.Can(permission, AccessTarget.ForRecord(p.Record).ForWrite()))
                || !await IsAssigneeAsync(s, focus, permission, skipPreparer, userId, ct))
                continue;

            items.Add(new WorkQueueItemDto
            {
                RecordId = focus.Id,
                FormId = s.Id,
                Version = focus.Version,
                PeriodId = s.PeriodId,
                PeriodName = s.Period.Name,
                MemberId = focus.MemberId,
                FullName = NameOf(focus),
                DepartmentName = s.Department?.Name ?? focus.Department?.Name,
                PartyCellName = s.PartyCell?.Name ?? focus.PartyCell?.Name,
                ApprovalAuthority = focus.ApprovalAuthority.ToString(),
                WorkflowProfileName = focus.WorkflowProfileCode,
                Status = status,
                StatusDisplayName = $"{label} · {s.GroupName}",
                Mode = StepMode.Internal.ToString()
            });
        }

        if (items.Count == 0)
            return none;
        return new List<WorkQueueGroupDto>
        {
            new()
            {
                Step = WorkQueueGroup,
                StepName = "Họp thống nhất",
                Count = items.Count,
                Items = items.OrderBy(i => i.PeriodName).ThenBy(i => i.FullName).ToList()
            }
        };
    }

    private static bool IsOutdated(CalibrationRecord row)
    {
        var (grade, score) = CalibrationRules.Before(row.Record);
        return grade != row.GradeBefore || !CalibrationRules.SameScore(score, row.ScoreBefore);
    }

    /// <summary>Quy tắc giao việc như các bước: người gần hồ sơ nhất, bỏ chủ hồ sơ trong biên bản (và người lập khi chốt); không còn ai thì mọi người có quyền.</summary>
    private async Task<bool> IsAssigneeAsync(CalibrationSession session, EvaluationRecord record, string permission, bool skipPreparer, Guid userId,
        CancellationToken ct)
    {
        if (_stepPerformers == null)
            return true;
        var owners = session.Records.Select(r => r.Record.MemberId).ToHashSet();
        var nearest = (await _stepPerformers.ListAsync(permission, AccessTarget.ForRecord(record), ct))
            .Where(id => !owners.Contains(id) && !(skipPreparer && session.PreparedById == id))
            .ToList();
        return nearest.Count == 0 || nearest.Contains(userId);
    }

    #endregion

    #region Hỗ trợ

    private bool IsOwn(EvaluationRecord record) => _currentUser.UserId is { } id && record.MemberId == id;

    /// <summary>Biên bản đã chốt ít nhất một lần và hồ sơ đã có quyết định: dòng giữ nguyên khi sửa, chốt lại.</summary>
    private static bool IsKept(CalibrationSession session, EvaluationRecord record) =>
        session.Revision > 1 && CalibrationRules.IsDecidedStatus(record.Status);

    private bool HasOwnRecord(CalibrationSession session) => session.Records.Any(r => IsOwn(r.Record));

    private void EnsureNoOwnRecord(CalibrationSession session)
    {
        if (HasOwnRecord(session))
            throw new ForbiddenException("Bạn không được thao tác trên biên bản họp thống nhất có hồ sơ của chính mình (xung đột lợi ích).");
    }

    private void EnsureCanView(CalibrationSession session)
    {
        EnsureNoOwnRecord(session);
        var target = new AccessTarget(DepartmentId: session.DepartmentId, PartyCellId: session.PartyCellId);
        if (_guard.Can(PermissionCodes.EvaluationCalibrationManage, target)
            || _guard.Can(PermissionCodes.EvaluationCalibrationConclude, target)
            || _guard.Can(PermissionCodes.EvaluationRead, target))
            return;
        _guard.Ensure(PermissionCodes.EvaluationCalibrationManage, target);
    }

    private static AccessTarget UnitWriteTarget(CalibrationSession s) => AccessTarget.ForUnitsWrite(s.DepartmentId, s.PartyCellId);

    private static string NameOf(EvaluationRecord record) => record.Member?.FullName ?? record.MemberId.ToString();

    private static PeriodSettings ParseSettings(EvaluationPeriod period)
    {
        try
        {
            return period.GetSettings();
        }
        catch (FormatException)
        {
            throw new ConflictException("Cấu hình của kỳ đánh giá bị lỗi định dạng. Hãy liên hệ người quản lý kỳ để sửa cấu hình.");
        }
    }

    private static uint RequireVersion(uint? version) =>
        version ?? throw new ValidationException("Thiếu phiên bản dữ liệu của biên bản. Hãy tải lại biên bản rồi thực hiện lại.");

    private static void EnsureVersion(CalibrationSession session, uint version)
    {
        if (session.Version != version)
            throw new ConflictException("Biên bản đã được người khác cập nhật sau khi bạn mở. Hãy tải lại biên bản để xem dữ liệu mới nhất rồi thực hiện lại.");
    }

    private static void EnsureDraft(CalibrationSession session, string action)
    {
        if (session.Status != CalibrationStatus.Draft)
            throw new ConflictException($"{action} không thành công: biên bản đang ở trạng thái \"{StatusName(session.Status)}\", chỉ thao tác được trên bản nháp.");
    }

    private static void EnsureOpenPeriod(CalibrationSession session)
    {
        if (session.Period.Status != PeriodStatus.Open)
            throw new ConflictException("Chỉ thao tác trên biên bản họp thống nhất khi kỳ đánh giá đang mở.");
    }

    private static void EnsureEditable(CalibrationSession session)
    {
        EnsureDraft(session, "Lưu biên bản");
        EnsureOpenPeriod(session);
    }

    private static string RequireReason(string? value, string action)
    {
        var reason = value?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ValidationException($"Hãy nhập lý do {action}.");
        if (reason.Length > MaxReasonLength)
            throw new ValidationException($"Lý do không được dài quá {MaxReasonLength} ký tự.");
        return reason;
    }

    private static string Required(string? value, int max, string field) =>
        Optional(value, max, field) ?? throw new ValidationException($"Hãy nhập {field.ToLowerInvariant()}.");

    private static string? Optional(string? value, int max, string field)
    {
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
            return null;
        if (text.Length > max)
            throw new ValidationException($"{field} không được dài quá {max} ký tự.");
        return text;
    }

    private static string? Truncate(string? value, int max) => value is { } v && v.Length > max ? v[..max] : value;

    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private void AddFormHistory(CalibrationSession session, EvaluationFormAction action, string actorName, DateTime now, string? reason, string? snapshot) =>
        _repo.AddHistory(new EvaluationFormHistory
        {
            FormType = EvaluationFormType.M20,
            FormId = session.Id,
            Action = action,
            ByUserId = _currentUser.UserId,
            ByUserName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            At = now,
            Reason = reason,
            Snapshot = snapshot
        });

    private async Task<CalibrationSession> RequireSessionAsync(Guid id, CancellationToken ct) =>
        await _repo.FindAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy biên bản họp thống nhất.");

    private async Task<CalibrationSessionDto> ReloadAsync(Guid id, CancellationToken ct) => await BuildDtoAsync(await RequireSessionAsync(id, ct), ct);

    private async Task<string> ActorNameAsync(CancellationToken ct)
    {
        var id = _currentUser.UserId;
        var name = id.HasValue ? await _records.GetMemberNameAsync(id.Value, ct) : null;
        return name ?? _currentUser.UserName;
    }

    private static string LowerFirst(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToLower(text[0], CultureInfo.GetCultureInfo("vi-VN")) + text[1..];

    private static string StatusName(CalibrationStatus status) => status switch
    {
        CalibrationStatus.Draft => "Đang soạn",
        CalibrationStatus.Concluded => "Đã chốt",
        CalibrationStatus.Void => "Đã hủy",
        _ => status.ToString()
    };

    private static string ActionName(EvaluationFormAction action) => action switch
    {
        EvaluationFormAction.Create => "Lập biên bản",
        EvaluationFormAction.Conclude => "Chốt biên bản",
        EvaluationFormAction.Reopen => "Mở lại biên bản",
        EvaluationFormAction.Cancel => "Hủy biên bản",
        EvaluationFormAction.Apply => "Áp kết quả họp thống nhất",
        _ => action.ToString()
    };

    #endregion
}
