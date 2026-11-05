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

    /// <summary>Trạng thái mục kiến nghị đã chấp nhận, chờ mở lại hồ sơ.</summary>
    public const string AwaitingReopenStatus = "AcceptedAwaitingReopen";

    public const string ImprovementPlansGroup = "IMPROVEMENT_PLANS";

    public const string PlanAcknowledgementGroup = "IMPROVEMENT_PLAN_ACK";

    /// <summary>Kiến nghị của chủ hồ sơ đã được trả lời hoặc đã xử lý xong, chủ hồ sơ chưa xem.</summary>
    public const string AppealAnswersGroup = "APPEAL_ANSWERS";

    /// <summary>Kiến nghị đã chấp nhận, hồ sơ đã mở lại và công bố lại.</summary>
    public const string AppealDoneStatus = "Done";

    /// <summary>Kiến nghị không còn ai xử lý được, hiện cho người gán vai trò.</summary>
    public const string UnassignedAppealsGroup = "APPEALS_UNASSIGNED";

    private readonly IPostPublishRepository _repo;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly AppealResolverCheck _resolverCheck;
    private readonly IStepPerformers? _stepPerformers;

    public PostPublishWorkQueue(
        IPostPublishRepository repo,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IEvaluationWorkflowRepository workflowRepo,
        IPermissionResolver permissions,
        IStepPerformers? stepPerformers = null)
    {
        _repo = repo;
        _guard = guard;
        _currentUser = currentUser;
        _resolverCheck = new AppealResolverCheck(workflowRepo, permissions);
        _stepPerformers = stepPerformers;
    }

    /// <inheritdoc />
    public async Task<List<WorkQueueGroupDto>> GetGroupsAsync(Guid? periodId, CancellationToken ct = default)
    {
        var groups = new List<WorkQueueGroupDto>();
        if (_currentUser.UserId is not { } userId)
            return groups;
        var period = periodId.HasValue && periodId.Value != Guid.Empty ? periodId : null;

        var appealItems = new List<WorkQueueItemDto>();
        if (_guard.HasAny(PermissionCodes.EvaluationAppealResolve))
        {
            // Loại chủ hồ sơ, người gửi và người có xung đột lợi ích của kiến nghị.
            appealItems.AddRange((await _repo.ListOpenAppealsAsync(period, ct))
                .Where(a => a.SubmittedById != userId && !a.ConflictedUserIds.Contains(userId))
                .Where(a => _guard.Can(PermissionCodes.EvaluationAppealResolve, AccessTarget.ForRecord(a.Record)))
                .Select(a => Item(a.Record, userId, a.Status.ToString(),
                    $"Kiến nghị: {AppealService.StatusName(a.Status)} (gửi {EvaluationMapping.Today(a.SubmittedAt).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})")));
        }
        if (_guard.HasAny(PermissionCodes.EvaluationReopen))
        {
            // Chấp nhận mà chưa mở lại thì kết quả cũ vẫn công bố: còn là việc của người mở lại được.
            appealItems.AddRange((await _repo.ListAppealsAwaitingReopenAsync(period, ct))
                .Where(a => a.Record.MemberId != userId && a.SubmittedById != userId && !a.ConflictedUserIds.Contains(userId))
                .Where(a => _guard.Can(PermissionCodes.EvaluationReopen, AccessTarget.ForRecord(a.Record)))
                .Select(a => Item(a.Record, userId, AwaitingReopenStatus, "Kiến nghị đã chấp nhận, chờ mở lại hồ sơ")));
        }
        AddGroup(groups, AppealsGroup, "Kiến nghị chờ xử lý", appealItems);

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
            AddGroup(groups, UnassignedAppealsGroup, "Kiến nghị chưa có người xử lý", unassigned.Select(a => Item(a.Record, userId,
                a.Status.ToString(), $"Không còn ai có quyền \"{permissionName}\" trong phạm vi hồ sơ (ngoài người có xung đột lợi ích) — "
                    + "hãy gán vai trò có quyền này cho người phù hợp")));
        }

        if (_guard.HasAny(PermissionCodes.EvaluationImprovementManage) || _guard.HasAny(PermissionCodes.EvaluationImprovementApprove))
        {
            // Chỉ kỳ đang mở/khóa dữ liệu và kỳ vừa đóng trong số ngày cảnh báo, không quét mọi kỳ cũ.
            var now = DateTime.UtcNow;
            var alertPeriods = (await _repo.ListPlanAlertPeriodsAsync(period, ct))
                .Where(p => ImprovementPlanService.InAlertWindow(p, now))
                .Select(p => p.Id)
                .ToList();
            var needing = (await _repo.ListRecordsNeedingPlanAsync(alertPeriods, ct))
                .Where(x => ImprovementPlanService.IsRequired(x.Record))
                // Kế hoạch do chính mình lập đang chờ người khác duyệt, không phải việc của mình.
                .Where(x => x.Plan == null || !ImprovementPlanService.IsPreparer(x.Plan, userId))
                .ToList();
            var assigned = new List<(EvaluationRecord Record, ImprovementPlan? Plan)>();
            foreach (var x in needing)
            {
                if (await IsPlanAssigneeAsync(x.Record, x.Plan, userId, ct))
                    assigned.Add(x);
            }
            needing = assigned;
            AddGroup(groups, ImprovementPlansGroup, "Kế hoạch 30-60-90 ngày", needing.Select(x => Item(x.Record, userId,
                x.Plan == null ? "PlanMissing" : "PlanDraft",
                x.Plan == null
                    ? $"Chưa có kế hoạch — bắt buộc với mức \"{CriteriaSetContent.GradeName(x.Record.FinalGrade)}\""
                    : "Kế hoạch đang lập, chưa duyệt")));
        }

        var toAcknowledge = await _repo.ListPlansAwaitingAcknowledgementAsync(userId, period, ct);
        AddGroup(groups, PlanAcknowledgementGroup, "Kế hoạch chờ bạn xác nhận", toAcknowledge.Select(p => Item(p.Record, userId,
            p.Status.ToString(), $"Đã duyệt bởi {p.ApprovedByName} — hãy đọc và xác nhận cam kết khắc phục")));

        // Tin cho chủ hồ sơ: chỉ kiến nghị mới nhất của mỗi hồ sơ, hết khi chủ hồ sơ mở xem.
        var answered = (await _repo.ListAnsweredAppealsOfOwnerAsync(userId, period, ct))
            .GroupBy(a => a.RecordId)
            .Select(g => g.OrderByDescending(a => a.SubmittedAt).First())
            .Where(a => a.HasUnseenNews(a.Record));
        AddGroup(groups, AppealAnswersGroup, "Kiến nghị đã được trả lời", answered.Select(a =>
        {
            var (status, name) = OwnerNews(a);
            return Item(a.Record, userId, status, name);
        }));

        return groups;
    }

    /// <summary>Mã và chữ của tin kiến nghị cho chủ hồ sơ.</summary>
    public static (string Status, string Name) OwnerNews(EvaluationAppeal appeal) =>
        appeal.IsDoneAfterReopen(appeal.Record) ? (AppealDoneStatus, "Đã xử lý xong, kết quả đã công bố lại")
        : appeal.Status == AppealStatus.Accepted ? (nameof(AppealStatus.Accepted), "Chấp nhận")
        : (nameof(AppealStatus.Rejected), "Không chấp nhận");

    /// <summary>
    /// Giao cho người gần hồ sơ nhất như các bước của luồng. Chưa có kế hoạch: người lập (người quản lý trực tiếp); không còn ai
    /// ngoài chủ hồ sơ có quyền lập thì người duyệt gần nhất lập thay. Đang lập: người duyệt, bỏ người lập; không còn ai gần hơn
    /// thì mọi người khác có quyền duyệt đều được giao.
    /// </summary>
    private async Task<bool> IsPlanAssigneeAsync(EvaluationRecord record, ImprovementPlan? plan, Guid userId, CancellationToken ct)
    {
        var target = AccessTarget.ForRecord(record);
        if (plan == null)
        {
            if (_guard.Can(PermissionCodes.EvaluationImprovementManage, target))
                return _stepPerformers == null
                    || (await _stepPerformers.ListAsync(PermissionCodes.EvaluationImprovementManage, target, ct)).Contains(userId);
            if (_stepPerformers == null || !_guard.Can(PermissionCodes.EvaluationImprovementApprove, target)
                || (await _stepPerformers.ListAsync(PermissionCodes.EvaluationImprovementManage, target, ct)).Count > 0)
                return false;
            return (await _stepPerformers.ListAsync(PermissionCodes.EvaluationImprovementApprove, target, ct)).Contains(userId);
        }

        if (plan.Status != ImprovementPlanStatus.Draft || !_guard.Can(PermissionCodes.EvaluationImprovementApprove, target))
            return false;
        if (_stepPerformers == null)
            return true;
        var nearest = (await _stepPerformers.ListAsync(PermissionCodes.EvaluationImprovementApprove, target, ct))
            .Where(id => !ImprovementPlanService.IsPreparer(plan, id))
            .ToList();
        return nearest.Count == 0 || nearest.Contains(userId);
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
