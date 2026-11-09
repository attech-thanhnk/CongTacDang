using System;
using System.Collections.Generic;

namespace CongTacDang.Application.DTOs;

/// <summary>Trang Tổng quan theo kỳ đang chọn; khối nào null là người xem không có quyền hoặc không có dữ liệu.</summary>
public class DashboardOverviewDto
{
    public Guid PeriodId { get; set; }
    public DateOnly Today { get; set; }
    public PeriodTimelineDto? Timeline { get; set; }
    public MyLastResultDto? LastResult { get; set; }
    public ScopeProgressDto? Scope { get; set; }
}

/// <summary>Các bước của kỳ kèm hạn; bỏ bước không áp dụng ở mọi diện quyết định liên quan.</summary>
public class PeriodTimelineDto
{
    public string PeriodName { get; set; } = string.Empty;
    public string PeriodStatus { get; set; } = string.Empty;
    /// <summary>Các diện quyết định người xem được thấy; diện của chính họ đứng đầu.</summary>
    public List<TimelineProfileDto> Profiles { get; set; } = new();
    public List<TimelineStepDto> Steps { get; set; } = new();
}

public class TimelineProfileDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsMine { get; set; }
}

public class TimelineDeadlineDto
{
    public string ProfileCode { get; set; } = string.Empty;
    public DateOnly? Deadline { get; set; }
}

public class TimelineStepDto
{
    public string Step { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Hạn sớm nhất giữa các diện quyết định; null khi không đặt hạn.</summary>
    public DateOnly? DeadlineFrom { get; set; }
    /// <summary>Hạn muộn nhất; bằng <see cref="DeadlineFrom"/> khi các diện cùng hạn.</summary>
    public DateOnly? DeadlineTo { get; set; }
    /// <summary>done | current | pending.</summary>
    public string State { get; set; } = string.Empty;
    /// <summary>Hạn theo từng diện có thực hiện bước này.</summary>
    public List<TimelineDeadlineDto> Deadlines { get; set; } = new();
}

/// <summary>Kết quả đã công bố gần nhất của chính người xem ở kỳ trước kỳ đang chọn.</summary>
public class MyLastResultDto
{
    public Guid RecordId { get; set; }
    public string PeriodName { get; set; } = string.Empty;
    public string Grade { get; set; } = string.Empty;
    public double Score { get; set; }
}

/// <summary>Tiến độ hồ sơ trong phạm vi evaluation.read của người xem.</summary>
public class ScopeProgressDto
{
    public int Total { get; set; }
    public int Published { get; set; }
    public int Overdue { get; set; }
    /// <summary>Hạn của bước hiện tại rơi trong số ngày làm việc này (kể cả hôm nay).</summary>
    public int DueSoon { get; set; }
    public int DueSoonWorkingDays { get; set; }
    /// <summary>Theo thứ tự bước; chỉ bước có hồ sơ.</summary>
    public List<ScopeStepCountDto> Steps { get; set; } = new();
    /// <summary>Quá hạn lâu nhất trước, tối đa <c>DashboardService.LateLimit</c> dòng.</summary>
    public List<LateRecordDto> Late { get; set; } = new();
    public List<ScopeGroupDto> Groups { get; set; } = new();
}

public class ScopeStepCountDto
{
    /// <summary>Mã bước, hoặc <c>Published</c>.</summary>
    public string Step { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Overdue { get; set; }
}

public class LateRecordDto
{
    public Guid RecordId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    /// <summary>Người làm được bước đang chờ, hoặc chủ thể của bước.</summary>
    public string Performer { get; set; } = string.Empty;
    public DateOnly Deadline { get; set; }
    public int DaysOverdue { get; set; }
}

/// <summary>Một tổ chức Đảng trong phạm vi; null Id = hồ sơ chưa gắn tổ chức Đảng.</summary>
public class ScopeGroupDto
{
    public Guid? PartyCellId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Overdue { get; set; }
    public List<ScopeStepCountDto> Steps { get; set; } = new();
}

/// <summary>Việc quản trị cần xử lý; chỉ gồm mục khác 0.</summary>
public class AdminSummaryDto
{
    public List<AdminAlertDto> Items { get; set; } = new();
    /// <summary>null khi người xem không có quyền xem tài khoản.</summary>
    public AccountUsageDto? Usage { get; set; }
}

public class AdminAlertDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Link { get; set; } = string.Empty;
}

public class AccountUsageDto
{
    public int RecentLogins { get; set; }
    public int ActiveAccounts { get; set; }
    public int Days { get; set; }
}
