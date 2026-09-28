using CongTacDang.Application.Common.Security;
using CongTacDang.Domain.Entities;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Kiểm thử quyền trên tệp đính kèm theo đối tượng liên quan (T-31, T-42).</summary>
public class AttachmentAccessPolicyTests
{
    private static readonly Guid CellA = Guid.NewGuid();
    private static readonly Guid CellB = Guid.NewGuid();
    private static readonly AccessOperation[] ReadOps = { AccessOperation.Read, AccessOperation.Export };

    private readonly AccessPolicy _policy = new();

    private static PartyMemberProfile User(Guid? cellId, string? roleCode = null, params string[] permissions)
    {
        var user = new PartyMemberProfile { Id = Guid.NewGuid(), PartyCellId = cellId };
        if (roleCode != null)
        {
            user.Roles.Add(new AppRole
            {
                Code = roleCode,
                Permissions = permissions.Select(p => new Permission { Code = p }).ToList()
            });
        }
        return user;
    }

    private static PartyMemberProfile Cadre(Guid? cellId) =>
        User(cellId, AppRoles.CAN_BO, AppPermissions.AttachmentsRead, AppPermissions.AttachmentsUpload);

    private static PartyMemberProfile Secretary(Guid? cellId) =>
        User(cellId, AppRoles.BI_THU_CHI_BO, AppPermissions.AttachmentsRead, AppPermissions.EvaluationsBranchVote);

    private static PartyMemberProfile Appraiser(Guid? cellId) =>
        User(cellId, AppRoles.TO_THAM_DINH, AppPermissions.AttachmentsRead, AppPermissions.AttachmentsDelete, AppPermissions.EvaluationsAppraise);

    private static PartyMemberProfile Admin() =>
        User(null, AppRoles.QUAN_TRI_HE_THONG, AppPermissions.AttachmentsRead, AppPermissions.AttachmentsDelete);

    private static EvaluationRecord RecordOf(PartyMemberProfile member) => new()
    {
        MemberId = member.Id,
        Member = member,
        PartyCellId = member.PartyCellId
    };

    private static TaskAttachment FileOf(PartyMemberProfile? uploader, string formCode = "MAU01") => new()
    {
        UploadedById = uploader?.Id,
        FormCode = formCode
    };

    private static List<AttachmentRecordLink> NoLinks() => new();

    [Fact]
    public void Evidence_LinkedToRecord_FollowsRecordScope()
    {
        var owner = Cadre(CellA);
        var file = FileOf(owner);
        var links = new List<AttachmentRecordLink> { new(RecordOf(owner), ViaTask: true) };

        foreach (var op in ReadOps)
        {
            Assert.True(_policy.CanAccessAttachment(owner, file, links, false, op));
            Assert.True(_policy.CanAccessAttachment(Secretary(CellA), file, links, false, op));
            Assert.True(_policy.CanAccessAttachment(Appraiser(CellB), file, links, false, op));
            Assert.False(_policy.CanAccessAttachment(Secretary(CellB), file, links, false, op));
            Assert.False(_policy.CanAccessAttachment(Cadre(CellA), file, links, false, op));
        }
    }

    [Fact]
    public void Evidence_Delete_OnlyUploaderRecordOwnerOrAdministrator()
    {
        var owner = Cadre(CellA);
        var file = FileOf(owner);
        var links = new List<AttachmentRecordLink> { new(RecordOf(owner), ViaTask: true) };

        Assert.True(_policy.CanAccessAttachment(owner, file, links, false, AccessOperation.Delete));
        Assert.True(_policy.CanAccessAttachment(Admin(), file, links, false, AccessOperation.Delete));
        Assert.False(_policy.CanAccessAttachment(Appraiser(CellA), file, links, false, AccessOperation.Delete));
        Assert.False(_policy.CanAccessAttachment(Secretary(CellA), file, links, false, AccessOperation.Delete));
    }

    [Fact]
    public void UnlinkedFile_OnlyUploaderAndAdministrator()
    {
        var uploader = Cadre(CellA);
        var file = FileOf(uploader);

        foreach (var op in new[] { AccessOperation.Read, AccessOperation.Delete })
        {
            Assert.True(_policy.CanAccessAttachment(uploader, file, NoLinks(), false, op));
            Assert.True(_policy.CanAccessAttachment(Admin(), file, NoLinks(), false, op));
            Assert.False(_policy.CanAccessAttachment(Secretary(CellA), file, NoLinks(), false, op));
            Assert.False(_policy.CanAccessAttachment(Appraiser(CellA), file, NoLinks(), false, op));
        }
    }

    [Fact]
    public void LegacyFileWithoutUploader_OnlyAdministratorWhenUnlinked()
    {
        var file = FileOf(null);

        Assert.True(_policy.CanAccessAttachment(Admin(), file, NoLinks(), false, AccessOperation.Read));
        Assert.False(_policy.CanAccessAttachment(Cadre(CellA), file, NoLinks(), false, AccessOperation.Read));
        Assert.False(_policy.CanAccessAttachment(Appraiser(CellA), file, NoLinks(), false, AccessOperation.Read));
    }

    [Fact]
    public void GeneralDocument_UploadedByAdministrator_ReadableByEveryone_ButNotDeletable()
    {
        var admin = Admin();
        var file = FileOf(admin, AccessPolicy.GeneralFormCode);
        var cadre = Cadre(CellB);

        Assert.True(_policy.CanAccessAttachment(cadre, file, NoLinks(), uploadedByAdministrator: true, AccessOperation.Read));
        Assert.False(_policy.CanAccessAttachment(cadre, file, NoLinks(), uploadedByAdministrator: true, AccessOperation.Delete));
    }

    [Fact]
    public void GeneralDocument_UploadedByCadre_IsNotPublic()
    {
        var uploader = Cadre(CellA);
        var file = FileOf(uploader, AccessPolicy.GeneralFormCode);

        Assert.False(_policy.CanAccessAttachment(Cadre(CellA), file, NoLinks(), uploadedByAdministrator: false, AccessOperation.Read));
    }

    [Fact]
    public void LinkingSomeoneElsesFileToOwnTask_DoesNotGrantAccess()
    {
        var victim = Cadre(CellA);
        var attacker = Cadre(CellB);
        var file = FileOf(victim);
        var hijackedLinks = new List<AttachmentRecordLink> { new(RecordOf(attacker), ViaTask: true) };

        Assert.False(_policy.CanAccessAttachment(attacker, file, hijackedLinks, false, AccessOperation.Read));
        Assert.False(_policy.CanAccessAttachment(Secretary(CellB), file, hijackedLinks, false, AccessOperation.Read));
    }
}
