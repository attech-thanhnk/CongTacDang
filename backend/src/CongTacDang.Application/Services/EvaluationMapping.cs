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

    /// <summary>Tiến trình 9 bước của hồ sơ theo hồ sơ luồng của hồ sơ.</summary>
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
            ApprovalAuthority = r.ApprovalAuthority.ToString(),
            WorkflowProfileCode = profile.Code,
            WorkflowProfileName = profile.Name,

            Status = r.Status.ToString(),
            StatusDisplayName = WorkflowSteps.StatusDisplayName(r.Status),
            CurrentStep = currentStep.HasValue ? WorkflowSteps.Code(currentStep.Value) : null,
            ReturnReason = r.ReturnReason,
            Progress = Progress(r.Status, profile, today),

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
            TaskResults = SafeParse(() => RecordFormContent.ParseTaskResults(r.TaskResults), new List<TaskResultRow>()),
            AxisNotes = SafeParse(() => RecordFormContent.ParseAxisNotes(r.AxisNotes), new Dictionary<string, AxisNote>()),

            PartyCellComment = r.PartyCellComment,
            CellConfirmedByName = r.CellConfirmedByName,
            CellConfirmedAt = r.CellConfirmedAt,

            CollectiveProposedGrade = GradeCode(r.CollectiveProposedGrade),
            CollectiveComment = r.CollectiveComment,
            CollectiveMeetingId = r.CollectiveMeetingId,
            CollectiveRecordedByName = r.CollectiveRecordedByName,
            CollectiveRecordedAt = r.CollectiveRecordedAt,

            AppraisalScore = r.AppraisalScore,
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
                .ToList()
        };
    }

    /// <summary>Ánh xạ nhiệm vụ; tên tệp minh chứng theo phiên bản hiện hành nếu có (T-53).</summary>
    public static EvaluationTaskDto ToTaskDto(EvaluationTask t, IReadOnlyDictionary<Guid, (Guid AttachmentId, string FileName)>? evidence)
    {
        (Guid AttachmentId, string FileName)? current = evidence != null && evidence.TryGetValue(t.Id, out var found) ? found : null;
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
            CriteriaA_Ratio = t.CriteriaA_Ratio,
            CriteriaB_Ratio = t.CriteriaB_Ratio,
            CriteriaC_Ratio = t.CriteriaC_Ratio,
            CriteriaD_Ratio = t.CriteriaD_Ratio,
            SelfScore = t.SelfScore,
            SupervisorScore = t.SupervisorScore,
            IsExceedStandard = t.IsExceedStandard,
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
            settings = PeriodSettings.FullPreset();
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
