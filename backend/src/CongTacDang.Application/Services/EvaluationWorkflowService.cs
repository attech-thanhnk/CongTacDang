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

    /// <summary>
    /// Lưu nháp phiếu tự chấm: cùng điều kiện quyền/bước/khóa kỳ/phiên bản như nộp phiếu nhưng không chuyển trạng thái, không ghi
    /// thời điểm nộp và không ghi lịch sử hồ sơ; chỉ kiểm định dạng, không bắt buộc nhập đủ.
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
    /// Ghi nhận kết quả của bước do cấp trên thực hiện (chế độ "Cấp trên thực hiện" trong hồ sơ luồng của hồ sơ) —
    /// quyền <c>evaluation.external.record</c>; hồ sơ chuyển sang bước áp dụng kế tiếp.
    /// </summary>
    Task<EvaluationRecordDto> RecordExternalResultAsync(Guid recordId, string step, ExternalResultRequestDto request, CancellationToken ct = default);

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
    private const int MaxCadreProposalLength = 2000;
    private const int MaxTaskAppraisalCommentLength = 1000;

    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttachmentService _attachments;
    private readonly IPostPublishWorkQueue? _postPublish;

    public EvaluationWorkflowService(
        IEvaluationWorkflowRepository repo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IAttachmentService attachments,
        IPostPublishWorkQueue? postPublish = null)
    {
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
    /// Áp nội dung phiếu tự chấm vào hồ sơ: điểm tiêu chí chung, điểm theo trục (09B) hoặc A-B-C-D từng nhiệm vụ (09A), tổng
    /// điểm, mức tự đề xuất và biểu mẫu cá nhân nhập kèm. <paramref name="draft"/> = lưu nháp: chỉ kiểm định dạng (không bắt buộc
    /// đủ tiêu chí/trục, lý do, mục bắt buộc 09C); điểm tổng tính trên phần đã nhập (thiếu = 0); mức để trống thì giữ "chưa xếp
    /// loại" thay vì gợi ý. Trả danh sách biểu mẫu có nội dung thay đổi (ghi lịch sử khi nộp).
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
        double? exceedRatio = null;

        if (criteria.UsesAxisScoring)
        {
            var axisScores = request.AxisScores ?? new Dictionary<string, double>();
            var axisError = EvaluationScoring.ValidateAxisScores(content, axisScores, partial: draft);
            if (axisError != null)
                throw new ValidationException(axisError);
            record.AxisScores = EvaluationScoring.AxisScoresToJson(content, axisScores);
            record.TasksScore = EvaluationScoring.AxisTasksScore(content, axisScores);
            record.SelfScoreForm = CriteriaSetContent.Form09B;
        }
        else
        {
            // Lưu nháp khi hồ sơ chưa có khung / chưa có danh mục: giữ nguyên điểm nhiệm vụ, vẫn lưu các phần còn lại.
            var frame = content.FindFrame(record.WeightFrameCode);
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
                // T-47: tệp minh chứng mới gắn vào nhiệm vụ phải tồn tại và thuộc quyền người gửi.
                var changed = tasks
                    .Select(task => (task, input: request.TaskScores?.FirstOrDefault(s => s.TaskId == task.Id)))
                    .Where(x => x.input != null && x.input.AttachmentId != x.task.AttachmentId)
                    .Select(x => x.input!.AttachmentId);
                await EnsureNewAttachmentsLinkableAsync(changed, new HashSet<Guid>());

                foreach (var task in tasks)
                {
                    var input = request.TaskScores?.FirstOrDefault(s => s.TaskId == task.Id);
                    if (input == null)
                        continue;
                    var scored = EvaluationScoring.ScoreTask(task.Weight, input.CriteriaA_Ratio, input.CriteriaB_Ratio,
                        input.CriteriaC_Ratio, input.CriteriaD_Ratio, frame, rounding.TaskScore);
                    task.CriteriaA_Ratio = scored.A;
                    task.CriteriaB_Ratio = scored.B;
                    task.CriteriaC_Ratio = scored.C;
                    task.CriteriaD_Ratio = scored.D;
                    task.IsExceedStandard = input.IsExceedStandard;
                    task.AttachmentId = input.AttachmentId;
                    task.SelfScore = scored.Score;
                }
            }

            record.TasksScore = EvaluationScoring.TasksScore(tasks.Select(t => t.SelfScore), rounding.TasksTotal);
            record.AxisScores = null;
            record.SelfScoreForm = CriteriaSetContent.Form09A;
            exceedRatio = EvaluationScoring.ExceedStandardRatio(tasks.Count(t => t.IsExceedStandard), tasks.Count);
        }

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

        return ApplyIndividualForms(record, criteria, request, partial: draft);
    }

    /// <summary>
    /// Lưu nội dung biểu mẫu cá nhân nhập cùng phiếu tự chấm (khi kỳ áp dụng theo bộ tiêu chí): nhiệm vụ và kết quả theo trục
    /// (nhiệm vụ kèm mức cho mục I Mẫu 09C, phần tóm tắt trục cho Mẫu 9D), nhận xét chung Mẫu 09C, phần tự luận theo trục của
    /// Mẫu 09B. Trường null trong yêu cầu = giữ nội dung đã lưu. Trả danh sách biểu mẫu có nội dung thay đổi (ghi lịch sử).
    /// <paramref name="partial"/> = lưu nháp: không bắt buộc mục 09C, bỏ qua dòng nhiệm vụ/ý con chưa có nội dung.
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
            // Mục I in kèm các nhiệm vụ theo trục: giới hạn ký tự và mục bắt buộc tính trên nội dung sẽ in (gửi lên hoặc đang lưu).
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

        if (request.AxisNotes != null && criteria.UsesAxisScoring)
        {
            var error = RecordFormContent.ValidateAxisNotes(content, request.AxisNotes);
            if (error != null)
                throw new ValidationException(error);
            var json = RecordFormContent.AxisNotesToJson(content, request.AxisNotes);
            if (json != record.AxisNotes)
                changed.Add("nội dung theo trục Mẫu 09B");
            record.AxisNotes = json;
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

    /// <summary>Tách nội dung theo trục thành phần nhiệm vụ (Mẫu 09C) và phần tóm tắt (Mẫu 9D) để ghi lịch sử phần nào thay đổi.</summary>
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
        ExecuteAsync(recordId, request, WorkflowActions.Appraise, null, async ctx =>
        {
            var grade = EvaluationMapping.ParseGrade(request.ProposedGrade)
                ?? throw new ValidationException("Hãy chọn mức xếp loại do cơ quan thẩm định đề xuất.");
            var criteria = RequireCriteria(ctx);
            var (general, tasksScore, total) = AppraisalScores(request, criteria.Content);

            // B-09: chênh lệch tự chấm – thẩm định (theo tổng) từ ngưỡng của bộ tiêu chí (hoặc làm đổi mức) → bắt buộc giải trình/căn cứ.
            var explanation = Trim(request.Explanation, MaxCommentLength, "Nội dung giải trình");
            if (explanation == null && EvaluationScoring.RequiresExplanation(criteria.Content, ctx.Record.TotalSelfScore, total))
            {
                var p = criteria.Content.Parameters;
                var diff = Math.Abs(ctx.Record.TotalSelfScore - total!.Value);
                var gradeChange = p.ExplanationOnGradeChange ? " hoặc làm đổi mức xếp loại" : string.Empty;
                throw new ValidationException(string.Create(CultureInfo.InvariantCulture,
                    $"Điểm thẩm định {total:0.##} chênh lệch {diff:0.##} điểm so với điểm tự chấm {ctx.Record.TotalSelfScore:0.##} (ngưỡng {p.ExplanationThreshold:0.##} điểm{gradeChange}): hãy nhập nội dung giải trình, căn cứ."));
            }

            // Bộ chấm theo danh mục nhiệm vụ (09A): bắt buộc xếp mức 1/2/3 cho mọi nhiệm vụ; bộ chấm theo trục (09B) bỏ qua.
            if (!criteria.UsesAxisScoring)
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
    /// Điểm thẩm định: theo nhóm (tiêu chí chung + kết quả thực hiện nhiệm vụ, mỗi nhóm 0..điểm tối đa nhóm của bộ, làm tròn theo
    /// quy tắc của nhóm), tổng = chung + nhiệm vụ làm tròn theo quy tắc "tổng điểm". Không gửi điểm nhóm → dùng tổng
    /// <see cref="AppraisalRequestDto.AppraisalScore"/> (client cũ; không có điểm nhóm). Không có điểm nào → cả ba null.
    /// </summary>
    private static (double? General, double? Tasks, double? Total) AppraisalScores(AppraisalRequestDto request, CriteriaSetContent criteria)
    {
        var p = criteria.Parameters;
        if (request.GeneralScore is null && request.TasksScore is null)
        {
            if (request.AppraisalScore is < 0 or > 100)
                throw new ValidationException("Điểm thẩm định phải từ 0 đến 100.");
            return (null, null, request.AppraisalScore);
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

    /// <summary>
    /// Ghi mức thẩm định (1/2/3) và nhận xét ngắn cho từng nhiệm vụ của hồ sơ: bắt buộc đủ mọi nhiệm vụ, không nhận nhiệm vụ
    /// của hồ sơ khác, mỗi nhiệm vụ một dòng.
    /// </summary>
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

        var missing = tasks
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
                grade = EvaluationMapping.ParseGrade(request.Grade) is { } g && g != EvaluationGrade.ChuaXepLoai
                    ? g
                    : throw new ValidationException(parsed == WorkflowStep.B4_DECISION
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
    /// Chép kết quả của cấp trên vào các trường của bước trên hồ sơ để báo cáo, biểu mẫu, mức hiệu lực dùng chung
    /// một nguồn với bước làm trong hệ thống. Người/đơn vị thực hiện = cơ quan cấp trên; người ghi nhận lưu ở bản ghi kết quả.
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
        // Task 20: nhóm việc sau công bố (kiến nghị chờ xử lý, kế hoạch 30-60-90 ngày) — cả với kỳ đã đóng.
        if (_postPublish != null)
        {
            var extra = await _postPublish.GetGroupsAsync(periodId, ct);
            queue.Groups.AddRange(extra);
            queue.Total += extra.Sum(g => g.Count);
        }
        return queue;
    }

    /// <summary>Hồ sơ đang chờ người dùng hiện tại ở các bước của luồng đánh giá.</summary>
    private async Task<WorkQueueDto> GetStepQueueAsync(Guid? periodId, CancellationToken ct)
    {
        var periods = await _repo.ListPeriodsAsync(ct);
        var targetPeriods = periodId.HasValue && periodId.Value != Guid.Empty
            ? periods.Where(p => p.Id == periodId.Value).ToList()
            : periods.Where(p => p.Status is PeriodStatus.Open or PeriodStatus.Locked).ToList();
        if (targetPeriods.Count == 0)
            return new WorkQueueDto();

        var settingsByPeriod = targetPeriods.ToDictionary(p => p.Id, ReadSettings);

        // Lọc sơ bộ: chỉ các bước mà ở hồ sơ luồng nào đó quyền thực hiện là quyền người dùng có ở phạm vi nào đó;
        // sau đó kiểm tra chính xác từng hồ sơ theo hồ sơ luồng của hồ sơ.
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
        var today = EvaluationMapping.Today(DateTime.UtcNow);
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
            if (WorkflowActions.PeriodBlockReason(period.Status, advance) != null)
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

    /// <summary>
    /// Người dùng hiện tại thực hiện được hành động trên hồ sơ: không xung đột lợi ích (bước không phải của chủ hồ sơ thì chủ hồ sơ
    /// không làm được) và guard cho phép quyền thực hiện của bước theo hồ sơ luồng.
    /// </summary>
    private bool CanPerform(WorkflowActionDefinition action, WorkflowProfile profile, EvaluationRecord record) =>
        !IsConflictOfInterest(action, record)
        && _guard.Can(WorkflowActions.PermissionFor(action, profile), AccessTarget.ForRecord(record));

    /// <summary>
    /// Xung đột lợi ích (HD03 tr.4): mọi bước không phải của chủ hồ sơ (duyệt, xác nhận, ghi nhận, thẩm định, nhận xét, đề xuất,
    /// quyết định, công bố, mở lại) không áp dụng trên hồ sơ của chính mình — kể cả khi quyền thực hiện bước được cấu hình
    /// bằng mã quyền khác mặc định.
    /// </summary>
    private bool IsConflictOfInterest(WorkflowActionDefinition action, EvaluationRecord record) =>
        (action.Kind == WorkflowAction.Reopen || !WorkflowSteps.OwnerSteps.Contains(action.Step))
        && _currentUser.UserId is { } userId && record.MemberId == userId;

    /// <summary>Tính các hành động được phép trên hồ sơ (theo hồ sơ luồng của hồ sơ).</summary>
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

        foreach (var action in candidates)
        {
            if (WorkflowActions.PeriodBlockReason(record.Period.Status, action) != null)
                continue;
            if (!CanPerform(action, profile, record))
                continue;

            var command = action.Kind switch
            {
                WorkflowAction.Return => WorkflowCommand.Return(action.Step),
                WorkflowAction.Reopen => null,
                _ => WorkflowCommand.Complete(action.Step)
            };
            if (command != null && !RecordStateMachine.Apply(record.Status, active, command).Succeeded)
                continue;

            var deadline = profile.Deadline(action.Step);
            var overdue = action.Advances && deadline.HasValue && today > deadline.Value;
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
                    ? RecordStateMachine.ReopenTargets(active).Select(WorkflowSteps.Code).ToList()
                    : null
            };
        }
    }

    #endregion

    #region Khung thực thi chung

    /// <summary>Ngữ cảnh của một hành động đang thực hiện.</summary>
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
        bool draft = false) =>
        ExecuteAsync(recordId, request, WorkflowActions.Get(actionCode), reason, apply, ct, reopenTarget, draft);

    /// <summary>
    /// Trình tự chung: phiên bản bắt buộc → nạp hồ sơ → chế độ bước theo hồ sơ luồng → xung đột lợi ích → guard (quyền thực hiện
    /// của bước theo hồ sơ luồng) → so phiên bản → luật kỳ + thời hạn → lý do → máy trạng thái → áp dữ liệu → lịch sử → lưu (xmin).
    /// <paramref name="draft"/> = lưu nháp dữ liệu của bước: kiểm như hành động (máy trạng thái phải cho phép hoàn thành bước,
    /// tức hồ sơ đang ở bước này) nhưng không chuyển trạng thái, không ghi lịch sử hồ sơ (mỗi lần lưu nháp đã có audit log tự động)
    /// và không chặn theo thời hạn của bước (không đưa hồ sơ đi tiếp).
    /// </summary>
    private async Task<EvaluationRecordDto> ExecuteAsync(
        Guid recordId,
        WorkflowRequestDto request,
        WorkflowActionDefinition action,
        string? reason,
        Func<ActionContext, Task<string?>> apply,
        CancellationToken ct,
        WorkflowStep? reopenTarget = null,
        bool draft = false)
    {
        ArgumentNullException.ThrowIfNull(request);

        // T-24: mọi hành động ghi phải gửi phiên bản đã đọc.
        if (request.Version is null)
            throw new ValidationException("Thiếu phiên bản dữ liệu (version) của hồ sơ. Hãy tải lại hồ sơ rồi thực hiện lại.");

        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");

        var period = record.Period;
        var settings = ReadSettings(period);
        var profile = EvaluationMapping.ProfileOf(settings, record);

        // Bước không áp dụng / do cấp trên thực hiện cho nhóm đối tượng này → hành động không khớp chế độ bước (409).
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

        var periodBlock = WorkflowActions.PeriodBlockReason(period.Status, action);
        if (periodBlock != null)
            throw new ConflictException(periodBlock);

        var now = DateTime.UtcNow;
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

        _unitOfWork.SetOriginalVersion(record, request.Version);
        await _unitOfWork.SaveChangesAsync(ct);
        return await LoadDtoAsync(record, ct);
    }

    /// <summary>Nạp lại hồ sơ vừa lưu (phiên bản mới) và chuyển sang DTO.</summary>
    private async Task<EvaluationRecordDto> LoadDtoAsync(EvaluationRecord record, CancellationToken ct)
    {
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

    /// <summary>Bộ tiêu chí của kỳ — bắt buộc cho các bước có chấm điểm.</summary>
    private static CriteriaSnapshot RequireCriteria(ActionContext ctx) =>
        ctx.Criteria ?? throw new ConflictException("Kỳ đánh giá chưa có bộ tiêu chí nên chưa chấm điểm được. Hãy liên hệ người quản lý kỳ để chọn bộ tiêu chí.");

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
    /// Kiểm tra từng dòng danh mục (Mẫu 01): đủ các cột Hướng dẫn 03 yêu cầu ("mỗi sản phẩm tối thiểu có: tên và đầu ra; trục;
    /// vai trò chủ trì/phối hợp; trọng số; chuẩn đạt; dấu hiệu vượt chuẩn; thời hạn; minh chứng; thẩm quyền xác nhận"), loại việc
    /// khi đăng ký chỉ là trọng tâm / thường xuyên (đột xuất ghi nhận trong kỳ), trục thuộc bộ tiêu chí của kỳ. Mã SP, thời hạn
    /// không bắt buộc (thời hạn trống = ngày cuối kỳ).
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
