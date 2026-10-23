using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Một mốc của kế hoạch 30-60-90 ngày, đúng các cột bảng Mẫu 17.</summary>
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

    /// <summary><see cref="ImprovementPlanContent.ResultAchieved"/> / <see cref="ImprovementPlanContent.ResultNotAchieved"/>; null = chưa đánh giá.</summary>
    public string? Result { get; set; }

    /// <summary>Minh chứng, nhận xét khi đánh giá mốc.</summary>
    public string? ResultNote { get; set; }

    public string? ResultRecordedByName { get; set; }

    public DateTime? ResultRecordedAt { get; set; }
}

/// <summary>Nội dung Mẫu 17 theo mốc <c>M30</c>, <c>M60</c>, <c>M90</c>. Họ tên, chức vụ, đơn vị, mức xếp loại của cán bộ
/// lấy từ hồ sơ, không lưu lại.</summary>
public sealed class ImprovementPlanContent
{
    public const string M30 = "M30";

    public const string M60 = "M60";

    public const string M90 = "M90";

    /// <summary>30/60 ngày: "Đạt yêu cầu"; 90 ngày: "Đạt (Đóng kế hoạch)".</summary>
    public const string ResultAchieved = "Achieved";

    /// <summary>30/60 ngày: "Chưa chuyển biến"; 90 ngày: "Không đạt (Xem xét nhân sự)".</summary>
    public const string ResultNotAchieved = "NotAchieved";

    /// <summary>Mỗi ô nội dung.</summary>
    public const int MaxTextLength = 2000;

    public static readonly IReadOnlyList<(string Code, int Days, string Name)> Milestones = new[]
    {
        (M30, 30, "Mốc 30 ngày (Khắc phục cấp bách)"),
        (M60, 60, "Mốc 60 ngày (Cải thiện hiệu suất)"),
        (M90, 90, "Mốc 90 ngày (Đánh giá chuyển biến)")
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>"Người trực tiếp hỗ trợ, giám sát".</summary>
    public string? SupporterName { get; set; }

    public string? SupporterTitle { get; set; }

    /// <summary>Khóa = mã mốc.</summary>
    public Dictionary<string, ImprovementMilestone> Stages { get; set; } = new();

    public static bool IsMilestone(string? code) => code != null && Milestones.Any(m => m.Code == code);

    /// <summary>Đúng chữ trên Mẫu 17.</summary>
    public static string ResultName(string milestone, string? result) => (milestone, result) switch
    {
        (M90, ResultAchieved) => "Đạt (Đóng kế hoạch)",
        (M90, ResultNotAchieved) => "Không đạt (Xem xét nhân sự)",
        (_, ResultAchieved) => "Đạt yêu cầu",
        (_, ResultNotAchieved) => "Chưa chuyển biến",
        _ => "Chưa đánh giá"
    };

    /// <summary>Tạo mới nếu chưa có.</summary>
    public ImprovementMilestone Stage(string code)
    {
        if (!Stages.TryGetValue(code, out var stage))
        {
            stage = new ImprovementMilestone();
            Stages[code] = stage;
        }
        return stage;
    }

    /// <summary>JSON sai định dạng → <see cref="FormatException"/>.</summary>
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

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    /// <summary>Luôn đủ ba mốc; chuỗi rỗng → null.</summary>
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

    /// <summary>Rỗng = hợp lệ.</summary>
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

    /// <summary>Thiếu trước khi duyệt: người hỗ trợ; mỗi mốc cần hạn chế, mục tiêu, biện pháp. Rỗng = đủ.</summary>
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
