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

/// <summary>Công khai kết quả đánh giá đã công bố.</summary>
public interface IPublishedResultService
{
    /// <summary>Theo phạm vi <c>evaluation.results.view</c> và kết quả của chính mình; lọc đơn vị gồm đơn vị con.</summary>
    Task<PublishedResultsDto> GetResultsAsync(Guid? periodId, Guid? departmentId, string? grade, CancellationToken ct = default);
}

/// <summary>
/// Chỉ trả họ tên, chức danh, đơn vị, mức (và điểm khi bộ tiêu chí bật <c>publishScores</c>) — không trả chi tiết hồ sơ.
/// Kết quả có kiến nghị chưa trả lời ghi chú "đang xem xét".
/// </summary>
public sealed class PublishedResultService : IPublishedResultService
{
    private readonly IPostPublishRepository _repo;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public PublishedResultService(IPostPublishRepository repo, IAuthorizationGuard guard, ICurrentUserService currentUser)
    {
        _repo = repo;
        _guard = guard;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<PublishedResultsDto> GetResultsAsync(Guid? periodId, Guid? departmentId, string? grade, CancellationToken ct = default)
    {
        EvaluationGrade? gradeFilter = null;
        if (!string.IsNullOrWhiteSpace(grade))
        {
            gradeFilter = EvaluationMapping.ParseGrade(grade) is { } g && g != EvaluationGrade.ChuaXepLoai
                ? g
                : throw new ValidationException($"Mức xếp loại \"{grade.Trim()}\" không hợp lệ.");
        }

        var scope = _guard.GetScope(PermissionCodes.EvaluationResultsView);
        // Chủ hồ sơ luôn thấy kết quả của mình.
        if (_currentUser.UserId is { } userId)
            scope = scope.Union(ScopeFilter.OwnerOnly(userId));
        if (scope.IsEmpty)
            return new PublishedResultsDto();

        // Hồ sơ chỉ nạp cho kỳ đang chọn (lọc kỳ và phạm vi trong truy vấn).
        var periods = await _repo.ListPublishedPeriodsAsync(scope, ct);
        var result = new PublishedResultsDto
        {
            Periods = periods
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).ThenByDescending(p => p.CreatedAt)
                .Select(p => new ResultFilterOptionDto { Id = p.Id, Name = p.Name })
                .ToList()
        };

        var selectedPeriod = periodId.HasValue && periodId.Value != Guid.Empty ? periodId : null;
        var records = (await _repo.ListPublishedRecordsAsync(scope, selectedPeriod, ct))
            .Where(r => r.FinalGrade != EvaluationGrade.ChuaXepLoai)
            .ToList();

        result.Departments = records
            .Where(r => r.DepartmentId.HasValue && r.Department != null)
            .GroupBy(r => r.DepartmentId!.Value)
            .Select(g => new ResultFilterOptionDto { Id = g.Key, Name = g.First().Department!.Name })
            .OrderBy(d => d.Name)
            .ToList();

        if (departmentId.HasValue && departmentId.Value != Guid.Empty)
        {
            var key = $"/{departmentId.Value:D}/";
            records = records
                .Where(r => r.DepartmentId == departmentId.Value
                    || (r.Department?.Path is { } path && path.Contains(key, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (gradeFilter.HasValue)
            records = records.Where(r => r.FinalGrade == gradeFilter.Value).ToList();

        var underReview = await _repo.GetRecordIdsWithOpenAppealsAsync(records.Select(r => r.Id).ToList(), ct);
        var publishScores = records.Select(r => r.Period).DistinctBy(p => p.Id).ToDictionary(p => p.Id, PublishScores);
        var currentUserId = _currentUser.UserId;

        result.Items = records
            .OrderBy(r => r.Department?.Name ?? string.Empty)
            .ThenBy(r => r.Member?.FullName ?? string.Empty)
            .Select(r => new PublishedResultItemDto
            {
                RecordId = _guard.Can(PermissionCodes.EvaluationRead, AccessTarget.ForRecord(r)) ? r.Id : null,
                PeriodId = r.PeriodId,
                PeriodName = r.Period.Name,
                FullName = r.Member?.FullName ?? string.Empty,
                PositionTitle = string.IsNullOrWhiteSpace(r.Member?.PositionTitle) ? null : r.Member!.PositionTitle,
                DepartmentId = r.DepartmentId,
                DepartmentName = r.Department?.Name,
                PartyCellId = r.PartyCellId,
                PartyCellName = r.PartyCell?.Name,
                FinalGrade = EvaluationMapping.GradeCode(r.FinalGrade),
                FinalGradeName = CriteriaSetContent.GradeName(r.FinalGrade),
                FinalScore = publishScores[r.PeriodId] ? r.FinalScore : null,
                PublishedAt = r.PublishedAt,
                UnderReview = underReview.Contains(r.Id),
                IsOwn = currentUserId.HasValue && r.MemberId == currentUserId.Value
            })
            .ToList();

        result.GradeCounts = CriteriaSetContent.RankedGrades
            .Select(g => new GradeCountDto
            {
                Grade = EvaluationMapping.GradeCode(g),
                GradeName = CriteriaSetContent.GradeName(g),
                Count = records.Count(r => r.FinalGrade == g)
            })
            .ToList();
        result.ShowsScores = result.Items.Any(i => i.FinalScore.HasValue);
        return result;
    }

    /// <summary>Kỳ chưa có / lỗi ảnh chụp bộ tiêu chí → chỉ công khai mức.</summary>
    private static bool PublishScores(EvaluationPeriod period)
    {
        try
        {
            return period.GetCriteria()?.Content.Parameters.PublishScores ?? false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
