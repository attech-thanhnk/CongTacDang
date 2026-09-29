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

/// <summary>Kiến nghị sau công bố (task 20 — T-87, HD03 PL II mục III.2).</summary>
public interface IAppealService
{
    /// <summary>Khối "Kiến nghị" của hồ sơ: danh sách kiến nghị, có gửi được không, các bước chọn được.</summary>
    Task<RecordAppealsDto> GetRecordAppealsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Chủ hồ sơ gửi kiến nghị (hồ sơ đã công bố, không có kiến nghị khác đang xử lý).</summary>
    Task<AppealDto> SubmitAsync(Guid recordId, SubmitAppealRequestDto request, CancellationToken ct = default);

    /// <summary>Nhận xem xét kiến nghị (Submitted → UnderReview).</summary>
    Task<AppealDto> StartReviewAsync(Guid appealId, VersionedRequestDto request, CancellationToken ct = default);

    /// <summary>Trả lời kiến nghị: chấp nhận / không chấp nhận, bắt buộc nêu căn cứ.</summary>
    Task<AppealDto> ResolveAsync(Guid appealId, ResolveAppealRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Mở lại hồ sơ theo kiến nghị đã chấp nhận — gọi thao tác "Mở lại" hiện có (quyền <c>evaluation.reopen</c>, lý do ghi vào lịch sử
    /// hồ sơ kèm dẫn chiếu kiến nghị); không tự động mở lại khi chấp nhận.
    /// </summary>
    Task<AppealDto> ReopenRecordAsync(Guid appealId, ReopenFromAppealRequestDto request, CancellationToken ct = default);
}

/// <summary>
/// Kiến nghị sau công bố. Quyền:
/// <list type="bullet">
/// <item>Gửi: <c>evaluation.appeal.submit</c> — chỉ chủ hồ sơ (guard), hồ sơ đã công bố, không trùng khi đang xử lý.</item>
/// <item>Xem: chủ hồ sơ, <c>evaluation.appeal.resolve</c> hoặc <c>evaluation.read</c> trong phạm vi (hồ sơ kiến nghị là thành phần hồ sơ,
/// PL II mục V).</item>
/// <item>Xử lý: <c>evaluation.appeal.resolve</c> trong phạm vi; không xử lý kiến nghị của mình (xung đột lợi ích trong guard) và không xử lý
/// kiến nghị liên quan tới bước mình đã thực hiện trên hồ sơ (PL II III.2: "người đó không chủ trì xử lý").</item>
/// </list>
/// </summary>
public sealed class AppealService : IAppealService
{
    private const int MaxContentLength = 4000;

    /// <summary>Các bước có người thực hiện trên hồ sơ mà kiến nghị có thể liên quan tới (theo thứ tự luồng).</summary>
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

    public AppealService(
        IPostPublishRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IEvaluationWorkflowService workflow)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
        _workflow = workflow;
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

        return new RecordAppealsDto
        {
            RecordId = record.Id,
            UnderReview = appeals.Any(a => a.IsOpen),
            CanSubmit = canSubmitPermission && blocked == null,
            SubmitBlockedReason = isOwner ? (canSubmitPermission ? blocked : $"Bạn chưa có quyền \"{PermissionCodes.DisplayName(PermissionCodes.EvaluationAppealSubmit)}\".") : null,
            StepOptions = isOwner ? StepOptions(record) : new List<AppealStepOptionDto>(),
            Appeals = appeals.Select(a => ToDto(a, record)).ToList()
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
            // Chụp người thực hiện các bước bị kiến nghị tại thời điểm gửi (hồ sơ có thể được mở lại, thực hiện lại sau đó).
            ConflictedUserIds = steps.Select(s => ActorOf(record, s).Id).OfType<Guid>().Where(id => id != userId).Distinct().ToList(),
            Status = AppealStatus.Submitted,
            CreatedAt = now
        };
        _repo.AddAppeal(appeal);
        await _unitOfWork.SaveChangesAsync(ct);
        return ToDto(appeal, record);
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
                throw new ValidationException("Hãy nhập nội dung trả lời, nêu rõ căn cứ chấp nhận hoặc không chấp nhận (HD03 PL II mục III.2).");
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

        // Thao tác "Mở lại" hiện có: kiểm tra quyền evaluation.reopen, xung đột lợi ích, trạng thái, phiên bản hồ sơ và ghi lịch sử.
        var resolvedAt = appeal.ResolvedAt.HasValue
            ? EvaluationMapping.Today(appeal.ResolvedAt.Value).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : string.Empty;
        await _workflow.ReopenAsync(record.Id, new ReopenRequestDto
        {
            Version = request.RecordVersion,
            TargetStep = request.TargetStep,
            Reason = $"Theo kiến nghị của {appeal.SubmittedByName} (chấp nhận ngày {resolvedAt}, mã {appeal.Id:D}): {reason}"
        }, ct);

        appeal.ReopenedAt = DateTime.UtcNow;
        appeal.ReopenedById = _currentUser.UserId;
        appeal.UpdatedAt = appeal.ReopenedAt;
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _repo.FindAppealAsync(appealId, ct) ?? appeal;
        return ToDto(saved, saved.Record);
    }

    #region Hỗ trợ

    /// <summary>Khung chung cho thao tác xử lý: nạp → quyền xử lý → phiên bản → áp → lưu (xmin).</summary>
    private async Task<AppealDto> ChangeAsync(Guid appealId, VersionedRequestDto request, Func<EvaluationAppeal, DateTime, Task> apply, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của kiến nghị. Hãy tải lại trang rồi thực hiện lại.");

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
        return ToDto(saved, saved.Record);
    }

    private async Task<EvaluationRecord> RequireRecordAsync(Guid recordId, CancellationToken ct) =>
        await _repo.FindRecordAsync(recordId, ct)
        ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

    private bool IsOwner(EvaluationRecord record) => _currentUser.UserId is { } id && record.MemberId == id;

    /// <summary>Xem kiến nghị: chủ hồ sơ, người xử lý kiến nghị hoặc người xem hồ sơ trong phạm vi.</summary>
    private void EnsureCanView(EvaluationRecord record)
    {
        var target = AccessTarget.ForRecord(record);
        if (_guard.Can(PermissionCodes.EvaluationRead, target) || _guard.Can(PermissionCodes.EvaluationAppealResolve, target))
            return;
        throw new ForbiddenException(
            $"Bạn không có quyền \"{PermissionCodes.DisplayName(PermissionCodes.EvaluationRead)}\" hoặc "
            + $"\"{PermissionCodes.DisplayName(PermissionCodes.EvaluationAppealResolve)}\" trên hồ sơ này (ngoài phạm vi được gán).");
    }

    /// <summary>Lý do không xử lý được kiến nghị; null = được.</summary>
    private string? ResolveBlockedReason(EvaluationAppeal appeal, EvaluationRecord record)
    {
        var userId = _currentUser.UserId;
        if (userId == null)
            return "Chưa đăng nhập.";
        if (userId == appeal.SubmittedById || userId == record.MemberId)
            return "Bạn không được xử lý kiến nghị của chính mình (xung đột lợi ích theo Hướng dẫn 03-HD/TVĐU).";
        if (appeal.ConflictedUserIds.Contains(userId.Value))
        {
            return "Kiến nghị liên quan tới bước bạn đã thực hiện trên hồ sơ này nên bạn không chủ trì xử lý (xung đột lợi ích — "
                + "HD03 PL II mục III.2). Hãy chuyển cho người có thẩm quyền khác.";
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

    /// <summary>Lý do chủ hồ sơ chưa gửi được kiến nghị; null = gửi được.</summary>
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

    /// <summary>Người thực hiện bước trên hồ sơ (Id null với bước do cấp trên thực hiện — tên là cơ quan cấp trên).</summary>
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

    /// <summary>Tên hiển thị trạng thái kiến nghị.</summary>
    public static string StatusName(AppealStatus status) => status switch
    {
        AppealStatus.Submitted => "Đã gửi, chờ xem xét",
        AppealStatus.UnderReview => "Đang xem xét",
        AppealStatus.Accepted => "Chấp nhận",
        AppealStatus.Rejected => "Không chấp nhận",
        _ => status.ToString()
    };

    private AppealDto ToDto(EvaluationAppeal appeal, EvaluationRecord record)
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
            // Chỉ báo lý do bị chặn cho người có quyền xử lý ở phạm vi nào đó (không lộ luật cho người chỉ xem).
            ResolveBlockedReason = appeal.IsOpen && blocked != null && _guard.HasAny(PermissionCodes.EvaluationAppealResolve) ? blocked : null,
            CanReopen = canReopen,
            CanAttach = appeal.IsOpen && _guard.Can(PermissionCodes.EvaluationSelf, AccessTarget.ForRecord(record))
        };
    }

    #endregion
}
