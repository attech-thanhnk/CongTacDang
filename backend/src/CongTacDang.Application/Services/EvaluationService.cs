using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Reports;
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
    private readonly IPositionRepository _positions;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuthorizationGuard _guard;

    public EvaluationService(
        IEvaluationWorkflowRepository repo,
        IPositionRepository positions,
        ICurrentUserService currentUser,
        IAuthorizationGuard guard)
    {
        _repo = repo;
        _positions = positions;
        _currentUser = currentUser;
        _guard = guard;
    }

    /// <inheritdoc />
    public async Task<EvaluationRecordDto?> GetMyRecordAsync(Guid periodId, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Không xác định được người dùng hiện tại.");
        if (periodId == Guid.Empty)
        {
            // Không truyền kỳ → kỳ đang hoạt động (Đang mở/Khóa dữ liệu mới nhất).
            periodId = EvaluationMapping.ActivePeriod(await _repo.ListPeriodsAsync(ct))?.Id
                ?? throw new NotFoundException("Hiện không có kỳ đánh giá đang mở hoặc đang khóa dữ liệu. Hãy chọn kỳ đánh giá cần xem (tham số periodId).");
        }
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
        if (periodId == Guid.Empty)
            throw new ValidationException("Hãy chọn kỳ đánh giá (tham số periodId) để xem danh sách hồ sơ.");
        var scope = _guard.GetScope(PermissionCodes.EvaluationRead);
        var records = (await _repo.ListRecordsAsync(periodId, ct))
            .Where(record => scope.Matches(record.MemberId, record.DepartmentId, record.PartyCellId))
            .ToList();
        return await MapManyAsync(records, ct);
    }

    /// <inheritdoc />
    public async Task<List<EvaluationRecordDto>> GetRecordsByBranchAsync(Guid periodId, Guid? branchId, CancellationToken ct = default)
    {
        // Danh sách lọc theo phạm vi evaluation.read (Global / đơn vị chính quyền / tổ chức Đảng gồm cấp dưới + hồ sơ của chính mình).
        // Chọn Chi bộ ngoài phạm vi → chỉ còn hồ sơ của chính mình (nếu có), không lộ hồ sơ khác.
        var scope = _guard.GetScope(PermissionCodes.EvaluationRead);
        var records = (await _repo.ListRecordsAsync(periodId, ct))
            .Where(record => branchId is null || branchId == Guid.Empty || record.PartyCellId == branchId)
            .Where(record => scope.Matches(record.MemberId, record.DepartmentId, record.PartyCellId))
            .ToList();
        return await MapManyAsync(records, ct);
    }

    /// <inheritdoc />
    public async Task<ExcellentQuotaCheckDto> CheckExcellentQuotasAsync(Guid periodId, Guid? partyCellId, CancellationToken ct = default)
    {
        if (periodId == Guid.Empty)
            throw new ValidationException("Hãy chọn kỳ đánh giá (tham số periodId).");
        var scope = _guard.GetScope(PermissionCodes.EvaluationRead);
        var period = await _repo.FindPeriodAsync(periodId, ct)
            ?? throw new NotFoundException($"Không tìm thấy kỳ đánh giá với Id: {periodId}.");
        var rule = EvaluationMapping.SafeCriteria(period)?.Content.Parameters.ExcellentQuota ?? new ExcellentQuotaRule();
        var cellId = partyCellId is { } id && id != Guid.Empty ? id : (Guid?)null;

        // Đơn vị của hồ sơ lấy theo ảnh chụp trên hồ sơ (không theo nơi sinh hoạt hiện tại của cán bộ).
        var records = (await _repo.ListRecordsAsync(periodId, ct))
            .Where(r => scope.Matches(r.MemberId, r.DepartmentId, r.PartyCellId))
            .ToList();
        if (cellId is { } selected)
        {
            if (!await _repo.PartyCellExistsAsync(selected, ct))
                throw new NotFoundException("Không tìm thấy tổ chức Đảng đã chọn.");
            var cellPaths = (await _positions.GetOrgPathsAsync(ct)).PartyCells;
            records = records
                .Where(r => r.PartyCellId is { } c && (c == selected || OrgTree.IsSelfOrDescendant(cellPaths.GetValueOrDefault(c), selected)))
                .ToList();
        }

        var at = ExcellentQuotaCalculator.PositionDate(period, DateTime.UtcNow);
        var held = await _positions.GetHeldPositionsAsync(records.Select(r => r.MemberId).Distinct().ToList(), at, ct);
        var groups = ExcellentQuotaCalculator.Compute(
            records.Select(r => ExcellentQuotaCalculator.EntryFor(r, held.GetValueOrDefault(r.MemberId))),
            rule.EffectiveRatio);

        return new ExcellentQuotaCheckDto
        {
            PeriodId = periodId,
            Ratio = rule.EffectiveRatio,
            PartyCellId = cellId,
            IsFullScope = scope.IsGlobal && cellId == null,
            ExceedingGroupCount = groups.Count(g => g.IsExceeding),
            Groups = groups.Select(ExcellentQuotaCalculator.ToDto).ToList()
        };
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
        var settingsCache = new Dictionary<Guid, (PeriodSettings Settings, CriteriaSnapshot? Criteria)>();
        return records.Select(r =>
        {
            if (!settingsCache.TryGetValue(r.PeriodId, out var cached))
                settingsCache[r.PeriodId] = cached = (SafeSettings(r.Period), EvaluationMapping.SafeCriteria(r.Period));
            return EvaluationMapping.ToDto(r, cached.Settings, today, evidence, cached.Criteria);
        }).ToList();
    }

    private static PeriodSettings SafeSettings(EvaluationPeriod? period)
    {
        if (period == null)
            return PeriodSettings.Default();
        try
        {
            return period.GetSettings();
        }
        catch (FormatException)
        {
            return PeriodSettings.Default();
        }
    }
}
