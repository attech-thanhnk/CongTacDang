using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Kiểm thử cơ chế kiểm tra quyền theo đối tượng (T-32, T-42).</summary>
public class AccessPolicyTests
{
    private static readonly Guid CellA = Guid.NewGuid();
    private static readonly Guid CellB = Guid.NewGuid();
    private static readonly Guid DeptA = Guid.NewGuid();

    private readonly AccessPolicy _policy = new();

    #region Dữ liệu mẫu theo ma trận quyền mặc định của DataSeeder

    private static readonly string[] CanBoPerms =
    {
        AppPermissions.UsersRead, AppPermissions.BranchesRead, AppPermissions.AttachmentsRead,
        AppPermissions.AttachmentsUpload, AppPermissions.ReportsExport, AppPermissions.EvaluationsRead,
        AppPermissions.EvaluationsRegister, AppPermissions.EvaluationsSelfScore
    };

    private static AppRole Role(string code, params string[] permissions) => new()
    {
        Code = code,
        Permissions = permissions.Select(p => new Permission { Code = p }).ToList()
    };

    private static AppRole CanBo() => Role(AppRoles.CAN_BO, CanBoPerms);

    private static AppRole BiThuChiBo() =>
        Role(AppRoles.BI_THU_CHI_BO, CanBoPerms.Concat(new[] { AppPermissions.BranchesUpdate, AppPermissions.EvaluationsBranchVote }).ToArray());

    private static AppRole ToThamDinh() =>
        Role(AppRoles.TO_THAM_DINH, CanBoPerms.Concat(new[] { AppPermissions.AttachmentsDelete, AppPermissions.EvaluationsAppraise }).ToArray());

    private static AppRole DangUyCoSo() =>
        Role(AppRoles.DANG_UY_CO_SO, AppPermissions.EvaluationsRead, AppPermissions.EvaluationsApprove, AppPermissions.ReportsExport);

    private static AppRole BanThuongVu() =>
        Role(AppRoles.BAN_THUONG_VU, CanBoPerms.Concat(new[]
        {
            AppPermissions.BranchesCreate, AppPermissions.BranchesUpdate, AppPermissions.BranchesDelete,
            AppPermissions.AttachmentsDelete, AppPermissions.EvaluationsBranchVote,
            AppPermissions.EvaluationsAppraise, AppPermissions.EvaluationsApprove
        }).ToArray());

    private static AppRole Admin() =>
        Role(AppRoles.QUAN_TRI_HE_THONG,
            AppPermissions.UsersRead, AppPermissions.UsersCreate, AppPermissions.UsersUpdate, AppPermissions.UsersDelete,
            AppPermissions.BranchesRead, AppPermissions.BranchesCreate, AppPermissions.BranchesUpdate, AppPermissions.BranchesDelete,
            AppPermissions.AttachmentsRead, AppPermissions.AttachmentsUpload, AppPermissions.AttachmentsDelete,
            AppPermissions.ReportsExport, AppPermissions.RolesManage, AppPermissions.EvaluationsRead);

    private static PartyMemberProfile User(Guid? cellId, params AppRole[] roles) => new()
    {
        Id = Guid.NewGuid(),
        Username = "u" + Guid.NewGuid().ToString("N")[..8],
        PartyCellId = cellId,
        DepartmentId = DeptA,
        Roles = roles.ToList()
    };

    private static EvaluationRecord RecordOf(PartyMemberProfile member, bool approvedByAttech = true)
    {
        member.IsApprovedByAttech = approvedByAttech;
        return new EvaluationRecord
        {
            MemberId = member.Id,
            Member = member,
            PartyCellId = member.PartyCellId
        };
    }

    /// <summary>Tập người dùng đại diện cho mọi vai trò mặc định và các tổ hợp thường gặp.</summary>
    private static IEnumerable<PartyMemberProfile> AllUsers()
    {
        foreach (var cell in new Guid?[] { CellA, CellB, null })
        {
            yield return User(cell);
            yield return User(cell, CanBo());
            yield return User(cell, BiThuChiBo(), CanBo());
            yield return User(cell, ToThamDinh(), CanBo());
            yield return User(cell, DangUyCoSo());
            yield return User(cell, BanThuongVu());
            yield return User(cell, BanThuongVu(), DangUyCoSo(), CanBo());
            yield return User(cell, Admin());
        }
    }

    /// <summary>Bản sao nguyên văn luật EvaluationService.CanReadRecord trước khi tách sang AccessPolicy.</summary>
    private static bool LegacyCanReadRecord(PartyMemberProfile requester, EvaluationRecord record)
    {
        bool HasPermission(string code) => requester.Roles.SelectMany(r => r.Permissions).Any(p => p.Code == code);
        bool HasRole(string code) => requester.Roles.Any(r => r.Code == code);

        if (HasRole(AppRoles.QUAN_TRI_HE_THONG)
            || HasPermission(AppPermissions.EvaluationsAppraise)
            || (HasRole(AppRoles.BAN_THUONG_VU) && HasPermission(AppPermissions.EvaluationsApprove)))
            return true;

        if (HasRole(AppRoles.DANG_UY_CO_SO) && HasPermission(AppPermissions.EvaluationsApprove))
            return record.Member?.IsApprovedByAttech == true;

        if (HasPermission(AppPermissions.EvaluationsBranchVote))
            return requester.PartyCellId.HasValue && requester.PartyCellId == record.PartyCellId;

        return requester.Id == record.MemberId;
    }

    #endregion

    #region Hành vi cũ của CanReadRecord được giữ nguyên

    [Fact]
    public void RecordRead_MatchesLegacyCanReadRecord_ForAllRoleCombinations()
    {
        var users = AllUsers().ToList();
        var owners = AllUsers().ToList();
        var checkedCases = 0;

        foreach (var owner in owners)
        {
            foreach (var approvedByAttech in new[] { true, false })
            {
                var record = RecordOf(owner, approvedByAttech);
                foreach (var requester in users.Append(owner))
                {
                    var expected = LegacyCanReadRecord(requester, record);
                    Assert.Equal(expected, _policy.CanAccessRecord(requester, record, AccessOperation.Read));
                    Assert.Equal(expected, _policy.CanAccessRecord(requester, record, AccessOperation.Export));
                    checkedCases++;
                }
            }
        }

        Assert.True(checkedCases > 1000);
    }

    #endregion

    #region Chủ hồ sơ / cùng Chi bộ / khác Chi bộ / cấp cao

    [Fact]
    public void Owner_CanReadExportAndUpdate_ButCannotDelete()
    {
        var owner = User(CellA, CanBo());
        var record = RecordOf(owner);

        Assert.True(_policy.CanAccessRecord(owner, record, AccessOperation.Read));
        Assert.True(_policy.CanAccessRecord(owner, record, AccessOperation.Export));
        Assert.True(_policy.CanAccessRecord(owner, record, AccessOperation.Update));
        Assert.False(_policy.CanAccessRecord(owner, record, AccessOperation.Delete));
        Assert.False(_policy.CanAccessRecord(owner, record, AccessOperation.BranchReview));
        Assert.False(_policy.CanAccessRecord(owner, record, AccessOperation.Approve));
    }

    [Fact]
    public void OtherCadre_SameBranch_WithoutBranchVote_IsDenied()
    {
        var record = RecordOf(User(CellA, CanBo()));
        var colleague = User(CellA, CanBo());

        foreach (var op in Enum.GetValues<AccessOperation>())
            Assert.False(_policy.CanAccessRecord(colleague, record, op));
    }

    [Fact]
    public void BranchSecretary_SameBranch_CanReadExportAndReview_ButNotUpdate()
    {
        var record = RecordOf(User(CellA, CanBo()));
        var secretary = User(CellA, BiThuChiBo(), CanBo());

        Assert.True(_policy.CanAccessRecord(secretary, record, AccessOperation.Read));
        Assert.True(_policy.CanAccessRecord(secretary, record, AccessOperation.Export));
        Assert.True(_policy.CanAccessRecord(secretary, record, AccessOperation.BranchReview));
        Assert.False(_policy.CanAccessRecord(secretary, record, AccessOperation.Update));
        Assert.False(_policy.CanAccessRecord(secretary, record, AccessOperation.Delete));
    }

    [Fact]
    public void BranchSecretary_OtherBranch_IsDenied()
    {
        var record = RecordOf(User(CellA, CanBo()));
        var secretary = User(CellB, BiThuChiBo(), CanBo());

        foreach (var op in Enum.GetValues<AccessOperation>())
            Assert.False(_policy.CanAccessRecord(secretary, record, op));
    }

    [Fact]
    public void Appraiser_CanReadAnyBranch_ButCannotUpdateOrReview()
    {
        var record = RecordOf(User(CellA, CanBo()));
        var appraiser = User(CellB, ToThamDinh(), CanBo());

        Assert.True(_policy.CanAccessRecord(appraiser, record, AccessOperation.Read));
        Assert.True(_policy.CanAccessRecord(appraiser, record, AccessOperation.Export));
        Assert.False(_policy.CanAccessRecord(appraiser, record, AccessOperation.Update));
        Assert.False(_policy.CanAccessRecord(appraiser, record, AccessOperation.BranchReview));
        Assert.False(_policy.CanAccessRecord(appraiser, record, AccessOperation.Delete));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    public void Approve_RequiresMatchingAuthorityLevel(bool approvedByAttech, bool expectBtv, bool expectDangUy)
    {
        var record = RecordOf(User(CellA, CanBo()), approvedByAttech);
        var btv = User(CellB, BanThuongVu());
        var dangUy = User(CellB, DangUyCoSo());

        Assert.Equal(expectBtv, _policy.CanAccessRecord(btv, record, AccessOperation.Approve));
        Assert.Equal(expectDangUy, _policy.CanAccessRecord(dangUy, record, AccessOperation.Approve));
    }

    [Fact]
    public void Administrator_CanReadAnyRecord_ButCannotUpdateReviewOrApprove()
    {
        var record = RecordOf(User(CellA, CanBo()));
        var admin = User(null, Admin());

        Assert.True(_policy.CanAccessRecord(admin, record, AccessOperation.Read));
        Assert.True(_policy.CanAccessRecord(admin, record, AccessOperation.Export));
        Assert.False(_policy.CanAccessRecord(admin, record, AccessOperation.Update));
        Assert.False(_policy.CanAccessRecord(admin, record, AccessOperation.BranchReview));
        Assert.False(_policy.CanAccessRecord(admin, record, AccessOperation.Approve));
    }

    #endregion

    #region Phạm vi Chi bộ (báo cáo theo Chi bộ/kỳ)

    [Fact]
    public void BranchExport_SecretaryOnlyOwnBranch_ElevatedAnyScope()
    {
        var secretary = User(CellA, BiThuChiBo(), CanBo());
        var cadre = User(CellA, CanBo());
        var appraiser = User(CellB, ToThamDinh(), CanBo());
        var admin = User(null, Admin());

        Assert.True(_policy.CanAccessBranch(secretary, CellA, AccessOperation.Export));
        Assert.False(_policy.CanAccessBranch(secretary, CellB, AccessOperation.Export));
        Assert.False(_policy.CanAccessBranch(secretary, null, AccessOperation.Export));
        Assert.False(_policy.CanAccessBranch(cadre, CellA, AccessOperation.Export));
        Assert.True(_policy.CanAccessBranch(appraiser, CellA, AccessOperation.Export));
        Assert.True(_policy.CanAccessBranch(appraiser, null, AccessOperation.Export));
        Assert.True(_policy.CanAccessBranch(admin, null, AccessOperation.Export));
    }

    [Fact]
    public void BranchRead_KeepsLegacyRule_OwnBranchOrElevated()
    {
        var cadre = User(CellA, CanBo());
        var dangUy = User(null, DangUyCoSo());

        Assert.True(_policy.CanAccessBranch(cadre, CellA, AccessOperation.Read));
        Assert.False(_policy.CanAccessBranch(cadre, CellB, AccessOperation.Read));
        Assert.True(_policy.CanAccessBranch(dangUy, CellB, AccessOperation.Read));
    }

    [Fact]
    public void BranchReview_RequiresBranchVoteInSameBranch_EvenForElevated()
    {
        var secretary = User(CellA, BiThuChiBo(), CanBo());
        var btv = User(CellB, BanThuongVu());
        var admin = User(CellA, Admin());

        Assert.True(_policy.CanAccessBranch(secretary, CellA, AccessOperation.BranchReview));
        Assert.False(_policy.CanAccessBranch(secretary, CellB, AccessOperation.BranchReview));
        Assert.False(_policy.CanAccessBranch(btv, CellA, AccessOperation.BranchReview));
        Assert.True(_policy.CanAccessBranch(btv, CellB, AccessOperation.BranchReview));
        Assert.False(_policy.CanAccessBranch(admin, CellA, AccessOperation.BranchReview));
    }

    #endregion

    #region Hồ sơ tập thể và biên bản (giữ nguyên luật của CollectiveEvaluationService)

    [Fact]
    public void Collective_ReadAndWrite_FollowOrganizationScope()
    {
        var secretary = User(CellA, BiThuChiBo(), CanBo());
        var cadre = User(CellA, CanBo());
        var admin = User(null, Admin());

        Assert.True(_policy.CanAccessCollective(secretary, CellA, null, AccessOperation.Read));
        Assert.True(_policy.CanAccessCollective(secretary, null, DeptA, AccessOperation.Update));
        Assert.False(_policy.CanAccessCollective(secretary, CellB, null, AccessOperation.Read));
        Assert.False(_policy.CanAccessCollective(cadre, CellA, null, AccessOperation.Read));
        Assert.True(_policy.CanAccessCollective(admin, CellB, null, AccessOperation.Read));
        Assert.False(_policy.CanAccessCollective(admin, CellB, null, AccessOperation.Update));
    }

    [Fact]
    public void Meeting_ReadAndWrite_FollowBranchScope()
    {
        var secretary = User(CellA, BiThuChiBo(), CanBo());
        var appraiser = User(CellB, ToThamDinh(), CanBo());
        var admin = User(null, Admin());

        Assert.True(_policy.CanAccessMeeting(secretary, CellA, AccessOperation.Read));
        Assert.False(_policy.CanAccessMeeting(secretary, CellB, AccessOperation.Read));
        Assert.True(_policy.CanAccessMeeting(secretary, CellA, AccessOperation.Update));
        Assert.True(_policy.CanAccessMeeting(appraiser, CellA, AccessOperation.Update));
        Assert.True(_policy.CanAccessMeeting(admin, CellA, AccessOperation.Read));
        Assert.False(_policy.CanAccessMeeting(admin, CellA, AccessOperation.Update));
    }

    #endregion

    #region Hồ sơ người dùng

    [Fact]
    public void Profile_SelfOnly_UnlessAdministratorWithUsersRead()
    {
        var cadre = User(CellA, CanBo());
        var other = User(CellA, CanBo());
        var admin = User(null, Admin());
        var btv = User(CellB, BanThuongVu());

        Assert.True(_policy.CanAccessProfile(cadre, cadre, AccessOperation.Read));
        Assert.False(_policy.CanAccessProfile(cadre, other, AccessOperation.Read));
        Assert.False(_policy.CanAccessProfile(btv, other, AccessOperation.Read));
        Assert.True(_policy.CanAccessProfile(admin, other, AccessOperation.Read));
        Assert.False(_policy.CanAccessProfile(cadre, cadre, AccessOperation.Delete));
    }

    #endregion
}
