using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Quyền Mẫu 03, 19, 20 và cổng trong cấu hình kỳ (schema 3, đọc được schema 2).</summary>
public sealed class FormGatesUnitTests
{
    private static readonly string[] NewCodes =
    {
        "evaluation.crosscheck.request", "evaluation.crosscheck.respond", "evaluation.crosscheck.confirm",
        "evaluation.calibration.manage", "evaluation.calibration.conclude",
        "evaluation.control.perform", "evaluation.control.confirm", "evaluation.opinion.record"
    };

    private static List<string> Validate(PeriodSettings s) => s.Validate(WorkflowActions.IsAssignableStepPermission);

    #region Danh mục quyền

    [Fact]
    public void Permissions_NewCodesAreRegistered_WithScopeAndEvaluationModule()
    {
        foreach (var code in NewCodes)
        {
            var definition = PermissionCodes.Find(code);
            Assert.NotNull(definition);
            Assert.Equal("evaluation", definition!.Module);
            Assert.True(definition.AppliesScope);
            Assert.False(string.IsNullOrWhiteSpace(definition.Name));
            Assert.Contains(code, PermissionCodes.FormPermissions);
        }
        Assert.Equal(NewCodes.Length, PermissionCodes.FormPermissions.Count);
        Assert.Equal("Yêu cầu đối soát chuyên ngành (Mẫu 03)", PermissionCodes.DisplayName(PermissionCodes.EvaluationCrosscheckRequest));
        Assert.Equal("Lập biên bản họp thống nhất (Mẫu 20)", PermissionCodes.DisplayName(PermissionCodes.EvaluationCalibrationManage));
        Assert.Equal("Lập phiếu kiểm tra (Mẫu 19)", PermissionCodes.DisplayName(PermissionCodes.EvaluationControlPerform));
    }

    [Fact]
    public void Permissions_NewCodesAreNotAssignableAsStepPermission()
    {
        Assert.All(NewCodes, code => Assert.False(WorkflowActions.IsAssignableStepPermission(code)));
        Assert.True(WorkflowActions.IsAssignableStepPermission(PermissionCodes.EvaluationAppraise));
    }

    #endregion

    #region Cổng trong cấu hình kỳ

    [Fact]
    public void Default_HasGates_RequiredForInternalDecision_RecordedForExternal()
    {
        var settings = PeriodSettings.Default();
        Assert.Equal(3, settings.SchemaVersion);
        Assert.Empty(Validate(settings));

        var baseProfile = settings.FindProfile("co-so")!;
        Assert.Equal(GateMode.Required, baseProfile.GateModeOf(WorkflowGates.Control));
        Assert.Equal(GateMode.Required, baseProfile.GateModeOf(WorkflowGates.Calibration));
        Assert.Equal(GateMode.Optional, baseProfile.GateModeOf(WorkflowGates.Crosscheck));

        foreach (var code in new[] { "cap-tren", "pho-giam-doc" })
        {
            var profile = settings.FindProfile(code)!;
            Assert.Equal(GateMode.Recorded, profile.GateModeOf(WorkflowGates.Control));
            Assert.Equal(GateMode.Recorded, profile.GateModeOf(WorkflowGates.Calibration));
        }
        Assert.Contains("\"gates\":{\"CROSSCHECK\":{\"mode\":\"Optional\"},\"CALIBRATION\":{\"mode\":\"Required\"},\"CONTROL\":{\"mode\":\"Required\"}}", settings.ToJson());
    }

    [Fact]
    public void Parse_Schema2_UpgradesToSchema3_FillingDefaultGates()
    {
        var json = "{\"schemaVersion\":2,\"profiles\":[{\"code\":\"a\",\"name\":\"Nhóm A\",\"steps\":{\"b4_decision\":{\"mode\":\"External\"}}},"
            + "{\"code\":\"b\",\"name\":\"Nhóm B\"}],\"defaultProfiles\":{\"CoSo\":\"b\",\"CapTren\":\"a\"}}";
        var settings = PeriodSettings.Parse(json);

        Assert.Equal(3, settings.SchemaVersion);
        Assert.Empty(Validate(settings));
        Assert.Equal(GateMode.Recorded, settings.FindProfile("a")!.GateModeOf(WorkflowGates.Control));
        Assert.Equal(GateMode.Required, settings.FindProfile("b")!.GateModeOf(WorkflowGates.Control));

        // Đọc lại bản đã nâng cho cùng kết quả (không đổi nội dung khi lưu lại).
        Assert.Equal(settings.ToJson(), PeriodSettings.Parse(settings.ToJson()).ToJson());
    }

    [Fact]
    public void Parse_Schema1_StillRejected()
    {
        var old = PeriodSettings.Parse("{\"schemaVersion\":1,\"steps\":{}}");
        Assert.Equal(1, old.SchemaVersion);
        Assert.Contains(Validate(old), e => e.Contains("Phiên bản cấu hình 1", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_KeepsExplicitGateModes_AndNormalizesKeys()
    {
        var settings = PeriodSettings.Parse(
            "{\"schemaVersion\":3,\"profiles\":[{\"code\":\"a\",\"name\":\"Nhóm A\",\"gates\":{\"control\":{\"mode\":\"Off\"},"
            + "\"CALIBRATION\":{\"mode\":\"Recorded\"}}}],\"defaultProfiles\":{\"CoSo\":\"a\",\"CapTren\":\"a\"}}");
        var profile = settings.FindProfile("a")!;

        Assert.Equal(GateMode.Off, profile.GateModeOf(WorkflowGates.Control));
        Assert.Equal(GateMode.Recorded, profile.GateModeOf(WorkflowGates.Calibration));
        Assert.Equal(GateMode.Optional, profile.GateModeOf(WorkflowGates.Crosscheck));
        Assert.Empty(Validate(settings));
    }

    [Fact]
    public void Validate_RejectsUnknownGateAndDisallowedModes()
    {
        var unknown = PeriodSettings.Default();
        unknown.FindProfile("co-so")!.Gates["KHAC"] = new GateSetting { Mode = GateMode.Off };
        Assert.Contains(Validate(unknown.Normalize()), e => e.Contains("mã cổng \"KHAC\"", StringComparison.Ordinal));

        var optionalOnControl = PeriodSettings.Default();
        optionalOnControl.FindProfile("co-so")!.Gates[WorkflowGates.Control].Mode = GateMode.Optional;
        Assert.Contains(Validate(optionalOnControl.Normalize()), e => e.Contains("Kiểm tra trước quyết định", StringComparison.Ordinal));

        var requiredOnCrosscheck = PeriodSettings.Default();
        requiredOnCrosscheck.FindProfile("co-so")!.Gates[WorkflowGates.Crosscheck].Mode = GateMode.Required;
        Assert.Contains(Validate(requiredOnCrosscheck.Normalize()), e => e.Contains("Xác nhận số liệu", StringComparison.Ordinal));

        var undefined = PeriodSettings.Default();
        undefined.FindProfile("co-so")!.Gates[WorkflowGates.Calibration].Mode = (GateMode)99;
        Assert.Contains(Validate(undefined.Normalize()), e => e.Contains("Họp thống nhất", StringComparison.Ordinal));

        var allowed = PeriodSettings.Default();
        allowed.FindProfile("co-so")!.Gates[WorkflowGates.Crosscheck].Mode = GateMode.Off;
        allowed.FindProfile("co-so")!.Gates[WorkflowGates.Control].Mode = GateMode.Recorded;
        Assert.Empty(Validate(allowed.Normalize()));
    }

    [Fact]
    public void GateChange_IsNotDeadlineOnlyChange_SoOpenPeriodRejectsIt()
    {
        var current = PeriodSettings.Default();
        var changed = current.Clone();
        changed.FindProfile("co-so")!.Gates[WorkflowGates.Control].Mode = GateMode.Off;
        Assert.False(changed.DiffersOnlyInDeadlines(current));
        Assert.True(current.Clone().DiffersOnlyInDeadlines(current));
    }

    #endregion
}
