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

/// <summary>Phiếu ý kiến chuyên môn độc lập / đối soát dữ liệu chuyên ngành (Mẫu 03).</summary>
public interface IExpertCrosscheckService
{
    /// <summary>Khối trên trang hồ sơ: người có <c>evaluation.read</c> (không phải chủ hồ sơ) thấy mọi phiếu; cơ quan xác nhận chỉ thấy phiếu gửi đến mình.</summary>
    Task<RecordCrosschecksDto> GetByRecordAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Phiếu trong phạm vi xem của người hiện tại, kể cả phiếu gửi đến đơn vị xác nhận mà người đó được gán.</summary>
    Task<List<ExpertCrosscheckSummaryDto>> ListAsync(Guid? periodId, string? status, CancellationToken ct = default);

    Task<ExpertCrosscheckDto> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Người có <c>evaluation.crosscheck.request</c> trên hồ sơ, khi kỳ đang mở và hồ sơ ở giai đoạn thẩm định hoặc nhận xét.</summary>
    Task<ExpertCrosscheckDto> CreateAsync(Guid recordId, SaveCrosscheckRequestDto request, CancellationToken ct = default);

    /// <summary>Sửa nội dung yêu cầu khi cơ quan xác nhận chưa lập.</summary>
    Task<ExpertCrosscheckDto> UpdateRequestAsync(Guid id, SaveCrosscheckRequestDto request, CancellationToken ct = default);

    /// <summary>Lưu kết quả xác nhận; chưa gửi xác nhận.</summary>
    Task<ExpertCrosscheckDto> SaveResponseAsync(Guid id, SaveCrosscheckResponseDto request, CancellationToken ct = default);

    Task<ExpertCrosscheckDto> SubmitAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default);

    /// <summary>Khác người lập phiếu.</summary>
    Task<ExpertCrosscheckDto> ConfirmAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default);

    /// <summary>Bắt buộc lý do; phiếu về trạng thái đang lập.</summary>
    Task<ExpertCrosscheckDto> ReturnAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default);

    /// <summary>Bắt buộc lý do; chỉ khi phiếu chưa xác nhận.</summary>
    Task<ExpertCrosscheckDto> CancelAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default);
}

/// <summary>Chặn hoàn thành thẩm định (B3b) khi còn yêu cầu xác nhận đang mở.</summary>
public interface IExpertCrosscheckGate
{
    /// <summary>Ném <see cref="ConflictException"/> khi cổng đối soát của diện quyết định không tắt và còn phiếu chưa xác nhận, chưa hủy.</summary>
    Task EnsureNoOpenAsync(EvaluationRecord record, WorkflowProfile profile, CancellationToken ct = default);

    /// <summary>Lý do cổng đối soát đang chặn thẩm định; null khi không chặn.</summary>
    Task<string?> OpenBlockReasonAsync(EvaluationRecord record, WorkflowProfile profile, CancellationToken ct = default);
}

/// <summary>Nhóm việc "Phiếu xác nhận số liệu chờ lập / xác nhận".</summary>
public interface ICrosscheckWorkQueue
{
    /// <summary>Nhóm rỗng không trả về.</summary>
    Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default);
}

/// <summary>
/// Phiếu tùy chọn và chỉ để tham khảo: không tự đổi điểm. Quyền lập, xác nhận kiểm theo đơn vị xác nhận (không cần quyền xem hồ sơ);
/// chủ hồ sơ không xem và không thao tác phiếu về mình. Cơ quan ngoài hệ thống: người có quyền yêu cầu nhập thay.
/// </summary>
public sealed class ExpertCrosscheckService : IExpertCrosscheckService, IExpertCrosscheckGate, ICrosscheckWorkQueue
{
    public const string WorkQueueGroup = "CROSSCHECKS";

    private readonly IEvaluationWorkflowRepository _records;
    private readonly IExpertCrosscheckRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public ExpertCrosscheckService(
        IEvaluationWorkflowRepository records,
        IExpertCrosscheckRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser)
    {
        _records = records;
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
    }

    #region Đọc

    /// <inheritdoc />
    public async Task<RecordCrosschecksDto> GetByRecordAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await RequireRecordAsync(recordId, ct);
        if (IsOwner(record))
            throw new ForbiddenException("Phiếu xác nhận số liệu về hồ sơ của chính bạn chỉ dành cho người xử lý hồ sơ.");

        var all = await _repo.ListByRecordAsync(record.Id, ct);
        var visible = all.Where(CanView).ToList();
        if (visible.Count == 0 && !CanReadRecord(record) && !_guard.Can(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(record)))
            throw new ForbiddenException("Bạn không có quyền xem phiếu xác nhận số liệu của hồ sơ này (ngoài phạm vi được gán).");

        var profile = ProfileOf(record);
        var applicable = profile.GateModeOf(WorkflowGates.Crosscheck) != GateMode.Off;
        var blocked = applicable ? StateBlockReason(record) : "Diện quyết định của hồ sơ này không dùng xác nhận số liệu.";
        return new RecordCrosschecksDto
        {
            RecordId = record.Id,
            Applicable = applicable,
            CanRequest = blocked == null && _guard.Can(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(record)),
            BlockedReason = _guard.Can(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(record)) ? blocked : null,
            HasOpen = visible.Any(c => CrosscheckRules.IsOpen(c.Status)),
            Items = visible.Select(ToSummary).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<List<ExpertCrosscheckSummaryDto>> ListAsync(Guid? periodId, string? status, CancellationToken ct = default)
    {
        CrosscheckStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<CrosscheckStatus>(status.Trim(), true, out var s) || !Enum.IsDefined(s))
                throw new ValidationException("Trạng thái phiếu xác nhận số liệu không hợp lệ.");
            parsed = s;
        }

        var all = await _repo.ListAsync(periodId, parsed, ct);
        return all.Where(CanView).Select(ToSummary).ToList();
    }

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var crosscheck = await RequireAsync(id, ct);
        EnsureCanView(crosscheck);
        return await BuildAsync(crosscheck, ct);
    }

    #endregion

    #region Yêu cầu

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> CreateAsync(Guid recordId, SaveCrosscheckRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var record = await RequireRecordAsync(recordId, ct);
        EnsureNotOwner(record, "yêu cầu xác nhận số liệu");
        _guard.Ensure(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(record));

        if (ProfileOf(record).GateModeOf(WorkflowGates.Crosscheck) == GateMode.Off)
            throw new ConflictException("Diện quyết định của hồ sơ này không dùng xác nhận số liệu.");
        var blocked = StateBlockReason(record);
        if (blocked != null)
            throw new ConflictException(blocked);

        var content = await ValidateRequestAsync(record, request, ct);
        var existing = await _repo.ListByRecordAsync(record.Id, ct);
        if (existing.Any(c => CrosscheckRules.IsOpen(c.Status) && SameExpert(c, content) && CrosscheckRules.FieldsOverlap(c.Fields, content.FieldsJson)))
            throw new ConflictException("Hồ sơ đã có yêu cầu xác nhận đang mở gửi tới cùng đơn vị ở lĩnh vực này. Hãy xử lý hoặc hủy yêu cầu đó trước.");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        var crosscheck = new ExpertCrosscheck
        {
            PeriodId = record.PeriodId,
            RecordId = record.Id,
            DepartmentId = record.DepartmentId,
            PartyCellId = record.PartyCellId,
            ExpertDepartmentId = content.ExpertDepartmentId,
            ExpertAgencyName = content.ExpertAgencyName,
            Fields = content.FieldsJson,
            AddresseeName = content.AddresseeName,
            RequestReason = content.RequestReason,
            Status = CrosscheckStatus.Requested,
            RequestedById = _currentUser.UserId,
            RequestedByName = actorName,
            RequestedAt = now,
            CreatedAt = now
        };
        _repo.Add(crosscheck);
        var order = 1;
        foreach (var row in content.Items)
        {
            _repo.AddItem(new ExpertCrosscheckItem
            {
                CrosscheckId = crosscheck.Id, Order = order++, TaskId = row.TaskId, ItemName = row.ItemName, SelfData = row.SelfData
            });
        }
        AddHistory(crosscheck.Id, EvaluationFormAction.Create, actorName, null, null, now);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(crosscheck.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> UpdateRequestAsync(Guid id, SaveCrosscheckRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var crosscheck = await RequireAsync(id, ct);
        EnsureNotOwner(crosscheck.Record, "sửa yêu cầu xác nhận số liệu");
        _guard.Ensure(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(crosscheck.Record));
        EnsureVersion(crosscheck, request.Version);
        if (crosscheck.Status != CrosscheckStatus.Requested)
            throw new ConflictException("Chỉ sửa được yêu cầu khi cơ quan xác nhận chưa lập phiếu.");

        var content = await ValidateRequestAsync(crosscheck.Record, request, ct);
        var others = (await _repo.ListByRecordAsync(crosscheck.RecordId, ct)).Where(c => c.Id != crosscheck.Id);
        if (others.Any(c => CrosscheckRules.IsOpen(c.Status) && SameExpert(c, content) && CrosscheckRules.FieldsOverlap(c.Fields, content.FieldsJson)))
            throw new ConflictException("Hồ sơ đã có yêu cầu xác nhận đang mở gửi tới cùng đơn vị ở lĩnh vực này. Hãy xử lý hoặc hủy yêu cầu đó trước.");

        var now = DateTime.UtcNow;
        crosscheck.ExpertDepartmentId = content.ExpertDepartmentId;
        crosscheck.ExpertAgencyName = content.ExpertAgencyName;
        crosscheck.Fields = content.FieldsJson;
        crosscheck.AddresseeName = content.AddresseeName;
        crosscheck.RequestReason = content.RequestReason;
        foreach (var old in crosscheck.Items.ToList())
            _repo.RemoveItem(old);
        var order = 1;
        foreach (var row in content.Items)
        {
            _repo.AddItem(new ExpertCrosscheckItem
            {
                CrosscheckId = crosscheck.Id, Order = order++, TaskId = row.TaskId, ItemName = row.ItemName, SelfData = row.SelfData
            });
        }
        crosscheck.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(crosscheck, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(crosscheck.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> CancelAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var crosscheck = await RequireAsync(id, ct);
        EnsureNotOwner(crosscheck.Record, "hủy yêu cầu xác nhận số liệu");
        _guard.Ensure(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(crosscheck.Record));
        EnsureVersion(crosscheck, request.Version);
        if (!CrosscheckRules.IsOpen(crosscheck.Status))
            throw new ConflictException($"Phiếu đã ở trạng thái \"{CrosscheckRules.StatusName(crosscheck.Status)}\" nên không còn hủy yêu cầu.");
        var reason = RequireReason(request.Reason, "Hãy nhập lý do hủy yêu cầu xác nhận.");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        crosscheck.Status = CrosscheckStatus.Cancelled;
        crosscheck.CancelReason = reason;
        crosscheck.CancelledById = _currentUser.UserId;
        crosscheck.CancelledByName = actorName;
        crosscheck.CancelledAt = now;
        crosscheck.UpdatedAt = now;
        AddHistory(crosscheck.Id, EvaluationFormAction.Cancel, actorName, reason, ItemsSnapshot(crosscheck), now);
        _unitOfWork.SetOriginalVersion(crosscheck, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(crosscheck.Id, ct);
    }

    #endregion

    #region Lập và xác nhận

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> SaveResponseAsync(Guid id, SaveCrosscheckResponseDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var crosscheck = await RequireAsync(id, ct);
        EnsureCanRespond(crosscheck);
        EnsureVersion(crosscheck, request.Version);
        if (crosscheck.Status is not (CrosscheckStatus.Requested or CrosscheckStatus.InProgress))
            throw new ConflictException($"Phiếu đang ở trạng thái \"{CrosscheckRules.StatusName(crosscheck.Status)}\" nên đã khóa sửa kết quả xác nhận.");

        var byId = crosscheck.Items.ToDictionary(i => i.Id);
        var seen = new HashSet<Guid>();
        foreach (var input in request.Items ?? new List<CrosscheckResponseItemDto>())
        {
            if (input == null)
                continue;
            if (!byId.TryGetValue(input.Id, out var item))
                throw new ValidationException("Có dòng không thuộc phiếu xác nhận số liệu. Hãy tải lại phiếu rồi nhập lại.");
            if (!seen.Add(input.Id))
                throw new ValidationException($"Dòng \"{item.ItemName}\" được nhập nhiều lần.");
            ApplyResponse(item, input);
        }

        var now = DateTime.UtcNow;
        crosscheck.IssuedDate = request.IssuedDate ?? crosscheck.IssuedDate;
        crosscheck.Status = CrosscheckStatus.InProgress;
        crosscheck.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(crosscheck, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(crosscheck.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> SubmitAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var crosscheck = await RequireAsync(id, ct);
        EnsureCanRespond(crosscheck);
        EnsureVersion(crosscheck, request.Version);
        if (crosscheck.Status is not (CrosscheckStatus.Requested or CrosscheckStatus.InProgress))
            throw new ConflictException($"Phiếu đang ở trạng thái \"{CrosscheckRules.StatusName(crosscheck.Status)}\" nên không còn gửi xác nhận.");
        if (crosscheck.Items.Count == 0)
            throw new ValidationException("Phiếu chưa có dòng nào cần xác nhận.");
        foreach (var item in crosscheck.Items.OrderBy(i => i.Order))
        {
            var error = CrosscheckRules.ValidateItemForSubmit(item);
            if (error != null)
                throw new ValidationException(error);
        }

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        crosscheck.Status = CrosscheckStatus.Submitted;
        crosscheck.SubmittedById = _currentUser.UserId;
        crosscheck.SubmittedByName = actorName;
        crosscheck.SubmittedAt = now;
        crosscheck.ReturnReason = null;
        crosscheck.IssuedDate ??= DateOnly.FromDateTime(now);
        crosscheck.UpdatedAt = now;
        AddHistory(crosscheck.Id, EvaluationFormAction.Submit, actorName, null, null, now);
        _unitOfWork.SetOriginalVersion(crosscheck, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(crosscheck.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> ConfirmAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var crosscheck = await RequireAsync(id, ct);
        EnsureCanConfirm(crosscheck);
        EnsureVersion(crosscheck, request.Version);
        if (crosscheck.Status != CrosscheckStatus.Submitted)
            throw new ConflictException("Chỉ xác nhận được phiếu đã gửi xác nhận.");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        crosscheck.Status = CrosscheckStatus.Confirmed;
        crosscheck.ConfirmedById = _currentUser.UserId;
        crosscheck.ConfirmedByName = actorName;
        crosscheck.ConfirmedAt = now;
        crosscheck.UpdatedAt = now;
        AddHistory(crosscheck.Id, EvaluationFormAction.Confirm, actorName, null, null, now);
        _unitOfWork.SetOriginalVersion(crosscheck, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(crosscheck.Id, ct);
    }

    /// <inheritdoc />
    public async Task<ExpertCrosscheckDto> ReturnAsync(Guid id, CrosscheckActionDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var crosscheck = await RequireAsync(id, ct);
        EnsureCanConfirm(crosscheck);
        EnsureVersion(crosscheck, request.Version);
        if (crosscheck.Status != CrosscheckStatus.Submitted)
            throw new ConflictException("Chỉ trả lại được phiếu đã gửi xác nhận.");
        var reason = RequireReason(request.Reason, "Hãy nhập lý do trả lại để cơ quan xác nhận biết cần sửa gì.");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        AddHistory(crosscheck.Id, EvaluationFormAction.Return, actorName, reason, ItemsSnapshot(crosscheck), now);
        crosscheck.Status = CrosscheckStatus.InProgress;
        crosscheck.ReturnReason = reason;
        crosscheck.SubmittedById = null;
        crosscheck.SubmittedByName = null;
        crosscheck.SubmittedAt = null;
        crosscheck.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(crosscheck, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(crosscheck.Id, ct);
    }

    #endregion

    #region Cổng thẩm định và việc cần xử lý

    /// <inheritdoc />
    public async Task EnsureNoOpenAsync(EvaluationRecord record, WorkflowProfile profile, CancellationToken ct = default)
    {
        if (await OpenBlockReasonAsync(record, profile, ct) is { } reason)
            throw new ConflictException(reason);
    }

    /// <inheritdoc />
    public async Task<string?> OpenBlockReasonAsync(EvaluationRecord record, WorkflowProfile profile, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.GateModeOf(WorkflowGates.Crosscheck) == GateMode.Off)
            return null;
        var open = await _repo.CountOpenAsync(record.Id, ct);
        return open > 0
            ? $"Còn {open} phiếu xác nhận số liệu chờ lập hoặc xác nhận. Hãy chờ phiếu được xác nhận hoặc hủy yêu cầu (nêu lý do) trước khi hoàn thành thẩm định."
            : null;
    }

    /// <inheritdoc />
    public async Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default)
    {
        if (!_guard.HasAny(PermissionCodes.EvaluationCrosscheckRespond)
            && !_guard.HasAny(PermissionCodes.EvaluationCrosscheckConfirm)
            && !_guard.HasAny(PermissionCodes.EvaluationCrosscheckRequest))
            return new List<WorkQueueGroupDto>();

        var periods = await _records.ListPeriodsAsync(ct);
        var targetIds = periods
            .Where(p => p.Status == PeriodStatus.Open && (periodId is null || periodId == Guid.Empty || p.Id == periodId))
            .Select(p => p.Id)
            .ToList();
        if (targetIds.Count == 0)
            return new List<WorkQueueGroupDto>();

        var items = new List<WorkQueueItemDto>();
        foreach (var c in await _repo.ListOpenAsync(targetIds, ct))
        {
            var waiting = CanRespondNow(c) && c.Status is CrosscheckStatus.Requested or CrosscheckStatus.InProgress
                ? "Chờ lập phiếu"
                : c.Status == CrosscheckStatus.Submitted && CanConfirmNow(c) ? "Chờ xác nhận" : null;
            if (waiting == null)
                continue;
            var record = c.Record;
            items.Add(new WorkQueueItemDto
            {
                RecordId = record.Id,
                FormId = c.Id,
                Version = record.Version,
                PeriodId = record.PeriodId,
                PeriodName = record.Period.Name,
                MemberId = record.MemberId,
                FullName = record.Member?.FullName ?? string.Empty,
                DepartmentName = record.Department?.Name,
                PartyCellName = record.PartyCell?.Name,
                ApprovalAuthority = record.ApprovalAuthority.ToString(),
                WorkflowProfileName = record.WorkflowProfileCode,
                Status = c.Status.ToString(),
                StatusDisplayName = waiting,
                Mode = StepMode.Internal.ToString()
            });
        }

        if (items.Count == 0)
            return new List<WorkQueueGroupDto>();
        return new List<WorkQueueGroupDto>
        {
            new()
            {
                Step = WorkQueueGroup,
                StepName = "Phiếu xác nhận số liệu chờ lập, xác nhận",
                Count = items.Count,
                Items = items.OrderBy(i => i.PeriodName).ThenBy(i => i.FullName).ToList()
            }
        };
    }

    #endregion

    #region Quyền

    private bool IsOwner(EvaluationRecord record) => _currentUser.UserId is { } id && record.MemberId == id;

    private bool CanReadRecord(EvaluationRecord record) =>
        !IsOwner(record) && _guard.Can(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));

    /// <summary>Đơn vị xác nhận trong danh mục; cơ quan ngoài hệ thống dùng đơn vị của hồ sơ.</summary>
    private static AccessTarget ExpertTarget(ExpertCrosscheck c) => c.ExpertDepartmentId.HasValue
        ? new AccessTarget(DepartmentId: c.ExpertDepartmentId)
        : new AccessTarget(DepartmentId: c.DepartmentId, PartyCellId: c.PartyCellId);

    private bool CanRequestSide(ExpertCrosscheck c) =>
        _guard.Can(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(c.Record));

    /// <summary>Cơ quan trong danh mục tự lập; cơ quan ngoài hệ thống do người yêu cầu nhập thay.</summary>
    private bool CanRespondNow(ExpertCrosscheck c) =>
        !IsOwner(c.Record)
        && (c.ExpertDepartmentId.HasValue
            ? _guard.Can(PermissionCodes.EvaluationCrosscheckRespond, ExpertTarget(c))
            : CanRequestSide(c));

    private bool CanConfirmNow(ExpertCrosscheck c) =>
        !IsOwner(c.Record)
        && _guard.Can(PermissionCodes.EvaluationCrosscheckConfirm, ExpertTarget(c))
        && !(c.SubmittedById.HasValue && c.SubmittedById == _currentUser.UserId);

    private bool CanView(ExpertCrosscheck c) =>
        !IsOwner(c.Record)
        && (_guard.Can(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(c.Record))
            || CanRequestSide(c)
            || _guard.Can(PermissionCodes.EvaluationCrosscheckRespond, ExpertTarget(c))
            || _guard.Can(PermissionCodes.EvaluationCrosscheckConfirm, ExpertTarget(c)));

    private void EnsureNotOwner(EvaluationRecord record, string action)
    {
        if (IsOwner(record))
            throw new ForbiddenException($"Bạn không được {action} về hồ sơ của chính mình (xung đột lợi ích).");
    }

    private void EnsureCanView(ExpertCrosscheck c)
    {
        if (IsOwner(c.Record))
            throw new ForbiddenException("Phiếu xác nhận số liệu về hồ sơ của chính bạn chỉ dành cho người xử lý hồ sơ.");
        if (!CanView(c))
            throw new ForbiddenException("Bạn không có quyền xem phiếu xác nhận số liệu này (ngoài phạm vi được gán).");
    }

    private void EnsureCanRespond(ExpertCrosscheck c)
    {
        if (IsOwner(c.Record))
            throw new ForbiddenException("Bạn không được lập phiếu xác nhận số liệu về hồ sơ của chính mình (xung đột lợi ích).");
        if (c.ExpertDepartmentId.HasValue)
            _guard.Ensure(PermissionCodes.EvaluationCrosscheckRespond, ExpertTarget(c));
        else
            _guard.Ensure(PermissionCodes.EvaluationCrosscheckRequest, AccessTarget.ForRecord(c.Record));
    }

    private void EnsureCanConfirm(ExpertCrosscheck c)
    {
        if (IsOwner(c.Record))
            throw new ForbiddenException("Bạn không được xác nhận phiếu số liệu về hồ sơ của chính mình (xung đột lợi ích).");
        _guard.Ensure(PermissionCodes.EvaluationCrosscheckConfirm, ExpertTarget(c));
        if (c.SubmittedById.HasValue && c.SubmittedById == _currentUser.UserId)
            throw new ForbiddenException("Người lập phiếu không được tự xác nhận hoặc trả lại phiếu của mình.");
    }

    #endregion

    #region Kiểm tra dữ liệu

    private sealed record RequestContent(
        Guid? ExpertDepartmentId, string? ExpertAgencyName, string FieldsJson, string AddresseeName, string RequestReason,
        List<RequestRow> Items);

    private sealed record RequestRow(Guid? TaskId, string ItemName, string SelfData);

    private async Task<RequestContent> ValidateRequestAsync(EvaluationRecord record, SaveCrosscheckRequestDto request, CancellationToken ct)
    {
        Guid? departmentId = request.ExpertDepartmentId is { } d && d != Guid.Empty ? d : null;
        var agency = request.ExpertAgencyName?.Trim();
        if (string.IsNullOrEmpty(agency))
            agency = null;
        if (departmentId == null && agency == null)
            throw new ValidationException("Hãy chọn đơn vị xác nhận trong danh mục hoặc nhập tên cơ quan xác nhận.");
        if (departmentId != null && agency != null)
            throw new ValidationException("Chỉ chọn đơn vị xác nhận trong danh mục hoặc nhập tên cơ quan ngoài hệ thống, không dùng cả hai.");
        if (agency is { Length: > CrosscheckRules.MaxNameLength })
            throw new ValidationException($"Tên cơ quan xác nhận không được dài quá {CrosscheckRules.MaxNameLength} ký tự.");
        if (departmentId != null && await _repo.GetDepartmentNameAsync(departmentId.Value, ct) == null)
            throw new ValidationException("Đơn vị xác nhận không tồn tại. Hãy chọn lại.");

        var fields = (request.Fields ?? new List<string>()).Select(f => f?.Trim() ?? string.Empty).Distinct().ToList();
        if (fields.Count == 0)
            throw new ValidationException("Hãy chọn ít nhất một lĩnh vực chuyên môn cần xác nhận.");
        var unknown = fields.FirstOrDefault(f => !CrosscheckFields.IsKnown(f));
        if (unknown != null)
            throw new ValidationException($"Lĩnh vực chuyên môn \"{unknown}\" không hợp lệ.");

        var reason = request.RequestReason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ValidationException("Hãy nhập lý do, nội dung yêu cầu xác nhận.");
        if (reason.Length > CrosscheckRules.MaxTextLength)
            throw new ValidationException($"Lý do yêu cầu không được dài quá {CrosscheckRules.MaxTextLength} ký tự.");
        var addressee = request.AddresseeName?.Trim() ?? string.Empty;
        if (addressee.Length > CrosscheckRules.MaxNameLength)
            throw new ValidationException($"Nơi nhận không được dài quá {CrosscheckRules.MaxNameLength} ký tự.");

        var rows = request.Items ?? new List<CrosscheckRequestItemDto>();
        if (rows.Count == 0)
            throw new ValidationException("Hãy nhập ít nhất một chỉ tiêu, nhiệm vụ cần xác nhận.");
        if (rows.Count > CrosscheckRules.MaxItems)
            throw new ValidationException($"Mỗi phiếu có tối đa {CrosscheckRules.MaxItems} dòng.");

        var tasks = (await _records.GetTasksAsync(record.Id, ct)).ToDictionary(t => t.Id);
        var usedTasks = new HashSet<Guid>();
        var items = new List<RequestRow>();
        foreach (var row in rows)
        {
            if (row == null)
                continue;
            EvaluationTask? task = null;
            if (row.TaskId is { } taskId && taskId != Guid.Empty)
            {
                if (!tasks.TryGetValue(taskId, out task))
                    throw new ValidationException("Có sản phẩm không thuộc danh mục của hồ sơ. Hãy tải lại hồ sơ rồi chọn lại.");
                if (!usedTasks.Add(taskId))
                    throw new ValidationException($"Sản phẩm \"{task.TaskName}\" đã có trong phiếu, không nhập trùng.");
            }
            var name = row.ItemName?.Trim();
            if (string.IsNullOrEmpty(name))
                name = task?.TaskName;
            if (string.IsNullOrEmpty(name))
                throw new ValidationException("Mỗi dòng cần xác nhận phải có tên chỉ tiêu, nhiệm vụ.");
            if (name.Length > CrosscheckRules.MaxNameLength)
                throw new ValidationException($"Tên chỉ tiêu không được dài quá {CrosscheckRules.MaxNameLength} ký tự.");
            var self = row.SelfData?.Trim() ?? string.Empty;
            if (self.Length > CrosscheckRules.MaxTextLength)
                throw new ValidationException($"Dữ liệu tự khai của \"{name}\" không được dài quá {CrosscheckRules.MaxTextLength} ký tự.");
            items.Add(new RequestRow(task?.Id, name, self));
        }

        return new RequestContent(departmentId, agency, CrosscheckRules.FieldsToJson(fields), addressee, reason, items);
    }

    private static bool SameExpert(ExpertCrosscheck c, RequestContent content) => content.ExpertDepartmentId.HasValue
        ? c.ExpertDepartmentId == content.ExpertDepartmentId
        : c.ExpertDepartmentId == null
            && string.Equals(c.ExpertAgencyName, content.ExpertAgencyName, StringComparison.OrdinalIgnoreCase);

    private static void ApplyResponse(ExpertCrosscheckItem item, CrosscheckResponseItemDto input)
    {
        item.NotApplicable = input.NotApplicable;
        if (input.NotApplicable)
        {
            item.SourceData = string.Empty;
            item.Result = null;
            item.AdjustmentType = CrosscheckAdjustment.None;
            item.AdjustmentPoints = null;
            item.BasisText = item.BasisDocNumber = item.BasisAgency = string.Empty;
            item.BasisDocDate = null;
            return;
        }

        item.SourceData = Limit(input.SourceData, CrosscheckRules.MaxTextLength, $"Kết quả xác nhận của \"{item.ItemName}\"");
        if (string.IsNullOrWhiteSpace(input.Result))
        {
            item.Result = null;
        }
        else
        {
            if (!Enum.TryParse<CrosscheckResult>(input.Result.Trim(), true, out var result) || !Enum.IsDefined(result))
                throw new ValidationException($"Kết luận của \"{item.ItemName}\" chỉ là đạt chuẩn, vượt chuẩn hoặc có khiếm khuyết.");
            item.Result = result;
        }

        var adjustment = CrosscheckAdjustment.None;
        if (!string.IsNullOrWhiteSpace(input.AdjustmentType)
            && (!Enum.TryParse(input.AdjustmentType.Trim(), true, out adjustment) || !Enum.IsDefined(adjustment)))
            throw new ValidationException($"Đề xuất điểm của \"{item.ItemName}\" chỉ là không đề xuất, giảm điểm hoặc ghi nhận vượt chuẩn.");
        if (input.AdjustmentPoints is { } points && (double.IsNaN(points) || double.IsInfinity(points) || points < 0 || points > 100))
            throw new ValidationException($"Số điểm đề xuất của \"{item.ItemName}\" phải từ 0 đến 100.");
        item.AdjustmentType = adjustment;
        item.AdjustmentPoints = adjustment == CrosscheckAdjustment.Decrease ? input.AdjustmentPoints : null;
        item.BasisText = Limit(input.BasisText, CrosscheckRules.MaxTextLength, $"Căn cứ của \"{item.ItemName}\"");
        item.BasisDocNumber = Limit(input.BasisDocNumber, 200, $"Số văn bản căn cứ của \"{item.ItemName}\"");
        item.BasisDocDate = input.BasisDocDate;
        item.BasisAgency = Limit(input.BasisAgency, CrosscheckRules.MaxNameLength, $"Cơ quan ban hành căn cứ của \"{item.ItemName}\"");
    }

    private static string Limit(string? value, int max, string label)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length > max)
            throw new ValidationException($"{label} không được dài quá {max} ký tự.");
        return text;
    }

    private static string RequireReason(string? reason, string emptyMessage)
    {
        var text = reason?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new ValidationException(emptyMessage);
        if (text.Length > CrosscheckRules.MaxReasonLength)
            throw new ValidationException($"Lý do không được dài quá {CrosscheckRules.MaxReasonLength} ký tự.");
        return text;
    }

    private static void EnsureVersion(ExpertCrosscheck crosscheck, uint? version)
    {
        if (version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu của phiếu. Hãy tải lại phiếu rồi thực hiện lại.");
        if (crosscheck.Version != version.Value)
            throw new ConflictException("Phiếu đã được người khác cập nhật sau khi bạn mở. Hãy tải lại phiếu để xem dữ liệu mới nhất rồi thực hiện lại.");
    }

    /// <summary>Null nếu được phép yêu cầu.</summary>
    private static string? StateBlockReason(EvaluationRecord record)
    {
        if (record.Period.Status != PeriodStatus.Open)
            return "Chỉ yêu cầu xác nhận khi kỳ đánh giá đang mở.";
        if (record.Status is not (RecordStatus.AwaitingAppraisal or RecordStatus.AwaitingDirectorReview))
            return "Chỉ yêu cầu xác nhận khi hồ sơ đang ở bước thẩm định hoặc nhận xét, đề xuất.";
        return null;
    }

    #endregion

    #region Hỗ trợ

    private static WorkflowProfile ProfileOf(EvaluationRecord record)
    {
        PeriodSettings settings;
        try
        {
            settings = record.Period.GetSettings();
        }
        catch (FormatException)
        {
            throw new ConflictException("Cấu hình của kỳ đánh giá bị lỗi định dạng. Hãy liên hệ người quản lý kỳ để sửa cấu hình.");
        }
        return EvaluationMapping.ProfileOf(settings, record);
    }

    private void AddHistory(Guid crosscheckId, EvaluationFormAction action, string actorName, string? reason, string? snapshot, DateTime now) =>
        _repo.AddHistory(new EvaluationFormHistory
        {
            FormType = EvaluationFormType.M03,
            FormId = crosscheckId,
            Action = action,
            ByUserId = _currentUser.UserId,
            ByUserName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            At = now,
            Reason = reason,
            Snapshot = snapshot
        });

    /// <summary>Nội dung các dòng trước khi trả lại hoặc hủy.</summary>
    private static string ItemsSnapshot(ExpertCrosscheck c) => JsonSerializer.Serialize(
        c.Items.OrderBy(i => i.Order).Select(i => new
        {
            i.ItemName, i.SelfData, i.SourceData, Result = i.Result?.ToString(), Adjustment = i.AdjustmentType.ToString(), i.AdjustmentPoints,
            i.BasisText, i.BasisDocNumber, i.BasisDocDate, i.BasisAgency, i.NotApplicable
        }));

    private async Task<EvaluationRecord> RequireRecordAsync(Guid recordId, CancellationToken ct) =>
        await _records.FindRecordAsync(recordId, ct)
        ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

    private async Task<ExpertCrosscheck> RequireAsync(Guid id, CancellationToken ct) =>
        await _repo.FindAsync(id, ct)
        ?? throw new NotFoundException($"Không tìm thấy phiếu xác nhận số liệu với Id: {id}.");

    private async Task<ExpertCrosscheckDto> ReloadAsync(Guid id, CancellationToken ct) =>
        await BuildAsync(await RequireAsync(id, ct), ct);

    private async Task<string> ActorNameAsync(CancellationToken ct)
    {
        var id = _currentUser.UserId;
        var name = id.HasValue ? await _records.GetMemberNameAsync(id.Value, ct) : null;
        return name ?? _currentUser.UserName;
    }

    private async Task<ExpertCrosscheckDto> BuildAsync(ExpertCrosscheck c, CancellationToken ct)
    {
        var history = await _repo.ListHistoryAsync(c.Id, ct);
        var open = CrosscheckRules.IsOpen(c.Status);
        var canRespond = CanRespondNow(c) && c.Status is CrosscheckStatus.Requested or CrosscheckStatus.InProgress;
        var canConfirm = c.Status == CrosscheckStatus.Submitted && CanConfirmNow(c);
        var dto = new ExpertCrosscheckDto
        {
            AddresseeName = c.AddresseeName,
            RequestReason = c.RequestReason,
            EnteredOnBehalf = !c.ExpertDepartmentId.HasValue,
            SubmittedByName = c.SubmittedByName,
            ConfirmedByName = c.ConfirmedByName,
            IssuedDate = c.IssuedDate,
            ReturnReason = c.ReturnReason,
            CancelReason = c.CancelReason,
            CancelledByName = c.CancelledByName,
            CancelledAt = c.CancelledAt,
            Items = c.Items.OrderBy(i => i.Order).Select(ToItemDto).ToList(),
            History = history.Select(h => new ExpertCrosscheckHistoryDto
            {
                Action = h.Action.ToString(),
                ActionName = ActionName(h.Action),
                ByUserName = h.ByUserName,
                At = h.At,
                Reason = h.Reason
            }).ToList(),
            CanEditRequest = c.Status == CrosscheckStatus.Requested && CanRequestSide(c),
            CanAttach = c.Status is CrosscheckStatus.Requested or CrosscheckStatus.InProgress && c.Record.Period?.Status == PeriodStatus.Open
                && !IsOwner(c.Record) && (CanRequestSide(c) || (c.ExpertDepartmentId.HasValue && CanRespondNow(c))),
            CanRespond = canRespond,
            CanSubmit = canRespond,
            CanConfirm = canConfirm,
            CanReturn = canConfirm,
            CanCancel = open && CanRequestSide(c)
        };
        FillSummary(dto, c);
        return dto;
    }

    private static string ActionName(EvaluationFormAction action) => action switch
    {
        EvaluationFormAction.Create => "Yêu cầu xác nhận",
        EvaluationFormAction.Submit => "Gửi xác nhận",
        EvaluationFormAction.Confirm => "Xác nhận",
        EvaluationFormAction.Return => "Trả lại",
        EvaluationFormAction.Cancel => "Hủy yêu cầu",
        _ => action.ToString()
    };

    private static ExpertCrosscheckSummaryDto ToSummary(ExpertCrosscheck c)
    {
        var dto = new ExpertCrosscheckSummaryDto();
        FillSummary(dto, c);
        return dto;
    }

    private static void FillSummary(ExpertCrosscheckSummaryDto dto, ExpertCrosscheck c)
    {
        var fields = CrosscheckRules.FieldsFromJson(c.Fields);
        dto.Id = c.Id;
        dto.Version = c.Version;
        dto.PeriodId = c.PeriodId;
        dto.PeriodName = c.Record.Period?.Name ?? string.Empty;
        dto.RecordId = c.RecordId;
        dto.MemberFullName = c.Record.Member?.FullName ?? string.Empty;
        dto.DepartmentName = c.Record.Department?.Name;
        dto.PartyCellName = c.Record.PartyCell?.Name;
        dto.ExpertDepartmentId = c.ExpertDepartmentId;
        dto.ExpertUnitName = c.ExpertDepartment?.Name ?? c.ExpertAgencyName ?? string.Empty;
        dto.Fields = fields;
        dto.FieldNames = fields.Select(CrosscheckFields.DisplayName).ToList();
        dto.Status = c.Status.ToString();
        dto.StatusName = CrosscheckRules.StatusName(c.Status);
        dto.ItemCount = c.Items.Count;
        dto.RequestedByName = c.RequestedByName;
        dto.RequestedAt = c.RequestedAt;
        dto.SubmittedAt = c.SubmittedAt;
        dto.ConfirmedAt = c.ConfirmedAt;
    }

    private static ExpertCrosscheckItemDto ToItemDto(ExpertCrosscheckItem i) => new()
    {
        Id = i.Id,
        Order = i.Order,
        TaskId = i.TaskId,
        ItemName = i.ItemName,
        SelfData = i.SelfData,
        SourceData = i.SourceData,
        Result = i.Result?.ToString(),
        ResultName = i.Result is { } r ? CrosscheckRules.ResultName(r) : null,
        AdjustmentType = i.AdjustmentType.ToString(),
        AdjustmentPoints = i.AdjustmentPoints,
        AdjustmentText = CrosscheckRules.FormatAdjustment(i),
        BasisText = i.BasisText,
        BasisDocNumber = i.BasisDocNumber,
        BasisDocDate = i.BasisDocDate,
        BasisAgency = i.BasisAgency,
        NotApplicable = i.NotApplicable
    };

    #endregion
}
