using System;
using System.Collections.Generic;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.DTOs;

/// <summary>Một dòng của bảng trọng số trước/sau điều chỉnh.</summary>
public class TaskChangeWeightDto
{
    /// <summary>Id việc (null với việc đột xuất chưa duyệt).</summary>
    public Guid? TaskId { get; set; }
    public string TaskName { get; set; } = string.Empty;
    /// <summary>Trọng số trước điều chỉnh (null với việc mới).</summary>
    public double? Before { get; set; }
    public double After { get; set; }
}

/// <summary>Đề nghị điều chỉnh, chuyển tiếp, loại trừ việc (Mẫu 04) hoặc ghi nhận việc đột xuất (Mẫu 05).</summary>
public class TaskChangeRequestDto
{
    public Guid Id { get; set; }
    /// <summary>Phiên bản xmin của đề nghị (gửi kèm khi duyệt / không duyệt).</summary>
    public uint Version { get; set; }
    public Guid RecordId { get; set; }
    /// <summary><c>Adjust</c> / <c>Carryover</c> / <c>Exclude</c> / <c>Urgent</c>.</summary>
    public string Type { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    /// <summary>Tên việc bị tác động (lúc đề nghị) hoặc tên việc đột xuất.</summary>
    public string? TaskName { get; set; }
    public TaskChangeProposal Proposal { get; set; } = new();
    public List<TaskChangeWeightDto> Weights { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    /// <summary><c>Pending</c> / <c>Approved</c> / <c>Rejected</c>.</summary>
    public string Status { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionComment { get; set; }
    /// <summary>Người xem duyệt / không duyệt được đề nghị này (máy chủ tính).</summary>
    public bool CanDecide { get; set; }
}

/// <summary>Khối "Điều chỉnh việc trong quý" của hồ sơ: danh sách đề nghị và cờ hành động.</summary>
public class RecordTaskChangesDto
{
    public Guid RecordId { get; set; }
    /// <summary>Hồ sơ áp dụng chức năng (bộ tiêu chí chấm theo sản phẩm, có bước đăng ký sản phẩm).</summary>
    public bool Applicable { get; set; }
    /// <summary>Chủ hồ sơ gửi được đề nghị điều chỉnh / chuyển tiếp / loại trừ.</summary>
    public bool CanRequestChange { get; set; }
    /// <summary>Người xem ghi nhận được việc đột xuất.</summary>
    public bool CanRecordUrgent { get; set; }
    /// <summary>Lý do chưa gửi được đề nghị (khi người xem là chủ hồ sơ / người duyệt nhưng hồ sơ không ở giai đoạn cho phép).</summary>
    public string? BlockedReason { get; set; }
    public bool HasPending { get; set; }
    public List<TaskChangeRequestDto> Requests { get; set; } = new();
}

/// <summary>Gửi đề nghị: <see cref="WorkflowRequestDto.Version"/> là phiên bản hồ sơ đã đọc.</summary>
public class CreateTaskChangeRequestDto : WorkflowRequestDto
{
    /// <summary><c>Adjust</c> / <c>Carryover</c> / <c>Exclude</c> / <c>Urgent</c>.</summary>
    public string? Type { get; set; }
    /// <summary>Việc bị tác động (không gửi với việc đột xuất).</summary>
    public Guid? TaskId { get; set; }
    public TaskChangeProposal? Proposal { get; set; }
    /// <summary>Bảng trọng số sau điều chỉnh: Id việc → trọng số (việc không có giữ trọng số hiện tại).</summary>
    public Dictionary<Guid, double>? Weights { get; set; }
    /// <summary>Lý do / nguyên nhân khách quan — bắt buộc.</summary>
    public string? Reason { get; set; }
}

/// <summary>Duyệt / không duyệt đề nghị: <see cref="Version"/> là phiên bản đề nghị đã đọc.</summary>
public class DecideTaskChangeRequestDto
{
    public uint? Version { get; set; }
    /// <summary>Ý kiến (duyệt) / lý do (không duyệt — bắt buộc).</summary>
    public string? Comment { get; set; }
}
