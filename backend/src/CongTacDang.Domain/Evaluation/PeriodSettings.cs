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
    /// <summary>Bước làm trong hệ thống bởi người có quyền thực hiện của bước.</summary>
    Internal = 0,

    /// <summary>
    /// Bước do cấp trên / cơ quan ngoài hệ thống thực hiện; người có quyền <c>evaluation.external.record</c>
    /// ghi nhận kết quả (cơ quan, số/ngày văn bản, nhận xét, mức, tệp đính kèm).
    /// </summary>
    External = 1,

    /// <summary>Bước không áp dụng cho nhóm đối tượng này (bỏ qua khi tính bước kế tiếp).</summary>
    Off = 2
}

/// <summary>
/// Mã quyền dùng làm giá trị mặc định trong cấu hình luồng (Domain không tham chiếu Application).
/// Phải trùng mã trong <c>Application/Common/Security/PermissionCodes</c> — có unit test đối chiếu.
/// </summary>
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

    /// <summary>Quyền thực hiện mặc định của bước khi ở chế độ nội bộ (theo bảng thiết kế luong-danh-gia.md mục 1).</summary>
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

/// <summary>Cấu hình một bước trong hồ sơ luồng.</summary>
public sealed class ProfileStepSetting
{
    /// <summary>Nội bộ / Cấp trên thực hiện / Không áp dụng.</summary>
    public StepMode Mode { get; set; } = StepMode.Internal;

    /// <summary>
    /// Mã quyền thực hiện bước khi <see cref="StepMode.Internal"/> (bước của chủ hồ sơ luôn là <c>evaluation.self</c>).
    /// Chế độ khác: null (bước cấp trên dùng quyền <c>evaluation.external.record</c>).
    /// </summary>
    public string? Permission { get; set; }

    /// <summary>Thời hạn (ngày). Chỉ hiển thị/cảnh báo, chỉ chặn khi <see cref="PeriodSettings.EnforceDeadlines"/> = true.</summary>
    public DateOnly? Deadline { get; set; }
}

/// <summary>
/// Hồ sơ luồng: cấu hình bước cho một nhóm đối tượng (ví dụ Diện Đảng ủy cơ sở, Diện BTV Đảng ủy Tổng công ty).
/// Mỗi hồ sơ đánh giá lưu mã hồ sơ luồng của mình (ảnh chụp khi thêm vào kỳ).
/// </summary>
public sealed class WorkflowProfile
{
    /// <summary>Mã (chữ thường, số, gạch nối; duy nhất trong kỳ).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên hiển thị.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mô tả (căn cứ, nhóm đối tượng áp dụng).</summary>
    public string? Description { get; set; }

    /// <summary>Cấu hình từng bước (khóa = mã bước).</summary>
    public Dictionary<string, ProfileStepSetting> Steps { get; set; } =
        WorkflowSteps.Ordered.ToDictionary(WorkflowSteps.Code, s => new ProfileStepSetting { Permission = WorkflowPermissions.DefaultFor(s) });

    /// <summary>Cấu hình của bước (mặc định: nội bộ, quyền mặc định).</summary>
    public ProfileStepSetting Step(WorkflowStep step) =>
        Steps.TryGetValue(WorkflowSteps.Code(step), out var setting) && setting != null
            ? setting
            : new ProfileStepSetting { Permission = WorkflowPermissions.DefaultFor(step) };

    /// <summary>Chế độ của bước.</summary>
    public StepMode Mode(WorkflowStep step) => Step(step).Mode;

    /// <summary>Bước có áp dụng (nội bộ hoặc cấp trên thực hiện).</summary>
    public bool IsActive(WorkflowStep step) => Mode(step) != StepMode.Off;

    /// <summary>Tập bước áp dụng (dùng cho máy trạng thái).</summary>
    public IReadOnlySet<WorkflowStep> ActiveSteps() => WorkflowSteps.Ordered.Where(IsActive).ToHashSet();

    /// <summary>
    /// Mã quyền thực hiện bước: bước của chủ hồ sơ → <c>evaluation.self</c>; cấp trên thực hiện →
    /// <c>evaluation.external.record</c>; nội bộ → quyền cấu hình; không áp dụng → null.
    /// </summary>
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

    /// <summary>Thời hạn của bước.</summary>
    public DateOnly? Deadline(WorkflowStep step) => Step(step).Deadline;

    /// <summary>
    /// Các bước mà hai hồ sơ luồng khác nhau về chế độ hoặc quyền thực hiện (không tính thời hạn) — dùng khi đổi hồ sơ luồng
    /// của một hồ sơ: chỉ được đổi khi hồ sơ chưa qua bước bị ảnh hưởng.
    /// </summary>
    public IReadOnlyList<WorkflowStep> DifferingSteps(WorkflowProfile other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return WorkflowSteps.Ordered
            .Where(s => Mode(s) != other.Mode(s) || !string.Equals(PermissionFor(s), other.PermissionFor(s), StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>Chuẩn hóa: đủ 9 bước (khóa đúng mã, đúng thứ tự), quyền chỉ giữ ở bước nội bộ không phải của chủ hồ sơ.</summary>
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

    /// <summary>Hồ sơ luồng với chế độ từng bước cho trước (bước không nêu: nội bộ, quyền mặc định).</summary>
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

/// <summary>Kiểu kỳ dựng sẵn khi tạo kỳ (mỗi kiểu sinh sẵn các hồ sơ luồng).</summary>
/// <param name="Code">Mã (dùng trong API).</param>
/// <param name="Name">Tên hiển thị.</param>
/// <param name="Description">Mô tả.</param>
public sealed record PeriodPreset(string Code, string Name, string Description)
{
    /// <summary>Cấu hình của kiểu kỳ.</summary>
    public PeriodSettings Build() => Code switch
    {
        PeriodSettings.PresetFull => PeriodSettings.FullPreset(),
        PeriodSettings.PresetTransitionQ3 => PeriodSettings.TransitionQ3Preset(),
        _ => throw new ArgumentOutOfRangeException(nameof(Code), Code, null)
    };
}

/// <summary>
/// Cấu hình theo kỳ (<c>EvaluationPeriod.Settings</c>, cột jsonb) — docs/thiet-ke/luong-danh-gia.md mục 3.2.
/// Luồng theo <b>hồ sơ luồng</b> (nhóm đối tượng): mỗi hồ sơ luồng đặt chế độ, quyền thực hiện, thời hạn của từng bước;
/// hồ sơ đánh giá dùng cấu hình bước của hồ sơ luồng của mình. Mẫu tự chấm và tham số nghiệp vụ chung cho cả kỳ.
/// </summary>
public sealed class PeriodSettings
{
    /// <summary>Phiên bản schema hiện tại (2 = theo hồ sơ luồng; bản 1 theo bước cấp kỳ không còn được hỗ trợ).</summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>Số hồ sơ luồng tối đa trong một kỳ.</summary>
    public const int MaxProfiles = 20;

    /// <summary>Mẫu tự chấm có Mẫu 01/02 (tính điểm A-B-C-D theo nhiệm vụ).</summary>
    public const string Form09A = "09A";

    /// <summary>Mẫu tự chấm trực tiếp 6 trục (Q3/2026).</summary>
    public const string Form09B = "09B";

    /// <summary>Mã kiểu kỳ "Đầy đủ".</summary>
    public const string PresetFull = "full";

    /// <summary>Mã kiểu kỳ "Quý III/2026 — chuyển tiếp".</summary>
    public const string PresetTransitionQ3 = "q3-2026-transition";

    /// <summary>Mã hồ sơ luồng "Diện Đảng ủy cơ sở" (PL III ví dụ 1).</summary>
    public const string ProfileBase = "co-so";

    /// <summary>Mã hồ sơ luồng "Diện BTV Đảng ủy Tổng công ty" (PL III ví dụ 2).</summary>
    public const string ProfileUpper = "cap-tren";

    /// <summary>Mã hồ sơ luồng "Bí thư/Phó bí thư Chi bộ là nhân viên" (PL III ví dụ 3).</summary>
    public const string ProfileCellSecretaryStaff = "bi-thu-nhan-vien";

    private static readonly Regex CodePattern = new("^[a-z0-9][a-z0-9-]{0,49}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Hai kiểu kỳ dựng sẵn.</summary>
    public static readonly IReadOnlyList<PeriodPreset> Presets = new[]
    {
        new PeriodPreset(PresetFull, "Đầy đủ",
            "Đủ các bước; tự chấm theo Mẫu 09A (có Mẫu 01/02). Sinh sẵn 3 hồ sơ luồng: Diện Đảng ủy cơ sở, "
            + "Diện BTV Đảng ủy Tổng công ty, Bí thư/Phó bí thư Chi bộ là nhân viên."),
        new PeriodPreset(PresetTransitionQ3, "Quý III/2026 — chuyển tiếp",
            "Như kiểu \"Đầy đủ\" nhưng không áp dụng đăng ký và duyệt sản phẩm (B1) ở mọi hồ sơ luồng; tự chấm trực tiếp 6 trục theo Mẫu 09B.")
    };

    /// <summary>Tùy chọn JSON: camelCase, không phân biệt hoa thường; khóa bước và khóa cấp quyết định giữ nguyên mã.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    /// <summary>Phiên bản schema của cấu hình.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Các hồ sơ luồng của kỳ.</summary>
    public List<WorkflowProfile> Profiles { get; set; } = new();

    /// <summary>
    /// Hồ sơ luồng mặc định theo cấp quyết định (khóa = <see cref="ApprovalAuthority"/>: <c>CoSo</c>, <c>CapTren</c>;
    /// giá trị = mã hồ sơ luồng) — dùng khi thêm người vào kỳ.
    /// </summary>
    public Dictionary<string, string> DefaultProfiles { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Chặn hoàn thành bước khi đã quá thời hạn.</summary>
    public bool EnforceDeadlines { get; set; }

    /// <summary>Mẫu tự chấm: <see cref="Form09A"/> hoặc <see cref="Form09B"/>.</summary>
    public string SelfScoreForm { get; set; } = Form09A;

    /// <summary>Tham số nghiệp vụ (mặc định = giá trị code đang dùng).</summary>
    public EvaluationParameters Parameters { get; set; } = new();

    #region Kiểu kỳ dựng sẵn

    /// <summary>
    /// Kiểu kỳ "Đầy đủ": 3 hồ sơ luồng theo PL III — cấu hình mặc định <b>chờ nghiệp vụ xác nhận</b>, sửa được trong giao diện kỳ.
    /// </summary>
    public static PeriodSettings FullPreset()
    {
        var settings = new PeriodSettings
        {
            Profiles = new List<WorkflowProfile>
            {
                // Ví dụ 1: B3a tập thể lãnh đạo Phòng, B3b Phòng TCCB-LĐ, B3c Giám đốc, B4 Đảng ủy cơ sở — tất cả trong hệ thống.
                WorkflowProfile.Create(ProfileBase, "Diện Đảng ủy cơ sở",
                    "PL III ví dụ 1: tập thể lãnh đạo Phòng đề xuất, Phòng TCCB-LĐ thẩm định, Giám đốc nhận xét, Đảng ủy cơ sở quyết định."),
                // Ví dụ 2: B3a tập thể lãnh đạo Công ty (nội bộ); thẩm định, nhận xét, quyết định ở cấp trên.
                WorkflowProfile.Create(ProfileUpper, "Diện BTV Đảng ủy Tổng công ty",
                    "PL III ví dụ 2: tập thể lãnh đạo Công ty đề xuất trong hệ thống; thẩm định (Ban TCĐU), nhận xét (HĐTV), "
                    + "quyết định (BTV Đảng ủy Tổng công ty) do cấp trên thực hiện — hệ thống ghi nhận kết quả.",
                    new Dictionary<WorkflowStep, StepMode>
                    {
                        [WorkflowStep.B3B_APPRAISAL] = StepMode.External,
                        [WorkflowStep.B3C_DIRECTOR] = StepMode.External,
                        [WorkflowStep.B4_DECISION] = StepMode.External
                    }),
                // Ví dụ 3: Trưởng phòng đề xuất thay bước cấp trực tiếp sử dụng của Giám đốc.
                WorkflowProfile.Create(ProfileCellSecretaryStaff, "Bí thư/Phó bí thư Chi bộ là nhân viên",
                    "PL III ví dụ 3: tập thể lãnh đạo Phòng đề xuất, Trưởng phòng đề xuất mức (thay Giám đốc), "
                    + "Phòng TCCB-LĐ thẩm định, Đảng ủy cơ sở quyết định.",
                    permissions: new Dictionary<WorkflowStep, string>
                    {
                        [WorkflowStep.B3C_DIRECTOR] = WorkflowPermissions.UnitReview
                    })
            },
            DefaultProfiles = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [nameof(ApprovalAuthority.CoSo)] = ProfileBase,
                [nameof(ApprovalAuthority.CapTren)] = ProfileUpper
            }
        };
        return settings;
    }

    /// <summary>Kiểu kỳ "Quý III/2026 — chuyển tiếp": như "Đầy đủ" nhưng B1 không áp dụng ở mọi hồ sơ luồng; tự chấm 09B.</summary>
    public static PeriodSettings TransitionQ3Preset()
    {
        var settings = FullPreset();
        settings.SelfScoreForm = Form09B;
        foreach (var profile in settings.Profiles)
        {
            profile.Step(WorkflowStep.B1_REGISTER).Mode = StepMode.Off;
            profile.Step(WorkflowStep.B1_APPROVE).Mode = StepMode.Off;
            profile.Normalize();
        }
        return settings;
    }

    /// <summary>Tìm kiểu kỳ theo mã; null nếu không có.</summary>
    public static PeriodPreset? FindPreset(string? code) =>
        Presets.FirstOrDefault(p => string.Equals(p.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    #endregion

    #region Đọc / ghi

    /// <summary>
    /// Đọc cấu hình từ JSON. Chuỗi rỗng → kiểu kỳ "Đầy đủ". JSON sai định dạng → <see cref="FormatException"/>.
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

    /// <summary>Chuẩn hóa: hồ sơ luồng đủ 9 bước, khóa cấp quyết định đúng tên, mẫu tự chấm viết hoa, tham số không null.</summary>
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

        Parameters ??= new EvaluationParameters();
        SelfScoreForm = string.IsNullOrWhiteSpace(SelfScoreForm) ? Form09A : SelfScoreForm.Trim().ToUpperInvariant();
        return this;
    }

    /// <summary>Ghi cấu hình ra JSON (lưu vào cột jsonb).</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Bản sao sâu (qua JSON).</summary>
    public PeriodSettings Clone() => Parse(ToJson());

    #endregion

    #region Tra cứu

    /// <summary>Hồ sơ luồng theo mã; null nếu không có.</summary>
    public WorkflowProfile? FindProfile(string? code) =>
        string.IsNullOrWhiteSpace(code)
            ? null
            : Profiles.FirstOrDefault(p => string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Mã hồ sơ luồng mặc định cho cấp quyết định (null nếu chưa cấu hình hoặc không tồn tại).</summary>
    public string? DefaultProfileCode(ApprovalAuthority authority) =>
        DefaultProfiles.TryGetValue(authority.ToString(), out var code) && FindProfile(code) != null ? FindProfile(code)!.Code : null;

    /// <summary>
    /// Hồ sơ luồng áp dụng cho một hồ sơ đánh giá: theo mã đã lưu trên hồ sơ; không có (mã rỗng/không còn) → mặc định theo
    /// cấp quyết định → hồ sơ luồng đầu tiên. Cấu hình không có hồ sơ luồng nào → luồng nội bộ đủ bước (phòng lỗi dữ liệu).
    /// </summary>
    public WorkflowProfile ResolveProfile(string? code, ApprovalAuthority authority) =>
        FindProfile(code)
        ?? FindProfile(DefaultProfileCode(authority))
        ?? Profiles.FirstOrDefault()
        ?? WorkflowProfile.Create("mac-dinh", "Mặc định", null);

    /// <summary>Tự chấm theo Mẫu 09B (chấm trực tiếp 6 trục).</summary>
    [JsonIgnore]
    public bool UsesAxisScoring => string.Equals(SelfScoreForm, Form09B, StringComparison.OrdinalIgnoreCase);

    #endregion

    #region Kiểm tra

    /// <summary>
    /// Kiểm tra cấu trúc cấu hình; trả danh sách lỗi (rỗng = hợp lệ). Mã quyền của bước nội bộ phải thuộc danh mục quyền
    /// — kiểm tra ở tầng Application (<paramref name="isAllowedPermission"/>).
    /// </summary>
    public List<string> Validate(Func<string, bool>? isAllowedPermission = null)
    {
        var errors = new List<string>();
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Phiên bản cấu hình {SchemaVersion} không được hỗ trợ (chỉ hỗ trợ phiên bản {CurrentSchemaVersion} — cấu hình theo hồ sơ luồng).");

        var profiles = Profiles ?? new List<WorkflowProfile>();
        if (profiles.Count == 0)
            errors.Add("Kỳ phải có ít nhất một hồ sơ luồng.");
        if (profiles.Count > MaxProfiles)
            errors.Add($"Một kỳ có tối đa {MaxProfiles} hồ sơ luồng.");

        foreach (var duplicate in profiles.GroupBy(p => p.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Mã hồ sơ luồng \"{duplicate.Key}\" bị trùng.");

        foreach (var profile in profiles)
            errors.AddRange(ValidateProfile(profile, isAllowedPermission));

        foreach (var authority in new[] { ApprovalAuthority.CoSo, ApprovalAuthority.CapTren })
        {
            var label = authority == ApprovalAuthority.CoSo ? "Đảng ủy cơ sở quyết định" : "cấp trên quyết định";
            if (!DefaultProfiles.TryGetValue(authority.ToString(), out var code) || string.IsNullOrWhiteSpace(code))
                errors.Add($"Hãy chọn hồ sơ luồng mặc định cho người thuộc diện {label}.");
            else if (FindProfile(code) == null)
                errors.Add($"Hồ sơ luồng mặc định \"{code}\" (diện {label}) không tồn tại.");
        }
        foreach (var key in DefaultProfiles.Keys.Where(k => !Enum.TryParse<ApprovalAuthority>(k, false, out _)))
            errors.Add($"Cấp quyết định \"{key}\" không hợp lệ (chỉ CoSo hoặc CapTren).");

        if (SelfScoreForm is not (Form09A or Form09B))
        {
            errors.Add($"Mẫu tự chấm phải là {Form09A} hoặc {Form09B}.");
        }
        else if (SelfScoreForm == Form09A)
        {
            foreach (var profile in profiles.Where(p => !p.IsActive(WorkflowStep.B1_REGISTER)))
                errors.Add($"Hồ sơ luồng \"{profile.Name}\": tự chấm theo Mẫu 09A cần danh mục sản phẩm (Mẫu 01) — hãy áp dụng bước đăng ký sản phẩm hoặc chọn Mẫu 09B.");
        }

        errors.AddRange((Parameters ?? new EvaluationParameters()).Validate());
        return errors;
    }

    private static IEnumerable<string> ValidateProfile(WorkflowProfile profile, Func<string, bool>? isAllowedPermission)
    {
        var label = string.IsNullOrWhiteSpace(profile.Name) ? profile.Code : profile.Name;
        if (!CodePattern.IsMatch(profile.Code ?? string.Empty))
            yield return $"Mã hồ sơ luồng \"{profile.Code}\" không hợp lệ: chỉ gồm chữ thường không dấu, số, gạch nối (tối đa 50 ký tự).";
        if (string.IsNullOrWhiteSpace(profile.Name))
            yield return $"Hồ sơ luồng \"{profile.Code}\" chưa có tên.";
        else if (profile.Name.Length > 200)
            yield return $"Tên hồ sơ luồng \"{profile.Code}\" không được dài quá 200 ký tự.";
        if (profile.Description is { Length: > 1000 })
            yield return $"Mô tả hồ sơ luồng \"{label}\" không được dài quá 1000 ký tự.";

        foreach (var key in (profile.Steps ?? new Dictionary<string, ProfileStepSetting>()).Keys.Where(k => WorkflowSteps.Parse(k) == null))
            yield return $"Hồ sơ luồng \"{label}\": mã bước \"{key}\" không tồn tại.";

        foreach (var step in WorkflowSteps.Ordered)
        {
            var setting = profile.Step(step);
            var name = WorkflowSteps.DisplayName(step);
            if (!Enum.IsDefined(setting.Mode))
            {
                yield return $"Hồ sơ luồng \"{label}\": chế độ của bước \"{name}\" không hợp lệ.";
                continue;
            }
            if (setting.Mode == StepMode.Off && WorkflowSteps.Mandatory.Contains(step))
                yield return $"Hồ sơ luồng \"{label}\": bước \"{name}\" là bước bắt buộc, không đặt \"Không áp dụng\" được.";
            if (setting.Mode == StepMode.External && WorkflowSteps.NeverExternal.Contains(step))
                yield return $"Hồ sơ luồng \"{label}\": bước \"{name}\" không giao cho cấp trên thực hiện được.";
            if (setting.Mode == StepMode.Internal && !WorkflowSteps.OwnerSteps.Contains(step) && isAllowedPermission != null
                && (string.IsNullOrWhiteSpace(setting.Permission) || !isAllowedPermission(setting.Permission)))
                yield return $"Hồ sơ luồng \"{label}\": quyền thực hiện bước \"{name}\" (\"{setting.Permission}\") không hợp lệ — hãy chọn một quyền đánh giá trong danh mục quyền.";
        }

        if (profile.IsActive(WorkflowStep.B1_APPROVE) && !profile.IsActive(WorkflowStep.B1_REGISTER))
            yield return $"Hồ sơ luồng \"{label}\": không thể áp dụng bước duyệt danh mục sản phẩm khi bước đăng ký sản phẩm không áp dụng.";
    }

    /// <summary>
    /// Hai cấu hình chỉ khác nhau ở thời hạn (dùng khi kỳ đã mở: chỉ được sửa thời hạn).
    /// </summary>
    public bool DiffersOnlyInDeadlines(PeriodSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return WithoutDeadlines(this) == WithoutDeadlines(other);
    }

    private static string WithoutDeadlines(PeriodSettings settings)
    {
        var copy = settings.Clone();
        foreach (var setting in copy.Profiles.SelectMany(p => p.Steps.Values))
            setting.Deadline = null;
        return copy.ToJson();
    }

    #endregion
}
