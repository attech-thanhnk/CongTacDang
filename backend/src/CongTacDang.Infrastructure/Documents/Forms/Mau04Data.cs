using System;
using System.Collections.Generic;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>
/// Dữ liệu Mẫu 04 — Phiếu đề nghị điều chỉnh, chuyển tiếp, loại trừ việc trong quý. Template:
/// <c>Mau_04_PhieuDeNghiDieuChinh.docx</c>. Mỗi đề nghị loại Adjust/Carryover/Exclude một phiếu.
/// </summary>
public sealed class Mau04Data
{
    public const string TemplateFileName = "Mau_04_PhieuDeNghiDieuChinh.docx";

    /// <summary>Thay ký hiệu ☐ in sẵn.</summary>
    public const string CheckedBox = "☒";

    /// <summary>Ngày gửi đề nghị.</summary>
    [TemplateField("DATE_LINE")] public string? DateLine { get; init; }
    [TemplateField("QUARTER")] public string? Quarter { get; init; }
    [TemplateField("YEAR")] public string? Year { get; init; }
    [TemplateField("FULL_NAME")] public string? FullName { get; init; }
    [TemplateField("POSITION")] public string? Position { get; init; }
    [TemplateField("DEPARTMENT")] public string? Department { get; init; }

    /// <summary>Tên việc kèm mã sản phẩm.</summary>
    [TemplateField("AFFECTED_TASK")] public string? AffectedTask { get; init; }

    [TemplateField("BOX_ADJUST")] public string? BoxAdjust { get; init; }
    [TemplateField("BOX_CARRYOVER")] public string? BoxCarryover { get; init; }
    [TemplateField("BOX_EXCLUDE")] public string? BoxExclude { get; init; }
    [TemplateField("BOX_WEIGHT")] public string? BoxWeight { get; init; }

    [TemplateField("CONTENT_BEFORE")] public string? ContentBefore { get; init; }
    [TemplateField("CONTENT_AFTER")] public string? ContentAfter { get; init; }

    /// <summary>Lý do, căn cứ chủ hồ sơ nhập khi gửi đề nghị.</summary>
    [TemplateField("REASON_DETAIL")] public string? ReasonDetail { get; init; }

    /// <summary>Minh chứng kèm theo chưa lưu theo đề nghị → giữ chữ mặc định.</summary>
    [TemplateField("EVIDENCE")] public string? Evidence { get; init; }

    [TemplateField("IMPACT")] public string? Impact { get; init; }

    /// <summary>Chỉ khi đề nghị đã được duyệt.</summary>
    [TemplateField("APPROVER_SIGN")] public string? ApproverSign { get; init; }

    [TemplateField("REQUESTER_SIGN")] public string? RequesterSign { get; init; }

    public static Mau04Data From(TaskChangeFormSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var record = source.Record;
        var request = source.Request;
        var target = source.Target;
        var name = FormText.OrNull(request.TaskNameSnapshot ?? target?.TaskName);
        var code = FormText.OrNull(target?.ProductCode);
        var weightChanged = source.WeightsChanged;
        var fieldLines = FieldLines(source.Proposal);

        return new Mau04Data
        {
            DateLine = CollectiveFormText.DateLine(request.RequestedAt),
            Quarter = Mau09Shared.Quarter(record),
            Year = Mau09Shared.Year(record),
            FullName = FormText.OrNull(record.Member?.FullName),
            Position = source.Positions ?? FormText.OrNull(record.Member?.PositionTitle),
            Department = FormText.OrNull(record.Department?.Name ?? record.PartyCell?.Name),
            AffectedTask = name == null ? null : code == null ? name : $"{name} (Mã SP: {code})",
            BoxAdjust = request.Type == TaskChangeType.Adjust && (fieldLines.Count > 0 || !weightChanged) ? CheckedBox : null,
            BoxCarryover = request.Type == TaskChangeType.Carryover ? CheckedBox : null,
            BoxExclude = request.Type == TaskChangeType.Exclude ? CheckedBox : null,
            BoxWeight = request.Type == TaskChangeType.Adjust && weightChanged ? CheckedBox : null,
            ContentBefore = Before(source, target),
            ContentAfter = After(source, fieldLines),
            ReasonDetail = FormText.OrNull(request.Reason),
            Impact = ImpactText(source),
            ApproverSign = request.Status == TaskChangeStatus.Approved ? FormText.OrNull(request.DecidedByName) : null,
            RequesterSign = FormText.OrNull(request.RequestedByName)
        };
    }

    // Điều chỉnh đã duyệt đã ghi đè nội dung việc nên chỉ trọng số còn giá trị cũ.
    private static string? Before(TaskChangeFormSource source, EvaluationTask? target)
    {
        if (target == null)
            return null;
        var lines = new List<string>();
        var contentKept = !(source.Request.Type == TaskChangeType.Adjust && source.Request.Status == TaskChangeStatus.Approved);
        if (contentKept)
        {
            if (!string.IsNullOrWhiteSpace(target.TargetOutput))
                lines.Add("Mục tiêu: " + target.TargetOutput.Trim());
            lines.Add("Thời hạn: " + FormText.Date(target.Deadline));
        }
        var weight = source.Before.TryGetValue(target.Id, out var w) ? w : target.Weight;
        lines.Add($"Trọng số: {TaskChangeFormSource.Weight(weight)} điểm");
        return string.Join("\n", lines);
    }

    private static string? After(TaskChangeFormSource source, List<string> fieldLines)
    {
        var request = source.Request;
        var lines = new List<string>();
        switch (request.Type)
        {
            case TaskChangeType.Carryover:
                lines.Add("Chuyển sang quý sau, không chấm trong quý này.");
                break;
            case TaskChangeType.Exclude:
                lines.Add("Loại trừ, trọng số 0, không chấm trong quý này.");
                break;
            default:
                lines.AddRange(fieldLines);
                if (request.TaskId is { } id && source.After.TryGetValue(id, out var weight))
                    lines.Add($"Trọng số: {TaskChangeFormSource.Weight(weight)} điểm");
                break;
        }
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    private static List<string> FieldLines(TaskChangeProposal p)
    {
        var lines = new List<string>();
        void Add(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                lines.Add($"{label}: {value.Trim()}");
        }
        Add("Tên việc", p.TaskName);
        Add("Mục tiêu", p.TargetOutput);
        Add("Thời hạn", p.Deadline.HasValue ? FormText.Date(p.Deadline.Value) : null);
        Add("Trục kết quả", p.AxisCode);
        Add("Mã sản phẩm", p.ProductCode);
        Add("Vai trò", TaskChangeRules.ParseRole(p.Role) switch { TaskRole.ChuTri => "Chủ trì", TaskRole.PhoiHop => "Phối hợp", _ => null });
        Add("Tiêu chí vượt chuẩn", p.ExceedCriteria);
        Add("Nguồn minh chứng", p.EvidenceSource);
        Add("Thẩm quyền xác nhận", p.Confirmer);
        return lines;
    }

    private static string ImpactText(TaskChangeFormSource source)
    {
        var lines = source.WeightChanges()
            .Select(c => $"{c.Name}: {TaskChangeFormSource.Weight(c.Before)} → {TaskChangeFormSource.Weight(c.After)} điểm")
            .ToList();
        lines.Insert(0, $"Tổng trọng số nhóm nhiệm vụ sau điều chỉnh: {TaskChangeFormSource.Weight(source.TotalAfter)} điểm.");
        return string.Join("\n", lines);
    }
}
