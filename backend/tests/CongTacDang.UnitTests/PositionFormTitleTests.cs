using CongTacDang.Application.Organization;
using CongTacDang.Domain.Entities;
using CongTacDang.Domain.Enums;
using Xunit;

namespace CongTacDang.UnitTests;

/// <summary>Chức danh in trên biểu mẫu: mẫu chức danh + đơn vị, bỏ "Quyền", bỏ "Quyền X" khi đã có "Phó X" cùng đơn vị.</summary>
public class PositionFormTitleTests
{
    private static readonly Guid CenterCell = Guid.NewGuid();
    private static readonly Guid CenterDept = Guid.NewGuid();

    private static MemberPosition Party(string name, string? formTitle, string? cellName, Guid? cellId = null, bool acting = false,
        bool primary = false, int sort = 0)
        => new()
        {
            Position = new Position { Name = name, FormTitle = formTitle, Side = PositionSide.Party, SortOrder = sort },
            PartyCellId = cellName == null ? null : cellId ?? Guid.NewGuid(),
            PartyCell = cellName == null ? null : new PartyCell { Name = cellName },
            IsActing = acting,
            IsPrimary = primary
        };

    private static MemberPosition Admin(string name, string? formTitle, string? deptName, Guid? deptId = null, bool acting = false,
        bool primary = false, int sort = 0)
        => new()
        {
            Position = new Position { Name = name, FormTitle = formTitle, Side = PositionSide.Administrative, SortOrder = sort },
            DepartmentId = deptName == null ? null : deptId ?? Guid.NewGuid(),
            Department = deptName == null ? null : new AdministrativeDepartment { Name = deptName },
            IsActing = acting,
            IsPrimary = primary
        };

    [Theory]
    [InlineData("Bí thư Chi bộ trực thuộc Đảng bộ bộ phận", "Bí thư {don_vi}", "Chi bộ Kỹ thuật tổng hợp", "Bí thư Chi bộ Kỹ thuật tổng hợp")]
    [InlineData("Trưởng phòng", "Trưởng {don_vi}", "Phòng Kế hoạch - Kinh doanh", "Trưởng Phòng Kế hoạch - Kinh doanh")]
    [InlineData("Đài trưởng", "Đài trưởng {DON_VI}", "Đài Vinh", "Đài trưởng Đài Vinh")]
    [InlineData("Giám đốc", "Giám đốc Công ty", "Công ty X", "Giám đốc Công ty")]
    [InlineData("Đảng viên", null, "Chi bộ A", "Đảng viên")]
    [InlineData("Trưởng phòng", "Trưởng {don_vi}  kiêm nhiệm", null, "Trưởng kiêm nhiệm")]
    public void FormTitle_FillsUnit(string name, string? formTitle, string? unit, string expected)
        => Assert.Equal(expected, PositionRules.FormTitle(name, formTitle, unit));

    [Fact]
    public void FormTitle_MemberPosition_UsesPartyCellOrDepartment_WithoutActingPrefix()
    {
        Assert.Equal("Đội trưởng Đội CNTT", PositionRules.FormTitle(Admin("Đội trưởng", "Đội trưởng {don_vi}", "Đội CNTT")));
        Assert.Equal("Phó Bí thư Chi bộ Đài dẫn đường Vinh", PositionRules.FormTitle(Party("Phó Bí thư Chi bộ", "Phó Bí thư {don_vi}", "Chi bộ Đài dẫn đường Vinh")));
        // Biểu mẫu không in "Quyền"; màn hình vẫn dùng DisplayName có "Quyền".
        var acting = Admin("Trưởng trung tâm", "Trưởng {don_vi}", "Trung tâm Bảo đảm kỹ thuật", acting: true);
        Assert.Equal("Trưởng Trung tâm Bảo đảm kỹ thuật", PositionRules.FormTitle(acting));
        Assert.Equal("Quyền Trưởng trung tâm", PositionRules.DisplayName(acting));
    }

    [Fact]
    public void FormTitles_DropsActingWhenDeputyOfSameUnit()
    {
        // Lâm Văn Hiển: Quyền Bí thư ĐBBP + Phó Bí thư ĐBBP cùng tổ chức → chỉ in cấp phó.
        var acting = Party("Bí thư Đảng bộ bộ phận", "Bí thư {don_vi}", "Đảng bộ bộ phận Trung tâm Bảo đảm kỹ thuật", CenterCell, acting: true, sort: 1);
        var deputy = Party("phó bí thư đảng bộ bộ phận", "Phó Bí thư {don_vi}", "Đảng bộ bộ phận Trung tâm Bảo đảm kỹ thuật", CenterCell, sort: 2);
        var center = Admin("Phó Trưởng trung tâm", "Phó Trưởng {don_vi}", "Trung tâm Bảo đảm kỹ thuật", CenterDept, sort: 3);

        var printed = PositionRules.FormTitles(new[] { acting, deputy, center });

        Assert.Equal(new[] { "Phó Bí thư Đảng bộ bộ phận Trung tâm Bảo đảm kỹ thuật", "Phó Trưởng Trung tâm Bảo đảm kỹ thuật" },
            printed.Select(p => p.Title));
        Assert.Same(acting, printed[0].Acting);
    }

    [Fact]
    public void FormTitles_KeepsActingWhenDeputyIsOtherUnit()
    {
        var acting = Admin("Trưởng trung tâm", "Trưởng {don_vi}", "Trung tâm A", acting: true);
        var deputy = Admin("Phó Trưởng trung tâm", "Phó Trưởng {don_vi}", "Trung tâm B");

        var titles = PositionRules.FormTitles(new[] { acting, deputy }).Select(p => p.Title);

        Assert.Equal(new[] { "Trưởng Trung tâm A", "Phó Trưởng Trung tâm B" }, titles);
    }

    [Fact]
    public void PrimaryFormTitle_ActingPrimaryWithDeputy_PrintsDeputy()
    {
        // Nguyễn Minh Tuấn: chức vụ chính "Quyền Trưởng trung tâm", có "Phó Trưởng trung tâm" cùng Trung tâm.
        var acting = Admin("Trưởng trung tâm", "Trưởng {don_vi}", "Trung tâm Bảo đảm kỹ thuật", CenterDept, acting: true, primary: true);
        var deputy = Admin("Phó Trưởng trung tâm", "Phó Trưởng {don_vi}", "Trung tâm Bảo đảm kỹ thuật", CenterDept);

        Assert.Equal("Phó Trưởng Trung tâm Bảo đảm kỹ thuật",
            PositionRules.PrimaryFormTitle(new[] { acting, deputy }, "Quyền Trưởng trung tâm"));
    }

    [Fact]
    public void PrimaryFormTitle_ManualProfileTitleWins_NoPositionFallsBack()
    {
        var head = Admin("Đội trưởng", "Đội trưởng {don_vi}", "Đội CNTT", primary: true);
        Assert.Equal("Đội trưởng Đội CNTT", PositionRules.PrimaryFormTitle(new[] { head }, "Đội trưởng"));
        Assert.Equal("Tổ trưởng kỹ thuật", PositionRules.PrimaryFormTitle(new[] { head }, "Tổ trưởng kỹ thuật"));
        Assert.Equal("Cán bộ", PositionRules.PrimaryFormTitle(Array.Empty<MemberPosition>(), "Cán bộ"));
    }

    [Fact]
    public void JoinFormTitles_OrdersBySideThenCatalog()
    {
        var positions = new[]
        {
            Admin("Đội trưởng", "Đội trưởng {don_vi}", "Đội CNTT", sort: 5),
            Party("Bí thư Chi bộ trực thuộc Đảng bộ bộ phận", "Bí thư {don_vi}", "Chi bộ Kỹ thuật tổng hợp", sort: 9)
        };
        Assert.Equal("Bí thư Chi bộ Kỹ thuật tổng hợp, Đội trưởng Đội CNTT", PositionRules.JoinFormTitles(positions));
    }

    [Theory]
    [InlineData("Phó Trưởng trung tâm", "Trưởng trung tâm", true)]
    [InlineData("phó  bí thư Chi bộ", "Bí thư Chi bộ", true)]
    [InlineData("Phó Giám đốc", "Giám đốc đơn vị trực thuộc", false)]
    public void IsDeputyOf_MatchesPhoPrefix(string deputy, string position, bool expected)
        => Assert.Equal(expected, PositionRules.IsDeputyOf(deputy, position));
}
