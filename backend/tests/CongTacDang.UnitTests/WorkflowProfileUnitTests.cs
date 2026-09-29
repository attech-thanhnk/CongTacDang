using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Task 15: hồ sơ luồng (Internal / External / Off) trong cấu hình kỳ — kiểu kỳ dựng sẵn, kiểm tra cấu hình, JSON,
/// máy trạng thái theo hồ sơ luồng, quyền thực hiện bước và khớp chế độ bước của hành động.
/// </summary>
public sealed class WorkflowProfileUnitTests
{
    private static List<string> Validate(PeriodSettings settings) => settings.Validate(WorkflowActions.IsAssignableStepPermission);

    private static WorkflowProfile Profile(PeriodSettings settings, string code) => settings.FindProfile(code)!;

    #region Kiểu kỳ dựng sẵn

    [Fact]
    public void Presets_AreValid_WithThreeProfiles_MatchingAppendixIII()
    {
        foreach (var preset in PeriodSettings.Presets)
            Assert.Empty(Validate(preset.Build()));
        Assert.Equal(new[] { "full", "q3-2026-transition" }, PeriodSettings.Presets.Select(p => p.Code));
        Assert.NotNull(PeriodSettings.FindPreset("FULL"));
        Assert.Null(PeriodSettings.FindPreset("khac"));

        var full = PeriodSettings.FullPreset();
        Assert.Equal("09A", full.SelfScoreForm);
        Assert.Equal(new[] { "co-so", "cap-tren", "bi-thu-nhan-vien" }, full.Profiles.Select(p => p.Code));
        Assert.Equal("co-so", full.DefaultProfileCode(ApprovalAuthority.CoSo));
        Assert.Equal("cap-tren", full.DefaultProfileCode(ApprovalAuthority.CapTren));

        // Ví dụ 1: mọi bước nội bộ, quyền mặc định theo thiết kế.
        var baseProfile = Profile(full, "co-so");
        Assert.All(WorkflowSteps.Ordered, s => Assert.Equal(StepMode.Internal, baseProfile.Mode(s)));
        Assert.Equal(PermissionCodes.EvaluationDirectorReview, baseProfile.PermissionFor(WorkflowStep.B3C_DIRECTOR));
        Assert.Equal(PermissionCodes.EvaluationDecide, baseProfile.PermissionFor(WorkflowStep.B4_DECISION));

        // Ví dụ 2: B3a nội bộ, B3b/B3c/B4 cấp trên thực hiện.
        var upper = Profile(full, "cap-tren");
        Assert.Equal(StepMode.Internal, upper.Mode(WorkflowStep.B3A_COLLECTIVE));
        foreach (var step in new[] { WorkflowStep.B3B_APPRAISAL, WorkflowStep.B3C_DIRECTOR, WorkflowStep.B4_DECISION })
        {
            Assert.Equal(StepMode.External, upper.Mode(step));
            Assert.Equal(PermissionCodes.EvaluationExternalRecord, upper.PermissionFor(step));
        }
        Assert.Equal(StepMode.Internal, upper.Mode(WorkflowStep.B5_PUBLISH));

        // Ví dụ 3: B3c do Lãnh đạo đơn vị (Trưởng phòng) thực hiện thay Giám đốc.
        var staff = Profile(full, "bi-thu-nhan-vien");
        Assert.Equal(StepMode.Internal, staff.Mode(WorkflowStep.B3C_DIRECTOR));
        Assert.Equal(PermissionCodes.EvaluationUnitReview, staff.PermissionFor(WorkflowStep.B3C_DIRECTOR));

        // Quý III/2026: B1 không áp dụng ở mọi hồ sơ luồng, 09B.
        var transition = PeriodSettings.TransitionQ3Preset();
        Assert.Equal("09B", transition.SelfScoreForm);
        Assert.True(transition.UsesAxisScoring);
        Assert.Equal(3, transition.Profiles.Count);
        Assert.All(transition.Profiles, p =>
        {
            Assert.Equal(StepMode.Off, p.Mode(WorkflowStep.B1_REGISTER));
            Assert.Equal(StepMode.Off, p.Mode(WorkflowStep.B1_APPROVE));
            Assert.Equal(7, p.ActiveSteps().Count);
        });
        Assert.Equal(StepMode.External, Profile(transition, "cap-tren").Mode(WorkflowStep.B4_DECISION));
    }

    [Fact]
    public void DomainPermissionLiterals_ExistInPermissionCatalog()
    {
        var literals = new[]
        {
            WorkflowPermissions.Self, WorkflowPermissions.TasksApprove, WorkflowPermissions.CellConfirm, WorkflowPermissions.CollectiveRecord,
            WorkflowPermissions.Appraise, WorkflowPermissions.DirectorReview, WorkflowPermissions.UnitReview, WorkflowPermissions.Decide,
            WorkflowPermissions.Publish, WorkflowPermissions.ExternalRecord
        };
        Assert.All(literals, code => Assert.True(PermissionCodes.IsDefined(code), code));
        Assert.All(WorkflowSteps.Ordered, step => Assert.True(PermissionCodes.IsDefined(WorkflowPermissions.DefaultFor(step))));

        Assert.Contains("evaluation.external.record", PermissionCodes.All);
        Assert.Contains("evaluation.unit.review", PermissionCodes.All);
    }

    [Fact]
    public void AssignableStepPermissions_AreEvaluationCodes_ExceptSelfReadReopen()
    {
        var codes = WorkflowActions.AssignableStepPermissions().Select(d => d.Code).ToList();
        Assert.Contains(PermissionCodes.EvaluationUnitReview, codes);
        Assert.Contains(PermissionCodes.EvaluationDirectorReview, codes);
        Assert.DoesNotContain(PermissionCodes.EvaluationSelf, codes);
        Assert.DoesNotContain(PermissionCodes.EvaluationRead, codes);
        Assert.DoesNotContain(PermissionCodes.EvaluationReopen, codes);
        Assert.DoesNotContain(PermissionCodes.SystemRolesManage, codes);
        Assert.False(WorkflowActions.IsAssignableStepPermission("khong.ton.tai"));
    }

    #endregion

    #region Kiểm tra cấu hình

    [Theory]
    [InlineData(WorkflowStep.B2_SELF_SCORE)]
    [InlineData(WorkflowStep.B4_DECISION)]
    [InlineData(WorkflowStep.B5_PUBLISH)]
    public void Validate_MandatoryStepsCannotBeOff(WorkflowStep step)
    {
        var settings = PeriodSettings.FullPreset();
        Profile(settings, "co-so").Step(step).Mode = StepMode.Off;
        Assert.Contains(Validate(settings.Normalize()), e => e.Contains("bước bắt buộc", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(WorkflowStep.B1_REGISTER)]
    [InlineData(WorkflowStep.B2_SELF_SCORE)]
    [InlineData(WorkflowStep.B5_PUBLISH)]
    public void Validate_OwnerStepsAndPublishCannotBeExternal(WorkflowStep step)
    {
        var settings = PeriodSettings.FullPreset();
        Profile(settings, "co-so").Step(step).Mode = StepMode.External;
        Assert.Contains(Validate(settings.Normalize()), e => e.Contains("không giao cho cấp trên", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AppraisalCanBeOff_AndAnyNonMandatoryStepCanBeExternal()
    {
        var settings = PeriodSettings.FullPreset();
        var profile = Profile(settings, "co-so");
        profile.Step(WorkflowStep.B3B_APPRAISAL).Mode = StepMode.Off;
        profile.Step(WorkflowStep.B3A_COLLECTIVE).Mode = StepMode.External;
        profile.Step(WorkflowStep.B2_CELL_CONFIRM).Mode = StepMode.External;
        Assert.Empty(Validate(settings.Normalize()));
    }

    [Fact]
    public void Validate_RejectsInconsistentConfigurations()
    {
        var approveWithoutRegister = PeriodSettings.FullPreset();
        approveWithoutRegister.SelfScoreForm = "09B";
        Profile(approveWithoutRegister, "co-so").Step(WorkflowStep.B1_REGISTER).Mode = StepMode.Off;
        Assert.Contains(Validate(approveWithoutRegister.Normalize()), e => e.Contains("duyệt danh mục", StringComparison.Ordinal));

        var aWithoutRegister = PeriodSettings.TransitionQ3Preset();
        aWithoutRegister.SelfScoreForm = "09A";
        Assert.Contains(Validate(aWithoutRegister.Normalize()), e => e.Contains("09A", StringComparison.Ordinal));

        var badForm = PeriodSettings.FullPreset();
        badForm.SelfScoreForm = "09C";
        Assert.Contains(Validate(badForm.Normalize()), e => e.Contains("Mẫu tự chấm", StringComparison.Ordinal));

        var oldVersion = PeriodSettings.Parse("{\"schemaVersion\":1,\"steps\":{\"B1_REGISTER\":{\"enabled\":true}}}");
        Assert.Contains(Validate(oldVersion), e => e.Contains("Phiên bản cấu hình 1", StringComparison.Ordinal));
        Assert.Contains(Validate(oldVersion), e => e.Contains("ít nhất một hồ sơ luồng", StringComparison.Ordinal));

        var unknownStep = PeriodSettings.FullPreset();
        Profile(unknownStep, "co-so").Steps["B9_UNKNOWN"] = new ProfileStepSetting();
        Assert.Contains(Validate(unknownStep.Normalize()), e => e.Contains("B9_UNKNOWN", StringComparison.Ordinal));

        var duplicate = PeriodSettings.FullPreset();
        duplicate.Profiles[1].Code = "CO-SO";
        Assert.Contains(Validate(duplicate.Normalize()), e => e.Contains("bị trùng", StringComparison.Ordinal));

        var badCode = PeriodSettings.FullPreset();
        badCode.Profiles[2].Code = "bí thư";
        Assert.Contains(Validate(badCode.Normalize()), e => e.Contains("không hợp lệ", StringComparison.Ordinal));

        var noName = PeriodSettings.FullPreset();
        noName.Profiles[2].Name = " ";
        Assert.Contains(Validate(noName.Normalize()), e => e.Contains("chưa có tên", StringComparison.Ordinal));

        var missingDefault = PeriodSettings.FullPreset();
        missingDefault.DefaultProfiles.Remove("CapTren");
        Assert.Contains(Validate(missingDefault), e => e.Contains("cấp trên quyết định", StringComparison.Ordinal));

        var wrongDefault = PeriodSettings.FullPreset();
        wrongDefault.DefaultProfiles["CoSo"] = "khong-co";
        Assert.Contains(Validate(wrongDefault), e => e.Contains("\"khong-co\"", StringComparison.Ordinal));

        var empty = PeriodSettings.FullPreset();
        empty.Profiles.Clear();
        Assert.Contains(Validate(empty), e => e.Contains("ít nhất một hồ sơ luồng", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("system.roles.manage")]
    [InlineData("evaluation.self")]
    [InlineData("evaluation.read")]
    [InlineData("evaluation.khong.ton.tai")]
    [InlineData("khong.ton.tai")]
    public void Validate_InternalStepPermission_MustBeAssignableEvaluationCode(string permission)
    {
        var settings = PeriodSettings.FullPreset();
        Profile(settings, "co-so").Step(WorkflowStep.B3C_DIRECTOR).Permission = permission;
        Assert.Contains(Validate(settings.Normalize()), e => e.Contains("quyền thực hiện bước", StringComparison.Ordinal));
        // Không truyền bộ kiểm tra mã quyền → Domain chỉ kiểm cấu trúc.
        Assert.Empty(settings.Validate());
    }

    [Fact]
    public void Normalize_KeepsPermissionOnlyForInternalNonOwnerSteps_FillsDefault()
    {
        var profile = new WorkflowProfile { Code = " Moi ", Name = " Mới ", Steps = new Dictionary<string, ProfileStepSetting>
        {
            ["b2_self_score"] = new() { Permission = "evaluation.appraise" },
            ["B3B_APPRAISAL"] = new() { Mode = StepMode.External, Permission = "evaluation.appraise" },
            ["B3C_DIRECTOR"] = new() { Permission = " " }
        } }.Normalize();

        Assert.Equal("moi", profile.Code);
        Assert.Equal("Mới", profile.Name);
        Assert.Equal(9, profile.Steps.Count);
        Assert.Null(profile.Step(WorkflowStep.B2_SELF_SCORE).Permission);
        Assert.Equal(PermissionCodes.EvaluationSelf, profile.PermissionFor(WorkflowStep.B2_SELF_SCORE));
        Assert.Null(profile.Step(WorkflowStep.B3B_APPRAISAL).Permission);
        Assert.Equal(PermissionCodes.EvaluationDirectorReview, profile.Step(WorkflowStep.B3C_DIRECTOR).Permission);
    }

    #endregion

    #region JSON, thời hạn, tra cứu

    [Fact]
    public void Json_RoundTrip_ModeAsString_EmptyMeansFullPreset()
    {
        var json = PeriodSettings.TransitionQ3Preset().ToJson();
        Assert.Contains("\"schemaVersion\":2", json);
        Assert.Contains("\"mode\":\"External\"", json);
        Assert.Contains("\"mode\":\"Off\"", json);
        Assert.Contains("\"defaultProfiles\":{\"CoSo\":\"co-so\",\"CapTren\":\"cap-tren\"}", json);
        Assert.Equal(json, PeriodSettings.Parse(json).ToJson());

        Assert.Equal(PeriodSettings.FullPreset().ToJson(), PeriodSettings.Parse(null).ToJson());
        Assert.Equal(PeriodSettings.FullPreset().ToJson(), PeriodSettings.Parse("  ").ToJson());

        // Bước thiếu được bổ sung (nội bộ, quyền mặc định); khóa bước, khóa cấp quyết định không phân biệt hoa thường.
        var partial = PeriodSettings.Parse(
            "{\"schemaVersion\":2,\"profiles\":[{\"code\":\"A\",\"name\":\"Nhóm A\",\"steps\":{\"b1_register\":{\"mode\":\"Off\",\"deadline\":\"2026-10-05\"},"
            + "\"b1_approve\":{\"mode\":\"Off\"}}}],\"defaultProfiles\":{\"coso\":\"a\",\"CAPTREN\":\"A\"},\"selfScoreForm\":\"09b\"}");
        var a = partial.FindProfile("a")!;
        Assert.Equal(9, a.Steps.Count);
        Assert.False(a.IsActive(WorkflowStep.B1_REGISTER));
        Assert.Equal(new DateOnly(2026, 10, 5), a.Deadline(WorkflowStep.B1_REGISTER));
        Assert.Equal("09B", partial.SelfScoreForm);
        Assert.Empty(Validate(partial));
        Assert.Equal("a", partial.DefaultProfileCode(ApprovalAuthority.CapTren));

        Assert.Throws<FormatException>(() => PeriodSettings.Parse("{không phải json"));
        Assert.Throws<FormatException>(() => PeriodSettings.Parse("{\"profiles\":[{\"steps\":{\"B1_REGISTER\":{\"mode\":\"Khac\"}}}]}"));
    }

    [Fact]
    public void DiffersOnlyInDeadlines_AcrossProfiles()
    {
        var current = PeriodSettings.FullPreset();
        var deadlines = current.Clone();
        Profile(deadlines, "cap-tren").Step(WorkflowStep.B4_DECISION).Deadline = new DateOnly(2026, 12, 20);
        Assert.True(deadlines.DiffersOnlyInDeadlines(current));

        var mode = current.Clone();
        Profile(mode, "co-so").Step(WorkflowStep.B3C_DIRECTOR).Mode = StepMode.Off;
        Assert.False(mode.DiffersOnlyInDeadlines(current));

        var permission = current.Clone();
        Profile(permission, "co-so").Step(WorkflowStep.B3C_DIRECTOR).Permission = PermissionCodes.EvaluationUnitReview;
        Assert.False(permission.DiffersOnlyInDeadlines(current));

        var added = current.Clone();
        added.Profiles.Add(WorkflowProfile.Create("moi", "Mới", null));
        Assert.False(added.DiffersOnlyInDeadlines(current));
    }

    [Fact]
    public void ResolveProfile_ByCode_ThenDefaultByAuthority_ThenFirst()
    {
        var settings = PeriodSettings.FullPreset();
        Assert.Equal("bi-thu-nhan-vien", settings.ResolveProfile("BI-THU-NHAN-VIEN", ApprovalAuthority.CapTren).Code);
        Assert.Equal("cap-tren", settings.ResolveProfile(null, ApprovalAuthority.CapTren).Code);
        Assert.Equal("co-so", settings.ResolveProfile("khong-con", ApprovalAuthority.CoSo).Code);
        settings.DefaultProfiles.Clear();
        Assert.Equal("co-so", settings.ResolveProfile(null, ApprovalAuthority.CapTren).Code);
        settings.Profiles.Clear();
        Assert.Equal(9, settings.ResolveProfile(null, ApprovalAuthority.CoSo).ActiveSteps().Count);
    }

    [Fact]
    public void DifferingSteps_IgnoreDeadlines()
    {
        var settings = PeriodSettings.FullPreset();
        var baseProfile = Profile(settings, "co-so");
        Assert.Equal(new[] { WorkflowStep.B3B_APPRAISAL, WorkflowStep.B3C_DIRECTOR, WorkflowStep.B4_DECISION },
            baseProfile.DifferingSteps(Profile(settings, "cap-tren")));
        Assert.Equal(new[] { WorkflowStep.B3C_DIRECTOR }, baseProfile.DifferingSteps(Profile(settings, "bi-thu-nhan-vien")));

        var withDeadline = PeriodSettings.FullPreset();
        Profile(withDeadline, "co-so").Step(WorkflowStep.B2_SELF_SCORE).Deadline = new DateOnly(2026, 12, 11);
        Assert.Empty(baseProfile.DifferingSteps(Profile(withDeadline, "co-so")));
    }

    #endregion

    #region Máy trạng thái theo hồ sơ luồng

    [Fact]
    public void StateMachine_ExternalStepsAreActive_OffStepsAreSkipped()
    {
        var upper = Profile(PeriodSettings.FullPreset(), "cap-tren");
        var status = RecordStateMachine.Initial(upper.ActiveSteps());
        var walked = new List<WorkflowStep>();
        while (WorkflowSteps.StepOf(status) is { } step)
        {
            walked.Add(step);
            var result = RecordStateMachine.Apply(status, upper.ActiveSteps(), WorkflowCommand.Complete(step));
            Assert.True(result.Succeeded, result.Error);
            status = result.Status;
        }
        Assert.Equal(WorkflowSteps.Ordered, walked);

        var custom = WorkflowProfile.Create("x", "X", null, new Dictionary<WorkflowStep, StepMode>
        {
            [WorkflowStep.B1_REGISTER] = StepMode.Off,
            [WorkflowStep.B1_APPROVE] = StepMode.Off,
            [WorkflowStep.B3A_COLLECTIVE] = StepMode.Off,
            [WorkflowStep.B3B_APPRAISAL] = StepMode.Off,
            [WorkflowStep.B3C_DIRECTOR] = StepMode.External
        });
        var active = custom.ActiveSteps();
        Assert.Equal(RecordStatus.AwaitingSelfScore, RecordStateMachine.Initial(active));
        Assert.Equal(RecordStatus.AwaitingDirectorReview, RecordStateMachine.NextAfter(WorkflowStep.B2_CELL_CONFIRM, active));
        // Thẩm định không áp dụng → không trả lại ở bước thẩm định được.
        Assert.False(RecordStateMachine.Apply(RecordStatus.AwaitingAppraisal, active, WorkflowCommand.Return(WorkflowStep.B3B_APPRAISAL)).Succeeded);
    }

    [Fact]
    public void Realign_MovesForwardOnlyWhenCurrentStepNoLongerApplies()
    {
        var baseProfile = Profile(PeriodSettings.FullPreset(), "co-so");
        var transitionBase = Profile(PeriodSettings.TransitionQ3Preset(), "co-so");
        var noDirector = WorkflowProfile.Create("x", "X", null, new Dictionary<WorkflowStep, StepMode> { [WorkflowStep.B3C_DIRECTOR] = StepMode.Off });

        Assert.Equal(RecordStatus.AwaitingSelfScore, RecordStateMachine.Realign(RecordStatus.AwaitingRegistration, transitionBase.ActiveSteps()));
        Assert.Equal(RecordStatus.AwaitingDecision, RecordStateMachine.Realign(RecordStatus.AwaitingDirectorReview, noDirector.ActiveSteps()));
        Assert.Equal(RecordStatus.AwaitingAppraisal, RecordStateMachine.Realign(RecordStatus.AwaitingAppraisal, noDirector.ActiveSteps()));
        Assert.Equal(RecordStatus.Published, RecordStateMachine.Realign(RecordStatus.Published, baseProfile.ActiveSteps()));
    }

    #endregion

    #region Hành động theo chế độ bước

    [Fact]
    public void PermissionFor_FollowsProfileMode()
    {
        var settings = PeriodSettings.FullPreset();
        var baseProfile = Profile(settings, "co-so");
        var upper = Profile(settings, "cap-tren");
        var staff = Profile(settings, "bi-thu-nhan-vien");

        Assert.Equal(PermissionCodes.EvaluationDecide, WorkflowActions.PermissionFor(WorkflowActions.CompleteOf(WorkflowStep.B4_DECISION), baseProfile));
        Assert.Equal(PermissionCodes.EvaluationExternalRecord,
            WorkflowActions.PermissionFor(WorkflowActions.ExternalOf(WorkflowStep.B4_DECISION), upper));
        Assert.Equal(PermissionCodes.EvaluationUnitReview, WorkflowActions.PermissionFor(WorkflowActions.CompleteOf(WorkflowStep.B3C_DIRECTOR), staff));
        Assert.Equal(PermissionCodes.EvaluationSelf, WorkflowActions.PermissionFor(WorkflowActions.CompleteOf(WorkflowStep.B2_SELF_SCORE), staff));
        Assert.Equal(PermissionCodes.EvaluationReopen, WorkflowActions.PermissionFor(WorkflowActions.Get(WorkflowActions.Reopen), upper));

        Assert.Equal(WorkflowAction.RecordExternal, WorkflowActions.AdvanceOf(WorkflowStep.B3B_APPRAISAL, upper)!.Kind);
        Assert.Equal(WorkflowAction.Complete, WorkflowActions.AdvanceOf(WorkflowStep.B3B_APPRAISAL, baseProfile)!.Kind);
        Assert.Null(WorkflowActions.AdvanceOf(WorkflowStep.B1_REGISTER, Profile(PeriodSettings.TransitionQ3Preset(), "co-so")));
    }

    [Fact]
    public void ModeBlockReason_RejectsActionNotMatchingStepMode()
    {
        var settings = PeriodSettings.TransitionQ3Preset();
        var baseProfile = Profile(settings, "co-so");
        var upper = Profile(settings, "cap-tren");

        Assert.Null(WorkflowActions.ModeBlockReason(WorkflowActions.CompleteOf(WorkflowStep.B4_DECISION), baseProfile));
        Assert.Contains("do cấp trên thực hiện",
            WorkflowActions.ModeBlockReason(WorkflowActions.CompleteOf(WorkflowStep.B4_DECISION), upper));
        Assert.Contains("không ghi nhận kết quả của cấp trên",
            WorkflowActions.ModeBlockReason(WorkflowActions.ExternalOf(WorkflowStep.B4_DECISION), baseProfile));
        Assert.Contains("không áp dụng",
            WorkflowActions.ModeBlockReason(WorkflowActions.CompleteOf(WorkflowStep.B1_REGISTER), baseProfile));
        Assert.Null(WorkflowActions.ModeBlockReason(WorkflowActions.Get(WorkflowActions.Reopen), upper));
    }

    #endregion
}
