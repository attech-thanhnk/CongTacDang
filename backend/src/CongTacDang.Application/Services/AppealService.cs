using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

/// <summary>Kiến nghị sau công bố.</summary>
public interface IAppealService
{
    Task<RecordAppealsDto> GetRecordAppealsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Hồ sơ đã công bố, không có kiến nghị khác đang xử lý.</summary>
    Task<AppealDto> SubmitAsync(Guid recordId, SubmitAppealRequestDto request, CancellationToken ct = default);

    Task<AppealDto> StartReviewAsync(Guid appealId, VersionedRequestDto request, CancellationToken ct = default);

    /// <summary>Bắt buộc nêu căn cứ.</summary>
    Task<AppealDto> ResolveAsync(Guid appealId, ResolveAppealRequestDto request, CancellationToken ct = default);

    /// <summary>Dùng thao tác "Mở lại" hiện có (<c>evaluation.reopen</c>); chấp nhận kiến nghị không tự mở lại hồ sơ.</summary>
    Task<AppealDto> ReopenRecordAsync(Guid appealId, ReopenFromAppealRequestDto request, CancellationToken ct = default);
}

/// <summary>
/// Người xử lý cần <c>evaluation.appeal.resolve</c> trong phạm vi, không phải chủ kiến nghị
/// và không xử lý kiến nghị liên quan tới bước mình đã thực hiện.
/// </summary>
public sealed class AppealService : IAppealService
{
    private const int MaxContentLength = 4000;

    /// <summary>Theo thứ tự luồng.</summary>
    private static readonly WorkflowStep[] ConcernableSteps =
    {
        WorkflowStep.B1_APPROVE, WorkflowStep.B2_CELL_CONFIRM, WorkflowStep.B3A_COLLECTIVE,
        WorkflowStep.B3B_APPRAISAL, WorkflowStep.B3C_DIRECTOR, WorkflowStep.B4_DECISION
    };

    private readonly IPostPublishRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IEvaluationWorkflowService _workflow;
    private readonly AppealResolverCheck _resolverCheck;

    public AppealService(
        IPostPublishRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IEvaluationWorkflowService workflow,
        IEvaluationWorkflowRepository workflowRepo,
        IPermissionResolver permissions)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
        _workflow = workflow;
        _resolverCheck = new AppealResolverCheck(workflowRepo, permissions);
    }

    /// <inheritdoc />
    public async Task<RecordAppealsDto> GetRecordAppealsAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await RequireRecordAsync(recordId, ct);
        EnsureCanView(record);

        var appeals = await _repo.ListAppealsAsync(recordId, ct);
        var isOwner = IsOwner(record);
        var canSubmitPermission = _guard.Can(PermissionCodes.EvaluationAppealSubmit, AccessTarget.ForRecord(record));
        var blocked = SubmitBlockedReason(record, appeals);
        var items = new List<AppealDto>();
        foreach (var appeal in appeals)
            items.Add(ToDto(appeal, record, await ResolverWarningAsync(appeal, record, ct)));

        return new RecordAppealsDto
        {
            RecordId = record.Id,
            UnderReview = appeals.Any(a => a.IsOpen),
            CanSubmit = canSubmitPermission && blocked == null,
            SubmitBlockedReason = isOwner ? (canSubmitPermission ? blocked : $"Bạn chưa có quyền \"{PermissionCodes.DisplayName(PermissionCodes.EvaluationAppealSubmit)}\".") : null,
            StepOptions = isOwner ? StepOptions(record) : new List<AppealStepOptionDto>(),
            Appeals = items
        };
    }

    /// <inheritdoc />
    public async Task<AppealDto> SubmitAsync(Guid recordId, SubmitAppealRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var record = await RequireRecordAsync(recordId, ct);
        _guard.Ensure(PermissionCodes.EvaluationAppealSubmit, AccessTarget.ForRecord(record));

        var content = request.Content?.Trim();
        if (string.IsNullOrEmpty(content))
            throw new ValidationException("Hãy nhập nội dung kiến nghị: điểm/mức nào cần xem xét lại và căn cứ, minh chứng kèm theo.");
        if (content.Length > MaxContentLength)
            throw new ValidationException($"Nội dung kiến nghị dài tối đa {MaxContentLength} ký tự.");

        var steps = new List<WorkflowStep>();
        foreach (var code in (request.ConcernedSteps ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct())
        {
            var step = WorkflowSteps.Parse(code);
            if (step == null || !ConcernableSteps.Contains(step.Value))
                throw new ValidationException($"Bước \"{code.Trim()}\" không chọn được làm bước kiến nghị liên quan tới.");
            steps.Add(step.Value);
        }

        var appeals = await _repo.ListAppealsAsync(recordId, ct);
        var blocked = SubmitBlockedReason(record, appeals);
        if (blocked != null)
            throw new ConflictException(blocked);

        var userId = _currentUser.UserId!.Value;
        var now = DateTime.UtcNow;
        var appeal = new EvaluationAppeal
        {
            RecordId = record.Id,
            SubmittedById = userId,
            SubmittedByName = await ActorNameAsync(ct),
            SubmittedAt = now,
            Content = content,
            ConcernedSteps = steps.OrderBy(WorkflowSteps.IndexOf).Select(WorkflowSteps.Code).ToList(),
            // Chụp người thực hiện tại thời điểm gửi vì hồ sơ có thể được mở lại, thực hiện lại sau đó.
            ConflictedUserIds = steps.Select(s => ActorOf(record, s).Id).OfType<Guid>().Where(id => id != userId).Distinct().ToList(),
            Status = AppealStatus.Submitted,
            CreatedAt = now
        };
        _repo.AddAppeal(appeal);
        await _unitOfWork.SaveChangesAsync(ct);
        // Vẫn nhận kiến nghị, chỉ báo rõ khi không còn ai xử lý được để quản trị gán người.
        return ToDto(appeal, record, await ResolverWarningAsync(appeal, record, ct));
    }

    /// <inheritdoc />
    public Task<AppealDto> StartReviewAsync(Guid appealId, VersionedRequestDto request, CancellationToken ct = default) =>
        ChangeAsync(appealId, request, async (appeal, now) =>
        {
            if (appeal.Status != AppealStatus.Submitted)
                throw new ConflictException($"Kiến nghị đang ở trạng thái \"{StatusName(appeal.Status)}\", không nhận xem xét được nữa.");
            appeal.Status = AppealStatus.UnderReview;
            appeal.ReviewStartedById = _currentUser.UserId;
            appeal.ReviewStartedByName = await ActorNameAsync(ct);
            appeal.ReviewStartedAt = now;
        }, ct);

    /// <inheritdoc />
    public Task<AppealDto> ResolveAsync(Guid appealId, ResolveAppealRequestDto request, CancellationToken ct = default) =>
        ChangeAsync(appealId, request, async (appeal, now) =>
        {
            if (!appeal.IsOpen)
                throw new ConflictException($"Kiến nghị đã được trả lời ({StatusName(appeal.Status)}).");
            if (request.Accepted is not { } accepted)
                throw new ValidationException("Hãy chọn chấp nhận hoặc không chấp nhận kiến nghị.");
            var response = request.Response?.Trim();
            if (string.IsNullOrEmpty(response))
                throw new ValidationException("Hãy nhập nội dung trả lời, nêu rõ căn cứ chấp nhận hoặc không chấp nhận.");
            if (response.Length > MaxContentLength)
                throw new ValidationException($"Nội dung trả lời dài tối đa {MaxContentLength} ký tự.");

            var actorName = await ActorNameAsync(ct);
            if (appeal.ReviewStartedAt == null)
            {
                appeal.ReviewStartedById = _currentUser.UserId;
                appeal.ReviewStartedByName = actorName;
                appeal.ReviewStartedAt = now;
            }
            appeal.Status = accepted ? AppealStatus.Accepted : AppealStatus.Rejected;
            appeal.Response = response;
            appeal.ResolvedById = _currentUser.UserId;
            appeal.ResolvedByName = actorName;
            appeal.ResolvedAt = now;
        }, ct);

    /// <inheritdoc />
    public async Task<AppealDto> ReopenRecordAsync(Guid appealId, ReopenFromAppealRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var appeal = await _repo.FindAppealAsync(appealId, ct)
            ?? throw new NotFoundException("Không tìm thấy kiến nghị.");
        var record = appeal.Record;
        EnsureCanView(record);
        if (appeal.Status != AppealStatus.Accepted)
            throw new ConflictException("Chỉ mở lại hồ sơ theo kiến nghị đã được chấp nhận.");
        if (appeal.ReopenedAt != null)
            throw new ConflictException("Hồ sơ đã được mở lại theo kiến nghị này.");
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ValidationException("Hãy nhập lý do mở lại hồ sơ (nội dung cần đính chính theo kiến nghị).");

        // "Mở lại" đã kiểm tra quyền, xung đột lợi ích, trạng thái, phiên bản và ghi lịch sử.
        var resolvedAt = appeal.ResolvedAt.HasValue
            ? EvaluationMapping.Today(appeal.ResolvedAt.Value).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : string.Empty;
        // Cùng transaction: tránh hồ sơ đã mở lại mà kiến nghị chưa ghi nhận (hoặc ngược lại).
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _workflow.ReopenAsync(record.Id, new ReopenRequestDto
            {
                Version = request.RecordVersion,
                TargetStep = request.TargetStep,
                UnlockUntil = request.UnlockUntil,
                Reason = $"Theo kiến nghị của {appeal.SubmittedByName} (chấp nhận ngày {resolvedAt}, mã {appeal.Id:D}): {reason}"
            }, ct);

            appeal.ReopenedAt = DateTime.UtcNow;
            appeal.ReopenedById = _currentUser.UserId;
            appeal.UpdatedAt = appeal.ReopenedAt;
            await _unitOfWork.SaveChangesAsync(ct);
        }, ct);

        var saved = await _repo.FindAppealAsync(appealId, ct) ?? appeal;
        return ToDto(saved, saved.Record);
    }

    #region Hỗ trợ

    /// <summary>Nạp → quyền xử lý → phiên bản → áp → lưu (xmin).</summary>
    private async Task<AppealDto> ChangeAsync(Guid appealId, VersionedRequestDto request, Func<EvaluationAppeal, DateTime, Task> apply, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu của kiến nghị. Hãy tải lại trang rồi thực hiện lại.");

        var appeal = await _repo.FindAppealAsync(appealId, ct)
            ?? throw new NotFoundException("Không tìm thấy kiến nghị.");
        EnsureCanResolve(appeal);
        if (appeal.Version != request.Version.Value)
            throw new ConflictException("Kiến nghị đã được người khác cập nhật sau khi bạn mở. Hãy tải lại để xem dữ liệu mới nhất.");

        var now = DateTime.UtcNow;
        await apply(appeal, now);
        appeal.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(appeal, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _repo.FindAppealAsync(appealId, ct) ?? appeal;
        return ToDto(saved, saved.Record, await ResolverWarningAsync(saved, saved.Record, ct));
    }

    /// <summary>Null = có người xử lý hoặc kiến nghị đã đóng.</summary>
    private async Task<string?> ResolverWarningAsync(EvaluationAppeal appeal, EvaluationRecord record, CancellationToken ct) =>
        appeal.IsOpen && !await _resolverCheck.HasResolverAsync(appeal, record, ct) ? AppealResolverCheck.NoResolverWarning : null;

    private async Task<EvaluationRecord> RequireRecordAsync(Guid recordId, CancellationToken ct) =>
        await _repo.FindRecordAsync(recordId, ct)
        ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

    private bool IsOwner(EvaluationRecord record) => _currentUser.UserId is { } id && record.MemberId == id;

    /// <summary>Chủ hồ sơ, người xử lý kiến nghị hoặc người xem hồ sơ trong phạm vi.</summary>
    private void EnsureCanView(EvaluationRecord record)
    {
        var target = AccessTarget.ForRecord(record);
        if (_guard.Can(PermissionCodes.EvaluationRead, target) || _guard.Can(PermissionCodes.EvaluationAppealResolve, target))
            return;
        throw new ForbiddenException(
            $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.EvaluationRead)}\" hoặc "
            + $"\"{PermissionCodes.DisplayName(PermissionCodes.EvaluationAppealResolve)}\" trên hồ sơ này (ngoài phạm vi được gán).");
    }

    /// <summary>Null = được.</summary>
    private string? ResolveBlockedReason(EvaluationAppeal appeal, EvaluationRecord record)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return "Chưa đăng nhập.";
        if (userId == appeal.SubmittedById || userId == record.MemberId)
            return "Bạn không được xử lý kiến nghị của chính mình (xung đột lợi ích).";
        if (appeal.ConflictedUserIds.Contains(userId.Value))
        {
            return "Kiến nghị liên quan tới bước bạn đã thực hiện trên hồ sơ này nên bạn không chủ trì xử lý (xung đột lợi ích). "
                + "Hãy chuyển cho người có thẩm quyền khác.";
        }
        if (!_guard.Can(PermissionCodes.EvaluationAppealResolve, AccessTarget.ForRecord(record)))
        {
            return $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.EvaluationAppealResolve)}\" trên hồ sơ này "
                + "(ngoài phạm vi được gán). Hãy liên hệ quản trị hệ thống nếu cần được cấp quyền.";
        }
        return null;
    }

    private void EnsureCanResolve(EvaluationAppeal appeal)
    {
        var reason = ResolveBlockedReason(appeal, appeal.Record);
        if (reason != null)
            throw new ForbiddenException(reason);
    }

    /// <summary>Null = gửi được.</summary>
    private static string? SubmitBlockedReason(EvaluationRecord record, IReadOnlyCollection<EvaluationAppeal> appeals)
    {
        if (record.Status != RecordStatus.Published)
            return "Chỉ gửi kiến nghị sau khi kết quả đã được công bố. Trước khi công bố, hãy giải trình, bổ sung minh chứng trong luồng đánh giá.";
        if (appeals.Any(a => a.IsOpen))
            return "Hồ sơ đang có kiến nghị chưa được trả lời. Hãy chờ kết quả xử lý trước khi gửi kiến nghị mới.";
        return null;
    }

    private static List<AppealStepOptionDto> StepOptions(EvaluationRecord record) =>
        ConcernableSteps
            .Select(step => (step, actor: ActorOf(record, step)))
            .Where(x => x.actor.Name != null)
            .Select(x => new AppealStepOptionDto
            {
                Step = WorkflowSteps.Code(x.step),
                StepName = WorkflowSteps.DisplayName(x.step),
                ActorName = x.actor.Name
            })
            .ToList();

    /// <summary>Bước do cấp trên thực hiện: Id null, tên là cơ quan cấp trên.</summary>
    private static (Guid? Id, string? Name) ActorOf(EvaluationRecord record, WorkflowStep step) => step switch
    {
        WorkflowStep.B1_APPROVE => (record.TasksApprovedById, record.TasksApprovedByName),
        WorkflowStep.B2_CELL_CONFIRM => (record.CellConfirmedById, record.CellConfirmedByName),
        WorkflowStep.B3A_COLLECTIVE => (record.CollectiveRecordedById, record.CollectiveRecordedByName),
        WorkflowStep.B3B_APPRAISAL => (record.AppraisedById, record.AppraisedByName),
        WorkflowStep.B3C_DIRECTOR => (record.DirectorReviewedById, record.DirectorReviewedByName),
        WorkflowStep.B4_DECISION => (record.DecisionRecordedById, record.DecisionAuthorityName ?? record.DecisionRecordedByName),
        _ => (null, null)
    };

    private async Task<string> ActorNameAsync(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var name = userId.HasValue ? await _repo.GetMemberNameAsync(userId.Value, ct) : null;
        return string.IsNullOrWhiteSpace(name) ? _currentUser.UserName : name;
    }

    public static string StatusName(AppealStatus status) => status switch
    {
        AppealStatus.Submitted => "Đã gửi, chờ xem xét",
        AppealStatus.UnderReview => "Đang xem xét",
        AppealStatus.Accepted => "Chấp nhận",
        AppealStatus.Rejected => "Không chấp nhận",
        _ => status.ToString()
    };

    private AppealDto ToDto(EvaluationAppeal appeal, EvaluationRecord record, string? resolverWarning = null)
    {
        var blocked = appeal.IsOpen ? ResolveBlockedReason(appeal, record) : null;
        var canReopen = appeal.Status == AppealStatus.Accepted
            && appeal.ReopenedAt == null
            && record.Status == RecordStatus.Published
            && !IsOwner(record)
            && _guard.Can(PermissionCodes.EvaluationReopen, AccessTarget.ForRecord(record));
        return new AppealDto
        {
            Id = appeal.Id,
            Version = appeal.Version,
            RecordId = appeal.RecordId,
            SubmittedByName = appeal.SubmittedByName,
            SubmittedAt = appeal.SubmittedAt,
            Content = appeal.Content,
            ConcernedSteps = appeal.ConcernedSteps.ToList(),
            ConcernedStepNames = appeal.ConcernedSteps
                .Select(code => WorkflowSteps.Parse(code) is { } s ? WorkflowSteps.DisplayName(s) : code)
                .ToList(),
            Status = appeal.Status.ToString(),
            StatusName = StatusName(appeal.Status),
            ReviewStartedByName = appeal.ReviewStartedByName,
            ReviewStartedAt = appeal.ReviewStartedAt,
            ResolvedByName = appeal.ResolvedByName,
            ResolvedAt = appeal.ResolvedAt,
            Response = appeal.Response,
            ReopenedAt = appeal.ReopenedAt,
            CanResolve = appeal.IsOpen && blocked == null,
            // Không lộ luật cho người chỉ xem.
            ResolveBlockedReason = appeal.IsOpen && blocked != null && _guard.HasAny(PermissionCodes.EvaluationAppealResolve) ? blocked : null,
            CanReopen = canReopen,
            CanAttach = appeal.IsOpen && _guard.Can(PermissionCodes.EvaluationSelf, AccessTarget.ForRecord(record)),
            ResolverWarning = resolverWarning
        };
    }

    #endregion
}
