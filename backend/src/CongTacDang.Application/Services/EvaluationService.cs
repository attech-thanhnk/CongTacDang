using System;
using System.Collections.Generic;
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
/// Tra cứu hồ sơ đánh giá theo phạm vi của người dùng (đọc). Công thức tính điểm nằm ở <see cref="EvaluationScoring"/>.
/// </summary>
public class EvaluationService : IEvaluationService
{
    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IOrganizationRepository _orgRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationGuard _guard;

    public EvaluationService(
        IEvaluationWorkflowRepository repo,
        IOrganizationRepository orgRepo,
        ICurrentUserService currentUser,
        IAuthorizationGuard guard)
    {
        _repo = repo;
        _orgRepo = orgRepo;
        _currentUser = currentUser;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<EvaluationRecordDto?> GetMyRecordAsync(Guid periodId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Không xác định được người dùng hiện tại.");
        var record = (await _repo.ListRecordsAsync(periodId, ct)).FirstOrDefault(r => r.MemberId == userId);
        return record == null ? null : await MapAsync(record, ct);
    }

    /// <inheritdoc />
    public async Task<EvaluationRecordDto> GetRecordByIdAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");
        _guard.Ensure(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));
        return await MapAsync(record, ct);
    }

    /// <inheritdoc />
    public async Task<List<EvaluationRecordHistoryDto>> GetRecordHistoryAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await _repo.FindRecordAsync(recordId, ct)
            ?? throw new NotFoundException($"Không tìm thấy hồ sơ đánh giá với Id: {recordId}.");
        _guard.Ensure(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(record));
        var history = await _repo.ListHistoryAsync(recordId, ct);
        return history.Select(EvaluationMapping.ToHistoryDto).ToList();
    }

    /// <inheritdoc />
    public async Task<List<EvaluationRecordDto>> GetRecordsByPeriodAsync(Guid periodId, CancellationToken ct = default)
    {
        var scope = _guard.GetScope(PermissionCodes.EvaluationRead);
        var records = (await _repo.ListRecordsAsync(periodId, ct))
            .Where(record => scope.Matches(record.MemberId, record.DepartmentId, record.PartyCellId))
            .ToList();
        return await MapManyAsync(records, ct);
    }

    /// <inheritdoc />
    public async Task<List<EvaluationRecordDto>> GetRecordsByBranchAsync(Guid periodId, Guid? branchId, CancellationToken ct = default)
    {
        // Danh sách lọc theo phạm vi evaluation.read (Global / Phòng / Chi bộ + hồ sơ của chính mình).
        // Chọn Chi bộ ngoài phạm vi → chỉ còn hồ sơ của chính mình (nếu có), không lộ hồ sơ khác.
        var scope = _guard.GetScope(PermissionCodes.EvaluationRead);
        var records = (await _repo.ListRecordsAsync(periodId, ct))
            .Where(record => branchId is null || branchId == Guid.Empty || record.PartyCellId == branchId)
            .Where(record => scope.Matches(record.MemberId, record.DepartmentId, record.PartyCellId))
            .ToList();
        return await MapManyAsync(records, ct);
    }

    /// <inheritdoc />
    public async Task<List<BranchQuotaCheckDto>> CheckBranchQuotasAsync(Guid periodId, CancellationToken ct = default)
    {
        var scope = _guard.GetScope(PermissionCodes.EvaluationRead);
        var period = await _repo.FindPeriodAsync(periodId, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        var parameters = SafeSettings(period).Parameters;
        var records = (await _repo.ListRecordsAsync(periodId, ct))
            .Where(record => scope.Matches(record.MemberId, record.DepartmentId, record.PartyCellId))
            .ToList();
        var cells = await _orgRepo.GetPartyCellsWithMembersAsync();

        var result = new List<BranchQuotaCheckDto>();
        foreach (var cell in cells)
        {
            var cellRecords = records.Where(r => r.Member?.PartyCellId == cell.Id || r.PartyCellId == cell.Id).ToList();

            // Mức đề xuất trước quyết định: thẩm định → tập thể lãnh đạo → (dữ liệu cũ) Chi bộ.
            int goodOrBetter = cellRecords.Count(r => ProposedGrade(r) is EvaluationGrade.HoanThanhXuatSac or EvaluationGrade.HoanThanhTot);
            int maxAllowed = EvaluationScoring.ExcellentQuota(goodOrBetter, parameters);
            int proposedExcellent = cellRecords.Count(r => ProposedGrade(r) == EvaluationGrade.HoanThanhXuatSac);
            double actualPercent = goodOrBetter > 0
                ? Math.Round(((double)proposedExcellent / goodOrBetter) * 100.0, 1)
                : 0.0;

            result.Add(new BranchQuotaCheckDto
            {
                BranchId = cell.Id,
                BranchName = cell.Name,
                TotalCadres = cellRecords.Count,
                GoodOrBetterCount = goodOrBetter,
                MaxExcellentAllowed = maxAllowed,
                ProposedExcellentCount = proposedExcellent,
                ActualExcellentPercentage = actualPercent,
                IsExceedingQuota = proposedExcellent > maxAllowed
            });
        }

        return result;
    }

    /// <summary>
    /// Mức đề xuất dùng kiểm soát trần (giữ thứ tự ưu tiên cũ: thẩm định trước; sau đó đề xuất tập thể lãnh đạo — trường mới
    /// thay chỗ mức Chi bộ đề xuất cũ; hồ sơ cũ chưa có thì dùng mức Chi bộ cũ).
    /// </summary>
    public static EvaluationGrade ProposedGrade(EvaluationRecord r)
    {
        if (r.AppraisalProposedGrade != EvaluationGrade.ChuaXepLoai)
            return r.AppraisalProposedGrade;
        if (r.CollectiveProposedGrade != EvaluationGrade.ChuaXepLoai)
            return r.CollectiveProposedGrade;
        return r.PartyCellProposedGrade;
    }

    private async Task<EvaluationRecordDto> MapAsync(EvaluationRecord record, CancellationToken ct)
    {
        var evidence = await _repo.GetCurrentEvidenceAsync(record.Tasks.ToList(), ct);
        return EvaluationMapping.ToDto(record, SafeSettings(record.Period), EvaluationMapping.Today(DateTime.UtcNow), evidence);
    }

    private async Task<List<EvaluationRecordDto>> MapManyAsync(List<EvaluationRecord> records, CancellationToken ct)
    {
        var evidence = await _repo.GetCurrentEvidenceAsync(records.SelectMany(r => r.Tasks).ToList(), ct);
        var today = EvaluationMapping.Today(DateTime.UtcNow);
        var settingsCache = new Dictionary<Guid, PeriodSettings>();
        return records.Select(r =>
        {
            if (!settingsCache.TryGetValue(r.PeriodId, out var settings))
                settingsCache[r.PeriodId] = settings = SafeSettings(r.Period);
            return EvaluationMapping.ToDto(r, settings, today, evidence);
        }).ToList();
    }

    private static PeriodSettings SafeSettings(EvaluationPeriod? period)
    {
        if (period == null)
            return PeriodSettings.FullPreset();
        try
        {
            return period.GetSettings();
        }
        catch (FormatException)
        {
            return PeriodSettings.FullPreset();
        }
    }
}
