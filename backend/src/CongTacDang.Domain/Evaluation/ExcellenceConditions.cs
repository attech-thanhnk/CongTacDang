using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Kết quả xét một điều kiện xếp loại.</summary>
public enum ConditionState
{
    /// <summary>Chưa đủ dữ liệu, người thẩm định quyết định.</summary>
    Unknown = 0,

    Met = 1,

    NotMet = 2,

    /// <summary>Không áp dụng với cán bộ này; coi như không chặn.</summary>
    NotApplicable = 3
}

/// <summary>Sáu điều kiện xếp loại Hoàn thành xuất sắc; (i), (ii) dùng chung cho mức Tốt.</summary>
public static class ExcellenceConditionCodes
{
    public const string Completion = "I";
    public const string UnitCompletion = "II";
    public const string ExceedStandard = "III";
    public const string Remediation = "IV";
    public const string NoCap = "V";
    public const string FocusLevel = "VI";

    public static readonly IReadOnlyList<(string Code, string Label, string Name)> All = new[]
    {
        (Completion, "(i)", "Hoàn thành 100% nhiệm vụ, đúng hạn, đạt chất lượng, hiệu quả"),
        (UnitCompletion, "(ii)", "Tập thể do mình phụ trách hoàn thành 100% nhiệm vụ"),
        (ExceedStandard, "(iii)", "Ít nhất 30% nhiệm vụ vượt chuẩn"),
        (Remediation, "(iv)", "Khắc phục 100% hạn chế đến hạn của kỳ trước"),
        (NoCap, "(v)", "Không thuộc trường hợp bị khống chế mức xếp loại"),
        (FocusLevel, "(vi)", "Không có nhiệm vụ trọng tâm Mức 2, Mức 3")
    };

    public static bool IsValid(string? code) => code != null && All.Any(c => c.Code == code);

    public static string Label(string code) => All.FirstOrDefault(c => c.Code == code).Label ?? code;

    /// <summary>jsonb <c>{ "I": true }</c>: true = Đạt, false = Không; mã lạ bị bỏ.</summary>
    public static Dictionary<string, bool> ParseOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Dictionary<string, bool>();
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
            return parsed.Where(p => IsValid(p.Key)).ToDictionary(p => p.Key, p => p.Value);
        }
        catch (JsonException)
        {
            return new Dictionary<string, bool>();
        }
    }

    /// <summary>Rỗng → null (không lưu).</summary>
    public static string? OverridesToJson(IReadOnlyDictionary<string, bool>? overrides)
    {
        var valid = (overrides ?? new Dictionary<string, bool>()).Where(p => IsValid(p.Key))
            .OrderBy(p => All.Select(c => c.Code).ToList().IndexOf(p.Key))
            .ToDictionary(p => p.Key, p => p.Value);
        return valid.Count == 0 ? null : JsonSerializer.Serialize(valid);
    }
}

/// <summary>Nhiệm vụ còn hiệu lực; <c>SelfProgressRatio</c> null = chưa tự chấm.</summary>
public sealed record ConditionTask(int Order, TaskKind? Kind, int? AppraisalLevel, bool IsExceedStandard, double? SelfProgressRatio);

/// <summary>Số liệu Mẫu 06 của đơn vị do cán bộ đứng đầu; null = chưa nhập.</summary>
public sealed record UnitTaskReport(int? Assigned, int? OnTime);

/// <summary>Mốc kế hoạch Mẫu 17 kỳ trước; <c>IsDue</c> = đã tới hạn tại ngày xét.</summary>
public sealed record PlanMilestoneState(string Name, string? Result, bool IsDue);

/// <summary>Kế hoạch Mẫu 17 của kỳ liền trước; <c>InEffect</c> false = chưa duyệt.</summary>
public sealed record PreviousPlanState(bool InEffect, IReadOnlyList<PlanMilestoneState> Milestones);

public sealed class ExcellenceConditionInput
{
    public IReadOnlyList<ConditionTask> Tasks { get; init; } = Array.Empty<ConditionTask>();

    /// <summary>Tỷ lệ vượt chuẩn tối thiểu của mức Xuất sắc; null = bộ không đặt.</summary>
    public double? MinExceedRatio { get; init; }

    /// <summary>Giữ chức vụ lãnh đạo, quản lý hoặc đứng đầu.</summary>
    public bool IsLeader { get; init; }

    /// <summary>Mẫu 06 cùng kỳ của đơn vị cán bộ đứng đầu; null = không có.</summary>
    public UnitTaskReport? UnitReport { get; init; }

    /// <summary>null = kỳ trước không có kế hoạch Mẫu 17.</summary>
    public PreviousPlanState? PreviousPlan { get; init; }

    public bool SelfScored { get; init; }

    public EvaluationGrade SelfProposedGrade { get; init; }

    public bool? SelfDeclaresNoCap { get; init; }

    public bool Appraised { get; init; }

    public EvaluationGrade? GradeCap { get; init; }

    public string? CapReason { get; init; }

    public IReadOnlyDictionary<string, bool> Overrides { get; init; } = new Dictionary<string, bool>();
}

public sealed record ConditionResult(string Code, string Label, string Name, ConditionState State, string Reason)
{
    /// <summary>"(iii) Mới có …".</summary>
    public string Line => $"{Label} {Reason}";
}

public sealed class ExcellenceConditionReport
{
    /// <summary>Theo phiếu tự chấm: chỉ (iii) và cam kết (v); còn lại Chưa xác định.</summary>
    public IReadOnlyList<ConditionResult> Self { get; init; } = Array.Empty<ConditionResult>();

    /// <summary>Hệ thống tính, chưa ghi đè.</summary>
    public IReadOnlyList<ConditionResult> Computed { get; init; } = Array.Empty<ConditionResult>();

    /// <summary>Đã áp ghi đè của người thẩm định.</summary>
    public IReadOnlyList<ConditionResult> Final { get; init; } = Array.Empty<ConditionResult>();

    /// <summary>Gợi ý cho (i): sản phẩm cần xem.</summary>
    public IReadOnlyList<string> CompletionHints { get; init; } = Array.Empty<string>();

    public bool SelfProposesExcellent { get; init; }

    public ConditionState SelfOverall { get; init; }

    public ConditionState FinalOverall { get; init; }
}

/// <summary>Xét sáu điều kiện Hoàn thành xuất sắc. Chỉ để cảnh báo và in phiếu, không chặn thao tác.</summary>
public static class ExcellenceConditions
{
    /// <summary>Chỉ dùng để gợi ý sản phẩm chậm tiến độ ở (i), không kết luận.</summary>
    public const double ProgressHintRatio = 0.90;

    /// <summary>Việc đã loại trừ / chuyển tiếp không xét; chưa tự chấm thì không có tỷ lệ tiến độ.</summary>
    public static List<ConditionTask> TasksOf(IEnumerable<Entities.EvaluationTask>? tasks) =>
        (tasks ?? Enumerable.Empty<Entities.EvaluationTask>())
            .Where(TaskChangeRules.IsActive)
            .OrderBy(t => t.TaskOrder)
            .Select(t => new ConditionTask(t.TaskOrder, t.Kind, t.AppraisalLevel, t.IsExceedStandard,
                TaskSelfScores.IsPending(t) ? null : t.CriteriaC_Ratio))
            .ToList();

    public static ExcellenceConditionReport Evaluate(ExcellenceConditionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tasks = input.Tasks ?? Array.Empty<ConditionTask>();
        var hints = CompletionHints(tasks);
        var exceed = ExceedStandard(tasks, input.MinExceedRatio);

        var hintText = string.Join("; ", hints);
        var computed = new List<ConditionResult>
        {
            Result(ExcellenceConditionCodes.Completion, ConditionState.Unknown, "chờ người thẩm định xác nhận; gợi ý: " + hintText),
            UnitCompletion(input),
            exceed,
            Remediation(input.PreviousPlan),
            NoCap(input),
            FocusLevel(tasks)
        };

        var overrides = input.Overrides ?? new Dictionary<string, bool>();
        var final = computed.Select(c =>
        {
            if (!overrides.TryGetValue(c.Code, out var met))
                return c;
            var state = met ? ConditionState.Met : ConditionState.NotMet;
            if (state == c.State)
                return c;
            var reason = c.Code == ExcellenceConditionCodes.Completion
                ? (met ? "người thẩm định xác nhận đạt" : "người thẩm định xác nhận không đạt; " + hintText)
                : (met ? "người thẩm định xác nhận đạt" : "người thẩm định xác nhận không đạt");
            return c with { State = state, Reason = reason };
        }).ToList();

        var selfDeclared = input.SelfDeclaresNoCap == true;
        var self = computed
            .Select(c => c.Code switch
            {
                ExcellenceConditionCodes.ExceedStandard => exceed,
                ExcellenceConditionCodes.NoCap => Result(c.Code, selfDeclared ? ConditionState.Met : ConditionState.Unknown,
                    selfDeclared ? "cán bộ cam kết không thuộc trường hợp bị khống chế" : "cán bộ chưa đánh dấu cam kết"),
                _ => c with { State = ConditionState.Unknown, Reason = "xét khi thẩm định" }
            })
            .ToList();

        return new ExcellenceConditionReport
        {
            Self = self,
            Computed = computed,
            Final = final,
            CompletionHints = hints,
            SelfProposesExcellent = input.SelfScored && input.SelfProposedGrade == EvaluationGrade.HoanThanhXuatSac,
            SelfOverall = Overall(self),
            FinalOverall = Overall(final)
        };
    }

    /// <summary>Có điều kiện Không → Không; mọi điều kiện Đạt / Không áp dụng → Đạt; còn lại Chưa xác định.</summary>
    public static ConditionState Overall(IEnumerable<ConditionResult> results)
    {
        var list = results.ToList();
        if (list.Any(r => r.State == ConditionState.NotMet))
            return ConditionState.NotMet;
        return list.All(r => r.State is ConditionState.Met or ConditionState.NotApplicable) ? ConditionState.Met : ConditionState.Unknown;
    }

    /// <summary>Ô "Tự nhận xét điều kiện bắt buộc" Mẫu 09A: chỉ xét (iii) và cam kết; chưa tự chấm → null.</summary>
    public static string? SelfRemark(bool selfScored, EvaluationGrade proposed, IReadOnlyList<ConditionTask> tasks, double? minExceedRatio, bool? selfDeclaresNoCap)
    {
        if (!selfScored || proposed == EvaluationGrade.ChuaXepLoai)
            return null;
        var name = CriteriaSetContent.GradeName(proposed);
        var text = $"Đáp ứng điều kiện mức {name}";
        if (proposed == EvaluationGrade.HoanThanhXuatSac)
        {
            var exceed = ExceedStandard(tasks ?? Array.Empty<ConditionTask>(), minExceedRatio);
            if (exceed.State == ConditionState.NotMet)
                text = $"Chưa đủ điều kiện {name}: {exceed.Reason}";
        }
        return selfDeclaresNoCap == true ? text + "; không thuộc trường hợp bị khống chế" : text;
    }

    /// <summary>Mức đề xuất cao hơn mức khống chế.</summary>
    public static bool ExceedsCap(EvaluationGrade proposed, EvaluationGrade? cap) =>
        cap is { } c && c != EvaluationGrade.ChuaXepLoai && proposed != EvaluationGrade.ChuaXepLoai && (int)proposed < (int)c;

    /// <summary>Mức được chọn làm mức khống chế: Tốt trở xuống.</summary>
    public static bool IsValidCap(EvaluationGrade grade) =>
        grade is EvaluationGrade.HoanThanhTot or EvaluationGrade.HoanThanh or EvaluationGrade.KhongHoanThanh;

    private static IReadOnlyList<string> CompletionHints(IReadOnlyList<ConditionTask> tasks)
    {
        var hints = new List<string>();
        foreach (var t in tasks.OrderBy(t => t.Order))
        {
            if (t.AppraisalLevel == 3)
                hints.Add($"SP{t.Order}: thẩm định Mức 3");
            if (t.SelfProgressRatio is { } c && c + CriteriaSetContent.Epsilon < ProgressHintRatio)
                hints.Add($"SP{t.Order}: tiến độ tự chấm {Percent(c)}, dưới {Percent(ProgressHintRatio)}");
        }
        if (hints.Count == 0)
            hints.Add("không có sản phẩm chậm tiến độ hay không đáp ứng");
        return hints;
    }

    private static ConditionResult UnitCompletion(ExcellenceConditionInput input)
    {
        const string code = ExcellenceConditionCodes.UnitCompletion;
        if (input.UnitReport is { } report)
        {
            if (report.Assigned is not { } assigned || report.OnTime is not { } onTime)
                return Result(code, ConditionState.Unknown, "báo cáo Mẫu 06 chưa nhập số nhiệm vụ được giao, đúng hạn");
            return onTime >= assigned
                ? Result(code, ConditionState.Met, $"đơn vị hoàn thành {onTime}/{assigned} nhiệm vụ đúng hạn")
                : Result(code, ConditionState.NotMet, $"đơn vị mới hoàn thành {onTime}/{assigned} nhiệm vụ đúng hạn");
        }
        return input.IsLeader
            ? Result(code, ConditionState.Unknown, "chưa có báo cáo Mẫu 06 của đơn vị phụ trách")
            : Result(code, ConditionState.NotApplicable, "không áp dụng");
    }

    private static ConditionResult ExceedStandard(IReadOnlyList<ConditionTask> tasks, double? minRatio)
    {
        const string code = ExcellenceConditionCodes.ExceedStandard;
        if (minRatio is not { } min || min <= 0)
            return Result(code, ConditionState.NotApplicable, "bộ tiêu chí không đặt tỷ lệ vượt chuẩn");
        if (tasks.Count == 0)
            return Result(code, ConditionState.Unknown, "chưa có sản phẩm đăng ký");
        var exceed = tasks.Count(t => t.IsExceedStandard);
        var ratio = (double)exceed / tasks.Count;
        return ratio + CriteriaSetContent.Epsilon >= min
            ? Result(code, ConditionState.Met, $"có {exceed}/{tasks.Count} sản phẩm vượt chuẩn ({Percent(ratio)})")
            : Result(code, ConditionState.NotMet, $"mới có {exceed}/{tasks.Count} sản phẩm vượt chuẩn ({Percent(ratio)}), cần từ {Percent(min)}");
    }

    private static ConditionResult Remediation(PreviousPlanState? plan)
    {
        const string code = ExcellenceConditionCodes.Remediation;
        if (plan == null)
            return Result(code, ConditionState.Met, "không có hạn chế phải khắc phục");
        if (!plan.InEffect)
            return Result(code, ConditionState.Unknown, "kế hoạch khắc phục kỳ trước chưa được duyệt");
        var failed = plan.Milestones.Where(m => m.Result == ImprovementPlanContent.ResultNotAchieved).Select(m => m.Name).ToList();
        if (failed.Count > 0)
            return Result(code, ConditionState.NotMet, $"kế hoạch kỳ trước chưa đạt: {string.Join(", ", failed)}");
        var missing = plan.Milestones.Where(m => m.IsDue && m.Result == null).Select(m => m.Name).ToList();
        if (missing.Count > 0)
            return Result(code, ConditionState.NotMet, $"kế hoạch kỳ trước chưa ghi kết quả mốc đến hạn: {string.Join(", ", missing)}");
        return Result(code, ConditionState.Met, "các mốc kế hoạch kỳ trước đã đạt");
    }

    private static ConditionResult NoCap(ExcellenceConditionInput input)
    {
        const string code = ExcellenceConditionCodes.NoCap;
        if (input.GradeCap is { } cap && cap != EvaluationGrade.ChuaXepLoai)
        {
            var reason = string.IsNullOrWhiteSpace(input.CapReason) ? string.Empty : $": {input.CapReason.Trim()}";
            return Result(code, ConditionState.NotMet, $"khống chế tối đa mức {CriteriaSetContent.GradeName(cap)}{reason}");
        }
        return input.Appraised
            ? Result(code, ConditionState.Met, "không vi phạm")
            : Result(code, ConditionState.Unknown, "chưa thẩm định");
    }

    private static ConditionResult FocusLevel(IReadOnlyList<ConditionTask> tasks)
    {
        const string code = ExcellenceConditionCodes.FocusLevel;
        var focus = tasks.Where(t => t.Kind == TaskKind.TrongTam).ToList();
        var bad = focus.Where(t => t.AppraisalLevel is 2 or 3).OrderBy(t => t.Order).ToList();
        if (bad.Count > 0)
            return Result(code, ConditionState.NotMet,
                "nhiệm vụ trọng tâm " + string.Join(", ", bad.Select(t => $"SP{t.Order} Mức {t.AppraisalLevel}")));
        if (focus.Any(t => t.AppraisalLevel == null))
            return Result(code, ConditionState.Unknown, "chưa xếp mức đủ nhiệm vụ trọng tâm");
        return Result(code, ConditionState.Met, "không có nhiệm vụ trọng tâm Mức 2, Mức 3");
    }

    private static ConditionResult Result(string code, ConditionState state, string reason)
    {
        var (_, label, name) = ExcellenceConditionCodes.All.First(c => c.Code == code);
        return new ConditionResult(code, label, name, state, reason);
    }

    private static string Percent(double ratio) =>
        Math.Round(ratio * 100, 1).ToString("0.#", CultureInfo.InvariantCulture).Replace('.', ',') + "%";
}
