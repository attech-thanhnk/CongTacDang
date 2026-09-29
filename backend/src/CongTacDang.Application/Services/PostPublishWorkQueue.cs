using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

/// <summary>
/// Nhóm việc sau công bố trong danh sách việc cần xử lý (task 20): kiến nghị chờ xử lý, kế hoạch 30-60-90 ngày cần lập/duyệt,
/// kế hoạch chờ chính người dùng xác nhận.
/// </summary>
public interface IPostPublishWorkQueue
{
    /// <summary>Các nhóm việc sau công bố của người dùng hiện tại (nhóm rỗng không trả về).</summary>
    Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default);
}

/// <summary>Triển khai nhóm việc sau công bố — lọc bằng guard trên từng hồ sơ.</summary>
public sealed class PostPublishWorkQueue : IPostPublishWorkQueue
{
    /// <summary>Mã nhóm "Kiến nghị chờ xử lý".</summary>
    public const string AppealsGroup = "APPEALS";

    /// <summary>Mã nhóm "Kế hoạch 30-60-90 ngày cần lập / duyệt".</summary>
    public const string ImprovementPlansGroup = "IMPROVEMENT_PLANS";

    /// <summary>Mã nhóm "Kế hoạch 30-60-90 ngày chờ bạn xác nhận".</summary>
    public const string PlanAcknowledgementGroup = "IMPROVEMENT_PLAN_ACK";

    private readonly IPostPublishRepository _repo;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;

    public PostPublishWorkQueue(IPostPublishRepository repo, IAuthorizationGuard guard, ICurrentUserService currentUser)
    {
        _repo = repo;
        _guard = guard;
        _currentUser = currentUser;
    }

    /// <inheritdoc />
    public async Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default)
    {
        var groups = new List<WorkQueueGroupDto>();
        if (_currentUser.UserId is not { } userId)
            return groups;
        var period = periodId.HasValue && periodId.Value != Guid.Empty ? periodId : null;

        if (_guard.HasAny(PermissionCodes.EvaluationAppealResolve))
        {
            // Người xử lý: có quyền trên hồ sơ, không phải chủ hồ sơ/người gửi, không thuộc người có xung đột lợi ích của kiến nghị.
            var appeals = (await _repo.ListOpenAppealsAsync(period, ct))
                .Where(a => a.SubmittedById != userId && !a.ConflictedUserIds.Contains(userId))
                .Where(a => _guard.Can(PermissionCodes.EvaluationAppealResolve, AccessTarget.ForRecord(a.Record)))
                .ToList();
            AddGroup(groups, AppealsGroup, "Kiến nghị chờ xử lý", appeals.Select(a => Item(a.Record, userId,
                a.Status.ToString(), $"Kiến nghị: {AppealService.StatusName(a.Status)} (gửi {EvaluationMapping.Today(a.SubmittedAt).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})")));
        }

        if (_guard.HasAny(PermissionCodes.EvaluationImprovementManage))
        {
            var needing = (await _repo.ListRecordsNeedingPlanAsync(period, ct))
                .Where(x => ImprovementPlanService.IsRequired(x.Record))
                .Where(x => _guard.Can(PermissionCodes.EvaluationImprovementManage, AccessTarget.ForRecord(x.Record)))
                .ToList();
            AddGroup(groups, ImprovementPlansGroup, "Kế hoạch 30-60-90 ngày cần lập / duyệt (Mẫu 17)", needing.Select(x => Item(x.Record, userId,
                x.Plan == null ? "PlanMissing" : "PlanDraft",
                x.Plan == null
                    ? $"Chưa có kế hoạch — bắt buộc với mức \"{CriteriaSetContent.GradeName(x.Record.FinalGrade)}\""
                    : "Kế hoạch đang lập, chưa duyệt")));
        }

        var toAcknowledge = await _repo.ListPlansAwaitingAcknowledgementAsync(userId, period, ct);
        AddGroup(groups, PlanAcknowledgementGroup, "Kế hoạch 30-60-90 ngày chờ bạn xác nhận", toAcknowledge.Select(p => Item(p.Record, userId,
            p.Status.ToString(), $"Đã duyệt bởi {p.ApprovedByName} — hãy đọc và xác nhận cam kết khắc phục")));

        return groups;
    }

    private static void AddGroup(List<WorkQueueGroupDto> groups, string code, string name, IEnumerable<WorkQueueItemDto> items)
    {
        var list = items.OrderBy(i => i.PeriodName).ThenBy(i => i.FullName).ToList();
        if (list.Count == 0)
            return;
        groups.Add(new WorkQueueGroupDto { Step = code, StepName = name, Count = list.Count, Items = list });
    }

    private static WorkQueueItemDto Item(EvaluationRecord record, Guid userId, string status, string statusName) => new()
    {
        RecordId = record.Id,
        Version = record.Version,
        PeriodId = record.PeriodId,
        PeriodName = record.Period?.Name ?? string.Empty,
        MemberId = record.MemberId,
        FullName = record.Member?.FullName ?? string.Empty,
        DepartmentName = record.Department?.Name,
        PartyCellName = record.PartyCell?.Name,
        ApprovalAuthority = record.ApprovalAuthority.ToString(),
        WorkflowProfileName = string.Empty,
        Status = status,
        StatusDisplayName = statusName,
        Mode = nameof(StepMode.Internal),
        IsOwnRecord = record.MemberId == userId,
        Deadline = null,
        Overdue = false
    };
}
