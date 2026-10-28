using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Chế độ thực hiện một bước trong diện quyết định.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StepMode>))]
public enum StepMode
{
    /// <summary>Làm trong hệ thống bởi người có quyền thực hiện của bước.</summary>
    Internal = 0,

    /// <summary>Cấp trên thực hiện; người có quyền <c>evaluation.external.record</c> ghi nhận kết quả.</summary>
    External = 1,

    /// <summary>Không áp dụng; bỏ qua khi tính bước kế tiếp.</summary>
    Off = 2
}

/// <summary>Mã quyền mặc định cho luồng (Domain không tham chiếu Application). Phải trùng
/// <c>PermissionCodes</c>, có unit test đối chiếu.</summary>
public static class WorkflowPermissions
{
    public const string Self = "evaluation.self";
    public const string TasksApprove = "evaluation.tasks.approve";
    public const string CellConfirm = "evaluation.cell.confirm";
    public const string CollectiveRecord = "evaluation.collective.record";
    public const string Appraise = "evaluation.appraise";
    public const string DirectorReview = "evaluation.director.review";
    public const string UnitReview = "evaluation.unit.review";
    public const string Decide = "evaluation.decide";
    public const string Publish = "evaluation.publish";
    public const string ExternalRecord = "evaluation.external.record";

    /// <summary>Quyền thực hiện mặc định của bước nội bộ.</summary>
    public static string DefaultFor(WorkflowStep step) => step switch
    {
        WorkflowStep.B1_REGISTER or WorkflowStep.B2_SELF_SCORE => Self,
        WorkflowStep.B1_APPROVE => TasksApprove,
        WorkflowStep.B2_CELL_CONFIRM => CellConfirm,
        WorkflowStep.B3A_COLLECTIVE => CollectiveRecord,
        WorkflowStep.B3B_APPRAISAL => Appraise,
        WorkflowStep.B3C_DIRECTOR => DirectorReview,
        WorkflowStep.B4_DECISION => Decide,
        WorkflowStep.B5_PUBLISH => Publish,
        _ => throw new ArgumentOutOfRangeException(nameof(step), step, null)
    };
}

public sealed class ProfileStepSetting
{
    public StepMode Mode { get; set; } = StepMode.Internal;

    /// <summary>Chỉ dùng khi <see cref="StepMode.Internal"/>; bước của chủ hồ sơ luôn là <c>evaluation.self</c>.</summary>
    public string? Permission { get; set; }

    /// <summary>Chỉ cảnh báo; chỉ chặn khi <see cref="PeriodSettings.EnforceDeadlines"/>.</summary>
    public DateOnly? Deadline { get; set; }

    /// <summary>Quy tắc điền ngày hạn theo quý (null = nhập tay). Chỉ ghi vào <see cref="Deadline"/> khi tạo kỳ hoặc khi
    /// người dùng yêu cầu (<see cref="PeriodSettings.ApplyDeadlineRules"/>), không tự tính lại.</summary>
    public DeadlineRule? Rule { get; set; }
}

/// <summary>Cấu hình bước cho một diện quyết định (ví dụ Diện Đảng ủy cơ sở). Hồ sơ đánh giá lưu mã diện quyết định
/// của mình (ảnh chụp khi thêm vào kỳ).</summary>
public sealed class WorkflowProfile
{
    /// <summary>Chữ thường, số, gạch nối; duy nhất trong kỳ.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Khóa = mã bước.</summary>
    public Dictionary<string, ProfileStepSetting> Steps { get; set; } =
        WorkflowSteps.Ordered.ToDictionary(WorkflowSteps.Code, s => new ProfileStepSetting { Permission = WorkflowPermissions.DefaultFor(s) });

    /// <summary>Khóa = mã cổng (<see cref="WorkflowGates"/>); cổng thiếu lấy mặc định theo chế độ bước quyết định.</summary>
    public Dictionary<string, GateSetting> Gates { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Cổng chưa cấu hình trả mặc định theo chế độ bước quyết định.</summary>
    public GateMode GateModeOf(string gate) =>
        Gates != null && Gates.TryGetValue(gate, out var setting) && setting != null
            ? setting.Mode
            : WorkflowGates.DefaultMode(gate, Mode(WorkflowStep.B4_DECISION));

    /// <summary>Thiếu thì trả mặc định: nội bộ, quyền mặc định.</summary>
    public ProfileStepSetting Step(WorkflowStep step) =>
        Steps.TryGetValue(WorkflowSteps.Code(step), out var setting) && setting != null
            ? setting
            : new ProfileStepSetting { Permission = WorkflowPermissions.DefaultFor(step) };

    public StepMode Mode(WorkflowStep step) => Step(step).Mode;

    /// <summary>Nội bộ hoặc cấp trên thực hiện.</summary>
    public bool IsActive(WorkflowStep step) => Mode(step) != StepMode.Off;

    public IReadOnlySet<WorkflowStep> ActiveSteps() => WorkflowSteps.Ordered.Where(IsActive).ToHashSet();

    /// <summary>Bước của chủ hồ sơ → <c>evaluation.self</c>; cấp trên → <c>evaluation.external.record</c>;
    /// nội bộ → quyền cấu hình; không áp dụng → null.</summary>
    public string? PermissionFor(WorkflowStep step)
    {
        var setting = Step(step);
        return setting.Mode switch
        {
            StepMode.Off => null,
            StepMode.External => WorkflowPermissions.ExternalRecord,
            _ => WorkflowSteps.OwnerSteps.Contains(step)
                ? WorkflowPermissions.Self
                : string.IsNullOrWhiteSpace(setting.Permission) ? WorkflowPermissions.DefaultFor(step) : setting.Permission.Trim()
        };
    }

    public DateOnly? Deadline(WorkflowStep step) => Step(step).Deadline;

    /// <summary>Các bước khác chế độ hoặc quyền (không xét thời hạn); chỉ đổi diện quyết định được khi hồ sơ
    /// chưa qua bước bị ảnh hưởng.</summary>
    public IReadOnlyList<WorkflowStep> DifferingSteps(WorkflowProfile other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return WorkflowSteps.Ordered
            .Where(s => Mode(s) != other.Mode(s) || !string.Equals(PermissionFor(s), other.PermissionFor(s), StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>Đủ 9 bước theo thứ tự; quyền chỉ giữ ở bước nội bộ không phải của chủ hồ sơ.</summary>
    public WorkflowProfile Normalize()
    {
        Code = Code?.Trim().ToLowerInvariant() ?? string.Empty;
        Name = Name?.Trim() ?? string.Empty;
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();

        var source = Steps ?? new Dictionary<string, ProfileStepSetting>();
        var known = new Dictionary<WorkflowStep, ProfileStepSetting>();
        var unknown = new List<KeyValuePair<string, ProfileStepSetting>>();
        foreach (var (key, value) in source)
        {
            var step = WorkflowSteps.Parse(key);
            if (step.HasValue)
                known[step.Value] = value ?? new ProfileStepSetting();
            else
                unknown.Add(new KeyValuePair<string, ProfileStepSetting>(key, value ?? new ProfileStepSetting()));
        }

        var steps = new Dictionary<string, ProfileStepSetting>(StringComparer.Ordinal);
        foreach (var step in WorkflowSteps.Ordered)
        {
            var setting = known.TryGetValue(step, out var found) ? found : new ProfileStepSetting();
            if (setting.Mode != StepMode.Internal || WorkflowSteps.OwnerSteps.Contains(step))
                setting.Permission = null;
            else
                setting.Permission = string.IsNullOrWhiteSpace(setting.Permission) ? WorkflowPermissions.DefaultFor(step) : setting.Permission.Trim();
            steps[WorkflowSteps.Code(step)] = setting;
        }
        foreach (var (key, value) in unknown)
            steps.TryAdd(key, value);

        Steps = steps;

        // Mã cổng lạ giữ nguyên để Validate báo lỗi; cổng thiếu được điền mặc định để đọc được cấu hình cũ chưa có cổng.
        var gates = new Dictionary<string, GateSetting>(StringComparer.Ordinal);
        foreach (var (key, value) in Gates ?? new Dictionary<string, GateSetting>())
            gates.TryAdd(key?.Trim().ToUpperInvariant() ?? string.Empty, value ?? new GateSetting());
        foreach (var gate in WorkflowGates.Ordered)
        {
            if (!gates.ContainsKey(gate))
                gates[gate] = new GateSetting { Mode = WorkflowGates.DefaultMode(gate, Mode(WorkflowStep.B4_DECISION)) };
        }
        Gates = gates;
        return this;
    }

    /// <summary>Bước không nêu: nội bộ, quyền mặc định.</summary>
    public static WorkflowProfile Create(string code, string name, string? description,
        IReadOnlyDictionary<WorkflowStep, StepMode>? modes = null,
        IReadOnlyDictionary<WorkflowStep, string>? permissions = null)
    {
        var profile = new WorkflowProfile { Code = code, Name = name, Description = description };
        foreach (var step in WorkflowSteps.Ordered)
        {
            var setting = profile.Steps[WorkflowSteps.Code(step)];
            if (modes != null && modes.TryGetValue(step, out var mode))
                setting.Mode = mode;
            if (permissions != null && permissions.TryGetValue(step, out var permission))
                setting.Permission = permission;
        }
        return profile.Normalize();
    }
}

/// <summary>Cấu hình kỳ (jsonb <c>EvaluationPeriod.Settings</c>), xem docs/thiet-ke/luong-danh-gia.md. Tiêu chí và
/// tham số chấm điểm thuộc bộ tiêu chí của kỳ (<see cref="CriteriaSnapshot"/>).</summary>
public sealed class PeriodSettings
{
    /// <summary>Bản 1 (theo bước cấp kỳ) không còn được hỗ trợ; bản 2 (chưa có cổng) được đọc rồi nâng lên bản 3.</summary>
    public const int CurrentSchemaVersion = 3;

    /// <summary>Bản cũ nhất còn đọc được.</summary>
    public const int MinSchemaVersion = 2;

    public const int MaxProfiles = 20;

    /// <summary>Diện Đảng ủy cơ sở.</summary>
    public const string ProfileBase = "co-so";

    /// <summary>Diện BTV Đảng ủy Tổng công ty.</summary>
    public const string ProfileUpper = "cap-tren";

    /// <summary>Bí thư/Phó bí thư Chi bộ là nhân viên.</summary>
    public const string ProfileCellSecretaryStaff = "bi-thu-nhan-vien";

    /// <summary>Phó Giám đốc: Chủ tịch nhận xét trước khi cấp trên thẩm định, BTV quyết định.</summary>
    public const string ProfileDeputyDirector = "pho-giam-doc";

    private static readonly Regex CodePattern = new("^[a-z0-9][a-z0-9-]{0,49}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Chữ thường không dấu, số, gạch nối; tối đa 50 ký tự.</summary>
    public static bool IsValidProfileCode(string? code) => code != null && CodePattern.IsMatch(code);

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<WorkflowProfile> Profiles { get; set; } = new();

    /// <summary>Khóa = <see cref="ApprovalAuthority"/> (<c>CoSo</c>, <c>CapTren</c>), giá trị = mã diện quyết định; dùng khi thêm người vào kỳ.</summary>
    public Dictionary<string, string> DefaultProfiles { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Chặn hoàn thành bước khi đã quá hạn.</summary>
    public bool EnforceDeadlines { get; set; }

    /// <summary>Điền hạn cho mọi bước đang áp dụng có quy tắc hợp lệ, ghi đè ngày hiện có; bước không có quy tắc giữ nguyên.</summary>
    public PeriodSettings ApplyDeadlineRules(int year, int quarter, IReadOnlyCollection<DateOnly>? holidays = null)
    {
        foreach (var setting in (Profiles ?? new()).SelectMany(p => p.Steps.Values))
        {
            if (setting.Rule != null && setting.Mode != StepMode.Off && setting.Rule.Validate() == null)
                setting.Deadline = DeadlineCalculator.Compute(setting.Rule, year, quarter, holidays);
        }
        return this;
    }

    #region Cấu hình mặc định

    /// <summary>Cấu hình kỳ mới: 4 diện quyết định mặc định, chờ nghiệp vụ xác nhận; sửa được trong giao diện kỳ.</summary>
    public static PeriodSettings Default()
    {
        var settings = new PeriodSettings
        {
            Profiles = new List<WorkflowProfile>
            {
                WorkflowProfile.Create(ProfileBase, "Diện Đảng ủy cơ sở",
                    "Tập thể lãnh đạo Phòng đề xuất, Phòng TCCB-LĐ thẩm định, Giám đốc nhận xét, Đảng ủy cơ sở quyết định."),
                WorkflowProfile.Create(ProfileUpper, "Diện BTV Đảng ủy Tổng công ty",
                    "Chủ tịch, Giám đốc, Kiểm soát viên: HĐTV Tổng công ty duyệt danh mục sản phẩm; tập thể lãnh đạo Công ty đề xuất "
                    + "trong hệ thống; thẩm định (Ban TCĐU), nhận xét (HĐTV), quyết định (BTV Đảng ủy Tổng công ty) do cấp trên thực hiện "
                    + "— hệ thống ghi nhận kết quả.",
                    new Dictionary<WorkflowStep, StepMode>
                    {
                        [WorkflowStep.B1_APPROVE] = StepMode.External,
                        [WorkflowStep.B3B_APPRAISAL] = StepMode.External,
                        [WorkflowStep.B3C_DIRECTOR] = StepMode.External,
                        [WorkflowStep.B4_DECISION] = StepMode.External
                    }),
                WorkflowProfile.Create(ProfileCellSecretaryStaff, "Bí thư/Phó bí thư Chi bộ là nhân viên",
                    "Không họp bỏ phiếu: Trưởng phòng đề xuất mức (thay Giám đốc) trên ý kiến của Phó trưởng phòng, "
                    + "Phòng TCCB-LĐ thẩm định, Đảng ủy cơ sở quyết định.",
                    modes: new Dictionary<WorkflowStep, StepMode>
                    {
                        [WorkflowStep.B3A_COLLECTIVE] = StepMode.Off
                    },
                    permissions: new Dictionary<WorkflowStep, string>
                    {
                        [WorkflowStep.B3C_DIRECTOR] = WorkflowPermissions.UnitReview
                    }),
                // Chủ tịch nhận xét (ngày 14) trước khi Ban TCĐU thẩm định (ngày 15), mà bước thẩm định luôn đứng trước
                // bước nhận xét nên thẩm định được ghi nhận cùng quyết định của BTV.
                WorkflowProfile.Create(ProfileDeputyDirector, "Phó Giám đốc",
                    "Chủ tịch duyệt danh mục sản phẩm; tập thể lãnh đạo Công ty đề xuất, Chủ tịch nhận xét, đề xuất trong hệ thống; "
                    + "thẩm định (Ban TCĐU) và quyết định (BTV Đảng ủy Tổng công ty) do cấp trên thực hiện — hệ thống ghi nhận kết quả.",
                    new Dictionary<WorkflowStep, StepMode>
                    {
                        [WorkflowStep.B3B_APPRAISAL] = StepMode.Off,
                        [WorkflowStep.B4_DECISION] = StepMode.External
                    })
            },
            DefaultProfiles = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [nameof(ApprovalAuthority.CoSo)] = ProfileBase,
                [nameof(ApprovalAuthority.CapTren)] = ProfileUpper
            }
        };
        foreach (var profile in settings.Profiles)
        {
            // Diện Phó Giám đốc và diện cấp trên làm đăng ký/duyệt ngay đầu quý; diện cơ sở làm trước quý.
            var companyLevel = profile.Code is ProfileUpper or ProfileDeputyDirector;
            SetDefaultRules(profile, companyLevel
                ? new DeadlineSet(Quarter(1), Quarter(5), 9, 14, 15, profile.Code == ProfileUpper ? 17 : 14, 18)
                : new DeadlineSet(BeforeQuarter(6), BeforeQuarter(2), 9, 10, 11, 14, 14));
        }
        return settings;
    }

    private static DeadlineRule Quarter(int workingDay) => new() { Kind = DeadlineRuleKind.WorkingDayOfQuarter, Value = workingDay };

    // "Ngày làm việc cuối quý trước − k" trong kế hoạch là lùi k ngày làm việc kể từ ngày cuối đó.
    private static DeadlineRule BeforeQuarter(int workingDays) => new() { Kind = DeadlineRuleKind.WorkingDaysBeforeQuarter, Value = workingDays };

    private sealed record DeadlineSet(DeadlineRule Register, DeadlineRule Approve, int SelfScore, int Collective, int Appraisal, int Review, int Decision);

    // Thời hạn mặc định theo kế hoạch đánh giá quý của Phòng TCCB-LĐ; chi bộ xác nhận cùng ngày tự chấm.
    private static void SetDefaultRules(WorkflowProfile profile, DeadlineSet set)
    {
        static DeadlineRule Day(int day) => new() { Kind = DeadlineRuleKind.DayOfLastMonth, Value = day };
        var rules = new Dictionary<WorkflowStep, DeadlineRule>
        {
            [WorkflowStep.B1_REGISTER] = set.Register,
            [WorkflowStep.B1_APPROVE] = set.Approve,
            [WorkflowStep.B2_SELF_SCORE] = Day(set.SelfScore),
            [WorkflowStep.B2_CELL_CONFIRM] = Day(set.SelfScore),
            [WorkflowStep.B3A_COLLECTIVE] = Day(set.Collective),
            [WorkflowStep.B3B_APPRAISAL] = Day(set.Appraisal),
            [WorkflowStep.B3C_DIRECTOR] = Day(set.Review),
            [WorkflowStep.B4_DECISION] = Day(set.Decision)
        };
        foreach (var (step, rule) in rules)
        {
            if (profile.IsActive(step))
                profile.Step(step).Rule = rule;
        }
    }

    #endregion

    #region Đọc / ghi

    /// <summary>Chuỗi rỗng → <see cref="Default"/>; JSON sai → <see cref="FormatException"/>.</summary>
    public static PeriodSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Default();

        PeriodSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<PeriodSettings>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Cấu hình kỳ không đúng định dạng JSON.", ex);
        }

        return (settings ?? Default()).Normalize();
    }

    /// <summary>Diện quyết định đủ 9 bước, khóa cấp quyết định đúng tên.</summary>
    public PeriodSettings Normalize()
    {
        if (SchemaVersion >= MinSchemaVersion && SchemaVersion < CurrentSchemaVersion)
            SchemaVersion = CurrentSchemaVersion;

        Profiles = (Profiles ?? new List<WorkflowProfile>()).Where(p => p != null).Select(p => p.Normalize()).ToList();

        var defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in DefaultProfiles ?? new Dictionary<string, string>())
        {
            var name = Enum.TryParse<ApprovalAuthority>(key?.Trim(), true, out var authority) && Enum.IsDefined(authority)
                ? authority.ToString()
                : key?.Trim() ?? string.Empty;
            defaults[name] = value?.Trim().ToLowerInvariant() ?? string.Empty;
        }
        DefaultProfiles = defaults;
        return this;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public PeriodSettings Clone() => Parse(ToJson());

    #endregion

    #region Tra cứu

    public WorkflowProfile? FindProfile(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Profiles.FirstOrDefault(p => string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Null nếu chưa cấu hình hoặc diện quyết định không tồn tại.</summary>
    public string? DefaultProfileCode(ApprovalAuthority authority) =>
        DefaultProfiles.TryGetValue(authority.ToString(), out var code) && FindProfile(code) != null ? FindProfile(code)!.Code : null;

    /// <summary>Theo mã đã lưu → mặc định theo cấp quyết định → diện quyết định đầu tiên → luồng nội bộ đủ bước
    /// (phòng lỗi dữ liệu).</summary>
    public WorkflowProfile ResolveProfile(string? code, ApprovalAuthority authority) =>
        FindProfile(code)
        ?? FindProfile(DefaultProfileCode(authority))
        ?? Profiles.FirstOrDefault()
        ?? WorkflowProfile.Create("mac-dinh", "Mặc định", null);

    /// <summary>Mặc định khi thêm người vào kỳ: diện quyết định gợi ý theo chức vụ nếu kỳ có, ngược lại theo cấp quyết định.</summary>
    public WorkflowProfile ResolveDefaultProfile(ApprovalAuthority authority, string? positionProfileCode) =>
        FindProfile(positionProfileCode) ?? ResolveProfile(null, authority);

    #endregion

    #region Kiểm tra

    /// <summary>Trả danh sách lỗi (rỗng = hợp lệ). Mã quyền kiểm ở tầng Application qua <paramref name="isAllowedPermission"/>.</summary>
    public List<string> Validate(Func<string, bool>? isAllowedPermission = null)
    {
        var errors = new List<string>();
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Phiên bản cấu hình {SchemaVersion} không được hỗ trợ (chỉ hỗ trợ phiên bản {MinSchemaVersion} trở lên — cấu hình theo diện quyết định).");

        var profiles = Profiles ?? new List<WorkflowProfile>();
        if (profiles.Count == 0)
            errors.Add("Kỳ phải có ít nhất một diện quyết định.");
        if (profiles.Count > MaxProfiles)
            errors.Add($"Một kỳ có tối đa {MaxProfiles} diện quyết định.");

        foreach (var duplicate in profiles.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Mã diện quyết định \"{duplicate.Key}\" bị trùng.");

        foreach (var profile in profiles)
            errors.AddRange(ValidateProfile(profile, isAllowedPermission));

        foreach (var authority in new[] { ApprovalAuthority.CoSo, ApprovalAuthority.CapTren })
        {
            var label = authority == ApprovalAuthority.CoSo ? "Đảng ủy cơ sở quyết định" : "cấp trên quyết định";
            if (!DefaultProfiles.TryGetValue(authority.ToString(), out var code) || string.IsNullOrWhiteSpace(code))
                errors.Add($"Hãy chọn diện quyết định mặc định cho người thuộc diện {label}.");
            else if (FindProfile(code) == null)
                errors.Add($"Diện quyết định mặc định \"{code}\" (diện {label}) không tồn tại.");
        }
        foreach (var key in DefaultProfiles.Keys.Where(k => !Enum.TryParse<ApprovalAuthority>(k, false, out _)))
            errors.Add($"Cấp quyết định \"{key}\" không hợp lệ (chỉ CoSo hoặc CapTren).");

        return errors;
    }

    private static IEnumerable<string> ValidateProfile(WorkflowProfile profile, Func<string, bool>? isAllowedPermission)
    {
        var label = string.IsNullOrWhiteSpace(profile.Name) ? profile.Code : profile.Name;
        if (!CodePattern.IsMatch(profile.Code ?? string.Empty))
            yield return $"Mã diện quyết định \"{profile.Code}\" không hợp lệ: chỉ gồm chữ thường không dấu, số, gạch nối (tối đa 50 ký tự).";
        if (string.IsNullOrWhiteSpace(profile.Name))
            yield return $"Diện quyết định \"{profile.Code}\" chưa có tên.";
        else if (profile.Name.Length > 200)
            yield return $"Tên diện quyết định \"{profile.Code}\" không được dài quá 200 ký tự.";
        if (profile.Description is { Length: > 1000 })
            yield return $"Mô tả diện quyết định \"{label}\" không được dài quá 1000 ký tự.";

        foreach (var (stepKey, stepSetting) in profile.Steps ?? new Dictionary<string, ProfileStepSetting>())
        {
            if (stepSetting?.Rule?.Validate() is { } ruleError)
                yield return $"Diện quyết định \"{label}\": quy tắc thời hạn của bước \"{stepKey}\" không hợp lệ — {ruleError}";
        }

        foreach (var key in (profile.Steps ?? new Dictionary<string, ProfileStepSetting>()).Keys.Where(k => WorkflowSteps.Parse(k) == null))
            yield return $"Diện quyết định \"{label}\": mã bước \"{key}\" không tồn tại.";

        foreach (var step in WorkflowSteps.Ordered)
        {
            var setting = profile.Step(step);
            var name = WorkflowSteps.DisplayName(step);
            if (!Enum.IsDefined(setting.Mode))
            {
                yield return $"Diện quyết định \"{label}\": chế độ của bước \"{name}\" không hợp lệ.";
                continue;
            }
            if (setting.Mode == StepMode.Off && WorkflowSteps.Mandatory.Contains(step))
                yield return $"Diện quyết định \"{label}\": bước \"{name}\" là bước bắt buộc, không đặt \"Không áp dụng\" được.";
            if (setting.Mode == StepMode.External && WorkflowSteps.NeverExternal.Contains(step))
                yield return $"Diện quyết định \"{label}\": bước \"{name}\" không giao cho cấp trên thực hiện được.";
            if (setting.Mode == StepMode.Internal && !WorkflowSteps.OwnerSteps.Contains(step) && isAllowedPermission != null
                && (string.IsNullOrWhiteSpace(setting.Permission) || !isAllowedPermission(setting.Permission)))
                yield return $"Diện quyết định \"{label}\": quyền thực hiện bước \"{name}\" (\"{setting.Permission}\") không hợp lệ — hãy chọn một quyền đánh giá trong danh mục quyền.";
        }

        foreach (var (gate, gateSetting) in profile.Gates ?? new Dictionary<string, GateSetting>())
        {
            if (!WorkflowGates.IsKnown(gate))
                yield return $"Diện quyết định \"{label}\": mã cổng \"{gate}\" không tồn tại.";
            else if (gateSetting == null || !Enum.IsDefined(gateSetting.Mode) || !WorkflowGates.IsAllowed(gate, gateSetting.Mode))
                yield return $"Diện quyết định \"{label}\": chế độ của cổng \"{WorkflowGates.DisplayName(gate)}\" không hợp lệ.";
        }

        if (profile.IsActive(WorkflowStep.B1_APPROVE) && !profile.IsActive(WorkflowStep.B1_REGISTER))
            yield return $"Diện quyết định \"{label}\": không thể áp dụng bước duyệt danh mục sản phẩm khi bước đăng ký sản phẩm không áp dụng.";
    }

    /// <summary>Kỳ đã mở chỉ được sửa thời hạn.</summary>
    public bool DiffersOnlyInDeadlines(PeriodSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return WithoutDeadlines(this) == WithoutDeadlines(other);
    }

    private static string WithoutDeadlines(PeriodSettings settings)
    {
        var copy = settings.Clone();
        foreach (var setting in copy.Profiles.SelectMany(p => p.Steps.Values))
        {
            setting.Deadline = null;
            setting.Rule = null;
        }
        return copy.ToJson();
    }

    #endregion
}
