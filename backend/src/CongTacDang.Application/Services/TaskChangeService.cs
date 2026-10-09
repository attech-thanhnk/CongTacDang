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

/// <summary>Điều chỉnh việc trong quý: đề nghị điều chỉnh, chuyển tiếp, loại trừ việc (Mẫu 04) và ghi nhận việc đột xuất (Mẫu 05).</summary>
public interface ITaskChangeService
{
    /// <summary>Khối "Điều chỉnh việc trong quý" của hồ sơ (xem hồ sơ: <c>evaluation.read</c> trong phạm vi hoặc chủ hồ sơ).</summary>
    Task<RecordTaskChangesDto> GetAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Gửi đề nghị (Mẫu 04: chủ hồ sơ; Mẫu 05: chủ hồ sơ hoặc người duyệt danh sách việc của hồ sơ).</summary>
    Task<RecordTaskChangesDto> CreateAsync(Guid recordId, CreateTaskChangeRequestDto request, CancellationToken ct = default);

    /// <summary>Duyệt đề nghị — áp vào danh mục việc của hồ sơ trong cùng một lần lưu.</summary>
    Task<RecordTaskChangesDto> ApproveAsync(Guid recordId, Guid requestId, DecideTaskChangeRequestDto request, CancellationToken ct = default);

    /// <summary>Không duyệt đề nghị (bắt buộc lý do).</summary>
    Task<RecordTaskChangesDto> RejectAsync(Guid recordId, Guid requestId, DecideTaskChangeRequestDto request, CancellationToken ct = default);
}

/// <summary>Nhóm việc "Đề nghị điều chỉnh việc chờ duyệt" trong danh sách việc cần xử lý / nhắc việc.</summary>
public interface ITaskChangeWorkQueue
{
    /// <summary>Nhóm việc của người dùng hiện tại (rỗng thì không trả về).</summary>
    Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default);
}

/// <summary>
/// Đề nghị thay đổi danh mục việc trong quý. Điều kiện: kỳ đang mở, hồ sơ đã qua duyệt danh sách việc và chưa nộp tự chấm
/// (<see cref="RecordStatus.AwaitingSelfScore"/>), bộ tiêu chí chấm theo sản phẩm (09A) và cách xét duyệt có bước đăng ký sản phẩm.
/// Mỗi hồ sơ tối đa một đề nghị đang chờ. Quyền duyệt = quyền thực hiện bước duyệt danh sách việc theo cách xét duyệt của hồ sơ
/// (bước do cấp trên thực hiện → <c>evaluation.external.record</c>: ghi nhận quyết định của cấp trên); chủ hồ sơ không tự duyệt.
/// </summary>
public sealed class TaskChangeService : ITaskChangeService, ITaskChangeWorkQueue
{
    /// <summary>Mã nhóm việc trong danh sách việc cần xử lý.</summary>
    public const string WorkQueueGroup = "TASK_CHANGES";

    private const int MaxCommentLength = 4000;

    private readonly IEvaluationWorkflowRepository _records;
    private readonly ITaskChangeRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public TaskChangeService(
        IEvaluationWorkflowRepository records,
        ITaskChangeRepository repo,
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
    public async Task<RecordTaskChangesDto> GetAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await RequireRecordAsync(recordId, ct);
        _guard.Ensure(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));
        return await BuildAsync(record, ct);
    }

    private async Task<RecordTaskChangesDto> BuildAsync(EvaluationRecord record, CancellationToken ct)
    {
        var requests = await _repo.ListByRecordAsync(record.Id, ct);
        var context = Context(record);
        var canDecide = context.Applicable && CanDecide(record, context.Profile);
        var isOwnerWithSelf = _guard.Can(PermissionCodes.EvaluationSelf, AccessTarget.ForRecord(record));
        var hasPending = requests.Any(r => r.Status == TaskChangeStatus.Pending);
        var blocked = context.Applicable ? StateBlockReason(record) ?? (hasPending ? PendingBlockReason : null) : null;

        var tasks = (record.Tasks ?? new List<EvaluationTask>()).Where(t => !t.IsDeleted).OrderBy(t => t.TaskOrder).ToList();
        return new RecordTaskChangesDto
        {
            RecordId = record.Id,
            Applicable = context.Applicable,
            CanRequestChange = context.Applicable && blocked == null && isOwnerWithSelf,
            CanRecordUrgent = context.Applicable && blocked == null && (isOwnerWithSelf || canDecide),
            BlockedReason = context.Applicable && (isOwnerWithSelf || canDecide) ? blocked : null,
            HasPending = hasPending,
            Requests = requests.Select(r => ToDto(r, tasks,
                canDecide && r.Status == TaskChangeStatus.Pending && StateBlockReason(record) == null)).ToList()
        };
    }

    #endregion

    #region Ghi

    /// <inheritdoc />
    public async Task<RecordTaskChangesDto> CreateAsync(Guid recordId, CreateTaskChangeRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại hồ sơ rồi thực hiện lại.");
        if (!Enum.TryParse<TaskChangeType>(request.Type?.Trim(), true, out var type) || !Enum.IsDefined(type))
            throw new ValidationException("Hãy chọn loại đề nghị: điều chỉnh, chuyển tiếp, loại trừ hoặc việc đột xuất.");

        var record = await RequireRecordAsync(recordId, ct);
        var context = Context(record);
        var target = AccessTarget.ForRecord(record);
        if (type == TaskChangeType.Urgent)
        {
            // Việc đột xuất: chủ hồ sơ hoặc người duyệt danh sách việc của hồ sơ (cấp giao việc).
            if (!_guard.Can(PermissionCodes.EvaluationSelf, target) && !CanDecide(record, context.Profile))
                _guard.Ensure(IsOwner(record) ? PermissionCodes.EvaluationSelf : ApproverPermission(context.Profile), target);
        }
        else
        {
            _guard.Ensure(PermissionCodes.EvaluationSelf, target);
        }

        if (record.Version != request.Version.Value)
            throw new ConflictException("Hồ sơ đã được người khác cập nhật sau khi bạn mở. Hãy tải lại hồ sơ để xem dữ liệu mới nhất rồi thực hiện lại.");
        if (!context.Applicable)
            throw new ConflictException(NotApplicableReason);
        var blocked = StateBlockReason(record);
        if (blocked != null)
            throw new ConflictException(blocked);
        var existing = await _repo.ListByRecordAsync(record.Id, ct);
        if (existing.Any(r => r.Status == TaskChangeStatus.Pending))
            throw new ConflictException(PendingBlockReason);

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
            throw new ValidationException("Hãy nhập lý do / nguyên nhân của đề nghị.");
        if (reason.Length > TaskChangeRules.MaxReasonLength)
            throw new ValidationException($"Lý do không được dài quá {TaskChangeRules.MaxReasonLength} ký tự.");

        var tasks = await _records.GetTasksAsync(record.Id, ct);
        var proposal = request.Proposal ?? new TaskChangeProposal();
        var weights = request.Weights ?? new Dictionary<Guid, double>();
        var error = TaskChangeRules.Validate(type, request.TaskId, proposal, weights, tasks, context.Criteria!.Content);
        if (error != null)
            throw new ValidationException(error);

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        var targetTask = request.TaskId is { } taskId ? tasks.FirstOrDefault(t => t.Id == taskId) : null;
        var change = new TaskChangeRequest
        {
            RecordId = record.Id,
            Type = type,
            TaskId = TaskChangeRules.TargetsTask(type) ? targetTask?.Id : null,
            TaskNameSnapshot = TaskChangeRules.TargetsTask(type) ? targetTask?.TaskName : proposal.TaskName?.Trim(),
            Proposal = TaskChangeRules.ProposalToJson(Normalize(type, proposal)),
            Weights = TaskChangeRules.WeightsToJson(TaskChangeRules.ResultingWeights(type, targetTask?.Id, proposal, weights, tasks)),
            WeightsBefore = TaskChangeRules.WeightsToJson(tasks.Where(TaskChangeRules.IsActive).ToDictionary(t => t.Id, t => t.Weight)),
            Reason = reason,
            Status = TaskChangeStatus.Pending,
            RequestedById = _currentUser.UserId,
            RequestedByName = actorName,
            RequestedAt = now,
            CreatedAt = now
        };
        _repo.Add(change);

        AddHistory(record, null, $"Đề nghị {TaskChangeRules.TypeName(type).ToLowerInvariant()}: {change.TaskNameSnapshot}.", null, actorName, now);
        record.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(record, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(record.Id, ct);
    }

    /// <inheritdoc />
    public Task<RecordTaskChangesDto> ApproveAsync(Guid recordId, Guid requestId, DecideTaskChangeRequestDto request, CancellationToken ct = default) =>
        DecideAsync(recordId, requestId, request, approve: true, ct);

    /// <inheritdoc />
    public Task<RecordTaskChangesDto> RejectAsync(Guid recordId, Guid requestId, DecideTaskChangeRequestDto request, CancellationToken ct = default) =>
        DecideAsync(recordId, requestId, request, approve: false, ct);

    private async Task<RecordTaskChangesDto> DecideAsync(Guid recordId, Guid requestId, DecideTaskChangeRequestDto request, bool approve,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của đề nghị. Hãy tải lại hồ sơ rồi thực hiện lại.");

        var record = await RequireRecordAsync(recordId, ct);
        var change = await _repo.FindAsync(requestId, ct);
        if (change == null || change.RecordId != record.Id)
            throw new NotFoundException("Không tìm thấy đề nghị điều chỉnh việc của hồ sơ.");

        var context = Context(record);
        var permission = ApproverPermission(context.Profile);
        if (IsOwner(record))
            throw new ForbiddenException("Bạn không được duyệt đề nghị điều chỉnh việc trên hồ sơ của chính mình (xung đột lợi ích).");
        _guard.Ensure(permission, AccessTarget.ForRecord(record));

        if (change.Version != request.Version.Value)
            throw new ConflictException("Đề nghị đã được người khác cập nhật sau khi bạn mở. Hãy tải lại hồ sơ rồi thực hiện lại.");
        if (change.Status != TaskChangeStatus.Pending)
            throw new ConflictException($"Đề nghị đã được xử lý ({TaskChangeRules.StatusName(change.Status)}).");

        var comment = request.Comment?.Trim();
        if (comment is { Length: > MaxCommentLength })
            throw new ValidationException($"Ý kiến không được dài quá {MaxCommentLength} ký tự.");
        if (!approve && string.IsNullOrEmpty(comment))
            throw new ValidationException("Hãy nhập lý do không duyệt để người đề nghị biết.");

        var now = DateTime.UtcNow;
        var actorName = await ActorNameAsync(ct);
        string history;
        if (approve)
        {
            if (!context.Applicable)
                throw new ConflictException(NotApplicableReason);
            var blocked = StateBlockReason(record);
            if (blocked != null)
                throw new ConflictException(blocked);

            var tasks = await _records.GetTasksAsync(record.Id, ct);
            var before = tasks.Where(TaskChangeRules.IsActive).ToDictionary(t => t.Id, t => t.Weight);
            var content = context.Criteria!.Content;
            var (error, created) = TaskChangeRules.Apply(change, tasks, content, record.Period.EndDate, content.FindFrame(record.WeightFrameCode));
            if (error != null)
                throw new ValidationException($"Không áp được đề nghị vào danh mục hiện tại: {error}");
            if (created != null)
            {
                _repo.AddTask(created);
                change.ResultTaskId = created.Id;
            }

            var weightChanges = TaskChangeRules.DescribeWeightChanges(tasks, before);
            history = $"Duyệt đề nghị {TaskChangeRules.TypeName(change.Type).ToLowerInvariant()}: {change.TaskNameSnapshot}."
                + (weightChanges.Length > 0 ? $" Trọng số: {weightChanges}." : string.Empty)
                + (created != null ? string.Create(CultureInfo.InvariantCulture, $" Thêm việc đột xuất, trọng số {created.Weight:0.##}.") : string.Empty)
                + (string.IsNullOrEmpty(comment) ? string.Empty : $" Ý kiến: {comment}");
            change.Status = TaskChangeStatus.Approved;
        }
        else
        {
            history = $"Không duyệt đề nghị {TaskChangeRules.TypeName(change.Type).ToLowerInvariant()}: {change.TaskNameSnapshot}.";
            change.Status = TaskChangeStatus.Rejected;
        }

        change.DecidedById = _currentUser.UserId;
        change.DecidedByName = actorName;
        change.DecidedAt = now;
        change.DecisionComment = string.IsNullOrEmpty(comment) ? null : comment;
        change.UpdatedAt = now;

        AddHistory(record, WorkflowStep.B1_APPROVE, history, approve ? null : comment, actorName, now);
        record.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(change, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(record.Id, ct);
    }

    #endregion

    #region Việc cần xử lý

    /// <inheritdoc />
    public async Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default)
    {
        var periods = await _records.ListPeriodsAsync(ct);
        var targetIds = periods
            .Where(p => p.Status == PeriodStatus.Open && (periodId is null || periodId == Guid.Empty || p.Id == periodId))
            .Select(p => p.Id)
            .ToList();
        if (targetIds.Count == 0)
            return new List<WorkQueueGroupDto>();

        var items = new List<WorkQueueItemDto>();
        foreach (var change in await _repo.ListPendingAsync(targetIds, ct))
        {
            var record = change.Record;
            if (record.Status != RecordStatus.AwaitingSelfScore)
                continue;
            PeriodSettings settings;
            try
            {
                settings = record.Period.GetSettings();
            }
            catch (FormatException)
            {
                continue;
            }
            var profile = EvaluationMapping.ProfileOf(settings, record);
            if (!CanDecide(record, profile))
                continue;
            items.Add(new WorkQueueItemDto
            {
                RecordId = record.Id,
                Version = record.Version,
                PeriodId = record.PeriodId,
                PeriodName = record.Period.Name,
                MemberId = record.MemberId,
                FullName = record.Member?.FullName ?? string.Empty,
                DepartmentName = record.Department?.Name,
                PartyCellName = record.PartyCell?.Name,
                ApprovalAuthority = record.ApprovalAuthority.ToString(),
                WorkflowProfileName = profile.Name,
                Status = record.Status.ToString(),
                StatusDisplayName = TaskChangeRules.TypeName(change.Type),
                Mode = profile.Mode(WorkflowStep.B1_APPROVE).ToString()
            });
        }

        if (items.Count == 0)
            return new List<WorkQueueGroupDto>();
        return new List<WorkQueueGroupDto>
        {
            new()
            {
                Step = WorkQueueGroup,
                StepName = "Duyệt đề nghị điều chỉnh việc",
                Count = items.Count,
                Items = items.OrderBy(i => i.PeriodName).ThenBy(i => i.FullName).ToList()
            }
        };
    }

    #endregion

    #region Hỗ trợ

    private const string NotApplicableReason =
        "Hồ sơ này không đăng ký danh mục sản phẩm nên không có chức năng điều chỉnh việc trong quý.";

    private const string PendingBlockReason =
        "Hồ sơ đang có một đề nghị chờ duyệt. Hãy chờ đề nghị đó được xử lý rồi gửi đề nghị mới.";

    private sealed record ChangeContext(bool Applicable, WorkflowProfile Profile, CriteriaSnapshot? Criteria);

    /// <summary>Cách xét duyệt và bộ tiêu chí của hồ sơ; chức năng áp dụng khi chấm theo sản phẩm và có bước đăng ký sản phẩm.</summary>
    private static ChangeContext Context(EvaluationRecord record)
    {
        PeriodSettings settings;
        CriteriaSnapshot? criteria;
        try
        {
            settings = record.Period.GetSettings();
            criteria = record.Period.GetCriteria();
        }
        catch (FormatException)
        {
            throw new ConflictException("Cấu hình của kỳ đánh giá bị lỗi định dạng. Hãy liên hệ người quản lý kỳ để sửa cấu hình.");
        }
        var profile = EvaluationMapping.ProfileOf(settings, record);
        var applicable = criteria != null && !criteria.UsesAxisScoring && profile.IsActive(WorkflowStep.B1_REGISTER);
        return new ChangeContext(applicable, profile, criteria);
    }

    /// <summary>Kỳ đang mở và hồ sơ đã duyệt danh sách việc, chưa nộp tự chấm; null nếu được phép.</summary>
    private static string? StateBlockReason(EvaluationRecord record)
    {
        if (record.Period.Status != PeriodStatus.Open)
            return "Chỉ điều chỉnh việc khi kỳ đánh giá đang mở.";
        if (record.Status != RecordStatus.AwaitingSelfScore)
            return "Chỉ điều chỉnh việc sau khi danh sách việc đã được duyệt và trước khi nộp phiếu tự chấm.";
        return null;
    }

    /// <summary>
    /// Quyền duyệt đề nghị: quyền thực hiện bước duyệt danh sách việc theo cách xét duyệt của hồ sơ; bước do cấp trên thực hiện →
    /// quyền ghi nhận kết quả của cấp trên.
    /// </summary>
    public static string ApproverPermission(WorkflowProfile profile) =>
        profile.Mode(WorkflowStep.B1_APPROVE) == StepMode.External
            ? PermissionCodes.EvaluationExternalRecord
            : WorkflowActions.PermissionFor(WorkflowActions.Get(WorkflowActions.ApproveTasks), profile);

    private bool CanDecide(EvaluationRecord record, WorkflowProfile profile) =>
        !IsOwner(record) && _guard.Can(ApproverPermission(profile), AccessTarget.ForRecord(record));

    private bool IsOwner(EvaluationRecord record) => _currentUser.UserId is { } id && record.MemberId == id;

    private static TaskChangeProposal Normalize(TaskChangeType type, TaskChangeProposal p) => type switch
    {
        // Chuyển tiếp / loại trừ không đổi nội dung việc.
        TaskChangeType.Carryover or TaskChangeType.Exclude => new TaskChangeProposal(),
        TaskChangeType.Adjust => new TaskChangeProposal
        {
            TaskName = p.TaskName?.Trim(), TargetOutput = p.TargetOutput?.Trim(), Deadline = p.Deadline, AxisCode = p.AxisCode?.Trim(),
            ProductCode = p.ProductCode?.Trim(), Role = p.Role?.Trim(), ExceedCriteria = p.ExceedCriteria?.Trim(),
            EvidenceSource = p.EvidenceSource?.Trim(), Confirmer = p.Confirmer?.Trim()
        },
        _ => new TaskChangeProposal
        {
            TaskName = p.TaskName?.Trim(), TargetOutput = p.TargetOutput?.Trim(), Deadline = p.Deadline, AxisCode = p.AxisCode?.Trim(),
            ProductCode = p.ProductCode?.Trim(), Role = p.Role?.Trim(), ExceedCriteria = p.ExceedCriteria?.Trim(),
            EvidenceSource = p.EvidenceSource?.Trim(), Confirmer = p.Confirmer?.Trim(),
            Weight = p.Weight.HasValue ? Math.Round(p.Weight.Value, 2) : null
        }
    };

    private void AddHistory(EvaluationRecord record, WorkflowStep? step, string comment, string? reason, string actorName, DateTime now) =>
        _records.AddHistory(new EvaluationRecordHistory
        {
            RecordId = record.Id,
            FromStatus = record.Status,
            ToStatus = record.Status,
            Step = step,
            Action = WorkflowAction.TaskChange,
            Reason = reason is { Length: > 2000 } ? reason[..2000] : reason,
            ScoreBefore = record.EffectiveScore(),
            ScoreAfter = record.EffectiveScore(),
            GradeBefore = record.EffectiveGrade(),
            GradeAfter = record.EffectiveGrade(),
            ActorId = _currentUser.UserId,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            Comment = comment,
            CreatedAt = now
        });

    private async Task<EvaluationRecord> RequireRecordAsync(Guid recordId, CancellationToken ct) =>
        await _records.FindRecordAsync(recordId, ct)
        ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

    private async Task<RecordTaskChangesDto> ReloadAsync(Guid recordId, CancellationToken ct)
    {
        var record = await RequireRecordAsync(recordId, ct);
        return await BuildAsync(record, ct);
    }

    private async Task<string> ActorNameAsync(CancellationToken ct)
    {
        var id = _currentUser.UserId;
        var name = id.HasValue ? await _records.GetMemberNameAsync(id.Value, ct) : null;
        return name ?? _currentUser.UserName;
    }

    private static TaskChangeRequestDto ToDto(TaskChangeRequest r, IReadOnlyList<EvaluationTask> tasks, bool canDecide)
    {
        var proposal = TaskChangeRules.ProposalFromJson(r.Proposal);
        var after = TaskChangeRules.WeightsFromJson(r.Weights);
        var before = TaskChangeRules.WeightsFromJson(r.WeightsBefore);
        var weights = new List<TaskChangeWeightDto>();
        foreach (var task in tasks)
        {
            var hasBefore = before.TryGetValue(task.Id, out var b);
            var hasAfter = after.TryGetValue(task.Id, out var a);
            if (r.ResultTaskId == task.Id || (!hasBefore && !hasAfter))
                continue;
            weights.Add(new TaskChangeWeightDto
            {
                TaskId = task.Id,
                TaskName = task.Id == r.TaskId && r.TaskNameSnapshot != null ? r.TaskNameSnapshot : task.TaskName,
                Before = hasBefore ? b : null,
                After = hasAfter ? a : 0
            });
        }
        if (r.Type == TaskChangeType.Urgent)
        {
            weights.Add(new TaskChangeWeightDto
            {
                TaskId = r.ResultTaskId,
                TaskName = proposal.TaskName ?? string.Empty,
                Before = null,
                After = proposal.Weight ?? 0
            });
        }

        return new TaskChangeRequestDto
        {
            Id = r.Id,
            Version = r.Version,
            RecordId = r.RecordId,
            Type = r.Type.ToString(),
            TypeName = TaskChangeRules.TypeName(r.Type),
            TaskId = r.TaskId,
            TaskName = r.TaskNameSnapshot,
            Proposal = proposal,
            Weights = weights,
            Reason = r.Reason,
            Status = r.Status.ToString(),
            StatusName = TaskChangeRules.StatusName(r.Status),
            RequestedByName = r.RequestedByName,
            RequestedAt = r.RequestedAt,
            DecidedByName = r.DecidedByName,
            DecidedAt = r.DecidedAt,
            DecisionComment = r.DecisionComment,
            CanDecide = canDecide
        };
    }

    #endregion
}
