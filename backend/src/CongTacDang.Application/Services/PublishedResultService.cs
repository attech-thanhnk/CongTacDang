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

/// <summary>Công khai kết quả đánh giá đã công bố (task 20 — T-86, HD03 Bước 5).</summary>
public interface IPublishedResultService
{
    /// <summary>
    /// Danh sách kết quả đã công bố trong phạm vi <c>evaluation.results.view</c> của người dùng (và kết quả của chính mình), lọc theo
    /// kỳ, đơn vị chính quyền (gồm đơn vị con), mức.
    /// </summary>
    Task<PublishedResultsDto> GetResultsAsync(Guid? periodId, Guid? departmentId, string? grade, CancellationToken ct = default);
}

/// <summary>
/// Công khai kết quả: chỉ họ tên, chức danh, đơn vị, mức chính thức (và điểm khi bộ tiêu chí của kỳ bật <c>publishScores</c>).
/// Phạm vi công khai do bản gán vai trò quyết định (Global = toàn công ty; đơn vị/tổ chức Đảng = đơn vị đó và đơn vị con) —
/// không cứng trong code. Không trả chi tiết hồ sơ, minh chứng, ý kiến (HD03 II.2: "công khai kết quả" không đồng nghĩa công bố
/// toàn bộ hồ sơ). Kết quả đang có kiến nghị chưa trả lời được ghi chú "đang xem xét" (PL II III.2).
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
        // Quyền được biết: chủ hồ sơ luôn thấy kết quả của mình.
        if (_currentUser.UserId is { } userId)
            scope = scope.Union(ScopeFilter.OwnerOnly(userId));
        if (scope.IsEmpty)
            return new PublishedResultsDto();

        var records = (await _repo.ListPublishedRecordsAsync(scope, null, ct))
            .Where(r => r.FinalGrade != EvaluationGrade.ChuaXepLoai)
            .ToList();

        var result = new PublishedResultsDto
        {
            Periods = records
                .GroupBy(r => r.PeriodId)
                .Select(g => g.First().Period)
                .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).ThenByDescending(p => p.CreatedAt)
                .Select(p => new ResultFilterOptionDto { Id = p.Id, Name = p.Name })
                .ToList()
        };

        if (periodId.HasValue && periodId.Value != Guid.Empty)
            records = records.Where(r => r.PeriodId == periodId.Value).ToList();

        result.Departments = records
            .Where(r => r.DepartmentId.HasValue && r.Department != null)
            .GroupBy(r => r.DepartmentId!.Value)
            .Select(g => new ResultFilterOptionDto { Id = g.Key, Name = g.First().Department!.Name })
            .OrderBy(d => d.Name)
            .ToList();

        if (departmentId.HasValue && departmentId.Value != Guid.Empty)
        {
            // Lọc theo đơn vị gồm cả đơn vị con (Path của cây đơn vị chứa Id đơn vị cha).
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

    /// <summary>Cờ công khai điểm của bộ tiêu chí của kỳ; kỳ chưa có / lỗi ảnh chụp → chỉ công khai mức.</summary>
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
