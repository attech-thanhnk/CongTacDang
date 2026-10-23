using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Luồng đánh giá ở tầng service với kho dữ liệu giả.</summary>
public sealed class EvaluationWorkflowServiceTests
{
    /// <summary>Quyền thực hiện bước duyệt danh mục cấu hình trong hồ sơ luồng (khác quyền mặc định evaluation.tasks.approve).</summary>
    private const string ApprovePermission = WorkflowPermissions.UnitReview;

    #region 1. Tự chấm 09A

    [Fact]
    public async Task SubmitTasks_InitializesSelfScoreZero_NotWeight()
    {
        var h = new Harness(RecordStatus.AwaitingRegistration);
        h.Tasks.Clear();
        var request = new SubmitTasksRequestDto
        {
            Version = h.Record.Version,
            Tasks = new[] { 30.0, 25, 15 }.Select((w, i) => new TaskInputDto
            {
                TaskName = $"Việc {i + 1}", TargetOutput = "Chuẩn", Weight = w, AxisCode = "T1", Kind = "TrongTam", Role = "ChuTri",
                ExceedCriteria = "Vượt", EvidenceSource = "Biên bản", Confirmer = "Trưởng phòng"
            }).ToList()
        };

        await h.Service.SubmitTasksAsync(h.Record.Id, request);

        Assert.Equal(3, h.Tasks.Count);
        Assert.All(h.Tasks, t => Assert.Equal(0, t.SelfScore));
    }

    [Fact]
    public async Task SubmitSelfScore_MissingActiveTask_Rejected_NamingTask()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);
        var request = h.SelfScoreRequest(h.Tasks[0], h.Tasks[2]);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.SubmitSelfScoreAsync(h.Record.Id, request));

        Assert.Contains("\"Việc 2\"", ex.Message);
        Assert.DoesNotContain("\"Việc 1\"", ex.Message);
        Assert.Equal(RecordStatus.AwaitingSelfScore, h.Record.Status);
        Assert.Equal(0, h.Uow.Saves);
    }

    [Fact]
    public async Task SubmitSelfScore_ExcludedTaskNeedsNoScore_AllActiveScored_Succeeds()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);
        h.Tasks[1].ChangeStatus = TaskChangeMark.Excluded;
        h.Tasks[1].Weight = 0;
        var request = h.SelfScoreRequest(h.Tasks[0], h.Tasks[2]);

        await h.Service.SubmitSelfScoreAsync(h.Record.Id, request);

        Assert.Equal(RecordStatus.AwaitingCellConfirm, h.Record.Status);
        Assert.Equal(0, h.Tasks[1].SelfScore);
        Assert.Equal(40.0, h.Record.TasksScore, 6); // 30 + 10 (tỷ lệ 100%)
    }

    [Fact]
    public async Task SaveSelfScoreDraft_MissingTask_KeepsStoredScore()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);
        var request = h.SelfScoreRequest(h.Tasks[0]);
        request.GeneralScores = new Dictionary<string, GeneralItemScore>();

        await h.Service.SaveSelfScoreDraftAsync(h.Record.Id, request);

        Assert.Equal(RecordStatus.AwaitingSelfScore, h.Record.Status);
        Assert.Equal(30.0, h.Tasks[0].SelfScore, 6);
        Assert.Equal(0, h.Tasks[1].SelfScore);
        Assert.Equal(1, h.Uow.Saves);
    }

    [Fact]
    public void PendingTask_NotEntered_DistinctFromEntered100Percent()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);

        // Việc 2, 3: vừa tạo (tỷ lệ khởi tạo, điểm 0) → chưa nhập, DTO để trống; Việc 1 đã lưu 100% → điểm = trọng số.
        Assert.False(TaskSelfScores.IsPending(h.Tasks[0]));
        Assert.True(TaskSelfScores.IsPending(h.Tasks[1]));
        var pending = EvaluationMapping.ToTaskDto(h.Tasks[1], null);
        Assert.Null(pending.CriteriaA_Ratio);
        Assert.False(pending.SelfScoreEntered);
        var entered = EvaluationMapping.ToTaskDto(h.Tasks[0], null);
        Assert.Equal(1.0, entered.CriteriaA_Ratio);
        Assert.True(entered.SelfScoreEntered);

        // Nhập 0% vẫn là "đã nhập".
        h.Tasks[2].CriteriaA_Ratio = 0;
        Assert.False(TaskSelfScores.IsPending(h.Tasks[2]));
    }

    [Fact]
    public async Task SubmitSelfScore_TaskWithEmptyRatio_Rejected_NamingTask()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);
        var request = h.SelfScoreRequest(h.Tasks[0], h.Tasks[1], h.Tasks[2]);
        request.TaskScores[1].CriteriaC_Ratio = null;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.SubmitSelfScoreAsync(h.Record.Id, request));

        Assert.Contains("\"Việc 2\"", ex.Message);
        Assert.Equal(0, h.Uow.Saves);
    }

    [Fact]
    public async Task SaveSelfScoreDraft_RoundTripsEnteredValues_SkipsIncompleteTask()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);
        var request = h.SelfScoreRequest(h.Tasks[1], h.Tasks[2]);
        request.GeneralScores = new Dictionary<string, GeneralItemScore>();
        request.TaskScores[0].CriteriaB_Ratio = 0.5;
        request.TaskScores[1].CriteriaD_Ratio = null; // Việc 3 chưa nhập đủ

        await h.Service.SaveSelfScoreDraftAsync(h.Record.Id, request);

        var saved = EvaluationMapping.ToTaskDto(h.Tasks[1], null);
        Assert.Equal(0.5, saved.CriteriaB_Ratio);
        Assert.True(saved.SelfScoreEntered);
        Assert.True(TaskSelfScores.IsPending(h.Tasks[2]));
        Assert.Null(EvaluationMapping.ToTaskDto(h.Tasks[2], null).CriteriaA_Ratio);
    }

    [Fact]
    public async Task SubmitSelfScore_ExceedWithoutEvidence_Rejected_WithEvidence_Accepted()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);
        var request = h.SelfScoreRequest(h.Tasks[0], h.Tasks[1], h.Tasks[2]);
        request.TaskScores[1].IsExceedStandard = true;

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.SubmitSelfScoreAsync(h.Record.Id, request));
        Assert.Contains("\"Việc 2\"", ex.Message);
        Assert.Contains("minh chứng", ex.Message);
        Assert.Equal(0, h.Uow.Saves);

        var ok = new Harness(RecordStatus.AwaitingSelfScore);
        ok.Tasks[1].AttachmentId = Guid.NewGuid();
        var okRequest = ok.SelfScoreRequest(ok.Tasks[0], ok.Tasks[1], ok.Tasks[2]);
        okRequest.TaskScores[1].IsExceedStandard = true;
        await ok.Service.SubmitSelfScoreAsync(ok.Record.Id, okRequest);
        Assert.Equal(RecordStatus.AwaitingCellConfirm, ok.Record.Status);
        Assert.True(ok.Tasks[1].IsExceedStandard);
    }

    [Theory]
    [InlineData(RecordStatus.AwaitingRegistration, true)]
    [InlineData(RecordStatus.AwaitingTaskApproval, true)]
    [InlineData(RecordStatus.AwaitingSelfScore, true)]
    [InlineData(RecordStatus.AwaitingCellConfirm, false)]
    [InlineData(RecordStatus.AwaitingAppraisal, false)]
    [InlineData(RecordStatus.Published, false)]
    public void WeightFrame_LockedAfterSelfScoreStep(RecordStatus status, bool canChange)
    {
        Assert.Equal(canChange, RecordWeightFrames.CanChangeFrame(new EvaluationRecord { Status = status }));
    }

    [Fact]
    public void Rescore_KeepsPendingTasks_RecomputesEntered()
    {
        var h = new Harness(RecordStatus.AwaitingSelfScore);
        h.Tasks[0].CriteriaD_Ratio = 0; // đã nhập: A,B,C 100%, D 0%
        var frame = new WeightFrame { Code = "X", Name = "X", A = 0.4, B = 0.3, C = 0.2, D = 0.1 };

        var count = TaskSelfScores.Rescore(h.Tasks, frame, h.Content.Parameters.Rounding.TaskScore);

        Assert.Equal(1, count);
        Assert.Equal(27.0, h.Tasks[0].SelfScore, 6);
        Assert.True(TaskSelfScores.IsPending(h.Tasks[1]));
        Assert.Equal(0, h.Tasks[1].SelfScore);
    }

    #endregion

    #region 2. "Chưa xếp loại" ở bước có xếp loại

    [Theory]
    [InlineData("ChuaXepLoai")]
    [InlineData("0")]
    public async Task InternalGradedSteps_RejectNotRated(string notRated)
    {
        var collective = new Harness(RecordStatus.AwaitingCollective);
        await Assert.ThrowsAsync<ValidationException>(() => collective.Service.RecordCollectiveProposalAsync(collective.Record.Id,
            new CollectiveProposalRequestDto { Version = collective.Record.Version, ProposedGrade = notRated }));

        var appraisal = new Harness(RecordStatus.AwaitingAppraisal);
        await Assert.ThrowsAsync<ValidationException>(() => appraisal.Service.AppraiseAsync(appraisal.Record.Id,
            appraisal.AppraisalRequest(notRated, 28, 66)));

        var director = new Harness(RecordStatus.AwaitingDirectorReview);
        await Assert.ThrowsAsync<ValidationException>(() => director.Service.DirectorReviewAsync(director.Record.Id,
            new DirectorReviewRequestDto { Version = director.Record.Version, ProposedGrade = notRated }));

        var decision = new Harness(RecordStatus.AwaitingDecision);
        await Assert.ThrowsAsync<ValidationException>(() => decision.Service.RecordDecisionAsync(decision.Record.Id,
            new DecisionRequestDto { Version = decision.Record.Version, FinalGrade = notRated }));

        foreach (var h in new[] { collective, appraisal, director, decision })
            Assert.Equal(0, h.Uow.Saves);
    }

    [Fact]
    public async Task RecordDecision_ValidGrade_Accepted()
    {
        var h = new Harness(RecordStatus.AwaitingDecision);
        await h.Service.RecordDecisionAsync(h.Record.Id, new DecisionRequestDto { Version = h.Record.Version, FinalGrade = "HoanThanhTot", Explanation = "Căn cứ kiểm thử" });
        Assert.Equal(EvaluationGrade.HoanThanhTot, h.Record.FinalGrade);
        Assert.Equal(RecordStatus.AwaitingPublish, h.Record.Status);
    }

    [Fact]
    public async Task Publish_DecisionWithoutGrade_Refused()
    {
        var h = new Harness(RecordStatus.AwaitingPublish);
        h.Record.DecisionRecordedAt = DateTime.UtcNow;
        h.Record.FinalGrade = EvaluationGrade.ChuaXepLoai;
        h.Record.DirectorProposedGrade = EvaluationGrade.HoanThanhTot;

        await Assert.ThrowsAsync<ConflictException>(() => h.Service.PublishAsync(h.Record.Id, new WorkflowRequestDto { Version = h.Record.Version }));
        Assert.Equal(RecordStatus.AwaitingPublish, h.Record.Status);

        h.Record.FinalGrade = EvaluationGrade.HoanThanhTot;
        await h.Service.PublishAsync(h.Record.Id, new WorkflowRequestDto { Version = h.Record.Version });
        Assert.Equal(RecordStatus.Published, h.Record.Status);
    }

    #endregion

    #region 3. Điểm thẩm định bắt buộc

    [Fact]
    public async Task Appraise_WithoutAnyScore_Rejected()
    {
        var h = new Harness(RecordStatus.AwaitingAppraisal);
        h.Record.TotalSelfScore = 95;
        var request = h.AppraisalRequest("HoanThanhTot", null, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => h.Service.AppraiseAsync(h.Record.Id, request));
        Assert.Contains("điểm thẩm định", ex.Message);
        Assert.Equal(0, h.Uow.Saves);
    }

    [Fact]
    public async Task Appraise_LegacyTotal_ExplanationThresholdEnforced_RoundedAndCappedByCriteria()
    {
        var h = new Harness(RecordStatus.AwaitingAppraisal);
        h.Record.TotalSelfScore = 95;

        // Chênh lệch ≥ ngưỡng (5 điểm) mà không giải trình → 400.
        var gap = h.AppraisalRequest("HoanThanhTot", null, null);
        gap.AppraisalScore = 85;
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.AppraiseAsync(h.Record.Id, gap));

        // Vượt tổng điểm tối đa của bộ (30 + 70) → 400.
        var over = h.AppraisalRequest("HoanThanhTot", null, null);
        over.AppraisalScore = 100.5;
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.AppraiseAsync(h.Record.Id, over));

        // Hợp lệ: làm tròn theo quy tắc "tổng điểm" của bộ (1 chữ số, nửa lên).
        var ok = h.AppraisalRequest("HoanThanhTot", null, null);
        ok.AppraisalScore = 93.45;
        ok.Explanation = "Căn cứ thẩm định";
        await h.Service.AppraiseAsync(h.Record.Id, ok);
        Assert.Equal(93.5, h.Record.AppraisalScore!.Value, 6);
        Assert.Equal(RecordStatus.AwaitingDirectorReview, h.Record.Status);
    }

    #endregion

    #region 5. Thu hồi duyệt danh mục

    [Fact]
    public async Task Actions_RevokeUsesFlowProfilePermission()
    {
        var h = Harness.Approved();
        var actions = await h.Service.GetActionsAsync(h.Record.Id);
        Assert.Contains(actions.Actions, a => a.Action == WorkflowActions.RevokeTaskApproval);

        // Chỉ có quyền mặc định evaluation.tasks.approve (không phải quyền của bước theo hồ sơ luồng) → không có nút, không thu hồi được.
        h.Guard.Granted.Remove(ApprovePermission);
        h.Guard.Granted.Add(PermissionCodes.EvaluationTasksApprove);
        actions = await h.Service.GetActionsAsync(h.Record.Id);
        Assert.DoesNotContain(actions.Actions, a => a.Action == WorkflowActions.RevokeTaskApproval);
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.RevokeTaskApprovalAsync(h.Record.Id, h.Request()));
    }

    [Fact]
    public async Task Revoke_Success_ReturnsToRegistration_WritesHistory()
    {
        var h = Harness.Approved();
        h.Record.FrameA = 0.25;

        await h.Service.RevokeTaskApprovalAsync(h.Record.Id, h.Request());

        Assert.Equal(RecordStatus.AwaitingRegistration, h.Record.Status);
        Assert.Null(h.Record.TasksApprovedById);
        Assert.Null(h.Record.TasksApprovedAt);
        Assert.Null(h.Record.FrameA);
        Assert.Equal(1, h.Uow.Saves);
        var last = h.Histories[^1];
        Assert.Equal(WorkflowAction.RevokeTaskApproval, last.Action);
        Assert.Equal(WorkflowStep.B1_APPROVE, last.Step);
        Assert.Equal(h.User.UserId, last.ActorId);
    }

    [Theory]
    [InlineData(PeriodStatus.Draft)]
    [InlineData(PeriodStatus.Locked)]
    [InlineData(PeriodStatus.Closed)]
    public async Task Revoke_PeriodNotOpen_Conflict(PeriodStatus status)
    {
        var h = Harness.Approved();
        h.Period.Status = status;
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.RevokeTaskApprovalAsync(h.Record.Id, h.Request()));
        Assert.Equal(0, h.Uow.Saves);
    }

    [Fact]
    public async Task Revoke_LastActionNotApproval_Conflict()
    {
        var h = Harness.Approved();
        h.Histories.Add(new EvaluationRecordHistory
        {
            RecordId = h.Record.Id, FromStatus = RecordStatus.AwaitingSelfScore, ToStatus = RecordStatus.AwaitingSelfScore,
            Step = WorkflowStep.B2_SELF_SCORE, Action = WorkflowAction.Complete, ActorName = "x", CreatedAt = DateTime.UtcNow.AddMinutes(1)
        });
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.RevokeTaskApprovalAsync(h.Record.Id, h.Request()));

        var none = Harness.Approved();
        none.Histories.Clear();
        await Assert.ThrowsAsync<ConflictException>(() => none.Service.RevokeTaskApprovalAsync(none.Record.Id, none.Request()));
        Assert.Equal(0, h.Uow.Saves + none.Uow.Saves);
    }

    [Fact]
    public async Task Revoke_AlreadySelfScored_Conflict()
    {
        var h = Harness.Approved();
        h.Record.SelfScoredAt = DateTime.UtcNow;
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.RevokeTaskApprovalAsync(h.Record.Id, h.Request()));
    }

    [Fact]
    public async Task Revoke_OnlyApprover()
    {
        var h = Harness.Approved();
        h.Record.TasksApprovedById = Guid.NewGuid(); // người khác (cùng quyền) đã duyệt
        var actions = await h.Service.GetActionsAsync(h.Record.Id);
        Assert.DoesNotContain(actions.Actions, a => a.Action == WorkflowActions.RevokeTaskApproval);
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.RevokeTaskApprovalAsync(h.Record.Id, h.Request()));
        Assert.Equal(0, h.Uow.Saves);
    }

    [Fact]
    public async Task Revoke_OwnRecord_Forbidden()
    {
        var h = Harness.Approved();
        h.Record.MemberId = h.User.UserId!.Value;
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.RevokeTaskApprovalAsync(h.Record.Id, h.Request()));
        var actions = await h.Service.GetActionsAsync(h.Record.Id);
        Assert.DoesNotContain(actions.Actions, a => a.Action == WorkflowActions.RevokeTaskApproval);
    }

    [Fact]
    public async Task Revoke_Version_RequiredAndChecked()
    {
        var h = Harness.Approved();
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.RevokeTaskApprovalAsync(h.Record.Id, new WorkflowRequestDto()));
        await Assert.ThrowsAsync<ConflictException>(() =>
            h.Service.RevokeTaskApprovalAsync(h.Record.Id, new WorkflowRequestDto { Version = h.Record.Version + 1 }));
        Assert.Equal(RecordStatus.AwaitingSelfScore, h.Record.Status);
        Assert.Equal(0, h.Uow.Saves);
    }

    #endregion

    #region 4. Làm tròn nửa về chẵn

    [Theory]
    [InlineData(2.355, 2, 2.36)] // double 2.355 = 2,35499… — tính trên decimal mới đúng nửa về chẵn
    [InlineData(2.345, 2, 2.34)]
    [InlineData(2.3451, 2, 2.35)]
    [InlineData(9.55, 1, 9.6)]
    [InlineData(9.45, 1, 9.4)]
    [InlineData(2.5, 0, 2.0)]
    [InlineData(3.5, 0, 4.0)]
    [InlineData(-2.355, 2, -2.36)]
    public void Round_HalfEven_UsesDecimal(double value, int decimals, double expected)
    {
        Assert.Equal(expected, EvaluationScoring.Round(value, new RoundingRule(decimals, ScoreRoundingMode.HalfEven)));
    }

    [Fact]
    public void ExcellentQuota_HalfUp_MatchesDecimal()
    {
        // 25 × 0,1 = 2,5 → 3 (0,5 trở lên thành 1); 35 × 0,1 = 3,5 → 4.
        Assert.Equal(3, EvaluationScoring.ExcellentQuota(25, 0.1));
        Assert.Equal(4, EvaluationScoring.ExcellentQuota(35, 0.1));
    }

    #endregion

    #region Kho dữ liệu giả

    private sealed class Harness
    {
        public EvaluationPeriod Period { get; }
        public EvaluationRecord Record { get; }
        public List<EvaluationTask> Tasks { get; } = new();
        public List<EvaluationRecordHistory> Histories { get; } = new();
        public FakeGuard Guard { get; } = new();
        public FakeUser User { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public CriteriaSetContent Content { get; }

        public Harness(RecordStatus status)
        {
            Content = CriteriaSetDefaults.Build();
            Content.RequiredForms = new List<string>();
            var settings = PeriodSettings.Default();
            foreach (var profile in settings.Profiles)
                profile.Step(WorkflowStep.B1_APPROVE).Permission = ApprovePermission;
            var snapshot = new CriteriaSnapshot { SetId = Guid.NewGuid(), Code = "IT", Name = "Bộ thử", Content = Content };

            Period = new EvaluationPeriod
            {
                Id = Guid.NewGuid(), Name = "Kỳ thử", Year = 2027, Quarter = (EvaluationQuarter)1, Status = PeriodStatus.Open,
                StartDate = DateTime.UtcNow.AddDays(-5), EndDate = DateTime.UtcNow.AddDays(60),
                Settings = settings.ToJson(), CriteriaSnapshot = snapshot.ToJson()
            };
            var ownerId = Guid.NewGuid();
            Record = new EvaluationRecord
            {
                Id = Guid.NewGuid(), PeriodId = Period.Id, Period = Period, MemberId = ownerId,
                Member = new PartyMemberProfile { Id = ownerId, FullName = "Chủ hồ sơ" },
                ApprovalAuthority = ApprovalAuthority.CoSo, WeightFrameCode = "K2", Status = status, Version = 7
            };
            var order = 1;
            foreach (var weight in new[] { 30.0, 30, 10 })
            {
                Tasks.Add(new EvaluationTask
                {
                    Id = Guid.NewGuid(), RecordId = Record.Id, TaskOrder = order, TaskName = $"Việc {order++}", TargetOutput = "Chuẩn",
                    Weight = weight, CriteriaA_Ratio = 1, CriteriaB_Ratio = 1, CriteriaC_Ratio = 1, CriteriaD_Ratio = 1, SelfScore = 0
                });
            }
            Tasks[0].SelfScore = 30; // điểm đã lưu nháp trước đó

            Guard.Granted.UnionWith(new[]
            {
                PermissionCodes.EvaluationRead, PermissionCodes.EvaluationSelf, ApprovePermission,
                WorkflowPermissions.CollectiveRecord, WorkflowPermissions.Appraise, WorkflowPermissions.DirectorReview,
                WorkflowPermissions.Decide, WorkflowPermissions.Publish
            });
            // Bước của chủ hồ sơ do chính chủ hồ sơ làm; bước khác do người dùng hiện tại (không phải chủ hồ sơ).
            if (status is RecordStatus.AwaitingRegistration or RecordStatus.AwaitingSelfScore)
                User.UserId = ownerId;
        }

        /// <summary>Hồ sơ vừa được người dùng hiện tại duyệt danh mục (thao tác cuối là B1_APPROVE).</summary>
        public static Harness Approved()
        {
            var h = new Harness(RecordStatus.AwaitingSelfScore);
            h.User.UserId = Guid.NewGuid();
            h.Record.TasksApprovedById = h.User.UserId;
            h.Record.TasksApprovedByName = "Người duyệt";
            h.Record.TasksApprovedAt = DateTime.UtcNow;
            h.Histories.Add(new EvaluationRecordHistory
            {
                RecordId = h.Record.Id, FromStatus = RecordStatus.AwaitingTaskApproval, ToStatus = RecordStatus.AwaitingSelfScore,
                Step = WorkflowStep.B1_APPROVE, Action = WorkflowAction.Complete, ActorId = h.User.UserId, ActorName = "Người duyệt",
                CreatedAt = DateTime.UtcNow
            });
            return h;
        }

        public WorkflowRequestDto Request() => new() { Version = Record.Version };

        public EvaluationWorkflowService Service =>
            new(new FakeRepository(this), Uow, Guard, User, null!);

        public SubmitSelfScoreRequestDto SelfScoreRequest(params EvaluationTask[] scored) => new()
        {
            Version = Record.Version,
            GeneralScores = Content.AllItems.ToDictionary(x => x.Item.Code, x => new GeneralItemScore { Score = x.Item.MaxScore }),
            TaskScores = scored.Select(t => new TaskScoreInputDto
            {
                TaskId = t.Id, CriteriaA_Ratio = 1, CriteriaB_Ratio = 1, CriteriaC_Ratio = 1, CriteriaD_Ratio = 1, AttachmentId = t.AttachmentId
            }).ToList(),
            SelfProposedGrade = "HoanThanhTot",
            SelfGradeExplanation = "Căn cứ kiểm thử"
        };

        public AppraisalRequestDto AppraisalRequest(string grade, double? general, double? tasks) => new()
        {
            Version = Record.Version,
            ProposedGrade = grade,
            GeneralScore = general,
            TasksScore = tasks,
            TaskLevels = Tasks.Select(t => new AppraisalTaskLevelDto { TaskId = t.Id, Level = 1 }).ToList()
        };
    }

    private sealed class FakeGuard : IAuthorizationGuard
    {
        public HashSet<string> Granted { get; } = new(StringComparer.Ordinal);
        public bool Can(string permission, AccessTarget target) => Granted.Contains(permission);
        public void Ensure(string permission, AccessTarget target)
        {
            if (!Can(permission, target))
                throw new ForbiddenException($"Không có quyền {permission}.");
        }
        public bool HasAny(string permission) => Granted.Contains(permission);
        public ScopeFilter GetScope(string permission) => Granted.Contains(permission) ? ScopeFilter.Global : ScopeFilter.None;
    }

    private sealed class FakeUser : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string UserName => "nguoi-dung";
        public string? IpAddress => null;
        public string? UserAgent => null;
        public string? RequestPath => null;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.FromResult(1);
        }
        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();
        public void SetOriginalVersion(object entity, uint? version) { }
        public uint GetVersion(object entity) => 0;
    }

    private sealed class FakeRepository : IEvaluationWorkflowRepository
    {
        private readonly Harness _h;
        public FakeRepository(Harness h) => _h = h;

        public Task<EvaluationRecord?> FindRecordAsync(Guid id, CancellationToken ct = default)
        {
            _h.Record.Tasks = _h.Tasks;
            return Task.FromResult<EvaluationRecord?>(id == _h.Record.Id ? _h.Record : null);
        }
        public Task<List<EvaluationTask>> GetTasksAsync(Guid recordId, CancellationToken ct = default) => Task.FromResult(_h.Tasks.ToList());
        public Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks, CancellationToken ct = default)
        {
            var list = tasks.ToList();
            _h.Tasks.Clear();
            _h.Tasks.AddRange(list);
            return Task.CompletedTask;
        }
        public void AddHistory(EvaluationRecordHistory history) => _h.Histories.Add(history);
        public Task<List<EvaluationRecordHistory>> ListHistoryAsync(Guid recordId, CancellationToken ct = default) => Task.FromResult(_h.Histories.ToList());
        public Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default) => Task.FromResult<string?>("Người thao tác");
        public Task<Dictionary<Guid, (Guid AttachmentId, string FileName)>> GetCurrentEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<Guid, (Guid AttachmentId, string FileName)>());
        public Task<HashSet<Guid>> GetTaskIdsWithEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct = default) =>
            Task.FromResult(tasks.Where(t => t.AttachmentId.HasValue).Select(t => t.Id).ToHashSet());
        public Task<EvaluationMeeting?> FindMeetingAsync(Guid id, CancellationToken ct = default) => Task.FromResult<EvaluationMeeting?>(null);
        public void AddExternalResult(EvaluationExternalResult result) { }
        public void AddVoteSummary(EvaluationMeetingVoteSummary summary) { }

        public Task<EvaluationPeriod?> FindPeriodAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationPeriod>> ListPeriodsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, int>> CountRecordsByPeriodAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public void AddPeriod(EvaluationPeriod period) => throw new NotSupportedException();
        public Task<EvaluationRecord?> FindRecordIncludingDeletedAsync(Guid periodId, Guid memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationRecord>> ListRecordsAsync(Guid periodId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationRecord>> ListRecordsForUpdateAsync(Guid periodId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationRecord>> ListRecordsByStatusAsync(IReadOnlyCollection<Guid> periodIds, IReadOnlyCollection<RecordStatus> statuses, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<HashSet<Guid>> GetParticipantIdsAsync(Guid periodId, CancellationToken ct = default) => throw new NotSupportedException();
        public void AddRecord(EvaluationRecord record) => throw new NotSupportedException();
        public void RemoveRecord(EvaluationRecord record) => throw new NotSupportedException();
        public Task<List<Guid>> ListActiveUserIdsWithAnyPermissionAsync(IReadOnlyCollection<string> permissionCodes, DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MemberSnapshotSource>> GetMembersAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MemberSnapshotSource>> SearchMembersAsync(Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, List<HeldPosition>>> GetHeldPositionsAsync(IReadOnlyCollection<Guid> userIds, DateTime at, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<HashSet<Guid>> GetPartyCellIdsWithChildrenAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<string, List<string>>> GetRoleNamesByPermissionAsync(IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<HashSet<Guid>> GetRecordIdsWithActionAsync(Guid periodId, WorkflowAction action, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DepartmentExistsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> PartyCellExistsAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsDepartmentWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) => Task.FromResult(rootId == unitId);
        public Task<bool> IsPartyCellWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) => Task.FromResult(rootId == unitId);
    }

    #endregion
}
