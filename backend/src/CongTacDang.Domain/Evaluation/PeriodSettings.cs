using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Cấu hình một bước trong kỳ.</summary>
public sealed class StepSetting
{
    /// <summary>Bước được áp dụng trong kỳ.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Thời hạn (ngày). Chỉ hiển thị/cảnh báo, chỉ chặn khi <see cref="PeriodSettings.EnforceDeadlines"/> = true.</summary>
    public DateOnly? Deadline { get; set; }
}

/// <summary>Mẫu cấu hình dựng sẵn khi tạo kỳ.</summary>
/// <param name="Code">Mã mẫu (dùng trong API).</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả.</param>
public sealed record PeriodPreset(string Code, string Name, string Description)
{
    /// <summary>Cấu hình của mẫu.</summary>
    public PeriodSettings Build() => Code switch
    {
        PeriodSettings.PresetFull => PeriodSettings.FullPreset(),
        PeriodSettings.PresetTransitionQ3 => PeriodSettings.TransitionQ3Preset(),
        _ => throw new ArgumentOutOfRangeException(nameof(Code), Code, null)
    };
}

/// <summary>
/// Cấu hình theo kỳ (<c>EvaluationPeriod.Settings</c>, cột jsonb) — docs/thiet-ke/luong-danh-gia.md mục 3.2.
/// Bước nào bật/tắt, thời hạn, mẫu tự chấm và tham số nghiệp vụ là cấu hình, không cứng trong code.
/// </summary>
public sealed class PeriodSettings
{
    /// <summary>Phiên bản schema hiện tại.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Mẫu tự chấm có Mẫu 01/02 (tính điểm A-B-C-D theo nhiệm vụ).</summary>
    public const string Form09A = "09A";

    /// <summary>Mẫu tự chấm trực tiếp 6 trục (Q3/2026).</summary>
    public const string Form09B = "09B";

    /// <summary>Mã mẫu "Đầy đủ theo HD03".</summary>
    public const string PresetFull = "full";

    /// <summary>Mã mẫu "Quý III/2026 — chuyển tiếp".</summary>
    public const string PresetTransitionQ3 = "q3-2026-transition";

    /// <summary>Hai mẫu cấu hình dựng sẵn.</summary>
    public static readonly IReadOnlyList<PeriodPreset> Presets = new[]
    {
        new PeriodPreset(PresetFull, "Đầy đủ theo HD03", "Bật tất cả các bước; tự chấm theo Mẫu 09A (có Mẫu 01/02)."),
        new PeriodPreset(PresetTransitionQ3, "Quý III/2026 — chuyển tiếp",
            "Tắt đăng ký và duyệt sản phẩm (B1); tự chấm trực tiếp 6 trục theo Mẫu 09B.")
    };

    /// <summary>Tùy chọn JSON: camelCase, không phân biệt hoa thường; khóa bước giữ nguyên mã (B1_REGISTER…).</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    /// <summary>Phiên bản schema của cấu hình.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Cấu hình từng bước (khóa = mã bước).</summary>
    public Dictionary<string, StepSetting> Steps { get; set; } = WorkflowSteps.Ordered.ToDictionary(WorkflowSteps.Code, _ => new StepSetting());

    /// <summary>Chặn hoàn thành bước khi đã quá thời hạn.</summary>
    public bool EnforceDeadlines { get; set; }

    /// <summary>Mẫu tự chấm: <see cref="Form09A"/> hoặc <see cref="Form09B"/>.</summary>
    public string SelfScoreForm { get; set; } = Form09A;

    /// <summary>Tham số nghiệp vụ (mặc định = giá trị code đang dùng).</summary>
    public EvaluationParameters Parameters { get; set; } = new();

    /// <summary>Mẫu "Đầy đủ theo HD03": bật tất cả, tự chấm 09A.</summary>
    public static PeriodSettings FullPreset() => new();

    /// <summary>Mẫu "Quý III/2026 — chuyển tiếp": tắt B1_REGISTER, B1_APPROVE; tự chấm 09B.</summary>
    public static PeriodSettings TransitionQ3Preset()
    {
        var settings = new PeriodSettings { SelfScoreForm = Form09B };
        settings.Steps[WorkflowSteps.Code(WorkflowStep.B1_REGISTER)].Enabled = false;
        settings.Steps[WorkflowSteps.Code(WorkflowStep.B1_APPROVE)].Enabled = false;
        return settings;
    }

    /// <summary>Tìm mẫu theo mã; null nếu không có.</summary>
    public static PeriodPreset? FindPreset(string? code) =>
        Presets.FirstOrDefault(p => string.Equals(p.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Đọc cấu hình từ JSON. Chuỗi rỗng (kỳ tạo trước task 12) → mẫu "Đầy đủ theo HD03".
    /// Bước thiếu trong JSON được bổ sung (bật, không thời hạn). JSON sai định dạng → <see cref="FormatException"/>.
    /// </summary>
    public static PeriodSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return FullPreset();

        PeriodSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<PeriodSettings>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Cấu hình kỳ không đúng định dạng JSON.", ex);
        }

        return (settings ?? FullPreset()).Normalize();
    }

    /// <summary>Chuẩn hóa: đủ 9 bước (khóa đúng mã), tham số không null.</summary>
    public PeriodSettings Normalize()
    {
        // Khóa theo đúng thứ tự bước (để so sánh/serialize ổn định), rồi đến khóa lạ (Validate báo lỗi).
        var source = Steps ?? new Dictionary<string, StepSetting>();
        var known = new Dictionary<WorkflowStep, StepSetting>();
        var unknown = new List<KeyValuePair<string, StepSetting>>();
        foreach (var (key, value) in source)
        {
            var step = WorkflowSteps.Parse(key);
            if (step.HasValue)
                known[step.Value] = value ?? new StepSetting();
            else
                unknown.Add(new KeyValuePair<string, StepSetting>(key, value ?? new StepSetting()));
        }

        var steps = new Dictionary<string, StepSetting>(StringComparer.Ordinal);
        foreach (var step in WorkflowSteps.Ordered)
            steps[WorkflowSteps.Code(step)] = known.TryGetValue(step, out var setting) ? setting : new StepSetting();
        foreach (var (key, value) in unknown)
            steps.TryAdd(key, value);

        Steps = steps;
        Parameters ??= new EvaluationParameters();
        SelfScoreForm = string.IsNullOrWhiteSpace(SelfScoreForm) ? Form09A : SelfScoreForm.Trim().ToUpperInvariant();
        return this;
    }

    /// <summary>Ghi cấu hình ra JSON (lưu vào cột jsonb).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Bản sao sâu (qua JSON).</summary>
    public PeriodSettings Clone() => Parse(ToJson());

    /// <summary>Bước có được áp dụng trong kỳ hay không.</summary>
    public bool IsEnabled(WorkflowStep step) =>
        Steps.TryGetValue(WorkflowSteps.Code(step), out var setting) ? setting.Enabled : true;

    /// <summary>Tập các bước đang bật.</summary>
    public IReadOnlySet<WorkflowStep> EnabledSteps() => WorkflowSteps.Ordered.Where(IsEnabled).ToHashSet();

    /// <summary>Thời hạn của bước (null nếu không đặt).</summary>
    public DateOnly? Deadline(WorkflowStep step) =>
        Steps.TryGetValue(WorkflowSteps.Code(step), out var setting) ? setting.Deadline : null;

    /// <summary>Tự chấm theo Mẫu 09B (chấm trực tiếp 6 trục).</summary>
    [JsonIgnore]
    public bool UsesAxisScoring => string.Equals(SelfScoreForm, Form09B, StringComparison.OrdinalIgnoreCase);

    /// <summary>Kiểm tra cấu hình; trả danh sách lỗi (rỗng = hợp lệ).</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Phiên bản cấu hình {SchemaVersion} không được hỗ trợ (chỉ hỗ trợ phiên bản {CurrentSchemaVersion}).");

        foreach (var key in (Steps ?? new Dictionary<string, StepSetting>()).Keys.Where(k => WorkflowSteps.Parse(k) == null))
            errors.Add($"Mã bước \"{key}\" không tồn tại.");

        foreach (var step in WorkflowSteps.Mandatory.OrderBy(WorkflowSteps.IndexOf))
        {
            if (!IsEnabled(step))
                errors.Add($"Bước \"{WorkflowSteps.DisplayName(step)}\" là bước bắt buộc, không tắt được.");
        }

        if (IsEnabled(WorkflowStep.B1_APPROVE) && !IsEnabled(WorkflowStep.B1_REGISTER))
            errors.Add("Không thể bật bước duyệt danh mục sản phẩm khi bước đăng ký sản phẩm đang tắt.");

        if (SelfScoreForm is not (Form09A or Form09B))
            errors.Add($"Mẫu tự chấm phải là {Form09A} hoặc {Form09B}.");
        else if (SelfScoreForm == Form09A && !IsEnabled(WorkflowStep.B1_REGISTER))
            errors.Add("Tự chấm theo Mẫu 09A cần danh mục sản phẩm (Mẫu 01): hãy bật bước đăng ký sản phẩm hoặc chọn Mẫu 09B.");

        errors.AddRange((Parameters ?? new EvaluationParameters()).Validate());
        return errors;
    }

    /// <summary>
    /// Hai cấu hình chỉ khác nhau ở thời hạn (dùng khi kỳ đã mở: chỉ được sửa thời hạn).
    /// </summary>
    public bool DiffersOnlyInDeadlines(PeriodSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var left = Clone();
        var right = other.Clone();
        foreach (var setting in left.Steps.Values) setting.Deadline = null;
        foreach (var setting in right.Steps.Values) setting.Deadline = null;
        return left.ToJson() == right.ToJson();
    }
}
