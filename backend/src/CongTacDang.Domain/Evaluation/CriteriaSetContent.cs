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
    /// <summary>"Đảm bảo" = điểm tối đa, "Không đảm bảo" = 0 (Mẫu 09A/09B).</summary>
    Binary = 0,

    /// <summary>Nhập điểm trong khoảng 0..điểm tối đa (thân HD03 tr.10: giảm điểm theo mức độ).</summary>
    Range = 1
}

/// <summary>Cách xử lý điểm khi tiêu chí con được đánh dấu "K/AD — Không áp dụng".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotApplicableRule>))]
public enum NotApplicableRule
{
    /// <summary>Bỏ tiêu chí khỏi mẫu số rồi quy đổi lại về điểm tối đa của nhóm tiêu chí chung (xử lý trọng số).</summary>
    ExcludeAndRescale = 0,

    /// <summary>Tính như đạt điểm tối đa.</summary>
    GrantFull = 1
}

/// <summary>Kiểu làm tròn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreRoundingMode>))]
public enum ScoreRoundingMode
{
    /// <summary>Nửa lên (0,5 trở lên làm tròn lên — xa số 0).</summary>
    HalfUp = 0,

    /// <summary>Nửa về số chẵn (làm tròn ngân hàng).</summary>
    HalfEven = 1,

    /// <summary>Cắt bỏ phần thừa (về phía 0).</summary>
    Truncate = 2
}

/// <summary>Mẫu số của trần tỷ lệ Hoàn thành xuất sắc.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QuotaDenominator>))]
public enum QuotaDenominator
{
    /// <summary>Số người xếp "Hoàn thành tốt nhiệm vụ" (câu chữ HD03 III.6, PDF tr.15).</summary>
    GoodOnly = 0,

    /// <summary>Số người xếp "Hoàn thành tốt" trở lên (nhãn cột Mẫu 15A/15B/16).</summary>
    GoodOrBetter = 1
}

/// <summary>Một tiêu chí con của nhóm tiêu chí chung.</summary>
public sealed class CriteriaItem
{
    /// <summary>Mã (duy nhất trong bộ, ví dụ "1.1").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Nội dung tiêu chí.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>Điểm tối đa.</summary>
    public double MaxScore { get; set; }
}

/// <summary>Nhóm tiêu chí chung (ví dụ nhóm 1 "Phẩm chất chính trị…" 18 điểm).</summary>
public sealed class CriteriaGroup
{
    /// <summary>Mã nhóm (duy nhất trong bộ).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên nhóm.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Cách chấm các tiêu chí con của nhóm.</summary>
    public CriteriaScoringMode ScoringMode { get; set; } = CriteriaScoringMode.Binary;

    /// <summary>Tiêu chí con.</summary>
    public List<CriteriaItem> Items { get; set; } = new();

    /// <summary>Điểm tối đa của nhóm = tổng điểm tối đa các tiêu chí con.</summary>
    [JsonIgnore]
    public double MaxScore => (Items ?? new List<CriteriaItem>()).Sum(i => i?.MaxScore ?? 0);
}

/// <summary>Trục kết quả thực hiện nhiệm vụ (T1…).</summary>
public sealed class ResultAxis
{
    /// <summary>Mã trục (duy nhất trong bộ, ví dụ "T1").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên trục.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Nội dung áp dụng.</summary>
    public string? Description { get; set; }

    /// <summary>Điểm tối đa khi tự chấm trực tiếp theo trục (Mẫu 09B); bộ 09A không dùng (được để 0).</summary>
    public double MaxScore { get; set; }

    /// <summary>Tiêu đề trục in ở cột "Nội dung tiêu chí" của Mẫu 09B (ví dụ "TRỤC (1) – …"); trống → dựng từ tên trục.</summary>
    public string? FormTitle { get; set; }

    /// <summary>Các nội dung gợi ý in dưới tiêu đề trục trên Mẫu 09B (mỗi dòng một gạch đầu dòng); trống → dùng nội dung áp dụng.</summary>
    public string? FormGuidance { get; set; }
}

/// <summary>Khung tỷ trọng A-B-C-D (tỷ lệ 0..1, tổng = 1).</summary>
public sealed class WeightFrame
{
    /// <summary>Mã khung (duy nhất trong bộ, ví dụ "K2").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên khung.</summary>
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

/// <summary>Một mức của thang quy đổi % A-B-C-D: mức áp dụng cho giá trị ≥ <see cref="MinPercent"/> (mức cao nhất thỏa).</summary>
public sealed class ConversionBand
{
    /// <summary>Ngưỡng dưới (%, gồm cả ngưỡng).</summary>
    public double MinPercent { get; set; }

    /// <summary>Nhãn (ví dụ "95 – 100%").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Cách hiểu.</summary>
    public string? Description { get; set; }
}

/// <summary>Ngưỡng điểm và điều kiện kèm theo của một mức xếp loại (4 mức cố định theo <see cref="EvaluationGrade"/>).</summary>
public sealed class GradeRule
{
    /// <summary>Mức xếp loại.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter<EvaluationGrade>))]
    public EvaluationGrade Grade { get; set; }

    /// <summary>Điểm tối thiểu (gồm cả ngưỡng).</summary>
    public double MinScore { get; set; }

    /// <summary>
    /// Tỷ lệ nhiệm vụ vượt chuẩn tối thiểu (0..1) để được gợi ý mức này; null = không đòi. Chỉ kiểm được khi tự chấm theo
    /// danh mục nhiệm vụ (Mẫu 09A) — Mẫu 09B không có dữ liệu nhiệm vụ nên chỉ xét ngưỡng điểm.
    /// </summary>
    public double? MinExceedStandardRatio { get; set; }

    /// <summary>Điều kiện đồng thời (hiển thị cho người chấm; phần mềm không tự kiểm các điều kiện định tính).</summary>
    public string? Conditions { get; set; }
}

/// <summary>Quy tắc làm tròn một loại điểm.</summary>
public sealed class RoundingRule
{
    /// <summary>Khởi tạo rỗng (JSON).</summary>
    public RoundingRule()
    {
    }

    /// <summary>Khởi tạo với giá trị cho trước.</summary>
    public RoundingRule(int decimals, ScoreRoundingMode mode)
    {
        Decimals = decimals;
        Mode = mode;
    }

    /// <summary>Số chữ số thập phân (0–4).</summary>
    public int Decimals { get; set; } = 1;

    /// <summary>Kiểu làm tròn.</summary>
    public ScoreRoundingMode Mode { get; set; } = ScoreRoundingMode.HalfUp;
}

/// <summary>Làm tròn theo từng loại điểm.</summary>
public sealed class RoundingSettings
{
    /// <summary>Điểm từng sản phẩm/nhiệm vụ (Mẫu 02).</summary>
    public RoundingRule TaskScore { get; set; } = new();

    /// <summary>Tổng điểm nhóm kết quả thực hiện nhiệm vụ (cộng các điểm sản phẩm đã làm tròn, hoặc tổng các trục 09B).</summary>
    public RoundingRule TasksTotal { get; set; } = new();

    /// <summary>Tổng điểm nhóm tiêu chí chung.</summary>
    public RoundingRule GeneralTotal { get; set; } = new();

    /// <summary>Tổng điểm (chung + nhiệm vụ).</summary>
    public RoundingRule Total { get; set; } = new();
}

/// <summary>Trần tỷ lệ Hoàn thành xuất sắc.</summary>
public sealed class ExcellentQuotaRule
{
    /// <summary>Tỷ lệ trần (0..1).</summary>
    public double Ratio { get; set; } = 0.20;

    /// <summary>Mẫu số.</summary>
    public QuotaDenominator Denominator { get; set; } = QuotaDenominator.GoodOnly;

    /// <summary>Làm tròn số người tối đa (0 chữ số thập phân).</summary>
    public ScoreRoundingMode Rounding { get; set; } = ScoreRoundingMode.HalfUp;
}

/// <summary>Tham số của bộ tiêu chí.</summary>
public sealed class CriteriaParameters
{
    /// <summary>Số sản phẩm/nhiệm vụ tối thiểu khi đăng ký (Mẫu 01).</summary>
    public int MinTasks { get; set; } = 3;

    /// <summary>Số sản phẩm/nhiệm vụ tối đa khi đăng ký (Mẫu 01).</summary>
    public int MaxTasks { get; set; } = 7;

    /// <summary>Tổng trọng số nhiệm vụ = điểm tối đa nhóm kết quả thực hiện nhiệm vụ (09A: tổng trọng số Mẫu 01; 09B: tổng điểm tối đa các trục).</summary>
    public double TotalTaskWeight { get; set; } = 70.0;

    /// <summary>Sai số cho phép khi so tổng trọng số (sau khi làm tròn 2 chữ số).</summary>
    public double TaskWeightTolerance { get; set; } = 0.05;

    /// <summary>Điểm tối đa nhóm tiêu chí chung (= tổng điểm tối đa các tiêu chí con).</summary>
    public double GeneralMaxScore { get; set; } = 30.0;

    /// <summary>
    /// Mã khung tỷ trọng gán cho hồ sơ khi cán bộ chưa có khung mặc định (phải có trong danh mục khung); null = để trống và
    /// kiểm tra kẹt luồng báo lỗi.
    /// </summary>
    public string? DefaultWeightFrameCode { get; set; }

    /// <summary>Cho phép đánh dấu tiêu chí con "K/AD — Không áp dụng" (bắt buộc nêu lý do).</summary>
    public bool AllowNotApplicable { get; set; } = true;

    /// <summary>Cách xử lý điểm khi có tiêu chí "K/AD".</summary>
    public NotApplicableRule NotApplicableRule { get; set; } = NotApplicableRule.ExcludeAndRescale;

    /// <summary>
    /// Khoản giảm (điểm tối đa − điểm đạt) của một tiêu chí con từ mức này trở lên phải nêu căn cứ; null = không bắt buộc.
    /// </summary>
    public double? DeductionReasonMinPoints { get; set; } = 1.0;

    /// <summary>Chênh lệch |tự chấm − thẩm định| từ mức này trở lên thì bắt buộc nhập nội dung giải trình/căn cứ.</summary>
    public double ExplanationThreshold { get; set; } = 5.0;

    /// <summary>Bắt buộc giải trình cả khi chênh lệch dưới ngưỡng nhưng làm đổi mức xếp loại theo ngưỡng điểm.</summary>
    public bool ExplanationOnGradeChange { get; set; } = true;

    /// <summary>Trần tỷ lệ Hoàn thành xuất sắc.</summary>
    public ExcellentQuotaRule ExcellentQuota { get; set; } = new();

    /// <summary>Làm tròn từng loại điểm.</summary>
    public RoundingSettings Rounding { get; set; } = new();

    /// <summary>Điểm tối đa nhóm tiêu chí chung của hồ sơ tập thể (Mẫu 06–08).</summary>
    public double CollectiveGeneralMaxScore { get; set; } = 30.0;

    /// <summary>Điểm tối đa nhóm kết quả thực hiện nhiệm vụ của hồ sơ tập thể.</summary>
    public double CollectiveTaskMaxScore { get; set; } = 70.0;

    /// <summary>
    /// Công khai kết quả (task 20 — T-86) có kèm điểm chính thức hay không; mặc định chỉ công khai mức xếp loại
    /// (HD03 II.2: "công khai kết quả" không đồng nghĩa công bố toàn bộ hồ sơ). Chờ nghiệp vụ xác nhận.
    /// </summary>
    public bool PublishScores { get; set; }

    /// <summary>
    /// Mức xếp loại chính thức bắt buộc lập kế hoạch hỗ trợ, khắc phục 30-60-90 ngày (Mẫu 17, task 20 — T-88). Mặc định theo
    /// chữ in trên Mẫu 17: "Hoàn thành nhiệm vụ - Mức C" và "Không hoàn thành nhiệm vụ - Mức D". Chờ nghiệp vụ xác nhận.
    /// </summary>
    [JsonConverter(typeof(GradeListJsonConverter))]
    public List<EvaluationGrade> ImprovementPlanRequiredGrades { get; set; } = new() { EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh };
}

/// <summary>
/// Nội dung bộ tiêu chí và thang điểm (cột jsonb <c>criteria_sets.Content</c>, có <see cref="SchemaVersion"/>): nhóm tiêu chí
/// chung + tiêu chí con, trục kết quả, khung tỷ trọng A-B-C-D, thang quy đổi %, mức xếp loại, tham số. Cấu trúc công thức
/// tính điểm cố định trong <see cref="EvaluationScoring"/>; bộ tiêu chí chỉ cung cấp số liệu và danh mục.
/// </summary>
public sealed class CriteriaSetContent
{
    /// <summary>Phiên bản schema hiện tại.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Mẫu tự chấm có Mẫu 01/02 (tính điểm A-B-C-D theo nhiệm vụ).</summary>
    public const string Form09A = "09A";

    /// <summary>Mẫu tự chấm trực tiếp theo trục (Quý III/2026).</summary>
    public const string Form09B = "09B";

    private static readonly Regex CodePattern = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,19}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Mã (tiêu chí, trục, khung) đúng định dạng: chữ không dấu, số, dấu chấm, gạch nối, gạch dưới, tối đa 20 ký tự.</summary>
    public static bool IsValidCode(string? code) => code != null && CodePattern.IsMatch(code);

    /// <summary>Sai số so sánh số thực (tổng điểm, tổng tỷ trọng).</summary>
    public const double Epsilon = 1e-6;

    /// <summary>Bốn mức xếp loại phải có ngưỡng, theo thứ tự từ cao xuống thấp.</summary>
    public static readonly IReadOnlyList<EvaluationGrade> RankedGrades = new[]
    {
        EvaluationGrade.HoanThanhXuatSac, EvaluationGrade.HoanThanhTot, EvaluationGrade.HoanThanh, EvaluationGrade.KhongHoanThanh
    };

    /// <summary>Tùy chọn JSON: camelCase, enum dạng chuỗi.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Lưu jsonb (không nhúng HTML): giữ nguyên chữ tiếng Việt cho dễ đọc khi tra cứu CSDL.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    /// <summary>Phiên bản schema.</summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Nhóm tiêu chí chung.</summary>
    public List<CriteriaGroup> GeneralGroups { get; set; } = new();

    /// <summary>Trục kết quả.</summary>
    public List<ResultAxis> Axes { get; set; } = new();

    /// <summary>Khung tỷ trọng A-B-C-D.</summary>
    public List<WeightFrame> WeightFrames { get; set; } = new();

    /// <summary>Thang quy đổi % cho A/B/C/D (mô tả mức; không đổi công thức).</summary>
    public List<ConversionBand> ConversionScale { get; set; } = new();

    /// <summary>Ngưỡng điểm và điều kiện của 4 mức xếp loại.</summary>
    public List<GradeRule> Grades { get; set; } = new();

    /// <summary>Tham số.</summary>
    public CriteriaParameters Parameters { get; set; } = new();

    /// <summary>
    /// Biểu mẫu cá nhân áp dụng cho hồ sơ của kỳ dùng bộ này (mã theo <see cref="RecordFormCodes"/>). Mẫu tự chấm (09A/09B) luôn
    /// theo <c>SelfScoreForm</c> của bộ — mã 09A/09B ghi ở đây bị bỏ qua khi xác định mẫu áp dụng
    /// (<see cref="CriteriaSnapshot.ApplicableForms"/>). Quyết định nút xuất trên hồ sơ và phần nhập 09C/9D khi tự chấm (task 18).
    /// </summary>
    public List<string> RequiredForms { get; set; } = new();

    /// <summary>Các mục tự luận của Mẫu 09C (khi <see cref="RequiredForms"/> có 09C).</summary>
    public List<SelfAssessmentSection> SelfAssessmentSections { get; set; } = new();

    #region Tra cứu

    /// <summary>Mọi tiêu chí con theo thứ tự nhóm.</summary>
    [JsonIgnore]
    public IEnumerable<(CriteriaGroup Group, CriteriaItem Item)> AllItems =>
        (GeneralGroups ?? new List<CriteriaGroup>()).Where(g => g != null)
            .SelectMany(g => (g.Items ?? new List<CriteriaItem>()).Where(i => i != null).Select(i => (g, i)));

    /// <summary>Trục theo mã (không phân biệt hoa thường); null nếu không có.</summary>
    public ResultAxis? FindAxis(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : Axes.FirstOrDefault(a => string.Equals(a.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Khung tỷ trọng theo mã (không phân biệt hoa thường); null nếu không có.</summary>
    public WeightFrame? FindFrame(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : WeightFrames.FirstOrDefault(f => string.Equals(f.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Ngưỡng của mức xếp loại; null nếu không có.</summary>
    public GradeRule? FindGrade(EvaluationGrade grade) => Grades.FirstOrDefault(g => g.Grade == grade);

    /// <summary>Mức của thang quy đổi áp dụng cho giá trị % (mức có ngưỡng dưới cao nhất ≤ giá trị).</summary>
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

    /// <summary>Ghi ra JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Bản sao sâu.</summary>
    public CriteriaSetContent Clone() => Parse(ToJson());

    /// <summary>Chuẩn hóa: danh sách không null, cắt khoảng trắng mã/tên.</summary>
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
            }
        }
        Axes = (Axes ?? new()).Where(a => a != null).ToList();
        foreach (var axis in Axes)
        {
            axis.Code = axis.Code?.Trim() ?? string.Empty;
            axis.Name = axis.Name?.Trim() ?? string.Empty;
            axis.Description = string.IsNullOrWhiteSpace(axis.Description) ? null : axis.Description.Trim();
            axis.FormTitle = string.IsNullOrWhiteSpace(axis.FormTitle) ? null : axis.FormTitle.Trim();
            axis.FormGuidance = string.IsNullOrWhiteSpace(axis.FormGuidance) ? null : axis.FormGuidance.Trim();
        }
        RequiredForms = (RequiredForms ?? new()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(RecordFormCodes.Normalize).ToList();
        SelfAssessmentSections = (SelfAssessmentSections ?? new()).Where(s => s != null).ToList();
        foreach (var section in SelfAssessmentSections)
        {
            section.Code = section.Code?.Trim() ?? string.Empty;
            section.Title = section.Title?.Trim() ?? string.Empty;
            section.Guidance = string.IsNullOrWhiteSpace(section.Guidance) ? null : section.Guidance.Trim();
            section.Note = string.IsNullOrWhiteSpace(section.Note) ? null : section.Note.Trim();
        }
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

    /// <summary>Mẫu tự chấm hợp lệ.</summary>
    public static bool IsValidForm(string? form) => form is Form09A or Form09B;

    /// <summary>
    /// Kiểm tra đầy đủ nội dung bộ tiêu chí cho mẫu tự chấm <paramref name="selfScoreForm"/>; trả danh sách lỗi (rỗng = hợp lệ).
    /// </summary>
    public List<string> Validate(string? selfScoreForm)
    {
        var errors = new List<string>();
        var p = Parameters ?? new CriteriaParameters();
        if (SchemaVersion != CurrentSchemaVersion)
            errors.Add($"Phiên bản nội dung bộ tiêu chí {SchemaVersion} không được hỗ trợ (chỉ hỗ trợ phiên bản {CurrentSchemaVersion}).");
        if (!IsValidForm(selfScoreForm))
            errors.Add($"Mẫu tự chấm phải là {Form09A} (chấm theo nhiệm vụ Mẫu 01/02) hoặc {Form09B} (chấm trực tiếp theo trục).");

        ValidateCodes(errors);
        ValidateGeneral(errors, p);
        ValidateAxes(errors, p, selfScoreForm);
        ValidateFrames(errors, selfScoreForm);
        if (!string.IsNullOrWhiteSpace(p.DefaultWeightFrameCode) && FindFrame(p.DefaultWeightFrameCode) == null)
            errors.Add($"Khung tỷ trọng mặc định \"{p.DefaultWeightFrameCode}\" không có trong danh mục khung của bộ.");
        ValidateScale(errors);
        ValidateGrades(errors, p);
        ValidateParameters(errors, p);
        ValidateForms(errors);
        return errors;
    }

    /// <summary>Độ dài tối đa tiêu đề trục / tiêu đề mục 09C.</summary>
    public const int MaxFormTitleLength = 500;

    /// <summary>Độ dài tối đa nội dung gợi ý của trục, câu dẫn và lưu ý của mục 09C.</summary>
    public const int MaxFormGuidanceLength = 8000;

    /// <summary>Giới hạn số ký tự tối đa được khai báo cho một mục 09C.</summary>
    public const int MaxSectionLengthLimit = 20000;

    private void ValidateForms(List<string> errors)
    {
        foreach (var code in RequiredForms.Where(c => !RecordFormCodes.IsKnown(c)))
            errors.Add($"Biểu mẫu \"{code}\" không có trong danh mục biểu mẫu cá nhân ({string.Join(", ", RecordFormCodes.All)}).");
        foreach (var dup in RequiredForms.GroupBy(c => c).Where(g => g.Count() > 1))
            errors.Add($"Biểu mẫu \"{dup.Key}\" được khai báo nhiều lần trong danh sách biểu mẫu áp dụng.");

        foreach (var axis in Axes)
        {
            if (axis.FormTitle is { Length: > MaxFormTitleLength })
                errors.Add($"Tiêu đề in trên Mẫu 09B của trục \"{axis.Code}\" không được dài quá {MaxFormTitleLength} ký tự.");
            if (axis.FormGuidance is { Length: > MaxFormGuidanceLength })
                errors.Add($"Nội dung gợi ý trên Mẫu 09B của trục \"{axis.Code}\" không được dài quá {MaxFormGuidanceLength} ký tự.");
        }

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
                if (!(item.MaxScore > 0) || item.MaxScore > 100)
                    errors.Add($"Điểm tối đa của tiêu chí con \"{item.Code}\" phải lớn hơn 0 và không quá 100.");
            }
        }

        var total = AllItems.Sum(x => x.Item.MaxScore);
        if (GeneralGroups.Count > 0 && Math.Abs(total - p.GeneralMaxScore) > Epsilon)
            errors.Add(Invariant($"Tổng điểm tối đa các tiêu chí con ({total:0.##}) phải bằng điểm tối đa nhóm tiêu chí chung ({p.GeneralMaxScore:0.##})."));
    }

    private void ValidateAxes(List<string> errors, CriteriaParameters p, string? form)
    {
        if (Axes.Count == 0)
            errors.Add("Bộ tiêu chí phải có ít nhất một trục kết quả.");
        foreach (var axis in Axes)
        {
            if (string.IsNullOrWhiteSpace(axis.Name))
                errors.Add($"Trục \"{axis.Code}\" chưa có tên.");
            if (axis.MaxScore < 0 || axis.MaxScore > 100)
                errors.Add($"Điểm tối đa của trục \"{axis.Code}\" phải từ 0 đến 100.");
            if (form == Form09B && !(axis.MaxScore > 0))
                errors.Add($"Mẫu 09B chấm trực tiếp theo trục: điểm tối đa của trục \"{axis.Code}\" phải lớn hơn 0.");
        }

        if (form == Form09B && Axes.Count > 0)
        {
            var total = Axes.Sum(a => a.MaxScore);
            if (Math.Abs(total - p.TotalTaskWeight) > Epsilon)
                errors.Add(Invariant($"Mẫu 09B: tổng điểm tối đa các trục ({total:0.##}) phải bằng điểm tối đa nhóm kết quả thực hiện nhiệm vụ ({p.TotalTaskWeight:0.##})."));
        }
    }

    private void ValidateFrames(List<string> errors, string? form)
    {
        if (form == Form09A && WeightFrames.Count == 0)
            errors.Add("Mẫu 09A tính điểm A-B-C-D theo khung tỷ trọng: bộ tiêu chí phải có ít nhất một khung tỷ trọng.");
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
        if (p.ExplanationThreshold < 0 || p.ExplanationThreshold > 100)
            errors.Add("Ngưỡng chênh lệch cần giải trình phải từ 0 đến 100 điểm.");
        if (p.ExcellentQuota.Ratio < 0 || p.ExcellentQuota.Ratio > 1)
            errors.Add("Trần tỷ lệ Hoàn thành xuất sắc phải từ 0 đến 100%.");
        if (!Enum.IsDefined(p.ExcellentQuota.Denominator) || !Enum.IsDefined(p.ExcellentQuota.Rounding))
            errors.Add("Cách tính mẫu số hoặc làm tròn của trần tỷ lệ Hoàn thành xuất sắc không hợp lệ.");
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
    }

    /// <summary>Tên hiển thị của mức xếp loại.</summary>
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

/// <summary>
/// Ảnh chụp bộ tiêu chí trong kỳ (<c>EvaluationPeriod.CriteriaSnapshot</c>, jsonb): chụp nguyên bộ khi kỳ chọn bộ và chụp lại
/// khi mở kỳ; bất biến cho kỳ đó dù bộ gốc bị lưu trữ hay có bản mới.
/// </summary>
public sealed class CriteriaSnapshot
{
    /// <summary>Id bộ tiêu chí gốc.</summary>
    public Guid SetId { get; set; }

    /// <summary>Mã bộ.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Tên bộ.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mẫu tự chấm (09A/09B).</summary>
    public string SelfScoreForm { get; set; } = CriteriaSetContent.Form09A;

    /// <summary>Thời điểm chụp.</summary>
    public DateTime TakenAt { get; set; }

    /// <summary>Nội dung bộ.</summary>
    public CriteriaSetContent Content { get; set; } = new();

    /// <summary>Tự chấm trực tiếp theo trục (Mẫu 09B).</summary>
    [JsonIgnore]
    public bool UsesAxisScoring => string.Equals(SelfScoreForm, CriteriaSetContent.Form09B, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Biểu mẫu cá nhân áp dụng cho hồ sơ của kỳ (task 18): mẫu tự chấm của bộ + các mẫu khác trong
    /// <see cref="CriteriaSetContent.RequiredForms"/>, theo thứ tự danh mục.
    /// </summary>
    public IReadOnlyList<string> ApplicableForms()
    {
        var self = RecordFormCodes.Normalize(SelfScoreForm);
        var others = (Content?.RequiredForms ?? new List<string>())
            .Select(RecordFormCodes.Normalize)
            .Where(c => c != RecordFormCodes.Form09A && c != RecordFormCodes.Form09B);
        var set = others.Append(self).ToHashSet();
        return RecordFormCodes.All.Where(set.Contains).ToList();
    }

    /// <summary>Kỳ áp dụng biểu mẫu <paramref name="formCode"/>.</summary>
    public bool AppliesForm(string formCode) => ApplicableForms().Contains(RecordFormCodes.Normalize(formCode));

    /// <summary>Đọc ảnh chụp; chuỗi rỗng → null; JSON sai → <see cref="FormatException"/>.</summary>
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

    /// <summary>Ghi ra JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, CriteriaSetContent.JsonOptions);
}

/// <summary>Danh sách mức xếp loại dạng chuỗi mã (<c>["HoanThanh","KhongHoanThanh"]</c>); đọc được cả số (task 20).</summary>
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
