using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Application.Services;

/// <summary>
/// Dịch vụ nghiệp vụ quản lý quy trình đánh giá, xếp loại cán bộ theo Hướng dẫn 03-HD/TVĐU
/// </summary>
public class EvaluationService : IEvaluationService
{
    private readonly IEvaluationRepository _evaluationRepo;
    private readonly IUserRepository _userRepo;
    private readonly IOrganizationRepository _orgRepo;

    public EvaluationService(
        IEvaluationRepository evaluationRepo,
        IUserRepository userRepo,
        IOrganizationRepository orgRepo)
    {
        _evaluationRepo = evaluationRepo;
        _userRepo = userRepo;
        _orgRepo = orgRepo;
    }

    #region Quản lý Kỳ đánh giá

    /// <summary>Lấy danh sách tất cả các kỳ đánh giá</summary>
    public async Task<List<EvaluationPeriodDto>> GetPeriodsAsync()
    {
        var periods = await _evaluationRepo.GetPeriodsAsync();
        return periods.Select(MapToPeriodDto).ToList();
    }

    /// <summary>Lấy kỳ đánh giá đang hoạt động</summary>
    public async Task<EvaluationPeriodDto?> GetActivePeriodAsync()
    {
        var period = await _evaluationRepo.GetActivePeriodAsync();
        return period == null ? null : MapToPeriodDto(period);
    }

    /// <summary>Tạo mới một kỳ đánh giá</summary>
    public async Task<EvaluationPeriodDto> CreatePeriodAsync(CreatePeriodDto dto)
    {
        var period = new EvaluationPeriod
        {
            Id = Guid.NewGuid(),
            Year = dto.Year,
            Quarter = (EvaluationQuarter)dto.Quarter,
            Name = dto.Name,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = PeriodStatus.TaskRegistration,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        await _evaluationRepo.AddPeriodAsync(period);
        return MapToPeriodDto(period);
    }

    /// <summary>Kích hoạt một kỳ đánh giá làm kỳ hiện hành</summary>
    public async Task<EvaluationPeriodDto> SetActivePeriodAsync(Guid periodId)
    {
        var periods = await _evaluationRepo.GetPeriodsAsync();
        EvaluationPeriod? targetPeriod = null;

        foreach (var p in periods)
        {
            if (p.Id == periodId)
            {
                p.IsActive = true;
                targetPeriod = p;
            }
            else
            {
                p.IsActive = false;
            }
            await _evaluationRepo.UpdatePeriodAsync(p);
        }

        if (targetPeriod == null)
            throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");

        return MapToPeriodDto(targetPeriod);
    }

    /// <summary>Cập nhật trạng thái tiến trình của kỳ đánh giá</summary>
    public async Task<EvaluationPeriodDto> UpdatePeriodStatusAsync(Guid periodId, PeriodStatus status)
    {
        var period = await _evaluationRepo.GetPeriodByIdAsync(periodId);
        if (period == null)
            throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}");

        period.Status = status;
        await _evaluationRepo.UpdatePeriodAsync(period);
        return MapToPeriodDto(period);
    }

    #endregion

    #region Tra cứu Hồ sơ đánh giá

    /// <summary>Lấy hồ sơ đánh giá của cán bộ theo kỳ và mã cán bộ</summary>
    public async Task<EvaluationRecordDto?> GetUserEvaluationRecordAsync(Guid periodId, Guid memberId)
    {
        var record = await _evaluationRepo.GetRecordAsync(periodId, memberId);
        return record == null ? null : MapToRecordDto(record);
    }

    /// <summary>Lấy chi tiết hồ sơ đánh giá theo Id</summary>
    public async Task<EvaluationRecordDto> GetRecordByIdAsync(Guid recordId)
    {
        var record = await _evaluationRepo.GetRecordByIdAsync(recordId);
        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}");

        return MapToRecordDto(record);
    }

    /// <summary>Lấy toàn bộ hồ sơ đánh giá trong một kỳ</summary>
    public async Task<List<EvaluationRecordDto>> GetRecordsByPeriodAsync(Guid periodId)
    {
        var records = await _evaluationRepo.GetRecordsByPeriodAsync(periodId);
        return records.Select(MapToRecordDto).ToList();
    }

    /// <summary>Lấy danh sách hồ sơ đánh giá của một Chi bộ trong kỳ (hoặc Chi bộ của người dùng)</summary>
    public async Task<List<EvaluationRecordDto>> GetRecordsByBranchAsync(Guid periodId, Guid? branchId = null, Guid? currentUserId = null)
    {
        Guid targetBranchId = Guid.Empty;
        if (branchId.HasValue && branchId.Value != Guid.Empty)
        {
            targetBranchId = branchId.Value;
        }
        else if (currentUserId.HasValue)
        {
            var user = await _userRepo.GetByIdAsync(currentUserId.Value);
            if (user?.PartyCellId.HasValue == true)
            {
                targetBranchId = user.PartyCellId.Value;
            }
        }

        if (targetBranchId == Guid.Empty)
        {
            var allRecords = await _evaluationRepo.GetRecordsByPeriodAsync(periodId);
            return allRecords.Select(MapToRecordDto).ToList();
        }

        var records = await _evaluationRepo.GetRecordsByBranchAsync(periodId, targetBranchId);
        return records.Select(MapToRecordDto).ToList();
    }

    #endregion

    #region Quy trình 5 bước theo Hướng dẫn 03-HD/TVĐU

    /// <summary>Bước 1: Cán bộ đăng ký 3-7 nhiệm vụ trọng tâm quý (Mẫu 01 - Tổng trọng số = 70.0)</summary>
    public async Task<EvaluationRecordDto> RegisterTasksAsync(Guid memberId, RegisterTasksRequestDto dto)
    {
        if (dto.Tasks == null || dto.Tasks.Count < 3 || dto.Tasks.Count > 7)
        {
            throw new ArgumentException("Số lượng nhiệm vụ đăng ký phải từ 3 đến 7 nhiệm vụ theo Hướng dẫn 03-HD/TVĐU.");
        }

        double totalWeight = Math.Round(dto.Tasks.Sum(t => t.Weight), 2);
        if (Math.Abs(totalWeight - 70.0) > 0.05)
        {
            throw new ArgumentException($"Tổng trọng số của các nhiệm vụ phải bằng đúng 70.0 điểm. Hiện tại là: {totalWeight} điểm.");
        }

        var member = await _userRepo.GetByIdAsync(memberId);
        if (member == null)
            throw new KeyNotFoundException($"Không tìm thấy thông tin cán bộ với Id: {memberId}");

        var period = await _evaluationRepo.GetPeriodByIdAsync(dto.PeriodId);
        if (period == null)
            throw new KeyNotFoundException($"Không tìm thấy kỳ đánh giá với Id: {dto.PeriodId}");

        var record = await _evaluationRepo.GetRecordAsync(dto.PeriodId, memberId);
        if (record == null)
        {
            record = new EvaluationRecord
            {
                Id = Guid.NewGuid(),
                PeriodId = dto.PeriodId,
                MemberId = memberId,
                PartyCellId = member.PartyCellId,
                DepartmentId = member.DepartmentId,
                JobGroup = member.JobGroup,
                Status = RecordStatus.TasksSubmitted,
                UpdatedAt = DateTime.UtcNow
            };
            await _evaluationRepo.AddRecordAsync(record);
        }
        else
        {
            record.Status = RecordStatus.TasksSubmitted;
            record.UpdatedAt = DateTime.UtcNow;
            await _evaluationRepo.UpdateRecordAsync(record);
        }

        // Tạo danh sách nhiệm vụ
        int order = 1;
        var newTasks = dto.Tasks.Select(t => new EvaluationTask
        {
            Id = Guid.NewGuid(),
            RecordId = record.Id,
            TaskOrder = order++,
            TaskName = t.TaskName,
            TargetOutput = t.TargetOutput,
            Weight = t.Weight,
            Deadline = t.Deadline,
            AttachmentId = t.AttachmentId,
            CriteriaA_Ratio = 1.0,
            CriteriaB_Ratio = 1.0,
            CriteriaC_Ratio = 1.0,
            CriteriaD_Ratio = 1.0,
            SelfScore = t.Weight // Khởi tạo điểm trần bằng trọng số
        }).ToList();

        await _evaluationRepo.ReplaceTasksAsync(record.Id, newTasks);

        var updatedRecord = await _evaluationRepo.GetRecordByIdAsync(record.Id);
        return MapToRecordDto(updatedRecord!);
    }

    /// <summary>Bước 2: Cán bộ tự chấm điểm Tiêu chí chung (Mẫu 09) và Sản phẩm chuyên môn (Mẫu 02)</summary>
    public async Task<EvaluationRecordDto> SubmitSelfScoreAsync(Guid memberId, SubmitSelfScoreRequestDto dto)
    {
        var record = await _evaluationRepo.GetRecordByIdAsync(dto.RecordId);
        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {dto.RecordId}");

        if (record.MemberId != memberId)
            throw new UnauthorizedAccessException("Bạn không có quyền tự chấm điểm cho hồ sơ của cán bộ khác.");

        if (dto.GeneralScores == null || dto.GeneralScores.Length != 6)
            throw new ArgumentException("Điểm tiêu chí chung phải bao gồm đúng 6 tiêu chí (T1 đến T6).");

        for (int i = 0; i < 6; i++)
        {
            if (dto.GeneralScores[i] < 0 || dto.GeneralScores[i] > 5.0)
                throw new ArgumentException($"Điểm tiêu chí T{i + 1} phải từ 0.0 đến 5.0 điểm.");
        }

        record.GeneralScoreT1 = dto.GeneralScores[0];
        record.GeneralScoreT2 = dto.GeneralScores[1];
        record.GeneralScoreT3 = dto.GeneralScores[2];
        record.GeneralScoreT4 = dto.GeneralScores[3];
        record.GeneralScoreT5 = dto.GeneralScores[4];
        record.GeneralScoreT6 = dto.GeneralScores[5];
        record.GeneralCriteriaScore = Math.Round(dto.GeneralScores.Sum(), 2);

        // Chấm điểm từng nhiệm vụ chuyên môn theo tỷ trọng Khung chức danh
        var tasks = await _evaluationRepo.GetTasksByRecordIdAsync(record.Id);
        var (wA, wB, wC, wD) = GetJobGroupWeights(record.JobGroup);

        double totalTaskScore = 0.0;
        foreach (var task in tasks)
        {
            var inputScore = dto.TaskScores?.FirstOrDefault(s => s.TaskId == task.Id);
            if (inputScore != null)
            {
                task.CriteriaA_Ratio = Math.Clamp(inputScore.CriteriaA_Ratio, 0.0, 1.0);
                task.CriteriaB_Ratio = Math.Clamp(inputScore.CriteriaB_Ratio, 0.0, 1.0);
                task.CriteriaC_Ratio = Math.Clamp(inputScore.CriteriaC_Ratio, 0.0, 1.0);
                task.CriteriaD_Ratio = Math.Clamp(inputScore.CriteriaD_Ratio, 0.0, 1.0);
                task.IsExceedStandard = inputScore.IsExceedStandard;
                task.AttachmentId = inputScore.AttachmentId;

                double weightedRatio = (task.CriteriaA_Ratio * wA) +
                                      (task.CriteriaB_Ratio * wB) +
                                      (task.CriteriaC_Ratio * wC) +
                                      (task.CriteriaD_Ratio * wD);

                task.SelfScore = Math.Round(task.Weight * weightedRatio, 2);
            }
            totalTaskScore += task.SelfScore;
        }

        record.TasksScore = Math.Round(totalTaskScore, 2);
        record.TotalSelfScore = Math.Round(record.GeneralCriteriaScore + record.TasksScore, 2);

        if (Enum.TryParse<EvaluationGrade>(dto.SelfProposedGrade, true, out var proposedGrade))
        {
            record.SelfProposedGrade = proposedGrade;
        }
        else
        {
            record.SelfProposedGrade = CalculateGradeFromScore(record.TotalSelfScore);
        }

        record.Status = RecordStatus.SelfEvaluated;
        record.UpdatedAt = DateTime.UtcNow;

        await _evaluationRepo.UpdateRecordAsync(record);

        var updatedRecord = await _evaluationRepo.GetRecordByIdAsync(record.Id);
        return MapToRecordDto(updatedRecord!);
    }

    /// <summary>Bước 3: Chi bộ nhận xét và bỏ phiếu đánh giá (Mẫu 10, 11, 13)</summary>
    public async Task<EvaluationRecordDto> SubmitBranchReviewAsync(Guid reviewerId, SubmitBranchReviewRequestDto dto)
    {
        var record = await _evaluationRepo.GetRecordByIdAsync(dto.RecordId);
        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {dto.RecordId}");

        record.PartyCellComment = dto.Comment;
        if (Enum.TryParse<EvaluationGrade>(dto.ProposedGrade, true, out var branchGrade))
        {
            record.PartyCellProposedGrade = branchGrade;
        }

        record.VotesExcellent = dto.VotesExcellent;
        record.VotesGood = dto.VotesGood;
        record.VotesSatisfactory = dto.VotesSatisfactory;
        record.VotesUnsatisfactory = dto.VotesUnsatisfactory;
        record.TotalVoters = dto.TotalVoters;

        record.Status = RecordStatus.Voted;
        record.UpdatedAt = DateTime.UtcNow;

        await _evaluationRepo.UpdateRecordAsync(record);

        var updatedRecord = await _evaluationRepo.GetRecordByIdAsync(record.Id);
        return MapToRecordDto(updatedRecord!);
    }

    /// <summary>Bước 4: Tổ thẩm định thẩm tra và chấm điểm (Mẫu 03)</summary>
    public async Task<EvaluationRecordDto> SubmitAppraisalAsync(Guid appraiserId, SubmitAppraisalRequestDto dto)
    {
        var record = await _evaluationRepo.GetRecordByIdAsync(dto.RecordId);
        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {dto.RecordId}");

        record.AppraisalScore = dto.AppraisalScore;
        record.AppraisalComment = dto.Comment;
        if (Enum.TryParse<EvaluationGrade>(dto.ProposedGrade, true, out var appraisalGrade))
        {
            record.AppraisalProposedGrade = appraisalGrade;
        }

        record.Status = RecordStatus.Reviewed;
        record.UpdatedAt = DateTime.UtcNow;

        await _evaluationRepo.UpdateRecordAsync(record);

        var updatedRecord = await _evaluationRepo.GetRecordByIdAsync(record.Id);
        return MapToRecordDto(updatedRecord!);
    }

    /// <summary>Bước 4b: Kiểm tra tỷ lệ trần 20% Hoàn thành xuất sắc nhiệm vụ của các Chi bộ (Mẫu 15)</summary>
    public async Task<List<BranchQuotaCheckDto>> CheckBranchQuotasAsync(Guid periodId)
    {
        var records = await _evaluationRepo.GetRecordsByPeriodAsync(periodId);
        var cells = await _orgRepo.GetPartyCellsWithMembersAsync();

        var result = new List<BranchQuotaCheckDto>();

        foreach (var cell in cells)
        {
            var cellRecords = records.Where(r => r.Member?.PartyCellId == cell.Id || r.PartyCellId == cell.Id).ToList();
            int total = cellRecords.Count;

            // Số cán bộ được đề xuất hoàn thành tốt trở lên (theo thẩm định hoặc chi bộ)
            int goodOrBetter = cellRecords.Count(r =>
            {
                var grade = r.AppraisalProposedGrade != EvaluationGrade.ChuaXepLoai
                    ? r.AppraisalProposedGrade
                    : r.PartyCellProposedGrade;

                return grade == EvaluationGrade.HoanThanhXuatSac || grade == EvaluationGrade.HoanThanhTot;
            });

            // Trần 20% trên số cán bộ Hoàn thành tốt trở lên
            int maxAllowed = (int)Math.Floor(goodOrBetter * 0.20);

            int proposedExcellent = cellRecords.Count(r =>
            {
                var grade = r.AppraisalProposedGrade != EvaluationGrade.ChuaXepLoai
                    ? r.AppraisalProposedGrade
                    : r.PartyCellProposedGrade;

                return grade == EvaluationGrade.HoanThanhXuatSac;
            });

            double actualPercent = goodOrBetter > 0
                ? Math.Round(((double)proposedExcellent / goodOrBetter) * 100.0, 1)
                : 0.0;

            result.Add(new BranchQuotaCheckDto
            {
                BranchId = cell.Id,
                BranchName = cell.Name,
                TotalCadres = total,
                GoodOrBetterCount = goodOrBetter,
                MaxExcellentAllowed = maxAllowed,
                ProposedExcellentCount = proposedExcellent,
                ActualExcellentPercentage = actualPercent,
                IsExceedingQuota = proposedExcellent > maxAllowed
            });
        }

        return result;
    }

    /// <summary>Bước 5: Ban Thường vụ phê duyệt và quyết định xếp loại chính thức (Mẫu 14, 16)</summary>
    public async Task<EvaluationRecordDto> ApproveFinalGradeAsync(Guid approverId, ApproveFinalGradeRequestDto dto)
    {
        var record = await _evaluationRepo.GetRecordByIdAsync(dto.RecordId);
        if (record == null)
            throw new KeyNotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {dto.RecordId}");

        record.FinalScore = dto.FinalScore;
        if (Enum.TryParse<EvaluationGrade>(dto.FinalGrade, true, out var finalGrade))
        {
            record.FinalGrade = finalGrade;
        }

        record.Status = RecordStatus.Approved;
        record.UpdatedAt = DateTime.UtcNow;

        await _evaluationRepo.UpdateRecordAsync(record);

        var updatedRecord = await _evaluationRepo.GetRecordByIdAsync(record.Id);
        return MapToRecordDto(updatedRecord!);
    }

    #endregion

    #region Helper & Mapping Functions

    /// <summary>Lấy trọng số 4 nhóm tiêu chí A-B-C-D theo Khung chức danh</summary>
    private static (double wa, double wb, double wc, double wd) GetJobGroupWeights(JobGroup group)
    {
        return group switch
        {
            JobGroup.Khung1_QuanLyDangDoanThe => (0.25, 0.35, 0.20, 0.20),
            JobGroup.Khung2_AnToanKyThuat => (0.15, 0.50, 0.15, 0.20),
            JobGroup.Khung3_DuAnDauTu => (0.20, 0.30, 0.35, 0.15),
            JobGroup.Khung4_KhcnChuyenDoiSo => (0.15, 0.30, 0.20, 0.35),
            _ => (0.25, 0.25, 0.25, 0.25)
        };
    }

    /// <summary>Tự động xác định mức xếp loại gợi ý từ tổng điểm</summary>
    private static EvaluationGrade CalculateGradeFromScore(double score)
    {
        if (score >= 90.0) return EvaluationGrade.HoanThanhXuatSac;
        if (score >= 70.0) return EvaluationGrade.HoanThanhTot;
        if (score >= 50.0) return EvaluationGrade.HoanThanh;
        return EvaluationGrade.KhongHoanThanh;
    }

    private static EvaluationPeriodDto MapToPeriodDto(EvaluationPeriod p)
    {
        return new EvaluationPeriodDto
        {
            Id = p.Id,
            Year = p.Year,
            Quarter = (int)p.Quarter,
            Name = p.Name,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            Status = p.Status.ToString(),
            StatusDisplayName = GetPeriodStatusDisplayName(p.Status),
            IsActive = p.IsActive
        };
    }

    private static EvaluationRecordDto MapToRecordDto(EvaluationRecord r)
    {
        return new EvaluationRecordDto
        {
            Id = r.Id,
            PeriodId = r.PeriodId,
            PeriodName = r.Period?.Name ?? string.Empty,
            MemberId = r.MemberId,
            FullName = r.Member?.FullName ?? string.Empty,
            PartyCardNumber = r.Member?.PartyCardNumber,
            PartyRole = r.Member?.PartyRole.ToString() ?? string.Empty,
            PositionTitle = r.Member?.PositionTitle ?? string.Empty,
            PartyCellName = r.Member?.PartyCell?.Name ?? r.PartyCell?.Name,
            PartyCellId = r.Member?.PartyCellId ?? r.PartyCellId,
            DepartmentName = r.Member?.Department?.Name ?? r.Department?.Name,
            JobGroup = r.JobGroup.ToString(),
            GeneralScores = new[]
            {
                r.GeneralScoreT1,
                r.GeneralScoreT2,
                r.GeneralScoreT3,
                r.GeneralScoreT4,
                r.GeneralScoreT5,
                r.GeneralScoreT6
            },
            GeneralCriteriaScore = r.GeneralCriteriaScore,
            TasksScore = r.TasksScore,
            TotalSelfScore = r.TotalSelfScore,
            SelfProposedGrade = r.SelfProposedGrade.ToString(),
            PartyCellComment = r.PartyCellComment,
            PartyCellProposedGrade = r.PartyCellProposedGrade.ToString(),
            VotesExcellent = r.VotesExcellent,
            VotesGood = r.VotesGood,
            VotesSatisfactory = r.VotesSatisfactory,
            VotesUnsatisfactory = r.VotesUnsatisfactory,
            TotalVoters = r.TotalVoters,
            AppraisalScore = r.AppraisalScore,
            AppraisalComment = r.AppraisalComment,
            AppraisalProposedGrade = r.AppraisalProposedGrade.ToString(),
            FinalScore = r.FinalScore,
            FinalGrade = r.FinalGrade.ToString(),
            Status = r.Status.ToString(),
            StatusDisplayName = GetRecordStatusDisplayName(r.Status),
            Tasks = r.Tasks?.Select(MapToTaskDto).ToList() ?? new List<EvaluationTaskDto>()
        };
    }

    private static EvaluationTaskDto MapToTaskDto(EvaluationTask t)
    {
        return new EvaluationTaskDto
        {
            Id = t.Id,
            RecordId = t.RecordId,
            TaskOrder = t.TaskOrder,
            TaskName = t.TaskName,
            TargetOutput = t.TargetOutput,
            Weight = t.Weight,
            Deadline = t.Deadline,
            CriteriaA_Ratio = t.CriteriaA_Ratio,
            CriteriaB_Ratio = t.CriteriaB_Ratio,
            CriteriaC_Ratio = t.CriteriaC_Ratio,
            CriteriaD_Ratio = t.CriteriaD_Ratio,
            SelfScore = t.SelfScore,
            SupervisorScore = t.SupervisorScore,
            IsExceedStandard = t.IsExceedStandard,
            AttachmentId = t.AttachmentId,
            AttachmentFileName = t.Attachment?.FileName ?? t.Attachment?.OriginalFileName,
            AttachmentOriginalName = t.Attachment?.OriginalFileName ?? t.Attachment?.FileName
        };
    }

    private static string GetPeriodStatusDisplayName(PeriodStatus status)
    {
        return status switch
        {
            PeriodStatus.Draft => "Dự thảo",
            PeriodStatus.TaskRegistration => "Đăng ký nhiệm vụ (Mẫu 01)",
            PeriodStatus.SelfEvaluation => "Tự đánh giá (Mẫu 02 & 09)",
            PeriodStatus.BranchReview => "Hội nghị Chi bộ (Mẫu 10 & 13)",
            PeriodStatus.Appraisal => "Thẩm định & Kiểm soát trần 20% (Mẫu 03 & 15)",
            PeriodStatus.Completed => "Hoàn thành & Chuẩn y (Mẫu 14 & 16)",
            _ => status.ToString()
        };
    }

    private static string GetRecordStatusDisplayName(RecordStatus status)
    {
        return status switch
        {
            RecordStatus.Draft => "Bản nháp",
            RecordStatus.TasksSubmitted => "Đã gửi đăng ký nhiệm vụ",
            RecordStatus.TasksApproved => "Nhiệm vụ đã được duyệt",
            RecordStatus.SelfEvaluated => "Đã tự chấm điểm",
            RecordStatus.Voted => "Chi bộ đã bỏ phiếu",
            RecordStatus.Reviewed => "Đã thẩm định hồ sơ",
            RecordStatus.Approved => "Đã phê duyệt chính thức",
            RecordStatus.Published => "Đã công bố kết quả",
            _ => status.ToString()
        };
    }

    #endregion
}
