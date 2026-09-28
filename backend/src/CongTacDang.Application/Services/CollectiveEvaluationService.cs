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

    public CollectiveEvaluationService(
        ICollectiveEvaluationRepository collectiveRepo,
        IEvaluationMeetingRepository meetingRepo,
        IEvaluationRepository evaluationRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork)
    {
        _collectiveRepo = collectiveRepo;
        _meetingRepo = meetingRepo;
        _evaluationRepo = evaluationRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
    }

    /// <summary>Lấy hồ sơ tập thể theo kỳ, biểu mẫu và phạm vi người dùng.</summary>
    public async Task<List<CollectiveEvaluationRecordDto>> GetCollectiveRecordsAsync(Guid periodId, Guid requesterId, string? form = null)
    {
        var requester = await GetUserAsync(requesterId);
        EnsureCanReadCollective(requester);
        var parsedForm = ParseCollectiveForm(form);
        var records = await _collectiveRepo.GetByPeriodAsync(periodId, parsedForm);
        return records
            .Where(record => CanAccessOrganization(requester, record.PartyCellId, record.DepartmentId))
            .Select(MapCollective)
            .ToList();
    }

    /// <summary>Lấy một hồ sơ tập thể và kiểm tra phạm vi tổ chức.</summary>
    public async Task<CollectiveEvaluationRecordDto> GetCollectiveRecordAsync(Guid id, Guid requesterId)
    {
        var requester = await GetUserAsync(requesterId);
        EnsureCanReadCollective(requester);
        var record = await _collectiveRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ tập thể với Id: {id}");

        EnsureCanAccessOrganization(requester, record.PartyCellId, record.DepartmentId);
        return MapCollective(record);
    }

    /// <summary>Tạo hồ sơ Mẫu 06, 07 hoặc 08 sau khi kiểm tra phạm vi tổ chức.</summary>
    public async Task<CollectiveEvaluationRecordDto> CreateCollectiveRecordAsync(Guid requesterId, SaveCollectiveEvaluationRequestDto dto)
    {
        var requester = await GetUserAsync(requesterId);
        EnsureCanWriteCollective(requester);
        var form = ParseCollectiveForm(dto.Form)
            ?? throw new ArgumentException("Hồ sơ tập thể phải có mã M06, M07 hoặc M08.");
        if (string.IsNullOrWhiteSpace(dto.SubjectName))
            throw new ArgumentException("Tên tập thể hoặc lĩnh vực đánh giá không được để trống.");

        EnsureCanAccessOrganization(requester, dto.PartyCellId, dto.DepartmentId);
        ValidateScore(dto.GeneralCriteriaScore, 30.0, "Điểm nhóm tiêu chí chung");
        ValidateScore(dto.TaskCriteriaScore, 70.0, "Điểm nhóm kết quả thực hiện nhiệm vụ");

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
            Status = RecordStatus.SelfEvaluated,
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
        var requester = await GetUserAsync(requesterId);
        EnsureCanReadMeeting(requester);
        if (!IsElevated(requester))
            partyCellId = requester.PartyCellId;

        var meetings = await _meetingRepo.GetByPeriodAsync(periodId, partyCellId);
        return meetings.Select(MapMeeting).ToList();
    }

    /// <summary>Lấy biên bản hội nghị và kiểm tra phạm vi Chi bộ.</summary>
    public async Task<EvaluationMeetingDto> GetMeetingAsync(Guid id, Guid requesterId)
    {
        var requester = await GetUserAsync(requesterId);
        EnsureCanReadMeeting(requester);
        var meeting = await _meetingRepo.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy biên bản hội nghị với Id: {id}");

        if (!IsElevated(requester) && meeting.PartyCellId != requester.PartyCellId)
            throw new ForbiddenException("Bạn không có quyền xem biên bản của Chi bộ khác.");

        return MapMeeting(meeting);
    }

    /// <summary>Tạo biên bản Mẫu 12 hoặc Mẫu 13, chỉ lưu tổng hợp phiếu không định danh.</summary>
    public async Task<EvaluationMeetingDto> CreateMeetingAsync(Guid requesterId, SaveEvaluationMeetingRequestDto dto)
    {
        var requester = await GetUserAsync(requesterId);
        EnsureCanWriteMeeting(requester);
        if (dto.PartyCellId == null || dto.PartyCellId == Guid.Empty)
            throw new ArgumentException("Biên bản hội nghị phải gắn với một Chi bộ.");
        if (!IsElevated(requester) && requester.PartyCellId != dto.PartyCellId)
            throw new ForbiddenException("Bạn chỉ được lập biên bản cho Chi bộ của mình.");
        if (dto.FormCode is not ("M12" or "M13"))
            throw new ArgumentException("Biên bản chỉ hỗ trợ M12 hoặc M13.");
        if (dto.FormCode == "M13" && dto.VoteSummaries.Count == 0)
            throw new ArgumentException("Mẫu 13 phải có tổng hợp phiếu theo từng hồ sơ cán bộ.");
        if (dto.InvitedCount < 0 || dto.PresentCount < 0 || dto.AbsentCount < 0 || dto.PresentCount > dto.InvitedCount)
            throw new ArgumentException("Số lượng triệu tập, có mặt, vắng mặt không hợp lệ.");

        var meeting = new EvaluationMeeting
        {
            PeriodId = dto.PeriodId,
            PartyCellId = dto.PartyCellId,
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

                var record = await _evaluationRepo.GetRecordByIdAsync(vote.RecordId)
                    ?? throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {vote.RecordId}");
                if (record.PeriodId != dto.PeriodId || record.PartyCellId != dto.PartyCellId)
                    throw new ForbiddenException("Biên bản chỉ được chứa hồ sơ cùng kỳ và cùng Chi bộ.");

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

    /// <summary>Lấy người dùng kèm role và permission.</summary>
    private async Task<PartyMemberProfile> GetUserAsync(Guid userId)
    {
        return await _userRepo.GetWithRolesAndPermissionsByIdAsync(userId)
            ?? throw new ForbiddenException("Không tìm thấy hồ sơ người dùng hiện tại.");
    }

    /// <summary>Kiểm tra người dùng có quyền nghiệp vụ cấp cao.</summary>
    private static bool IsElevated(PartyMemberProfile user)
    {
        return user.Roles.Any(role => role.Code == AppRoles.QUAN_TRI_HE_THONG)
            || HasPermission(user, AppPermissions.EvaluationsAppraise)
            || HasPermission(user, AppPermissions.EvaluationsApprove);
    }

    /// <summary>Kiểm tra người dùng có quyền nguyên tử.</summary>
    private static bool HasPermission(PartyMemberProfile user, string permission)
    {
        return user.Roles.SelectMany(role => role.Permissions).Any(x => x.Code == permission);
    }

    /// <summary>Kiểm tra quyền xem hồ sơ tập thể.</summary>
    private static void EnsureCanReadCollective(PartyMemberProfile user)
    {
        if (!IsElevated(user) && !HasPermission(user, AppPermissions.EvaluationsBranchVote))
            throw new ForbiddenException("Bạn không có quyền xem hồ sơ tập thể.");
    }

    /// <summary>Kiểm tra quyền tạo hồ sơ tập thể.</summary>
    private static void EnsureCanWriteCollective(PartyMemberProfile user)
    {
        if (!HasPermission(user, AppPermissions.EvaluationsBranchVote)
            && !HasPermission(user, AppPermissions.EvaluationsAppraise)
            && !HasPermission(user, AppPermissions.EvaluationsApprove))
            throw new ForbiddenException("Quản trị hệ thống chỉ được quản lý kỹ thuật, không được lập hồ sơ đánh giá.");
    }

    /// <summary>Kiểm tra quyền xem biên bản hội nghị.</summary>
    private static void EnsureCanReadMeeting(PartyMemberProfile user)
    {
        if (!IsElevated(user) && !HasPermission(user, AppPermissions.EvaluationsBranchVote))
            throw new ForbiddenException("Bạn không có quyền xem biên bản hội nghị.");
    }

    /// <summary>Kiểm tra quyền tạo biên bản hội nghị.</summary>
    private static void EnsureCanWriteMeeting(PartyMemberProfile user)
    {
        if (!HasPermission(user, AppPermissions.EvaluationsBranchVote)
            && !HasPermission(user, AppPermissions.EvaluationsAppraise)
            && !HasPermission(user, AppPermissions.EvaluationsApprove))
            throw new ForbiddenException("Quản trị hệ thống chỉ được quản lý kỹ thuật, không được lập biên bản đánh giá.");
    }

    /// <summary>Giới hạn hồ sơ theo Chi bộ hoặc phòng ban của người dùng không đặc quyền.</summary>
    private static bool CanAccessOrganization(PartyMemberProfile user, Guid? partyCellId, Guid? departmentId)
    {
        if (IsElevated(user))
            return true;
        return (partyCellId.HasValue && partyCellId == user.PartyCellId)
            || (departmentId.HasValue && departmentId == user.DepartmentId);
    }

    /// <summary>Kiểm tra và báo lỗi nếu hồ sơ nằm ngoài phạm vi tổ chức của người dùng.</summary>
    private static void EnsureCanAccessOrganization(PartyMemberProfile user, Guid? partyCellId, Guid? departmentId)
    {
        if (!CanAccessOrganization(user, partyCellId, departmentId))
            throw new ForbiddenException("Bạn không có quyền thao tác hồ sơ của tổ chức khác.");
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
