using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Evaluation;

namespace CongTacDang.Application.Services;

public enum CalibrationGateState
{
    /// <summary>Hồ sơ thuộc biên bản đã chốt, còn hiệu lực.</summary>
    Satisfied = 0,

    /// <summary>Hồ sơ chưa thuộc biên bản hiệu chuẩn nào còn hiệu lực.</summary>
    NoSession = 1,

    /// <summary>Biên bản còn đang soạn.</summary>
    Pending = 2,

    /// <summary>Biên bản đã chốt nhưng hồ sơ đã đổi sau lúc chốt.</summary>
    Stale = 3
}

/// <summary>Kết quả kiểm tra điều kiện biên bản Mẫu 20 của một hồ sơ.</summary>
public sealed record CalibrationGateResult(CalibrationGateState State, Guid? SessionId)
{
    public bool IsSatisfied => State == CalibrationGateState.Satisfied;

    /// <summary>Lý do hiển thị khi chưa đạt; null khi đạt.</summary>
    public string? Reason => State switch
    {
        CalibrationGateState.NoSession => "Hồ sơ chưa thuộc biên bản hiệu chuẩn nào còn hiệu lực.",
        CalibrationGateState.Pending => "Biên bản hiệu chuẩn của hồ sơ chưa được chốt.",
        CalibrationGateState.Stale => "Hồ sơ đã thay đổi sau khi biên bản hiệu chuẩn được chốt; cần chốt lại biên bản.",
        _ => null
    };
}

/// <summary>Cổng hiệu chuẩn: chỉ đọc, không kiểm quyền (người gọi là luồng nghiệp vụ).</summary>
public interface ICalibrationGate
{
    Task<CalibrationGateResult> EvaluateAsync(Guid recordId, CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, CalibrationGateResult>> EvaluateManyAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);
}

/// <summary>Mã khóa hành động bước quyết định, giao diện chỉ hiển thị.</summary>
public static class ApprovalGateCodes
{
    public const string ControlRequired = "CONTROL_CHECK_REQUIRED";

    public const string CalibrationRequired = "CALIBRATION_REQUIRED";
}

/// <summary>Một cổng chưa đạt: chặn (Required) hoặc chỉ cảnh báo (Recorded).</summary>
public sealed record GateOutcome(string Code, string Reason, bool Blocks);

/// <summary>Quy tắc thuần ghép chế độ cổng trong hồ sơ luồng với kết quả kiểm tra; hiệu chuẩn xét trước kiểm soát.</summary>
public static class ApprovalGatePolicy
{
    public static List<GateOutcome> Evaluate(WorkflowProfile profile, CalibrationGateResult? calibration, ControlGateResult? control)
    {
        var outcomes = new List<GateOutcome>();
        Add(outcomes, profile.GateModeOf(WorkflowGates.Calibration), ApprovalGateCodes.CalibrationRequired, calibration?.IsSatisfied ?? false, calibration?.Reason);
        Add(outcomes, profile.GateModeOf(WorkflowGates.Control), ApprovalGateCodes.ControlRequired, control?.IsSatisfied ?? false, control?.Reason);
        return outcomes;
    }

    private static void Add(List<GateOutcome> outcomes, GateMode mode, string code, bool satisfied, string? reason)
    {
        if (mode is GateMode.Off or GateMode.Optional || satisfied)
            return;
        outcomes.Add(new GateOutcome(code, reason ?? "Hồ sơ chưa đáp ứng điều kiện của bước này.", mode == GateMode.Required));
    }

    /// <summary>Cổng chặn đầu tiên theo thứ tự hiệu chuẩn rồi kiểm soát; null nếu không bị chặn.</summary>
    public static GateOutcome? FirstBlock(IEnumerable<GateOutcome> outcomes) => outcomes.FirstOrDefault(o => o.Blocks);

    /// <summary>Hồ sơ luồng bắt buộc hiệu chuẩn mà chưa thuộc biên bản đã chốt còn hiệu lực; dùng cho điều kiện thứ tự của phiếu kiểm soát.</summary>
    public static List<EvaluationRecord> Uncalibrated(
        PeriodSettings settings, IEnumerable<EvaluationRecord> records, IReadOnlyDictionary<Guid, CalibrationGateResult> results) =>
        records
            .Where(r => EvaluationMapping.ProfileOf(settings, r).GateModeOf(WorkflowGates.Calibration) == GateMode.Required
                && !(results.TryGetValue(r.Id, out var result) && result.IsSatisfied))
            .ToList();

    /// <summary>Có thể chọn một người lập và một người xác nhận khác nhau từ hai tập tài khoản.</summary>
    public static bool HasDistinctPair(IReadOnlyCollection<Guid> preparers, IReadOnlyCollection<Guid> approvers) =>
        preparers.Count > 0 && approvers.Count > 0
        && !(preparers.Count == 1 && approvers.Count == 1 && preparers.First() == approvers.First());
}
