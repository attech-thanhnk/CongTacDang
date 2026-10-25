using System;
using System.Linq;
using CongTacDang.Domain.Entities;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 05 — Phiếu ghi nhận nhiệm vụ bổ sung, khẩn cấp, đột xuất. Template:
/// <c>Mau_05_PhieuNhiemVuDotXuat.docx</c>. Mỗi đề nghị loại Urgent một phiếu.
/// </summary>
public sealed class Mau05Data
{
    public const string TemplateFileName = "Mau_05_PhieuNhiemVuDotXuat.docx";

    /// <summary>Thay ký hiệu ☐ in sẵn.</summary>
    public const string CheckedBox = "☒";

    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Cấp giao việc chưa lưu theo đề nghị → giữ chữ mặc định.</summary>
    [TemplateField("ASSIGNER")] public string? Assigner { get; init; }

    /// <summary>Căn cứ, lý do nhập khi ghi nhận.</summary>
    [TemplateField("BASIS")] public string? Basis { get; init; }

    /// <summary>Ngày ghi nhận việc.</summary>
    [TemplateField("ASSIGNED_DATE")] public string? AssignedDate { get; init; }

    [TemplateField("TASK_NAME")] public string? TaskName { get; init; }
    [TemplateField("OUTPUT")] public string? Output { get; init; }
    [TemplateField("DEADLINE")] public string? Deadline { get; init; }

    /// <summary>Đơn vị / cá nhân phối hợp chưa lưu theo việc → giữ chữ mặc định.</summary>
    [TemplateField("COORDINATION")] public string? Coordination { get; init; }

    /// <summary>☒ khi các việc khác bị giảm trọng số để nhường cho việc mới.</summary>
    [TemplateField("BOX_REBALANCE")] public string? BoxRebalance { get; init; }

    [TemplateField("NEW_WEIGHT")] public string? NewWeight { get; init; }

    /// <summary>Chỉ khi đề nghị đã được duyệt.</summary>
    [TemplateField("ASSIGNEE_SIGN")] public string? AssigneeSign { get; init; }

    public static Mau05Data From(TaskChangeFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var record = source.Record;
        var request = source.Request;
        var proposal = source.Proposal;
        var created = request.ResultTaskId is { } id ? record.Tasks.FirstOrDefault(t => t.Id == id) : null;
        var reduced = source.WeightChanges().Any(c => c.After < c.Before);

        return new Mau05Data
        {
            DateLine = CollectiveFormText.DateLine(request.RequestedAt),
            Quarter = Mau09Shared.Quarter(record),
            Year = Mau09Shared.Year(record),
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = source.Positions ?? FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            Basis = FormText.OrNull(request.Reason),
            AssignedDate = TaskChangeFormSource.DeadlineLine(CollectiveFormText.ToVietnamTime(request.RequestedAt)),
            TaskName = FormText.OrNull(proposal.TaskName ?? request.TaskNameSnapshot),
            Output = FormText.OrNull(proposal.TargetOutput),
            Deadline = TaskChangeFormSource.DeadlineLine(proposal.Deadline ?? created?.Deadline),
            BoxRebalance = reduced ? CheckedBox : null,
            NewWeight = reduced && proposal.Weight.HasValue ? TaskChangeFormSource.Weight(proposal.Weight.Value) : null,
            AssigneeSign = request.Status == TaskChangeStatus.Approved ? FormText.OrNull(record.Member?.FullName) : null
        };
    }
}
