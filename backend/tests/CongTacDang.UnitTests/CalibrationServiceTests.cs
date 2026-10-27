using CongTacDang.Application.Common.Exceptions;
using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.DTOs;
using CongTacDang.Application.Organization;
using CongTacDang.Application.Services;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Biên bản hiệu chuẩn (Mẫu 20) ở tầng service với kho dữ liệu giả.</summary>
public sealed class CalibrationServiceTests
{
    [Fact]
    public async Task Create_SnapshotsBeforeValues_AndRecordsHistory()
    {
        var h = new Harness();

        var dto = await h.Service.CreateAsync(h.CreateRequest());

        Assert.Equal("Draft", dto.Status);
        Assert.Equal(2, dto.Records.Count);
        var good = dto.Records.Single(r => r.RecordId == h.Records[0].Id);
        Assert.Equal("HoanThanhTot", good.GradeBefore);
        Assert.Equal(85, good.ScoreBefore);
        Assert.Contains(h.Calibrations.History, x => x.Action == EvaluationFormAction.Create && x.FormType == EvaluationFormType.M20);
        // Tổng hợp theo mức tính từ hồ sơ: 1 Tốt, 1 Hoàn thành.
        Assert.Equal(1, dto.Summary.Single(s => s.GradeCode == "HoanThanhTot").CountBefore);
        Assert.Equal(50, dto.Summary.Single(s => s.GradeCode == "HoanThanhTot").PercentBefore);
        Assert.Equal(2, dto.Summary.Single(s => s.GradeCode == "TOTAL").CountBefore);
    }

    [Fact]
    public async Task Create_RecordOfCurrentUser_IsForbidden()
    {
        var h = new Harness();
        h.User.UserId = h.Records[0].MemberId;

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.CreateAsync(h.CreateRequest()));
        Assert.Empty(h.Calibrations.Sessions);
    }

    [Fact]
    public async Task Create_RecordNotYetAppraised_IsRejected()
    {
        var h = new Harness();
        h.Records[1].Status = RecordStatus.AwaitingAppraisal;

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Service.CreateAsync(h.CreateRequest()));

        Assert.Contains("không thành công", ex.Message);
    }

    [Fact]
    public async Task Create_RecordAlreadyInAnotherSession_IsRejected()
    {
        var h = new Harness();
        await h.Service.CreateAsync(h.CreateRequest());

        var ex = await Assert.ThrowsAsync<ConflictException>(() => h.Service.CreateAsync(h.CreateRequest()));

        Assert.Contains("biên bản họp thống nhất khác", ex.Message);
    }

    [Fact]
    public async Task Create_WithoutManagePermission_IsForbidden()
    {
        var h = new Harness();
        h.Guard.Granted.Remove(PermissionCodes.EvaluationCalibrationManage);

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.CreateAsync(h.CreateRequest()));
    }

    [Fact]
    public async Task Update_SummaryCountsComeFromRecords_NotFromInput()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.CreateRequest());
        var request = h.FullContent(created.Version);
        request.Cases[0].GradeAfter = "HoanThanh";
        request.Summary.Add(new CalibrationSummaryRowInputDto { GradeCode = "HoanThanhTot", NotableDifference = "Chênh lệch giữa hai người", PreliminaryConclusion = "Giữ" });

        var dto = await h.Service.UpdateAsync(created.Id, request);

        var tot = dto.Summary.Single(s => s.GradeCode == "HoanThanhTot");
        Assert.Equal(1, tot.CountBefore);
        Assert.Equal(0, tot.CountAfter);
        Assert.Equal("Chênh lệch giữa hai người", tot.NotableDifference);
        Assert.Equal(2, dto.Summary.Single(s => s.GradeCode == "HoanThanh").CountAfter);
        Assert.True(dto.Cases[0].Changed);
        Assert.False(dto.Cases[1].Changed);
    }

    [Fact]
    public async Task Update_CaseForRecordOutsideSession_IsRejected()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.CreateRequest());
        var request = h.FullContent(created.Version);
        request.Cases[0].RecordId = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() => h.Service.UpdateAsync(created.Id, request));
    }

    [Fact]
    public async Task Update_StaleVersion_IsConflict()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.CreateRequest());

        await Assert.ThrowsAsync<ConflictException>(() => h.Service.UpdateAsync(created.Id, h.FullContent(created.Version + 1)));
        await Assert.ThrowsAsync<ValidationException>(() => h.Service.UpdateAsync(created.Id, h.FullContent(null)));
    }

    [Fact]
    public async Task Conclude_SamePersonWhoPrepared_IsForbidden()
    {
        var h = new Harness();
        var saved = await h.SavedAsync();
        h.Guard.Granted.Add(PermissionCodes.EvaluationCalibrationConclude);

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version }));
    }

    [Fact]
    public async Task Conclude_IncompleteContent_ListsWhatIsMissing()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.CreateRequest());
        h.SwitchToConcluder();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            h.Service.ConcludeAsync(created.Id, new ConcludeCalibrationDto { Version = created.Version }));

        Assert.Contains("chủ trì", ex.Message);
        Assert.Contains("thành phần tham dự", ex.Message);
        Assert.All(h.Records, r => Assert.Null(r.CalibratedGrade));
    }

    [Fact]
    public async Task Conclude_AppliesCalibratedValues_OnlyForChangedCases_AndWritesRecordHistory()
    {
        var h = new Harness();
        var saved = await h.SavedAsync(changeFirst: true);
        h.SwitchToConcluder();

        var dto = await h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version });

        Assert.Equal("Concluded", dto.Status);
        var changed = h.Records[0];
        Assert.Equal(EvaluationGrade.HoanThanh, changed.CalibratedGrade);
        Assert.Equal(78, changed.CalibratedScore);
        Assert.Equal(saved.Id, changed.CalibrationSessionId);
        // Giá trị sau hiệu chuẩn trở thành mặc định của B3c, B4.
        Assert.Equal(78, changed.ReviewedScore());
        Assert.Equal(EvaluationGrade.HoanThanh, changed.ReviewedGrade());
        Assert.Equal(78, changed.EffectiveScore());
        Assert.Equal(EvaluationGrade.HoanThanh, changed.EffectiveGrade());
        // Hồ sơ không đổi thì không bị ghi.
        Assert.Null(h.Records[1].CalibratedGrade);
        var history = Assert.Single(h.Histories);
        Assert.Equal(WorkflowAction.Calibrate, history.Action);
        Assert.Equal(changed.Id, history.RecordId);
        Assert.Equal(85, history.ScoreBefore);
        Assert.Equal(78, history.ScoreAfter);
        Assert.Equal("Theo biên bản kiểm tra thực tế", history.Reason);
        Assert.Contains(h.Calibrations.History, x => x.Action == EvaluationFormAction.Conclude);
        Assert.Contains(h.Calibrations.History, x => x.Action == EvaluationFormAction.Apply);
    }

    [Fact]
    public async Task Conclude_RecordMovedOnToDecisionRecorded_IsRejected()
    {
        var h = new Harness();
        var saved = await h.SavedAsync(changeFirst: true);
        h.SwitchToConcluder();
        h.Records[0].Status = RecordStatus.AwaitingPublish;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version }));

        Assert.Contains("không thành công", ex.Message);
        Assert.Null(h.Records[0].CalibratedGrade);
    }

    [Fact]
    public async Task Conclude_AppraisalChangedAfterBeingAdded_AsksToRefreshSnapshot()
    {
        var h = new Harness();
        var saved = await h.SavedAsync();
        h.SwitchToConcluder();
        h.Records[1].AppraisalScore = 60;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version }));

        Assert.Contains("mốc trước họp thống nhất", ex.Message);
    }

    [Fact]
    public async Task Reopen_RequiresReason_ClearsCalibratedValues_AndBumpsRevision()
    {
        var h = new Harness();
        var saved = await h.SavedAsync(changeFirst: true);
        h.SwitchToConcluder();
        var concluded = await h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version });

        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Service.ReopenAsync(saved.Id, new CalibrationReasonDto { Version = concluded.Version, Reason = " " }));
        var reopened = await h.Service.ReopenAsync(saved.Id, new CalibrationReasonDto { Version = concluded.Version, Reason = "Bổ sung minh chứng" });

        Assert.Equal("Draft", reopened.Status);
        Assert.Equal(2, reopened.Revision);
        Assert.Null(h.Records[0].CalibratedGrade);
        Assert.Null(h.Records[0].CalibrationSessionId);
        Assert.Equal(85, h.Records[0].EffectiveScore());
        Assert.Equal(2, h.Histories.Count);
        Assert.Contains(h.Calibrations.History, x => x.Action == EvaluationFormAction.Reopen && x.Reason == "Bổ sung minh chứng" && x.Snapshot != null);
    }

    [Fact]
    public async Task Void_ReleasesRecordsForAnotherSession_AndNeedsReason()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.CreateRequest());

        await Assert.ThrowsAsync<ValidationException>(() =>
            h.Service.VoidAsync(created.Id, new CalibrationReasonDto { Version = created.Version, Reason = null }));
        var voided = await h.Service.VoidAsync(created.Id, new CalibrationReasonDto { Version = created.Version, Reason = "Lập nhầm nhóm" });

        Assert.Equal("Void", voided.Status);
        var again = await h.Service.CreateAsync(h.CreateRequest());
        Assert.Equal("Draft", again.Status);
    }

    [Fact]
    public async Task Get_OwnerOfARecordInSession_IsForbidden()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.CreateRequest());
        h.User.UserId = h.Records[1].MemberId;
        h.Guard.Granted.Add(PermissionCodes.EvaluationRead);

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.GetAsync(created.Id));
        Assert.Empty(await h.Service.ListAsync(null));
    }

    [Fact]
    public async Task Get_UserWithoutAnyRelevantPermission_IsForbidden()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(h.CreateRequest());
        h.Guard.Granted.Clear();

        await Assert.ThrowsAsync<ForbiddenException>(() => h.Service.GetAsync(created.Id));
    }

    [Fact]
    public async Task Candidates_ExcludeOwnRecordAndRecordsAlreadyInSession()
    {
        var h = new Harness();
        Assert.Equal(2, (await h.Service.ListCandidatesAsync(h.Period.Id, null, null)).Count);

        await h.Service.CreateAsync(h.CreateRequest(h.Records[0]));
        var left = await h.Service.ListCandidatesAsync(h.Period.Id, null, null);
        Assert.Single(left);
        Assert.Equal(h.Records[1].Id, left[0].RecordId);

        h.User.UserId = h.Records[1].MemberId;
        Assert.Empty(await h.Service.ListCandidatesAsync(h.Period.Id, null, null));
    }

    #region Cổng hiệu chuẩn

    [Fact]
    public async Task Gate_NoSession_Draft_Concluded_FollowSessionState()
    {
        var h = new Harness();
        var record = h.Records[0];
        var gate = (ICalibrationGate)h.Service;

        Assert.Equal(CalibrationGateState.NoSession, (await gate.EvaluateAsync(record.Id)).State);

        var saved = await h.SavedAsync(changeFirst: true);
        Assert.Equal(CalibrationGateState.Pending, (await gate.EvaluateAsync(record.Id)).State);

        h.SwitchToConcluder();
        await h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version });
        var result = await gate.EvaluateAsync(record.Id);
        Assert.True(result.IsSatisfied);
        Assert.Null(result.Reason);
        Assert.Equal(saved.Id, result.SessionId);
        // Việc áp dụng của chính biên bản không làm biên bản hết hiệu lực.
        Assert.True((await gate.EvaluateAsync(h.Records[1].Id)).IsSatisfied);
    }

    [Theory]
    [InlineData(WorkflowAction.Return)]
    [InlineData(WorkflowAction.Reopen)]
    [InlineData(WorkflowAction.Complete)]
    [InlineData(WorkflowAction.RecordExternal)]
    [InlineData(WorkflowAction.Calibrate)]
    public async Task Gate_RecordChangedAfterConclude_IsStale(WorkflowAction action)
    {
        var h = new Harness();
        var saved = await h.SavedAsync();
        h.SwitchToConcluder();
        var concluded = await h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version });
        h.Histories.Add(new EvaluationRecordHistory
        {
            RecordId = h.Records[1].Id, Action = action, CreatedAt = concluded.ConcludedAt!.Value.AddSeconds(1)
        });

        var gate = (ICalibrationGate)h.Service;

        // Chỉ hồ sơ đã đổi bị khóa; hồ sơ còn lại trong biên bản vẫn đạt cổng.
        Assert.Equal(CalibrationGateState.Satisfied, (await gate.EvaluateAsync(h.Records[0].Id)).State);
        Assert.Equal(CalibrationGateState.Stale, (await gate.EvaluateAsync(h.Records[1].Id)).State);
    }

    [Fact]
    public async Task Gate_DecisionOfOneRecord_DoesNotStaleOthers()
    {
        var h = new Harness();
        var saved = await h.SavedAsync();
        h.SwitchToConcluder();
        var concluded = await h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version });
        h.Histories.Add(new EvaluationRecordHistory
        {
            RecordId = h.Records[1].Id, Action = WorkflowAction.Complete, FromStatus = RecordStatus.AwaitingDecision,
            CreatedAt = concluded.ConcludedAt!.Value.AddSeconds(1)
        });

        var gate = (ICalibrationGate)h.Service;
        Assert.Equal(CalibrationGateState.Satisfied, (await gate.EvaluateAsync(h.Records[0].Id)).State);
        Assert.Equal(CalibrationGateState.Satisfied, (await gate.EvaluateAsync(h.Records[1].Id)).State);
    }

    [Fact]
    public async Task Gate_ReopenedOrVoided_NotSatisfied()
    {
        var h = new Harness();
        var saved = await h.SavedAsync();
        h.SwitchToConcluder();
        var concluded = await h.Service.ConcludeAsync(saved.Id, new ConcludeCalibrationDto { Version = saved.Version });
        var reopened = await h.Service.ReopenAsync(saved.Id, new CalibrationReasonDto { Version = concluded.Version, Reason = "Bổ sung minh chứng" });
        var gate = (ICalibrationGate)h.Service;

        Assert.Equal(CalibrationGateState.Pending, (await gate.EvaluateAsync(h.Records[0].Id)).State);

        h.Guard.Granted.Add(PermissionCodes.EvaluationCalibrationManage);
        await h.Service.VoidAsync(saved.Id, new CalibrationReasonDto { Version = reopened.Version, Reason = "Lập nhầm nhóm" });
        Assert.Equal(CalibrationGateState.NoSession, (await gate.EvaluateAsync(h.Records[0].Id)).State);
    }

    #endregion

    #region Hỗ trợ

    private sealed class Harness
    {
        public EvaluationPeriod Period { get; }
        public List<EvaluationRecord> Records { get; } = new();
        public List<EvaluationRecordHistory> Histories { get; } = new();
        public FakeGuard Guard { get; } = new();
        public FakeUser User { get; } = new();
        public FakeUnitOfWork Uow { get; } = new();
        public FakeCalibrationRepository Calibrations { get; }
        public Guid DepartmentId { get; } = Guid.NewGuid();
        public Guid PreparerId { get; } = Guid.NewGuid();

        public Harness()
        {
            Period = new EvaluationPeriod
            {
                Id = Guid.NewGuid(), Name = "Kỳ thử", Year = 2027, Quarter = (EvaluationQuarter)1, Status = PeriodStatus.Open,
                StartDate = DateTime.UtcNow.AddDays(-5), EndDate = DateTime.UtcNow.AddDays(60), Settings = PeriodSettings.Default().ToJson()
            };
            Records.Add(NewRecord("Nguyễn Văn A", EvaluationGrade.HoanThanhTot, 85));
            Records.Add(NewRecord("Trần Thị B", EvaluationGrade.HoanThanh, 72));
            Calibrations = new FakeCalibrationRepository(this);
            Guard.Granted.UnionWith(new[] { PermissionCodes.EvaluationCalibrationManage });
            User.UserId = PreparerId;
        }

        private EvaluationRecord NewRecord(string name, EvaluationGrade grade, double score)
        {
            var memberId = Guid.NewGuid();
            return new EvaluationRecord
            {
                Id = Guid.NewGuid(), PeriodId = Period.Id, Period = Period, MemberId = memberId,
                Member = new PartyMemberProfile { Id = memberId, FullName = name },
                DepartmentId = DepartmentId, Status = RecordStatus.AwaitingDirectorReview, ApprovalAuthority = ApprovalAuthority.CoSo,
                AppraisalProposedGrade = grade, AppraisalScore = score, AppraisedAt = DateTime.UtcNow.AddHours(-1)
            };
        }

        public CalibrationService Service =>
            new(Calibrations, new FakeWorkflowRepository(this), Uow, Guard, User);

        public CreateCalibrationSessionDto CreateRequest(params EvaluationRecord[] records) => new()
        {
            PeriodId = Period.Id,
            DepartmentId = DepartmentId,
            GroupName = "Nhóm chuyên viên kỹ thuật",
            RecordIds = (records.Length == 0 ? Records.ToArray() : records).Select(r => r.Id).ToList()
        };

        /// <summary>Nội dung đủ để chốt: hai trường hợp, hồ sơ đầu có thể đổi kết quả.</summary>
        public UpdateCalibrationSessionDto FullContent(uint? version, bool changeFirst = false) => new()
        {
            Version = version,
            GroupName = "Nhóm chuyên viên kỹ thuật",
            HeldAt = DateTime.UtcNow,
            Location = "Phòng họp",
            ChairName = "Chủ trì họp",
            SecretaryName = "Thư ký họp",
            RecordIds = Records.Select(r => r.Id).ToList(),
            Attendees = new List<CalibrationAttendeeInputDto>
            {
                new() { FullName = "Thành viên 1", Capacity = "Ủy viên" },
                new() { FullName = "Thành viên 2", Capacity = "Ủy viên", DissentNote = "Đề nghị xem lại điểm" }
            },
            Cases = Records.Select((r, i) => new CalibrationCaseInputDto
            {
                RecordId = r.Id,
                Clarification = "Đã đối chiếu minh chứng",
                GradeAfter = changeFirst && i == 0 ? "HoanThanh" : null,
                ScoreAfter = changeFirst && i == 0 ? 78 : null,
                Basis = changeFirst && i == 0 ? "Theo biên bản kiểm tra thực tế" : null,
                ResponsibleName = changeFirst && i == 0 ? "Chủ trì họp" : null
            }).ToList()
        };

        /// <summary>Biên bản đã lưu đủ nội dung (do người lập), chưa chốt.</summary>
        public async Task<CalibrationSessionDto> SavedAsync(bool changeFirst = false)
        {
            var created = await Service.CreateAsync(CreateRequest());
            return await Service.UpdateAsync(created.Id, FullContent(created.Version, changeFirst));
        }

        public void SwitchToConcluder()
        {
            User.UserId = Guid.NewGuid();
            Guard.Granted.Clear();
            Guard.Granted.Add(PermissionCodes.EvaluationCalibrationConclude);
            Guard.Granted.Add(PermissionCodes.EvaluationRead);
        }
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
        private uint _version = 1;
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default) => operation();
        public void SetOriginalVersion(object entity, uint? version)
        {
            // Giả lập xmin: mỗi lần lưu biên bản tăng phiên bản.
            if (entity is CalibrationSession session)
                session.Version = ++_version;
        }
        public uint GetVersion(object entity) => 0;
    }

    private sealed class FakeCalibrationRepository : ICalibrationRepository
    {
        private readonly Harness _h;
        public FakeCalibrationRepository(Harness h) => _h = h;

        public List<CalibrationSession> Sessions { get; } = new();
        public List<EvaluationFormHistory> History { get; } = new();

        public Task<CalibrationSession?> FindAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

        public Task<List<CalibrationSession>> ListAsync(Guid? periodId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.Where(s => periodId == null || s.PeriodId == periodId).ToList());

        public Task<Dictionary<Guid, Guid>> GetActiveSessionByRecordAsync(IReadOnlyCollection<Guid> recordIds, Guid? exceptSessionId, CancellationToken ct = default) =>
            Task.FromResult(Sessions.SelectMany(s => s.Records)
                .Where(r => r.IsActive && recordIds.Contains(r.RecordId) && r.SessionId != exceptSessionId)
                .ToDictionary(r => r.RecordId, r => r.SessionId));

        public Task<List<CalibrationRecord>> ListActiveRowsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default) =>
            Task.FromResult(Sessions.SelectMany(s => s.Records).Where(r => r.IsActive && recordIds.Contains(r.RecordId)).ToList());

        public Task<List<EvaluationRecordHistory>> ListRecordHistorySinceAsync(IReadOnlyCollection<Guid> recordIds, DateTime since, CancellationToken ct = default) =>
            Task.FromResult(_h.Histories.Where(x => recordIds.Contains(x.RecordId) && x.CreatedAt > since).ToList());

        public Task<List<EvaluationRecord>> GetRecordsAsync(IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default) =>
            Task.FromResult(_h.Records.Where(r => recordIds.Contains(r.Id)).ToList());

        public void Add(CalibrationSession session)
        {
            session.Period = _h.Period;
            session.Version = 1;
            Sessions.Add(session);
        }

        public void AddRecord(CalibrationRecord record)
        {
            var session = Sessions.Single(s => s.Id == record.SessionId);
            record.Session = session;
            record.Record = _h.Records.Single(r => r.Id == record.RecordId);
            session.Records.Add(record);
        }

        public void RemoveRecord(CalibrationRecord record) => record.Session.Records.Remove(record);

        public void RemoveChildren(IEnumerable<CalibrationAttendee> attendees, IEnumerable<CalibrationSummaryRow> rows, IEnumerable<CalibrationCase> cases)
        {
            foreach (var a in attendees)
                a.Session.Attendees.Remove(a);
            foreach (var r in rows)
                r.Session.SummaryRows.Remove(r);
            foreach (var c in cases)
                c.Session.Cases.Remove(c);
        }

        public void AddAttendee(CalibrationAttendee attendee) => Attach(attendee.SessionId).Attendees.Add(Link(attendee, attendee.SessionId));

        public void AddSummaryRow(CalibrationSummaryRow row) => Attach(row.SessionId).SummaryRows.Add(Link(row, row.SessionId));

        public void AddCase(CalibrationCase item) => Attach(item.SessionId).Cases.Add(Link(item, item.SessionId));

        private CalibrationSession Attach(Guid sessionId) => Sessions.Single(s => s.Id == sessionId);

        private T Link<T>(T child, Guid sessionId) where T : class
        {
            var session = Attach(sessionId);
            switch (child)
            {
                case CalibrationAttendee a: a.Session = session; break;
                case CalibrationSummaryRow r: r.Session = session; break;
                case CalibrationCase c: c.Session = session; break;
            }
            return child;
        }

        public void AddHistory(EvaluationFormHistory history) => History.Add(history);

        public Task<List<EvaluationFormHistory>> ListHistoryAsync(Guid sessionId, CancellationToken ct = default) =>
            Task.FromResult(History.Where(h => h.FormId == sessionId).ToList());
    }

    private sealed class FakeWorkflowRepository : IEvaluationWorkflowRepository
    {
        private readonly Harness _h;
        public FakeWorkflowRepository(Harness h) => _h = h;

        public Task<EvaluationPeriod?> FindPeriodAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<EvaluationPeriod?>(id == _h.Period.Id ? _h.Period : null);
        public Task<List<EvaluationRecord>> ListRecordsByStatusAsync(IReadOnlyCollection<Guid> periodIds, IReadOnlyCollection<RecordStatus> statuses, CancellationToken ct = default) =>
            Task.FromResult(_h.Records.Where(r => periodIds.Contains(r.PeriodId) && statuses.Contains(r.Status)).ToList());
        public void AddHistory(EvaluationRecordHistory history) => _h.Histories.Add(history);
        public Task<string?> GetMemberNameAsync(Guid memberId, CancellationToken ct = default) => Task.FromResult<string?>("Người thao tác");
        public Task<bool> DepartmentExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);
        public Task<bool> PartyCellExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(true);

        public Task<EvaluationRecord?> FindRecordAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationPeriod>> ListPeriodsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, int>> CountRecordsByPeriodAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public void AddPeriod(EvaluationPeriod period) => throw new NotSupportedException();
        public Task<EvaluationRecord?> FindRecordIncludingDeletedAsync(Guid periodId, Guid memberId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationRecord>> ListRecordsAsync(Guid periodId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationRecord>> ListRecordsForUpdateAsync(Guid periodId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<HashSet<Guid>> GetParticipantIdsAsync(Guid periodId, CancellationToken ct = default) => throw new NotSupportedException();
        public void AddRecord(EvaluationRecord record) => throw new NotSupportedException();
        public void RemoveRecord(EvaluationRecord record) => throw new NotSupportedException();
        public Task ReplaceTasksAsync(Guid recordId, IEnumerable<EvaluationTask> tasks, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationTask>> GetTasksAsync(Guid recordId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<EvaluationRecordHistory>> ListHistoryAsync(Guid recordId, CancellationToken ct = default) => throw new NotSupportedException();
        public void AddExternalResult(EvaluationExternalResult result) => throw new NotSupportedException();
        public Task<List<Guid>> ListActiveUserIdsWithAnyPermissionAsync(IReadOnlyCollection<string> permissionCodes, DateTime now, CancellationToken ct = default) => throw new NotSupportedException();
        public void AddVoteSummary(EvaluationMeetingVoteSummary summary) => throw new NotSupportedException();
        public Task<EvaluationMeeting?> FindMeetingAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MemberSnapshotSource>> GetMembersAsync(IReadOnlyCollection<Guid> memberIds, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<List<MemberSnapshotSource>> SearchMembersAsync(Guid? departmentId, Guid? partyCellId, string? query, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, List<HeldPosition>>> GetHeldPositionsAsync(IReadOnlyCollection<Guid> userIds, DateTime at, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<HashSet<Guid>> GetPartyCellIdsWithChildrenAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<string, List<string>>> GetRoleNamesByPermissionAsync(IReadOnlyCollection<string> permissionCodes, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<HashSet<Guid>> GetRecordIdsWithActionAsync(Guid periodId, WorkflowAction action, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Dictionary<Guid, (Guid AttachmentId, string FileName)>> GetCurrentEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<HashSet<Guid>> GetTaskIdsWithEvidenceAsync(IReadOnlyCollection<EvaluationTask> tasks, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsDepartmentWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> IsPartyCellWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    #endregion
}
