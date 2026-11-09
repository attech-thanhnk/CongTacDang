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

/// <summary>Số liệu trang Tổng quan; mọi danh sách lọc theo quyền và phạm vi của người xem.</summary>
public interface IDashboardService
{
    /// <summary>Lịch kỳ, kết quả gần nhất của tôi, tiến độ hồ sơ trong phạm vi evaluation.read.</summary>
    Task<DashboardOverviewDto> GetOverviewAsync(Guid periodId, CancellationToken ct = default);

    /// <summary>Việc quản trị cần xử lý theo quyền system.users.read / period.manage.</summary>
    Task<AdminSummaryDto> GetAdminSummaryAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class DashboardService : IDashboardService
{
    public const int LateLimit = 8;
    public const int DueSoonWorkingDays = 2;
    public const string PublishedKey = "Published";

    private readonly IEvaluationWorkflowRepository _repo;
    private readonly IAuthorizationGuard _guard;
    private readonly ICurrentUserService _currentUser;
    private readonly IHolidayRepository _holidays;
    private readonly IStepPerformers _performers;
    private readonly IUserAccountService _accounts;
    private readonly IPeriodService _periods;

    public DashboardService(
        IEvaluationWorkflowRepository repo,
        IAuthorizationGuard guard,
        ICurrentUserService currentUser,
        IHolidayRepository holidays,
        IStepPerformers performers,
        IUserAccountService accounts,
        IPeriodService periods)
    {
        _repo = repo;
        _guard = guard;
        _currentUser = currentUser;
        _holidays = holidays;
        _performers = performers;
        _accounts = accounts;
        _periods = periods;
    }

    /// <inheritdoc />
    public async Task<DashboardOverviewDto> GetOverviewAsync(Guid periodId, CancellationToken ct = default)
    {
        if (periodId == Guid.Empty)
            throw new ValidationException("Hãy chọn kỳ đánh giá (tham số periodId).");
        var userId = _currentUser.UserId ?? throw new ForbiddenException("Xác định người dùng hiện tại không thành công.");
        var period = await _repo.FindPeriodAsync(periodId, ct) ?? throw new NotFoundException("Không tìm thấy kỳ đánh giá.");
        var settings = SafeSettings(period);
        var today = EvaluationMapping.Today(DateTime.UtcNow);
        var all = await _repo.ListRecordsAsync(periodId, ct);
        var mine = all.FirstOrDefault(r => r.MemberId == userId);

        var canRead = _guard.HasAny(PermissionCodes.EvaluationRead);
        var readScope = canRead ? _guard.GetScope(PermissionCodes.EvaluationRead) : ScopeFilter.None;
        var scoped = all.Where(r => readScope.Matches(r.MemberId, r.DepartmentId, r.PartyCellId)).ToList();

        var result = new DashboardOverviewDto { PeriodId = periodId, Today = today };

        // Lịch kỳ: người có hồ sơ theo diện của mình; người xem phạm vi theo các diện có trong phạm vi.
        var others = scoped.Where(r => r.MemberId != userId).ToList();
        if (mine != null || others.Count > 0 || _guard.HasAny(PermissionCodes.PeriodManage))
        {
            // Gộp cả diện của hồ sơ của tôi để hạn các bước của chính mình không bị thiếu.
            var shown = mine != null ? others.Append(mine) : others;
            var profiles = others.Count == 0 && (mine == null || _guard.HasAny(PermissionCodes.PeriodManage))
                ? settings.Profiles
                : shown.Select(r => EvaluationMapping.ProfileOf(settings, r)).GroupBy(p => p.Code).Select(g => g.First()).ToList();
            result.Timeline = BuildTimeline(period, profiles, today);
        }

        result.LastResult = await LastResultAsync(period, userId, ct);

        // Chỉ có hồ sơ của chính mình thì thẻ Hồ sơ của tôi đã đủ.
        if (others.Count > 0)
            result.Scope = await BuildScopeAsync(scoped, settings, today, ct);
        return result;
    }

    /// <inheritdoc />
    public async Task<AdminSummaryDto> GetAdminSummaryAsync(CancellationToken ct = default)
    {
        var result = new AdminSummaryDto();

        // Người thực hiện các bước: chỉ người quản lý kỳ toàn công ty xem được kiểm tra kẹt luồng.
        if (_guard.Can(PermissionCodes.PeriodManage, AccessTarget.None)
            && EvaluationMapping.ActivePeriod(await _repo.ListPeriodsAsync(ct)) is { } open)
        {
            var readiness = await _periods.GetReadinessAsync(open.Id, ct);
            var missing = readiness.Issues
                .Where(i => !string.IsNullOrEmpty(i.Permission))
                .Select(i => (i.Step, i.Scope, i.ScopeName))
                .Distinct()
                .Count();
            Add(result, "missingPerformers", "Bước chưa có người thực hiện", missing, $"/periods/{open.Id}?tab=readiness");
        }

        // Chỉ người xử lý được tài khoản mới cần các mục này; danh sách tài khoản vẫn lọc theo quyền xem.
        if (_guard.HasAny(PermissionCodes.SystemUsersManage) && _guard.HasAny(PermissionCodes.SystemUsersRead))
        {
            async Task<int> Count(string? flag, bool? isActive = null) =>
                (await _accounts.SearchAsync(new AccountSearchQuery(1, 1, IsActive: isActive, Flag: flag), ct)).TotalCount;

            Add(result, AccountFlags.Locked, "Tài khoản đang bị khóa", await Count(AccountFlags.Locked), UsersLink(AccountFlags.Locked));
            Add(result, AccountFlags.NeverLoggedIn, "Tài khoản chưa đăng nhập lần nào", await Count(AccountFlags.NeverLoggedIn), UsersLink(AccountFlags.NeverLoggedIn));
            Add(result, AccountFlags.RoleExpiring, $"Vai trò hết hạn trong {AccountFlags.RoleExpiringDays} ngày tới", await Count(AccountFlags.RoleExpiring), UsersLink(AccountFlags.RoleExpiring));
            Add(result, AccountFlags.CellWithChildren, "Sinh hoạt tại tổ chức Đảng có tổ chức con", await Count(AccountFlags.CellWithChildren), UsersLink(AccountFlags.CellWithChildren));
            Add(result, AccountFlags.NoPartyCell, "Đảng viên chưa có tổ chức Đảng", await Count(AccountFlags.NoPartyCell), UsersLink(AccountFlags.NoPartyCell));
            Add(result, AccountFlags.NoPosition, "Cán bộ chưa có chức vụ", await Count(AccountFlags.NoPosition), UsersLink(AccountFlags.NoPosition));
            result.Usage = new AccountUsageDto
            {
                RecentLogins = await Count(AccountFlags.RecentLogin),
                ActiveAccounts = await Count(null, true),
                Days = AccountFlags.RecentLoginDays
            };
        }

        return result;
    }

    private static string UsersLink(string flag) => $"/admin/users?flag={flag}";

    private static void Add(AdminSummaryDto result, string code, string label, int count, string link)
    {
        if (count > 0)
            result.Items.Add(new AdminAlertDto { Code = code, Label = label, Count = count, Link = link });
    }

    private static PeriodTimelineDto BuildTimeline(EvaluationPeriod period, IReadOnlyList<WorkflowProfile> profiles, DateOnly today)
    {
        var timeline = new PeriodTimelineDto { PeriodName = period.Name, PeriodStatus = period.Status.ToString() };
        foreach (var step in WorkflowSteps.Ordered)
        {
            var active = profiles.Where(p => p.IsActive(step)).ToList();
            if (active.Count == 0)
                continue;
            var deadlines = active.Select(p => p.Deadline(step)).Where(d => d.HasValue).Select(d => d!.Value).ToList();
            timeline.Steps.Add(new TimelineStepDto
            {
                Step = WorkflowSteps.Code(step),
                Name = active[0].StepDisplayName(step),
                DeadlineFrom = deadlines.Count > 0 ? deadlines.Min() : null,
                DeadlineTo = deadlines.Count > 0 ? deadlines.Max() : null,
                State = "pending"
            });
        }

        // Kỳ đang chạy: bước đầu tiên chưa hết hạn là bước đang diễn ra, các bước trước đã qua.
        if (period.Status == PeriodStatus.Closed)
            timeline.Steps.ForEach(s => s.State = "done");
        else if (period.Status is PeriodStatus.Open or PeriodStatus.Locked)
        {
            var current = timeline.Steps.FindIndex(s => s.DeadlineTo is { } d && d >= today);
            for (var i = 0; i < timeline.Steps.Count; i++)
                timeline.Steps[i].State = current < 0 || i < current ? "done" : i == current ? "current" : "pending";
        }
        return timeline;
    }

    private async Task<MyLastResultDto?> LastResultAsync(EvaluationPeriod period, Guid userId, CancellationToken ct)
    {
        var earlier = (await _repo.ListPeriodsAsync(ct))
            .Where(p => p.Id != period.Id && (p.Year < period.Year || (p.Year == period.Year && p.Quarter < period.Quarter)))
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).ThenByDescending(p => p.CreatedAt)
            .Take(4)
            .ToList();
        foreach (var p in earlier)
        {
            var record = await _repo.FindRecordIncludingDeletedAsync(p.Id, userId, ct);
            if (record is { IsDeleted: false, Status: RecordStatus.Published })
                return new MyLastResultDto
                {
                    RecordId = record.Id,
                    PeriodName = p.Name,
                    Grade = EvaluationMapping.GradeCode(record.FinalGrade),
                    Score = record.FinalScore
                };
        }
        return null;
    }

    private async Task<ScopeProgressDto> BuildScopeAsync(List<EvaluationRecord> records, PeriodSettings settings, DateOnly today, CancellationToken ct)
    {
        var holidays = (await _holidays.ListAsync(today, today.AddDays(60), ct)).Select(h => h.Date).ToHashSet();
        var dto = new ScopeProgressDto { Total = records.Count, DueSoonWorkingDays = DueSoonWorkingDays };
        var stepCounts = new Dictionary<string, ScopeStepCountDto>(StringComparer.Ordinal);
        var groups = new Dictionary<Guid, ScopeGroupDto>();
        var noCell = new ScopeGroupDto { Name = string.Empty };
        var late = new List<(EvaluationRecord Record, WorkflowProfile Profile, WorkflowStep Step, DateOnly Deadline, int Days)>();

        foreach (var r in records)
        {
            var profile = EvaluationMapping.ProfileOf(settings, r);
            var step = WorkflowSteps.StepOf(r.Status);
            var key = step.HasValue ? WorkflowSteps.Code(step.Value) : PublishedKey;
            var name = step.HasValue ? profile.StepDisplayName(step.Value) : "Đã công bố";
            var deadline = step.HasValue ? profile.Deadline(step.Value) : null;
            var overdue = deadline.HasValue && today > deadline.Value;

            if (!step.HasValue)
                dto.Published++;
            if (overdue)
            {
                dto.Overdue++;
                late.Add((r, profile, step!.Value, deadline!.Value, today.DayNumber - deadline.Value.DayNumber));
            }
            else if (deadline.HasValue && WorkingDaysUntil(today, deadline.Value, holidays) <= DueSoonWorkingDays)
                dto.DueSoon++;

            Count(stepCounts, key, name, overdue);
            var group = r.PartyCellId is { } cellId
                ? groups.TryGetValue(cellId, out var g) ? g : groups[cellId] = new ScopeGroupDto { PartyCellId = cellId, Name = r.PartyCell?.Name ?? string.Empty }
                : noCell;
            group.Total++;
            if (overdue)
                group.Overdue++;
            var groupStep = group.Steps.FirstOrDefault(s => s.Step == key);
            if (groupStep == null)
                group.Steps.Add(groupStep = new ScopeStepCountDto { Step = key, Name = name });
            groupStep.Count++;
            if (overdue)
                groupStep.Overdue++;
        }

        dto.Steps = stepCounts.Values.OrderBy(s => Order(s.Step)).ToList();
        if (noCell.Total > 0)
            groups[Guid.Empty] = noCell;
        var vi = StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), false);
        dto.Groups = groups.Values
            .Select(g => { g.Steps = g.Steps.OrderBy(s => Order(s.Step)).ToList(); return g; })
            .OrderByDescending(g => g.Overdue)
            .ThenBy(g => g.PartyCellId == null ? 1 : 0)
            .ThenBy(g => g.Name, vi)
            .ToList();

        foreach (var item in late.OrderByDescending(x => x.Days).ThenBy(x => x.Record.Member?.FullName, vi).Take(LateLimit))
        {
            dto.Late.Add(new LateRecordDto
            {
                RecordId = item.Record.Id,
                FullName = item.Record.Member?.FullName ?? string.Empty,
                Step = WorkflowSteps.Code(item.Step),
                StepName = item.Profile.StepDisplayName(item.Step),
                Performer = await PerformerAsync(item.Record, item.Profile, item.Step, ct),
                Deadline = item.Deadline,
                DaysOverdue = item.Days
            });
        }
        return dto;
    }

    private static void Count(Dictionary<string, ScopeStepCountDto> counts, string key, string name, bool overdue)
    {
        if (!counts.TryGetValue(key, out var item))
            counts[key] = item = new ScopeStepCountDto { Step = key, Name = name };
        item.Count++;
        if (overdue)
            item.Overdue++;
    }

    private static int Order(string key) =>
        WorkflowSteps.Parse(key) is { } step ? WorkflowSteps.IndexOf(step) : int.MaxValue;

    /// <summary>Số ngày làm việc sau hôm nay đến hết hạn (hạn hôm nay = 0).</summary>
    private static int WorkingDaysUntil(DateOnly today, DateOnly deadline, IReadOnlyCollection<DateOnly> holidays)
    {
        var count = 0;
        for (var d = today.AddDays(1); d <= deadline && count <= DueSoonWorkingDays; d = d.AddDays(1))
            if (DeadlineCalculator.IsWorkingDay(d, holidays))
                count++;
        return count;
    }

    /// <summary>Cùng quy tắc người gần nhất với việc cần xử lý; bước của chủ hồ sơ hoặc cấp trên dùng tên chủ thể.</summary>
    private async Task<string> PerformerAsync(EvaluationRecord record, WorkflowProfile profile, WorkflowStep step, CancellationToken ct)
    {
        var fallback = EvaluationMapping.StepActor(step, profile, record);
        if (WorkflowSteps.OwnerSteps.Contains(step) || profile.Mode(step) != StepMode.Internal
            || WorkflowActions.AdvanceOf(step, profile) is not { } action)
            return fallback;
        var ids = (await _performers.ListAsync(WorkflowActions.PermissionFor(action, profile), AccessTarget.ForRecord(record), ct)).ToList();
        if (ids.Count == 0)
            return fallback;
        var names = (await _repo.GetMembersAsync(ids, ct)).Select(m => m.FullName).Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.Create(new System.Globalization.CultureInfo("vi-VN"), false)).ToList();
        return names.Count switch
        {
            0 => fallback,
            <= 2 => string.Join(", ", names),
            _ => $"{names[0]}, {names[1]} và {names.Count - 2} người khác"
        };
    }

    private static PeriodSettings SafeSettings(EvaluationPeriod period)
    {
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
