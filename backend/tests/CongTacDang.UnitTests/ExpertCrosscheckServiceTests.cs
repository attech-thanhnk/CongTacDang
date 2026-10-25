using System.Reflection;
using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Phiếu đối soát chuyên ngành (Mẫu 03): quy tắc, máy trạng thái, quyền theo đơn vị đối soát và cổng thẩm định.</summary>
public sealed class ExpertCrosscheckServiceTests
{
    #region Quy tắc

    [Fact]
    public void Rules_OpenStatuses_AreRequestedInProgressSubmitted()
    {
        Assert.True(CrosscheckRules.IsOpen(CrosscheckStatus.Requested));
        Assert.True(CrosscheckRules.IsOpen(CrosscheckStatus.InProgress));
        Assert.True(CrosscheckRules.IsOpen(CrosscheckStatus.Submitted));
        Assert.False(CrosscheckRules.IsOpen(CrosscheckStatus.Confirmed));
        Assert.False(CrosscheckRules.IsOpen(CrosscheckStatus.Cancelled));
    }

    [Fact]
    public void Rules_ValidateItem_BasisRequiredForDefectOrAdjustment_PointsRequiredForDecrease()
    {
        Assert.Contains("kết luận", CrosscheckRules.ValidateItemForSubmit(new ExpertCrosscheckItem { ItemName = "A" }));

        var pass = new ExpertCrosscheckItem { ItemName = "A", Result = CrosscheckResult.Pass, SourceData = "Khớp" };
        Assert.Null(CrosscheckRules.ValidateItemForSubmit(pass));

        var defect = new ExpertCrosscheckItem { ItemName = "A", Result = CrosscheckResult.Defect, SourceData = "Lệch" };
        Assert.Contains("căn cứ", CrosscheckRules.ValidateItemForSubmit(defect));
        defect.BasisDocNumber = "12/BC-ATCL";
        Assert.Null(CrosscheckRules.ValidateItemForSubmit(defect));

        var decrease = new ExpertCrosscheckItem
        {
            ItemName = "A", Result = CrosscheckResult.Defect, SourceData = "Lệch", AdjustmentType = CrosscheckAdjustment.Decrease, BasisText = "Báo cáo"
        };
        Assert.Contains("số điểm", CrosscheckRules.ValidateItemForSubmit(decrease));
        decrease.AdjustmentPoints = 1.5;
        Assert.Null(CrosscheckRules.ValidateItemForSubmit(decrease));

        var exceed = new ExpertCrosscheckItem
        {
            ItemName = "A", Result = CrosscheckResult.Exceed, SourceData = "Vượt", AdjustmentType = CrosscheckAdjustment.RecognizeExceed
        };
        Assert.Contains("căn cứ", CrosscheckRules.ValidateItemForSubmit(exceed));

        Assert.Null(CrosscheckRules.ValidateItemForSubmit(new ExpertCrosscheckItem { ItemName = "A", NotApplicable = true }));
    }

    [Fact]
    public void Rules_FormatAdjustment()
    {
        Assert.Equal("0", CrosscheckRules.FormatAdjustment(new ExpertCrosscheckItem()));
        Assert.Equal("−1,5đ", CrosscheckRules.FormatAdjustment(
            new ExpertCrosscheckItem { AdjustmentType = CrosscheckAdjustment.Decrease, AdjustmentPoints = 1.5 }));
        Assert.Equal("+ ghi nhận vượt chuẩn", CrosscheckRules.FormatAdjustment(
            new ExpertCrosscheckItem { AdjustmentType = CrosscheckAdjustment.RecognizeExceed }));
        Assert.Equal("K/AD", CrosscheckRules.FormatAdjustment(new ExpertCrosscheckItem { NotApplicable = true }));
    }

    [Fact]
    public void Rules_FieldsOverlap_UsesCommonCode()
    {
        var a = CrosscheckRules.FieldsToJson(new[] { CrosscheckFields.Finance, CrosscheckFields.Atm });
        Assert.True(CrosscheckRules.FieldsOverlap(a, CrosscheckRules.FieldsToJson(new[] { CrosscheckFields.Atm })));
        Assert.False(CrosscheckRules.FieldsOverlap(a, CrosscheckRules.FieldsToJson(new[] { CrosscheckFields.SafetyQuality })));
        Assert.All(CrosscheckFields.All, c => Assert.True(CrosscheckFields.IsKnown(c)));
    }

    #endregion

    #region Yêu cầu

    [Fact]
    public async Task Create_Valid_RequestedWithRowsAndHistory()
    {
        var h = new Harness();

        var dto = await h.Service.CreateAsync(h.Record.Id, h.Request());

        Assert.Equal("Requested", dto.Status);
        Assert.Equal(2, dto.Items.Count);
        Assert.Equal(h.Tasks[0].TaskName, dto.Items[0].ItemName);
        Assert.Equal(h.Record.DepartmentId, h.Repo.Items.Single().DepartmentId);
        Assert.Equal(EvaluationFormAction.Create, h.Repo.Histories.Single().Action);
        Assert.Equal(EvaluationFormType.M03, h.Repo.Histories.Single().FormType);
        Assert.False(dto.EnteredOnBehalf);
    }

    [Theory]
    [InlineData("noUnit")]
    [InlineData("bothUnits")]
    [InlineData("noFields")]
    [InlineData("badField")]
    [InlineData("noReason")]
    [InlineData("noRows")]
    [InlineData("foreignTask")]
    [InlineData("duplicateTask")]
    public async Task Create_InvalidInput_Rejected(string defect)
    {
        var h = new Harness();
        var request = h.Request();
        switch (defect)
        {
            case "noUnit": request.ExpertDepartmentId = null; break;
            case "bothUnits": request.ExpertAgencyName = "Cục Hàng không"; break;
            case "noFields": request.Fields = new List<string>(); break;
            case "badField": request.Fields = new List<string> { "XYZ" }; break;
            case "noReason": request.RequestReason = " "; break;
            case "noRows": request.Items = new List<CrosscheckRequestItemDto>(); break;
            case "foreignTask": request.Items![0].TaskId = Guid.NewGuid(); break;
            case "duplicateTask": request.Items![1].TaskId = request.Items[0].TaskId; break;
        }

        await Assert.ThrowsAsync<ValidationException>(() => h.Service.CreateAsync(h.Record.Id, request));
        Assert.Empty(h.Repo.Items);
    }

    [Fact]
    public async Task Create_ExternalAgency_NoDepartment_OnBehalf()
    {
        var h = new Harness();
        var request = h.Request();
        request.ExpertDepartmentId = null;
        request.ExpertAgencyName = "  Cục Hàng không  ";

        var dto = await h.Service.CreateAsync(h.Record.Id, request);

        Assert.True(dto.EnteredOnBehalf);
        Assert.Equal("Cục Hàng không", dto.ExpertUnitName);
    }

    [Fact]
    public async Task Create_OverlappingOpenRequest_Conflict_AfterCancelAllowed()
    {
        var h = new Harness();
        var first = await h.Service.CreateAsync(h.Record.Id, h.Request());

        await Assert.ThrowsAsync<ConflictException>(() => h.Service.CreateAsync(h.Record.Id, h.Request()));
        var other = h.Request();
        other.Fields = new List<string> { CrosscheckFields.Finance };
        await h.Service.CreateAsync(h.Record.Id, other);

        await h.Service.CancelAsync(first.Id, new CrosscheckActionDto { Version = first.Version, Reason = "Không cần nữa" });
        await h.Service.CreateAsync(h.Record.Id, h.Request());
    }

    [Theory]
    [InlineData(RecordStatus.AwaitingSelfScore)]
    [InlineData(RecordStatus.AwaitingDecision)]
    [InlineData(RecordStatus.Published)]
    public async Task Create_OutsideAppraisalStage_Conflict(RecordStatus status)
    {
        var h = new Harness();
        h.Record.Status = status;

        await Assert.ThrowsAsync<ConflictException>(() => h.Service.CreateAsync(h.Record.Id, h.Request()));
    }

    [Fact]
    public async Task Create_DirectorReviewStageAllowed_ClosedPeriodAndGateOffRejected()
    {
        var h = new Harness();
        h.Record.Status = RecordStatus.AwaitingDirectorReview;
        await h.Service.CreateAsync(h.Record.Id, h.Request());

        h.Period.Status = PeriodStatus.Locked;
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.CreateAsync(h.Record.Id, h.Request()));

        h.Period.Status = PeriodStatus.Open;
        var settings = h.Period.GetSettings();
        foreach (var profile in settings.Profiles)
            profile.Gates[WorkflowGates.Crosscheck] = new GateSetting { Mode = GateMode.Off };
        h.Period.Settings = settings.ToJson();
        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Service.CreateAsync(h.Record.Id, h.Request()));
        Assert.Contains("không dùng đối soát", ex.Message);
    }

    [Fact]
    public async Task UpdateRequest_OnlyWhileRequested_ReplacesRows()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());
        var edit = h.Request();
        edit.Version = created.Version;
        edit.Items = new List<CrosscheckRequestItemDto> { new() { ItemName = "Chỉ tiêu mới", SelfData = "100%" } };

        var updated = await h.Service.UpdateRequestAsync(created.Id, edit);

        Assert.Single(updated.Items);
        Assert.Equal("Chỉ tiêu mới", updated.Items[0].ItemName);

        h.Act(Role.Responder);
        await h.Service.SaveResponseAsync(created.Id, new SaveCrosscheckResponseDto { Version = updated.Version });
        h.Act(Role.Requester);
        edit.Version = updated.Version;
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.UpdateRequestAsync(created.Id, edit));
    }

    #endregion

    #region Lập, xác nhận, hủy

    [Fact]
    public async Task Flow_Respond_Submit_Confirm_LocksAfterConfirm()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());
        h.Act(Role.Responder);

        // Gửi khi chưa nhập kết quả → 400.
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.SubmitAsync(created.Id, new CrosscheckActionDto { Version = created.Version }));

        var saved = await h.Service.SaveResponseAsync(created.Id, h.Response(created));
        Assert.Equal("InProgress", saved.Status);
        Assert.True(saved.CanSubmit);
        Assert.False(saved.CanConfirm);

        var submitted = await h.Service.SubmitAsync(created.Id, new CrosscheckActionDto { Version = saved.Version });
        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal(h.User.UserId, h.Repo.Items.Single().SubmittedById);
        Assert.False(submitted.CanRespond);

        // Người lập (chỉ có quyền lập) không xác nhận; người vừa lập cũng có quyền xác nhận thì vẫn bị chặn.
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.ConfirmAsync(created.Id, new CrosscheckActionDto { Version = submitted.Version }));
        h.Guard.Granted.Add(PermissionCodes.EvaluationCrosscheckConfirm);
        var selfConfirm = await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.ConfirmAsync(created.Id, new CrosscheckActionDto { Version = submitted.Version }));
        Assert.Contains("Người lập phiếu", selfConfirm.Message);

        h.Act(Role.Confirmer);
        var confirmed = await h.Service.ConfirmAsync(created.Id, new CrosscheckActionDto { Version = submitted.Version });
        Assert.Equal("Confirmed", confirmed.Status);
        Assert.Equal(new[] { EvaluationFormAction.Create, EvaluationFormAction.Submit, EvaluationFormAction.Confirm },
            h.Repo.Histories.Select(x => x.Action).ToArray());

        // Đã xác nhận: khóa sửa, không hủy, không trả lại.
        h.Guard.Granted.Add(PermissionCodes.EvaluationCrosscheckRespond);
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.SaveResponseAsync(created.Id, h.Response(created)));
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.ReturnAsync(created.Id, new CrosscheckActionDto { Version = confirmed.Version, Reason = "x" }));
        h.Act(Role.Requester);
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.CancelAsync(created.Id, new CrosscheckActionDto { Version = confirmed.Version, Reason = "x" }));
    }

    [Fact]
    public async Task Return_NeedsReason_BackToInProgress_KeepsSnapshot()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());
        h.Act(Role.Responder);
        var saved = await h.Service.SaveResponseAsync(created.Id, h.Response(created));
        var submitted = await h.Service.SubmitAsync(created.Id, new CrosscheckActionDto { Version = saved.Version });

        h.Act(Role.Confirmer);
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.ReturnAsync(created.Id, new CrosscheckActionDto { Version = submitted.Version, Reason = " " }));
        var returned = await h.Service.ReturnAsync(created.Id, new CrosscheckActionDto { Version = submitted.Version, Reason = "Thiếu số liệu quý" });

        Assert.Equal("InProgress", returned.Status);
        Assert.Equal("Thiếu số liệu quý", returned.ReturnReason);
        var history = h.Repo.Histories.Last();
        Assert.Equal(EvaluationFormAction.Return, history.Action);
        Assert.Equal("Thiếu số liệu quý", history.Reason);
        Assert.NotNull(history.Snapshot);
    }

    [Fact]
    public async Task Cancel_NeedsReason_ClosesOpenRequest()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());

        await Assert.ThrowsAsync<ValidationException>(() => h.Service.CancelAsync(created.Id, new CrosscheckActionDto { Version = created.Version }));
        var cancelled = await h.Service.CancelAsync(created.Id, new CrosscheckActionDto { Version = created.Version, Reason = "Đã có văn bản giấy" });

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.Equal("Đã có văn bản giấy", cancelled.CancelReason);
        Assert.Equal(EvaluationFormAction.Cancel, h.Repo.Histories.Last().Action);
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.CancelAsync(created.Id, new CrosscheckActionDto { Version = cancelled.Version, Reason = "lần nữa" }));
    }

    [Fact]
    public async Task Respond_DifferentExpertUnit_Forbidden_ByTargetNotRecord()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());
        h.Act(Role.Responder);
        h.Guard.Rule = (permission, target) => permission == PermissionCodes.EvaluationCrosscheckRespond && target.DepartmentId == Guid.NewGuid();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.SaveResponseAsync(created.Id, h.Response(created)));

        // Phạm vi đúng đơn vị đối soát: lập được dù không có quyền xem hồ sơ.
        h.Guard.Rule = (permission, target) => permission == PermissionCodes.EvaluationCrosscheckRespond && target.DepartmentId == h.ExpertDepartment;
        var saved = await h.Service.SaveResponseAsync(created.Id, h.Response(created));
        Assert.Equal("InProgress", saved.Status);
        var read = await h.Service.GetAsync(created.Id);
        Assert.Equal(created.Id, read.Id);
    }

    [Fact]
    public async Task OwnerOfRecord_CannotViewRespondOrConfirm()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());
        h.Guard.Rule = (_, _) => true;
        h.User.UserId = h.Record.MemberId;

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.GetAsync(created.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.GetByRecordAsync(h.Record.Id));
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.SaveResponseAsync(created.Id, h.Response(created)));
        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.ConfirmAsync(created.Id, new CrosscheckActionDto { Version = created.Version }));
        Assert.Empty(await h.Service.ListAsync(null, null));
    }

    [Fact]
    public async Task ExternalAgency_RequestHolderEntersOnBehalf_ConfirmByScopeOfRecordUnit()
    {
        var h = new Harness();
        var request = h.Request();
        request.ExpertDepartmentId = null;
        request.ExpertAgencyName = "Cục Hàng không";
        var created = await h.Service.CreateAsync(h.Record.Id, request);

        var saved = await h.Service.SaveResponseAsync(created.Id, h.Response(created));
        var submitted = await h.Service.SubmitAsync(created.Id, new CrosscheckActionDto { Version = saved.Version });
        Assert.Equal("Submitted", submitted.Status);

        h.Act(Role.Confirmer);
        var confirmed = await h.Service.ConfirmAsync(created.Id, new CrosscheckActionDto { Version = submitted.Version });
        Assert.Equal("Confirmed", confirmed.Status);
    }

    [Fact]
    public async Task StaleVersion_Conflict_MissingVersion_Validation()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());
        h.Act(Role.Responder);
        var request = h.Response(created);

        request.Version = created.Version + 1;
        await Assert.ThrowsAsync<ConflictException>(() => h.Service.SaveResponseAsync(created.Id, request));
        request.Version = null;
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.SaveResponseAsync(created.Id, request));
    }

    #endregion

    #region Cổng thẩm định và hàng đợi

    [Fact]
    public async Task Gate_OpenRequestBlocks_ConfirmedOrCancelledDoNot_OffSkips()
    {
        var h = new Harness();
        var profile = EvaluationMapping.ProfileOf(h.Period.GetSettings(), h.Record);
        await h.Service.EnsureNoOpenAsync(h.Record, profile);

        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());
        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Service.EnsureNoOpenAsync(h.Record, profile));
        Assert.Contains("1 phiếu đối soát", ex.Message);

        var settings = h.Period.GetSettings();
        foreach (var p in settings.Profiles)
            p.Gates[WorkflowGates.Crosscheck] = new GateSetting { Mode = GateMode.Off };
        h.Period.Settings = settings.ToJson();
        await h.Service.EnsureNoOpenAsync(h.Record, EvaluationMapping.ProfileOf(h.Period.GetSettings(), h.Record));

        await h.Service.CancelAsync(created.Id, new CrosscheckActionDto { Version = created.Version, Reason = "Hủy" });
        await h.Service.EnsureNoOpenAsync(h.Record, profile);
    }

    [Fact]
    public async Task WorkQueue_ResponderSeesWaitingToFill_ConfirmerSeesSubmitted()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.Record.Id, h.Request());

        h.Act(Role.Responder);
        var groups = await h.Service.GetGroupsAsync(null);
        var item = Assert.Single(Assert.Single(groups).Items);
        Assert.Equal(ExpertCrosscheckService.WorkQueueGroup, groups[0].Step);
        Assert.Equal(created.Id, item.FormId);
        Assert.Equal("Chờ lập phiếu", item.StatusDisplayName);

        var saved = await h.Service.SaveResponseAsync(created.Id, h.Response(created));
        await h.Service.SubmitAsync(created.Id, new CrosscheckActionDto { Version = saved.Version });
        Assert.Empty(await h.Service.GetGroupsAsync(null));

        h.Act(Role.Confirmer);
        item = Assert.Single(Assert.Single(await h.Service.GetGroupsAsync(null)).Items);
        Assert.Equal("Chờ xác nhận", item.StatusDisplayName);

        h.Act(Role.None);
        Assert.Empty(await h.Service.GetGroupsAsync(null));
    }

    #endregion

    #region Kho dữ liệu giả

    private enum Role { None, Requester, Responder, Confirmer }

    private sealed class Harness
    {
        public EvaluationPeriod Period { get; }
        public EvaluationRecord Record { get; }
        public List<EvaluationTask> Tasks { get; } = new();
        public Guid ExpertDepartment { get; } = Guid.NewGuid();
        public FakeGuard Guard { get; } = new();
        public FakeUser User { get; } = new();
        public FakeCrosscheckRepository Repo { get; }
        public ExpertCrosscheckService Service { get; }

        public Harness()
        {
            var settings = PeriodSettings.Default();
            Period = new EvaluationPeriod
            {
                Id = Guid.NewGuid(), Name = "Kỳ thử", Year = 2027, Quarter = (EvaluationQuarter)1, Status = PeriodStatus.Open,
                StartDate = DateTime.UtcNow.AddDays(-5), EndDate = DateTime.UtcNow.AddDays(60), Settings = settings.ToJson()
            };
            var ownerId = Guid.NewGuid();
            Record = new EvaluationRecord
            {
                Id = Guid.NewGuid(), PeriodId = Period.Id, Period = Period, MemberId = ownerId,
                Member = new PartyMemberProfile { Id = ownerId, FullName = "Chủ hồ sơ" },
                DepartmentId = Guid.NewGuid(), PartyCellId = Guid.NewGuid(),
                ApprovalAuthority = ApprovalAuthority.CoSo, WeightFrameCode = "K2", Status = RecordStatus.AwaitingAppraisal, Version = 3
            };
            for (var i = 1; i <= 2; i++)
                Tasks.Add(new EvaluationTask { Id = Guid.NewGuid(), RecordId = Record.Id, TaskOrder = i, TaskName = $"Việc {i}", TargetOutput = "Chuẩn", Weight = 10 });

            Repo = new FakeCrosscheckRepository(this);
            var records = DispatchProxy.Create<IEvaluationWorkflowRepository, RecordRepositoryProxy>();
            ((RecordRepositoryProxy)(object)records).Handler = (method, args) => method?.Name switch
            {
                nameof(IEvaluationWorkflowRepository.FindRecordAsync) =>
                    Task.FromResult<EvaluationRecord?>((Guid)args![0]! == Record.Id ? Record : null),
                nameof(IEvaluationWorkflowRepository.GetTasksAsync) => Task.FromResult(Tasks.ToList()),
                nameof(IEvaluationWorkflowRepository.GetMemberNameAsync) => Task.FromResult<string?>("Người thao tác"),
                nameof(IEvaluationWorkflowRepository.ListPeriodsAsync) => Task.FromResult(new List<EvaluationPeriod> { Period }),
                _ => throw new NotSupportedException(method?.Name)
            };
            Service = new ExpertCrosscheckService(records, Repo, new FakeUnitOfWork(), Guard, User);
            Act(Role.Requester);
        }

        /// <summary>Người dùng hiện tại và các quyền của vai trò (người khác chủ hồ sơ).</summary>
        public void Act(Role role)
        {
            User.UserId = Guid.NewGuid();
            Guard.Granted.Clear();
            Guard.Rule = null;
            switch (role)
            {
                case Role.Requester:
                    Guard.Granted.Add(PermissionCodes.EvaluationCrosscheckRequest);
                    Guard.Granted.Add(PermissionCodes.EvaluationRead);
                    break;
                case Role.Responder:
                    Guard.Granted.Add(PermissionCodes.EvaluationCrosscheckRespond);
                    break;
                case Role.Confirmer:
                    Guard.Granted.Add(PermissionCodes.EvaluationCrosscheckConfirm);
                    break;
            }
        }

        public SaveCrosscheckRequestDto Request() => new()
        {
            ExpertDepartmentId = ExpertDepartment,
            Fields = new List<string> { CrosscheckFields.SafetyQuality, CrosscheckFields.Atm },
            AddresseeName = "Ban An toàn - Chất lượng",
            RequestReason = "Xác nhận số liệu sự cố trong quý",
            Items = new List<CrosscheckRequestItemDto>
            {
                new() { TaskId = Tasks[0].Id, SelfData = "0 sự cố" },
                new() { ItemName = "Tỷ lệ chuyến bay đúng giờ", SelfData = "98%" }
            }
        };

        public SaveCrosscheckResponseDto Response(ExpertCrosscheckDto dto) => new()
        {
            Version = Repo.Items.Single(c => c.Id == dto.Id).Version,
            Items = dto.Items.Select(i => new CrosscheckResponseItemDto { Id = i.Id, SourceData = "Khớp hệ thống", Result = "Pass" }).ToList()
        };
    }

    private sealed class FakeGuard : IAuthorizationGuard
    {
        public HashSet<string> Granted { get; } = new(StringComparer.Ordinal);
        public Func<string, AccessTarget, bool>? Rule { get; set; }
        public bool Can(string permission, AccessTarget target) => Rule?.Invoke(permission, target) ?? Granted.Contains(permission);
        public void Ensure(string permission, AccessTarget target)
        {
            if (!Can(permission, target))
                throw new ForbiddenException($"Không có quyền {permission}.");
        }
        public bool HasAny(string permission) => Granted.Contains(permission) || Rule != null;
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
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();
        public void SetOriginalVersion(object entity, uint? version) { }
        public uint GetVersion(object entity) => 0;
    }

    /// <summary>Chỉ cài các hàm service dùng; hàm khác báo lỗi.</summary>
    public class RecordRepositoryProxy : DispatchProxy
    {
        public Func<MethodInfo?, object?[]?, object?> Handler { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod, args);
    }

    private sealed class FakeCrosscheckRepository : IExpertCrosscheckRepository
    {
        private readonly Harness _h;
        public FakeCrosscheckRepository(Harness h) => _h = h;

        public List<ExpertCrosscheck> Items { get; } = new();
        public List<EvaluationFormHistory> Histories { get; } = new();

        public Task<ExpertCrosscheck?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(c => c.Id == id));

        public Task<List<ExpertCrosscheck>> ListByRecordAsync(Guid recordId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(c => c.RecordId == recordId).OrderByDescending(c => c.RequestedAt).ToList());

        public Task<List<ExpertCrosscheck>> ListAsync(Guid? periodId, CrosscheckStatus? status, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(c => status == null || c.Status == status).ToList());

        public Task<List<ExpertCrosscheck>> ListOpenAsync(IReadOnlyCollection<Guid> periodIds, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(c => periodIds.Contains(c.PeriodId) && CrosscheckRules.IsOpen(c.Status)).ToList());

        public Task<int> CountOpenAsync(Guid recordId, CancellationToken ct = default) =>
            Task.FromResult(Items.Count(c => c.RecordId == recordId && CrosscheckRules.IsOpen(c.Status)));

        public Task<string?> GetDepartmentNameAsync(Guid departmentId, CancellationToken ct = default) =>
            Task.FromResult<string?>(departmentId == _h.ExpertDepartment ? "Ban An toàn - Chất lượng" : null);

        public Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid crosscheckId, CancellationToken ct = default) =>
            Task.FromResult(Histories.Where(x => x.FormId == crosscheckId).ToList());

        public void Add(ExpertCrosscheck crosscheck)
        {
            crosscheck.Record = _h.Record;
            if (crosscheck.ExpertDepartmentId == _h.ExpertDepartment)
                crosscheck.ExpertDepartment = new AdministrativeDepartment { Id = _h.ExpertDepartment, Name = "Ban An toàn - Chất lượng" };
            Items.Add(crosscheck);
        }

        public void AddItem(ExpertCrosscheckItem item) => Items.Single(c => c.Id == item.CrosscheckId).Items.Add(item);

        public void RemoveItem(ExpertCrosscheckItem item) => Items.Single(c => c.Id == item.CrosscheckId).Items.Remove(item);

        public void AddHistory(EvaluationFormHistory history) => Histories.Add(history);
    }

    #endregion
}
