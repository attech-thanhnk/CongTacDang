using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Chế độ thực hiện một bước trong hồ sơ luồng.</summary>
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

    /// <summary>Quy tắc điền ngày hạn theo quý (null = nhập tay). Chỉ ghi vào <see cref="Deadline"/> khi người dùng
    /// yêu cầu (<see cref="PeriodSettings.ApplyDeadlineRules"/>), không tự tính lại.</summary>
    public DeadlineRule? Rule { get; set; }
}

/// <summary>Cấu hình bước cho một nhóm đối tượng (ví dụ Diện Đảng ủy cơ sở). Hồ sơ đánh giá lưu mã hồ sơ luồng
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

    /// <summary>Các bước khác chế độ hoặc quyền (không xét thời hạn); chỉ đổi hồ sơ luồng được khi hồ sơ
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
    /// <summary>Bản 1 (theo bước cấp kỳ) không còn được hỗ trợ.</summary>
    public const int CurrentSchemaVersion = 2;

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

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<WorkflowProfile> Profiles { get; set; } = new();

    /// <summary>Khóa = <see cref="ApprovalAuthority"/> (<c>CoSo</c>, <c>CapTren</c>), giá trị = mã hồ sơ luồng; dùng khi thêm người vào kỳ.</summary>
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

    /// <summary>09A cần bước đăng ký, duyệt sản phẩm; 09B chấm theo trục, không đăng ký. Chưa chọn bộ (null) → có đăng ký.</summary>
    public static bool UsesRegistration(string? selfScoreForm) =>
        !string.Equals(selfScoreForm, CriteriaSetContent.Form09B, StringComparison.OrdinalIgnoreCase);

    /// <summary>Cấu hình kỳ mới: 4 hồ sơ luồng mặc định, chờ nghiệp vụ xác nhận; sửa được trong giao diện kỳ.</summary>
    public static PeriodSettings Default(string? selfScoreForm = null)
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
                    "Tập thể lãnh đạo Phòng đề xuất, Trưởng phòng đề xuất mức (thay Giám đốc), "
                    + "Phòng TCCB-LĐ thẩm định, Đảng ủy cơ sở quyết định.",
                    permissions: new Dictionary<WorkflowStep, string>
                    {
                        [WorkflowStep.B3C_DIRECTOR] = WorkflowPermissions.UnitReview
                    }),
                // Không có bước thẩm định riêng: Ban TCĐU thẩm định ở cấp trên, kết quả ghi nhận cùng bước quyết định.
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
        return settings.AlignWithCriteria(selfScoreForm);
    }

    /// <summary>Có đăng ký (09A): bước đăng ký nội bộ, bước duyệt đang "Không áp dụng" thì bật lại. Không đăng ký (09B):
    /// cả hai bước "Không áp dụng". Bước khác giữ nguyên.</summary>
    public PeriodSettings AlignWithCriteria(string? selfScoreForm)
    {
        var registration = UsesRegistration(selfScoreForm);
        foreach (var profile in Profiles ?? new())
        {
            var approve = profile.Step(WorkflowStep.B1_APPROVE);
            profile.Steps[WorkflowSteps.Code(WorkflowStep.B1_REGISTER)] = new ProfileStepSetting
            {
                Mode = registration ? StepMode.Internal : StepMode.Off,
                Deadline = profile.Step(WorkflowStep.B1_REGISTER).Deadline,
                Rule = profile.Step(WorkflowStep.B1_REGISTER).Rule
            };
            if (!registration)
                approve.Mode = StepMode.Off;
            else if (approve.Mode == StepMode.Off)
                approve.Mode = StepMode.Internal;
            profile.Steps[WorkflowSteps.Code(WorkflowStep.B1_APPROVE)] = approve;
            profile.Normalize();
        }
        return this;
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

    /// <summary>Hồ sơ luồng đủ 9 bước, khóa cấp quyết định đúng tên.</summary>
    public PeriodSettings Normalize()
    {
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

    /// <summary>Null nếu chưa cấu hình hoặc hồ sơ luồng không tồn tại.</summary>
    public string? DefaultProfileCode(ApprovalAuthority authority) =>
        DefaultProfiles.TryGetValue(authority.ToString(), out var code) && FindProfile(code) != null ? FindProfile(code)!.Code : null;

    /// <summary>Theo mã đã lưu → mặc định theo cấp quyết định → hồ sơ luồng đầu tiên → luồng nội bộ đủ bước
    /// (phòng lỗi dữ liệu).</summary>
    public WorkflowProfile ResolveProfile(string? code, ApprovalAuthority authority) =>
        FindProfile(code)
        ?? FindProfile(DefaultProfileCode(authority))
        ?? Profiles.FirstOrDefault()
        ?? WorkflowProfile.Create("mac-dinh", "Mặc định", null);

    #endregion

    #region Kiểm tra

    /// <summary>Trả danh sách lỗi (rỗng = hợp lệ). Mã quyền kiểm ở tầng Application qua <paramref name="isAllowedPermission"/>;
    /// <paramref name="selfScoreForm"/> null = chưa chọn bộ.</summary>
    public List<string> Validate(Func<string, bool>? isAllowedPermission = null, string? selfScoreForm = null)
    {
        var errors = new List<string>();
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Phiên bản cấu hình {SchemaVersion} không được hỗ trợ (chỉ hỗ trợ phiên bản {CurrentSchemaVersion} — cấu hình theo cách xét duyệt).");

        var profiles = Profiles ?? new List<WorkflowProfile>();
        if (profiles.Count == 0)
            errors.Add("Kỳ phải có ít nhất một cách xét duyệt.");
        if (profiles.Count > MaxProfiles)
            errors.Add($"Một kỳ có tối đa {MaxProfiles} cách xét duyệt.");

        foreach (var duplicate in profiles.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Mã cách xét duyệt \"{duplicate.Key}\" bị trùng.");

        foreach (var profile in profiles)
            errors.AddRange(ValidateProfile(profile, isAllowedPermission));

        foreach (var authority in new[] { ApprovalAuthority.CoSo, ApprovalAuthority.CapTren })
        {
            var label = authority == ApprovalAuthority.CoSo ? "Đảng ủy cơ sở quyết định" : "cấp trên quyết định";
            if (!DefaultProfiles.TryGetValue(authority.ToString(), out var code) || string.IsNullOrWhiteSpace(code))
                errors.Add($"Hãy chọn cách xét duyệt mặc định cho người thuộc diện {label}.");
            else if (FindProfile(code) == null)
                errors.Add($"Cách xét duyệt mặc định \"{code}\" (diện {label}) không tồn tại.");
        }
        foreach (var key in DefaultProfiles.Keys.Where(k => !Enum.TryParse<ApprovalAuthority>(k, false, out _)))
            errors.Add($"Cấp quyết định \"{key}\" không hợp lệ (chỉ CoSo hoặc CapTren).");

        // Bước đăng ký, duyệt đi theo bộ tiêu chí (AlignWithCriteria đặt sẵn khi tạo kỳ, đổi bộ).
        if (selfScoreForm != null && UsesRegistration(selfScoreForm))
        {
            foreach (var profile in profiles.Where(p => !p.IsActive(WorkflowStep.B1_REGISTER)))
                errors.Add($"Cách xét duyệt \"{profile.Name}\": bộ tiêu chí của kỳ tự chấm theo Mẫu {selfScoreForm}, cần danh mục sản phẩm "
                    + "(Mẫu 01) — bước đăng ký sản phẩm phải áp dụng.");
        }
        else if (selfScoreForm != null)
        {
            foreach (var profile in profiles.Where(p => p.IsActive(WorkflowStep.B1_REGISTER) || p.IsActive(WorkflowStep.B1_APPROVE)))
                errors.Add($"Cách xét duyệt \"{profile.Name}\": bộ tiêu chí của kỳ tự chấm theo Mẫu {selfScoreForm}, không đăng ký sản phẩm — "
                    + "bước đăng ký, duyệt sản phẩm phải \"Không áp dụng\".");
        }

        return errors;
    }

    private static IEnumerable<string> ValidateProfile(WorkflowProfile profile, Func<string, bool>? isAllowedPermission)
    {
        var label = string.IsNullOrWhiteSpace(profile.Name) ? profile.Code : profile.Name;
        if (!CodePattern.IsMatch(profile.Code ?? string.Empty))
            yield return $"Mã cách xét duyệt \"{profile.Code}\" không hợp lệ: chỉ gồm chữ thường không dấu, số, gạch nối (tối đa 50 ký tự).";
        if (string.IsNullOrWhiteSpace(profile.Name))
            yield return $"Cách xét duyệt \"{profile.Code}\" chưa có tên.";
        else if (profile.Name.Length > 200)
            yield return $"Tên cách xét duyệt \"{profile.Code}\" không được dài quá 200 ký tự.";
        if (profile.Description is { Length: > 1000 })
            yield return $"Mô tả cách xét duyệt \"{label}\" không được dài quá 1000 ký tự.";

        foreach (var (stepKey, stepSetting) in profile.Steps ?? new Dictionary<string, ProfileStepSetting>())
        {
            if (stepSetting?.Rule?.Validate() is { } ruleError)
                yield return $"Cách xét duyệt \"{label}\": quy tắc thời hạn của bước \"{stepKey}\" không hợp lệ — {ruleError}";
        }

        foreach (var key in (profile.Steps ?? new Dictionary<string, ProfileStepSetting>()).Keys.Where(k => WorkflowSteps.Parse(k) == null))
            yield return $"Cách xét duyệt \"{label}\": mã bước \"{key}\" không tồn tại.";

        foreach (var step in WorkflowSteps.Ordered)
        {
            var setting = profile.Step(step);
            var name = WorkflowSteps.DisplayName(step);
            if (!Enum.IsDefined(setting.Mode))
            {
                yield return $"Cách xét duyệt \"{label}\": chế độ của bước \"{name}\" không hợp lệ.";
                continue;
            }
            if (setting.Mode == StepMode.Off && WorkflowSteps.Mandatory.Contains(step))
                yield return $"Cách xét duyệt \"{label}\": bước \"{name}\" là bước bắt buộc, không đặt \"Không áp dụng\" được.";
            if (setting.Mode == StepMode.External && WorkflowSteps.NeverExternal.Contains(step))
                yield return $"Cách xét duyệt \"{label}\": bước \"{name}\" không giao cho cấp trên thực hiện được.";
            if (setting.Mode == StepMode.Internal && !WorkflowSteps.OwnerSteps.Contains(step) && isAllowedPermission != null
                && (string.IsNullOrWhiteSpace(setting.Permission) || !isAllowedPermission(setting.Permission)))
                yield return $"Cách xét duyệt \"{label}\": quyền thực hiện bước \"{name}\" (\"{setting.Permission}\") không hợp lệ — hãy chọn một quyền đánh giá trong danh mục quyền.";
        }

        if (profile.IsActive(WorkflowStep.B1_APPROVE) && !profile.IsActive(WorkflowStep.B1_REGISTER))
            yield return $"Cách xét duyệt \"{label}\": không thể áp dụng bước duyệt danh mục sản phẩm khi bước đăng ký sản phẩm không áp dụng.";
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
