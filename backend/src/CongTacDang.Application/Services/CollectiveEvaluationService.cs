using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>Quản lý hồ sơ đánh giá tập thể và biên bản hội nghị theo các Mẫu 06-08, 12-13.</summary>
public interface ICollectiveEvaluationService
{
    /// <summary>Lấy hồ sơ tập thể trong phạm vi của người dùng.</summary>
    Task<List<CollectiveEvaluationRecordDto>> GetCollectiveRecordsAsync(Guid periodId, Guid requesterId, string? form = null);

    /// <summary>Lấy một hồ sơ tập thể trong phạm vi của người dùng.</summary>
    Task<CollectiveEvaluationRecordDto> GetCollectiveRecordAsync(Guid id, Guid requesterId);

    /// <summary>Tạo hồ sơ tập thể mới.</summary>
    Task<CollectiveEvaluationRecordDto> CreateCollectiveRecordAsync(Guid requesterId, SaveCollectiveEvaluationRequestDto dto);

    /// <summary>Lấy biên bản hội nghị trong phạm vi của người dùng.</summary>
    Task<List<EvaluationMeetingDto>> GetMeetingsAsync(Guid periodId, Guid requesterId, Guid? partyCellId = null);

    /// <summary>Lấy một biên bản hội nghị trong phạm vi của người dùng.</summary>
    Task<EvaluationMeetingDto> GetMeetingAsync(Guid id, Guid requesterId);

    /// <summary>Tạo biên bản hội nghị hoặc biên bản kiểm phiếu.</summary>
    Task<EvaluationMeetingDto> CreateMeetingAsync(Guid requesterId, SaveEvaluationMeetingRequestDto dto);
}

/// <summary>Triển khai nghiệp vụ hồ sơ tập thể và hội nghị đánh giá.</summary>
public class CollectiveEvaluationService : ICollectiveEvaluationService
{
    private readonly ICollectiveEvaluationRepository _collectiveRepo;
    private readonly IEvaluationMeetingRepository _meetingRepo;
    private readonly IEvaluationRepository _evaluationRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationGuard _guard;

    public CollectiveEvaluationService(
        ICollectiveEvaluationRepository collectiveRepo,
        IEvaluationMeetingRepository meetingRepo,
        IEvaluationRepository evaluationRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IAuthorizationGuard guard)
    {
        _collectiveRepo = collectiveRepo;
        _meetingRepo = meetingRepo;
        _evaluationRepo = evaluationRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _guard = guard;
    }

    /// <summary>Lấy hồ sơ tập thể theo kỳ, biểu mẫu và phạm vi người dùng.</summary>
    public async Task<List<CollectiveEvaluationRecordDto>> GetCollectiveRecordsAsync(Guid periodId, Guid requesterId, string? form = null)
    {
        var scope = CollectiveReadScope();
        var parsedForm = ParseCollectiveForm(form);
        var records = await _collectiveRepo.GetByPeriodAsync(periodId, parsedForm);
        return records
            .Where(record => scope.Matches(null, record.DepartmentId, record.PartyCellId))
            .Select(MapCollective)
            .ToList();
    }

    /// <summary>Lấy một hồ sơ tập thể và kiểm tra phạm vi tổ chức.</summary>
    public async Task<CollectiveEvaluationRecordDto> GetCollectiveRecordAsync(Guid id, Guid requesterId)
    {
        var record = await _collectiveRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ tập thể với Id: {id}");

        var target = new AccessTarget(DepartmentId: record.DepartmentId, PartyCellId: record.PartyCellId);
        if (!_guard.Can(PermissionCodes.EvaluationRead, target) && !_guard.Can(PermissionCodes.CollectiveManage, target))
            throw new ForbiddenException("Bạn không có quyền xem hồ sơ tập thể của tổ chức này (ngoài phạm vi được gán).");
        return MapCollective(record);
    }

    /// <summary>Tạo hồ sơ Mẫu 06, 07 hoặc 08 sau khi kiểm tra phạm vi tổ chức.</summary>
    public async Task<CollectiveEvaluationRecordDto> CreateCollectiveRecordAsync(Guid requesterId, SaveCollectiveEvaluationRequestDto dto)
    {
        var form = ParseCollectiveForm(dto.Form)
            ?? throw new ArgumentException("Hồ sơ tập thể phải có mã M06, M07 hoặc M08.");
        if (string.IsNullOrWhiteSpace(dto.SubjectName))
            throw new ArgumentException("Tên tập thể hoặc lĩnh vực đánh giá không được để trống.");

        _guard.Ensure(PermissionCodes.CollectiveManage, new AccessTarget(DepartmentId: dto.DepartmentId, PartyCellId: dto.PartyCellId));
        // Điểm tối đa lấy từ tham số kỳ (mặc định 30 / 70 như trước task 12).
        var period = await _evaluationRepo.GetPeriodByIdAsync(dto.PeriodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {dto.PeriodId}");
        var parameters = SafeParameters(period);
        ValidateScore(dto.GeneralCriteriaScore, parameters.CollectiveGeneralMaxScore, "Điểm nhóm tiêu chí chung");
        ValidateScore(dto.TaskCriteriaScore, parameters.CollectiveTaskMaxScore, "Điểm nhóm kết quả thực hiện nhiệm vụ");

        var record = new CollectiveEvaluationRecord
        {
            PeriodId = dto.PeriodId,
            Form = form,
            PartyCellId = dto.PartyCellId,
            DepartmentId = dto.DepartmentId,
            HeadId = dto.HeadId,
            SubjectName = dto.SubjectName.Trim(),
            Strengths = dto.Strengths,
            Limitations = dto.Limitations,
            Causes = dto.Causes,
            PreviousRemediation = dto.PreviousRemediation,
            Explanation = dto.Explanation,
            Responsibilities = dto.Responsibilities,
            RemediationPlan = dto.RemediationPlan,
            GeneralCriteriaScore = dto.GeneralCriteriaScore,
            TaskCriteriaScore = dto.TaskCriteriaScore,
            TotalScore = Math.Round(dto.GeneralCriteriaScore + dto.TaskCriteriaScore, 2),
            SelfProposedGrade = ParseGrade(dto.SelfProposedGrade),
            Status = CollectiveRecordStatus.Submitted,
            CreatedBy = requesterId,
            UpdatedBy = requesterId,
            UpdatedAt = DateTime.UtcNow,
            Items = dto.Items.Select((item, index) => new CollectiveEvaluationItem
            {
                ItemOrder = item.ItemOrder > 0 ? item.ItemOrder : index + 1,
                Category = item.Category,
                TaskName = item.TaskName,
                PlanOrDirection = item.PlanOrDirection,
                Result = item.Result,
                Limitations = item.Limitations,
                Notes = item.Notes
            }).ToList()
        };

        await _collectiveRepo.AddAsync(record);
        var saved = await _collectiveRepo.GetByIdAsync(record.Id) ?? record;
        return MapCollective(saved);
    }

    /// <summary>Lấy danh sách biên bản hội nghị theo kỳ và Chi bộ.</summary>
    public async Task<List<EvaluationMeetingDto>> GetMeetingsAsync(Guid periodId, Guid requesterId, Guid? partyCellId = null)
    {
        // T-45: chỉ thấy biên bản thuộc phạm vi meeting.read/meeting.manage được gán; không có phạm vi phù hợp → danh sách rỗng.
        var scope = _guard.GetScope(PermissionCodes.MeetingRead).Union(_guard.GetScope(PermissionCodes.MeetingManage));
        if (scope.IsEmpty)
            return new List<EvaluationMeetingDto>();

        var meetings = await _meetingRepo.GetByPeriodAsync(periodId, partyCellId);
        return meetings
            .Where(meeting => scope.Matches(null, meeting.DepartmentId, meeting.PartyCellId))
            .Select(MapMeeting)
            .ToList();
    }

    /// <summary>Lấy biên bản hội nghị và kiểm tra phạm vi Chi bộ.</summary>
    public async Task<EvaluationMeetingDto> GetMeetingAsync(Guid id, Guid requesterId)
    {
        var meeting = await _meetingRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy biên bản hội nghị với Id: {id}");

        var target = new AccessTarget(DepartmentId: meeting.DepartmentId, PartyCellId: meeting.PartyCellId);
        if (!_guard.Can(PermissionCodes.MeetingRead, target) && !_guard.Can(PermissionCodes.MeetingManage, target))
            throw new ForbiddenException("Bạn không có quyền xem biên bản của Phòng/Chi bộ này (ngoài phạm vi được gán).");

        return MapMeeting(meeting);
    }

    /// <summary>
    /// Tạo biên bản Mẫu 12 hoặc Mẫu 13, chỉ lưu tổng hợp phiếu không định danh. Biên bản gắn Chi bộ, Phòng (hội nghị tập thể
    /// lãnh đạo cấp Phòng — task 12) hoặc không gắn (cấp Công ty, chỉ phạm vi Toàn công ty).
    /// </summary>
    public async Task<EvaluationMeetingDto> CreateMeetingAsync(Guid requesterId, SaveEvaluationMeetingRequestDto dto)
    {
        var partyCellId = dto.PartyCellId == Guid.Empty ? null : dto.PartyCellId;
        var departmentId = dto.DepartmentId == Guid.Empty ? null : dto.DepartmentId;
        _guard.Ensure(PermissionCodes.MeetingManage, new AccessTarget(DepartmentId: departmentId, PartyCellId: partyCellId));
        if (dto.FormCode is not ("M12" or "M13"))
            throw new ArgumentException("Biên bản chỉ hỗ trợ M12 hoặc M13.");
        WorkflowStep? stage = null;
        if (!string.IsNullOrWhiteSpace(dto.Stage))
        {
            stage = WorkflowSteps.Parse(dto.Stage);
            if (stage is not (WorkflowStep.B3A_COLLECTIVE or WorkflowStep.B4_DECISION))
                throw new ArgumentException("Biên bản chỉ dùng cho bước đề xuất của tập thể lãnh đạo (B3A_COLLECTIVE) hoặc quyết định (B4_DECISION).");
        }
        if (dto.FormCode == "M13" && dto.VoteSummaries.Count == 0)
            throw new ArgumentException("Mẫu 13 phải có tổng hợp phiếu theo từng hồ sơ cán bộ.");
        if (dto.InvitedCount < 0 || dto.PresentCount < 0 || dto.AbsentCount < 0 || dto.PresentCount > dto.InvitedCount)
            throw new ArgumentException("Số lượng triệu tập, có mặt, vắng mặt không hợp lệ: không âm và số có mặt không vượt số triệu tập.");
        _ = await _evaluationRepo.GetPeriodByIdAsync(dto.PeriodId)
            ?? throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {dto.PeriodId}");

        var meeting = new EvaluationMeeting
        {
            PeriodId = dto.PeriodId,
            PartyCellId = partyCellId,
            DepartmentId = departmentId,
            Stage = stage,
            FormCode = dto.FormCode,
            MeetingType = dto.MeetingType,
            Location = dto.Location,
            StartedAt = dto.StartedAt,
            EndedAt = dto.EndedAt,
            InvitedCount = dto.InvitedCount,
            PresentCount = dto.PresentCount,
            AbsentCount = dto.AbsentCount,
            AbsentReasons = dto.AbsentReasons,
            ChairId = dto.ChairId,
            ChairName = dto.ChairName,
            SecretaryId = dto.SecretaryId,
            SecretaryName = dto.SecretaryName,
            MinutesContent = dto.MinutesContent,
            OutcomeContent = dto.OutcomeContent,
            VoteCountingContent = dto.VoteCountingContent,
            CreatedBy = requesterId,
            UpdatedBy = requesterId,
            UpdatedAt = DateTime.UtcNow
        };

        if (dto.FormCode == "M13")
        {
            foreach (var vote in dto.VoteSummaries)
            {
                if (vote.VotesExcellent < 0 || vote.VotesGood < 0 || vote.VotesSatisfactory < 0 || vote.VotesUnsatisfactory < 0 || vote.InvalidVotes < 0)
                    throw new ArgumentException("Số phiếu không được âm.");
                var total = vote.VotesExcellent + vote.VotesGood + vote.VotesSatisfactory + vote.VotesUnsatisfactory + vote.InvalidVotes;
                if (total > dto.PresentCount)
                    throw new ArgumentException(
                        $"Tổng số phiếu các mức và phiếu không hợp lệ ({total}) vượt số người có mặt ({dto.PresentCount}). Hãy kiểm tra lại kết quả kiểm phiếu.");
                if (meeting.VoteSummaries.Any(v => v.RecordId == vote.RecordId))
                    throw new ArgumentException("Mỗi hồ sơ chỉ có một dòng kết quả kiểm phiếu trong biên bản.");

                var record = await _evaluationRepo.GetRecordByIdAsync(vote.RecordId)
                    ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {vote.RecordId}");
                if (record.PeriodId != dto.PeriodId
                    || (partyCellId.HasValue && record.PartyCellId != partyCellId)
                    || (departmentId.HasValue && record.DepartmentId != departmentId))
                    throw new ForbiddenException("Biên bản chỉ được chứa hồ sơ cùng kỳ và cùng Phòng/Chi bộ với biên bản.");

                meeting.VoteSummaries.Add(new EvaluationMeetingVoteSummary
                {
                    RecordId = vote.RecordId,
                    VotesExcellent = vote.VotesExcellent,
                    VotesGood = vote.VotesGood,
                    VotesSatisfactory = vote.VotesSatisfactory,
                    VotesUnsatisfactory = vote.VotesUnsatisfactory,
                    InvalidVotes = vote.InvalidVotes,
                    Notes = vote.Notes
                });
            }
        }

        await _meetingRepo.AddAsync(meeting);
        var saved = await _meetingRepo.GetByIdAsync(meeting.Id) ?? meeting;
        return MapMeeting(saved);
    }

    /// <summary>
    /// Phạm vi đọc hồ sơ tập thể: hợp phạm vi <c>evaluation.read</c> (không tính luật chủ hồ sơ) và <c>collective.manage</c>.
    /// </summary>
    private ScopeFilter CollectiveReadScope()
    {
        var read = _guard.GetScope(PermissionCodes.EvaluationRead) with { OwnerId = null };
        return read.Union(_guard.GetScope(PermissionCodes.CollectiveManage));
    }

    /// <summary>Tham số của kỳ (mặc định nếu cấu hình lỗi).</summary>
    private static EvaluationParameters SafeParameters(EvaluationPeriod period)
    {
        try
        {
            return period.GetSettings().Parameters;
        }
        catch (FormatException)
        {
            return new EvaluationParameters();
        }
    }

    /// <summary>Chuyển mã Mẫu 06-08 sang enum nghiệp vụ.</summary>
    private static CollectiveEvaluationForm? ParseCollectiveForm(string? form)
    {
        if (string.IsNullOrWhiteSpace(form)) return null;
        if (string.Equals(form, "M06", StringComparison.OrdinalIgnoreCase)) return CollectiveEvaluationForm.M06;
        if (string.Equals(form, "M07", StringComparison.OrdinalIgnoreCase)) return CollectiveEvaluationForm.M07;
        if (string.Equals(form, "M08", StringComparison.OrdinalIgnoreCase)) return CollectiveEvaluationForm.M08;
        throw new ArgumentException("Hồ sơ tập thể chỉ hỗ trợ M06, M07 hoặc M08.");
    }

    /// <summary>Kiểm tra giới hạn điểm của nhóm tiêu chí.</summary>
    private static void ValidateScore(double score, double maximum, string name)
    {
        if (score < 0 || score > maximum)
            throw new ArgumentException($"{name} phải từ 0 đến {maximum} điểm.");
    }

    /// <summary>Chuyển chuỗi xếp loại sang enum an toàn.</summary>
    private static EvaluationGrade ParseGrade(string? value)
    {
        return Enum.TryParse<EvaluationGrade>(value, true, out var grade)
            ? grade
            : EvaluationGrade.ChuaXepLoai;
    }

    /// <summary>Ánh xạ hồ sơ tập thể sang DTO.</summary>
    private CollectiveEvaluationRecordDto MapCollective(CollectiveEvaluationRecord record)
    {
        return new CollectiveEvaluationRecordDto
        {
            Id = record.Id,
            Version = _unitOfWork.GetVersion(record),
            PeriodId = record.PeriodId,
            Form = record.Form.ToString(),
            PartyCellId = record.PartyCellId,
            PartyCellName = record.PartyCell?.Name,
            DepartmentId = record.DepartmentId,
            DepartmentName = record.Department?.Name,
            HeadId = record.HeadId,
            HeadName = record.Head?.FullName,
            SubjectName = record.SubjectName,
            Strengths = record.Strengths,
            Limitations = record.Limitations,
            Causes = record.Causes,
            PreviousRemediation = record.PreviousRemediation,
            Explanation = record.Explanation,
            Responsibilities = record.Responsibilities,
            RemediationPlan = record.RemediationPlan,
            GeneralCriteriaScore = record.GeneralCriteriaScore,
            TaskCriteriaScore = record.TaskCriteriaScore,
            TotalScore = record.TotalScore,
            SelfProposedGrade = record.SelfProposedGrade.ToString(),
            Status = record.Status.ToString(),
            Items = record.Items.Select(item => new CollectiveEvaluationItemDto
            {
                Id = item.Id,
                ItemOrder = item.ItemOrder,
                Category = item.Category,
                TaskName = item.TaskName,
                PlanOrDirection = item.PlanOrDirection,
                Result = item.Result,
                Limitations = item.Limitations,
                Notes = item.Notes
            }).ToList()
        };
    }

    /// <summary>Ánh xạ biên bản hội nghị sang DTO.</summary>
    private EvaluationMeetingDto MapMeeting(EvaluationMeeting meeting)
    {
        return new EvaluationMeetingDto
        {
            Id = meeting.Id,
            Version = _unitOfWork.GetVersion(meeting),
            PeriodId = meeting.PeriodId,
            PartyCellId = meeting.PartyCellId,
            PartyCellName = meeting.PartyCell?.Name,
            DepartmentId = meeting.DepartmentId,
            DepartmentName = meeting.Department?.Name,
            Stage = meeting.Stage.HasValue ? WorkflowSteps.Code(meeting.Stage.Value) : null,
            FormCode = meeting.FormCode,
            MeetingType = meeting.MeetingType,
            Location = meeting.Location,
            StartedAt = meeting.StartedAt,
            EndedAt = meeting.EndedAt,
            InvitedCount = meeting.InvitedCount,
            PresentCount = meeting.PresentCount,
            AbsentCount = meeting.AbsentCount,
            AbsentReasons = meeting.AbsentReasons,
            ChairId = meeting.ChairId,
            ChairName = meeting.ChairName,
            SecretaryId = meeting.SecretaryId,
            SecretaryName = meeting.SecretaryName,
            MinutesContent = meeting.MinutesContent,
            OutcomeContent = meeting.OutcomeContent,
            VoteCountingContent = meeting.VoteCountingContent,
            VoteSummaries = meeting.VoteSummaries.Select(vote => new EvaluationMeetingVoteSummaryDto
            {
                Id = vote.Id,
                RecordId = vote.RecordId,
                FullName = vote.Record?.Member?.FullName,
                VotesExcellent = vote.VotesExcellent,
                VotesGood = vote.VotesGood,
                VotesSatisfactory = vote.VotesSatisfactory,
                VotesUnsatisfactory = vote.VotesUnsatisfactory,
                InvalidVotes = vote.InvalidVotes,
                Notes = vote.Notes
            }).ToList()
        };
    }
}
