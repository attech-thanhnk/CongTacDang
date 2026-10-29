using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Quy tắc ghép chế độ cổng với kết quả kiểm tra, điều kiện thứ tự Mẫu 19 sau Mẫu 20 và nhãn bước khi có hiệu chuẩn.</summary>
public sealed class ApprovalGatePolicyTests
{
    private static WorkflowProfile Profile(GateMode calibration, GateMode control)
    {
        var settings = PeriodSettings.Default();
        var profile = settings.Profiles[0];
        profile.Gates[WorkflowGates.Calibration] = new GateSetting { Mode = calibration };
        profile.Gates[WorkflowGates.Control] = new GateSetting { Mode = control };
        return profile;
    }

    private static readonly CalibrationGateResult CalibrationOk = new(CalibrationGateState.Satisfied, null);
    private static readonly ControlGateResult ControlOk = new(ControlGateState.Satisfied, null);

    [Theory]
    [InlineData(GateMode.Required, ControlGateState.Satisfied, 0, false)]
    [InlineData(GateMode.Required, ControlGateState.NoCheck, 1, true)]
    [InlineData(GateMode.Required, ControlGateState.Pending, 1, true)]
    [InlineData(GateMode.Required, ControlGateState.NeedsWork, 1, true)]
    [InlineData(GateMode.Required, ControlGateState.Stale, 1, true)]
    [InlineData(GateMode.Recorded, ControlGateState.Satisfied, 0, false)]
    [InlineData(GateMode.Recorded, ControlGateState.NoCheck, 1, false)]
    [InlineData(GateMode.Recorded, ControlGateState.Stale, 1, false)]
    [InlineData(GateMode.Off, ControlGateState.NoCheck, 0, false)]
    [InlineData(GateMode.Off, ControlGateState.Stale, 0, false)]
    public void Control_ModeAndState(GateMode mode, ControlGateState state, int outcomes, bool blocks)
    {
        var result = ApprovalGatePolicy.Evaluate(Profile(GateMode.Off, mode), null, new ControlGateResult(state, null));

        Assert.Equal(outcomes, result.Count);
        Assert.Equal(blocks, ApprovalGatePolicy.FirstBlock(result) != null);
        Assert.All(result, o => Assert.Equal(ApprovalGateCodes.ControlRequired, o.Code));
    }

    [Theory]
    [InlineData(GateMode.Required, CalibrationGateState.Satisfied, 0, false)]
    [InlineData(GateMode.Required, CalibrationGateState.NoSession, 1, true)]
    [InlineData(GateMode.Required, CalibrationGateState.Pending, 1, true)]
    [InlineData(GateMode.Required, CalibrationGateState.Stale, 1, true)]
    [InlineData(GateMode.Recorded, CalibrationGateState.NoSession, 1, false)]
    [InlineData(GateMode.Recorded, CalibrationGateState.Satisfied, 0, false)]
    [InlineData(GateMode.Off, CalibrationGateState.NoSession, 0, false)]
    public void Calibration_ModeAndState(GateMode mode, CalibrationGateState state, int outcomes, bool blocks)
    {
        var result = ApprovalGatePolicy.Evaluate(Profile(mode, GateMode.Off), new CalibrationGateResult(state, null), null);

        Assert.Equal(outcomes, result.Count);
        Assert.Equal(blocks, ApprovalGatePolicy.FirstBlock(result) != null);
        Assert.All(result, o => Assert.Equal(ApprovalGateCodes.CalibrationRequired, o.Code));
    }

    [Fact]
    public void BothUnmet_CalibrationReportedFirst()
    {
        var result = ApprovalGatePolicy.Evaluate(Profile(GateMode.Required, GateMode.Required),
            new CalibrationGateResult(CalibrationGateState.NoSession, null), new ControlGateResult(ControlGateState.NoCheck, null));

        Assert.Equal(2, result.Count);
        Assert.Equal(ApprovalGateCodes.CalibrationRequired, ApprovalGatePolicy.FirstBlock(result)!.Code);
        Assert.Equal(ApprovalGateCodes.ControlRequired, result[1].Code);
    }

    [Fact]
    public void BothSatisfied_NothingReported()
    {
        Assert.Empty(ApprovalGatePolicy.Evaluate(Profile(GateMode.Required, GateMode.Required), CalibrationOk, ControlOk));
    }

    [Fact]
    public void Uncalibrated_OnlyRequiredProfilesWithoutConcludedSession()
    {
        var settings = PeriodSettings.Default();
        foreach (var profile in settings.Profiles)
            profile.Gates[WorkflowGates.Calibration] = new GateSetting { Mode = GateMode.Required };
        var required = Record("A");
        var missing = Record("B");
        var results = new Dictionary<Guid, CalibrationGateResult>
        {
            [required.Id] = CalibrationOk,
            [missing.Id] = new CalibrationGateResult(CalibrationGateState.Pending, null)
        };

        // Cổng hiệu chuẩn Required.
        var list = ApprovalGatePolicy.Uncalibrated(settings, new[] { required, missing }, results);
        Assert.Equal(new[] { missing.Id }, list.Select(r => r.Id));

        // Không có kết quả cho hồ sơ coi như chưa hiệu chuẩn.
        Assert.Equal(2, ApprovalGatePolicy.Uncalibrated(settings, new[] { required, missing }, new Dictionary<Guid, CalibrationGateResult>()).Count);

        foreach (var profile in settings.Profiles)
            profile.Gates[WorkflowGates.Calibration] = new GateSetting { Mode = GateMode.Recorded };
        Assert.Empty(ApprovalGatePolicy.Uncalibrated(settings, new[] { required, missing }, results));
        foreach (var profile in settings.Profiles)
            profile.Gates[WorkflowGates.Calibration] = new GateSetting { Mode = GateMode.Off };
        Assert.Empty(ApprovalGatePolicy.Uncalibrated(settings, new[] { required, missing }, results));
    }

    [Fact]
    public void HasDistinctPair_NeedsTwoDifferentAccounts()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Assert.False(ApprovalGatePolicy.HasDistinctPair(Array.Empty<Guid>(), new[] { a }));
        Assert.False(ApprovalGatePolicy.HasDistinctPair(new[] { a }, Array.Empty<Guid>()));
        Assert.False(ApprovalGatePolicy.HasDistinctPair(new[] { a }, new[] { a }));
        Assert.True(ApprovalGatePolicy.HasDistinctPair(new[] { a }, new[] { b }));
        Assert.True(ApprovalGatePolicy.HasDistinctPair(new[] { a, b }, new[] { a }));
        Assert.True(ApprovalGatePolicy.HasDistinctPair(new[] { a }, new[] { a, b }));
    }

    [Fact]
    public void EffectiveStep_CalibratedAfterDirector_IsAppraisalStage()
    {
        var record = Record("A");
        record.AppraisalProposedGrade = EvaluationGrade.HoanThanhTot;
        record.DirectorProposedGrade = EvaluationGrade.HoanThanhTot;
        record.DirectorReviewedAt = DateTime.UtcNow.AddHours(-2);
        Assert.Equal(WorkflowStep.B3C_DIRECTOR, GradeProposalRules.EffectiveStep(record));

        record.CalibratedGrade = EvaluationGrade.HoanThanh;
        record.CalibratedAt = DateTime.UtcNow;
        Assert.Equal(WorkflowStep.B3B_APPRAISAL, GradeProposalRules.EffectiveStep(record));
        Assert.Equal(EvaluationGrade.HoanThanh, record.EffectiveGrade());

        // Cấp trực tiếp sử dụng nhận xét sau hiệu chuẩn thì quay lại bước của cấp đó.
        record.DirectorReviewedAt = DateTime.UtcNow.AddMinutes(1);
        Assert.Equal(WorkflowStep.B3C_DIRECTOR, GradeProposalRules.EffectiveStep(record));

        record.FinalGrade = EvaluationGrade.HoanThanh;
        Assert.Equal(WorkflowStep.B4_DECISION, GradeProposalRules.EffectiveStep(record));
    }

    private static EvaluationRecord Record(string name) => new()
    {
        Id = Guid.NewGuid(), MemberId = Guid.NewGuid(), Member = new PartyMemberProfile { FullName = name },
        ApprovalAuthority = ApprovalAuthority.CoSo
    };
}
