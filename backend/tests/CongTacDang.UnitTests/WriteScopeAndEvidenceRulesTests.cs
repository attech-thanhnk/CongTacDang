using CongTacDang.Application.Common.Interfaces;
using CongTacDang.Application.Common.Security;
using CongTacDang.Application.Organization;
using CongTacDang.Domain.Enums;
using CongTacDang.Domain.Evaluation;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>
/// Phạm vi ghi (mọi đơn vị của đối tượng phải trong phạm vi), luật minh chứng theo giai đoạn hồ sơ và luật hồ sơ thuộc
/// tổ chức của biên bản theo cây con.
/// </summary>
public class WriteScopeAndEvidenceRulesTests
{
    private static readonly Guid DeptA = Guid.NewGuid();
    private static readonly Guid DeptX = Guid.NewGuid();
    private static readonly Guid CellA = Guid.NewGuid();
    private static readonly Guid CellChild = Guid.NewGuid();
    private static readonly Guid CellB = Guid.NewGuid();

    private static EffectivePermissions With(params PermissionGrant[] grants) => new(Guid.NewGuid(), grants);

    private static PermissionGrant Grant(string code, ScopeType type, Guid? scopeId, params Guid[] covered) =>
        new(code, type, scopeId, Guid.NewGuid(), "Vai trò thử", covered.Length == 0 ? null : covered);

    #region Phạm vi ghi

    [Fact]
    public void WriteMode_RequiresEveryReferencedUnit_ReadModeKeepsOrRule()
    {
        var cellManager = With(Grant(PermissionCodes.CollectiveManage, ScopeType.PartyCell, CellA, CellA, CellChild));
        var mixed = new AccessTarget(DepartmentId: DeptX, PartyCellId: CellA);

        // Đọc: một đơn vị trong phạm vi là đủ.
        Assert.True(AuthorizationGuard.Evaluate(cellManager, PermissionCodes.CollectiveManage, mixed));
        // Ghi: đơn vị chính quyền X ngoài phạm vi → không được.
        Assert.False(AuthorizationGuard.Evaluate(cellManager, PermissionCodes.CollectiveManage, mixed.ForWrite()));
        Assert.False(AuthorizationGuard.Evaluate(cellManager, PermissionCodes.CollectiveManage, AccessTarget.ForUnitsWrite(DeptX, CellA)));
        // Ghi: chỉ gắn tổ chức Đảng (kể cả tổ chức con) → được; tổ chức khác → không.
        Assert.True(AuthorizationGuard.Evaluate(cellManager, PermissionCodes.CollectiveManage, AccessTarget.ForUnitsWrite(null, CellA)));
        Assert.True(AuthorizationGuard.Evaluate(cellManager, PermissionCodes.CollectiveManage, AccessTarget.ForUnitsWrite(null, CellChild)));
        Assert.False(AuthorizationGuard.Evaluate(cellManager, PermissionCodes.CollectiveManage, AccessTarget.ForUnitsWrite(null, CellB)));
        // Không gắn đơn vị nào (cấp Công ty) → chỉ phạm vi Toàn công ty.
        Assert.False(AuthorizationGuard.Evaluate(cellManager, PermissionCodes.CollectiveManage, AccessTarget.ForUnitsWrite(null, null)));
    }

    [Fact]
    public void WriteMode_CoveredByTwoDifferentGrants_OrGlobal_IsAllowed()
    {
        var both = With(
            Grant(PermissionCodes.MeetingManage, ScopeType.Department, DeptA, DeptA),
            Grant(PermissionCodes.MeetingManage, ScopeType.PartyCell, CellA, CellA));
        Assert.True(AuthorizationGuard.Evaluate(both, PermissionCodes.MeetingManage, AccessTarget.ForUnitsWrite(DeptA, CellA)));
        Assert.False(AuthorizationGuard.Evaluate(both, PermissionCodes.MeetingManage, AccessTarget.ForUnitsWrite(DeptX, CellA)));

        var global = With(Grant(PermissionCodes.MeetingManage, ScopeType.Global, null));
        Assert.True(AuthorizationGuard.Evaluate(global, PermissionCodes.MeetingManage, AccessTarget.ForUnitsWrite(DeptX, CellB)));
        Assert.True(AuthorizationGuard.Evaluate(global, PermissionCodes.MeetingManage, AccessTarget.ForUnitsWrite(null, null)));
    }

    #endregion

    #region Minh chứng theo giai đoạn

    [Theory]
    [InlineData(RecordStatus.AwaitingRegistration, PeriodStatus.Open, EvidenceStage.Open)]
    [InlineData(RecordStatus.AwaitingTaskApproval, PeriodStatus.Open, EvidenceStage.Open)]
    [InlineData(RecordStatus.AwaitingSelfScore, PeriodStatus.Open, EvidenceStage.Open)]
    [InlineData(RecordStatus.AwaitingCellConfirm, PeriodStatus.Open, EvidenceStage.AddOnly)]
    [InlineData(RecordStatus.AwaitingAppraisal, PeriodStatus.Open, EvidenceStage.AddOnly)]
    [InlineData(RecordStatus.AwaitingPublish, PeriodStatus.Open, EvidenceStage.AddOnly)]
    [InlineData(RecordStatus.Published, PeriodStatus.Open, EvidenceStage.Locked)]
    [InlineData(RecordStatus.AwaitingSelfScore, PeriodStatus.Locked, EvidenceStage.Locked)]
    [InlineData(RecordStatus.AwaitingAppraisal, PeriodStatus.Locked, EvidenceStage.Locked)]
    [InlineData(RecordStatus.Published, PeriodStatus.Closed, EvidenceStage.Locked)]
    [InlineData(RecordStatus.AwaitingRegistration, PeriodStatus.Draft, EvidenceStage.Locked)]
    public void EvidenceStage_FollowsRecordAndPeriod(RecordStatus record, PeriodStatus period, EvidenceStage expected)
    {
        var stage = EvidenceRules.StageOf(record, period);
        Assert.Equal(expected, stage);
        Assert.Equal(expected != EvidenceStage.Locked, EvidenceRules.CanAdd(stage));
        Assert.Equal(expected == EvidenceStage.Open, EvidenceRules.CanReplace(stage));
        Assert.Equal(expected == EvidenceStage.Open, EvidenceRules.CanRemove(stage));
        Assert.Equal(expected == EvidenceStage.Open, EvidenceRules.ChangeBlockReason(stage, record, period) == null);
        Assert.Equal(expected != EvidenceStage.Locked, EvidenceRules.AddBlockReason(stage, record, period) == null);
    }

    [Fact]
    public void EvidenceReasons_PointPublishedRecordsToAppeal()
    {
        var reason = EvidenceRules.AddBlockReason(EvidenceStage.Locked, RecordStatus.Published, PeriodStatus.Closed);
        Assert.Contains("kiến nghị", reason);
        Assert.Contains("chỉ được bổ sung", EvidenceRules.ChangeBlockReason(EvidenceStage.AddOnly, RecordStatus.AwaitingCellConfirm, PeriodStatus.Open));
    }

    #endregion

    #region Biên bản theo cây con

    [Fact]
    public async Task MeetingAcceptsRecordsInSubtree_RejectsOthers()
    {
        var parentPath = OrgTree.BuildPath(null, CellA);
        var reader = new TreeReader(
            departments: new Dictionary<Guid, string>
            {
                [DeptA] = OrgTree.BuildPath(null, DeptA),
                [DeptX] = OrgTree.BuildPath(null, DeptX)
            },
            cells: new Dictionary<Guid, string>
            {
                [CellA] = parentPath,
                [CellChild] = OrgTree.BuildPath(parentPath, CellChild),
                [CellB] = OrgTree.BuildPath(null, CellB)
            });

        // Biên bản ở tổ chức cha nhận hồ sơ của tổ chức con và của chính nó.
        Assert.True(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, null, CellA, DeptX, CellChild));
        Assert.True(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, null, CellA, null, CellA));
        // Ngoài cây con, hoặc hồ sơ không gắn tổ chức Đảng → từ chối.
        Assert.False(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, null, CellA, null, CellB));
        Assert.False(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, null, CellA, DeptA, null));
        // Tổ chức con không nhận hồ sơ của tổ chức cha.
        Assert.False(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, null, CellChild, null, CellA));
        // Biên bản gắn đơn vị chính quyền: so theo cây đơn vị chính quyền.
        Assert.True(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, DeptA, null, DeptA, CellB));
        Assert.False(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, DeptA, null, DeptX, CellA));
        // Biên bản cấp Công ty (không gắn) nhận mọi hồ sơ.
        Assert.True(await OrgSubtreeRules.RecordWithinMeetingAsync(reader, null, null, DeptX, CellB));
    }

    private sealed class TreeReader(IReadOnlyDictionary<Guid, string> departments, IReadOnlyDictionary<Guid, string> cells) : IOrgSubtreeReader
    {
        public Task<bool> IsDepartmentWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) =>
            Task.FromResult(OrgTree.IsSelfOrDescendant(departments.GetValueOrDefault(unitId), rootId));

        public Task<bool> IsPartyCellWithinAsync(Guid rootId, Guid unitId, CancellationToken ct = default) =>
            Task.FromResult(OrgTree.IsSelfOrDescendant(cells.GetValueOrDefault(unitId), rootId));
    }

    #endregion
}
