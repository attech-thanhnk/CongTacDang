using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Infrastructure.Documents.Forms;

/// <summary>Nguồn Mẫu 04/05: một đề nghị thay đổi việc và hồ sơ của nó; chỉ đọc giá trị đã lưu.</summary>
public sealed record TaskChangeFormSource(EvaluationRecord Record, TaskChangeRequest Request, string? Positions)
{
    public TaskChangeProposal Proposal => TaskChangeRules.ProposalFromJson(Request.Proposal);

    /// <summary>Trọng số từng việc còn hiệu lực sau đề nghị.</summary>
    public Dictionary<Guid, double> After => TaskChangeRules.WeightsFromJson(Request.Weights);

    /// <summary>Trọng số từng việc lúc gửi đề nghị.</summary>
    public Dictionary<Guid, double> Before => TaskChangeRules.WeightsFromJson(Request.WeightsBefore);

    public EvaluationTask? Target => Request.TaskId is { } id ? Record.Tasks.FirstOrDefault(t => t.Id == id) : null;

    /// <summary>Có việc đổi trọng số (kể cả việc bị loại trừ / chuyển tiếp về 0).</summary>
    public bool WeightsChanged => WeightChanges().Count > 0;

    /// <summary>Các việc đổi trọng số (tên, trước, sau) theo thứ tự trên phiếu.</summary>
    public List<(string Name, double Before, double After)> WeightChanges()
    {
        var before = Before;
        var after = After;
        var changes = new List<(string, double, double)>();
        foreach (var task in Record.Tasks.OrderBy(t => t.TaskOrder))
        {
            if (!before.TryGetValue(task.Id, out var b))
                continue;
            var a = after.TryGetValue(task.Id, out var v) ? v : 0;
            if (Math.Abs(a - b) > 0.0001)
                changes.Add((task.Id == Request.TaskId && Request.TaskNameSnapshot != null ? Request.TaskNameSnapshot : task.TaskName, b, a));
        }
        return changes;
    }

    /// <summary>Tổng trọng số các việc còn hiệu lực sau đề nghị (kể cả việc đột xuất mới).</summary>
    public double TotalAfter => After.Where(kv => kv.Key != Request.ResultTaskId).Sum(kv => kv.Value)
        + (Request.Type == TaskChangeType.Urgent ? Proposal.Weight ?? 0 : 0);

    public static string Weight(double value) => Mau09Shared.Compact(value);

    /// <summary>"Ngày 15 tháng 09 năm 2026"; ngày lưu không đổi múi giờ.</summary>
    public static string? DeadlineLine(DateTime? date) => date.HasValue
        ? date.Value.ToString("'Ngày 'dd' tháng 'MM' năm 'yyyy", CultureInfo.InvariantCulture)
        : null;
}
