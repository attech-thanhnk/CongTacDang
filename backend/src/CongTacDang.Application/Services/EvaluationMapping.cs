using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Ánh xạ hồ sơ/kỳ sang DTO và các tính toán hiển thị dùng chung (tiến trình, thời hạn).</summary>
public static class EvaluationMapping
{
    /// <summary>Múi giờ nghiệp vụ (Việt Nam, UTC+7) để so thời hạn theo ngày.</summary>
    public static readonly TimeSpan BusinessUtcOffset = TimeSpan.FromHours(7);

    /// <summary>Ngày hiện tại theo giờ Việt Nam.</summary>
    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow + BusinessUtcOffset);

    /// <summary>
    /// Khoảng cách tới thời hạn theo ngày: hôm nay sau ngày hạn → số ngày quá hạn (≥ 1); ngược lại → số ngày còn lại
    /// (0 = hạn là hôm nay). Không có hạn → (null, null).
    /// </summary>
    public static (int? DaysOverdue, int? DaysLeft) DeadlineDistance(DateOnly? deadline, DateOnly today)
    {
        if (!deadline.HasValue)
            return (null, null);
        var diff = deadline.Value.DayNumber - today.DayNumber;
        return diff < 0 ? (-diff, null) : (null, diff);
    }

    /// <summary>Tên mức xếp loại (mã enum) — rỗng khi chưa xếp loại để frontend hiển thị "—".</summary>
    public static string GradeCode(EvaluationGrade grade) => grade.ToString();

    /// <summary>Đọc mức xếp loại từ chuỗi (mã enum, không phân biệt hoa thường); null nếu không hợp lệ.</summary>
    public static EvaluationGrade? ParseGrade(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return Enum.TryParse<EvaluationGrade>(value.Trim(), true, out var grade) && Enum.IsDefined(grade) ? grade : null;
    }

    /// <summary>Hồ sơ luồng áp dụng cho hồ sơ (theo mã đã lưu; không có → mặc định theo cấp quyết định).</summary>
    public static WorkflowProfile ProfileOf(PeriodSettings settings, EvaluationRecord record) =>
        settings.ResolveProfile(record.WorkflowProfileCode, record.ApprovalAuthority);

    /// <summary>
    /// Tiến trình 9 bước của hồ sơ theo hồ sơ luồng của hồ sơ, kèm chủ thể thực hiện từng bước và người/thời điểm
    /// hoàn thành của bước đã xong (lấy từ các trường …ByName/…At của hồ sơ hoặc kết quả do cấp trên thực hiện).
    /// </summary>
    public static List<RecordStepProgressDto> Progress(EvaluationRecord record, WorkflowProfile profile, DateOnly today)
    {
        var result = Progress(record.Status, profile, today);
        foreach (var item in result)
        {
            var step = WorkflowSteps.Parse(item.Step)!.Value;
            item.Actor = StepActor(step, profile, record);
            if (item.State != "done")
                continue;
            var (at, by) = StepCompletion(step, profile, record);
            item.DoneAt = at;
            item.DoneByName = by;
        }

        return result;
    }

    /// <summary>
    /// Mô tả chủ thể thực hiện bước theo chế độ trong hồ sơ luồng và dữ liệu ảnh chụp của hồ sơ (Phòng, Chi bộ, cấp quyết định).
    /// Không ghi cứng tên đơn vị — tên lấy từ dữ liệu.
    /// </summary>
    public static string StepActor(WorkflowStep step, WorkflowProfile profile, EvaluationRecord record)
    {
        var mode = profile.Mode(step);
        if (mode == StepMode.Off)
            return string.Empty;
        if (mode == StepMode.External)
            return "Cấp trên thực hiện";

        var department = Clean(record.Department?.Name);
        var cell = Clean(record.PartyCell?.Name);
        var unitLeaders = department != null ? $"Lãnh đạo {department}" : "Lãnh đạo đơn vị";
        return step switch
        {
            WorkflowStep.B1_REGISTER or WorkflowStep.B2_SELF_SCORE => Clean(record.Member?.FullName) ?? "Chủ hồ sơ",
            WorkflowStep.B1_APPROVE => unitLeaders,
            WorkflowStep.B2_CELL_CONFIRM => cell == null
                ? "Chi bộ"
                : cell.StartsWith("Chi bộ", StringComparison.OrdinalIgnoreCase) ? cell : $"Chi bộ {cell}",
            WorkflowStep.B3A_COLLECTIVE => department != null ? $"Tập thể lãnh đạo {department}" : "Tập thể lãnh đạo đơn vị",
            WorkflowStep.B3B_APPRAISAL => "Cơ quan thẩm định",
            WorkflowStep.B3C_DIRECTOR => string.Equals(profile.PermissionFor(step), WorkflowPermissions.UnitReview, StringComparison.Ordinal)
                ? unitLeaders
                : "Cấp trực tiếp sử dụng",
            WorkflowStep.B4_DECISION => record.ApprovalAuthority == ApprovalAuthority.CapTren
                ? "Cấp trên có thẩm quyền quyết định"
                : "Đảng ủy cơ sở",
            WorkflowStep.B5_PUBLISH => "Cơ quan công bố kết quả",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Thời điểm và người hoàn thành bước: bước cấp trên → kết quả ghi nhận (thời điểm ghi nhận, tên cơ quan);
    /// bước nội bộ → các trường …At/…ByName của hồ sơ. Bước đăng ký sản phẩm không lưu thời điểm riêng → null.
    /// </summary>
    public static (DateTime? At, string? ByName) StepCompletion(WorkflowStep step, WorkflowProfile profile, EvaluationRecord record)
    {
        if (profile.Mode(step) == StepMode.External)
        {
            var external = record.ExternalResults?.FirstOrDefault(x => x.Step == step);
            if (external != null)
                return (external.RecordedAt, Clean(external.AuthorityName) ?? Clean(external.RecordedByName));
        }

        return step switch
        {
            WorkflowStep.B1_REGISTER => (null, Clean(record.Member?.FullName)),
            WorkflowStep.B1_APPROVE => (record.TasksApprovedAt, Clean(record.TasksApprovedByName)),
            WorkflowStep.B2_SELF_SCORE => (record.SelfScoredAt, Clean(record.Member?.FullName)),
            WorkflowStep.B2_CELL_CONFIRM => (record.CellConfirmedAt, Clean(record.CellConfirmedByName)),
            WorkflowStep.B3A_COLLECTIVE => (record.CollectiveRecordedAt, Clean(record.CollectiveRecordedByName)),
            WorkflowStep.B3B_APPRAISAL => (record.AppraisedAt, Clean(record.AppraisedByName)),
            WorkflowStep.B3C_DIRECTOR => (record.DirectorReviewedAt, Clean(record.DirectorReviewedByName)),
            WorkflowStep.B4_DECISION => (record.DecisionRecordedAt, Clean(record.DecisionRecordedByName) ?? Clean(record.DecisionAuthorityName)),
            WorkflowStep.B5_PUBLISH => (record.PublishedAt, Clean(record.PublishedByName)),
            _ => (null, null)
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Tiến trình 9 bước theo trạng thái (chưa kèm chủ thể và người hoàn thành).</summary>
    public static List<RecordStepProgressDto> Progress(RecordStatus status, WorkflowProfile profile, DateOnly today)
    {
        var enabled = profile.ActiveSteps();
        var current = WorkflowSteps.StepOf(status);
        var currentIndex = current.HasValue ? WorkflowSteps.IndexOf(current.Value) : int.MaxValue;
        var result = new List<RecordStepProgressDto>();

        foreach (var step in WorkflowSteps.Ordered)
        {
            var index = WorkflowSteps.IndexOf(step);
            string state;
            if (!enabled.Contains(step))
                state = "skipped";
            else if (status == RecordStatus.Published || index < currentIndex)
                state = "done";
            else if (index == currentIndex)
                state = "current";
            else
                state = "pending";

            var deadline = profile.Deadline(step);
            result.Add(new RecordStepProgressDto
            {
                Step = WorkflowSteps.Code(step),
                Name = WorkflowSteps.DisplayName(step),
                Enabled = enabled.Contains(step),
                Mode = profile.Mode(step).ToString(),
                State = state,
                Deadline = deadline,
                Overdue = deadline.HasValue && state is "current" or "pending" && today > deadline.Value
            });
        }

        return result;
    }

    /// <summary>Ánh xạ hồ sơ sang DTO. <paramref name="evidence"/>: tệp minh chứng hiện hành theo Id nhiệm vụ (T-53).</summary>
    public static EvaluationRecordDto ToDto(
        EvaluationRecord r,
        PeriodSettings settings,
        DateOnly today,
        IReadOnlyDictionary<Guid, (Guid AttachmentId, string FileName)>? evidence = null,
        CriteriaSnapshot? criteria = null)
    {
        criteria ??= SafeCriteria(r.Period);
        var currentStep = WorkflowSteps.StepOf(r.Status);
        var profile = ProfileOf(settings, r);

        return new EvaluationRecordDto
        {
            Id = r.Id,
            Version = r.Version,
            PeriodId = r.PeriodId,
            PeriodName = r.Period?.Name ?? string.Empty,
            PeriodStatus = r.Period?.Status.ToString() ?? string.Empty,
            SelfScoreForm = criteria?.SelfScoreForm ?? string.Empty,
            MemberId = r.MemberId,
            FullName = r.Member?.FullName ?? string.Empty,
            PartyCardNumber = r.Member?.PartyCardNumber,
            PositionTitle = r.Member?.PositionTitle ?? string.Empty,
            PartyCellId = r.PartyCellId,
            PartyCellName = r.PartyCell?.Name,
            DepartmentId = r.DepartmentId,
            DepartmentName = r.Department?.Name,
            WeightFrameCode = r.WeightFrameCode,
            BaseWeightFrame = RecordWeightFrameDto.From(RecordWeightFrames.Base(r, criteria?.Content)),
            WeightFrame = RecordWeightFrameDto.From(RecordWeightFrames.Effective(r, criteria?.Content)),
            WeightFrameAdjusted = RecordWeightFrames.IsAdjusted(r) && RecordWeightFrames.Base(r, criteria?.Content) != null,
            FrameAdjustLimit = criteria?.Content.Parameters.AllowsFrameAdjustment == true ? criteria.Content.Parameters.FrameAdjustLimit : null,
            ApprovalAuthority = r.ApprovalAuthority.ToString(),
            WorkflowProfileCode = profile.Code,
            WorkflowProfileName = profile.Name,

            Status = r.Status.ToString(),
            StatusDisplayName = WorkflowSteps.StatusDisplayName(r.Status),
            CurrentStep = currentStep.HasValue ? WorkflowSteps.Code(currentStep.Value) : null,
            ReturnReason = r.ReturnReason,
            Progress = Progress(r, profile, today),

            TasksApprovedByName = r.TasksApprovedByName,
            TasksApprovedAt = r.TasksApprovedAt,
            TasksApprovalComment = r.TasksApprovalComment,

            GeneralScores = SafeGeneralScores(r.GeneralScores),
            GeneralCriteriaScore = r.GeneralCriteriaScore,
            TasksScore = r.TasksScore,
            AxisScores = SafeAxisScores(r.AxisScores),
            TotalSelfScore = r.TotalSelfScore,
            SelfProposedGrade = GradeCode(r.SelfProposedGrade),
            SelfScoredAt = r.SelfScoredAt,
            SelfAssessment = SafeParse(() => RecordFormContent.ParseSelfAssessment(r.SelfAssessment), new Dictionary<string, string>()),
            AxisReports = SafeParse(() => RecordFormContent.ParseAxisReports(r.AxisReports), new Dictionary<string, AxisReport>()),
            AxisNotes = SafeParse(() => RecordFormContent.ParseAxisNotes(r.AxisNotes), new Dictionary<string, AxisNote>()),
            GeneralGroupNotes = SafeParse(() => RecordFormContent.ParseGeneralGroupNotes(r.GeneralGroupNotes), new Dictionary<string, string>()),

            PartyCellComment = r.PartyCellComment,
            CellConfirmedByName = r.CellConfirmedByName,
            CellConfirmedAt = r.CellConfirmedAt,

            CollectiveProposedGrade = GradeCode(r.CollectiveProposedGrade),
            CollectiveComment = r.CollectiveComment,
            CollectiveMeetingId = r.CollectiveMeetingId,
            CollectiveRecordedByName = r.CollectiveRecordedByName,
            CollectiveRecordedAt = r.CollectiveRecordedAt,

            AppraisalScore = r.AppraisalScore,
            AppraisalGeneralScore = r.AppraisalGeneralScore,
            AppraisalTasksScore = r.AppraisalTasksScore,
            AppraisalComment = r.AppraisalComment,
            AppraisalExplanation = r.AppraisalExplanation,
            AppraisalProposedGrade = GradeCode(r.AppraisalProposedGrade),
            AppraisedByName = r.AppraisedByName,
            AppraisedAt = r.AppraisedAt,

            DirectorComment = r.DirectorComment,
            DirectorProposedGrade = GradeCode(r.DirectorProposedGrade),
            DirectorReviewedByName = r.DirectorReviewedByName,
            DirectorReviewedAt = r.DirectorReviewedAt,

            FinalScore = r.FinalScore,
            FinalGrade = GradeCode(r.FinalGrade),
            DecisionDocumentNumber = r.DecisionDocumentNumber,
            DecisionDocumentDate = r.DecisionDocumentDate,
            DecisionAuthorityName = r.DecisionAuthorityName,
            CadreWorkProposal = r.CadreWorkProposal,
            DecisionMeetingId = r.DecisionMeetingId,
            DecisionRecordedByName = r.DecisionRecordedByName,
            DecisionRecordedAt = r.DecisionRecordedAt,

            PublishedByName = r.PublishedByName,
            PublishedAt = r.PublishedAt,

            ExternalResults = (r.ExternalResults ?? new List<EvaluationExternalResult>())
                .OrderBy(x => WorkflowSteps.IndexOf(x.Step))
                .Select(ToExternalResultDto)
                .ToList(),

            Tasks = (r.Tasks ?? new List<EvaluationTask>())
                .Where(t => !t.IsDeleted)
                .OrderBy(t => t.TaskOrder)
                .Select(t => ToTaskDto(t, evidence))
                .ToList(),

            GradeWarnings = GradeWarnings(r, r.EffectiveGrade(), criteria).ToList(),
            ExcellentMinExceedRatio = criteria is { UsesAxisScoring: false } ? GradeConditions.ExcellentMinExceedRatio(criteria.Content) : null
        };
    }

    /// <summary>
    /// Cảnh báo điều kiện xếp loại của hồ sơ với mức <paramref name="grade"/> theo mức thẩm định từng nhiệm vụ — chỉ khi bộ
    /// tiêu chí của kỳ chấm theo danh mục nhiệm vụ (09A); bộ chấm theo trục (09B) không có nhiệm vụ → rỗng.
    /// </summary>
    public static IReadOnlyList<string> GradeWarnings(EvaluationRecord r, EvaluationGrade grade, CriteriaSnapshot? criteria)
    {
        if (criteria is not { UsesAxisScoring: false } || r.Tasks == null)
            return Array.Empty<string>();
        var tasks = r.Tasks
            .Where(t => !t.IsDeleted && TaskChangeRules.IsActive(t))
            .Select(t => new GradeConditionTask(t.Kind, t.AppraisalLevel, t.IsExceedStandard))
            .ToList();
        return GradeConditions.Check(tasks, grade, GradeConditions.ExcellentMinExceedRatio(criteria.Content));
    }

    /// <summary>Ánh xạ nhiệm vụ; tên tệp minh chứng theo phiên bản hiện hành nếu có (T-53).</summary>
    public static EvaluationTaskDto ToTaskDto(EvaluationTask t, IReadOnlyDictionary<Guid, (Guid AttachmentId, string FileName)>? evidence)
    {
        (Guid AttachmentId, string FileName)? current = evidence != null && evidence.TryGetValue(t.Id, out var found) ? found : null;
        // Chưa nhập tỷ lệ A-B-C-D: trả null để phiếu tự chấm để trống, không hiện thành 100%.
        var entered = !TaskSelfScores.IsPending(t);
        return new EvaluationTaskDto
        {
            Id = t.Id,
            Version = t.Version,
            RecordId = t.RecordId,
            TaskOrder = t.TaskOrder,
            TaskName = t.TaskName,
            TargetOutput = t.TargetOutput,
            Weight = t.Weight,
            Deadline = t.Deadline,
            AxisCode = t.AxisCode,
            ProductCode = t.ProductCode,
            Kind = t.Kind?.ToString(),
            Role = t.Role?.ToString(),
            ExceedCriteria = t.ExceedCriteria,
            EvidenceSource = t.EvidenceSource,
            Confirmer = t.Confirmer,
            ChangeStatus = t.ChangeStatus?.ToString(),
            CriteriaA_Ratio = entered ? t.CriteriaA_Ratio : null,
            CriteriaB_Ratio = entered ? t.CriteriaB_Ratio : null,
            CriteriaC_Ratio = entered ? t.CriteriaC_Ratio : null,
            CriteriaD_Ratio = entered ? t.CriteriaD_Ratio : null,
            SelfScoreEntered = entered,
            SelfScore = t.SelfScore,
            SupervisorScore = t.SupervisorScore,
            IsExceedStandard = t.IsExceedStandard,
            AppraisalLevel = t.AppraisalLevel,
            AppraisalComment = t.AppraisalComment,
            AttachmentId = current?.AttachmentId ?? t.AttachmentId,
            AttachmentFileName = current?.FileName ?? t.Attachment?.FileName ?? t.Attachment?.OriginalFileName,
            AttachmentOriginalName = current?.FileName ?? t.Attachment?.OriginalFileName ?? t.Attachment?.FileName
        };
    }

    /// <summary>Ánh xạ kết quả ghi nhận của bước do cấp trên thực hiện.</summary>
    public static ExternalResultDto ToExternalResultDto(EvaluationExternalResult x) => new()
    {
        Step = WorkflowSteps.Code(x.Step),
        StepName = WorkflowSteps.DisplayName(x.Step),
        AuthorityName = x.AuthorityName,
        DocumentNumber = x.DocumentNumber,
        DocumentDate = x.DocumentDate,
        Comment = x.Comment,
        Grade = GradeCode(x.Grade),
        Score = x.Score,
        AttachmentId = x.AttachmentId,
        RecordedByName = x.RecordedByName,
        RecordedAt = x.RecordedAt
    };

    /// <summary>Ánh xạ lịch sử.</summary>
    public static EvaluationRecordHistoryDto ToHistoryDto(EvaluationRecordHistory x) => new()
    {
        Id = x.Id,
        RecordId = x.RecordId,
        FromStatus = x.FromStatus?.ToString(),
        FromStatusName = x.FromStatus.HasValue ? SafeStatusName(x.FromStatus.Value) : null,
        ToStatus = x.ToStatus.ToString(),
        ToStatusName = SafeStatusName(x.ToStatus),
        Step = x.Step.HasValue ? WorkflowSteps.Code(x.Step.Value) : null,
        StepName = x.Step.HasValue ? WorkflowSteps.DisplayName(x.Step.Value) : null,
        Action = x.Action.ToString(),
        ActionName = WorkflowSteps.ActionDisplayName(x.Action),
        Reason = x.Reason,
        ScoreBefore = x.ScoreBefore,
        ScoreAfter = x.ScoreAfter,
        GradeBefore = x.GradeBefore?.ToString(),
        GradeAfter = x.GradeAfter?.ToString(),
        ActorId = x.ActorId,
        ActorName = x.ActorName,
        Comment = x.Comment,
        CreatedAt = x.CreatedAt
    };

    /// <summary>Ảnh chụp bộ tiêu chí của kỳ; null nếu chưa chọn hoặc lỗi định dạng (hiển thị không làm hỏng trang).</summary>
    public static CriteriaSnapshot? SafeCriteria(EvaluationPeriod? period)
    {
        if (period == null)
            return null;
        try
        {
            return period.GetCriteria();
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static Dictionary<string, GeneralItemScore> SafeGeneralScores(string? json)
    {
        try
        {
            return EvaluationScoring.ParseGeneralScores(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return new Dictionary<string, GeneralItemScore>();
        }
    }

    private static Dictionary<string, double>? SafeAxisScores(string? json)
    {
        try
        {
            return EvaluationScoring.ParseAxisScores(json);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    /// <summary>Đọc nội dung biểu mẫu (jsonb) trên hồ sơ; lỗi định dạng → giá trị rỗng (hiển thị không làm hỏng trang).</summary>
    private static T SafeParse<T>(Func<T> parse, T fallback)
    {
        try
        {
            return parse();
        }
        catch (System.Text.Json.JsonException)
        {
            return fallback;
        }
    }

    private static string SafeStatusName(RecordStatus status) =>
        Enum.IsDefined(status) ? WorkflowSteps.StatusDisplayName(status) : $"Trạng thái cũ ({(int)status})";

    /// <summary>Ánh xạ kỳ.</summary>
    public static EvaluationPeriodDto ToPeriodDto(EvaluationPeriod p, int totalRecords, Guid? activePeriodId)
    {
        PeriodSettings settings;
        try
        {
            settings = p.GetSettings();
        }
        catch (FormatException)
        {
            settings = PeriodSettings.Default();
        }

        return new EvaluationPeriodDto
        {
            Id = p.Id,
            Version = p.Version,
            Year = p.Year,
            Quarter = (int)p.Quarter,
            Name = p.Name,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Status = p.Status.ToString(),
            StatusDisplayName = WorkflowSteps.PeriodStatusDisplayName(p.Status),
            StatusReason = p.StatusReason,
            TotalRecords = totalRecords,
            IsActive = activePeriodId.HasValue && activePeriodId.Value == p.Id,
            Settings = settings,
            CriteriaSetId = p.CriteriaSetId,
            Criteria = SafeCriteria(p)
        };
    }

    /// <summary>
    /// Kỳ hiện hành = kỳ Đang mở/Khóa dữ liệu mới nhất (năm, quý, thời điểm tạo); không có thì null.
    /// </summary>
    public static EvaluationPeriod? ActivePeriod(IEnumerable<EvaluationPeriod> periods) =>
        periods
            .Where(p => p.Status is PeriodStatus.Open or PeriodStatus.Locked)
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Quarter)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefault();
}
