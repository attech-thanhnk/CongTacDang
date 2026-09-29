using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Một mốc của kế hoạch 30-60-90 ngày — đúng các cột của bảng Mẫu 17.</summary>
public sealed class ImprovementMilestone
{
    /// <summary>"Hạn chế cần khắc phục".</summary>
    public string? Limitation { get; set; }

    /// <summary>"Mục tiêu/sản phẩm".</summary>
    public string? Target { get; set; }

    /// <summary>"Biện pháp &amp; đào tạo hỗ trợ".</summary>
    public string? Measures { get; set; }

    /// <summary>"Phối hợp/giám sát".</summary>
    public string? Coordination { get; set; }

    /// <summary>
    /// "Kết quả sau mỗi mốc": <see cref="ImprovementPlanContent.ResultAchieved"/> / <see cref="ImprovementPlanContent.ResultNotAchieved"/>;
    /// null = chưa đánh giá.
    /// </summary>
    public string? Result { get; set; }

    /// <summary>Ghi chú kết quả (minh chứng, nhận xét) khi đánh giá mốc.</summary>
    public string? ResultNote { get; set; }

    /// <summary>Người ghi kết quả mốc.</summary>
    public string? ResultRecordedByName { get; set; }

    /// <summary>Thời điểm ghi kết quả mốc.</summary>
    public DateTime? ResultRecordedAt { get; set; }
}

/// <summary>
/// Nội dung Mẫu 17 "KẾ HOẠCH HỖ TRỢ, KHẮC PHỤC VÀ PHÁT TRIỂN 30-60-90 NGÀY" (biểu mẫu gốc HD03, PDF tr.79) theo mã mục:
/// người trực tiếp hỗ trợ, giám sát (họ tên, chức vụ) và ba mốc <c>M30</c> (khắc phục cấp bách), <c>M60</c> (cải thiện hiệu suất),
/// <c>M90</c> (đánh giá chuyển biến). Họ tên, chức vụ, đơn vị, mức xếp loại của cán bộ lấy từ hồ sơ, không lưu lại.
/// </summary>
public sealed class ImprovementPlanContent
{
    /// <summary>Mã mốc 30 ngày.</summary>
    public const string M30 = "M30";

    /// <summary>Mã mốc 60 ngày.</summary>
    public const string M60 = "M60";

    /// <summary>Mã mốc 90 ngày.</summary>
    public const string M90 = "M90";

    /// <summary>Kết quả mốc: đạt (30/60 ngày: "Đạt yêu cầu"; 90 ngày: "Đạt (Đóng kế hoạch)").</summary>
    public const string ResultAchieved = "Achieved";

    /// <summary>Kết quả mốc: chưa đạt (30/60 ngày: "Chưa chuyển biến"; 90 ngày: "Không đạt (Xem xét nhân sự)").</summary>
    public const string ResultNotAchieved = "NotAchieved";

    /// <summary>Độ dài tối đa của một ô nội dung.</summary>
    public const int MaxTextLength = 2000;

    /// <summary>Các mốc theo thứ tự, kèm số ngày và tên giai đoạn như trên Mẫu 17.</summary>
    public static readonly IReadOnlyList<(string Code, int Days, string Name)> Milestones = new[]
    {
        (M30, 30, "Mốc 30 ngày (Khắc phục cấp bách)"),
        (M60, 60, "Mốc 60 ngày (Cải thiện hiệu suất)"),
        (M90, 90, "Mốc 90 ngày (Đánh giá chuyển biến)")
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>"Người trực tiếp hỗ trợ, giám sát".</summary>
    public string? SupporterName { get; set; }

    /// <summary>Chức vụ của người trực tiếp hỗ trợ, giám sát.</summary>
    public string? SupporterTitle { get; set; }

    /// <summary>Các mốc theo mã (<see cref="M30"/>, <see cref="M60"/>, <see cref="M90"/>).</summary>
    public Dictionary<string, ImprovementMilestone> Stages { get; set; } = new();

    /// <summary>Mã mốc hợp lệ.</summary>
    public static bool IsMilestone(string? code) => code != null && Milestones.Any(m => m.Code == code);

    /// <summary>Tên hiển thị kết quả mốc theo đúng chữ trên Mẫu 17.</summary>
    public static string ResultName(string milestone, string? result) => (milestone, result) switch
    {
        (M90, ResultAchieved) => "Đạt (Đóng kế hoạch)",
        (M90, ResultNotAchieved) => "Không đạt (Xem xét nhân sự)",
        (_, ResultAchieved) => "Đạt yêu cầu",
        (_, ResultNotAchieved) => "Chưa chuyển biến",
        _ => "Chưa đánh giá"
    };

    /// <summary>Mốc theo mã (tạo mới nếu chưa có).</summary>
    public ImprovementMilestone Stage(string code)
    {
        if (!Stages.TryGetValue(code, out var stage))
        {
            stage = new ImprovementMilestone();
            Stages[code] = stage;
        }
        return stage;
    }

    /// <summary>Đọc từ JSON; JSON sai định dạng → <see cref="FormatException"/>.</summary>
    public static ImprovementPlanContent Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ImprovementPlanContent().Normalize();
        try
        {
            return (JsonSerializer.Deserialize<ImprovementPlanContent>(json, JsonOptions) ?? new ImprovementPlanContent()).Normalize();
        }
        catch (JsonException ex)
        {
            throw new FormatException("Nội dung kế hoạch 30-60-90 ngày không đúng định dạng JSON.", ex);
        }
    }

    /// <summary>Ghi ra JSON.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Chuẩn hóa: chỉ giữ ba mốc hợp lệ (đủ cả ba), cắt khoảng trắng, chuỗi rỗng → null.</summary>
    public ImprovementPlanContent Normalize()
    {
        SupporterName = Clean(SupporterName);
        SupporterTitle = Clean(SupporterTitle);
        var stages = Stages ?? new Dictionary<string, ImprovementMilestone>();
        Stages = new Dictionary<string, ImprovementMilestone>();
        foreach (var (code, _, _) in Milestones)
        {
            var stage = stages.TryGetValue(code, out var s) && s != null ? s : new ImprovementMilestone();
            stage.Limitation = Clean(stage.Limitation);
            stage.Target = Clean(stage.Target);
            stage.Measures = Clean(stage.Measures);
            stage.Coordination = Clean(stage.Coordination);
            stage.ResultNote = Clean(stage.ResultNote);
            stage.Result = stage.Result is ResultAchieved or ResultNotAchieved ? stage.Result : null;
            Stages[code] = stage;
        }
        return this;
    }

    /// <summary>Lỗi độ dài các ô nội dung (rỗng = hợp lệ).</summary>
    public List<string> ValidateLengths()
    {
        var errors = new List<string>();
        if (SupporterName is { Length: > 200 } || SupporterTitle is { Length: > 200 })
            errors.Add("Họ tên, chức vụ người trực tiếp hỗ trợ, giám sát dài tối đa 200 ký tự.");
        foreach (var (code, _, name) in Milestones)
        {
            var stage = Stage(code);
            if (new[] { stage.Limitation, stage.Target, stage.Measures, stage.Coordination, stage.ResultNote }.Any(v => v is { Length: > MaxTextLength }))
                errors.Add($"{name}: mỗi ô dài tối đa {MaxTextLength} ký tự.");
        }
        return errors;
    }

    /// <summary>
    /// Lỗi còn thiếu trước khi duyệt: người trực tiếp hỗ trợ, và với mỗi mốc có "Hạn chế cần khắc phục", "Mục tiêu/sản phẩm",
    /// "Biện pháp &amp; đào tạo hỗ trợ" (rỗng = đủ).
    /// </summary>
    public List<string> MissingForApproval()
    {
        var errors = new List<string>();
        if (SupporterName == null)
            errors.Add("Chưa nhập người trực tiếp hỗ trợ, giám sát.");
        foreach (var (code, _, name) in Milestones)
        {
            var stage = Stage(code);
            var missing = new List<string>();
            if (stage.Limitation == null) missing.Add("hạn chế cần khắc phục");
            if (stage.Target == null) missing.Add("mục tiêu/sản phẩm");
            if (stage.Measures == null) missing.Add("biện pháp & đào tạo hỗ trợ");
            if (missing.Count > 0)
                errors.Add($"{name}: chưa nhập {string.Join(", ", missing)}.");
        }
        return errors;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
