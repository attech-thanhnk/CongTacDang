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
/// Luồng đánh giá theo cấu hình kỳ. Mọi hành động: nạp hồ sơ → <c>guard.Ensure</c> → phiên bản → trạng thái kỳ + máy trạng thái
/// → áp dữ liệu → lịch sử → <c>SaveChanges</c> (xmin).
/// </summary>
public interface IEvaluationWorkflowService
{
    Task<EvaluationRecordDto> SubmitTasksAsync(Guid recordId, SubmitTasksRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ApproveTasksAsync(Guid recordId, ApproveTasksRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReturnTasksAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> RevokeTaskApprovalAsync(Guid recordId, WorkflowRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> SubmitSelfScoreAsync(Guid recordId, SubmitSelfScoreRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Cùng điều kiện như nộp phiếu nhưng không chuyển trạng thái, không ghi lịch sử; chỉ kiểm định dạng, không bắt buộc nhập đủ.
    /// </summary>
    Task<EvaluationRecordDto> SaveSelfScoreDraftAsync(Guid recordId, SubmitSelfScoreRequestDto request, CancellationToken ct = default);

    Task<EvaluationRecordDto> ConfirmByCellAsync(Guid recordId, CommentRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReturnByCellAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> RecordCollectiveProposalAsync(Guid recordId, CollectiveProposalRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> AppraiseAsync(Guid recordId, AppraisalRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReturnByAppraiserAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> DirectorReviewAsync(Guid recordId, DirectorReviewRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> RecordDecisionAsync(Guid recordId, DecisionRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> PublishAsync(Guid recordId, WorkflowRequestDto request, CancellationToken ct = default);
    Task<EvaluationRecordDto> ReopenAsync(Guid recordId, ReopenRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Kỳ khóa dữ liệu, hồ sơ đã mở riêng và chưa tới thẩm định: <c>evaluation.reopen</c> hoặc quyền bước thẩm định; bắt buộc lý do.
    /// </summary>
    Task<EvaluationRecordDto> ExtendUnlockAsync(Guid recordId, UnlockRecordRequestDto request, CancellationToken ct = default);

    /// <summary>Bước do cấp trên thực hiện: <c>evaluation.external.record</c>; hồ sơ sang bước áp dụng kế tiếp.</summary>
    Task<EvaluationRecordDto> RecordExternalResultAsync(Guid recordId, string step, ExternalResultRequestDto request, CancellationToken ct = default);

    Task<RecordActionsDto> GetActionsAsync(Guid recordId, CancellationToken ct = default);

    /// <summary>Nhóm theo bước, lọc bằng phạm vi guard.</summary>
    Task<WorkQueueDto> GetWorkQueueAsync(Guid? periodId, CancellationToken ct = default);
}

public sealed class EvaluationWorkflowService : IEvaluationWorkflowService
{
    private const int MaxReasonLength = 2000;
    private const int MaxCommentLength = 4000;
    private const int MaxCadreProposalLength = 2000;
    private const int MaxTaskAppraisalCommentLength = 1000;

    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttachmentService _attachments;
    private readonly IPostPublishWorkQueue? _postPublish;
    private readonly ITaskChangeWorkQueue? _taskChanges;

    public EvaluationWorkflowService(
        IEvaluationWorkflowRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IAttachmentService attachments,
        IPostPublishWorkQueue? postPublish = null,
        ITaskChangeWorkQueue? taskChanges = null)
    {
        _taskChanges = taskChanges;
        _repo = repo;
        _unitOfWork = unitOfWork;
        _guard = guard;
        _currentUser = currentUser;
        _attachments = attachments;
        _postPublish = postPublish;
    }

    #region Hành động theo bước

    /// <inheritdoc />
    public Task<EvaluationRecordDto> SubmitTasksAsync(Guid recordId, SubmitTasksRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.SubmitTasks, null, async (ctx) =>
        {
            var criteria = RequireCriteria(ctx);
            var tasks = request.Tasks ?? new List<TaskInputDto>();
            var error = EvaluationScoring.ValidateTaskRegistration(tasks.Select(t => t.Weight).ToList(), criteria.Content.Parameters);
            if (error != null)
                throw new ValidationException(error);
            ValidateTaskRows(tasks, criteria.Content);

            // Tệp minh chứng mới gắn vào nhiệm vụ phải tồn tại và thuộc quyền người gửi.
            var existing = (await _repo.GetTasksAsync(ctx.Record.Id, ct))
                .Where(t => t.AttachmentId.HasValue).Select(t => t.AttachmentId!.Value).ToHashSet();
            await EnsureNewAttachmentsLinkableAsync(tasks.Select(t => t.AttachmentId), existing);

            var order = 1;
            var newTasks = tasks.Select(t => new EvaluationTask
            {
                Id = Guid.NewGuid(),
                RecordId = ctx.Record.Id,
                TaskOrder = order++,
                TaskName = t.TaskName!.Trim(),
                TargetOutput = t.TargetOutput?.Trim() ?? string.Empty,
                Weight = t.Weight,
                Deadline = t.Deadline.HasValue ? DateTime.SpecifyKind(t.Deadline.Value, DateTimeKind.Utc) : ctx.Record.Period.EndDate,
                AxisCode = criteria.Content.FindAxis(t.AxisCode)?.Code,
                ProductCode = string.IsNullOrWhiteSpace(t.ProductCode) ? null : t.ProductCode.Trim(),
                Kind = ParseTaskKind(t.Kind),
                Role = ParseTaskRole(t.Role),
                ExceedCriteria = t.ExceedCriteria!.Trim(),
                EvidenceSource = t.EvidenceSource!.Trim(),
                Confirmer = t.Confirmer!.Trim(),
                AttachmentId = t.AttachmentId,
                CriteriaA_Ratio = 1.0,
                CriteriaB_Ratio = 1.0,
                CriteriaC_Ratio = 1.0,
                CriteriaD_Ratio = 1.0,
                SelfScore = 0 // Chưa tự chấm (TaskSelfScores.IsPending): phiếu để trống cho tới khi chủ hồ sơ nhập tỷ lệ A-B-C-D
            }).ToList();
            await _repo.ReplaceTasksAsync(ctx.Record.Id, newTasks, ct);
            ctx.Record.ReturnReason = null;
            return "Nộp danh mục sản phẩm, nhiệm vụ.";
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ApproveTasksAsync(Guid recordId, ApproveTasksRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ApproveTasks, null, ctx =>
        {
            var comment = Trim(request.Comment, MaxCommentLength, "Ý kiến");
            var history = "Duyệt danh mục sản phẩm.";

            // Cấp giao nhiệm vụ điều chỉnh tỷ lệ A-B-C-D của khung (trong biên độ của bộ tiêu chí).
            if (request.FrameAdjustment is { } adjust)
            {
                var content = RequireCriteria(ctx).Content;
                var baseFrame = RecordWeightFrames.Base(ctx.Record, content);
                var error = RecordWeightFrames.ValidateAdjustment(content, baseFrame, adjust.A, adjust.B, adjust.C, adjust.D);
                if (error != null)
                    throw new ValidationException(error);
                if (RecordWeightFrames.Apply(ctx.Record, baseFrame!, adjust.A, adjust.B, adjust.C, adjust.D))
                {
                    var effective = RecordWeightFrames.Effective(ctx.Record, content)!;
                    history += RecordWeightFrames.IsAdjusted(ctx.Record)
                        ? $" Điều chỉnh tỷ lệ A-B-C-D: {RecordWeightFrames.Describe(effective)} (khung {baseFrame!.Code}: {RecordWeightFrames.Describe(baseFrame)})."
                        : $" Bỏ điều chỉnh tỷ lệ A-B-C-D, dùng tỷ lệ của khung {baseFrame!.Code}: {RecordWeightFrames.Describe(baseFrame)}.";
                }
            }

            ctx.Record.TasksApprovedById = ctx.ActorId;
            ctx.Record.TasksApprovedByName = ctx.ActorName;
            ctx.Record.TasksApprovedAt = ctx.Now;
            ctx.Record.TasksApprovalComment = comment;
            return Task.FromResult<string?>(history);
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ReturnTasksAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ReturnTasks, request.Reason, ctx =>
        {
            ctx.Record.TasksApprovedById = null;
            ctx.Record.TasksApprovedByName = null;
            ctx.Record.TasksApprovedAt = null;
            return Task.FromResult<string?>(null);
        }, ct, unlockUntil: request.UnlockUntil);

    /// <inheritdoc />
    public async Task<EvaluationRecordDto> RevokeTaskApprovalAsync(Guid recordId, WorkflowRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại hồ sơ rồi thử lại.");

        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");
        var settings = ReadSettings(record.Period);
        var profile = EvaluationMapping.ProfileOf(settings, record);
        var revokeAction = WorkflowActions.Get(WorkflowActions.RevokeTaskApproval);

        if (record.Period.Status != PeriodStatus.Open)
            throw new ConflictException("Chỉ được thu hồi duyệt khi kỳ đánh giá đang mở.");
        if (!CanRevokeTaskApproval(record, profile))
            throw new ConflictException("Chỉ được thu hồi duyệt ngay sau khi duyệt danh mục và trước khi chủ hồ sơ tự chấm.");

        var histories = await _repo.ListHistoryAsync(record.Id, ct);
        var latest = histories.OrderByDescending(h => h.CreatedAt).FirstOrDefault();
        if (latest is null
            || latest.Action != WorkflowAction.Complete
            || latest.Step != WorkflowStep.B1_APPROVE
            || latest.ToStatus != RecordStatus.AwaitingSelfScore)
            throw new ConflictException("Hồ sơ đã có thao tác khác sau khi duyệt; không thể thu hồi tự động.");

        // Cùng mã quyền với API actions.
        var permission = WorkflowActions.PermissionFor(revokeAction, profile);
        if (IsConflictOfInterest(revokeAction, record))
            throw new ForbiddenException("Người được đánh giá không được thu hồi duyệt trên hồ sơ của chính mình.");
        _guard.Ensure(permission, AccessTarget.ForRecord(record));

        var actorId = _currentUser.UserId;
        if (record.TasksApprovedById != actorId)
            throw new ForbiddenException("Chỉ người đã bấm duyệt danh mục mới được thu hồi thao tác này.");

        if (record.Version != request.Version.Value)
            throw new ConflictException("Hồ sơ đã được người khác cập nhật. Hãy tải lại hồ sơ rồi thử lại.");

        var now = DateTime.UtcNow;
        var actorName = actorId.HasValue
            ? await _repo.GetMemberNameAsync(actorId.Value, ct) ?? _currentUser.UserName
            : _currentUser.UserName;
        var fromStatus = record.Status;
        var scoreBefore = record.EffectiveScore();
        var gradeBefore = record.EffectiveGrade();

        record.Status = RecordStatus.AwaitingRegistration;
        record.TasksApprovedById = null;
        record.TasksApprovedByName = null;
        record.TasksApprovedAt = null;
        record.TasksApprovalComment = null;
        record.FrameA = null;
        record.FrameB = null;
        record.FrameC = null;
        record.FrameD = null;
        record.ReturnReason = "Thu hồi duyệt danh mục để chỉnh sửa trước khi tự chấm.";
        record.UpdatedAt = now;

        _repo.AddHistory(new EvaluationRecordHistory
        {
            RecordId = record.Id,
            FromStatus = fromStatus,
            ToStatus = record.Status,
            Step = WorkflowStep.B1_APPROVE,
            Action = WorkflowAction.RevokeTaskApproval,
            ScoreBefore = scoreBefore,
            ScoreAfter = scoreBefore,
            GradeBefore = gradeBefore,
            GradeAfter = gradeBefore,
            ActorId = actorId,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            Comment = "Thu hồi duyệt danh mục trước khi tự chấm.",
            CreatedAt = now
        });

        _unitOfWork.SetOriginalVersion(record, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await LoadDtoAsync(record, ct);
    }

    /// <inheritdoc />
    public Task<EvaluationRecordDto> SubmitSelfScoreAsync(Guid recordId, SubmitSelfScoreRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.SubmitSelfScore, null, async ctx =>
        {
            var formChanges = await ApplySelfScoreAsync(ctx, request, draft: false, ct);
            ctx.Record.SelfScoredAt = ctx.Now;
            ctx.Record.ReturnReason = null;
            return formChanges.Count == 0
                ? "Nộp phiếu tự chấm."
                : $"Nộp phiếu tự chấm; cập nhật {string.Join(", ", formChanges)}.";
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> SaveSelfScoreDraftAsync(Guid recordId, SubmitSelfScoreRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.SubmitSelfScore, null, async ctx =>
        {
            await ApplySelfScoreAsync(ctx, request, draft: true, ct);
            return null;
        }, ct, draft: true);

    /// <summary>
    /// <paramref name="draft"/> = lưu nháp: chỉ kiểm định dạng, tổng tính trên phần đã nhập, mức trống giữ "chưa xếp loại".
    /// Trả các biểu mẫu có nội dung thay đổi (ghi lịch sử khi nộp).
    /// </summary>
    private async Task<List<string>> ApplySelfScoreAsync(ActionContext ctx, SubmitSelfScoreRequestDto request, bool draft, CancellationToken ct)
    {
        var record = ctx.Record;
        var criteria = RequireCriteria(ctx);
        var content = criteria.Content;
        var rounding = content.Parameters.Rounding;
        var general = request.GeneralScores ?? new Dictionary<string, GeneralItemScore>();
        var generalError = EvaluationScoring.ValidateGeneralScores(content, general, partial: draft);
        if (generalError != null)
            throw new ValidationException(generalError);
        if (general.Values.Any(s => s?.Reason is { Length: > MaxCommentLength }))
            throw new ValidationException($"Lý do/căn cứ của tiêu chí không được dài quá {MaxCommentLength} ký tự.");

        record.GeneralScores = EvaluationScoring.GeneralScoresToJson(content, general);
        record.GeneralCriteriaScore = EvaluationScoring.GeneralCriteriaScore(content, general);

        // Lưu nháp khi chưa có khung / danh mục: giữ nguyên điểm nhiệm vụ, vẫn lưu phần còn lại.
        var frame = RecordWeightFrames.Effective(record, content);
        if (frame == null && !draft)
            throw new ValidationException(string.IsNullOrEmpty(record.WeightFrameCode)
                ? "Hồ sơ chưa có khung tỷ trọng A-B-C-D nên chưa tự chấm được theo Mẫu 09A. Hãy liên hệ người quản lý kỳ để chọn khung."
                : $"Khung tỷ trọng \"{record.WeightFrameCode}\" của hồ sơ không có trong bộ tiêu chí \"{criteria.Name}\" của kỳ. "
                  + "Hãy liên hệ người quản lý kỳ để sửa khung.");

        var tasks = await _repo.GetTasksAsync(record.Id, ct);
        if (tasks.Count == 0 && !draft)
            throw new ValidationException("Hồ sơ chưa có danh mục sản phẩm (Mẫu 01) nên chưa tự chấm theo Mẫu 09A được.");

        if (frame != null)
        {
            // Tệp minh chứng mới gắn vào nhiệm vụ phải tồn tại và thuộc quyền người gửi.
            var changed = tasks
                .Select(task => (task, input: request.TaskScores?.FirstOrDefault(s => s.TaskId == task.Id)))
                .Where(x => x.input != null && x.input.AttachmentId != x.task.AttachmentId)
                .Select(x => x.input!.AttachmentId);
            await EnsureNewAttachmentsLinkableAsync(changed, new HashSet<Guid>());

            // Nộp phiếu: mọi nhiệm vụ còn hiệu lực phải đủ bốn tỷ lệ; không lấy giá trị đã lưu/khởi tạo thay phần chưa nhập.
            if (!draft)
            {
                var missing = tasks
                    .Where(TaskChangeRules.IsActive)
                    .Where(t => !IsComplete(request.TaskScores?.FirstOrDefault(s => s != null && s.TaskId == t.Id)))
                    .OrderBy(t => t.TaskOrder)
                    .Select(t => $"\"{t.TaskName}\"")
                    .ToList();
                if (missing.Count > 0)
                    throw new ValidationException($"Chưa nhập đủ tỷ lệ A-B-C-D cho nhiệm vụ: {string.Join(", ", missing)}.");
            }

            foreach (var task in tasks)
            {
                // Việc đã loại trừ / chuyển tiếp (Mẫu 04): không chấm, điểm 0.
                if (!TaskChangeRules.IsActive(task))
                {
                    task.SelfScore = 0;
                    task.IsExceedStandard = false;
                    continue;
                }
                var input = request.TaskScores?.FirstOrDefault(s => s != null && s.TaskId == task.Id);
                if (!IsComplete(input))
                    continue; // Chỉ xảy ra khi lưu nháp: giữ nội dung đã lưu của nhiệm vụ chưa nhập đủ.
                var scored = EvaluationScoring.ScoreTask(task.Weight, input!.CriteriaA_Ratio!.Value, input.CriteriaB_Ratio!.Value,
                    input.CriteriaC_Ratio!.Value, input.CriteriaD_Ratio!.Value, frame, rounding.TaskScore);
                task.CriteriaA_Ratio = scored.A;
                task.CriteriaB_Ratio = scored.B;
                task.CriteriaC_Ratio = scored.C;
                task.CriteriaD_Ratio = scored.D;
                task.IsExceedStandard = input.IsExceedStandard;
                task.AttachmentId = input.AttachmentId;
                task.SelfScore = scored.Score;
            }

            if (!draft)
                await EnsureExceedEvidenceAsync(tasks, ct);
        }

        record.TasksScore = EvaluationScoring.TasksScore(tasks.Select(t => t.SelfScore), rounding.TasksTotal);
        var scoredTasks = tasks.Where(TaskChangeRules.IsActive).ToList();
        var exceedRatio = EvaluationScoring.ExceedStandardRatio(scoredTasks.Count(t => t.IsExceedStandard), scoredTasks.Count);

        record.TotalSelfScore = EvaluationScoring.TotalScore(record.GeneralCriteriaScore, record.TasksScore, rounding.Total);
        if (!string.IsNullOrWhiteSpace(request.SelfProposedGrade))
        {
            record.SelfProposedGrade = EvaluationMapping.ParseGrade(request.SelfProposedGrade)
                ?? throw new ValidationException("Mức tự đề xuất không hợp lệ.");
        }
        else
        {
            record.SelfProposedGrade = draft
                ? EvaluationGrade.ChuaXepLoai
                : EvaluationScoring.SuggestGrade(content, record.TotalSelfScore, exceedRatio);
        }

        // null = giữ lý do đã lưu (client không gửi trường này); nháp không bắt buộc.
        var gradeExplanation = request.SelfGradeExplanation == null
            ? record.SelfGradeExplanation
            : Trim(request.SelfGradeExplanation, MaxCommentLength, "Lý do đề xuất mức");
        if (!draft)
            GradeProposalRules.EnsureExplained(content, WorkflowStep.B2_SELF_SCORE, record.SelfProposedGrade, record.TotalSelfScore, exceedRatio,
                gradeExplanation, "\"Lý do đề xuất mức\"");
        record.SelfGradeExplanation = gradeExplanation;

        return ApplyIndividualForms(record, criteria, request, partial: draft);
    }

    /// <summary>Tỷ lệ không phải số → 400.</summary>
    private static bool IsComplete(TaskScoreInputDto? input)
    {
        if (input == null)
            return false;
        var ratios = new[] { input.CriteriaA_Ratio, input.CriteriaB_Ratio, input.CriteriaC_Ratio, input.CriteriaD_Ratio };
        if (ratios.Any(r => r.HasValue && !double.IsFinite(r.Value)))
            throw new ValidationException("Tỷ lệ A-B-C-D phải là số.");
        return ratios.All(r => r.HasValue);
    }

    /// <summary>Nhiệm vụ vượt chuẩn phải có ít nhất một tệp minh chứng gắn với nhiệm vụ.</summary>
    private async Task EnsureExceedEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct)
    {
        var exceed = tasks.Where(t => TaskChangeRules.IsActive(t) && t.IsExceedStandard).ToList();
        if (exceed.Count == 0)
            return;
        var withEvidence = await _repo.GetTaskIdsWithEvidenceAsync(exceed, ct);
        var missing = exceed.Where(t => !withEvidence.Contains(t.Id))
            .OrderBy(t => t.TaskOrder)
            .Select(t => $"\"{t.TaskName}\"")
            .ToList();
        if (missing.Count > 0)
            throw new ValidationException($"Nhiệm vụ đánh dấu vượt chuẩn phải có minh chứng. Hãy đính kèm minh chứng cho: {string.Join(", ", missing)}.");
    }

    /// <summary>
    /// Trường null = giữ nội dung đã lưu; trả các biểu mẫu có nội dung thay đổi. <paramref name="partial"/> = lưu nháp:
    /// không bắt buộc mục 09C, bỏ qua dòng chưa có nội dung.
    /// </summary>
    private static List<string> ApplyIndividualForms(EvaluationRecord record, CriteriaSnapshot criteria, SubmitSelfScoreRequestDto request,
        bool partial = false)
    {
        var content = criteria.Content;
        var changed = new List<string>();
        var uses09C = criteria.AppliesForm(RecordFormCodes.Form09C);
        var uses9D = criteria.AppliesForm(RecordFormCodes.Form9D);

        if (request.AxisReports != null && (uses09C || uses9D))
        {
            var error = RecordFormContent.ValidateAxisReports(content, request.AxisReports, partial);
            if (error != null)
                throw new ValidationException(error);
            var json = RecordFormContent.AxisReportsToJson(content, request.AxisReports, includeTasks: uses09C, includeSummary: uses9D);
            if (json != record.AxisReports)
            {
                var before = AxisReportParts(content, record.AxisReports);
                var after = AxisReportParts(content, json);
                if (before.Tasks != after.Tasks)
                    changed.Add("nhiệm vụ theo trục Mẫu 09C");
                if (before.Summary != after.Summary)
                    changed.Add("Mẫu 9D");
            }
            record.AxisReports = json;
        }

        if (uses09C)
        {
            // Giới hạn ký tự và mục bắt buộc tính trên nội dung sẽ in (gửi lên hoặc đang lưu).
            var sections = request.SelfAssessment
                ?? RecordFormContent.ParseSelfAssessment(record.SelfAssessment).ToDictionary(kv => kv.Key, kv => (string?)kv.Value);
            var error = RecordFormContent.ValidateSelfAssessment(content, sections, ParseAxisReportsOrEmpty(record.AxisReports), partial);
            if (error != null)
                throw new ValidationException(error);
            if (request.SelfAssessment != null)
            {
                var json = RecordFormContent.SelfAssessmentToJson(content, request.SelfAssessment);
                if (json != record.SelfAssessment)
                    changed.Add("Mẫu 09C");
                record.SelfAssessment = json;
            }
        }

        if (request.GeneralGroupNotes != null)
        {
            var error = RecordFormContent.ValidateGeneralGroupNotes(content, request.GeneralGroupNotes);
            if (error != null)
                throw new ValidationException(error);
            var json = RecordFormContent.GeneralGroupNotesToJson(content, request.GeneralGroupNotes);
            if (json != record.GeneralGroupNotes)
                changed.Add("nhận xét theo nhóm tiêu chí chung");
            record.GeneralGroupNotes = json;
        }

        return changed;
    }

    private static Dictionary<string, AxisReport> ParseAxisReportsOrEmpty(string? json)
    {
        try
        {
            return RecordFormContent.ParseAxisReports(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return new Dictionary<string, AxisReport>(StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>Tách phần nhiệm vụ (09C) và tóm tắt (9D) để ghi lịch sử phần nào thay đổi.</summary>
    private static (string Tasks, string Summary) AxisReportParts(CriteriaSetContent content, string? json)
    {
        var input = ParseAxisReportsOrEmpty(json).ToDictionary(kv => kv.Key, kv => (AxisReport?)kv.Value);
        return (RecordFormContent.AxisReportsToJson(content, input, includeTasks: true, includeSummary: false),
            RecordFormContent.AxisReportsToJson(content, input, includeTasks: false, includeSummary: true));
    }

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
        ExecuteAsync(recordId, request, WorkflowActions.ReturnByCell, request.Reason, _ => Task.FromResult<string?>(null), ct,
            unlockUntil: request.UnlockUntil);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> RecordCollectiveProposalAsync(Guid recordId, CollectiveProposalRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.RecordCollectiveProposal, null, async ctx =>
        {
            var grade = RequireGrade(request.ProposedGrade, "Hãy chọn mức xếp loại do tập thể lãnh đạo đề xuất.");
            var summary = await AttachVotesAsync(ctx.Record, request.MeetingId, request.Votes, WorkflowStep.B3A_COLLECTIVE, ct);
            // Biên bản có kết quả kiểm phiếu của hồ sơ: mức ghi nhận phải là mức được biểu quyết quá bán.
            if (summary is { } tally && CollectiveVoteRules.Validate(grade, tally.Tally, tally.PresentCount) is { } voteError)
                throw new ValidationException(voteError);

            var comment = Trim(request.Comment, MaxCommentLength, "Nhận xét");
            if (ctx.Criteria is { } criteria && GradeProposalRules.HasScore(ctx.Record))
                GradeProposalRules.EnsureExplained(criteria.Content, WorkflowStep.B3A_COLLECTIVE, grade, ctx.Record.TotalSelfScore,
                    GradeProposalRules.ExceedRatio(ctx.Record, criteria), comment, "\"Nhận xét\"");

            ctx.Record.CollectiveProposedGrade = grade;
            ctx.Record.CollectiveComment = comment;
            ctx.Record.CollectiveMeetingId = request.MeetingId;
            ctx.Record.CollectiveRecordedById = ctx.ActorId;
            ctx.Record.CollectiveRecordedByName = ctx.ActorName;
            ctx.Record.CollectiveRecordedAt = ctx.Now;
            return "Ghi nhận đề xuất của tập thể lãnh đạo.";
        }, ct);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> AppraiseAsync(Guid recordId, AppraisalRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.Appraise, null, async ctx =>
        {
            var grade = RequireGrade(request.ProposedGrade, "Hãy chọn mức xếp loại do cơ quan thẩm định đề xuất.");
            var criteria = RequireCriteria(ctx);
            var (general, tasksScore, total) = AppraisalScores(request, criteria.Content);

            // Chênh lệch tự chấm – thẩm định vượt ngưỡng (hoặc làm đổi mức) → bắt buộc giải trình.
            var explanation = Trim(request.Explanation, MaxCommentLength, "Nội dung giải trình");
            if (explanation == null && EvaluationScoring.RequiresExplanation(criteria.Content, ctx.Record.TotalSelfScore, total))
            {
                var p = criteria.Content.Parameters;
                var diff = Math.Abs(ctx.Record.TotalSelfScore - total);
                var gradeChange = p.ExplanationOnGradeChange ? " hoặc làm đổi mức xếp loại" : string.Empty;
                throw new ValidationException(string.Create(CultureInfo.InvariantCulture,
                    $"Điểm thẩm định {total:0.##} chênh lệch {diff:0.##} điểm so với điểm tự chấm {ctx.Record.TotalSelfScore:0.##} (ngưỡng {p.ExplanationThreshold:0.##} điểm{gradeChange}): hãy nhập nội dung giải trình, căn cứ."));
            }
            GradeProposalRules.EnsureExplained(criteria.Content, WorkflowStep.B3B_APPRAISAL, grade, total,
                GradeProposalRules.ExceedRatio(ctx.Record, criteria), explanation, "\"Giải trình\"");

            // Bắt buộc xếp mức mọi nhiệm vụ.
            ApplyTaskLevels(await _repo.GetTasksAsync(ctx.Record.Id, ct), request.TaskLevels);

            ctx.Record.AppraisalScore = total;
            ctx.Record.AppraisalGeneralScore = general;
            ctx.Record.AppraisalTasksScore = tasksScore;
            ctx.Record.AppraisalExplanation = explanation;
            ctx.Record.AppraisalComment = Trim(request.Comment, MaxCommentLength, "Ý kiến thẩm định") ?? string.Empty;
            ctx.Record.AppraisalProposedGrade = grade;
            ctx.Record.AppraisedById = ctx.ActorId;
            ctx.Record.AppraisedByName = ctx.ActorName;
            ctx.Record.AppraisedAt = ctx.Now;
            return "Thẩm định hồ sơ.";
        }, ct);

    /// <summary>
    /// Theo nhóm (chung + nhiệm vụ), làm tròn theo quy tắc từng nhóm; không gửi điểm nhóm → dùng tổng (client cũ).
    /// Thẩm định nội bộ bắt buộc có điểm để kiểm chênh lệch với tự chấm.
    /// </summary>
    private static (double? General, double? Tasks, double Total) AppraisalScores(AppraisalRequestDto request, CriteriaSetContent criteria)
    {
        var p = criteria.Parameters;
        if (request.GeneralScore is null && request.TasksScore is null)
        {
            if (request.AppraisalScore is not { } legacy)
                throw new ValidationException("Hãy nhập điểm thẩm định nhóm tiêu chí chung và nhóm kết quả thực hiện nhiệm vụ.");
            var max = p.GeneralMaxScore + p.TotalTaskWeight;
            if (double.IsNaN(legacy) || legacy < 0 || legacy > max + CriteriaSetContent.Epsilon)
                throw new ValidationException(string.Create(CultureInfo.InvariantCulture, $"Điểm thẩm định phải từ 0 đến {max:0.##}."));
            return (null, null, EvaluationScoring.Round(legacy, p.Rounding.Total));
        }

        if (request.GeneralScore is not { } general || request.TasksScore is not { } tasks)
            throw new ValidationException("Hãy nhập đủ điểm thẩm định nhóm tiêu chí chung và nhóm kết quả thực hiện nhiệm vụ.");
        if (double.IsNaN(general) || general < 0 || general > p.GeneralMaxScore + CriteriaSetContent.Epsilon)
            throw new ValidationException(string.Create(CultureInfo.InvariantCulture,
                $"Điểm thẩm định nhóm tiêu chí chung phải từ 0 đến {p.GeneralMaxScore:0.##}."));
        if (double.IsNaN(tasks) || tasks < 0 || tasks > p.TotalTaskWeight + CriteriaSetContent.Epsilon)
            throw new ValidationException(string.Create(CultureInfo.InvariantCulture,
                $"Điểm thẩm định nhóm kết quả thực hiện nhiệm vụ phải từ 0 đến {p.TotalTaskWeight:0.##}."));

        var roundedGeneral = EvaluationScoring.Round(general, p.Rounding.GeneralTotal);
        var roundedTasks = EvaluationScoring.Round(tasks, p.Rounding.TasksTotal);
        return (roundedGeneral, roundedTasks, EvaluationScoring.TotalScore(roundedGeneral, roundedTasks, p.Rounding.Total));
    }

    /// <summary>Bắt buộc đủ mọi nhiệm vụ của hồ sơ (trừ việc đã loại trừ / chuyển tiếp), mỗi nhiệm vụ một dòng.</summary>
    private static void ApplyTaskLevels(IReadOnlyList<EvaluationTask> tasks, IReadOnlyList<AppraisalTaskLevelDto>? levels)
    {
        levels ??= Array.Empty<AppraisalTaskLevelDto>();
        var byId = tasks.ToDictionary(t => t.Id);
        var seen = new HashSet<Guid>();
        foreach (var input in levels)
        {
            if (input == null)
                continue;
            if (!byId.TryGetValue(input.TaskId, out var task))
                throw new ValidationException("Có nhiệm vụ được xếp mức không thuộc danh mục của hồ sơ. Hãy tải lại hồ sơ rồi thẩm định lại.");
            if (!seen.Add(input.TaskId))
                throw new ValidationException($"Nhiệm vụ \"{task.TaskName}\" được xếp mức nhiều lần.");
            if (input.Level is { } level && !GradeConditions.IsValidLevel(level))
                throw new ValidationException($"Mức thẩm định của nhiệm vụ \"{task.TaskName}\" chỉ là Mức 1, Mức 2 hoặc Mức 3.");
        }

        // Việc đã loại trừ / chuyển tiếp không chấm nên không bắt buộc xếp mức.
        var missing = tasks
            .Where(TaskChangeRules.IsActive)
            .Where(t => levels.FirstOrDefault(l => l != null && l.TaskId == t.Id)?.Level is null)
            .OrderBy(t => t.TaskOrder)
            .Select(t => $"\"{t.TaskName}\"")
            .ToList();
        if (missing.Count > 0)
            throw new ValidationException($"Chưa xếp mức thẩm định cho nhiệm vụ: {string.Join(", ", missing)}.");

        foreach (var input in levels.Where(l => l != null))
        {
            var task = byId[input.TaskId];
            task.AppraisalLevel = input.Level;
            task.AppraisalComment = Trim(input.Comment, MaxTaskAppraisalCommentLength, $"Nhận xét nhiệm vụ \"{task.TaskName}\"");
        }
    }

    /// <inheritdoc />
    public Task<EvaluationRecordDto> ReturnByAppraiserAsync(Guid recordId, ReturnRecordRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.ReturnByAppraiser, request.Reason, _ => Task.FromResult<string?>(null), ct,
            unlockUntil: request.UnlockUntil);

    /// <inheritdoc />
    public Task<EvaluationRecordDto> DirectorReviewAsync(Guid recordId, DirectorReviewRequestDto request, CancellationToken ct = default) =>
        ExecuteAsync(recordId, request, WorkflowActions.DirectorReview, null, ctx =>
        {
            var grade = RequireGrade(request.ProposedGrade, "Hãy chọn mức xếp loại do cấp trực tiếp sử dụng đề xuất.");
            var comment = Trim(request.Comment, MaxCommentLength, "Nhận xét");
            if (ctx.Criteria is { } criteria && GradeProposalRules.HasScore(ctx.Record))
                GradeProposalRules.EnsureExplained(criteria.Content, WorkflowStep.B3C_DIRECTOR, grade, ctx.Record.AppraisalScore ?? ctx.Record.TotalSelfScore,
                    GradeProposalRules.ExceedRatio(ctx.Record, criteria), comment, "\"Nhận xét\"");
            ctx.Record.DirectorComment = comment;
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
            var grade = RequireGrade(request.FinalGrade, "Hãy chọn mức xếp loại được quyết định.");
            if (request.FinalScore is < 0 or > 100)
                throw new ValidationException("Điểm chính thức phải từ 0 đến 100.");
            await AttachVotesAsync(ctx.Record, request.MeetingId, request.Votes, WorkflowStep.B4_DECISION, ct);

            var finalScore = request.FinalScore ?? ctx.Record.AppraisalScore ?? ctx.Record.TotalSelfScore;
            var explanation = Trim(request.Explanation, MaxCommentLength, "Căn cứ quyết định mức");
            if (ctx.Criteria is { } criteria && (request.FinalScore.HasValue || GradeProposalRules.HasScore(ctx.Record)))
                GradeProposalRules.EnsureExplained(criteria.Content, WorkflowStep.B4_DECISION, grade, finalScore,
                    GradeProposalRules.ExceedRatio(ctx.Record, criteria), explanation, "\"Căn cứ quyết định mức\"");

            ctx.Record.FinalGrade = grade;
            ctx.Record.FinalScore = finalScore;
            ctx.Record.DecisionExplanation = explanation;
            ctx.Record.DecisionDocumentNumber = Trim(request.DocumentNumber, 100, "Số văn bản");
            ctx.Record.DecisionDocumentDate = request.DocumentDate.HasValue
                ? DateTime.SpecifyKind(request.DocumentDate.Value, DateTimeKind.Utc)
                : null;
            ctx.Record.DecisionAuthorityName = Trim(request.AuthorityName, 300, "Cơ quan quyết định");
            ctx.Record.CadreWorkProposal = Trim(request.CadreWorkProposal, MaxCadreProposalLength, "Đề xuất nội dung về công tác cán bộ");
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
            // Chỉ công bố khi đã có mức được quyết định.
            if (ctx.Record.FinalGrade == EvaluationGrade.ChuaXepLoai)
                throw new ConflictException(ctx.Record.DecisionRecordedAt == null
                    ? "Hồ sơ chưa có quyết định mức xếp loại nên chưa công bố được."
                    : "Hồ sơ chưa có mức xếp loại được quyết định nên chưa công bố được. Hãy ghi nhận lại quyết định mức xếp loại.");
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
            // Giữ dữ liệu các bước sau để đối chiếu; chỉ gỡ dấu công bố.
            ctx.Record.PublishedById = null;
            ctx.Record.PublishedByName = null;
            ctx.Record.PublishedAt = null;

            // Kỳ đã đóng → về "Khóa dữ liệu" để các bước sau thao tác được.
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
        }, ct, target, unlockUntil: request.UnlockUntil);
    }

    /// <inheritdoc />
    public async Task<EvaluationRecordDto> ExtendUnlockAsync(Guid recordId, UnlockRecordRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại hồ sơ rồi thực hiện lại.");

        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");
        var profile = EvaluationMapping.ProfileOf(ReadSettings(record.Period), record);

        if (_currentUser.UserId is { } userId && record.MemberId == userId)
            throw new ForbiddenException("Bạn không được gia hạn mở riêng hồ sơ của chính mình (xung đột lợi ích).");
        if (!CanExtendUnlock(record, profile))
            _guard.Ensure(PermissionCodes.EvaluationReopen, AccessTarget.ForRecord(record));

        if (record.Version != request.Version.Value)
            throw new ConflictException("Hồ sơ đã được người khác cập nhật sau khi bạn mở. Hãy tải lại hồ sơ để xem dữ liệu mới nhất rồi thực hiện lại.");
        if (!IsExtendableUnlock(record))
            throw new ConflictException("Chỉ gia hạn được hồ sơ đang được mở riêng khi kỳ khóa dữ liệu và chưa tới bước thẩm định.");

        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            throw new ValidationException("Hãy nhập lý do gia hạn mở riêng hồ sơ.");
        if (reason.Length > MaxReasonLength)
            throw new ValidationException($"Lý do không được dài quá {MaxReasonLength} ký tự.");

        var now = DateTime.UtcNow;
        var until = ValidateUnlockUntil(request.UnlockUntil, now)
            ?? throw new ValidationException("Hãy chọn hạn mở riêng hồ sơ.");
        var actorId = _currentUser.UserId;
        var actorName = actorId.HasValue
            ? await _repo.GetMemberNameAsync(actorId.Value, ct) ?? _currentUser.UserName
            : _currentUser.UserName;

        GrantUnlock(record, until, reason, actorId, actorName, now);
        record.UpdatedAt = now;
        _unitOfWork.SetOriginalVersion(record, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await LoadDtoAsync(record, ct);
    }

    /// <inheritdoc />
    public Task<EvaluationRecordDto> RecordExternalResultAsync(Guid recordId, string step, ExternalResultRequestDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parsed = WorkflowSteps.Parse(step)
            ?? throw new ValidationException($"Mã bước \"{step}\" không tồn tại.");
        if (WorkflowSteps.NeverExternal.Contains(parsed))
            throw new ValidationException($"Bước \"{WorkflowSteps.DisplayName(parsed)}\" không do cấp trên thực hiện được.");

        return ExecuteAsync(recordId, request, WorkflowActions.ExternalOf(parsed), null, async ctx =>
        {
            var authority = Trim(request.AuthorityName, 300, "Cơ quan / cấp thực hiện")
                ?? throw new ValidationException("Hãy nhập cơ quan / cấp đã thực hiện bước này (ví dụ \"BTV Đảng ủy Tổng công ty\").");
            var documentNumber = Trim(request.DocumentNumber, 100, "Số văn bản");
            var comment = Trim(request.Comment, MaxCommentLength, "Nhận xét");
            var cadreProposal = Trim(request.CadreWorkProposal, MaxCadreProposalLength, "Đề xuất nội dung về công tác cán bộ");
            if (cadreProposal != null && parsed != WorkflowStep.B4_DECISION)
                throw new ValidationException("Đề xuất nội dung về công tác cán bộ (cột 13 Mẫu 14) chỉ ghi ở bước quyết định mức xếp loại.");
            if (request.Score is < 0 or > 100)
                throw new ValidationException("Điểm phải từ 0 đến 100.");

            var grade = EvaluationGrade.ChuaXepLoai;
            if (WorkflowSteps.GradedSteps.Contains(parsed))
            {
                grade = RequireGrade(request.Grade, parsed == WorkflowStep.B4_DECISION
                    ? "Hãy chọn mức xếp loại được cấp trên quyết định."
                    : "Hãy chọn mức xếp loại do cấp trên đề xuất.");
            }

            var record = ctx.Record;
            var result = record.ExternalResults.FirstOrDefault(x => x.Step == parsed);
            var linked = result?.AttachmentId is { } oldAttachment ? new HashSet<Guid> { oldAttachment } : new HashSet<Guid>();
            await EnsureNewAttachmentsLinkableAsync(new[] { request.AttachmentId }, linked);

            if (result == null)
            {
                result = new EvaluationExternalResult { RecordId = record.Id, Step = parsed, CreatedAt = ctx.Now };
                record.ExternalResults.Add(result);
                _repo.AddExternalResult(result);
            }
            result.AuthorityName = authority;
            result.DocumentNumber = documentNumber;
            result.DocumentDate = request.DocumentDate.HasValue ? DateTime.SpecifyKind(request.DocumentDate.Value, DateTimeKind.Utc) : null;
            result.Comment = comment;
            result.Grade = grade;
            result.Score = request.Score;
            result.AttachmentId = request.AttachmentId is { } id && id != Guid.Empty ? id : null;
            result.RecordedById = ctx.ActorId;
            result.RecordedByName = ctx.ActorName;
            result.RecordedAt = ctx.Now;
            result.UpdatedAt = ctx.Now;

            ApplyExternalResultToRecord(record, result, ctx);
            if (parsed == WorkflowStep.B4_DECISION)
                record.CadreWorkProposal = cadreProposal;
            return $"Ghi nhận kết quả của cấp trên ({authority}).";
        }, ct);
    }

    /// <summary>
    /// Chép vào các trường của bước để báo cáo, biểu mẫu, mức hiệu lực dùng chung một nguồn với bước làm trong hệ thống.
    /// </summary>
    private static void ApplyExternalResultToRecord(EvaluationRecord record, EvaluationExternalResult result, ActionContext ctx)
    {
        switch (result.Step)
        {
            case WorkflowStep.B1_APPROVE:
                record.TasksApprovedById = null;
                record.TasksApprovedByName = result.AuthorityName;
                record.TasksApprovedAt = ctx.Now;
                record.TasksApprovalComment = result.Comment;
                break;
            case WorkflowStep.B2_CELL_CONFIRM:
                record.PartyCellComment = result.Comment ?? string.Empty;
                record.CellConfirmedById = null;
                record.CellConfirmedByName = result.AuthorityName;
                record.CellConfirmedAt = ctx.Now;
                break;
            case WorkflowStep.B3A_COLLECTIVE:
                record.CollectiveProposedGrade = result.Grade;
                record.CollectiveComment = result.Comment;
                record.CollectiveMeetingId = null;
                record.CollectiveRecordedById = null;
                record.CollectiveRecordedByName = result.AuthorityName;
                record.CollectiveRecordedAt = ctx.Now;
                break;
            case WorkflowStep.B3B_APPRAISAL:
                record.AppraisalScore = result.Score;
                record.AppraisalGeneralScore = null;
                record.AppraisalTasksScore = null;
                record.AppraisalComment = result.Comment ?? string.Empty;
                record.AppraisalProposedGrade = result.Grade;
                record.AppraisedById = null;
                record.AppraisedByName = result.AuthorityName;
                record.AppraisedAt = ctx.Now;
                break;
            case WorkflowStep.B3C_DIRECTOR:
                record.DirectorComment = result.Comment;
                record.DirectorProposedGrade = result.Grade;
                record.DirectorReviewedById = null;
                record.DirectorReviewedByName = result.AuthorityName;
                record.DirectorReviewedAt = ctx.Now;
                break;
            case WorkflowStep.B4_DECISION:
                record.FinalGrade = result.Grade;
                record.FinalScore = result.Score ?? record.AppraisalScore ?? record.TotalSelfScore;
                record.DecisionDocumentNumber = result.DocumentNumber;
                record.DecisionDocumentDate = result.DocumentDate;
                record.DecisionAuthorityName = result.AuthorityName;
                record.DecisionMeetingId = null;
                record.DecisionRecordedById = ctx.ActorId;
                record.DecisionRecordedByName = ctx.ActorName;
                record.DecisionRecordedAt = ctx.Now;
                break;
        }
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
        var queue = await GetStepQueueAsync(periodId, ct);
        // Nhóm việc sau công bố, kể cả với kỳ đã đóng.
        if (_postPublish != null)
        {
            var extra = await _postPublish.GetGroupsAsync(periodId, ct);
            queue.Groups.AddRange(extra);
            queue.Total += extra.Sum(g => g.Count);
        }
        if (_taskChanges != null)
        {
            var changes = await _taskChanges.GetGroupsAsync(periodId, ct);
            queue.Groups.AddRange(changes);
            queue.Total += changes.Sum(g => g.Count);
        }
        return queue;
    }

    private async Task<WorkQueueDto> GetStepQueueAsync(Guid? periodId, CancellationToken ct)
    {
        var periods = await _repo.ListPeriodsAsync(ct);
        var targetPeriods = periodId.HasValue && periodId.Value != Guid.Empty
            ? periods.Where(p => p.Id == periodId.Value).ToList()
            : periods.Where(p => p.Status is PeriodStatus.Open or PeriodStatus.Locked).ToList();
        if (targetPeriods.Count == 0)
            return new WorkQueueDto();

        var settingsByPeriod = targetPeriods.ToDictionary(p => p.Id, ReadSettings);

        // Lọc sơ bộ theo quyền có ở phạm vi nào đó; sau đó kiểm tra chính xác từng hồ sơ.
        var steps = settingsByPeriod.Values
            .SelectMany(s => s.Profiles)
            .SelectMany(profile => WorkflowSteps.Ordered
                .Select(step => (step, action: WorkflowActions.AdvanceOf(step, profile), profile)))
            .Where(x => x.action != null)
            .Where(x => WorkflowActions.PermissionFor(x.action!, x.profile) is var code
                && (code == PermissionCodes.EvaluationSelf || _guard.HasAny(code)))
            .Select(x => x.step)
            .Distinct()
            .ToList();
        if (steps.Count == 0)
            return new WorkQueueDto();

        var statuses = steps.Select(WorkflowSteps.StatusOf).ToList();
        var records = await _repo.ListRecordsByStatusAsync(targetPeriods.Select(p => p.Id).ToList(), statuses, ct);
        var periodById = targetPeriods.ToDictionary(p => p.Id);
        var now = DateTime.UtcNow;
        var today = EvaluationMapping.Today(now);
        var userId = _currentUser.UserId;

        var items = new List<(WorkflowStep Step, WorkQueueItemDto Item)>();
        foreach (var record in records)
        {
            var step = WorkflowSteps.StepOf(record.Status);
            if (step == null)
                continue;
            var profile = EvaluationMapping.ProfileOf(settingsByPeriod[record.PeriodId], record);
            var advance = WorkflowActions.AdvanceOf(step.Value, profile);
            if (advance == null)
                continue;

            var period = periodById[record.PeriodId];
            if (WorkflowActions.PeriodBlockReason(period.Status, advance, record.UnlockedUntil, now) != null)
                continue;
            if (!CanPerform(advance, profile, record))
                continue;

            var deadline = profile.Deadline(step.Value);
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
                WorkflowProfileName = profile.Name,
                Status = record.Status.ToString(),
                StatusDisplayName = WorkflowSteps.StatusDisplayName(record.Status),
                Mode = profile.Mode(step.Value).ToString(),
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

    private bool CanPerform(WorkflowActionDefinition action, WorkflowProfile profile, EvaluationRecord record) =>
        (action.Kind != WorkflowAction.RevokeTaskApproval || CanRevokeTaskApproval(record, profile))
        && (action.Kind != WorkflowAction.RevokeTaskApproval || record.TasksApprovedById == _currentUser.UserId)
        && !IsConflictOfInterest(action, record)
        && _guard.Can(WorkflowActions.PermissionFor(action, profile), AccessTarget.ForRecord(record));

    private static bool CanRevokeTaskApproval(EvaluationRecord record, WorkflowProfile profile) =>
        profile.Mode(WorkflowStep.B1_APPROVE) == StepMode.Internal
        && record.Status == RecordStatus.AwaitingSelfScore
        && record.TasksApprovedById.HasValue
        && record.TasksApprovedAt.HasValue
        && !record.SelfScoredAt.HasValue;

    /// <summary>
    /// Mọi bước không phải của chủ hồ sơ không áp dụng trên hồ sơ của chính mình, kể cả khi quyền bước cấu hình bằng mã khác mặc định.
    /// </summary>
    private bool IsConflictOfInterest(WorkflowActionDefinition action, EvaluationRecord record) =>
        (action.Kind == WorkflowAction.Reopen || !WorkflowSteps.OwnerSteps.Contains(action.Step))
        && _currentUser.UserId is { } userId && record.MemberId == userId;

    private IEnumerable<RecordActionDto> AvailableActions(EvaluationRecord record, PeriodSettings settings, DateOnly today)
    {
        var profile = EvaluationMapping.ProfileOf(settings, record);
        var active = profile.ActiveSteps();
        var candidates = new List<WorkflowActionDefinition>();

        if (record.Status == RecordStatus.Published)
        {
            candidates.Add(WorkflowActions.Get(WorkflowActions.Reopen));
        }
        else if (WorkflowSteps.StepOf(record.Status) is { } step && WorkflowActions.AdvanceOf(step, profile) is { } advance)
        {
            candidates.Add(advance);
            if (profile.Mode(step) == StepMode.Internal && WorkflowActions.ReturnOf(step) is { } returnAction)
                candidates.Add(returnAction);
        }

        if (CanRevokeTaskApproval(record, profile))
            candidates.Add(WorkflowActions.Get(WorkflowActions.RevokeTaskApproval));

        var now = DateTime.UtcNow;
        foreach (var action in candidates)
        {
            if (WorkflowActions.PeriodBlockReason(record.Period.Status, action, record.UnlockedUntil, now) != null)
                continue;
            if (!CanPerform(action, profile, record))
                continue;

            var command = action.Kind switch
            {
                WorkflowAction.RevokeTaskApproval => null,
                WorkflowAction.Return => WorkflowCommand.Return(action.Step),
                WorkflowAction.Reopen => null,
                _ => WorkflowCommand.Complete(action.Step)
            };
            if (command != null && !RecordStateMachine.Apply(record.Status, active, command).Succeeded)
                continue;

            var deadline = action.Kind == WorkflowAction.RevokeTaskApproval ? null : profile.Deadline(action.Step);
            var overdue = action.Advances && deadline.HasValue && today > deadline.Value;
            if (overdue && settings.EnforceDeadlines)
                continue;

            var targets = action.Kind == WorkflowAction.Reopen ? RecordStateMachine.ReopenTargets(active) : null;
            var unlockSteps = action.Kind switch
            {
                WorkflowAction.Reopen => targets!.Where(t => UnlockDeadlineNeeded(record.Period.Status, action, t)).ToList(),
                WorkflowAction.Return when UnlockDeadlineNeeded(record.Period.Status, action, null) => BackwardTarget(action, null) is { } returnTarget
                    ? new List<WorkflowStep> { returnTarget }
                    : new List<WorkflowStep>(),
                _ => new List<WorkflowStep>()
            };
            var requiresUnlock = unlockSteps.Count > 0;

            yield return new RecordActionDto
            {
                Action = action.Code,
                Step = WorkflowSteps.Code(action.Step),
                Label = action.Label,
                RequiresReason = action.Kind is WorkflowAction.Return or WorkflowAction.Reopen,
                ReasonOptional = false,
                Overdue = overdue,
                TargetSteps = targets?.Select(WorkflowSteps.Code).ToList(),
                RequiresUnlockDeadline = requiresUnlock,
                UnlockDeadlineSteps = requiresUnlock && action.Kind == WorkflowAction.Reopen ? unlockSteps.Select(WorkflowSteps.Code).ToList() : null,
                CurrentUnlockedUntil = requiresUnlock && record.IsUnlockedAt(now) ? record.UnlockedUntil : null,
                DefaultUnlockDays = requiresUnlock ? WorkflowActions.DefaultUnlockDays : null,
                MaxUnlockDays = requiresUnlock ? WorkflowActions.MaxUnlockDays : null
            };
        }

        if (IsExtendableUnlock(record) && CanExtendUnlock(record, profile))
        {
            yield return new RecordActionDto
            {
                Action = WorkflowActions.ExtendUnlock,
                Step = WorkflowSteps.Code(WorkflowSteps.StepOf(record.Status)!.Value),
                Label = "Gia hạn mở riêng hồ sơ",
                RequiresReason = true,
                ReasonOptional = false,
                RequiresUnlockDeadline = true,
                CurrentUnlockedUntil = record.UnlockedUntil,
                DefaultUnlockDays = WorkflowActions.DefaultUnlockDays,
                MaxUnlockDays = WorkflowActions.MaxUnlockDays
            };
        }
    }

    #endregion

    #region Khung thực thi chung

    private sealed record ActionContext(
        EvaluationRecord Record, PeriodSettings Settings, WorkflowProfile Profile, Guid? ActorId, string ActorName, DateTime Now,
        CriteriaSnapshot? Criteria);

    private Task<EvaluationRecordDto> ExecuteAsync(
        Guid recordId,
        WorkflowRequestDto request,
        string actionCode,
        string? reason,
        Func<ActionContext, Task<string?>> apply,
        CancellationToken ct,
        WorkflowStep? reopenTarget = null,
        bool draft = false,
        DateTime? unlockUntil = null) =>
        ExecuteAsync(recordId, request, WorkflowActions.Get(actionCode), reason, apply, ct, reopenTarget, draft, unlockUntil);

    /// <summary>
    /// Thứ tự kiểm tra: phiên bản → nạp → chế độ bước → xung đột lợi ích → guard → so phiên bản → luật kỳ + hạn → lý do → máy trạng thái.
    /// <paramref name="draft"/>: kiểm như hành động nhưng không chuyển trạng thái, không ghi lịch sử, không chặn theo hạn.
    /// </summary>
    private async Task<EvaluationRecordDto> ExecuteAsync(
        Guid recordId,
        WorkflowRequestDto request,
        WorkflowActionDefinition action,
        string? reason,
        Func<ActionContext, Task<string?>> apply,
        CancellationToken ct,
        WorkflowStep? reopenTarget = null,
        bool draft = false,
        DateTime? unlockUntil = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Mọi hành động ghi phải gửi phiên bản đã đọc.
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại hồ sơ rồi thực hiện lại.");

        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

        var period = record.Period;
        var settings = ReadSettings(period);
        var profile = EvaluationMapping.ProfileOf(settings, record);

        var modeBlock = WorkflowActions.ModeBlockReason(action, profile);
        if (modeBlock != null)
            throw new ConflictException(modeBlock);

        var permission = WorkflowActions.PermissionFor(action, profile);
        if (IsConflictOfInterest(action, record))
        {
            throw new ForbiddenException(
                $"Bạn không được thực hiện \"{PermissionCodes.DisplayName(permission)}\" trên hồ sơ của chính mình (xung đột lợi ích).");
        }
        _guard.Ensure(permission, AccessTarget.ForRecord(record));

        if (record.Version != request.Version.Value)
            throw new ConflictException("Hồ sơ đã được người khác cập nhật sau khi bạn mở. Hãy tải lại hồ sơ để xem dữ liệu mới nhất rồi thực hiện lại.");

        var now = DateTime.UtcNow;
        var periodBlock = WorkflowActions.PeriodBlockReason(period.Status, action, record.UnlockedUntil, now);
        if (periodBlock != null)
            throw new ConflictException(periodBlock);
        if (!draft && action.Advances && settings.EnforceDeadlines
            && profile.Deadline(action.Step) is { } deadline && EvaluationMapping.Today(now) > deadline)
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
        var transition = RecordStateMachine.Apply(record.Status, profile.ActiveSteps(), command);
        if (!transition.Succeeded)
            throw new ConflictException(transition.Error!);

        // Kỳ khóa mà hồ sơ quay về trước thẩm định: chỉ mở riêng hồ sơ tới hạn được chọn, không mở lại cả kỳ.
        // Không chọn hạn mới khi đang mở riêng còn hạn → giữ hạn cũ.
        DateTime? grantUntil = null;
        if (!draft && UnlockDeadlineNeeded(period.Status, action, reopenTarget))
        {
            grantUntil = ValidateUnlockUntil(unlockUntil, now);
            if (grantUntil == null && !record.IsUnlockedAt(now))
                throw new ValidationException("Kỳ đánh giá đang khóa dữ liệu: hãy chọn hạn mở riêng hồ sơ để các bước trước thẩm định thao tác được.");
        }

        var actorId = _currentUser.UserId;
        var actorName = actorId.HasValue
            ? await _repo.GetMemberNameAsync(actorId.Value, ct) ?? _currentUser.UserName
            : _currentUser.UserName;
        var scoreBefore = record.EffectiveScore();
        var gradeBefore = record.EffectiveGrade();
        var fromStatus = record.Status;

        var comment = await apply(new ActionContext(record, settings, profile, actorId, actorName, now, ReadCriteria(period)));

        record.UpdatedAt = now;
        if (draft)
        {
            _unitOfWork.SetOriginalVersion(record, request.Version);
            await _unitOfWork.SaveChangesAsync(ct);
            return await LoadDtoAsync(record, ct);
        }

        if (action.Kind == WorkflowAction.Return)
            record.ReturnReason = cleanReason;
        record.Status = transition.Status;

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

        if (grantUntil is { } until)
            GrantUnlock(record, until, cleanReason, actorId, actorName, now.AddMilliseconds(1));
        else if (record.UnlockedUntil.HasValue
            && (WorkflowSteps.StepOf(record.Status) is not { } waiting || !WorkflowSteps.IsLockedStep(waiting)))
            ClearUnlock(record, actorId, actorName, now.AddMilliseconds(1));

        _unitOfWork.SetOriginalVersion(record, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await LoadDtoAsync(record, ct);
    }

    /// <summary>Null với hành động không phải trả lại / mở lại.</summary>
    private static WorkflowStep? BackwardTarget(WorkflowActionDefinition action, WorkflowStep? reopenTarget) => action.Kind switch
    {
        WorkflowAction.Return => WorkflowSteps.ReturnTargets.TryGetValue(action.Step, out var target) ? target : null,
        WorkflowAction.Reopen => reopenTarget ?? WorkflowSteps.EarliestReopenStep,
        _ => null
    };

    /// <summary>Kỳ khóa dữ liệu (kể cả mở lại khi kỳ đã đóng) và bước quay về nằm trước bước đầu tiên được thao tác khi kỳ khóa.</summary>
    private static bool UnlockDeadlineNeeded(PeriodStatus status, WorkflowActionDefinition action, WorkflowStep? reopenTarget)
    {
        var lockedAfter = status == PeriodStatus.Locked || (status == PeriodStatus.Closed && action.Kind == WorkflowAction.Reopen);
        return lockedAfter && BackwardTarget(action, reopenTarget) is { } target && WorkflowSteps.IsLockedStep(target);
    }

    /// <summary>Sau hiện tại, không quá <see cref="WorkflowActions.MaxUnlockDays"/> ngày.</summary>
    private static DateTime? ValidateUnlockUntil(DateTime? value, DateTime now)
    {
        if (value is not { } raw)
            return null;
        var until = raw.Kind switch
        {
            DateTimeKind.Utc => raw,
            DateTimeKind.Local => raw.ToUniversalTime(),
            _ => DateTime.SpecifyKind(raw, DateTimeKind.Utc)
        };
        if (until <= now)
            throw new ValidationException("Hạn mở riêng hồ sơ phải sau thời điểm hiện tại.");
        if (until > now.AddDays(WorkflowActions.MaxUnlockDays))
            throw new ValidationException($"Hạn mở riêng hồ sơ không được quá {WorkflowActions.MaxUnlockDays} ngày kể từ hôm nay.");
        return until;
    }

    private void GrantUnlock(EvaluationRecord record, DateTime until, string? reason, Guid? actorId, string actorName, DateTime at)
    {
        var extended = record.UnlockedUntil.HasValue;
        record.UnlockedUntil = until;
        record.UnlockedAt = at;
        record.UnlockedById = actorId;
        record.UnlockedByName = Truncate(actorName, 200);
        record.UnlockReason = reason;
        _repo.AddHistory(new EvaluationRecordHistory
        {
            RecordId = record.Id,
            FromStatus = record.Status,
            ToStatus = record.Status,
            Step = WorkflowSteps.StepOf(record.Status),
            Action = WorkflowAction.GrantUnlock,
            Reason = reason,
            ScoreBefore = record.EffectiveScore(),
            ScoreAfter = record.EffectiveScore(),
            GradeBefore = record.EffectiveGrade(),
            GradeAfter = record.EffectiveGrade(),
            ActorId = actorId,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            Comment = $"{(extended ? "Gia hạn mở riêng" : "Mở riêng")} hồ sơ khi kỳ khóa dữ liệu đến {WorkflowActions.FormatLocal(until)}.",
            CreatedAt = at
        });
    }

    /// <summary>Hồ sơ đã tới bước được thao tác khi kỳ khóa.</summary>
    private void ClearUnlock(EvaluationRecord record, Guid? actorId, string actorName, DateTime at)
    {
        record.UnlockedUntil = null;
        record.UnlockedAt = null;
        record.UnlockedById = null;
        record.UnlockedByName = null;
        record.UnlockReason = null;
        _repo.AddHistory(new EvaluationRecordHistory
        {
            RecordId = record.Id,
            FromStatus = record.Status,
            ToStatus = record.Status,
            Step = WorkflowSteps.StepOf(record.Status),
            Action = WorkflowAction.ClearUnlock,
            ScoreBefore = record.EffectiveScore(),
            ScoreAfter = record.EffectiveScore(),
            GradeBefore = record.EffectiveGrade(),
            GradeAfter = record.EffectiveGrade(),
            ActorId = actorId,
            ActorName = string.IsNullOrWhiteSpace(actorName) ? "system" : actorName,
            Comment = "Hồ sơ đã tới bước được thao tác khi kỳ khóa dữ liệu; kết thúc mở riêng.",
            CreatedAt = at
        });
    }

    /// <summary>Đã mở riêng (còn hoặc hết hạn), kỳ đang khóa dữ liệu, chưa tới bước thẩm định.</summary>
    private static bool IsExtendableUnlock(EvaluationRecord record) =>
        record.UnlockedUntil.HasValue
        && record.Period.Status == PeriodStatus.Locked
        && WorkflowSteps.StepOf(record.Status) is { } step && WorkflowSteps.IsLockedStep(step);

    /// <summary><c>evaluation.reopen</c> hoặc quyền bước thẩm định trên hồ sơ; không phải hồ sơ của chính mình.</summary>
    private bool CanExtendUnlock(EvaluationRecord record, WorkflowProfile profile)
    {
        if (_currentUser.UserId is { } userId && record.MemberId == userId)
            return false;
        var target = AccessTarget.ForRecord(record);
        if (_guard.Can(PermissionCodes.EvaluationReopen, target))
            return true;
        return profile.Mode(WorkflowSteps.FirstStepWhenLocked) == StepMode.Internal
            && _guard.Can(WorkflowActions.PermissionFor(WorkflowActions.CompleteOf(WorkflowSteps.FirstStepWhenLocked), profile), target);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length > maxLength ? value[..maxLength] : value;

    private async Task<EvaluationRecordDto> LoadDtoAsync(EvaluationRecord record, CancellationToken ct)
    {
        var saved = await _repo.FindRecordAsync(record.Id, ct) ?? record;
        var evidence = await _repo.GetCurrentEvidenceAsync(saved.Tasks.ToList(), ct);
        return EvaluationMapping.ToDto(saved, ReadSettings(saved.Period), EvaluationMapping.Today(DateTime.UtcNow), evidence);
    }

    /// <summary>
    /// Chỉ lưu tổng hợp: biên bản cùng kỳ, đúng bước, hồ sơ thuộc đơn vị của biên bản; tổng phiếu ≤ số có mặt, không âm.
    /// Trả kết quả kiểm phiếu của hồ sơ trong biên bản (vừa ghi hoặc đã có) kèm số người có mặt; null nếu không có.
    /// </summary>
    private async Task<(VoteTally Tally, int PresentCount)?> AttachVotesAsync(EvaluationRecord record, Guid? meetingId, VoteTallyDto? votes,
        WorkflowStep stage, CancellationToken ct)
    {
        if (meetingId is null)
        {
            if (votes != null)
                throw new ValidationException("Kết quả kiểm phiếu phải gắn với một biên bản hội nghị (Mẫu 12/13). Hãy chọn biên bản.");
            return null;
        }

        var meeting = await _repo.FindMeetingAsync(meetingId.Value, ct)
            ?? throw new ValidationException("Không tìm thấy biên bản hội nghị đã chọn.");
        if (meeting.PeriodId != record.PeriodId)
            throw new ValidationException("Biên bản thuộc kỳ đánh giá khác với hồ sơ.");
        if (meeting.Stage.HasValue && meeting.Stage.Value != stage)
            throw new ValidationException($"Biên bản đã chọn không dùng cho bước \"{WorkflowSteps.DisplayName(stage)}\".");
        // Biên bản ở tổ chức cấp trên nhận hồ sơ của cây con; ngoài cây con thì từ chối.
        if (!await OrgSubtreeRules.RecordWithinMeetingAsync(
                _repo, meeting.DepartmentId, meeting.PartyCellId, record.DepartmentId, record.PartyCellId, ct))
            throw new ValidationException("Hồ sơ không thuộc đơn vị/tổ chức Đảng của biên bản đã chọn hoặc đơn vị, tổ chức cấp dưới của nó.");

        if (votes == null)
        {
            var existing = meeting.VoteSummaries.FirstOrDefault(v => v.RecordId == record.Id);
            return existing == null ? null : (TallyOf(existing), meeting.PresentCount);
        }

        var counts = new[] { votes.VotesExcellent, votes.VotesGood, votes.VotesSatisfactory, votes.VotesUnsatisfactory, votes.VotesNotRated, votes.InvalidVotes };
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
        summary.VotesNotRated = votes.VotesNotRated;
        summary.InvalidVotes = votes.InvalidVotes;
        summary.Notes = votes.Notes?.Trim() ?? string.Empty;
        meeting.UpdatedAt = DateTime.UtcNow;
        return (TallyOf(summary), meeting.PresentCount);
    }

    private static VoteTally TallyOf(EvaluationMeetingVoteSummary s) =>
        new(s.VotesExcellent, s.VotesGood, s.VotesSatisfactory, s.VotesUnsatisfactory, s.VotesNotRated, s.InvalidVotes);

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

    private static CriteriaSnapshot? ReadCriteria(EvaluationPeriod period)
    {
        try
        {
            return period.GetCriteria();
        }
        catch (FormatException)
        {
            throw new ConflictException("Ảnh chụp bộ tiêu chí của kỳ bị lỗi định dạng. Hãy liên hệ người quản lý kỳ.");
        }
    }

    /// <summary>Bắt buộc cho các bước có chấm điểm.</summary>
    private static CriteriaSnapshot RequireCriteria(ActionContext ctx) =>
        ctx.Criteria ?? throw new ConflictException("Kỳ đánh giá chưa có bộ tiêu chí nên chưa chấm điểm được. Hãy liên hệ người quản lý kỳ để chọn bộ tiêu chí.");

    /// <summary>Không nhận "Chưa xếp loại".</summary>
    private static EvaluationGrade RequireGrade(string? value, string message) =>
        EvaluationMapping.ParseGrade(value) is { } grade && grade != EvaluationGrade.ChuaXepLoai
            ? grade
            : throw new ValidationException(message);

    private static string? Trim(string? value, int maxLength, string field)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            throw new ValidationException($"{field} không được dài quá {maxLength} ký tự.");
        return trimmed;
    }

    /// <summary>
    /// Đủ các cột bắt buộc của Mẫu 01; loại việc khi đăng ký chỉ trọng tâm / thường xuyên; trục thuộc bộ tiêu chí của kỳ.
    /// Mã SP, thời hạn không bắt buộc (trống = ngày cuối kỳ).
    /// </summary>
    private static void ValidateTaskRows(IReadOnlyList<TaskInputDto> tasks, CriteriaSetContent criteria)
    {
        for (var i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            var row = $"Dòng {i + 1}";
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(t.TaskName)) missing.Add("tên sản phẩm");
            if (string.IsNullOrWhiteSpace(t.Kind)) missing.Add("loại việc");
            if (string.IsNullOrWhiteSpace(t.Role)) missing.Add("vai trò");
            if (criteria.Axes.Count > 0 && string.IsNullOrWhiteSpace(t.AxisCode)) missing.Add("trục kết quả");
            if (string.IsNullOrWhiteSpace(t.TargetOutput)) missing.Add("chuẩn đạt");
            if (string.IsNullOrWhiteSpace(t.ExceedCriteria)) missing.Add("tiêu chí vượt chuẩn");
            if (string.IsNullOrWhiteSpace(t.EvidenceSource)) missing.Add("nguồn minh chứng");
            if (string.IsNullOrWhiteSpace(t.Confirmer)) missing.Add("thẩm quyền xác nhận");
            if (missing.Count > 0)
                throw new ValidationException($"{row}: chưa nhập {string.Join(", ", missing)}.");

            if (ParseTaskKind(t.Kind) is not (TaskKind.TrongTam or TaskKind.ThuongXuyen))
                throw new ValidationException($"{row}: loại việc khi đăng ký là \"Trọng tâm\" hoặc \"Thường xuyên\" (việc đột xuất ghi nhận trong kỳ).");
            if (ParseTaskRole(t.Role) == null)
                throw new ValidationException($"{row}: vai trò là \"Chủ trì\" hoặc \"Phối hợp\".");
            if (!string.IsNullOrWhiteSpace(t.AxisCode) && criteria.FindAxis(t.AxisCode) == null)
                throw new ValidationException($"{row}: trục kết quả \"{t.AxisCode.Trim()}\" không có trong bộ tiêu chí của kỳ "
                    + $"(có: {string.Join(", ", criteria.Axes.Select(a => a.Code))}).");

            Trim(t.TaskName, 500, $"{row}: tên sản phẩm");
            Trim(t.ProductCode, 50, $"{row}: mã sản phẩm");
            Trim(t.TargetOutput, 2000, $"{row}: chuẩn đạt");
            Trim(t.ExceedCriteria, 2000, $"{row}: tiêu chí vượt chuẩn");
            Trim(t.EvidenceSource, 1000, $"{row}: nguồn minh chứng");
            Trim(t.Confirmer, 500, $"{row}: thẩm quyền xác nhận");
        }
    }

    private static TaskKind? ParseTaskKind(string? value) =>
        Enum.TryParse<TaskKind>(value?.Trim(), true, out var kind) && Enum.IsDefined(kind) ? kind : null;

    private static TaskRole? ParseTaskRole(string? value) =>
        Enum.TryParse<TaskRole>(value?.Trim(), true, out var role) && Enum.IsDefined(role) ? role : null;

    #endregion
}
