using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CongTacDang.Domain.Enums;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Cách chấm tiêu chí con của một nhóm tiêu chí chung.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CriteriaScoringMode>))]
public enum CriteriaScoringMode
{
    /// <summary>"Đảm bảo" = điểm tối đa, "Không đảm bảo" = 0.</summary>
    Binary = 0,

    /// <summary>Nhập điểm từ 0 đến điểm tối đa.</summary>
    Range = 1
}

/// <summary>Cách xử lý điểm khi tiêu chí con được đánh dấu "K/AD — Không áp dụng".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotApplicableRule>))]
public enum NotApplicableRule
{
    /// <summary>Bỏ tiêu chí khỏi mẫu số rồi quy đổi về điểm tối đa của nhóm.</summary>
    ExcludeAndRescale = 0,

    /// <summary>Tính như đạt điểm tối đa.</summary>
    GrantFull = 1
}

[JsonConverter(typeof(JsonStringEnumConverter<ScoreRoundingMode>))]
public enum ScoreRoundingMode
{
    /// <summary>Nửa lên (xa số 0).</summary>
    HalfUp = 0,

    /// <summary>Nửa về số chẵn (làm tròn ngân hàng).</summary>
    HalfEven = 1,

    /// <summary>Cắt bỏ phần thừa (về phía 0).</summary>
    Truncate = 2
}


/// <summary>Nhóm điểm của hồ sơ tập thể.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<CollectiveScoreKind>))]
public enum CollectiveScoreKind
{
    /// <summary>Cộng vào điểm nhóm tiêu chí chung (tối đa <see cref="CriteriaParameters.CollectiveGeneralMaxScore"/>).</summary>
    General = 0,

    /// <summary>Cộng vào điểm nhóm kết quả thực hiện nhiệm vụ (tối đa <see cref="CriteriaParameters.CollectiveTaskMaxScore"/>).</summary>
    Task = 1
}

/// <summary>Nhóm của khung tiêu chí tập thể; điểm tiêu chí con cộng vào nhóm điểm <see cref="Kind"/>.</summary>
public sealed class CollectiveCriteriaGroup
{
    /// <summary>Mã nhóm (ví dụ "I", "II").</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CollectiveScoreKind Kind { get; set; } = CollectiveScoreKind.General;

    /// <summary>Mã duy nhất trong toàn khung tập thể.</summary>
    public List<CriteriaItem> Items { get; set; } = new();
}

/// <summary>Một tiêu chí con của nhóm tiêu chí chung.</summary>
public sealed class CriteriaItem
{
    /// <summary>Mã (duy nhất trong bộ, ví dụ "1.1").</summary>
    public string Code { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    /// <summary>Nguyên văn in trên phiếu tự chấm; trống thì in <see cref="Text"/>.</summary>
    public string? FormText { get; set; }

    public double MaxScore { get; set; }
}

/// <summary>Nhóm tiêu chí chung (ví dụ nhóm 1 "Phẩm chất chính trị…" 18 điểm).</summary>
public sealed class CriteriaGroup
{
    /// <summary>Mã nhóm (duy nhất trong bộ).</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public CriteriaScoringMode ScoringMode { get; set; } = CriteriaScoringMode.Binary;

    public List<CriteriaItem> Items { get; set; } = new();

    /// <summary>Tổng điểm tối đa các tiêu chí con.</summary>
    [JsonIgnore]
    public double MaxScore => (Items ?? new List<CriteriaItem>()).Sum(i => i?.MaxScore ?? 0);
}

/// <summary>Trục kết quả (T1…): nhóm việc/sản phẩm đăng ký ở Mẫu 01, gom dòng ở Mẫu 09C, 9D.</summary>
public sealed class ResultAxis
{
    /// <summary>Mã trục (duy nhất trong bộ, ví dụ "T1").</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Nội dung áp dụng.</summary>
    public string? Description { get; set; }
}

/// <summary>Khung tỷ trọng A-B-C-D (tỷ lệ 0..1, tổng = 1).</summary>
public sealed class WeightFrame
{
    /// <summary>Mã khung (duy nhất trong bộ, ví dụ "K2").</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Tỷ trọng A — khối lượng.</summary>
    public double A { get; set; }

    /// <summary>Tỷ trọng B — chất lượng, an toàn.</summary>
    public double B { get; set; }

    /// <summary>Tỷ trọng C — tiến độ.</summary>
    public double C { get; set; }

    /// <summary>Tỷ trọng D — tổ chức thực hiện, hiệu quả.</summary>
    public double D { get; set; }
}

/// <summary>Mức thang quy đổi %: áp dụng khi giá trị ≥ <see cref="MinPercent"/> (lấy mức cao nhất thỏa).</summary>
public sealed class ConversionBand
{
    /// <summary>Ngưỡng dưới (%, gồm cả ngưỡng).</summary>
    public double MinPercent { get; set; }

    /// <summary>Nhãn (ví dụ "95 – 100%").</summary>
    public string Label { get; set; } = string.Empty;

    public string? Description { get; set; }
}

/// <summary>Ngưỡng điểm và điều kiện kèm theo của một mức xếp loại (4 mức cố định theo <see cref="EvaluationGrade"/>).</summary>
public sealed class GradeRule
{
    [JsonConverter(typeof(JsonStringEnumConverter<EvaluationGrade>))]
    public EvaluationGrade Grade { get; set; }

    /// <summary>Điểm tối thiểu (gồm cả ngưỡng).</summary>
    public double MinScore { get; set; }

    /// <summary>Tỷ lệ nhiệm vụ vượt chuẩn tối thiểu (0..1); null = không đòi.</summary>
    public double? MinExceedStandardRatio { get; set; }

    /// <summary>Điều kiện đồng thời (hiển thị cho người chấm; phần mềm không tự kiểm các điều kiện định tính).</summary>
    public string? Conditions { get; set; }
}

public sealed class RoundingRule
{
    public RoundingRule()
    {
    }

    public RoundingRule(int decimals, ScoreRoundingMode mode)
    {
        Decimals = decimals;
        Mode = mode;
    }

    /// <summary>Số chữ số thập phân (0–4).</summary>
    public int Decimals { get; set; } = 1;

    public ScoreRoundingMode Mode { get; set; } = ScoreRoundingMode.HalfUp;
}

public sealed class RoundingSettings
{
    /// <summary>Điểm từng sản phẩm/nhiệm vụ (Mẫu 02).</summary>
    public RoundingRule TaskScore { get; set; } = new();

    /// <summary>Tổng điểm nhóm kết quả thực hiện nhiệm vụ (cộng các điểm sản phẩm đã làm tròn).</summary>
    public RoundingRule TasksTotal { get; set; } = new();

    public RoundingRule GeneralTotal { get; set; } = new();

    /// <summary>Tổng điểm (chung + nhiệm vụ).</summary>
    public RoundingRule Total { get; set; } = new();
}

/// <summary>Trần Xuất sắc = tỷ lệ × số "Hoàn thành tốt" trong nhóm chức danh
/// (<see cref="EvaluationScoring.ExcellentQuota(int, double)"/>); bộ chỉ đặt tỷ lệ 20%–25%.</summary>
public sealed class ExcellentQuotaRule
{
    public const double MinRatio = 0.20;

    /// <summary>Cấp có thẩm quyền được nâng tới mức này khi đơn vị có thành tích nổi trội.</summary>
    public const double MaxRatio = 0.25;

    public const double DefaultRatio = MinRatio;

    public double Ratio { get; set; } = DefaultRatio;

    /// <summary>Giá trị không hợp lệ thì lấy mặc định; ngoài khoảng thì kẹp về 20%–25%.</summary>
    [JsonIgnore]
    public double EffectiveRatio => double.IsFinite(Ratio) && Ratio > 0 ? Math.Clamp(Ratio, MinRatio, MaxRatio) : DefaultRatio;
}

public sealed class CriteriaParameters
{
    /// <summary>Số sản phẩm/nhiệm vụ tối thiểu khi đăng ký (Mẫu 01).</summary>
    public int MinTasks { get; set; } = 3;

    /// <summary>Số sản phẩm/nhiệm vụ tối đa khi đăng ký (Mẫu 01).</summary>
    public int MaxTasks { get; set; } = 7;

    /// <summary>Điểm tối đa nhóm kết quả = tổng trọng số Mẫu 01.</summary>
    public double TotalTaskWeight { get; set; } = 70.0;

    /// <summary>Sai số cho phép khi so tổng trọng số (sau khi làm tròn 2 chữ số).</summary>
    public double TaskWeightTolerance { get; set; } = 0.05;

    /// <summary>Điểm tối đa nhóm tiêu chí chung (= tổng điểm tối đa các tiêu chí con).</summary>
    public double GeneralMaxScore { get; set; } = 30.0;

    /// <summary>Khung gán khi cán bộ chưa có khung mặc định; null = để trống, kiểm tra kẹt luồng sẽ báo lỗi.</summary>
    public string? DefaultWeightFrameCode { get; set; }

    /// <summary>Biên độ (điểm %) cấp giao nhiệm vụ được chỉnh từng tỷ trọng A-B-C-D khi duyệt danh mục; tổng vẫn 100%.
    /// null hoặc 0 = không cho chỉnh.</summary>
    public double? FrameAdjustLimit { get; set; }

    [JsonIgnore]
    public bool AllowsFrameAdjustment => FrameAdjustLimit is > 0;

    /// <summary>Cho phép đánh dấu tiêu chí con "K/AD — Không áp dụng" (bắt buộc nêu lý do).</summary>
    public bool AllowNotApplicable { get; set; } = true;

    public NotApplicableRule NotApplicableRule { get; set; } = NotApplicableRule.ExcludeAndRescale;

    /// <summary>Khoản giảm điểm của một tiêu chí con từ mức này trở lên phải nêu căn cứ; null = không bắt buộc.</summary>
    public double? DeductionReasonMinPoints { get; set; } = 1.0;

    /// <summary>Chênh lệch |tự chấm − thẩm định| từ mức này trở lên thì bắt buộc nhập nội dung giải trình/căn cứ.</summary>
    public double ExplanationThreshold { get; set; } = 5.0;

    /// <summary>Bắt buộc giải trình cả khi chênh lệch dưới ngưỡng nhưng làm đổi mức xếp loại theo ngưỡng điểm.</summary>
    public bool ExplanationOnGradeChange { get; set; } = true;

    public ExcellentQuotaRule ExcellentQuota { get; set; } = new();

    public RoundingSettings Rounding { get; set; } = new();

    /// <summary>Điểm tối đa nhóm tiêu chí chung của hồ sơ tập thể (Mẫu 06–08).</summary>
    public double CollectiveGeneralMaxScore { get; set; } = 30.0;

    /// <summary>Điểm tối đa nhóm kết quả thực hiện nhiệm vụ của hồ sơ tập thể.</summary>
    public double CollectiveTaskMaxScore { get; set; } = 70.0;

    /// <summary>Công khai kết quả kèm điểm chính thức; mặc định chỉ công khai mức xếp loại. Chờ nghiệp vụ xác nhận.</summary>
    public bool PublishScores { get; set; }

    /// <summary>Mức xếp loại bắt buộc lập kế hoạch 30-60-90 ngày (Mẫu 17). Chờ nghiệp vụ xác nhận.</summary>
    [JsonConverter(typeof(GradeListJsonConverter))]
    public List<EvaluationGrade> ImprovementPlanRequiredGrades { get; set; } = new() { EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh };

    /// <summary>Chỉ cảnh báo thiếu kế hoạch 30-60-90 với kỳ đang mở/khóa và kỳ đã đóng trong số ngày này.</summary>
    public int ImprovementPlanAlertDays { get; set; } = 90;
}

/// <summary>Nội dung bộ tiêu chí (jsonb <c>criteria_sets.Content</c>). Cấu trúc công thức cố định trong
/// <see cref="EvaluationScoring"/>; bộ chỉ cung cấp số liệu và danh mục.</summary>
public sealed class CriteriaSetContent
{
    public const int CurrentSchemaVersion = 1;

    private static readonly Regex CodePattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,19}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Chữ không dấu, số, dấu chấm, gạch nối, gạch dưới; tối đa 20 ký tự.</summary>
    public static bool IsValidCode(string? code) => code != null && CodePattern.IsMatch(code);

    /// <summary>Sai số so sánh số thực (tổng điểm, tổng tỷ trọng).</summary>
    public const double Epsilon = 1e-6;

    /// <summary>Bốn mức xếp loại, từ cao xuống thấp.</summary>
    public static readonly IReadOnlyList<EvaluationGrade> RankedGrades = new[]
    {
        EvaluationGrade.HoanThanhXuatSac, EvaluationGrade.HoanThanhTot, EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh
    };

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Lưu jsonb (không nhúng HTML): giữ nguyên chữ tiếng Việt cho dễ đọc khi tra cứu CSDL.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public List<CriteriaGroup> GeneralGroups { get; set; } = new();

    public List<ResultAxis> Axes { get; set; } = new();

    public List<WeightFrame> WeightFrames { get; set; } = new();

    /// <summary>Thang quy đổi % cho A/B/C/D (chỉ mô tả mức, không đổi công thức).</summary>
    public List<ConversionBand> ConversionScale { get; set; } = new();

    public List<GradeRule> Grades { get; set; } = new();

    public CriteriaParameters Parameters { get; set; } = new();

    /// <summary>Biểu mẫu cá nhân áp dụng (<see cref="RecordFormCodes"/>); Mẫu 09A luôn áp dụng
    /// (<see cref="CriteriaSnapshot.ApplicableForms"/>).</summary>
    public List<string> RequiredForms { get; set; } = new();

    /// <summary>Biểu mẫu cấp tập thể áp dụng (<see cref="CollectiveFormCodes"/>).</summary>
    public List<string> RequiredCollectiveForms { get; set; } = new();

    /// <summary>Biểu mẫu tổng hợp, báo cáo, kiểm soát áp dụng (<see cref="SummaryFormCodes"/>).</summary>
    public List<string> RequiredSummaryForms { get; set; } = new();

    /// <summary>Khung tiêu chí tập thể; rỗng = hồ sơ tập thể nhập thẳng hai điểm nhóm.</summary>
    public List<CollectiveCriteriaGroup> CollectiveGroups { get; set; } = new();

    /// <summary>Các mục tự luận của Mẫu 09C (khi <see cref="RequiredForms"/> có 09C).</summary>
    public List<SelfAssessmentSection> SelfAssessmentSections { get; set; } = new();

    /// <summary>Các mức tự đánh giá của từng nhiệm vụ (ô "Mức" ở Mẫu 9D). Rỗng = không dùng mức.</summary>
    public List<TaskLevel> TaskLevels { get; set; } = new();

    /// <summary>Tiền tố mỗi nhiệm vụ khi dựng Mẫu 09C; <c>{n}</c> là số thứ tự trong trục (ví dụ "Nhiệm vụ {n}:"). Trống = "{n}.".</summary>
    public string? TaskLineLabel { get; set; }

    /// <summary>Câu nối sau mỗi nhiệm vụ đã chọn mức khi dựng Mẫu 09C; <c>{level}</c> là mã mức (ví dụ "Tự đánh giá mức {level}.").</summary>
    public string? TaskLevelPhrase { get; set; }

    #region Tra cứu

    [JsonIgnore]
    public IEnumerable<(CriteriaGroup Group, CriteriaItem Item)> AllItems =>
        (GeneralGroups ?? new List<CriteriaGroup>()).Where(g => g != null)
            .SelectMany(g => (g.Items ?? new List<CriteriaItem>()).Where(i => i != null).Select(i => (g, i)));

    public ResultAxis? FindAxis(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : Axes.FirstOrDefault(a => string.Equals(a.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public WeightFrame? FindFrame(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : WeightFrames.FirstOrDefault(f => string.Equals(f.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public GradeRule? FindGrade(EvaluationGrade grade) => Grades.FirstOrDefault(g => g.Grade == grade);

    /// <summary>Mức có ngưỡng dưới cao nhất ≤ giá trị %.</summary>
    public ConversionBand? BandFor(double percent) =>
        ConversionScale.Where(b => percent + Epsilon >= b.MinPercent).OrderByDescending(b => b.MinPercent).FirstOrDefault();

    #endregion

    #region Đọc / ghi

    /// <summary>Đọc từ JSON; JSON sai định dạng → <see cref="FormatException"/>.</summary>
    public static CriteriaSetContent Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CriteriaSetContent().Normalize();
        try
        {
            return (JsonSerializer.Deserialize<CriteriaSetContent>(json, JsonOptions) ?? new CriteriaSetContent()).Normalize();
        }
        catch (JsonException ex)
        {
            throw new FormatException("Nội dung bộ tiêu chí không đúng định dạng JSON.", ex);
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public CriteriaSetContent Clone() => Parse(ToJson());

    public CriteriaSetContent Normalize()
    {
        GeneralGroups = (GeneralGroups ?? new()).Where(g => g != null).ToList();
        foreach (var group in GeneralGroups)
        {
            group.Code = group.Code?.Trim() ?? string.Empty;
            group.Name = group.Name?.Trim() ?? string.Empty;
            group.Items = (group.Items ?? new()).Where(i => i != null).ToList();
            foreach (var item in group.Items)
            {
                item.Code = item.Code?.Trim() ?? string.Empty;
                item.Text = item.Text?.Trim() ?? string.Empty;
                item.FormText = string.IsNullOrWhiteSpace(item.FormText) ? null : item.FormText.Trim();
            }
        }
        CollectiveGroups = (CollectiveGroups ?? new()).Where(g => g != null).ToList();
        foreach (var group in CollectiveGroups)
        {
            group.Code = group.Code?.Trim() ?? string.Empty;
            group.Name = group.Name?.Trim() ?? string.Empty;
            group.Items = (group.Items ?? new()).Where(i => i != null).ToList();
            foreach (var item in group.Items)
            {
                item.Code = item.Code?.Trim() ?? string.Empty;
                item.Text = item.Text?.Trim() ?? string.Empty;
                item.FormText = string.IsNullOrWhiteSpace(item.FormText) ? null : item.FormText.Trim();
            }
        }
        Axes = (Axes ?? new()).Where(a => a != null).ToList();
        foreach (var axis in Axes)
        {
            axis.Code = axis.Code?.Trim() ?? string.Empty;
            axis.Name = axis.Name?.Trim() ?? string.Empty;
            axis.Description = string.IsNullOrWhiteSpace(axis.Description) ? null : axis.Description.Trim();
        }
        RequiredForms = (RequiredForms ?? new()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(RecordFormCodes.Normalize).ToList();
        RequiredCollectiveForms = (RequiredCollectiveForms ?? new()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(CollectiveFormCodes.Normalize).ToList();
        RequiredSummaryForms = (RequiredSummaryForms ?? new()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(SummaryFormCodes.Normalize).ToList();
        SelfAssessmentSections = (SelfAssessmentSections ?? new()).Where(s => s != null).ToList();
        foreach (var section in SelfAssessmentSections)
        {
            section.Code = section.Code?.Trim() ?? string.Empty;
            section.Title = section.Title?.Trim() ?? string.Empty;
            section.Guidance = string.IsNullOrWhiteSpace(section.Guidance) ? null : section.Guidance.Trim();
            section.Note = string.IsNullOrWhiteSpace(section.Note) ? null : section.Note.Trim();
        }
        TaskLevels = (TaskLevels ?? new()).Where(l => l != null).ToList();
        foreach (var level in TaskLevels)
            level.Label = level.Label?.Trim() ?? string.Empty;
        TaskLineLabel = string.IsNullOrWhiteSpace(TaskLineLabel) ? null : TaskLineLabel.Trim();
        TaskLevelPhrase = string.IsNullOrWhiteSpace(TaskLevelPhrase) ? null : TaskLevelPhrase.Trim();
        WeightFrames = (WeightFrames ?? new()).Where(f => f != null).ToList();
        foreach (var frame in WeightFrames)
        {
            frame.Code = frame.Code?.Trim() ?? string.Empty;
            frame.Name = frame.Name?.Trim() ?? string.Empty;
        }
        ConversionScale = (ConversionScale ?? new()).Where(b => b != null).OrderByDescending(b => b.MinPercent).ToList();
        foreach (var band in ConversionScale)
        {
            band.Label = band.Label?.Trim() ?? string.Empty;
            band.Description = string.IsNullOrWhiteSpace(band.Description) ? null : band.Description.Trim();
        }
        Grades = (Grades ?? new()).Where(g => g != null).ToList();
        foreach (var grade in Grades)
            grade.Conditions = string.IsNullOrWhiteSpace(grade.Conditions) ? null : grade.Conditions.Trim();
        Parameters ??= new CriteriaParameters();
        Parameters.DefaultWeightFrameCode = string.IsNullOrWhiteSpace(Parameters.DefaultWeightFrameCode) ? null : Parameters.DefaultWeightFrameCode.Trim();
        Parameters.ExcellentQuota ??= new ExcellentQuotaRule();
        Parameters.Rounding ??= new RoundingSettings();
        Parameters.Rounding.TaskScore ??= new RoundingRule();
        Parameters.Rounding.TasksTotal ??= new RoundingRule();
        Parameters.Rounding.GeneralTotal ??= new RoundingRule();
        Parameters.Rounding.Total ??= new RoundingRule();
        Parameters.ImprovementPlanRequiredGrades = (Parameters.ImprovementPlanRequiredGrades
                ?? new List<EvaluationGrade> { EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh })
            .Distinct()
            .ToList();
        return this;
    }

    #endregion

    #region Kiểm tra

    /// <summary>Trả danh sách lỗi; rỗng = hợp lệ.</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        var p = Parameters ?? new CriteriaParameters();
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Phiên bản nội dung bộ tiêu chí {SchemaVersion} không được hỗ trợ (chỉ hỗ trợ phiên bản {CurrentSchemaVersion}).");

        ValidateCodes(errors);
        ValidateGeneral(errors, p);
        ValidateCollective(errors, p);
        ValidateAxes(errors);
        ValidateFrames(errors);
        if (!string.IsNullOrWhiteSpace(p.DefaultWeightFrameCode) && FindFrame(p.DefaultWeightFrameCode) == null)
            errors.Add($"Khung tỷ trọng mặc định \"{p.DefaultWeightFrameCode}\" không có trong danh mục khung của bộ.");
        ValidateScale(errors);
        ValidateGrades(errors, p);
        ValidateParameters(errors, p);
        ValidateForms(errors);
        return errors;
    }

    /// <summary>Độ dài tối đa tiêu đề mục 09C, nhãn mức, tên nhóm tập thể.</summary>
    public const int MaxFormTitleLength = 500;

    /// <summary>Độ dài tối đa câu dẫn và lưu ý của mục 09C.</summary>
    public const int MaxFormGuidanceLength = 8000;

    public const int MaxTaskLevels = 9;

    /// <summary>Đơn vị: điểm phần trăm.</summary>
    public const double MaxFrameAdjustLimit = 50;

    /// <summary>Số ngày tối đa sau khi đóng kỳ còn cảnh báo lập kế hoạch 30-60-90 ngày.</summary>
    public const int MaxImprovementPlanAlertDays = 3650;

    /// <summary>Giới hạn số ký tự tối đa được khai báo cho một mục 09C.</summary>
    public const int MaxSectionLengthLimit = 20000;

    private void ValidateForms(List<string> errors)
    {
        foreach (var code in RequiredForms.Where(c => !RecordFormCodes.IsKnown(c)))
            errors.Add($"Biểu mẫu \"{code}\" không có trong danh mục biểu mẫu cá nhân ({string.Join(", ", RecordFormCodes.All)}).");
        foreach (var dup in RequiredForms.GroupBy(c => c).Where(g => g.Count() > 1))
            errors.Add($"Biểu mẫu \"{dup.Key}\" được khai báo nhiều lần trong danh sách biểu mẫu áp dụng.");
        foreach (var code in RequiredCollectiveForms.Where(c => !CollectiveFormCodes.All.Contains(c)))
            errors.Add($"Biểu mẫu tập thể \"{code}\" không có trong danh mục ({string.Join(", ", CollectiveFormCodes.All)}).");
        foreach (var code in RequiredSummaryForms.Where(c => !SummaryFormCodes.All.Contains(c)))
            errors.Add($"Biểu mẫu tổng hợp \"{code}\" không có trong danh mục ({string.Join(", ", SummaryFormCodes.All)}).");
        foreach (var dup in RequiredCollectiveForms.GroupBy(c => c).Concat(RequiredSummaryForms.GroupBy(c => c)).Where(g => g.Count() > 1))
            errors.Add($"Biểu mẫu \"{dup.Key}\" được khai báo nhiều lần trong danh sách biểu mẫu áp dụng.");

        if (RequiredForms.Contains(RecordFormCodes.Form09C) && SelfAssessmentSections.Count == 0)
            errors.Add("Bộ tiêu chí áp dụng Mẫu 09C phải khai báo ít nhất một mục tự đánh giá.");
        foreach (var section in SelfAssessmentSections)
        {
            if (!IsValidCode(section.Code))
                errors.Add($"Mã mục Mẫu 09C \"{section.Code}\" không hợp lệ: chỉ gồm chữ không dấu, số, dấu chấm, gạch nối, gạch dưới (tối đa 20 ký tự).");
            if (string.IsNullOrWhiteSpace(section.Title))
                errors.Add($"Mục Mẫu 09C \"{section.Code}\" chưa có tiêu đề.");
            else if (section.Title.Length > MaxFormTitleLength)
                errors.Add($"Tiêu đề mục Mẫu 09C \"{section.Code}\" không được dài quá {MaxFormTitleLength} ký tự.");
            if ((section.Guidance?.Length ?? 0) > MaxFormGuidanceLength || (section.Note?.Length ?? 0) > MaxFormGuidanceLength)
                errors.Add($"Câu dẫn/lưu ý của mục Mẫu 09C \"{section.Code}\" không được dài quá {MaxFormGuidanceLength} ký tự.");
            if (section.MaxLength < 100 || section.MaxLength > MaxSectionLengthLimit)
                errors.Add($"Số ký tự tối đa của mục Mẫu 09C \"{section.Code}\" phải từ 100 đến {MaxSectionLengthLimit}.");
        }
        foreach (var dup in SelfAssessmentSections.GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Mã mục Mẫu 09C \"{dup.Key}\" bị trùng.");

        if (TaskLevels.Count > MaxTaskLevels)
            errors.Add($"Một bộ tiêu chí có tối đa {MaxTaskLevels} mức tự đánh giá nhiệm vụ.");
        foreach (var level in TaskLevels)
        {
            if (level.Code < 1 || level.Code > 99)
                errors.Add($"Mã mức tự đánh giá nhiệm vụ \"{level.Code}\" không hợp lệ: số nguyên từ 1 đến 99.");
            if (string.IsNullOrWhiteSpace(level.Label))
                errors.Add($"Mức tự đánh giá nhiệm vụ {level.Code} chưa có nhãn.");
            else if (level.Label.Length > MaxFormTitleLength)
                errors.Add($"Nhãn mức tự đánh giá nhiệm vụ {level.Code} không được dài quá {MaxFormTitleLength} ký tự.");
        }
        foreach (var dup in TaskLevels.GroupBy(l => l.Code).Where(g => g.Count() > 1))
            errors.Add($"Mã mức tự đánh giá nhiệm vụ {dup.Key} bị trùng.");
        if ((TaskLineLabel?.Length ?? 0) > 100 || (TaskLevelPhrase?.Length ?? 0) > 200)
            errors.Add("Câu chữ dựng Mẫu 09C: tiền tố nhiệm vụ tối đa 100 ký tự, câu nối mức tối đa 200 ký tự.");
        if (TaskLevels.Count > 0 && (TaskLevelPhrase == null || !TaskLevelPhrase.Contains("{level}")))
            errors.Add("Câu nối mức (Mẫu 09C) phải chứa {level} khi bộ có khai báo mức tự đánh giá nhiệm vụ.");
    }

    private void ValidateCodes(List<string> errors)
    {
        void Check(string kind, IEnumerable<string> codes)
        {
            var list = codes.ToList();
            foreach (var code in list.Where(c => !CodePattern.IsMatch(c ?? string.Empty)))
                errors.Add($"Mã {kind} \"{code}\" không hợp lệ: chỉ gồm chữ không dấu, số, dấu chấm, gạch nối, gạch dưới (tối đa 20 ký tự).");
            foreach (var dup in list.GroupBy(c => c, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
                errors.Add($"Mã {kind} \"{dup.Key}\" bị trùng.");
        }

        Check("nhóm/tiêu chí chung", GeneralGroups.Select(g => g.Code).Concat(AllItems.Select(x => x.Item.Code)));
        Check("trục kết quả", Axes.Select(a => a.Code));
        Check("khung tỷ trọng", WeightFrames.Select(f => f.Code));
    }

    private void ValidateGeneral(List<string> errors, CriteriaParameters p)
    {
        if (GeneralGroups.Count == 0)
            errors.Add("Bộ tiêu chí phải có ít nhất một nhóm tiêu chí chung.");
        foreach (var group in GeneralGroups)
        {
            var label = string.IsNullOrWhiteSpace(group.Name) ? group.Code : group.Name;
            if (string.IsNullOrWhiteSpace(group.Name))
                errors.Add($"Nhóm tiêu chí chung \"{group.Code}\" chưa có tên.");
            if (!Enum.IsDefined(group.ScoringMode))
                errors.Add($"Cách chấm của nhóm \"{label}\" không hợp lệ.");
            if (group.Items.Count == 0)
                errors.Add($"Nhóm \"{label}\" phải có ít nhất một tiêu chí con.");
            foreach (var item in group.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Text))
                    errors.Add($"Tiêu chí con \"{item.Code}\" chưa có nội dung.");
                else if (item.Text.Length > 2000)
                    errors.Add($"Nội dung tiêu chí con \"{item.Code}\" không được dài quá 2000 ký tự.");
                if ((item.FormText?.Length ?? 0) > 4000)
                    errors.Add($"Nội dung in trên phiếu của tiêu chí con \"{item.Code}\" không được dài quá 4000 ký tự.");
                if (!(item.MaxScore > 0) || item.MaxScore > 100)
                    errors.Add($"Điểm tối đa của tiêu chí con \"{item.Code}\" phải lớn hơn 0 và không quá 100.");
            }
        }

        var total = AllItems.Sum(x => x.Item.MaxScore);
        if (GeneralGroups.Count > 0 && Math.Abs(total - p.GeneralMaxScore) > Epsilon)
            errors.Add(Invariant($"Tổng điểm tối đa các tiêu chí con ({total:0.##}) phải bằng điểm tối đa nhóm tiêu chí chung ({p.GeneralMaxScore:0.##})."));
    }

    /// <summary>Khung tập thể rỗng là hợp lệ. Mã dùng không gian riêng, không so với nhóm tiêu chí chung cá nhân.</summary>
    private void ValidateCollective(List<string> errors, CriteriaParameters p)
    {
        if (CollectiveGroups.Count == 0)
            return;

        var items = CollectiveGroups.SelectMany(g => g.Items.Select(i => (Group: g, Item: i))).ToList();
        var codes = CollectiveGroups.Select(g => g.Code).Concat(items.Select(x => x.Item.Code)).ToList();
        foreach (var code in codes.Where(c => !IsValidCode(c)))
            errors.Add($"Mã nhóm/tiêu chí tập thể \"{code}\" không hợp lệ: chỉ gồm chữ không dấu, số, dấu chấm, gạch nối, gạch dưới (tối đa 20 ký tự).");
        foreach (var dup in codes.GroupBy(c => c, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add($"Mã nhóm/tiêu chí tập thể \"{dup.Key}\" bị trùng.");

        foreach (var group in CollectiveGroups)
        {
            var label = string.IsNullOrWhiteSpace(group.Name) ? group.Code : group.Name;
            if (string.IsNullOrWhiteSpace(group.Name))
                errors.Add($"Nhóm tiêu chí tập thể \"{group.Code}\" chưa có tên.");
            else if (group.Name.Length > MaxFormTitleLength)
                errors.Add($"Tên nhóm tiêu chí tập thể \"{group.Code}\" không được dài quá {MaxFormTitleLength} ký tự.");
            if (!Enum.IsDefined(group.Kind))
                errors.Add($"Nhóm điểm của nhóm tiêu chí tập thể \"{label}\" không hợp lệ.");
            if (group.Items.Count == 0)
                errors.Add($"Nhóm tiêu chí tập thể \"{label}\" phải có ít nhất một tiêu chí con.");
            foreach (var item in group.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Text))
                    errors.Add($"Tiêu chí tập thể \"{item.Code}\" chưa có nội dung.");
                else if (item.Text.Length > 2000)
                    errors.Add($"Nội dung tiêu chí tập thể \"{item.Code}\" không được dài quá 2000 ký tự.");
                if ((item.FormText?.Length ?? 0) > 4000)
                    errors.Add($"Nội dung in trên phiếu của tiêu chí tập thể \"{item.Code}\" không được dài quá 4000 ký tự.");
                if (!(item.MaxScore > 0) || item.MaxScore > 100)
                    errors.Add($"Điểm tối đa của tiêu chí tập thể \"{item.Code}\" phải lớn hơn 0 và không quá 100.");
            }
        }

        foreach (var (kind, max, name) in new[]
                 {
                     (CollectiveScoreKind.General, p.CollectiveGeneralMaxScore, "nhóm tiêu chí chung"),
                     (CollectiveScoreKind.Task, p.CollectiveTaskMaxScore, "nhóm kết quả thực hiện nhiệm vụ")
                 })
        {
            var total = items.Where(x => x.Group.Kind == kind).Sum(x => x.Item.MaxScore);
            if (Math.Abs(total - max) > Epsilon)
                errors.Add(Invariant($"Khung tiêu chí tập thể: tổng điểm tối đa các tiêu chí thuộc {name} ({total:0.##}) phải bằng điểm tối đa {name} của hồ sơ tập thể ({max:0.##})."));
        }
    }

    private void ValidateAxes(List<string> errors)
    {
        if (Axes.Count == 0)
            errors.Add("Bộ tiêu chí phải có ít nhất một trục kết quả.");
        foreach (var axis in Axes.Where(a => string.IsNullOrWhiteSpace(a.Name)))
            errors.Add($"Trục \"{axis.Code}\" chưa có tên.");
    }

    private void ValidateFrames(List<string> errors)
    {
        if (WeightFrames.Count == 0)
            errors.Add("Bộ tiêu chí phải có ít nhất một khung tỷ trọng A-B-C-D.");
        foreach (var frame in WeightFrames)
        {
            if (string.IsNullOrWhiteSpace(frame.Name))
                errors.Add($"Khung tỷ trọng \"{frame.Code}\" chưa có tên.");
            var parts = new[] { frame.A, frame.B, frame.C, frame.D };
            if (parts.Any(x => x < 0 || x > 1 || double.IsNaN(x)) || Math.Abs(parts.Sum() - 1.0) > Epsilon)
                errors.Add($"Tỷ trọng A-B-C-D của khung \"{frame.Code}\" phải từ 0 đến 100% và có tổng bằng 100%.");
        }
    }

    private void ValidateScale(List<string> errors)
    {
        if (ConversionScale.Count == 0)
        {
            errors.Add("Bộ tiêu chí phải có thang quy đổi % A-B-C-D (ít nhất một mức, mức thấp nhất bắt đầu từ 0%).");
            return;
        }
        foreach (var band in ConversionScale)
        {
            if (band.MinPercent < 0 || band.MinPercent > 100)
                errors.Add(Invariant($"Ngưỡng dưới của mức \"{band.Label}\" ({band.MinPercent:0.##}%) phải từ 0 đến 100%."));
            if (string.IsNullOrWhiteSpace(band.Label))
                errors.Add(Invariant($"Mức từ {band.MinPercent:0.##}% của thang quy đổi chưa có nhãn."));
        }
        foreach (var dup in ConversionScale.GroupBy(b => b.MinPercent).Where(g => g.Count() > 1))
            errors.Add(Invariant($"Thang quy đổi có hai mức cùng ngưỡng dưới {dup.Key:0.##}%."));
        if (!ConversionScale.Any(b => Math.Abs(b.MinPercent) < Epsilon))
            errors.Add("Thang quy đổi phải có mức bắt đầu từ 0% (mọi giá trị đều có mức tương ứng).");
    }

    private void ValidateGrades(List<string> errors, CriteriaParameters p)
    {
        foreach (var grade in Grades.Where(g => !RankedGrades.Contains(g.Grade)))
            errors.Add($"Mức xếp loại \"{grade.Grade}\" không dùng được trong bộ tiêu chí (chỉ 4 mức: xuất sắc, tốt, hoàn thành, không hoàn thành).");
        foreach (var dup in Grades.GroupBy(g => g.Grade).Where(g => g.Count() > 1))
            errors.Add($"Mức xếp loại \"{GradeName(dup.Key)}\" được khai báo nhiều lần.");

        var ranked = new List<GradeRule>();
        foreach (var grade in RankedGrades)
        {
            var rule = FindGrade(grade);
            if (rule == null)
                errors.Add($"Thiếu ngưỡng điểm của mức \"{GradeName(grade)}\".");
            else
                ranked.Add(rule);
        }
        if (ranked.Count != RankedGrades.Count)
            return;

        var max = p.GeneralMaxScore + p.TotalTaskWeight;
        for (var i = 0; i < ranked.Count; i++)
        {
            var rule = ranked[i];
            if (rule.MinScore < 0 || rule.MinScore > max + Epsilon)
                errors.Add(Invariant($"Ngưỡng điểm của mức \"{GradeName(rule.Grade)}\" phải từ 0 đến {max:0.##}."));
            if (i > 0 && !(ranked[i - 1].MinScore > rule.MinScore))
                errors.Add($"Ngưỡng điểm phải giảm dần: \"{GradeName(ranked[i - 1].Grade)}\" phải cao hơn \"{GradeName(rule.Grade)}\".");
            if (rule.MinExceedStandardRatio is { } ratio && (ratio < 0 || ratio > 1))
                errors.Add($"Tỷ lệ nhiệm vụ vượt chuẩn tối thiểu của mức \"{GradeName(rule.Grade)}\" phải từ 0 đến 100%.");
            if (rule.Conditions is { Length: > 4000 })
                errors.Add($"Điều kiện của mức \"{GradeName(rule.Grade)}\" không được dài quá 4000 ký tự.");
        }
        if (Math.Abs(ranked[^1].MinScore) > Epsilon)
            errors.Add("Ngưỡng điểm của mức \"Không hoàn thành nhiệm vụ\" phải là 0 (mức thấp nhất nhận mọi điểm còn lại).");
    }

    private static void ValidateParameters(List<string> errors, CriteriaParameters p)
    {
        if (p.MinTasks < 1 || p.MinTasks > 50)
            errors.Add("Số sản phẩm tối thiểu phải từ 1 đến 50.");
        if (p.MaxTasks < p.MinTasks || p.MaxTasks > 50)
            errors.Add("Số sản phẩm tối đa phải lớn hơn hoặc bằng số tối thiểu và không quá 50.");
        if (!(p.TotalTaskWeight > 0) || p.TotalTaskWeight > 100)
            errors.Add("Tổng trọng số nhiệm vụ (điểm tối đa nhóm kết quả) phải lớn hơn 0 và không quá 100.");
        if (p.TaskWeightTolerance < 0 || p.TaskWeightTolerance > 5)
            errors.Add("Sai số tổng trọng số phải từ 0 đến 5.");
        if (!(p.GeneralMaxScore > 0) || p.GeneralMaxScore > 100)
            errors.Add("Điểm tối đa nhóm tiêu chí chung phải lớn hơn 0 và không quá 100.");
        if (!Enum.IsDefined(p.NotApplicableRule))
            errors.Add("Cách xử lý điểm khi \"Không áp dụng\" không hợp lệ.");
        if (p.DeductionReasonMinPoints is { } deduction && !(deduction > 0))
            errors.Add("Mức giảm điểm phải nêu căn cứ phải lớn hơn 0 (để trống nếu không bắt buộc).");
        if (p.FrameAdjustLimit is { } adjust && (double.IsNaN(adjust) || adjust < 0 || adjust > MaxFrameAdjustLimit))
            errors.Add(Invariant($"Biên độ điều chỉnh tỷ lệ A-B-C-D phải từ 0 đến {MaxFrameAdjustLimit:0.##} điểm phần trăm (để trống nếu không cho điều chỉnh)."));
        if (p.ExplanationThreshold < 0 || p.ExplanationThreshold > 100)
            errors.Add("Ngưỡng chênh lệch cần giải trình phải từ 0 đến 100 điểm.");
        if (double.IsNaN(p.ExcellentQuota.Ratio)
            || p.ExcellentQuota.Ratio < ExcellentQuotaRule.MinRatio - Epsilon
            || p.ExcellentQuota.Ratio > ExcellentQuotaRule.MaxRatio + Epsilon)
            errors.Add("Trần tỷ lệ Hoàn thành xuất sắc phải từ 20% đến 25%.");
        foreach (var (name, rule) in new[]
                 {
                     ("điểm sản phẩm", p.Rounding.TaskScore), ("tổng điểm nhiệm vụ", p.Rounding.TasksTotal),
                     ("tổng điểm tiêu chí chung", p.Rounding.GeneralTotal), ("tổng điểm", p.Rounding.Total)
                 })
        {
            if (rule.Decimals < 0 || rule.Decimals > 4 || !Enum.IsDefined(rule.Mode))
                errors.Add($"Làm tròn {name}: số chữ số thập phân phải từ 0 đến 4 và kiểu làm tròn hợp lệ.");
        }
        if (!(p.CollectiveGeneralMaxScore > 0) || !(p.CollectiveTaskMaxScore > 0))
            errors.Add("Điểm tối đa của hồ sơ tập thể phải lớn hơn 0.");
        if ((p.ImprovementPlanRequiredGrades ?? new List<EvaluationGrade>()).Any(g => !RankedGrades.Contains(g)))
            errors.Add("Mức bắt buộc lập kế hoạch 30-60-90 ngày chỉ chọn trong bốn mức xếp loại.");
        if (p.ImprovementPlanAlertDays < 1 || p.ImprovementPlanAlertDays > MaxImprovementPlanAlertDays)
            errors.Add($"Số ngày sau khi đóng kỳ còn cảnh báo lập kế hoạch 30-60-90 ngày phải từ 1 đến {MaxImprovementPlanAlertDays}.");
    }

    public static string GradeName(EvaluationGrade grade) => grade switch
    {
        EvaluationGrade.HoanThanhXuatSac => "Hoàn thành xuất sắc nhiệm vụ",
        EvaluationGrade.HoanThanhTot => "Hoàn thành tốt nhiệm vụ",
        EvaluationGrade.HoanThanh => "Hoàn thành nhiệm vụ",
        EvaluationGrade.KhongHoanThanh => "Không hoàn thành nhiệm vụ",
        _ => "Chưa xếp loại"
    };

    private static string Invariant(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    #endregion
}

/// <summary>Ảnh chụp bộ tiêu chí trong kỳ (jsonb): chụp khi chọn bộ và chụp lại khi mở kỳ; bất biến dù bộ gốc đổi.</summary>
public sealed class CriteriaSnapshot
{
    public Guid SetId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTime TakenAt { get; set; }

    public CriteriaSetContent Content { get; set; } = new();

    /// <summary>Mẫu 09A + các mẫu trong <see cref="CriteriaSetContent.RequiredForms"/>, theo thứ tự danh mục.</summary>
    public IReadOnlyList<string> ApplicableForms()
    {
        var set = (Content?.RequiredForms ?? new List<string>())
            .Select(RecordFormCodes.Normalize)
            .Append(RecordFormCodes.Form09A)
            .ToHashSet();
        return RecordFormCodes.All.Where(set.Contains).ToList();
    }

    public bool AppliesForm(string formCode) => ApplicableForms().Contains(RecordFormCodes.Normalize(formCode));

    /// <summary>Chuỗi rỗng → null; JSON sai → <see cref="FormatException"/>.</summary>
    public static CriteriaSnapshot? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            var snapshot = JsonSerializer.Deserialize<CriteriaSnapshot>(json, CriteriaSetContent.JsonOptions);
            if (snapshot == null)
                return null;
            snapshot.Content = (snapshot.Content ?? new CriteriaSetContent()).Normalize();
            return snapshot;
        }
        catch (JsonException ex)
        {
            throw new FormatException("Ảnh chụp bộ tiêu chí của kỳ không đúng định dạng JSON.", ex);
        }
    }

    public string ToJson() => JsonSerializer.Serialize(this, CriteriaSetContent.JsonOptions);
}

/// <summary>Ghi mức xếp loại dạng chuỗi mã; đọc được cả số.</summary>
public sealed class GradeListJsonConverter : JsonConverter<List<EvaluationGrade>>
{
    /// <inheritdoc />
    public override List<EvaluationGrade>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Danh sách mức xếp loại phải là mảng.");

        var result = new List<EvaluationGrade>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var number) && Enum.IsDefined(typeof(EvaluationGrade), number))
                result.Add((EvaluationGrade)number);
            else if (reader.TokenType == JsonTokenType.String
                     && Enum.TryParse<EvaluationGrade>(reader.GetString(), true, out var grade) && Enum.IsDefined(grade))
                result.Add(grade);
            else
                throw new JsonException("Mức xếp loại không hợp lệ.");
        }
        return result;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, List<EvaluationGrade> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var grade in value)
            writer.WriteStringValue(grade.ToString());
        writer.WriteEndArray();
    }
}
