using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CongTacDang.Domain.Evaluation;

/// <summary>Chế độ của một cổng kiểm soát trong diện quyết định.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GateMode>))]
public enum GateMode
{
    /// <summary>Chặn hành động tiếp theo cho tới khi cổng được đáp ứng.</summary>
    Required = 0,

    /// <summary>Không chặn, chỉ cảnh báo và ghi nhận thiếu.</summary>
    Recorded = 1,

    /// <summary>Không áp dụng cổng này.</summary>
    Off = 2,

    /// <summary>Chỉ dùng cho đối soát chuyên ngành: có thì dùng, không có không chặn.</summary>
    Optional = 3
}

/// <summary>Cấu hình một cổng trong diện quyết định.</summary>
public sealed class GateSetting
{
    public GateMode Mode { get; set; } = GateMode.Required;
}

/// <summary>Mã cổng và chế độ cho phép của từng cổng.</summary>
public static class WorkflowGates
{
    /// <summary>Đối soát chuyên ngành (Mẫu 03).</summary>
    public const string Crosscheck = "CROSSCHECK";

    /// <summary>Hiệu chuẩn kết quả (Mẫu 20).</summary>
    public const string Calibration = "CALIBRATION";

    /// <summary>Kiểm soát trước phê duyệt (Mẫu 19).</summary>
    public const string Control = "CONTROL";

    public static readonly IReadOnlyList<string> Ordered = new[] { Crosscheck, Calibration, Control };

    public static string DisplayName(string gate) => gate switch
    {
        Crosscheck => "Đối soát chuyên ngành",
        Calibration => "Hiệu chuẩn kết quả",
        Control => "Kiểm soát trước phê duyệt",
        _ => gate
    };

    public static bool IsKnown(string? gate) => gate != null && (gate == Crosscheck || gate == Calibration || gate == Control);

    /// <summary>Đối soát chỉ Optional/Off; hiệu chuẩn và kiểm soát chỉ Required/Recorded/Off.</summary>
    public static bool IsAllowed(string gate, GateMode mode) => gate == Crosscheck
        ? mode is GateMode.Optional or GateMode.Off
        : mode is GateMode.Required or GateMode.Recorded or GateMode.Off;

    /// <summary>Hiệu chuẩn, kiểm soát: Required khi bước quyết định làm trong hệ thống, Recorded khi cấp trên thực hiện.</summary>
    public static GateMode DefaultMode(string gate, StepMode decisionMode) => gate == Crosscheck
        ? GateMode.Optional
        : decisionMode == StepMode.Internal ? GateMode.Required : GateMode.Recorded;
}
