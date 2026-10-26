using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Phiếu kiểm soát tính hợp lệ, công bằng và nhất quán (Mẫu 19).</summary>
public interface IControlCheckService
{
    /// <summary>Phiếu trong phạm vi người xem; chủ hồ sơ không thấy phiếu chứa hồ sơ của mình.</summary>
    Task<List<ControlCheckSummaryDto>> ListAsync(Guid? periodId, CancellationToken ct = default);

    Task<ControlCheckDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lập phiếu nháp với đủ mười mục chưa chọn kết quả.</summary>
    Task<ControlCheckDto> CreateAsync(CreateControlCheckDto request, CancellationToken ct = default);

    /// <summary>Lưu bản nháp (kết quả từng mục, kết luận, danh sách hồ sơ).</summary>
    Task<ControlCheckDto> UpdateAsync(Guid id, UpdateControlCheckDto request, CancellationToken ct = default);

    /// <summary>Kiểm tra đủ mười mục và kết luận rồi chuyển sang chờ xác nhận.</summary>
    Task<ControlCheckDto> SubmitAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default);

    /// <summary>Người xác nhận phải khác người lập.</summary>
    Task<ControlCheckDto> ConfirmAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default);

    /// <summary>Phiếu chờ xác nhận về nháp; bắt buộc lý do.</summary>
    Task<ControlCheckDto> ReturnAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default);

    /// <summary>Phiếu đã xác nhận về nháp, tăng Revision; bắt buộc lý do.</summary>
    Task<ControlCheckDto> ReopenAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default);

    /// <summary>Hủy phiếu và giải phóng hồ sơ; bắt buộc lý do.</summary>
    Task<ControlCheckDto> CancelAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default);
}

public enum ControlGateState
{
    /// <summary>Có phiếu Đủ điều kiện, đã xác nhận, còn hiệu lực.</summary>
    Satisfied = 0,

    /// <summary>Hồ sơ chưa thuộc phiếu nào còn hiệu lực.</summary>
    NoCheck = 1,

    /// <summary>Phiếu còn đang soạn hoặc chờ xác nhận.</summary>
    Pending = 2,

    /// <summary>Phiếu đã xác nhận nhưng kết luận Cần hoàn thiện.</summary>
    NeedsWork = 3,

    /// <summary>Phiếu Đủ điều kiện nhưng hồ sơ trong phiếu đã đổi sau lúc xác nhận.</summary>
    Stale = 4
}

/// <summary>Kết quả kiểm tra điều kiện phiếu Mẫu 19 của một hồ sơ.</summary>
public sealed record ControlGateResult(ControlGateState State, Guid? CheckId)
{
    public bool IsSatisfied => State == ControlGateState.Satisfied;

    /// <summary>Lý do hiển thị khi chưa đạt; null khi đạt.</summary>
    public string? Reason => State switch
    {
        ControlGateState.NoCheck => "Hồ sơ chưa thuộc phiếu kiểm soát nào còn hiệu lực.",
        ControlGateState.Pending => "Phiếu kiểm soát của hồ sơ chưa được xác nhận.",
        ControlGateState.NeedsWork => "Phiếu kiểm soát kết luận cần hoàn thiện trước khi trình.",
        ControlGateState.Stale => "Hồ sơ đã thay đổi sau khi phiếu kiểm soát được xác nhận; cần xác nhận lại phiếu.",
        _ => null
    };
}

/// <summary>Cổng kiểm soát trước phê duyệt: chỉ đọc, không kiểm quyền (người gọi là luồng nghiệp vụ).</summary>
public interface IControlCheckGate
{
    Task<ControlGateResult> EvaluateAsync(Guid recordId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, ControlGateResult>> EvaluateManyAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);
}

/// <summary>
/// Mỗi hồ sơ thuộc tối đa một phiếu còn hiệu lực; người xác nhận khác người lập; quyền kiểm trên từng hồ sơ trong phiếu.
/// Phiếu đã xác nhận hết hiệu lực khi lịch sử hồ sơ trong phiếu đổi kết quả hoặc bước sau lúc xác nhận.
/// </summary>
public sealed class ControlCheckService : IControlCheckService, IControlCheckGate
{
    private readonly IEvaluationWorkflowRepository _records;
    private readonly IControlCheckRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly ICalibrationGate? _calibrationGate;

    public ControlCheckService(
        IEvaluationWorkflowRepository records,
        IControlCheckRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        ICalibrationGate? calibrationGate = null)
    {
        _records = records;
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
        _calibrationGate = calibrationGate;
    }

    #region Đọc

    /// <inheritdoc />
    public async Task<List<ControlCheckSummaryDto>> ListAsync(Guid? periodId, CancellationToken ct = default)
    {
        var scope = _guard.GetScope(PermissionCodes.EvaluationControlPerform)
            .Union(_guard.GetScope(PermissionCodes.EvaluationControlConfirm))
            .Union(_guard.GetScope(PermissionCodes.EvaluationRead));
        var me = _currentUser.UserId;

        var visible = (await _repo.ListAsync(periodId == Guid.Empty ? null : periodId, ct))
            .Where(c => scope.Matches(null, c.DepartmentId, c.PartyCellId) && !HasOwnRecord(c, me))
            .ToList();
        var stale = await StaleSetAsync(visible, ct);
        return visible.Select(c => ToSummary(c, new ControlCheckSummaryDto(), stale.Any(x => x.CheckId == c.Id))).ToList();
    }

    /// <inheritdoc />
    public async Task<ControlCheckDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var check = await RequireAsync(id, ct);
        EnsureCanView(check);
        return await BuildAsync(check, ct);
    }

    private async Task<ControlCheckDto> BuildAsync(ControlCheck check, CancellationToken ct)
    {
        var staleRows = await StaleSetAsync(new[] { check }, ct);
        var stale = staleRows.Count > 0;
        var history = await _repo.ListHistoryAsync(check.Id, ct);
        var dto = ToSummary(check, new ControlCheckDto(), stale);
        dto.ItemsVersion = check.ItemsVersion;
        dto.NeedsWorkContent = check.NeedsWorkContent;
        dto.ReturnReason = check.ReturnReason;
        dto.Items = check.Items.OrderBy(i => i.ItemCode, StringComparer.Ordinal).Select(i => new ControlCheckItemDto
        {
            ItemCode = i.ItemCode,
            ItemText = i.ItemText,
            Result = i.Result?.ToString(),
            ResultName = i.Result is { } r ? ControlCheckRules.ResultName(r) : null,
            Evidence = i.Evidence
        }).ToList();
        dto.Records = check.Records.OrderBy(r => r.Record.Member?.FullName, StringComparer.CurrentCulture).Select(r => new ControlCheckRecordDto
        {
            RecordId = r.RecordId,
            MemberId = r.Record.MemberId,
            FullName = r.Record.Member?.FullName ?? string.Empty,
            DepartmentName = r.Record.Department?.Name,
            PartyCellName = r.Record.PartyCell?.Name,
            IsCurrent = r.IsCurrent,
            IsStale = staleRows.Contains((check.Id, r.RecordId)),
            GradeAtCheck = r.GradeAtCheck.ToString(),
            ScoreAtCheck = r.ScoreAtCheck
        }).ToList();
        dto.History = history.Select(h => new ControlCheckHistoryDto
        {
            Action = h.Action.ToString(), ByUserName = h.ByUserName, At = h.At, Reason = h.Reason
        }).ToList();

        var records = check.Records.Select(r => r.Record).ToList();
        var me = _currentUser.UserId;
        var canPerform = CanAll(PermissionCodes.EvaluationControlPerform, records);
        var canConfirm = CanAll(PermissionCodes.EvaluationControlConfirm, records);
        dto.CanEdit = check.Status == ControlCheckStatus.Draft && canPerform;
        dto.CanSubmit = dto.CanEdit;
        dto.CanConfirm = check.Status == ControlCheckStatus.Submitted && canConfirm && me != check.PerformedById;
        dto.CanReturn = check.Status == ControlCheckStatus.Submitted && canConfirm;
        dto.CanReopen = check.Status == ControlCheckStatus.Confirmed && canConfirm;
        dto.CanCancel = check.Status != ControlCheckStatus.Cancelled && (canPerform || canConfirm);
        return dto;
    }

    private static T ToSummary<T>(ControlCheck c, T dto, bool stale) where T : ControlCheckSummaryDto
    {
        dto.Id = c.Id;
        dto.Version = c.Version;
        dto.PeriodId = c.PeriodId;
        dto.PeriodName = c.Period?.Name ?? string.Empty;
        dto.GroupName = c.GroupName;
        dto.DepartmentId = c.DepartmentId;
        dto.PartyCellId = c.PartyCellId;
        dto.Status = c.Status.ToString();
        dto.StatusName = ControlCheckRules.StatusName(c.Status);
        dto.Conclusion = c.Conclusion?.ToString();
        dto.ConclusionName = c.Conclusion is { } k ? ControlCheckRules.ConclusionName(k) : null;
        dto.Revision = c.Revision;
        dto.RecordCount = c.Records.Count;
        dto.PerformedByName = c.PerformedByName;
        dto.PerformedAt = c.PerformedAt;
        dto.ConfirmedByName = c.ConfirmedByName;
        dto.ConfirmedAt = c.ConfirmedAt;
        dto.IsStale = stale;
        dto.CreatedAt = c.CreatedAt;
        return dto;
    }

    #endregion

    #region Ghi

    /// <inheritdoc />
    public async Task<ControlCheckDto> CreateAsync(CreateControlCheckDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var groupName = request.GroupName?.Trim();
        if (string.IsNullOrEmpty(groupName))
            throw new ValidationException("Hãy nhập nhóm chức danh/phạm vi rà soát.");
        if (groupName.Length > ControlCheckRules.MaxGroupNameLength)
            throw new ValidationException($"Nhóm chức danh/phạm vi rà soát không được dài quá {ControlCheckRules.MaxGroupNameLength} ký tự.");
        var ids = (request.RecordIds ?? new List<Guid>()).Distinct().ToList();
        if (ids.Count == 0)
            throw new ValidationException("Hãy chọn ít nhất một hồ sơ cho phiếu kiểm soát.");

        var records = await LoadRecordsAsync(ids, ct);
        if (records.Any(r => r.PeriodId != request.PeriodId))
            throw new ValidationException("Mọi hồ sơ trong phiếu phải thuộc kỳ đánh giá đã chọn.");
        if (records[0].Period.Status != PeriodStatus.Open)
            throw new ConflictException("Chỉ lập phiếu kiểm soát khi kỳ đánh giá đang mở.");
        EnsureNotOwner(records);
        EnsureAll(PermissionCodes.EvaluationControlPerform, records);

        await ReleaseOrRejectAsync(records, null, ct);

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        var check = new ControlCheck
        {
            PeriodId = request.PeriodId,
            GroupName = groupName,
            DepartmentId = request.DepartmentId ?? Common(records.Select(r => r.DepartmentId)),
            PartyCellId = request.PartyCellId ?? Common(records.Select(r => r.PartyCellId)),
            Status = ControlCheckStatus.Draft,
            ItemsVersion = ControlCheckRules.ItemsVersion,
            Revision = 1,
            PerformedById = _currentUser.UserId,
            PerformedByName = actorName,
            CreatedAt = now
        };
        foreach (var (code, text) in ControlCheckRules.Items)
            check.Items.Add(new ControlCheckItem { CheckId = check.Id, ItemCode = code, ItemText = text });
        foreach (var record in records)
            check.Records.Add(NewRow(check, record));
        _repo.Add(check);
        AddHistory(check.Id, EvaluationFormAction.Create, null, null, actorName, now);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(check.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ControlCheckDto> UpdateAsync(Guid id, UpdateControlCheckDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await RequireAsync(id, ct);
        EnsureNoOwnRecord(check);
        RequireVersion(request.Version);
        if (check.Status != ControlCheckStatus.Draft)
            throw new ConflictException("Chỉ sửa được phiếu đang soạn. Hãy trả lại hoặc mở lại phiếu trước khi sửa.");
        EnsureAll(PermissionCodes.EvaluationControlPerform, check.Records.Select(r => r.Record));
        CheckVersion(check, request.Version);

        // Đổi danh sách hồ sơ trước vì có thể lưu trung gian khi nhả hồ sơ khỏi phiếu hết hiệu lực.
        if (request.RecordIds != null)
            await ReplaceRecordsAsync(check, request.RecordIds.Distinct().ToList(), ct);

        if (request.GroupName != null)
        {
            var groupName = request.GroupName.Trim();
            if (groupName.Length == 0)
                throw new ValidationException("Hãy nhập nhóm chức danh/phạm vi rà soát.");
            if (groupName.Length > ControlCheckRules.MaxGroupNameLength)
                throw new ValidationException($"Nhóm chức danh/phạm vi rà soát không được dài quá {ControlCheckRules.MaxGroupNameLength} ký tự.");
            check.GroupName = groupName;
        }

        foreach (var input in request.Items ?? new List<ControlCheckItemInputDto>())
        {
            var item = check.Items.FirstOrDefault(i => i.ItemCode == input.ItemCode?.Trim());
            if (item == null)
                throw new ValidationException($"Mục kiểm soát \"{input.ItemCode}\" không tồn tại.");
            var evidence = input.Evidence?.Trim();
            if (evidence is { Length: > ControlCheckRules.MaxTextLength })
                throw new ValidationException($"Minh chứng/biện pháp của mục {item.ItemCode} không được dài quá {ControlCheckRules.MaxTextLength} ký tự.");
            item.Result = ParseResult(input.Result, item.ItemCode);
            item.Evidence = string.IsNullOrEmpty(evidence) ? null : evidence;
        }

        if (request.Conclusion != null)
            check.Conclusion = ParseConclusion(request.Conclusion);
        if (request.NeedsWorkContent != null)
        {
            var content = request.NeedsWorkContent.Trim();
            if (content.Length > ControlCheckRules.MaxTextLength)
                throw new ValidationException($"Nội dung cần hoàn thiện không được dài quá {ControlCheckRules.MaxTextLength} ký tự.");
            check.NeedsWorkContent = content.Length == 0 ? null : content;
        }

        check.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.SetOriginalVersion(check, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ControlCheckDto> SubmitAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await RequireAsync(id, ct);
        EnsureNoOwnRecord(check);
        RequireVersion(request.Version);
        if (check.Status != ControlCheckStatus.Draft)
            throw new ConflictException("Chỉ gửi xác nhận được phiếu đang soạn.");
        var records = check.Records.Select(r => r.Record).ToList();
        EnsureAll(PermissionCodes.EvaluationControlPerform, records);
        CheckVersion(check, request.Version);

        if (check.Records.Count == 0)
            throw new ValidationException("Phiếu chưa có hồ sơ nào.");
        var released = check.Records.Where(r => !r.IsCurrent).Select(r => r.Record.Member?.FullName).ToList();
        if (released.Count > 0)
            throw new ConflictException($"Hồ sơ đã thuộc phiếu kiểm soát khác: {string.Join(", ", released)}. Hãy gỡ khỏi phiếu này.");
        var error = ControlCheckRules.ValidateForSubmit(
            check.Items.Select(i => (i.ItemCode, i.Result, i.Evidence)).ToList(), check.Conclusion, check.NeedsWorkContent);
        if (error != null)
            throw new ValidationException(error);
        await EnsureCalibratedAsync(check, "Gửi xác nhận", ct);

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        foreach (var row in check.Records)
            Snapshot(row, row.Record);
        if (check.Conclusion == ControlConclusion.Eligible)
            check.NeedsWorkContent = null;
        check.Status = ControlCheckStatus.Submitted;
        check.PerformedById = _currentUser.UserId;
        check.PerformedByName = actorName;
        check.PerformedAt = now;
        check.ReturnReason = null;
        check.UpdatedAt = now;
        AddHistory(check.Id, EvaluationFormAction.Submit, null, null, actorName, now);
        _unitOfWork.SetOriginalVersion(check, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ControlCheckDto> ConfirmAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await RequireAsync(id, ct);
        EnsureNoOwnRecord(check);
        RequireVersion(request.Version);
        EnsureAll(PermissionCodes.EvaluationControlConfirm, check.Records.Select(r => r.Record));
        CheckVersion(check, request.Version);
        if (check.Status != ControlCheckStatus.Submitted)
            throw new ConflictException("Chỉ xác nhận được phiếu đang chờ xác nhận.");
        if (_currentUser.UserId is null || _currentUser.UserId == check.PerformedById)
            throw new ForbiddenException("Người xác nhận phải khác người lập phiếu kiểm soát.");
        if (check.Records.Any(r => !r.IsCurrent))
            throw new ConflictException("Phiếu có hồ sơ đã thuộc phiếu kiểm soát khác. Hãy trả lại phiếu để người lập gỡ hồ sơ đó.");
        await EnsureCalibratedAsync(check, "Xác nhận phiếu", ct);

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        check.Status = ControlCheckStatus.Confirmed;
        check.ConfirmedById = _currentUser.UserId;
        check.ConfirmedByName = actorName;
        check.ConfirmedAt = now;
        check.UpdatedAt = now;
        AddHistory(check.Id, EvaluationFormAction.Confirm, TrimReason(request.Reason), null, actorName, now);
        _unitOfWork.SetOriginalVersion(check, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ControlCheckDto> ReturnAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await RequireAsync(id, ct);
        EnsureNoOwnRecord(check);
        RequireVersion(request.Version);
        EnsureAll(PermissionCodes.EvaluationControlConfirm, check.Records.Select(r => r.Record));
        CheckVersion(check, request.Version);
        if (check.Status != ControlCheckStatus.Submitted)
            throw new ConflictException("Chỉ trả lại được phiếu đang chờ xác nhận.");
        var reason = RequireReason(request.Reason, "trả lại");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        check.Status = ControlCheckStatus.Draft;
        check.ReturnReason = reason;
        check.UpdatedAt = now;
        AddHistory(check.Id, EvaluationFormAction.Return, reason, null, actorName, now);
        _unitOfWork.SetOriginalVersion(check, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ControlCheckDto> ReopenAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await RequireAsync(id, ct);
        EnsureNoOwnRecord(check);
        RequireVersion(request.Version);
        EnsureAll(PermissionCodes.EvaluationControlConfirm, check.Records.Select(r => r.Record));
        CheckVersion(check, request.Version);
        if (check.Status != ControlCheckStatus.Confirmed)
            throw new ConflictException("Chỉ mở lại được phiếu đã xác nhận.");
        var reason = RequireReason(request.Reason, "mở lại");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        var snapshot = JsonSerializer.Serialize(new
        {
            check.Revision,
            check.Conclusion,
            check.NeedsWorkContent,
            check.ConfirmedByName,
            check.ConfirmedAt,
            Items = check.Items.OrderBy(i => i.ItemCode, StringComparer.Ordinal)
                .Select(i => new { i.ItemCode, i.Result, i.Evidence })
        });
        check.Status = ControlCheckStatus.Draft;
        check.Revision++;
        check.ConfirmedById = null;
        check.ConfirmedByName = null;
        check.ConfirmedAt = null;
        check.ReturnReason = reason;
        check.UpdatedAt = now;
        AddHistory(check.Id, EvaluationFormAction.Reopen, reason, snapshot, actorName, now);
        _unitOfWork.SetOriginalVersion(check, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(id, ct);
    }

    /// <inheritdoc />
    public async Task<ControlCheckDto> CancelAsync(Guid id, ControlCheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var check = await RequireAsync(id, ct);
        EnsureNoOwnRecord(check);
        RequireVersion(request.Version);
        var records = check.Records.Select(r => r.Record).ToList();
        if (!CanAll(PermissionCodes.EvaluationControlPerform, records))
            EnsureAll(PermissionCodes.EvaluationControlConfirm, records);
        CheckVersion(check, request.Version);
        if (check.Status == ControlCheckStatus.Cancelled)
            throw new ConflictException("Phiếu đã được hủy.");
        var reason = RequireReason(request.Reason, "hủy");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        check.Status = ControlCheckStatus.Cancelled;
        foreach (var row in check.Records)
            row.IsCurrent = false;
        check.UpdatedAt = now;
        AddHistory(check.Id, EvaluationFormAction.Cancel, reason, null, actorName, now);
        _unitOfWork.SetOriginalVersion(check, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(id, ct);
    }

    #endregion

    /// <summary>Diện quyết định bắt buộc hiệu chuẩn thì mọi hồ sơ trong phiếu phải thuộc biên bản hiệu chuẩn đã chốt.</summary>
    private async Task EnsureCalibratedAsync(ControlCheck check, string action, CancellationToken ct)
    {
        if (_calibrationGate == null || check.Records.Count == 0)
            return;
        var period = await _records.FindPeriodAsync(check.PeriodId, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {check.PeriodId}.");
        PeriodSettings settings;
        try
        {
            settings = period.GetSettings();
        }
        catch (FormatException)
        {
            throw new ConflictException("Cấu hình của kỳ đánh giá bị lỗi định dạng. Hãy liên hệ người quản lý kỳ để sửa cấu hình.");
        }

        var current = check.Records.Where(r => r.IsCurrent).Select(r => r.Record).ToList();
        var results = await _calibrationGate.EvaluateManyAsync(current.Select(r => r.Id).ToList(), ct);
        var missing = ApprovalGatePolicy.Uncalibrated(settings, current, results)
            .Select(r => r.Member?.FullName ?? r.Id.ToString()).ToList();
        if (missing.Count > 0)
            throw new ConflictException($"{action} không thành công: hồ sơ của {string.Join(", ", missing)} chưa thuộc biên bản hiệu chuẩn đã chốt còn hiệu lực.");
    }

    #region Cổng kiểm soát

    /// <inheritdoc />
    public async Task<ControlGateResult> EvaluateAsync(Guid recordId, CancellationToken ct = default)
    {
        var all = await EvaluateManyAsync(new[] { recordId }, ct);
        return all[recordId];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ControlGateResult>> EvaluateManyAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        var result = recordIds.Distinct().ToDictionary(id => id, _ => new ControlGateResult(ControlGateState.NoCheck, null));
        if (result.Count == 0)
            return result;

        var rows = await _repo.ListCurrentRowsAsync(result.Keys.ToList(), ct);
        var checks = rows.Select(r => r.Check).DistinctBy(c => c.Id).ToList();
        var stale = await StaleSetAsync(checks, ct);
        foreach (var row in rows)
        {
            var check = row.Check;
            var state = check.Status switch
            {
                ControlCheckStatus.Confirmed when check.Conclusion == ControlConclusion.Eligible =>
                    stale.Contains((check.Id, row.RecordId)) ? ControlGateState.Stale : ControlGateState.Satisfied,
                ControlCheckStatus.Confirmed => ControlGateState.NeedsWork,
                ControlCheckStatus.Draft or ControlCheckStatus.Submitted => ControlGateState.Pending,
                _ => ControlGateState.NoCheck
            };
            result[row.RecordId] = new ControlGateResult(state, check.Id);
        }
        return result;
    }

    /// <summary>Cặp (phiếu, hồ sơ) của phiếu đã xác nhận mà hồ sơ đó đã đổi sau lúc xác nhận; xét riêng từng hồ sơ để hồ sơ khác được quyết định không khóa cả phiếu.</summary>
    private async Task<HashSet<(Guid CheckId, Guid RecordId)>> StaleSetAsync(IEnumerable<ControlCheck> checks, CancellationToken ct)
    {
        var confirmed = checks.Where(c => c.Status == ControlCheckStatus.Confirmed && c.ConfirmedAt.HasValue).ToList();
        var stale = new HashSet<(Guid CheckId, Guid RecordId)>();
        if (confirmed.Count == 0)
            return stale;

        var since = confirmed.Min(c => c.ConfirmedAt!.Value);
        var histories = await _repo.ListRecordHistorySinceAsync(
            confirmed.SelectMany(c => c.Records.Select(r => r.RecordId)).Distinct().ToList(), since, ct);
        var byRecord = histories.ToLookup(h => h.RecordId);
        foreach (var check in confirmed)
            foreach (var row in check.Records)
                if (ControlCheckRules.IsStale(check.ConfirmedAt!.Value, byRecord[row.RecordId]))
                    stale.Add((check.Id, row.RecordId));
        return stale;
    }

    #endregion

    #region Hỗ trợ

    private async Task<ControlCheck> RequireAsync(Guid id, CancellationToken ct) =>
        await _repo.FindAsync(id, ct) ?? throw new NotFoundException($"Không tìm thấy phiếu kiểm soát với Id: {id}.");

    private async Task<ControlCheckDto> ReloadAsync(Guid id, CancellationToken ct) => await BuildAsync(await RequireAsync(id, ct), ct);

    private async Task<List<EvaluationRecord>> LoadRecordsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var records = new List<EvaluationRecord>();
        foreach (var id in ids)
            records.Add(await RequireRecordAsync(id, ct));
        return records;
    }

    private async Task<EvaluationRecord> RequireRecordAsync(Guid id, CancellationToken ct) =>
        await _records.FindRecordAsync(id, ct) ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {id}.");

    /// <summary>Chủ của bất kỳ hồ sơ nào trong phiếu không được thao tác trên phiếu (xung đột lợi ích).</summary>
    private void EnsureNoOwnRecord(ControlCheck check)
    {
        if (HasOwnRecord(check, _currentUser.UserId))
            throw new ForbiddenException("Bạn không được thao tác trên phiếu kiểm soát có hồ sơ của chính mình (xung đột lợi ích).");
    }

    private void EnsureNotOwner(IEnumerable<EvaluationRecord> records)
    {
        if (_currentUser.UserId is { } me && records.Any(r => r.MemberId == me))
            throw new ForbiddenException("Bạn không được lập phiếu kiểm soát có hồ sơ của chính mình (xung đột lợi ích).");
    }

    private void EnsureCanView(ControlCheck check)
    {
        if (HasOwnRecord(check, _currentUser.UserId))
            throw new ForbiddenException("Bạn không được xem phiếu kiểm soát có hồ sơ của chính mình.");
        var target = new AccessTarget(null, check.DepartmentId, check.PartyCellId);
        if (!_guard.Can(PermissionCodes.EvaluationControlPerform, target)
            && !_guard.Can(PermissionCodes.EvaluationControlConfirm, target)
            && !_guard.Can(PermissionCodes.EvaluationRead, target))
            throw new ForbiddenException("Bạn không có quyền xem phiếu kiểm soát này.");
    }

    private static bool HasOwnRecord(ControlCheck check, Guid? me) =>
        me is { } id && check.Records.Any(r => r.Record != null && r.Record.MemberId == id);

    private void EnsureAll(string permission, IEnumerable<EvaluationRecord> records)
    {
        foreach (var record in records)
            _guard.Ensure(permission, AccessTarget.ForRecord(record).ForWrite());
    }

    private bool CanAll(string permission, IEnumerable<EvaluationRecord> records) =>
        records.All(r => _guard.Can(permission, AccessTarget.ForRecord(r).ForWrite()));

    private static Guid? Common(IEnumerable<Guid?> values)
    {
        var distinct = values.Distinct().ToList();
        return distinct.Count == 1 ? distinct[0] : null;
    }

    private static ControlCheckRecord NewRow(ControlCheck check, EvaluationRecord record)
    {
        var row = new ControlCheckRecord { CheckId = check.Id, RecordId = record.Id, Record = record, IsCurrent = true };
        Snapshot(row, record);
        return row;
    }

    private static void Snapshot(ControlCheckRecord row, EvaluationRecord record)
    {
        row.GradeAtCheck = record.EffectiveGrade();
        row.ScoreAtCheck = record.EffectiveScore();
    }

    /// <summary>Hồ sơ đang thuộc phiếu khác: nhả nếu phiếu đó đã hết hiệu lực, ngược lại báo lỗi.</summary>
    private async Task ReleaseOrRejectAsync(IReadOnlyCollection<EvaluationRecord> records, Guid? exceptCheckId, CancellationToken ct)
    {
        var rows = (await _repo.ListCurrentRowsAsync(records.Select(r => r.Id).ToList(), ct))
            .Where(r => r.CheckId != exceptCheckId).ToList();
        if (rows.Count == 0)
            return;

        var stale = await StaleSetAsync(rows.Select(r => r.Check).DistinctBy(c => c.Id).ToList(), ct);
        var blocked = rows.Where(r => !stale.Contains((r.CheckId, r.RecordId))).ToList();
        if (blocked.Count > 0)
        {
            var names = blocked.Select(r => records.First(x => x.Id == r.RecordId).Member?.FullName ?? r.RecordId.ToString());
            throw new ConflictException(
                $"Hồ sơ đã thuộc phiếu kiểm soát khác còn hiệu lực: {string.Join(", ", names)}. Hãy hủy hoặc gỡ hồ sơ khỏi phiếu đó trước.");
        }

        foreach (var row in rows)
            row.IsCurrent = false;
        // Lưu riêng để chỉ mục duy nhất theo hồ sơ không vướng khi thêm dòng mới.
        await _unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ReplaceRecordsAsync(ControlCheck check, List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            throw new ValidationException("Phiếu phải có ít nhất một hồ sơ.");
        var existing = check.Records.Select(r => r.RecordId).ToHashSet();
        var added = ids.Where(id => !existing.Contains(id)).ToList();
        var addedRecords = await LoadRecordsAsync(added, ct);
        if (addedRecords.Any(r => r.PeriodId != check.PeriodId))
            throw new ValidationException("Mọi hồ sơ trong phiếu phải thuộc kỳ đánh giá của phiếu.");
        EnsureNotOwner(addedRecords);
        EnsureAll(PermissionCodes.EvaluationControlPerform, addedRecords);

        foreach (var row in check.Records.Where(r => !ids.Contains(r.RecordId)).ToList())
        {
            check.Records.Remove(row);
            _repo.RemoveRecord(row);
        }
        await ReleaseOrRejectAsync(addedRecords, check.Id, ct);
        foreach (var record in addedRecords)
        {
            var row = NewRow(check, record);
            check.Records.Add(row);
            _repo.AddRecord(row);
        }
    }

    private void RequireVersion(uint? version)
    {
        if (version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu của phiếu. Hãy tải lại phiếu rồi thực hiện lại.");
    }

    private void CheckVersion(ControlCheck check, uint? version)
    {
        if (check.Version != version)
            throw new ConflictException("Phiếu đã được người khác cập nhật sau khi bạn mở. Hãy tải lại phiếu để xem dữ liệu mới nhất rồi thực hiện lại.");
    }

    private static string RequireReason(string? reason, string action)
    {
        var text = TrimReason(reason);
        if (string.IsNullOrEmpty(text))
            throw new ValidationException($"Hãy nhập lý do {action} phiếu.");
        return text;
    }

    private static string? TrimReason(string? reason)
    {
        var text = reason?.Trim();
        if (text is { Length: > 2000 })
            throw new ValidationException("Lý do không được dài quá 2000 ký tự.");
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private static ControlItemResult? ParseResult(string? value, string code)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (Enum.TryParse<ControlItemResult>(value.Trim(), true, out var result) && Enum.IsDefined(result))
            return result;
        throw new ValidationException($"Kết quả của mục {code} không hợp lệ: chọn Đạt, Chưa đạt hoặc K/AD.");
    }

    private static ControlConclusion? ParseConclusion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (Enum.TryParse<ControlConclusion>(value.Trim(), true, out var conclusion) && Enum.IsDefined(conclusion))
            return conclusion;
        throw new ValidationException("Kết luận không hợp lệ: chọn đủ điều kiện trình phê duyệt hoặc cần hoàn thiện.");
    }

    private void AddHistory(Guid checkId, EvaluationFormAction action, string? reason, string? snapshot, string actorName, DateTime now) =>
        _repo.AddHistory(new EvaluationFormHistory
        {
            FormType = EvaluationFormType.M19,
            FormId = checkId,
            Action = action,
            ByUserId = _currentUser.UserId,
            ByUserName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            At = now,
            Reason = reason,
            Snapshot = snapshot
        });

    private async Task<string> ActorNameAsync(CancellationToken ct)
    {
        var id = _currentUser.UserId;
        var name = id.HasValue ? await _records.GetMemberNameAsync(id.Value, ct) : null;
        return name ?? _currentUser.UserName;
    }

    #endregion
}
