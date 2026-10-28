using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Diện quyết định (Internal / External / Off): cấu hình mặc định, kiểm tra, JSON, máy trạng thái, quyền thực hiện bước.</summary>
public sealed class WorkflowProfileUnitTests
{
    private static List<string> Validate(PeriodSettings settings) => settings.Validate(WorkflowActions.IsAssignableStepPermission);

    private static WorkflowProfile Profile(PeriodSettings settings, string code) => settings.FindProfile(code)!;

    #region Cấu hình mặc định

    [Fact]
    public void Default_IsValid_WithFourProfiles_MatchingAppendixIIIAndDeputyDirector()
    {
        var full = PeriodSettings.Default();
        Assert.Empty(full.Validate(WorkflowActions.IsAssignableStepPermission));
        Assert.Equal(PeriodSettings.Default().ToJson(), full.ToJson());
        Assert.Equal(new[] { "co-so", "cap-tren", "bi-thu-nhan-vien", "pho-giam-doc" }, full.Profiles.Select(p => p.Code));
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

        // Chủ tịch, Giám đốc, Kiểm soát viên: HĐTV Tổng công ty duyệt danh mục sản phẩm (cấp trên thực hiện).
        Assert.Equal(StepMode.External, upper.Mode(WorkflowStep.B1_APPROVE));

        // Phó Giám đốc: tập thể Công ty, Chủ tịch nhận xét trong hệ thống; không có bước thẩm định riêng; BTV quyết định.
        var deputy = Profile(full, "pho-giam-doc");
        Assert.Equal(StepMode.Internal, deputy.Mode(WorkflowStep.B1_APPROVE));
        Assert.Equal(StepMode.Internal, deputy.Mode(WorkflowStep.B3A_COLLECTIVE));
        Assert.Equal(StepMode.Off, deputy.Mode(WorkflowStep.B3B_APPRAISAL));
        Assert.Equal(StepMode.Internal, deputy.Mode(WorkflowStep.B3C_DIRECTOR));
        Assert.Equal(StepMode.External, deputy.Mode(WorkflowStep.B4_DECISION));

        // Đăng ký sản phẩm luôn do chủ hồ sơ thực hiện ở mọi diện quyết định.
        Assert.All(full.Profiles, p => Assert.Equal(StepMode.Internal, p.Mode(WorkflowStep.B1_REGISTER)));
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
    [InlineData(WorkflowStep.B1_REGISTER)]
    [InlineData(WorkflowStep.B2_SELF_SCORE)]
    [InlineData(WorkflowStep.B4_DECISION)]
    [InlineData(WorkflowStep.B5_PUBLISH)]
    public void Validate_MandatoryStepsCannotBeOff(WorkflowStep step)
    {
        var settings = PeriodSettings.Default();
        Profile(settings, "co-so").Step(step).Mode = StepMode.Off;
        Assert.Contains(Validate(settings.Normalize()), e => e.Contains("bước bắt buộc", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(WorkflowStep.B1_REGISTER)]
    [InlineData(WorkflowStep.B2_SELF_SCORE)]
    [InlineData(WorkflowStep.B5_PUBLISH)]
    public void Validate_OwnerStepsAndPublishCannotBeExternal(WorkflowStep step)
    {
        var settings = PeriodSettings.Default();
        Profile(settings, "co-so").Step(step).Mode = StepMode.External;
        Assert.Contains(Validate(settings.Normalize()), e => e.Contains("không giao cho cấp trên", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AppraisalCanBeOff_AndAnyNonMandatoryStepCanBeExternal()
    {
        var settings = PeriodSettings.Default();
        var profile = Profile(settings, "co-so");
        profile.Step(WorkflowStep.B3B_APPRAISAL).Mode = StepMode.Off;
        profile.Step(WorkflowStep.B3A_COLLECTIVE).Mode = StepMode.External;
        profile.Step(WorkflowStep.B2_CELL_CONFIRM).Mode = StepMode.External;
        Assert.Empty(Validate(settings.Normalize()));
    }

    [Fact]
    public void Validate_RejectsInconsistentConfigurations()
    {
        var approveWithoutRegister = PeriodSettings.Default();
        Profile(approveWithoutRegister, "co-so").Step(WorkflowStep.B1_REGISTER).Mode = StepMode.Off;
        Assert.Contains(Validate(approveWithoutRegister.Normalize()), e => e.Contains("duyệt danh mục", StringComparison.Ordinal));

        var oldVersion = PeriodSettings.Parse("{\"schemaVersion\":1,\"steps\":{\"B1_REGISTER\":{\"enabled\":true}}}");
        Assert.Contains(Validate(oldVersion), e => e.Contains("Phiên bản cấu hình 1", StringComparison.Ordinal));
        Assert.Contains(Validate(oldVersion), e => e.Contains("ít nhất một diện quyết định", StringComparison.Ordinal));

        var unknownStep = PeriodSettings.Default();
        Profile(unknownStep, "co-so").Steps["B9_UNKNOWN"] = new ProfileStepSetting();
        Assert.Contains(Validate(unknownStep.Normalize()), e => e.Contains("B9_UNKNOWN", StringComparison.Ordinal));

        var duplicate = PeriodSettings.Default();
        duplicate.Profiles[1].Code = "CO-SO";
        Assert.Contains(Validate(duplicate.Normalize()), e => e.Contains("bị trùng", StringComparison.Ordinal));

        var badCode = PeriodSettings.Default();
        badCode.Profiles[2].Code = "bí thư";
        Assert.Contains(Validate(badCode.Normalize()), e => e.Contains("không hợp lệ", StringComparison.Ordinal));

        var noName = PeriodSettings.Default();
        noName.Profiles[2].Name = " ";
        Assert.Contains(Validate(noName.Normalize()), e => e.Contains("chưa có tên", StringComparison.Ordinal));

        var missingDefault = PeriodSettings.Default();
        missingDefault.DefaultProfiles.Remove("CapTren");
        Assert.Contains(Validate(missingDefault), e => e.Contains("cấp trên quyết định", StringComparison.Ordinal));

        var wrongDefault = PeriodSettings.Default();
        wrongDefault.DefaultProfiles["CoSo"] = "khong-co";
        Assert.Contains(Validate(wrongDefault), e => e.Contains("\"khong-co\"", StringComparison.Ordinal));

        var empty = PeriodSettings.Default();
        empty.Profiles.Clear();
        Assert.Contains(Validate(empty), e => e.Contains("ít nhất một diện quyết định", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("system.roles.manage")]
    [InlineData("evaluation.self")]
    [InlineData("evaluation.read")]
    [InlineData("evaluation.khong.ton.tai")]
    [InlineData("khong.ton.tai")]
    public void Validate_InternalStepPermission_MustBeAssignableEvaluationCode(string permission)
    {
        var settings = PeriodSettings.Default();
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
    public void Json_RoundTrip_ModeAsString_EmptyMeansDefault()
    {
        var withOff = PeriodSettings.Default();
        Profile(withOff, "co-so").Step(WorkflowStep.B3C_DIRECTOR).Mode = StepMode.Off;
        var json = withOff.Normalize().ToJson();
        Assert.Contains("\"schemaVersion\":3", json);
        Assert.Contains("\"mode\":\"External\"", json);
        Assert.Contains("\"mode\":\"Off\"", json);
        Assert.Contains("\"defaultProfiles\":{\"CoSo\":\"co-so\",\"CapTren\":\"cap-tren\"}", json);
        Assert.Equal(json, PeriodSettings.Parse(json).ToJson());

        Assert.Equal(PeriodSettings.Default().ToJson(), PeriodSettings.Parse(null).ToJson());
        Assert.Equal(PeriodSettings.Default().ToJson(), PeriodSettings.Parse("  ").ToJson());

        // Bước thiếu được bổ sung (nội bộ, quyền mặc định); khóa bước, khóa cấp quyết định không phân biệt hoa thường.
        var partial = PeriodSettings.Parse(
            "{\"schemaVersion\":3,\"profiles\":[{\"code\":\"A\",\"name\":\"Nhóm A\",\"steps\":{\"b1_register\":{\"deadline\":\"2026-10-05\"},"
            + "\"b1_approve\":{\"mode\":\"Off\"}}}],\"defaultProfiles\":{\"coso\":\"a\",\"CAPTREN\":\"A\"}}");
        var a = partial.FindProfile("a")!;
        Assert.Equal(9, a.Steps.Count);
        Assert.True(a.IsActive(WorkflowStep.B1_REGISTER));
        Assert.False(a.IsActive(WorkflowStep.B1_APPROVE));
        Assert.Equal(new DateOnly(2026, 10, 5), a.Deadline(WorkflowStep.B1_REGISTER));
        Assert.Empty(Validate(partial));
        Assert.Equal("a", partial.DefaultProfileCode(ApprovalAuthority.CapTren));

        Assert.Throws<FormatException>(() => PeriodSettings.Parse("{không phải json"));
        Assert.Throws<FormatException>(() => PeriodSettings.Parse("{\"profiles\":[{\"steps\":{\"B1_REGISTER\":{\"mode\":\"Khac\"}}}]}"));
    }

    [Fact]
    public void DiffersOnlyInDeadlines_AcrossProfiles()
    {
        var current = PeriodSettings.Default();
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
        var settings = PeriodSettings.Default();
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
        var settings = PeriodSettings.Default();
        var baseProfile = Profile(settings, "co-so");
        Assert.Equal(new[] { WorkflowStep.B1_APPROVE, WorkflowStep.B3B_APPRAISAL, WorkflowStep.B3C_DIRECTOR, WorkflowStep.B4_DECISION },
            baseProfile.DifferingSteps(Profile(settings, "cap-tren")));
        Assert.Equal(new[] { WorkflowStep.B3A_COLLECTIVE, WorkflowStep.B3C_DIRECTOR }, baseProfile.DifferingSteps(Profile(settings, "bi-thu-nhan-vien")));

        var withDeadline = PeriodSettings.Default();
        Profile(withDeadline, "co-so").Step(WorkflowStep.B2_SELF_SCORE).Deadline = new DateOnly(2026, 12, 11);
        Assert.Empty(baseProfile.DifferingSteps(Profile(withDeadline, "co-so")));
    }

    #endregion

    #region Máy trạng thái theo diện quyết định

    [Fact]
    public void StateMachine_ExternalStepsAreActive_OffStepsAreSkipped()
    {
        var upper = Profile(PeriodSettings.Default(), "cap-tren");
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
        var baseProfile = Profile(PeriodSettings.Default(), "co-so");
        var noApproval = WorkflowProfile.Create("y", "Y", null, new Dictionary<WorkflowStep, StepMode> { [WorkflowStep.B1_APPROVE] = StepMode.Off });
        var noDirector = WorkflowProfile.Create("x", "X", null, new Dictionary<WorkflowStep, StepMode> { [WorkflowStep.B3C_DIRECTOR] = StepMode.Off });

        Assert.Equal(RecordStatus.AwaitingSelfScore, RecordStateMachine.Realign(RecordStatus.AwaitingTaskApproval, noApproval.ActiveSteps()));
        Assert.Equal(RecordStatus.AwaitingRegistration, RecordStateMachine.Realign(RecordStatus.AwaitingRegistration, noApproval.ActiveSteps()));
        Assert.Equal(RecordStatus.AwaitingDecision, RecordStateMachine.Realign(RecordStatus.AwaitingDirectorReview, noDirector.ActiveSteps()));
        Assert.Equal(RecordStatus.AwaitingAppraisal, RecordStateMachine.Realign(RecordStatus.AwaitingAppraisal, noDirector.ActiveSteps()));
        Assert.Equal(RecordStatus.Published, RecordStateMachine.Realign(RecordStatus.Published, baseProfile.ActiveSteps()));
    }

    #endregion

    #region Hành động theo chế độ bước

    [Fact]
    public void PermissionFor_FollowsProfileMode()
    {
        var settings = PeriodSettings.Default();
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
        var noApproval = WorkflowProfile.Create("y", "Y", null, new Dictionary<WorkflowStep, StepMode> { [WorkflowStep.B1_APPROVE] = StepMode.Off });
        Assert.Null(WorkflowActions.AdvanceOf(WorkflowStep.B1_APPROVE, noApproval));
    }

    [Fact]
    public void ModeBlockReason_RejectsActionNotMatchingStepMode()
    {
        var settings = PeriodSettings.Default();
        var baseProfile = Profile(settings, "co-so");
        var upper = Profile(settings, "cap-tren");
        baseProfile.Step(WorkflowStep.B1_APPROVE).Mode = StepMode.Off;

        Assert.Null(WorkflowActions.ModeBlockReason(WorkflowActions.CompleteOf(WorkflowStep.B4_DECISION), baseProfile));
        Assert.Contains("do cấp trên thực hiện",
            WorkflowActions.ModeBlockReason(WorkflowActions.CompleteOf(WorkflowStep.B4_DECISION), upper));
        Assert.Contains("không ghi nhận kết quả của cấp trên",
            WorkflowActions.ModeBlockReason(WorkflowActions.ExternalOf(WorkflowStep.B4_DECISION), baseProfile));
        Assert.Contains("không áp dụng",
            WorkflowActions.ModeBlockReason(WorkflowActions.CompleteOf(WorkflowStep.B1_APPROVE), baseProfile));
        Assert.Null(WorkflowActions.ModeBlockReason(WorkflowActions.Get(WorkflowActions.Reopen), upper));
    }

    #endregion
}
