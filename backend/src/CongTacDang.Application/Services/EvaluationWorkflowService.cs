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

/// <summary>
/// Luồng đánh giá 9 bước theo cấu hình kỳ (docs/thiet-ke/luong-danh-gia.md). Mỗi hành động một phương thức; tất cả đi qua
/// cùng một trình tự: nạp hồ sơ → <c>guard.Ensure</c> → kiểm phiên bản → kiểm trạng thái kỳ + máy trạng thái → áp dữ liệu →
/// ghi lịch sử → <c>SaveChanges</c> với <c>version</c> (xmin).
/// </summary>
public interface IEvaluationWorkflowService
{
    Task<EvaluationRecordDto> SubmitTasksAsync(Guid recordId, SubmitTasksRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ApproveTasksAsync(Guid recordId, CommentRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReturnTasksAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> SubmitSelfScoreAsync(Guid recordId, SubmitSelfScoreRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ConfirmByCellAsync(Guid recordId, CommentRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReturnByCellAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> RecordCollectiveProposalAsync(Guid recordId, CollectiveProposalRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> AppraiseAsync(Guid recordId, AppraisalRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReturnByAppraiserAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> DirectorReviewAsync(Guid recordId, DirectorReviewRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> RecordDecisionAsync(Guid recordId, DecisionRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> PublishAsync(Guid recordId, WorkflowRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReopenAsync(Guid recordId, ReopenRequestDto request, CancellationToken ct = default);

    /// <summary>Hành động người hiện tại được làm trên hồ sơ (thiết kế mục 5).</summary>
    Task<RecordActionsDto> GetActionsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Hồ sơ đang chờ người hiện tại xử lý, nhóm theo bước (lọc bằng phạm vi guard).</summary>
    Task<WorkQueueDto> GetWorkQueueAsync(Guid? periodId, CancellationToken ct = default);
}

/// <summary>Triển khai luồng đánh giá theo cấu hình kỳ.</summary>
public sealed class EvaluationWorkflowService : IEvaluationWorkflowService
{
    private const int MaxReasonLength = 2000;
    private const int MaxCommentLength = 4000;

    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttachmentService _attachments;

    public EvaluationWorkflowService(
        IEvaluationWorkflowRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IAttachmentService attachments)
    {
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
        _attachments = attachments;
    }

    #region Hành động theo bước

    /// <inheritdoc />
    public Task<EvaluationRecordDto> SubmitTasksAsync(Guid recordId, SubmitTasksRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.SubmitTasks, null, async (ctx) =>
        {
            var tasks = request.Tasks ?? new List<TaskInputDto>();
            var error = EvaluationScoring.ValidateTaskRegistration(tasks.Select(t => t.Weight).ToList(), ctx.Settings.Parameters);
            if (error != null)
                throw new ValidationException(error);
            if (tasks.Any(t => string.IsNullOrWhiteSpace(t.TaskName)))
                throw new ValidationException("Tên sản phẩm/nhiệm vụ không được để trống.");

            // T-47: tệp minh chứng mới gắn vào nhiệm vụ phải tồn tại và thuộc quyền người gửi.
            var existing = (await _repo.GetTasksAsync(ctx.Record.Id, ct))
                .Where(t => t.AttachmentId.HasValue).Select(t => t.AttachmentId!.Value).ToHashSet();
            await EnsureNewAttachmentsLinkableAsync(tasks.Select(t => t.AttachmentId), existing);

            var order = 1;
            var newTasks = tasks.Select(t => new EvaluationTask
            {
                Id = Guid.NewGuid(),
                RecordId = ctx.Record.Id,
                TaskOrder = order++,
                TaskName = t.TaskName.Trim(),
                TargetOutput = t.TargetOutput?.Trim() ?? string.Empty,
                Weight = t.Weight,
                Deadline = t.Deadline.HasValue ? DateTime.SpecifyKind(t.Deadline.Value, DateTimeKind.Utc) : ctx.Record.Period.EndDate,
                AttachmentId = t.AttachmentId,
                CriteriaA_Ratio = 1.0,
                CriteriaB_Ratio = 1.0,
                CriteriaC_Ratio = 1.0,
                CriteriaD_Ratio = 1.0,
                SelfScore = t.Weight // Khởi tạo điểm trần bằng trọng số (như trước task 12)
            }).ToList();
            await _repo.ReplaceTasksAsync(ctx.Record.Id, newTasks, ct);
            ctx.Record.ReturnReason = null;
            return "Nộp danh mục sản phẩm, nhiệm vụ.";
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ApproveTasksAsync(Guid recordId, CommentRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ApproveTasks, null, ctx =>
        {
            ctx.Record.TasksApprovedById = ctx.ActorId;
            ctx.Record.TasksApprovedByName = ctx.ActorName;
            ctx.Record.TasksApprovedAt = ctx.Now;
            ctx.Record.TasksApprovalComment = Trim(request.Comment, MaxCommentLength, "Ý kiến");
            return Task.FromResult<string?>("Duyệt danh mục sản phẩm.");
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ReturnTasksAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ReturnTasks, request.Reason, ctx =>
        {
            ctx.Record.TasksApprovedById = null;
            ctx.Record.TasksApprovedByName = null;
            ctx.Record.TasksApprovedAt = null;
            return Task.FromResult<string?>(null);
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> SubmitSelfScoreAsync(Guid recordId, SubmitSelfScoreRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.SubmitSelfScore, null, async ctx =>
        {
            var record = ctx.Record;
            var parameters = ctx.Settings.Parameters;
            var generalError = EvaluationScoring.ValidateGeneralScores(request.GeneralScores, parameters);
            if (generalError != null)
                throw new ValidationException(generalError);

            record.GeneralScoreT1 = request.GeneralScores[0];
            record.GeneralScoreT2 = request.GeneralScores[1];
            record.GeneralScoreT3 = request.GeneralScores[2];
            record.GeneralScoreT4 = request.GeneralScores[3];
            record.GeneralScoreT5 = request.GeneralScores[4];
            record.GeneralScoreT6 = request.GeneralScores[5];
            record.GeneralCriteriaScore = EvaluationScoring.GeneralCriteriaScore(request.GeneralScores);

            if (ctx.Settings.UsesAxisScoring)
            {
                var axisError = EvaluationScoring.ValidateAxisScores(request.AxisScores, parameters);
                if (axisError != null)
                    throw new ValidationException(axisError);
                var axis = request.AxisScores!;
                (record.AxisScoreT1, record.AxisScoreT2, record.AxisScoreT3) = (axis[0], axis[1], axis[2]);
                (record.AxisScoreT4, record.AxisScoreT5, record.AxisScoreT6) = (axis[3], axis[4], axis[5]);
                record.TasksScore = EvaluationScoring.AxisTasksScore(axis);
                record.SelfScoreForm = PeriodSettings.Form09B;
            }
            else
            {
                var tasks = await _repo.GetTasksAsync(record.Id, ct);
                if (tasks.Count == 0)
                    throw new ValidationException("Hồ sơ chưa có danh mục sản phẩm (Mẫu 01) nên chưa tự chấm theo Mẫu 09A được.");

                // T-47: tệp minh chứng mới gắn vào nhiệm vụ phải tồn tại và thuộc quyền người gửi.
                var changed = tasks
                    .Select(task => (task, input: request.TaskScores?.FirstOrDefault(s => s.TaskId == task.Id)))
                    .Where(x => x.input != null && x.input.AttachmentId != x.task.AttachmentId)
                    .Select(x => x.input!.AttachmentId);
                await EnsureNewAttachmentsLinkableAsync(changed, new HashSet<Guid>());

                var taskScores = new List<double>();
                foreach (var task in tasks)
                {
                    var input = request.TaskScores?.FirstOrDefault(s => s.TaskId == task.Id);
                    if (input != null)
                    {
                        var scored = EvaluationScoring.ScoreTask(task.Weight, input.CriteriaA_Ratio, input.CriteriaB_Ratio,
                            input.CriteriaC_Ratio, input.CriteriaD_Ratio, record.JobGroup, parameters);
                        task.CriteriaA_Ratio = scored.A;
                        task.CriteriaB_Ratio = scored.B;
                        task.CriteriaC_Ratio = scored.C;
                        task.CriteriaD_Ratio = scored.D;
                        task.IsExceedStandard = input.IsExceedStandard;
                        task.AttachmentId = input.AttachmentId;
                        task.SelfScore = scored.Score;
                    }
                    taskScores.Add(task.SelfScore);
                }

                record.TasksScore = EvaluationScoring.TasksScore(taskScores);
                (record.AxisScoreT1, record.AxisScoreT2, record.AxisScoreT3) = (null, null, null);
                (record.AxisScoreT4, record.AxisScoreT5, record.AxisScoreT6) = (null, null, null);
                record.SelfScoreForm = PeriodSettings.Form09A;
            }

            record.TotalSelfScore = EvaluationScoring.TotalScore(record.GeneralCriteriaScore, record.TasksScore);
            if (string.IsNullOrWhiteSpace(request.SelfProposedGrade))
            {
                record.SelfProposedGrade = EvaluationScoring.GradeFromScore(record.TotalSelfScore, parameters);
            }
            else
            {
                record.SelfProposedGrade = EvaluationMapping.ParseGrade(request.SelfProposedGrade)
                    ?? throw new ValidationException("Mức tự đề xuất không hợp lệ.");
            }

            record.SelfScoredAt = ctx.Now;
            record.ReturnReason = null;
            return "Nộp phiếu tự chấm.";
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ConfirmByCellAsync(Guid recordId, CommentRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ConfirmByCell, null, ctx =>
        {
            ctx.Record.PartyCellComment = Trim(request.Comment, MaxCommentLength, "Ý kiến xác nhận") ?? string.Empty;
            ctx.Record.CellConfirmedById = ctx.ActorId;
            ctx.Record.CellConfirmedByName = ctx.ActorName;
            ctx.Record.CellConfirmedAt = ctx.Now;
            return Task.FromResult<string?>("Chi bộ xác nhận phiếu tự chấm.");
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ReturnByCellAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ReturnByCell, request.Reason, _ => Task.FromResult<string?>(null), ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> RecordCollectiveProposalAsync(Guid recordId, CollectiveProposalRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.RecordCollectiveProposal, null, async ctx =>
        {
            var grade = EvaluationMapping.ParseGrade(request.ProposedGrade)
                ?? throw new ValidationException("Hãy chọn mức xếp loại do tập thể lãnh đạo đề xuất.");
            await AttachVotesAsync(ctx.Record, request.MeetingId, request.Votes, WorkflowStep.B3A_COLLECTIVE, ct);

            ctx.Record.CollectiveProposedGrade = grade;
            ctx.Record.CollectiveComment = Trim(request.Comment, MaxCommentLength, "Nhận xét");
            ctx.Record.CollectiveMeetingId = request.MeetingId;
            ctx.Record.CollectiveRecordedById = ctx.ActorId;
            ctx.Record.CollectiveRecordedByName = ctx.ActorName;
            ctx.Record.CollectiveRecordedAt = ctx.Now;
            return "Ghi nhận đề xuất của tập thể lãnh đạo.";
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> AppraiseAsync(Guid recordId, AppraisalRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.Appraise, null, ctx =>
        {
            var grade = EvaluationMapping.ParseGrade(request.ProposedGrade)
                ?? throw new ValidationException("Hãy chọn mức xếp loại do cơ quan thẩm định đề xuất.");
            if (request.AppraisalScore is < 0 or > 100)
                throw new ValidationException("Điểm thẩm định phải từ 0 đến 100.");

            ctx.Record.AppraisalScore = request.AppraisalScore;
            ctx.Record.AppraisalComment = Trim(request.Comment, MaxCommentLength, "Ý kiến thẩm định") ?? string.Empty;
            ctx.Record.AppraisalProposedGrade = grade;
            ctx.Record.AppraisedById = ctx.ActorId;
            ctx.Record.AppraisedByName = ctx.ActorName;
            ctx.Record.AppraisedAt = ctx.Now;
            return Task.FromResult<string?>("Thẩm định hồ sơ.");
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ReturnByAppraiserAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ReturnByAppraiser, request.Reason, _ => Task.FromResult<string?>(null), ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> DirectorReviewAsync(Guid recordId, DirectorReviewRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.DirectorReview, null, ctx =>
        {
            var grade = EvaluationMapping.ParseGrade(request.ProposedGrade)
                ?? throw new ValidationException("Hãy chọn mức xếp loại do cấp trực tiếp sử dụng đề xuất.");
            ctx.Record.DirectorComment = Trim(request.Comment, MaxCommentLength, "Nhận xét");
            ctx.Record.DirectorProposedGrade = grade;
            ctx.Record.DirectorReviewedById = ctx.ActorId;
            ctx.Record.DirectorReviewedByName = ctx.ActorName;
            ctx.Record.DirectorReviewedAt = ctx.Now;
            return Task.FromResult<string?>("Nhận xét, đề xuất của cấp trực tiếp sử dụng.");
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> RecordDecisionAsync(Guid recordId, DecisionRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.RecordDecision, null, async ctx =>
        {
            var grade = EvaluationMapping.ParseGrade(request.FinalGrade)
                ?? throw new ValidationException("Hãy chọn mức xếp loại được quyết định.");
            if (request.FinalScore is < 0 or > 100)
                throw new ValidationException("Điểm chính thức phải từ 0 đến 100.");
            await AttachVotesAsync(ctx.Record, request.MeetingId, request.Votes, WorkflowStep.B4_DECISION, ct);

            ctx.Record.FinalGrade = grade;
            ctx.Record.FinalScore = request.FinalScore ?? ctx.Record.AppraisalScore ?? ctx.Record.TotalSelfScore;
            ctx.Record.DecisionDocumentNumber = Trim(request.DocumentNumber, 100, "Số văn bản");
            ctx.Record.DecisionDocumentDate = request.DocumentDate.HasValue
                ? DateTime.SpecifyKind(request.DocumentDate.Value, DateTimeKind.Utc)
                : null;
            ctx.Record.DecisionAuthorityName = Trim(request.AuthorityName, 300, "Cơ quan quyết định");
            ctx.Record.DecisionMeetingId = request.MeetingId;
            ctx.Record.DecisionRecordedById = ctx.ActorId;
            ctx.Record.DecisionRecordedByName = ctx.ActorName;
            ctx.Record.DecisionRecordedAt = ctx.Now;
            return "Ghi nhận quyết định mức xếp loại.";
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> PublishAsync(Guid recordId, WorkflowRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.Publish, null, ctx =>
        {
            if (ctx.Record.FinalGrade == EvaluationGrade.ChuaXepLoai && ctx.Record.DecisionRecordedAt == null)
                throw new ConflictException("Hồ sơ chưa có quyết định mức xếp loại nên chưa công bố được.");
            ctx.Record.PublishedById = ctx.ActorId;
            ctx.Record.PublishedByName = ctx.ActorName;
            ctx.Record.PublishedAt = ctx.Now;
            return Task.FromResult<string?>("Công bố, khóa kết quả.");
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ReopenAsync(Guid recordId, ReopenRequestDto request, CancellationToken ct = default)
    {
        var target = WorkflowSteps.Parse(request.TargetStep)
            ?? throw new ValidationException("Hãy chọn bước mà hồ sơ quay về khi mở lại.");
        return ExecuteAsync(recordId, request, WorkflowActions.Reopen, request.Reason, async ctx =>
        {
            // Giữ dữ liệu các bước sau để đối chiếu (lịch sử ghi điểm/mức trước–sau); chỉ gỡ dấu công bố.
            ctx.Record.PublishedById = null;
            ctx.Record.PublishedByName = null;
            ctx.Record.PublishedAt = null;

            // Kỳ đã đóng → quay về "Khóa dữ liệu" để các bước sau khi mở lại thao tác được (ghi vào kỳ, có lý do).
            var period = ctx.Record.Period;
            if (period.Status == PeriodStatus.Closed)
            {
                period.Status = PeriodStatus.Locked;
                period.StatusReason = $"Mở lại hồ sơ của {ctx.Record.Member?.FullName}: {request.Reason?.Trim()}";
                period.StatusChangedAt = ctx.Now;
                period.StatusChangedBy = ctx.ActorId;
            }

            await Task.CompletedTask;
            return "Mở lại hồ sơ đã công bố.";
        }, ct, target);
    }

    #endregion

    #region Hành động được phép, việc cần xử lý

    /// <inheritdoc />
    public async Task<RecordActionsDto> GetActionsAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");
        _guard.Ensure(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));

        var settings = ReadSettings(record.Period);
        var today = EvaluationMapping.Today(DateTime.UtcNow);
        var actions = AvailableActions(record, settings, today).ToList();
        return new RecordActionsDto
        {
            RecordId = record.Id,
            Version = record.Version,
            Status = record.Status.ToString(),
            Actions = actions
        };
    }

    /// <inheritdoc />
    public async Task<WorkQueueDto> GetWorkQueueAsync(Guid? periodId, CancellationToken ct = default)
    {
        var periods = await _repo.ListPeriodsAsync(ct);
        var targetPeriods = periodId.HasValue && periodId.Value != Guid.Empty
            ? periods.Where(p => p.Id == periodId.Value).ToList()
            : periods.Where(p => p.Status is PeriodStatus.Open or PeriodStatus.Locked).ToList();
        if (targetPeriods.Count == 0)
            return new WorkQueueDto();

        // Chỉ các bước người dùng có quyền ở phạm vi nào đó; lọc sơ bộ theo phạm vi guard rồi kiểm tra chính xác từng hồ sơ.
        var steps = WorkflowSteps.Ordered
            .Where(step => StepPermissions(step).Any(code => code == PermissionCodes.EvaluationSelf || _guard.HasAny(code)))
            .ToList();
        if (steps.Count == 0)
            return new WorkQueueDto();

        var statuses = steps.Select(WorkflowSteps.StatusOf).ToList();
        var records = await _repo.ListRecordsByStatusAsync(targetPeriods.Select(p => p.Id).ToList(), statuses, ct);
        var settingsByPeriod = targetPeriods.ToDictionary(p => p.Id, ReadSettings);
        var periodById = targetPeriods.ToDictionary(p => p.Id);
        var today = EvaluationMapping.Today(DateTime.UtcNow);
        var userId = _currentUser.UserId;

        var items = new List<(WorkflowStep Step, WorkQueueItemDto Item)>();
        foreach (var record in records)
        {
            var step = WorkflowSteps.StepOf(record.Status);
            if (step == null)
                continue;
            var settings = settingsByPeriod[record.PeriodId];
            if (!settings.IsEnabled(step.Value))
                continue;

            var complete = WorkflowActions.CompleteOf(step.Value);
            var period = periodById[record.PeriodId];
            if (WorkflowActions.PeriodBlockReason(period.Status, complete) != null)
                continue;
            if (!_guard.Can(WorkflowActions.PermissionFor(complete, record.ApprovalAuthority), AccessTarget.ForRecord(record)))
                continue;

            var deadline = settings.Deadline(step.Value);
            items.Add((step.Value, new WorkQueueItemDto
            {
                RecordId = record.Id,
                Version = record.Version,
                PeriodId = record.PeriodId,
                PeriodName = period.Name,
                MemberId = record.MemberId,
                FullName = record.Member?.FullName ?? string.Empty,
                DepartmentName = record.Department?.Name,
                PartyCellName = record.PartyCell?.Name,
                ApprovalAuthority = record.ApprovalAuthority.ToString(),
                Status = record.Status.ToString(),
                StatusDisplayName = WorkflowSteps.StatusDisplayName(record.Status),
                IsOwnRecord = userId.HasValue && record.MemberId == userId.Value,
                ReturnReason = record.ReturnReason,
                Deadline = deadline,
                Overdue = deadline.HasValue && today > deadline.Value
            }));
        }

        var groups = items
            .GroupBy(x => x.Step)
            .OrderBy(g => WorkflowSteps.IndexOf(g.Key))
            .Select(g => new WorkQueueGroupDto
            {
                Step = WorkflowSteps.Code(g.Key),
                StepName = WorkflowSteps.DisplayName(g.Key),
                Count = g.Count(),
                Items = g.Select(x => x.Item).OrderBy(x => x.PeriodName).ThenBy(x => x.FullName).ToList()
            })
            .ToList();

        return new WorkQueueDto { Total = items.Count, Groups = groups };
    }

    /// <summary>Các mã quyền có thể thực hiện bước (B4 gồm cả hai cấp quyết định).</summary>
    private static IEnumerable<string> StepPermissions(WorkflowStep step)
    {
        var complete = WorkflowActions.CompleteOf(step);
        if (step == WorkflowStep.B4_DECISION)
            return new[] { PermissionCodes.EvaluationDecide, PermissionCodes.EvaluationDecideExternal };
        return new[] { WorkflowActions.PermissionFor(complete, ApprovalAuthority.CoSo) };
    }

    /// <summary>Tính các hành động được phép trên hồ sơ.</summary>
    private IEnumerable<RecordActionDto> AvailableActions(EvaluationRecord record, PeriodSettings settings, DateOnly today)
    {
        var enabled = settings.EnabledSteps();
        var target = AccessTarget.ForRecord(record);
        var candidates = new List<WorkflowActionDefinition>();

        if (record.Status == RecordStatus.Published)
        {
            candidates.Add(WorkflowActions.Get(WorkflowActions.Reopen));
        }
        else if (WorkflowSteps.StepOf(record.Status) is { } step && enabled.Contains(step))
        {
            candidates.Add(WorkflowActions.CompleteOf(step));
            if (WorkflowActions.ReturnOf(step) is { } returnAction)
                candidates.Add(returnAction);
        }

        foreach (var action in candidates)
        {
            if (WorkflowActions.PeriodBlockReason(record.Period.Status, action) != null)
                continue;
            if (!_guard.Can(WorkflowActions.PermissionFor(action, record.ApprovalAuthority), target))
                continue;

            var command = action.Kind switch
            {
                WorkflowAction.Return => WorkflowCommand.Return(action.Step),
                WorkflowAction.Reopen => null,
                _ => WorkflowCommand.Complete(action.Step)
            };
            if (command != null && !RecordStateMachine.Apply(record.Status, enabled, command).Succeeded)
                continue;

            var deadline = settings.Deadline(action.Step);
            var overdue = action.Kind == WorkflowAction.Complete && deadline.HasValue && today > deadline.Value;
            if (overdue && settings.EnforceDeadlines)
                continue;

            yield return new RecordActionDto
            {
                Action = action.Code,
                Step = WorkflowSteps.Code(action.Step),
                Label = action.Label,
                RequiresReason = action.Kind is WorkflowAction.Return or WorkflowAction.Reopen,
                ReasonOptional = false,
                Overdue = overdue,
                TargetSteps = action.Kind == WorkflowAction.Reopen
                    ? RecordStateMachine.ReopenTargets(enabled).Select(WorkflowSteps.Code).ToList()
                    : null
            };
        }
    }

    #endregion

    #region Khung thực thi chung

    /// <summary>Ngữ cảnh của một hành động đang thực hiện.</summary>
    private sealed record ActionContext(EvaluationRecord Record, PeriodSettings Settings, Guid? ActorId, string ActorName, DateTime Now);

    /// <summary>
    /// Trình tự chung: phiên bản bắt buộc → nạp hồ sơ → guard → so phiên bản → luật kỳ + thời hạn → lý do → máy trạng thái →
    /// áp dữ liệu → lịch sử → lưu (xmin).
    /// </summary>
    private async Task<EvaluationRecordDto> ExecuteAsync(
        Guid recordId,
        WorkflowRequestDto request,
        string actionCode,
        string? reason,
        Func<ActionContext, Task<string?>> apply,
        CancellationToken ct,
        WorkflowStep? reopenTarget = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        var action = WorkflowActions.Get(actionCode);

        // T-24: mọi hành động ghi phải gửi phiên bản đã đọc.
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại hồ sơ rồi thực hiện lại.");

        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

        _guard.Ensure(WorkflowActions.PermissionFor(action, record.ApprovalAuthority), AccessTarget.ForRecord(record));

        if (record.Version != request.Version.Value)
            throw new ConflictException("Hồ sơ đã được người khác cập nhật sau khi bạn mở. Hãy tải lại hồ sơ để xem dữ liệu mới nhất rồi thực hiện lại.");

        var period = record.Period;
        var settings = ReadSettings(period);
        var periodBlock = WorkflowActions.PeriodBlockReason(period.Status, action);
        if (periodBlock != null)
            throw new ConflictException(periodBlock);

        var now = DateTime.UtcNow;
        if (action.Kind == WorkflowAction.Complete && settings.EnforceDeadlines
            && settings.Deadline(action.Step) is { } deadline && EvaluationMapping.Today(now) > deadline)
        {
            throw new ConflictException(
                $"Đã quá thời hạn của bước \"{WorkflowSteps.DisplayName(action.Step)}\" ({deadline.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}). "
                + "Liên hệ người quản lý kỳ để gia hạn.");
        }

        var requiresReason = action.Kind is WorkflowAction.Return or WorkflowAction.Reopen;
        var cleanReason = reason?.Trim();
        if (requiresReason && string.IsNullOrWhiteSpace(cleanReason))
            throw new ValidationException(action.Kind == WorkflowAction.Return
                ? "Hãy nhập lý do trả lại để chủ hồ sơ biết cần sửa gì."
                : "Hãy nhập lý do mở lại hồ sơ.");
        if (cleanReason is { Length: > MaxReasonLength })
            throw new ValidationException($"Lý do không được dài quá {MaxReasonLength} ký tự.");

        var command = action.Kind switch
        {
            WorkflowAction.Return => WorkflowCommand.Return(action.Step),
            WorkflowAction.Reopen => WorkflowCommand.Reopen(reopenTarget ?? WorkflowSteps.EarliestReopenStep),
            _ => WorkflowCommand.Complete(action.Step)
        };
        var transition = RecordStateMachine.Apply(record.Status, settings.EnabledSteps(), command);
        if (!transition.Succeeded)
            throw new ConflictException(transition.Error!);

        var actorId = _currentUser.UserId;
        var actorName = actorId.HasValue
            ? await _repo.GetMemberNameAsync(actorId.Value, ct) ?? _currentUser.UserName
            : _currentUser.UserName;
        var scoreBefore = record.EffectiveScore();
        var gradeBefore = record.EffectiveGrade();
        var fromStatus = record.Status;

        var comment = await apply(new ActionContext(record, settings, actorId, actorName, now));

        if (action.Kind == WorkflowAction.Return)
            record.ReturnReason = cleanReason;
        record.Status = transition.Status;
        record.UpdatedAt = now;

        _repo.AddHistory(new EvaluationRecordHistory
        {
            RecordId = record.Id,
            FromStatus = fromStatus,
            ToStatus = record.Status,
            Step = action.Kind == WorkflowAction.Reopen ? reopenTarget : action.Step,
            Action = action.Kind,
            Reason = requiresReason ? cleanReason : null,
            ScoreBefore = scoreBefore,
            ScoreAfter = record.EffectiveScore(),
            GradeBefore = gradeBefore,
            GradeAfter = record.EffectiveGrade(),
            ActorId = actorId,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            Comment = comment ?? action.Label,
            CreatedAt = now
        });

        _unitOfWork.SetOriginalVersion(record, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);

        var saved = await _repo.FindRecordAsync(record.Id, ct) ?? record;
        var evidence = await _repo.GetCurrentEvidenceAsync(saved.Tasks.ToList(), ct);
        return EvaluationMapping.ToDto(saved, ReadSettings(saved.Period), EvaluationMapping.Today(DateTime.UtcNow), evidence);
    }

    /// <summary>
    /// Gắn kết quả kiểm phiếu (chỉ tổng hợp) của hồ sơ vào biên bản: biên bản cùng kỳ, đúng bước, hồ sơ thuộc đơn vị của
    /// biên bản; tổng phiếu các mức + không hợp lệ ≤ số có mặt; không âm (thiết kế mục 4).
    /// </summary>
    private async Task AttachVotesAsync(EvaluationRecord record, Guid? meetingId, VoteTallyDto? votes, WorkflowStep stage, CancellationToken ct)
    {
        if (meetingId is null)
        {
            if (votes != null)
                throw new ValidationException("Kết quả kiểm phiếu phải gắn với một biên bản hội nghị (Mẫu 12/13). Hãy chọn biên bản.");
            return;
        }

        var meeting = await _repo.FindMeetingAsync(meetingId.Value, ct)
            ?? throw new ValidationException("Không tìm thấy biên bản hội nghị đã chọn.");
        if (meeting.PeriodId != record.PeriodId)
            throw new ValidationException("Biên bản thuộc kỳ đánh giá khác với hồ sơ.");
        if (meeting.Stage.HasValue && meeting.Stage.Value != stage)
            throw new ValidationException($"Biên bản đã chọn không dùng cho bước \"{WorkflowSteps.DisplayName(stage)}\".");
        if ((meeting.PartyCellId.HasValue && meeting.PartyCellId != record.PartyCellId)
            || (meeting.DepartmentId.HasValue && meeting.DepartmentId != record.DepartmentId))
            throw new ValidationException("Hồ sơ không thuộc Phòng/Chi bộ của biên bản đã chọn.");

        if (votes == null)
            return;

        var counts = new[] { votes.VotesExcellent, votes.VotesGood, votes.VotesSatisfactory, votes.VotesUnsatisfactory, votes.InvalidVotes };
        if (counts.Any(c => c < 0))
            throw new ValidationException("Số phiếu không được âm.");
        var total = counts.Sum();
        if (total > meeting.PresentCount)
            throw new ValidationException(
                $"Tổng số phiếu các mức và phiếu không hợp lệ ({total}) vượt số người có mặt ghi trong biên bản ({meeting.PresentCount}). Hãy kiểm tra lại kết quả kiểm phiếu.");

        var summary = meeting.VoteSummaries.FirstOrDefault(v => v.RecordId == record.Id);
        if (summary == null)
        {
            summary = new EvaluationMeetingVoteSummary { MeetingId = meeting.Id, RecordId = record.Id };
            _repo.AddVoteSummary(summary);
        }

        summary.VotesExcellent = votes.VotesExcellent;
        summary.VotesGood = votes.VotesGood;
        summary.VotesSatisfactory = votes.VotesSatisfactory;
        summary.VotesUnsatisfactory = votes.VotesUnsatisfactory;
        summary.InvalidVotes = votes.InvalidVotes;
        summary.Notes = votes.Notes?.Trim() ?? string.Empty;
        meeting.UpdatedAt = DateTime.UtcNow;
    }

    private async Task EnsureNewAttachmentsLinkableAsync(IEnumerable<Guid?> attachmentIds, ISet<Guid> alreadyLinked)
    {
        var newIds = attachmentIds
            .Where(id => id.HasValue && id.Value != Guid.Empty && !alreadyLinked.Contains(id.Value))
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (newIds.Count > 0 && _currentUser.UserId is { } userId)
            await _attachments.EnsureCanLinkAttachmentsAsync(newIds, userId);
    }

    private static PeriodSettings ReadSettings(EvaluationPeriod period)
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

    private static string? Trim(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{field} không được dài quá {maxLength} ký tự.");
        return trimmed;
    }

    #endregion
}
