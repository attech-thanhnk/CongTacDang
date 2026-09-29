using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Application.DTOs;

namespace CongTacDang.Application.Services;

/// <summary>Cấu hình nhắc việc (<c>Notifications:*</c>).</summary>
public sealed class NotificationOptions
{
    /// <summary>Bước còn từ 0 tới số ngày này tới hạn thì báo "sắp tới hạn" (<c>Notifications:DueSoonDays</c>, mặc định 2).</summary>
    public int DueSoonDays { get; set; } = 2;
}

/// <summary>Nhắc việc trong ứng dụng (task 20 — T-89): số việc trên chuông ở header, tính khi tải.</summary>
public interface INotificationService
{
    /// <summary>Tổng hợp việc đang chờ người dùng hiện tại.</summary>
    Task<NotificationSummaryDto> GetSummaryAsync(CancellationToken ct = default);
}

/// <summary>
/// Tổng hợp từ danh sách việc cần xử lý (cùng một nguồn với trang "Việc cần xử lý", đã lọc quyền/phạm vi): hồ sơ chờ ở các bước,
/// bước sắp tới hạn (theo hạn của bước trong hồ sơ luồng) và quá hạn, kiến nghị chờ xử lý, kế hoạch 30-60-90 ngày cần lập và
/// kế hoạch chờ xác nhận. Không gửi email, không đẩy thời gian thực.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private const int MaxItems = 20;

    private readonly IEvaluationWorkflowService _workflow;
    private readonly NotificationOptions _options;

    public NotificationService(IEvaluationWorkflowService workflow, NotificationOptions options)
    {
        _workflow = workflow;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<NotificationSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        var queue = await _workflow.GetWorkQueueAsync(null, ct);
        var today = EvaluationMapping.Today(DateTime.UtcNow);
        var dueSoonDays = Math.Max(0, _options.DueSoonDays);
        var postPublishCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            PostPublishWorkQueue.AppealsGroup, PostPublishWorkQueue.ImprovementPlansGroup, PostPublishWorkQueue.PlanAcknowledgementGroup
        };

        var stepGroups = queue.Groups.Where(g => !postPublishCodes.Contains(g.Step)).ToList();
        var stepItems = stepGroups.SelectMany(g => g.Items.Select(i => (Group: g, Item: i))).ToList();
        bool IsDueSoon(WorkQueueItemDto i) =>
            !i.Overdue && i.Deadline is { } d && d >= today && d.DayNumber - today.DayNumber <= dueSoonDays;

        int CountOf(string code) => queue.Groups.FirstOrDefault(g => g.Step == code)?.Count ?? 0;

        var summary = new NotificationSummaryDto
        {
            Total = queue.Total,
            PendingSteps = stepItems.Count,
            Overdue = stepItems.Count(x => x.Item.Overdue),
            DueSoon = stepItems.Count(x => IsDueSoon(x.Item)),
            Appeals = CountOf(PostPublishWorkQueue.AppealsGroup),
            ImprovementPlans = CountOf(PostPublishWorkQueue.ImprovementPlansGroup),
            PlansToAcknowledge = CountOf(PostPublishWorkQueue.PlanAcknowledgementGroup),
            DueSoonDays = dueSoonDays
        };

        // Thứ tự ưu tiên: quá hạn → sắp tới hạn → kiến nghị → kế hoạch → xác nhận → việc khác.
        var items = new List<NotificationItemDto>();
        items.AddRange(stepItems.Where(x => x.Item.Overdue).OrderBy(x => x.Item.Deadline)
            .Select(x => StepItem("overdue", x.Group, x.Item, $"Quá hạn {x.Group.StepName.ToLowerInvariant()}")));
        items.AddRange(stepItems.Where(x => IsDueSoon(x.Item)).OrderBy(x => x.Item.Deadline)
            .Select(x => StepItem("dueSoon", x.Group, x.Item, $"Sắp tới hạn: {x.Group.StepName}")));
        foreach (var (code, kind) in new[]
                 {
                     (PostPublishWorkQueue.AppealsGroup, "appeal"),
                     (PostPublishWorkQueue.ImprovementPlansGroup, "improvementPlan"),
                     (PostPublishWorkQueue.PlanAcknowledgementGroup, "acknowledge")
                 })
        {
            var group = queue.Groups.FirstOrDefault(g => g.Step == code);
            if (group != null)
                items.AddRange(group.Items.Select(i => StepItem(kind, group, i, group.StepName, $"{i.FullName} · {i.StatusDisplayName}")));
        }
        items.AddRange(stepItems.Where(x => !x.Item.Overdue && !IsDueSoon(x.Item))
            .Select(x => StepItem("pending", x.Group, x.Item, x.Group.StepName)));

        summary.Items = items.Take(MaxItems).ToList();
        return summary;
    }

    private static NotificationItemDto StepItem(string kind, WorkQueueGroupDto group, WorkQueueItemDto item, string title, string? detail = null) => new()
    {
        Kind = kind,
        Title = title,
        Detail = detail ?? $"{item.FullName}{(item.IsOwnRecord ? " (hồ sơ của bạn)" : string.Empty)} · {item.PeriodName}",
        Link = $"/evaluations/{item.RecordId}",
        Deadline = item.Deadline
    };
}
