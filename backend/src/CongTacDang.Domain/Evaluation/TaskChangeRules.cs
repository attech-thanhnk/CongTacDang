using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>
/// Nội dung đề nghị thay đổi một việc (lưu jsonb trong <see cref="TaskChangeRequest.Proposal"/>). Điều chỉnh: trường null = giữ nguyên;
/// việc đột xuất: toàn bộ việc mới (tên, chuẩn đạt, trọng số bắt buộc; trục bắt buộc khi bộ có trục).
/// </summary>
public sealed class TaskChangeProposal
{
    public string? TaskName { get; set; }
    public string? TargetOutput { get; set; }
    public DateTime? Deadline { get; set; }
    public string? AxisCode { get; set; }
    public string? ProductCode { get; set; }
    /// <summary>Vai trò: <c>ChuTri</c> / <c>PhoiHop</c>.</summary>
    public string? Role { get; set; }
    public string? ExceedCriteria { get; set; }
    public string? EvidenceSource { get; set; }
    public string? Confirmer { get; set; }
    /// <summary>Trọng số của việc đột xuất mới (việc hiện có lấy trọng số trong bảng trọng số sau điều chỉnh).</summary>
    public double? Weight { get; set; }
}

/// <summary>
/// Luật thuần của đề nghị thay đổi danh mục việc trong quý (Mẫu 04, 05): kiểm tra nội dung, tính bảng trọng số sau điều chỉnh và áp
/// vào danh mục. Số việc và tổng trọng số của các việc còn hiệu lực kiểm bằng <see cref="EvaluationScoring.ValidateTaskRegistration"/>
/// (việc đã loại trừ / chuyển tiếp không tính). Không đổi công thức chấm điểm.
/// </summary>
public static class TaskChangeRules
{
    public const int MaxNameLength = 500;
    public const int MaxTextLength = 2000;
    public const int MaxReasonLength = 4000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Việc còn hiệu lực (chưa loại trừ / chuyển tiếp, chưa xóa).</summary>
    public static bool IsActive(EvaluationTask task) => !task.IsDeleted && task.ChangeStatus == null;

    /// <summary>Loại đề nghị có tác động lên một việc đang có.</summary>
    public static bool TargetsTask(TaskChangeType type) => type != TaskChangeType.Urgent;

    /// <summary>Loại đề nghị làm việc bị tác động thôi hiệu lực (loại trừ / chuyển tiếp).</summary>
    public static TaskChangeMark? MarkOf(TaskChangeType type) => type switch
    {
        TaskChangeType.Exclude => TaskChangeMark.Excluded,
        TaskChangeType.Carryover => TaskChangeMark.CarriedOver,
        _ => null
    };

    /// <summary>Tên loại đề nghị (giao diện, lịch sử).</summary>
    public static string TypeName(TaskChangeType type) => type switch
    {
        TaskChangeType.Adjust => "Điều chỉnh việc",
        TaskChangeType.Carryover => "Chuyển tiếp sang quý sau",
        TaskChangeType.Exclude => "Loại trừ việc",
        TaskChangeType.Urgent => "Việc đột xuất",
        _ => type.ToString()
    };

    /// <summary>Tên trạng thái đề nghị.</summary>
    public static string StatusName(TaskChangeStatus status) => status switch
    {
        TaskChangeStatus.Pending => "Chờ duyệt",
        TaskChangeStatus.Approved => "Đã duyệt",
        TaskChangeStatus.Rejected => "Không duyệt",
        _ => status.ToString()
    };

    #region JSON

    public static string ProposalToJson(TaskChangeProposal proposal) => JsonSerializer.Serialize(proposal, JsonOptions);

    public static TaskChangeProposal ProposalFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new TaskChangeProposal();
        try
        {
            return JsonSerializer.Deserialize<TaskChangeProposal>(json, JsonOptions) ?? new TaskChangeProposal();
        }
        catch (JsonException)
        {
            return new TaskChangeProposal();
        }
    }

    public static string WeightsToJson(IReadOnlyDictionary<Guid, double> weights) =>
        JsonSerializer.Serialize(weights.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value), JsonOptions);

    public static Dictionary<Guid, double> WeightsFromJson(string? json)
    {
        var result = new Dictionary<Guid, double>();
        if (string.IsNullOrWhiteSpace(json))
            return result;
        try
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, double>>(json, JsonOptions) ?? new();
            foreach (var (key, value) in raw)
            {
                if (Guid.TryParse(key, out var id))
                    result[id] = value;
            }
        }
        catch (JsonException)
        {
            // Dữ liệu hỏng → coi như không có bảng trọng số (giữ trọng số hiện tại).
        }
        return result;
    }

    #endregion

    #region Kiểm tra

    /// <summary>
    /// Kiểm tra đề nghị trên danh mục hiện tại của hồ sơ. Trả thông báo lỗi (tiếng Việt) hoặc null nếu hợp lệ:
    /// việc bị tác động còn hiệu lực; nội dung hợp lệ; bảng trọng số chỉ chứa việc còn hiệu lực, trọng số dương; số việc và tổng trọng số
    /// sau điều chỉnh đúng tham số bộ tiêu chí.
    /// </summary>
    public static string? Validate(
        TaskChangeType type,
        Guid? taskId,
        TaskChangeProposal? proposal,
        IReadOnlyDictionary<Guid, double>? weights,
        IReadOnlyList<EvaluationTask> tasks,
        CriteriaSetContent criteria)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(criteria);
        if (!Enum.IsDefined(type))
            return "Loại đề nghị không hợp lệ.";
        proposal ??= new TaskChangeProposal();
        weights ??= new Dictionary<Guid, double>();

        var active = tasks.Where(IsActive).ToList();
        EvaluationTask? target = null;
        if (TargetsTask(type))
        {
            if (taskId is not { } id || id == Guid.Empty)
                return "Hãy chọn việc cần điều chỉnh, chuyển tiếp hoặc loại trừ.";
            target = tasks.FirstOrDefault(t => t.Id == id && !t.IsDeleted);
            if (target == null)
                return "Việc được chọn không thuộc danh mục của hồ sơ.";
            if (!IsActive(target))
                return $"Việc \"{target.TaskName}\" đã được loại trừ hoặc chuyển tiếp, không đề nghị thay đổi được nữa.";
        }
        else if (taskId is { } urgentTask && urgentTask != Guid.Empty)
        {
            return "Việc đột xuất là việc mới, không chọn việc đang có.";
        }

        var contentError = type switch
        {
            TaskChangeType.Adjust => ValidateFields(proposal, criteria, requireAll: false),
            TaskChangeType.Urgent => ValidateFields(proposal, criteria, requireAll: true),
            _ => null
        };
        if (contentError != null)
            return contentError;

        // Việc bị loại trừ / chuyển tiếp: trọng số trong bảng (nếu có) bỏ qua — việc về 0.
        var remaining = active.Where(t => !(MarkOf(type) != null && t.Id == target!.Id)).ToList();
        var allowed = active.Select(t => t.Id).ToHashSet();
        if (weights.Keys.Any(k => !allowed.Contains(k)))
            return "Bảng trọng số sau điều chỉnh có việc không còn hiệu lực trong danh mục của hồ sơ.";
        if (weights.Values.Any(w => double.IsNaN(w) || double.IsInfinity(w) || w < 0))
            return "Trọng số không hợp lệ.";

        var resulting = ResultingWeights(type, target?.Id, proposal, weights, tasks);
        foreach (var task in remaining)
        {
            if (resulting[task.Id] <= 0)
                return $"Trọng số của việc \"{task.TaskName}\" phải lớn hơn 0. Nếu không thực hiện việc này, hãy đề nghị loại trừ hoặc chuyển tiếp.";
        }

        var list = remaining.Select(t => resulting[t.Id]).ToList();
        if (type == TaskChangeType.Urgent)
            list.Add(proposal.Weight ?? 0);

        if (type == TaskChangeType.Adjust && !ChangesSomething(target!, proposal, resulting, tasks))
            return "Đề nghị điều chỉnh chưa có nội dung mới so với việc hiện tại.";

        return EvaluationScoring.ValidateTaskRegistration(list, criteria.Parameters);
    }

    /// <summary>
    /// Trọng số sau điều chỉnh của mọi việc còn hiệu lực (trừ việc bị loại trừ / chuyển tiếp): lấy trong bảng trọng số, việc không có
    /// trong bảng giữ trọng số hiện tại.
    /// </summary>
    public static Dictionary<Guid, double> ResultingWeights(
        TaskChangeType type, Guid? targetId, TaskChangeProposal proposal, IReadOnlyDictionary<Guid, double> weights,
        IReadOnlyList<EvaluationTask> tasks)
    {
        var result = new Dictionary<Guid, double>();
        foreach (var task in tasks.Where(IsActive))
        {
            if (MarkOf(type) != null && task.Id == targetId)
                continue;
            result[task.Id] = weights.TryGetValue(task.Id, out var w) ? Math.Round(w, 2) : task.Weight;
        }
        return result;
    }

    private static string? ValidateFields(TaskChangeProposal p, CriteriaSetContent criteria, bool requireAll)
    {
        if (requireAll)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(p.TaskName)) missing.Add("tên việc");
            if (string.IsNullOrWhiteSpace(p.TargetOutput)) missing.Add("chuẩn đạt");
            if (criteria.Axes.Count > 0 && string.IsNullOrWhiteSpace(p.AxisCode)) missing.Add("trục kết quả");
            if (p.Weight is null or <= 0) missing.Add("trọng số");
            if (missing.Count > 0)
                return $"Việc đột xuất chưa nhập {string.Join(", ", missing)}.";
        }
        else if (p.TaskName != null && string.IsNullOrWhiteSpace(p.TaskName))
        {
            return "Tên việc không được để trống.";
        }

        if (p.Weight is { } weight && (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0))
            return "Trọng số không hợp lệ.";
        if (!string.IsNullOrWhiteSpace(p.AxisCode) && criteria.FindAxis(p.AxisCode) == null)
            return $"Trục kết quả \"{p.AxisCode.Trim()}\" không có trong bộ tiêu chí của kỳ.";
        if (!string.IsNullOrWhiteSpace(p.Role) && ParseRole(p.Role) == null)
            return "Vai trò là \"Chủ trì\" hoặc \"Phối hợp\".";
        if (p.TaskName?.Trim().Length > MaxNameLength)
            return $"Tên việc không được dài quá {MaxNameLength} ký tự.";
        foreach (var (value, name) in new[]
                 {
                     (p.TargetOutput, "Chuẩn đạt"), (p.ExceedCriteria, "Tiêu chí vượt chuẩn"),
                     (p.EvidenceSource, "Nguồn minh chứng"), (p.Confirmer, "Thẩm quyền xác nhận")
                 })
        {
            if (value?.Trim().Length > MaxTextLength)
                return $"{name} không được dài quá {MaxTextLength} ký tự.";
        }
        if (p.ProductCode?.Trim().Length > 50)
            return "Mã sản phẩm không được dài quá 50 ký tự.";
        return null;
    }

    private static bool ChangesSomething(EvaluationTask target, TaskChangeProposal p, IReadOnlyDictionary<Guid, double> resulting,
        IReadOnlyList<EvaluationTask> tasks)
    {
        static bool Differs(string? proposed, string? current) =>
            proposed != null && !string.Equals(proposed.Trim(), current?.Trim() ?? string.Empty, StringComparison.Ordinal);

        if (Differs(p.TaskName, target.TaskName) || Differs(p.TargetOutput, target.TargetOutput)
            || Differs(p.ExceedCriteria, target.ExceedCriteria) || Differs(p.EvidenceSource, target.EvidenceSource)
            || Differs(p.Confirmer, target.Confirmer) || Differs(p.ProductCode, target.ProductCode))
            return true;
        if (!string.IsNullOrWhiteSpace(p.AxisCode) && !string.Equals(p.AxisCode.Trim(), target.AxisCode, StringComparison.OrdinalIgnoreCase))
            return true;
        if (ParseRole(p.Role) is { } role && role != target.Role)
            return true;
        if (p.Deadline is { } deadline && deadline.Date != target.Deadline.Date)
            return true;
        return tasks.Where(IsActive).Any(t => resulting.TryGetValue(t.Id, out var w) && Math.Abs(w - t.Weight) > 0.0001);
    }

    #endregion

    #region Áp đề nghị

    /// <summary>
    /// Áp đề nghị đã kiểm tra vào danh mục: điều chỉnh → sửa việc; loại trừ / chuyển tiếp → đánh dấu, trọng số 0, không chấm; đột xuất →
    /// thêm việc mới loại <see cref="TaskKind.DotXuat"/>; cập nhật trọng số các việc còn hiệu lực theo bảng sau điều chỉnh. Điểm tạm của
    /// việc đổi trọng số tính lại theo khung tỷ trọng của hồ sơ (nếu có). Trả lỗi (không thay đổi gì) hoặc việc mới (đột xuất).
    /// </summary>
    public static (string? Error, EvaluationTask? NewTask) Apply(
        TaskChangeRequest request,
        IReadOnlyList<EvaluationTask> tasks,
        CriteriaSetContent criteria,
        DateTime defaultDeadline,
        WeightFrame? frame)
    {
        ArgumentNullException.ThrowIfNull(request);
        var proposal = ProposalFromJson(request.Proposal);
        var weights = WeightsFromJson(request.Weights);
        var error = Validate(request.Type, request.TaskId, proposal, weights, tasks, criteria);
        if (error != null)
            return (error, null);

        var resulting = ResultingWeights(request.Type, request.TaskId, proposal, weights, tasks);
        var target = request.TaskId is { } id ? tasks.First(t => t.Id == id) : null;

        if (request.Type == TaskChangeType.Adjust)
            ApplyFields(target!, proposal, criteria);

        if (MarkOf(request.Type) is { } mark)
        {
            target!.ChangeStatus = mark;
            target.Weight = 0;
            target.SelfScore = 0;
            target.IsExceedStandard = false;
        }

        foreach (var task in tasks.Where(IsActive))
        {
            if (!resulting.TryGetValue(task.Id, out var weight) || Math.Abs(weight - task.Weight) < 0.0001)
                continue;
            task.Weight = weight;
            task.SelfScore = frame != null
                ? EvaluationScoring.ScoreTask(weight, task.CriteriaA_Ratio, task.CriteriaB_Ratio, task.CriteriaC_Ratio, task.CriteriaD_Ratio,
                    frame, criteria.Parameters.Rounding.TaskScore).Score
                : weight;
        }

        EvaluationTask? created = null;
        if (request.Type == TaskChangeType.Urgent)
        {
            var weight = Math.Round(proposal.Weight!.Value, 2);
            created = new EvaluationTask
            {
                Id = Guid.NewGuid(),
                RecordId = request.RecordId,
                TaskOrder = tasks.Where(t => !t.IsDeleted).Select(t => t.TaskOrder).DefaultIfEmpty(0).Max() + 1,
                TaskName = proposal.TaskName!.Trim(),
                TargetOutput = proposal.TargetOutput?.Trim() ?? string.Empty,
                Weight = weight,
                Deadline = proposal.Deadline.HasValue ? DateTime.SpecifyKind(proposal.Deadline.Value, DateTimeKind.Utc) : defaultDeadline,
                AxisCode = criteria.FindAxis(proposal.AxisCode)?.Code,
                ProductCode = Clean(proposal.ProductCode),
                Kind = TaskKind.DotXuat,
                Role = ParseRole(proposal.Role) ?? TaskRole.ChuTri,
                ExceedCriteria = Clean(proposal.ExceedCriteria),
                EvidenceSource = Clean(proposal.EvidenceSource),
                Confirmer = Clean(proposal.Confirmer),
                CriteriaA_Ratio = 1.0,
                CriteriaB_Ratio = 1.0,
                CriteriaC_Ratio = 1.0,
                CriteriaD_Ratio = 1.0,
                SelfScore = 0 // Chưa tự chấm (như nhiệm vụ đăng ký): nộp phiếu bắt buộc nhập tỷ lệ A-B-C-D cho việc này.
            };
        }

        return (null, created);
    }

    private static void ApplyFields(EvaluationTask task, TaskChangeProposal p, CriteriaSetContent criteria)
    {
        if (!string.IsNullOrWhiteSpace(p.TaskName)) task.TaskName = p.TaskName.Trim();
        if (p.TargetOutput != null) task.TargetOutput = p.TargetOutput.Trim();
        if (p.Deadline.HasValue) task.Deadline = DateTime.SpecifyKind(p.Deadline.Value, DateTimeKind.Utc);
        if (criteria.FindAxis(p.AxisCode) is { } axis) task.AxisCode = axis.Code;
        if (p.ProductCode != null) task.ProductCode = Clean(p.ProductCode);
        if (ParseRole(p.Role) is { } role) task.Role = role;
        if (p.ExceedCriteria != null) task.ExceedCriteria = Clean(p.ExceedCriteria);
        if (p.EvidenceSource != null) task.EvidenceSource = Clean(p.EvidenceSource);
        if (p.Confirmer != null) task.Confirmer = Clean(p.Confirmer);
    }

    /// <summary>Mô tả ngắn thay đổi trọng số (ghi lịch sử hồ sơ): "Việc A 30 → 25; Việc B 20 → 15".</summary>
    public static string DescribeWeightChanges(IReadOnlyList<EvaluationTask> tasks, IReadOnlyDictionary<Guid, double> before)
    {
        var parts = tasks
            .Where(t => before.TryGetValue(t.Id, out var old) && Math.Abs(old - t.Weight) > 0.0001)
            .OrderBy(t => t.TaskOrder)
            .Select(t => string.Create(CultureInfo.InvariantCulture, $"{t.TaskName} {before[t.Id]:0.##} → {t.Weight:0.##}"));
        return string.Join("; ", parts);
    }

    #endregion

    public static TaskRole? ParseRole(string? value) =>
        Enum.TryParse<TaskRole>(value?.Trim(), true, out var role) && Enum.IsDefined(role) ? role : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
