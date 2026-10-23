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

/// <summary>Nhóm việc sau công bố: kiến nghị, kế hoạch 30-60-90 ngày cần lập/duyệt/xác nhận.</summary>
public interface IPostPublishWorkQueue
{
    /// <summary>Nhóm rỗng không trả về.</summary>
    Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default);
}

public sealed class PostPublishWorkQueue : IPostPublishWorkQueue
{
    public const string AppealsGroup = "APPEALS";

    public const string ImprovementPlansGroup = "IMPROVEMENT_PLANS";

    public const string PlanAcknowledgementGroup = "IMPROVEMENT_PLAN_ACK";

    /// <summary>Kiến nghị không còn ai xử lý được, hiện cho người gán vai trò.</summary>
    public const string UnassignedAppealsGroup = "APPEALS_UNASSIGNED";

    private readonly IPostPublishRepository _repo;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly AppealResolverCheck _resolverCheck;

    public PostPublishWorkQueue(
        IPostPublishRepository repo,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IEvaluationWorkflowRepository workflowRepo,
        IPermissionResolver permissions)
    {
        _repo = repo;
        _guard = guard;
        _currentUser = currentUser;
        _resolverCheck = new AppealResolverCheck(workflowRepo, permissions);
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
            // Loại chủ hồ sơ, người gửi và người có xung đột lợi ích của kiến nghị.
            var appeals = (await _repo.ListOpenAppealsAsync(period, ct))
                .Where(a => a.SubmittedById != userId && !a.ConflictedUserIds.Contains(userId))
                .Where(a => _guard.Can(PermissionCodes.EvaluationAppealResolve, AccessTarget.ForRecord(a.Record)))
                .ToList();
            AddGroup(groups, AppealsGroup, "Kiến nghị chờ xử lý", appeals.Select(a => Item(a.Record, userId,
                a.Status.ToString(), $"Kiến nghị: {AppealService.StatusName(a.Status)} (gửi {EvaluationMapping.Today(a.SubmittedAt).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})")));
        }

        if (_guard.HasAny(PermissionCodes.SystemAssignmentsManage))
        {
            // Tính lại mỗi lần tải nên nhóm tự hết khi đã gán người xử lý.
            var unassigned = new List<EvaluationAppeal>();
            foreach (var appeal in await _repo.ListOpenAppealsAsync(period, ct))
            {
                if (!await _resolverCheck.HasResolverAsync(appeal, appeal.Record, ct))
                    unassigned.Add(appeal);
            }
            var permissionName = PermissionCodes.DisplayName(PermissionCodes.EvaluationAppealResolve);
            AddGroup(groups, UnassignedAppealsGroup, "Kiến nghị chưa có người xử lý (cần gán vai trò)", unassigned.Select(a => Item(a.Record, userId,
                a.Status.ToString(), $"Không còn ai có quyền \"{permissionName}\" trong phạm vi hồ sơ (ngoài người có xung đột lợi ích) — "
                    + "hãy gán vai trò có quyền này cho người phù hợp")));
        }

        if (_guard.HasAny(PermissionCodes.EvaluationImprovementManage))
        {
            // Chỉ kỳ đang mở/khóa dữ liệu và kỳ vừa đóng trong số ngày cảnh báo, không quét mọi kỳ cũ.
            var now = DateTime.UtcNow;
            var alertPeriods = (await _repo.ListPlanAlertPeriodsAsync(period, ct))
                .Where(p => ImprovementPlanService.InAlertWindow(p, now))
                .Select(p => p.Id)
                .ToList();
            var needing = (await _repo.ListRecordsNeedingPlanAsync(alertPeriods, ct))
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
